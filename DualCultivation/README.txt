DualCultivation

功能：
- 在 NPC 对话的“修行”分支追加“开始双修”选项。
- 默认要求对方好感达到 100，且对方是当前伴侣。
- 双修默认耗时 1 天，不可取消；完成后双方按对方生命、内力上限获得上限提升，并提升对方好感 10 点。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `DualCultivation.dll` 放到游戏 `Mods` 目录。

配置：
配置文件路径：`<游戏目录>\UserData\DualCultivation.cfg`

非法数值会在启动时回退为默认值并写回配置文件；缺失配置项会自动补齐。

- `EnableMod=true`：是否启用双修入口。
- `RequiredFavor=100`：开始双修需要的最低好感；小于 0 时回退默认值。
- `RelationRequirement=1`：关系限制。`0` 不检查伴侣，`1` 仅当前伴侣，`2` 当前伴侣或前任伴侣；其他值回退默认值。
- `MonthlyLimit=1`：每月可与同一 NPC 双修的次数；小于 1 时回退默认值。
- `WorkDays=1`：双修耗时天数；小于 1 时回退默认值。
- `RewardRate=0.01`：收益倍率。自己获得的生命上限 = 对方生命上限 × `RewardRate`，自己获得的内力上限 = 对方内力上限 × `RewardRate`；对方同理按玩家上限计算。实际收益还会受到 `MaxHpGain` 和 `MaxManaGain` 限制；小于等于 0 时回退默认值。
- `MaxHpGain=50`：单次最多获得的生命上限；小于 1 时回退默认值。
- `MaxManaGain=100`：单次最多获得的内力上限；小于 1 时回退默认值。
- `FavorGain=10`：完成后增加的对方好感；小于 0 时回退默认值。
- `PromptText={称呼}可愿与我共参阴阳、同调内息？|今日气息圆融，{称呼}可愿与我同修一日？|我近日修行略有所悟，想与{称呼}互证一番。|若{称呼}得闲，不妨与我闭关同修。|你我内息相近，今日同修或许会有所进境。`：确认对话文本。可用 `|` 分隔多条文本，确认时随机显示其中一条。
- `RewardPreviewText=可获得{HpGain}点生命上限和{ManaGain}点内力上限，对方可获得{TargetHpGain}点生命上限和{TargetManaGain}点内力上限。`：收益预览文本。
- `StartChoiceText=开始双修`：开始选项文本。
- `CancelChoiceText=转念一想`：取消选项文本。
- `ResultText=此番双修我获益匪浅，不知{称呼}收获如何？|今日与你气机相合，周天运转竟比平日顺畅许多。|此番同修之效远胜独自闭关，看来你我确有默契。|真气往复之间，我隐有所悟，想来{称呼}也必有所获。|这一日修行颇有进境，若日后得闲，不妨再试。`：完成后对话文本。可用 `|` 分隔多条文本，完成时随机显示其中一条。
- `ResultChoiceText=我亦获益良多`：完成后选项文本。
- `VerboseLog=false`：是否输出详细日志。

文本里的 `{称呼}` 会替换为对方对玩家的称呼；收益文本支持 `{HpGain}`、`{ManaGain}`、`{TargetHpGain}`、`{TargetManaGain}` 占位符。

发布文件：
1. DLL：`DualCultivation.dll`
2. 压缩包：`DualCultivation-v1.0.0.zip`
