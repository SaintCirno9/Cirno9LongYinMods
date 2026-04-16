ForcePermissionTweaks

功能：
- 当前版本已实现“建筑管理按钮”权限覆盖。
- 当前版本也已实现“藏经阁相关按钮”权限覆盖。
- 可分别修改按钮的显示条件和可点击所需职位。

安装：
1. 确保游戏已安装 MelonLoader。
2. 将 `ForcePermissionTweaks.dll` 放到游戏 `Mods` 目录。

配置：
1. 首次启动后会生成配置文件：`<游戏目录>\UserData\ForcePermissionTweaks.cfg`
2. 当前默认配置为：
3. `EnableBuildButtonOverride=true`
4. `BuildButtonVisibilityMode=PlayerForceAndServantForce`
5. `BuildButtonMinimumRole=亲传弟子`
6. `EnableSpeBookStorageOverride=true`
7. `SpeBookStorageVisibilityMode=PlayerForceAndServantForce`
8. `SpeBookStorageMinimumRole=副掌门`
9. `IgnoreCenterBuildingDiscipleCountForUpgrade=true`
10. `VerboseLog=false`

配置说明：
1. `EnableBuildButtonOverride`
2. 是否启用建筑管理按钮权限覆盖；设为 `false` 时恢复原版逻辑。
3. `BuildButtonVisibilityMode`
4. 合法值：`Original`、`PlayerForceAreaOnly`、`PlayerForceAndServantForce`、`PlayerForceServantAndAllyForce`、`Always`
5. `Original` 表示不覆盖原版显示条件。
6. `PlayerForceAreaOnly` 表示只有当前区域属于玩家势力时才显示。
7. `PlayerForceAndServantForce` 表示当前区域属于本门派或本门派附庸时显示。
8. `PlayerForceServantAndAllyForce` 表示当前区域属于本门派、附庸或盟友时显示。
9. `Always` 表示始终显示建筑管理按钮。
10. `BuildButtonMinimumRole`
11. 合法值：`Original`、`外门弟子`、`入门弟子`、`正式弟子`、`亲传弟子`、`长老`、`副掌门`、`掌门`
12. `Original` 表示不覆盖原版可点击条件。
13. `掌门` 按主角 `isLeader` 判定，其余职位按职位等级“及以上”判定。
14. `EnableSpeBookStorageOverride`
15. 是否启用藏经阁相关按钮的权限覆盖；设为 `false` 时恢复原版逻辑。
16. `SpeBookStorageVisibilityMode`
17. 合法值与 `BuildButtonVisibilityMode` 相同。
18. `Original` 表示沿用原版显示条件。
19. 其他模式会按当前区域所属门派关系覆盖显示条件。
20. `SpeBookStorageMinimumRole`
21. 合法值与 `BuildButtonMinimumRole` 相同。
22. `Original` 表示沿用原版可点击条件。
23. `IgnoreCenterBuildingDiscipleCountForUpgrade`
24. 是否忽略“门派正厅”升级时对弟子数的要求。
25. 同时会隐藏升级描述文本里对应的弟子数不足提示。
26. 仅绕过正厅升级所需弟子数，其他资源、等级和施工状态等原版限制仍然保留。
27. `VerboseLog`
28. 是否输出详细日志。
29. 配置项缺失、非法值或未知项会自动回退默认值并重写配置文件。

原版参考：
1. 建筑管理按钮原版显示条件为“当前区域属于玩家势力”。
2. 当前 Mod 默认显示条件已改为“本门派或附庸门派区域”。
3. 建筑管理按钮原版可点击条件为“亲传弟子及以上”，不是“仅掌门”。
4. 藏经阁“管理藏书”当前默认显示条件为“本门派或附庸门派区域”。
5. 藏经阁“管理藏书”当前默认最低职位为“副掌门”。

藏经阁覆盖范围：
1. 当前版本会按同一套权限规则覆盖以下藏经阁相关按钮：
2. `ManageBookStore`
3. `ShowBookStore`
4. `ChooseReadBook`
5. `ChooseReadBookMoney`
6. `ShowBookWriter`
7. `ShowBookWriterSelf`
8. `StartSpeBookStorage`

发布文件：
1. DLL：`ForcePermissionTweaks.dll`
2. 压缩包：`ForcePermissionTweaks-v1.1.0.zip`
