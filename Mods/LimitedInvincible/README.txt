LimitedInvincible

功能：
- 将战斗中的“无敌”效果改为有限抵挡次数，而不是按固定回合或持续时间生效。
- 原版无敌 buff 的 `time` 参数会按正整数层数读取，再换算成可抵挡次数。
- 当前换算规则：
- 按 `Ceiling(time)` 作为基础层数。
- 最终可抵挡次数 = 基础层数 × `InvincibleHitsPerLayer`。
- 无敌每次成功抵挡伤害时消耗 1 次，耗尽后会主动清除原版无敌 buff。
- 如果原版无敌因时间结束而自然消失，记录的抵挡次数也会同步清掉。
- AI 在技能规划时会忽略目标的无敌状态，不再因为目标有无敌而直接放弃攻击。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `LimitedInvincible.dll` 放到游戏 `Mods` 目录。

配置：
1. 配置文件路径：`<游戏目录>\UserData\LimitedInvincible.cfg`
2. 如果配置文件不存在，mod 启动时会自动生成默认配置。
3. 当前支持的配置项：
- `EnableLimitedInvincible=true`
- 控制是否启用“无敌改为抵挡次数”逻辑。
- `InvincibleHitsPerLayer=3`
- 控制每层无敌可抵挡多少次伤害。
- 该项必须是大于 `0` 的整数；如果填写非法值，mod 会回退到当前有效配置并重写配置文件。
- `EnableAiIgnoreInvincible=true`
- 控制 AI 规划时是否忽略目标无敌。
- `VerboseLog=false`
- 控制是否输出详细控制台日志。

发布文件：
1. DLL：`LimitedInvincible.dll`
2. 压缩包：`LimitedInvincible-v1.0.0.zip`
