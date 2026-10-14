PotentialCapUnlock

功能：
1. 将人物六维属性潜力上限从原版 120 提升到可配置值。
2. 将人物武学潜力上限从原版 120 提升到可配置值。
3. 将人物技艺潜力上限从原版 100 提升到可配置值。
4. 同时修正人物详情页和开局捏人界面的属性条显示，避免高上限时进度条无限拉长溢出 UI。

安装：
1. 确保游戏已经安装 MelonLoader。
2. 将 `PotentialCapUnlock.dll` 放到游戏根目录的 `Mods` 文件夹。
3. 首次启动游戏后，mod 会自动生成配置文件。

配置：
1. 配置文件路径：`UserData\PotentialCapUnlock.cfg`
2. 如果配置文件不存在，mod 启动后会自动生成默认配置。
3. 配置项如下：
- `EnableCapOverride=true`
- `AttriCap=1000`
- `FightSkillCap=1000`
- `LivingSkillCap=1000`
- `VerboseLog=false`
4. `EnableCapOverride` 控制是否开启硬上限修改，默认值 `true`；设为 `false` 后会恢复原版硬上限（六维 `120`、武学 `120`、技艺 `100`）。
5. `AttriCap` 控制六维属性潜力上限，必须为大于 0 的整数，默认值 `1000`。
6. `FightSkillCap` 控制武学潜力上限，必须为大于 0 的整数，默认值 `1000`。
7. `LivingSkillCap` 控制技艺潜力上限，必须为大于 0 的整数，默认值 `1000`。
8. `VerboseLog` 控制是否输出详细日志，默认值 `false`。
9. 任何缺失项、未知项或非法值都会回退到当前有效值或默认值，并自动重写配置文件。
