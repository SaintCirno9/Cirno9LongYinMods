# LongYinLiZhiZhuan Mods 仓库

这个目录既是 `MelonLoader` 实际读取的 `Mods` 目录，也是当前各个 Mod 的本地开发工作区。

因此仓库里会同时存在两类内容：

- 根目录下可被 `MelonLoader` 直接加载的 `.dll`
- 各个同名子目录下的源码、文档、打包脚本与发布包

这不是误放文件，而是当前仓库的有意布局。

## 顶层目录说明

- `GameSource/`
  - 游戏 API 抽取脚本、逆向参考资料与旧工程留档。
  - 其中 `Extract-DllApi.ps1` 用于从 `Assembly-CSharp.dll` 生成接口文档。
- `LongYinAttrReinforceMod/`
  - 六维属性额外换算生命与内力的 Mod 工程。
- `LongYinCustomBgm/`
  - 自定义 BGM Mod 工程。
  - 当前带有 `PackageRoot`，发布包会同时包含 `Mods` 与 `UserData` 所需内容。
- `LongYinCustomEvent/`
  - 世界事件与随机事件外部注入 Mod 工程。
- `LongYinCustomHeroTags/`
  - 英雄标签覆盖与扩展 Mod 工程。
- `LongYinCustomKungfuSkill/`
  - 功法属性与图标自定义 Mod 工程。
- `LongYinCustomSpeHeroData/`
  - 特殊角色数据自定义 Mod 工程。
- `LongYinFontReplacement/`
  - 运行时字体替换 Mod 工程。
- `LongYinGameplayTweaks/`
  - 玩法调整类 Mod 工程。
- `LongYinPlayerSpeSkeleton/`
  - 玩家特殊立绘替换 Mod 工程。
- `NoGenderLimit/`
  - 七夕与互动性别限制移除 Mod 工程。

## 根目录 DLL 说明

根目录下的 `.dll` 供 `MelonLoader` 直接读取。

这类文件包括但不限于：

- 本仓库内项目构建得到的 DLL，例如 `LongYinCustomEvent.dll`
- 外部来源或历史保留的 DLL，例如 `LYMod.dll`

不要因为“看起来像构建产物”就把这些 DLL 挪走；对当前仓库来说，它们本身就是运行目录的一部分。

## 开发约定

### 1. 目录定位

- 根目录承担运行职责，保持可直接被 `MelonLoader` 加载。
- 每个 Mod 子目录承担源码与项目文档职责。
- 与游戏运行相关的 `UserData` 不在本目录下，而是在 `Mods` 同级目录。

### 2. 构建输出

- 大多数项目会把 DLL 直接输出到仓库根目录，也就是当前 `Mods` 目录。
- 多数项目在构建后还会自动在各自项目目录生成带版本号的 zip 包。
- `LongYinGameplayTweaks` 当前只输出 DLL，不参与自动 zip 打包。
- `LongYinCustomBgm` 的打包规则包含额外 `UserData` 资源，和其他仅 DLL 型项目不同。

### 3. README 分工

- 各项目目录内的 README 以玩家安装、配置、使用说明为主。
- 仓库根 README 负责说明整体结构、开发约定和工具入口。

### 4. 临时文件

- `obj/`、`.dotnet/`、`.vscode/`、临时导出物等内容不作为正式发布内容。
- 是否保留这些目录，以实际开发需要为准，但不应影响玩家阅读项目 README。

## 常用开发入口

### Extract-DllApi

`GameSource/Extract-DllApi.ps1` 用于从游戏 DLL 生成 API 文档。

常见输出包括：

- `api.md`
- `api.html`
- `api.json`
- `api.compact.json`

具体参数和使用方式请查看 [GameSource/README.md](<游戏目录>/Mods/GameSource/README.md)。

### 项目构建

各子项目通常都提供独立的 `.csproj` 和 `.vscode/tasks.json`。

目前仓库内主流规则是：

- 构建目标为 `.NET 6`
- DLL 输出到当前 `Mods` 根目录
- 部分项目自动生成版本号 zip

如果需要调整统一规则，优先从各项目 `.csproj` 中的输出路径和 `PackageBuildOutput` 目标入手。

## 维护建议

- 项目 README 面向玩家时，避免写过多开发细节。
- 仓库级规则优先写在本文件，避免信息分散到每个项目中。
- 若新增 Mod，建议保持“根目录 DLL + 同名项目目录”的现有结构，减少运行与维护成本。

## 免责声明

- 本仓库仅作个人/社区学习实验使用。
- 请按游戏与平台许可条款使用 Mod，不建议在违反规则的环境中使用。
