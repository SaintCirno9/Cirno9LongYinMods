# Changelog

## 2.4.0

- 首次生成的 `Data\000\PortraitLayout.csv` 现在只包含表头，不再默认启用示例人物的立绘替换。
- 默认示例人物更新为 `沈.浣溪`，并同步更新 `SpeHeroData.csv` 与 `SpeHeroFaceData.csv` 示例内容。
- 自动生成示例配置时，日志会明确提示 `PortraitLayout.csv` 是空立绘布局表。
- 立绘场景识别改为按父级节点名称判断，减少 UI 路径变化导致详情、对话、卡片和战斗立绘场景识别失效的情况。

## 2.3.0

- `SpeHeroData.csv` 的 `关系设定` 现在支持填写 `夫妻:id`，例如 `夫妻:172`。
- 夫妻关系只需要在其中一名角色的配置里填写一次，Mod 会自动让双方互相成为配偶。
- `夫妻:id` 里的 `id` 仍填写原配置 `id`，新增特殊人物不需要手动换算运行时 `id`。
- 同一名角色只能配置一名配偶；目标不存在、指向自己或多配偶冲突时会跳过并在日志中提示。
- 如果已经存在 `Data\000` 目录，首次启动不再自动生成示例 CSV，避免干扰玩家已经准备好的空数据包目录。

## 2.2.1

- 新增特殊人物现在会参考原版最大特殊人物 `id`，由 Mod 自动分配连续运行时 `id`，避免 `SpeHeroDataBase` 按列表下标访问时失效。
- `SpeHeroFaceData.csv` 会复用同一份源配置 `id` 到运行时 `id` 映射，保证新增人物脸数据与人物数据一致。
- 自定义立绘仍按原配置 `id` 读取图片和布局，例如配置 `id=171` 的人物运行时被分配为 `171` 之后的连续 `id` 时，仍读取 `PortraitLayout.csv` 的 `171` 行和 `Portraits\171.png`。
- `PortraitLayout.csv` 的 `名字` 列现在参与旧存档兼容匹配；若该列为空，会从同 `id` 的 `SpeHeroData.csv` 记录补齐名字。
- 直接按 `id` 命中立绘布局时，会校验布局名字与运行时人物名是否一致；不一致时不再误用该 `id` 的立绘。
- 按名字回退到原配置 `id` 时，会忽略点号、间隔符和空格差异，例如 `姜.映潭` 可匹配运行时的 `姜映潭`。
- 按名字回退只会在运行时 `id` 大于原版特殊人物最大 `id` 时启用，避免原版人物或当前档人物因同名被误映射。
- 立绘缓存签名现在同时包含 `PortraitLayout.csv` 和 `SpeHeroData.csv`，修改名字来源后会自动刷新匹配缓存。
- 原版表导出现在会优先写出 `TextAsset.bytes` 原始字节，避免 GBK 编码的 `Vanilla_SpeHeroData.csv` 和 `Vanilla_SpeHeroFaceData.csv` 被错误解码成乱码。

## 2.2.0

- 立绘替换与 NPC 数据注入解耦：自定义立绘现在只由 `PortraitLayout.csv` 驱动，不再依赖 `SpeHeroData.csv` 或 `SpeHeroFaceData.csv`。
- `PortraitLayout.csv` 新增 `名字` 备注列，新模板表头为 `id,名字,...`，运行时仍只按 `id` 匹配。
- 旧版缺少 `名字` 列的 `PortraitLayout.csv` 会在启动时自动补齐，新增列留空，原有布局数值按表头保留。
- 立绘图片来源改为跟随最终生效的 `PortraitLayout.csv` 所在目录。
- 新增 `EnableVerboseLog=false` 配置项，用于输出立绘路径缓存、Sprite 缓存命中、重载和图片路径等调试日志。
- `已应用 NPC 自定义立绘` 日志移动到 verbose 模式，默认不再输出到普通日志。
