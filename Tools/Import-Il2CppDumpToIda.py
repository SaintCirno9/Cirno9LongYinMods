from pathlib import Path

import idaapi
import ida_auto
import ida_funcs
import idc


repository_path = Path(__file__).resolve().parent.parent
game_source_path = repository_path / "GameSource"
script_json_path = game_source_path / "script.json"
il2cpp_header_path = game_source_path / "il2cpp.h"
dumper_script_path = Path(r"<Il2CppDumper目录>\ida_with_struct_py3.py")

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
