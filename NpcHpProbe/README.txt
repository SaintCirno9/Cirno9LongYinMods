NpcHpProbe

功能：
1. 这是一个专门用于排查 NPC 莫名空血、最大生命异常和日常回血链异常的测试 Mod。
2. 默认只重点监控 `ManageAIOneDay`、`FinishAIStuff`、`HeroData.GetHeroForceLvDescribe` 这 3 个关键异常点。
3. 默认只监控 NPC，不监控玩家。
4. 发现异常时，会把对应 NPC 的 `belongForceID`、门派名、`heroForceLv`、职位、地区、AI 目标等上下文一起打出来。
5. 详细的回血流程日志默认关闭，只有打开 `VerboseLog` 后才会输出。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `NpcHpProbe.dll` 放到游戏 `Mods` 目录。
3. 启动游戏后，日志会写入 MelonLoader 控制台和 `MelonLoader\Latest.log`。

配置：
1. 配置文件路径：`<游戏目录>\UserData\NpcHpProbe.cfg`
2. `TrackPlayer`
   默认值：`false`
   作用：是否连玩家一起监控。
   非法值处理：MelonPreferences 无法解析时回退到默认值并重写。
3. `VerboseLog`
   默认值：`false`
   作用：是否输出详细流程日志；关闭时默认只保留关键异常角色日志。
   非法值处理：无法解析时回退到默认值并重写。
4. `LogCountHeroData`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时记录 `CountHeroData` 详细异常。
   非法值处理：无法解析时回退到默认值并重写。
5. `LogChangeHp`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时记录 `HeroData.ChangeHp` 详细异常。
   非法值处理：无法解析时回退到默认值并重写。
6. `LogRecoverFlow`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时记录 `ManageHeroAutoRecover` 相关异常。
   非法值处理：无法解析时回退到默认值并重写。
7. `LogAiFlow`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时记录 `ManageOneAI`、`ManageAIOneDay` 等详细流程。
   非法值处理：无法解析时回退到默认值并重写。
8. `LogAiExceptions`
   默认值：`true`
   作用：是否记录 `ManageAIOneDay`、`FinishAIStuff`、`GetHeroForceLvDescribe` 的关键异常和对应角色。
   非法值处理：无法解析时回退到默认值并重写。
9. `LogRecoverRate`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时记录 `HeroData.GetRecoverRate(...)` 计算出的非正恢复率。
   非法值处理：无法解析时回退到默认值并重写。
10. `TrackUnhealedHeroes`
   默认值：`false`
   作用：是否在 `VerboseLog=true` 时追踪“没有满血但没有恢复”的 NPC。
   非法值处理：无法解析时回退到默认值并重写。
11. `UnhealedHpRatioThreshold`
   默认值：`0.95`
   作用：只跟踪生命比例不高于该阈值的角色；`0.95` 表示血量低于 95% 都会纳入观察。
   非法值处理：小于等于 0 或大于 1 时回退到 `0.95` 并重写。
12. `CaptureStackTraceOnAnomaly`
   默认值：`true`
   作用：异常时是否附带托管堆栈，便于继续定位是谁触发了这次改血。
   非法值处理：无法解析时回退到默认值并重写。

使用建议：
1. 第一次复现问题时，先保持默认配置。
2. 重点关注日志里的 `ManageAIOneDay 抛出异常`、`FinishAIStuff 抛出异常`、`GetHeroForceLvDescribe 抛出异常`。
3. 只有在需要看完整流程时，才把 `VerboseLog` 和对应的 `LogAiFlow`、`LogRecoverFlow`、`LogChangeHp` 打开。
4. 如果你怀疑玩家和 NPC 共用同一条坏链路，再把 `TrackPlayer` 改成 `true`。

发布文件：
1. DLL：`NpcHpProbe.dll`
2. 压缩包：`NpcHpProbe-v1.2.0.zip`
