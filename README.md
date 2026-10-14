# Cirno9 LongYin Mods

SaintCirno9 维护的龙胤立志传 MelonLoader 模组开发仓库。源码与游戏运行目录分离，游戏的 `Mods` 目录仅保存运行 DLL。

## 目录

| 目录 / 文件 | 用途 |
| --- | --- |
| `Mods/<模组名>/` | 独立工程、源码、玩家说明、发布资源；保留工程内部原有布局 |
| `Scripts/` | 部署、游戏 Dump 更新与验证脚本 |
| `Tools/` | IDA 导入等开发工具 |
| `GameSource/` | IL2CPP 导出与 IDA 数据库，本地保留，不提交 |
| `External/` | Unity-Mod 旧参考仓库、自定义游戏数据，本地保留，不提交 |
| `Artifacts/bin/<模组名>/<配置>/` | 构建 DLL，不提交 |
| `Artifacts/packages/` | 原有版本化发布 zip，不提交 |
| `.cache/` | 导出备份和验证日志，不提交 |
| `GamePaths.props` | 构建时读取 `.env` 的游戏路径，不含本机安装路径 |
| `.env.example` | 可提交的本机配置模板 |
| `Private/` | 私人工作资料，不提交 |
| `.env` | 游戏及工具的实际路径，不提交 |

## 构建与部署

从仓库根目录执行，构建成功后默认自动部署指定 DLL，并核对 SHA256：

```powershell
dotnet build "Mods/ForcePermissionTweaks/ForcePermissionTweaks.csproj" -c Release
```

仅构建、打包，不修改游戏：

```powershell
dotnet build "Mods/ForcePermissionTweaks/ForcePermissionTweaks.csproj" -c Release -p:DeployMods=false
```

统一部署入口（默认 Release，可指定多个名称）：

```powershell
pwsh -NoProfile -File "Scripts/Deploy-Mod.ps1" ForcePermissionTweaks
& ./Scripts/Deploy-Mod.ps1 -ModName @('MailFilter', 'HotkeyRemap')
pwsh -NoProfile -File "Scripts/Deploy-Mod.ps1" ForcePermissionTweaks -NoBuild
pwsh -NoProfile -File "Scripts/Deploy-Mod.ps1" ForcePermissionTweaks -NoBuild -WhatIf
```

`-NoBuild` 部署指定配置的已有产物，不检查它是否比源码更新。部署在游戏运行时明确报错，不终止进程。只复制所选模组 DLL，不同步源码、文档、运行依赖或其他 DLL，也不自动启用历史模组。

存在 `PackageRoot/UserData` 时，部署脚本仅补充缺失资源，保留已有用户文件。游戏的 `UserData` 作为运行配置目录，不纳入源码仓库。多数工程仍自动打包；`LongYinGameplayTweaks` 保留仅输出 DLL 的行为，BGM 发布包保留 `Mods` 和 `UserData` 结构。

首次构建前，复制模板到 `.env`，填写三个路径。实际配置文件不会提交，也不需要预先设置系统环境变量：

```powershell
Copy-Item ".env.example" ".env"
```

```dotenv
LONGYIN_GAME_ROOT=
IL2CPP_DUMPER_PATH=
IL2CPP_DUMPER_IDA_SCRIPT=
```

`LONGYIN_GAME_ROOT` 为游戏安装目录；后两项分别为 Il2CppDumper 可执行文件和官方 `ida_with_struct_py3.py` 的完整路径。值可用单引号或双引号包裹，Windows 反斜杠保留原样。不支持变量展开或行尾注释，注释放在独立行，每个键只填写一次。只构建和部署模组时仅需填写游戏路径。

也可分别给构建传 `-p:GameRootDir=...`，给脚本传 `-GameRootDir ...`。依赖直接引用该游戏下的 `MelonLoader/net6` 和 `MelonLoader/Il2CppAssemblies`。

## 开发工具

```powershell
pwsh -NoProfile -File "Scripts/Update-GameSource.ps1"
```

导出到仓库 `GameSource`。旧 Dump 和 DummyDll 先移入 `.cache/GameSource-backups`，保留原有 IDA 数据库和分析日志。IDA 导入工具位于 `Tools/Import-Il2CppDumpToIda.py`，从仓库 `GameSource` 读取导出文件。Dump 更新工具读取 `.env` 的 `IL2CPP_DUMPER_PATH`，也可传入 `-DumperPath` 覆盖；IDA 导入工具读取 `.env` 的 `IL2CPP_DUMPER_IDA_SCRIPT`。

## 验证与提交

以独立项目构建和游戏内最小回归为准；构建、打包、部署哈希通过不等于游戏内验收。仅暂存明确的相关文件，不提交 DLL、zip、obj、本机配置和导出资料。

## 仓库内容说明

本仓库不包含游戏本体、游戏导出资料和第三方音频，也不包含本机路径、凭据、用户运行配置和私人资料。

## 许可证

本仓库代码采用 [MIT License](LICENSE) 授权。

Copyright (c) 2026 SaintCirno9

你可以使用、修改和分发本仓库的代码，包括用于商业用途和闭源分发，只需在副本中保留版权声明和许可证文本。

自动打包的模组 DLL 发布包附带 `LICENSE`。直接分发 DLL（包括不自动打包的工程）时也应附上该文件。

本许可证不覆盖游戏《龙胤立志传》本体及其资源，也不覆盖任何第三方素材。

## AI 辅助生成说明

本仓库的代码由 AI 编码代理生成，作者负责需求和维护。
