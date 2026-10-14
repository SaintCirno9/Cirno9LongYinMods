# BattleSkillRuleTweaks 维护说明

## 游戏更新后的兼容性风险

此模组直接写入 `BattleController` 的 IL2CPP 对象字段偏移，例如 `nowActiveUnitAttacked`。游戏更新、重新导出 IL2CPP 程序集或字段布局变化后，旧偏移可能失效。

当前代码会通过 Il2Cpp 字段元数据自动检测 `nowActiveUnitAttacked` 和协程 `<>4__this` 的实际偏移。检测成功时使用运行时偏移；关键字段检测失败时禁用技能后继续行动补丁，避免写错对象内存。

典型表现包括：

- 右键撤回移动时回到错误格子。
- `BattleUnit.EnterGrid`、`BattleController.RegretMove`、`BattleController.RunBattle`、`BattleController.Update` 附近出现 `IndexOutOfRangeException` 或 `ArgumentOutOfRangeException`。
- 内功、轻功、绝技释放后继续行动状态异常。

## 处理方法

遇到疑似游戏更新导致的问题时，不要直接猜测偏移。按以下顺序确认：

1. 查看仓库根目录的 `GameSource\dump.cs`（本地导出，不提交）中 `BattleController` 的字段布局，确认 `originUnitData`、`nowActiveUnitMoved`、`nowActiveUnitAttacked`、`nowActiveState` 的最新偏移。
2. 如 `dump.cs` 无法确认，使用 IDA 查看 `BattleController.RegretMove`、`BattleController.RunBattle`、`BattleUnit.EnterGrid` 的实际读写位置。
3. 更新 `Main.cs` 中对应的已知偏移常量，确保日志中的 old/new 对照仍有参考价值。
4. 执行 `dotnet build ".\BattleSkillRuleTweaks.csproj"` 验证构建。
5. 在游戏内复测：移动、释放内功/轻功/绝技、右键撤回移动、结束回合。

当前已确认的关键偏移：

- `BattleController.originUnitData`: `0x118`
- `BattleController.nowActiveUnitMoved`: `0x120`
- `BattleController.nowActiveUnitAttacked`: `0x121`
- `BattleController.nowActiveState`: `0x124`
- `BattleController` 协程 `<>4__this`: `0x20`
