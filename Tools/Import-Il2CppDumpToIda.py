import re
from pathlib import Path

import idaapi
import ida_auto
import ida_funcs
import idc


repository_path = Path(__file__).resolve().parent.parent
env_path = repository_path / ".env"
if not env_path.is_file():
    raise FileNotFoundError("找不到 .env，请复制 .env.example 并填写工具路径。")
settings = {}
for line_number, line in enumerate(env_path.read_text(encoding="utf-8-sig").splitlines(), 1):
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    match = re.fullmatch(r"\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*?)\s*", line)
    if not match:
        raise ValueError(f".env 第 {line_number} 行格式错误，应使用 KEY=VALUE。")
    key, value = match.groups()
    if key in settings:
        raise ValueError(f".env 第 {line_number} 行重复配置：{key}。")
    if value.startswith(('"', "'")):
        if len(value) < 2 or value[-1] != value[0]:
            raise ValueError(f".env 第 {line_number} 行引号不匹配：{key}。")
        value = value[1:-1]
    settings[key] = value
game_source_path = repository_path / "GameSource"
script_json_path = game_source_path / "script.json"
il2cpp_header_path = game_source_path / "il2cpp.h"
dumper_script_value = settings.get("IL2CPP_DUMPER_IDA_SCRIPT")
if not dumper_script_value:
    raise RuntimeError(".env 中未配置 IL2CPP_DUMPER_IDA_SCRIPT。")
dumper_script_path = Path(dumper_script_value)

for path in (script_json_path, il2cpp_header_path, dumper_script_path):
    if not path.is_file():
        raise FileNotFoundError(f"找不到必需文件：{path}")

# Il2CppDumper 脚本按顺序询问 script.json 和 il2cpp.h。
selected_paths = iter((str(script_json_path), str(il2cpp_header_path)))
original_ask_file = idaapi.ask_file
idaapi.ask_file = lambda *_args, **_kwargs: next(selected_paths)

try:
    ida_auto.auto_wait()
    # 官方脚本依赖 IDA 旧式脚本环境中自动导入的 idc 全局符号。
    for name in (
        "SN_NOWARN",
        "SN_NOCHECK",
        "FUNCATTR_START",
        "parse_decls",
        "parse_decl",
        "apply_type",
    ):
        globals()[name] = getattr(idc, name)

    source = dumper_script_path.read_text(encoding="utf-8")
    exec(compile(source, str(dumper_script_path), "exec"), globals(), globals())
finally:
    idaapi.ask_file = original_ask_file
