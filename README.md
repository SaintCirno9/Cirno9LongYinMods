# Cirno9 LongYin Mods

SaintCirno9 维护的龙胤立志传 MelonLoader 模组开发仓库。工作区位于 `<用户目录>\Documents\Cirno9LongYinMods`，游戏的 `Mods` 目录仅保存运行 DLL。

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
| `.cache/` | 迁移校验清单、导出备份和验证日志，不提交 |
| `GamePaths.props` | 默认游戏路径 |
| `GamePaths.local.props` | 可选的本机路径覆盖，不提交 |

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

存在 `PackageRoot/UserData` 时，部署脚本仅补充缺失资源，保留已有用户文件。游戏的 `UserData` 继续作为运行配置目录，不随源码迁移。多数工程仍自动打包；`LongYinGameplayTweaks` 保留仅输出 DLL 的行为，BGM 发布包保留 `Mods` 和 `UserData` 结构。

游戏位置变化时，在本机新建以下文件：

```xml
<!-- GamePaths.local.props -->
<Project>
  <PropertyGroup>
    <GameRootDir>C:\Path\To\LongYinLiZhiZhuan\</GameRootDir>
  </PropertyGroup>
</Project>
```

也可分别给构建传 `-p:GameRootDir=...`，给脚本传 `-GameRootDir ...`。依赖直接引用该游戏下的 `MelonLoader/net6` 和 `MelonLoader/Il2CppAssemblies`。

## 开发工具

```powershell
pwsh -NoProfile -File "Scripts/Update-GameSource.ps1"
```

导出到仓库 `GameSource`。旧 Dump 和 DummyDll 先移入 `.cache/GameSource-backups`，保留原有 IDA 数据库和分析日志。IDA 导入工具位于 `Tools/Import-Il2CppDumpToIda.py`，从仓库 `GameSource` 读取导出文件。两项工具仍使用 `<Il2CppDumper目录>`。

## 验证与提交

以独立项目构建和游戏内最小回归为准；构建、打包、部署哈希通过不等于游戏内验收。仅暂存明确的相关文件，不提交 DLL、zip、obj、本机配置和导出资料。迁移保留了原 Git 历史、未提交修改及本地文件；原 Mods 下的运行 DLL 未因搬迁替换。
