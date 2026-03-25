# LongYinLiZhiZhuan Mods 仓库

这个仓库汇总了 `LongYinLiZhiZhuan` 的多个本地 Mod 方案和一个工具脚本（`GameSource`），用于修改游戏行为、增强可玩性以及自动化 API 文档生成。

## 目录结构

- `GameSource/`
  - `Extract-DllApi.ps1`：从 `Assembly-CSharp.dll` 抽取 API 的脚本，生成 `api.md`、`api.html`、`api.json`、`api.compact.json`。
  - `README.md`：仅针对 `Extract-DllApi` 的使用说明。
- `LongYinCustomHeroTags/`：自定义英雄标签 Mod 源代码（`.csproj` + `Main.cs`）。
- `LongYinCustomKungfuSkill/`：自定义功法技能 Mod 源代码（`.csproj` + `Main.cs`）。
- `LongYinGameplayTweaks/`：游戏玩法调整 Mod 源代码（`.csproj` + `Main.cs`）。

还有若干编译输出 DLL：`LongYinCustomHeroTags.dll`, `LongYinCustomKungfuSkill.dll`, `LYMod.dll` 等。

## 使用说明

### 1. Extract-DllApi 文档生成（推荐）

1. 打开 PowerShell 7（建议 7.0 及以上）。
2. 进入 `GameSource` 目录：

   ```powershell
   cd "<游戏目录>\Mods\GameSource"
   ```
3. 直接运行（默认路径已设为：`MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`）：

   ```powershell
   .\Extract-DllApi.ps1
   ```
4. 生成输出将落在脚本目录（或指定 `-OutputDir`）：
   - `api.md`
   - `api.html`
   - `api.json`
   - `api.compact.json`

### 2. 编译各 Mod

每个子项目均为 `.NET` 经典项目，按以下步骤操作：

1. 进入子目录，例如：
   - `LongYinCustomHeroTags/`
   - `LongYinCustomKungfuSkill/`
   - `LongYinGameplayTweaks/`
2. 使用 `dotnet build`（或 Visual Studio 直接构建）：

   ```powershell
   dotnet build -c Release
   ```
3. 将生成的 DLL 复制到游戏 mods 目录（例如 `MelonLoader\Mods`）。

## 运行

- 根据各 MOD 设计，启动游戏后 Mod 会自动加载。
- 若需要开启更多日志，可参考 `MelonLoader` 文档及 mod 内指定的 `Main.cs` 实现。

## 贡献说明

欢迎提交 PR：

- 新增功能：请附带测试说明；
- 修复 bug：请说明复现步骤；
- 文档改进：请补全效果说明、配置项说明。

## 免责声明

- 本仓库仅作个人/社区学习实验使用。
- 请按游戏与平台许可条款使用 Mod，不建议在违反规则的环境中使用。
