# Repository Guidelines

## 项目结构与模块组织
此仓库同时是 `MelonLoader` 的实际 `Mods` 目录和开发工作区。根目录下的 `*.dll` 会被游戏直接加载，不要因为它们像构建产物就移动或清理。各模组通常使用“同名目录 + 根目录 DLL”结构，例如 `ForcePermissionTweaks/` 存放源码、`ForcePermissionTweaks.dll` 存放运行产物。典型项目目录包含 `Main.cs`、`*.csproj`、`README.txt`，部分模组还会拆分 `*Config.cs`、补丁文件或资源文件。`GameSource/` 存放 Il2CppDumper 导出的 `dump.cs`、`il2cpp.h`、`script.json`、`stringliteral.json` 和 `DummyDll/`，`Unity-Mod/` 主要是旧样例和参考工程。

## 构建、测试与开发命令
优先按单个模组构建，不要假设仓库存在统一解决方案。

```powershell
dotnet build ".\ForcePermissionTweaks\ForcePermissionTweaks.csproj"
dotnet build ".\MailFilter\MailFilter.csproj"
```

大多数 `.csproj` 会把 DLL 直接输出到仓库根目录，并在项目目录下生成版本化 zip；`LongYinGameplayTweaks` 目前只输出 DLL。仓库没有统一自动化测试工程，默认验证方式是：单项目构建成功，再进行游戏内最小回归。

## 代码风格与命名约定
C# 项目统一为 `net6.0`，现有代码使用 4 空格缩进和 Allman 大括号风格。类型、方法、属性使用 `PascalCase`，局部变量与参数使用 `camelCase`。Harmony 补丁类通常按 `类型_方法_Patch` 命名，配置文件常见 `Main.Config.cs` 或 `*Config.cs`。注释、说明文字和文档字符串使用简体中文；所有作者字段必须写为 `SaintCirno9`，例如 `MelonInfo(..., "SaintCirno9", ...)`。模组代码版本号以 `Main.cs` 中的 `MelonInfo` 那一行作为准。仅做手术式修改，不顺手重构无关代码。

## 测试指南
若改动影响配置解析、按钮权限、UI 注入或数据覆盖，至少完成两步：`dotnet build` 成功；在游戏内确认对应入口可加载、功能可触发、日志无新增报错。当前没有覆盖率门槛，也没有 `xUnit`、`NUnit` 或 `MSTest` 测试项目。

## 提交与 Pull Request 规范
Git 历史采用简洁的 Conventional Commits，常见前缀为 `feat:` 和 `fix:`，标题通常直接描述具体行为，例如 `fix: 兼容高势力秘籍角标回退`。PR 应说明影响的模组目录、玩家可见行为变化、构建结果，以及是否更新了根目录对应 DLL。涉及 UI 变更时附截图；涉及配置或数据文件时列出新增路径。不要提交临时目录、`obj/`、`.dotnet/` 或自动打包 zip。

## PowerShell 约定
在 PowerShell 中执行搜索或批量命令时，始终显式提供路径并使用双引号，例如 `rg "MelonInfo" ".\ForcePermissionTweaks"`。这能避免误扫整个游戏目录，也符合仓库现有工作流。

## 可用 Dump 文件
- 游戏导出的主 Dump 文件位于仓库相对路径：`.\GameSource\dump.cs`
- 需要查找游戏类型、方法签名、字段、枚举或 IL2CPP 导出结构时，优先查看该文件。

## 源码查看方式
- 如需进一步分析实现逻辑、调用关系、交叉引用或原生层细节，可连接 IDA 进行查看。
- 在 `dump.cs` 无法直接确认具体行为时，应结合 IDA 继续定位函数入口、调用链和相关数据结构。
