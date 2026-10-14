# Repository Guidelines

## 项目结构

此仓库是独立开发工作区，不是游戏运行目录。模组工程统一位于 `Mods/<模组名>/`，保留各工程的 `Main.cs`、配置、补丁、README 和发布资源布局。仓库工具脚本位于 `Scripts/`，IDA 导入工具位于 `Tools/`；`GameSource/` 存放本地 Il2CppDumper 导出和 IDA 数据库；`External/` 存放外部参考仓库和自定义游戏数据。

游戏默认位于 `<游戏目录>`。实际游戏 `Mods` 仅放运行 DLL，不迁移或清理已有 DLL。

## 构建与部署

优先按单个模组构建：

```powershell
dotnet build "Mods/ForcePermissionTweaks/ForcePermissionTweaks.csproj" -c Release
dotnet build "Mods/MailFilter/MailFilter.csproj" -c Release -p:DeployMods=false
pwsh -NoProfile -File "Scripts/Deploy-Mod.ps1" MailFilter
```

`Directory.Build.props/targets` 统一游戏路径、依赖校验和部署入口。构建 DLL 位于 `Artifacts/bin/<模组名>/<配置>/`，版本化 zip 位于 `Artifacts/packages/`；构建默认自动部署，`-p:DeployMods=false` 仅构建。部署只复制所选 DLL，校验 SHA256；游戏运行时拒绝部署，不强杀进程。附带 `PackageRoot/UserData` 仅补充缺失资源，不覆盖用户配置。`LongYinGameplayTweaks` 保留不打包 zip 的行为。

路径在 `GamePaths.props` 定义，可用忽略的 `GamePaths.local.props` 或参数覆盖。运行配置继续位于游戏 `UserData`，不纳入源码迁移。

## 风格与范围

C# 使用 `net6.0`、4 空格和 Allman 大括号。类型、方法、属性使用 PascalCase，局部变量和参数使用 camelCase。Harmony 补丁通常按 `类型_方法_Patch` 命名。注释和文档使用简体中文，所有作者字段使用 `SaintCirno9`。模组版本以 `Main.cs` 中 `MelonInfo` 为准。仅做请求范围内修改，不顺手重构，不隐藏错误。分析或审查时不修改源码。

## 验证与提交

至少完成单项目构建；影响配置、UI、权限或数据时还需游戏内最小回归及日志检查。静态构建、发布包、部署哈希和实机验收分别报告。提交采用简洁的 Conventional Commits，说明行为和目标；按模组或职责分类，仅暂存明确文件，不使用 `git add .` 或 `git add -A`。不提交 `Artifacts`、DLL、zip、obj、本机配置、GameSource、External 和缓存。

## 工具与 Dump

Windows 使用 PowerShell 7 `pwsh`。项目搜索优先 `rg`，显式提供路径，正则用单引号，字面量优先 `rg -F`。主 Dump 位于 `GameSource/dump.cs`。进一步查看原生实现、调用链和交叉引用时使用 IDA。`Scripts/Update-GameSource.ps1` 更新导出并备份旧产物，`Tools/Import-Il2CppDumpToIda.py` 导入最新 Dump；不在更新 Dump 时删除 IDA 数据库。
