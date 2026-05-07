《龙胤立志传》特殊角色数据与自定义立绘 Mod (LongYinCustomSpeHeroData)

【安装说明】
1. 本 Mod 依赖 MelonLoader 运行环境。
2. 将 `LongYinCustomSpeHeroData.dll` 放入游戏目录下的 `Mods` 文件夹。

【使用说明】
1. 第一次启动游戏后，Mod 会在 `UserData\LongYinCustomSpeHeroData` 下生成以下内容：
   - `LongYinCustomSpeHeroData.cfg`
   - `Vanilla_SpeHeroData.csv`
   - `Vanilla_SpeHeroFaceData.csv`
   - `Data`
2. 新版推荐把你的配置放进 `Data\数字目录`，例如：
   - `UserData\LongYinCustomSpeHeroData\Data\000\SpeHeroData.csv`
   - `UserData\LongYinCustomSpeHeroData\Data\000\SpeHeroFaceData.csv`
   - `UserData\LongYinCustomSpeHeroData\Data\000\PortraitLayout.csv`
   - `UserData\LongYinCustomSpeHeroData\Data\000\Portraits`
3. 如果第一次启动时没有检测到任何配置，Mod 会自动生成：
   - `Data\000\SpeHeroData.csv`
   - `Data\000\SpeHeroFaceData.csv`
   - `Data\000\PortraitLayout.csv`
   - `Data\000\Portraits`
4. 为兼容旧版本，Mod 仍会读取根目录下已有的：
   - `SpeHeroData.csv`
   - `SpeHeroFaceData.csv`
   - `PortraitLayout.csv`
   - `Portraits`
   但新版不会再自动创建这些旧路径，建议迁移到 `Data\数字目录`。
5. `LongYinCustomSpeHeroData.cfg` 用于控制是否允许所有特殊人物可招募：
   - 默认值为 `EnableRecruitAllSpeHeroes=true`
   - 设置为 `true` 时，会在特殊人物详情界面和实际招募入口中自动放开招募
   - 设置为 `false` 时，保持游戏原版招募逻辑
   - `EnableVerboseLog=false` 用于控制是否输出立绘缓存命中、重载和路径等调试日志
6. `SpeHeroData.csv` 对应 `GameData/SpeHeroData`。
   - `id` 小于等于原版最大特殊人物 `id` 时，视为覆盖原版人物
   - `id` 大于原版最大特殊人物 `id` 时，视为新增人物；Mod 会自动分配连续运行时 `id`
   - 新增人物的 `SpeHeroFaceData.csv`、`PortraitLayout.csv` 和图片文件名仍然填写原配置 `id`
7. `SpeHeroFaceData.csv` 对应 `GameData/SpeHeroFaceData`。
8. 多个 `Data\数字目录` 会按数字从小到大加载，例如 `000 -> 010 -> 100`。
9. 如果多个配置文件里出现相同 `id`：
   - 后加载的配置会覆盖先加载的配置
   - 立绘图片会跟随 `PortraitLayout.csv` 中最终生效的那份配置来源
10. 自定义 NPC 立绘用法：
   - 把图片放进对应配置目录的 `Portraits` 文件夹
   - 文件名必须和特殊人物 `id` 一致，例如 `171.png`
   - 支持 `png / jpg / jpeg`
   - 如果 `Data\100\Portraits\171.png` 不存在，Mod 会继续尝试根目录 `Portraits\171.png`
   - 只有 `PortraitLayout.csv` 中实际出现过的特殊人物 `id`，Mod 才会尝试读取对应图片
11. `PortraitLayout.csv` 用于给每个 NPC 单独配置图片缩放和偏移，表头如下：
   - `id,名字,通用缩放,通用偏移X,通用偏移Y,详情缩放,详情偏移X,详情偏移Y,卡片缩放,卡片偏移X,卡片偏移Y,战斗卡片缩放,战斗卡片偏移X,战斗卡片偏移Y,战斗立绘缩放,战斗立绘偏移X,战斗立绘偏移Y`
   - `id` 填写原配置 `id`，用于匹配 `SpeHeroData.csv`、`SpeHeroFaceData.csv`、图片文件名和布局来源
   - `名字` 同时用于旧存档兼容：运行时 `id` 命中但名字不一致时，会继续按名字查找原配置 `id`
   - 按名字查找原配置 `id` 只会在运行时 `id` 大于原版特殊人物最大 `id` 时启用
   - 旧版缺少 `名字` 列的表头会在启动时自动补齐，新增列留空
   - `通用*` 是立绘基础配置
   - `详情*` 是人物详情大立绘在通用配置上的额外倍率和偏移
   - `卡片*` 是卡片/列表小立绘在通用配置上的额外倍率和偏移
   - `战斗卡片*` 是战斗队伍卡片在通用配置上的额外倍率和偏移
   - `战斗立绘*` 是战斗当前立绘在通用配置上的额外倍率和偏移
   - 最终缩放 = 通用缩放 * 场景缩放；最终偏移 = 通用偏移 + 场景偏移
   - 例如通用缩放 `1.3`、卡片缩放 `1.2` 时，卡片最终缩放为 `1.56`
   - 留空或旧 CSV 缺少新列时，缩放按 1，偏移按 0 处理
12. 只要该 NPC 被游戏通过 `HeroData.SetSkeletonGraphic(...)` 显示立绘，Mod 就会优先显示你的外部图片；找不到图片时自动回退原版立绘。
13. 修改保存后重新进入游戏，新的 CSV、布局参数和图片即可生效。

【注意事项】
1. 建议使用 VS Code 或 Notepad++ 编辑 CSV，避免 Excel 改坏编码或分隔符。
2. Mod 已兼容 UTF-8 与 GBK 读取，但仍建议统一保存为 UTF-8。
3. 如果某张图片读取失败或路径不存在，MelonLoader 控制台会输出警告，但不会阻断整个 Mod。
