NoGenderLimit

功能说明
- 去掉七夕事件候选对象的性别限制。
- 在普通互动中补充“结缘”入口，避免因性别限制导致入口缺失。

当前实现
- 重写 PlotController.ChooseQiXiFestivalFriend() 的候选筛选逻辑。
- 在 PlotController.AskHeroMakeFriend() 的 Postfix 中按需补充 AskHeroToLover 选项。

已保留的原始条件
- 好感度至少 50
- 目标未入狱
- 目标未隐藏

构建说明
- 直接执行 dotnet build NoGenderLimit.csproj 即可。
- 构建后会自动生成 DLL，并在项目目录输出带版本号的 zip 包。
