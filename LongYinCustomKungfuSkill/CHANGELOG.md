# Changelog

## 2.4.2

- 修复自定义功法加入江湖秘籍随机池时误写入 `0` 号长乐帮秘籍池的问题。

## 2.4.1

- 兼容新版游戏中 `GameDataController.kungfuSkillDataList` 改为字典结构，修复加载后注入江湖秘籍随机池时的 `MissingMethodException`。
- 自定义功法加入江湖秘籍随机池时会跳过不存在的稀有度池，避免异常中断后续加载。
