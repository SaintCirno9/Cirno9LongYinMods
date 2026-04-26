HotkeyRemap

功能：
1. 为《龙胤立志传》提供独立的热键重映射 Mod。
2. 启动时自动生成并校验 `UserData\HotkeyRemap.cfg`。
3. 已接入以下热键映射：
4. `QuickSave`：快速保存
5. `QuickLoad`：快速读档
6. `MissionToggle`：任务开关
7. `BigMapRest`：大地图休息
8. `PlotAdvance`：剧情推进
9. `HeroDetail`：主场景人物详情
10. `LeaveCity`：离开城市 / 返回大地图
11. `ShowMap`：显示地图
12. `BattleAutoToggle`：战斗自动
13. `BattleActionFastW/E/S/D/X/C`：战斗动作快捷键
14. `StudyUp/StudyDown/StudyLeft/StudyRight`：修炼方向键
15. `UiSubmit/UiCancel`：界面确认/取消
16. 设置菜单内已提供 `热键设置` 入口，可在游戏内直接修改键位并保存到配置文件。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `HotkeyRemap.dll` 放到游戏 `Mods` 目录。
3. 首次启动后会自动生成 `UserData\HotkeyRemap.cfg`。

游戏内使用：
1. 进入游戏设置菜单。
2. 点击设置页中的 `热键 / 设置` 入口。
3. 在热键面板中点击右侧键位按钮。
4. 按下新的按键后会立即写回 `UserData\HotkeyRemap.cfg`。
5. 若要输出定位和注入细节日志，可将 `VerboseLog=true`。

配置文件：
1. 路径：`<游戏目录>\UserData\HotkeyRemap.cfg`
2. 所有按键均使用 Unity `KeyCode` 名称。
3. 常见别名支持：`Enter`、`Esc`、`PgUp`、`PgDn`。
4. 缺失项、未知项或非法值会回退默认值并重写配置文件。

默认配置：
1. `EnableGlobalRemap=true`
2. `EnablePlotAdvanceRemap=true`
3. `EnableUiNavigationRemap=false`
4. `VerboseLog=false`
5. `QuickSave=F5`
6. `QuickLoad=F9`
7. `MissionToggle=Tab`
8. `BigMapRest=Space`
9. `PlotAdvance=Space`
10. `HeroDetail=Q`
11. `LeaveCity=E`
12. `ShowMap=M`
13. `BattleAutoToggle=F2`
14. `BattleActionFastW=W`
15. `BattleActionFastE=E`
16. `BattleActionFastS=S`
17. `BattleActionFastD=D`
18. `BattleActionFastX=X`
19. `BattleActionFastC=C`
20. `StudyUp=W`
21. `StudyDown=S`
22. `StudyLeft=A`
23. `StudyRight=D`
24. `UiSubmit=Return`
25. `UiCancel=Escape`

实现说明：
1. `HeroDetail=Q` 时保留原版人物详情逻辑，不由 mod 接管。
2. 只有当 `HeroDetail` 改成非 `Q` 键位时，才会由 `HudController.Update` 和原生补丁接管。
3. `LeaveCity` 使用 `HudController.Update` 接管，并直接调用 `AreaController.ReturnBigMapButtonClicked()`，保留原版离城剧情和返回大地图逻辑。
4. `ShowMap` 使用 `HudController.Update` 接管，并调用 `QuickTravelUIController.ShowQuickTravelUIShowType()` 显示地图界面。
5. `BattleActionFastW/E/S/D/X/C` 会重写战斗动作快捷键列表。
6. 设置菜单入口与热键面板为运行时注入，不修改原游戏资源文件。

发布文件：
1. DLL：`HotkeyRemap.dll`
2. 压缩包：`HotkeyRemap-v1.2.0.zip`
