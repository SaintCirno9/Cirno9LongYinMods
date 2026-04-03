# NoGenderLimit

这个项目用于修改七夕事件的可选对象筛选逻辑。

当前目标：

- 去掉“只能选择与主角性别不同角色”的限制
- 去掉普通互动中因性别导致的结缘入口缺失
- 保留原有的好感度、监禁、隐藏等基础筛选条件
- 保持最小补丁范围，只修改性别限制相关入口

当前已知逻辑：

- 原方法：`PlotController.ChooseQiXiFestivalFriend()`
- 原筛选条件之一：`hero.isFemale != player.isFemale`
- 普通互动中的“结缘”按钮会在部分情况下缺失，因此额外补一个 `AskHeroToLover`
- 其余已确认条件：
  - `hero.Favor(false) >= 50`
  - `!hero.inPrison`
  - `!hero.hide`

当前实现状态：

- 已实现：
  - 七夕对象不限性别
  - 普通互动补充“结缘”入口
- 已完成本地构建验证：
  - `dotnet restore .\NoGenderLimit.csproj`
  - `dotnet build .\NoGenderLimit.csproj --no-restore -p:OutputPath=.\build\ -p:AppendTargetFrameworkToOutputPath=false`
- 当前构建产物：
  - `build\NoGenderLimit.dll`

后续待确认项：

- `ChooseController.ShowChoosePanel(...)` 的全部参数是否与原始 IL2CPP 调用完全一致
- 是否需要补 `sendResultParam`
- `ChooseType` 与 `ChooseFilterType` 是否需要改成更明确的枚举名
- 游戏内实机验证：
  - 七夕名单是否已不限性别
  - 普通互动在交友后是否出现“结缘”
