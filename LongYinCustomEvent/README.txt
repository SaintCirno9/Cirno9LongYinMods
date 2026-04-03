《龙胤立志传》世界事件 & 随机事件外部注入 Mod (LongYinCustomEvent)

【安装说明】
1. 本 Mod 依赖 MelonLoader 运行环境。
2. 将 `LongYinCustomEvent.dll` 放入游戏目录下的 `Mods` 文件夹。

【当前配置方式】
1. 第一次启动游戏后，Mod 会在 `UserData\LongYinCustomEvent` 下创建配置目录。
2. Mod 会在 `UserData\LongYinCustomEvent\WorldEventTemplates` 下为所有原版世界事件重建模板文件。
3. Mod 会在 `UserData\LongYinCustomEvent\RandomEventTemplates` 下为所有原版随机事件重建模板文件。
4. 自定义世界事件配置文件：`WorldEvent_Custom.json`
   - 顶层必须是一个对象，核心字段是 `events`。
   - 每条事件支持 `copyFromEventName`，先复制一个原版世界事件，再覆盖需要修改的字段。
   - 常用可改内容包括时间、刷新、描述文本、剧情页、选项、消耗、触发函数等。
5. 自定义随机事件配置文件：`RandomEvent_Custom.json`
   - 顶层必须是一个对象，核心字段是 `events`。
   - 每条事件支持 `copyFromEventName`，先复制一个原版随机事件，再覆盖需要修改的字段。
   - 随机事件只有 `eventData` 层，没有世界事件的外层时间/重复等字段。

【模板目录说明】
1. `WorldEventTemplates` 目录下每个 JSON 文件对应一个原版世界事件模板。
2. `RandomEventTemplates` 目录下每个 JSON 文件对应一个原版随机事件模板。
3. 文件名格式为 `序号-事件名.json`，方便直接按事件名查找。
4. 模板文件每次启动游戏时自动重建，目录内容会被覆盖刷新。

【配置说明】
1. `copyFromEventName`：
   - 推荐填写一个原版事件名，例如世界事件 `比武大赛`，随机事件 `运镖队伍`。
   - 如果没写，Mod 会默认复制第一条原版事件作为基础。
2. 数组字段：
   - 不写字段：保留基础事件原值。
   - 写成 `[]`：主动清空该列表。
3. 数值枚举字段：
   - 例如 `repeatType`、`eventRandomArea`、`plotSource`、`plotTarget`、`heroFaceHightLightType`。
   - 按游戏内部数值填写，可参考模板文件中的原版数值。

【使用建议】
1. 最稳妥的做法是先在对应模板目录里找到行为接近的原版事件，复制其内容到自定义配置文件中修改。
2. 如果只想改文案、时间、选项、回调函数，现有格式已经足够维护。
3. 建议使用 VS Code 或 Notepad++ 编辑 JSON，并统一保存为 UTF-8。

【发布文件】
1. DLL：`LongYinCustomEvent.dll`
2. 压缩包：`LongYinCustomEvent-v1.2.0.zip`
