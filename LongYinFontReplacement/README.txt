《龙胤立志传》字体替换 Mod 骨架（LongYinFontReplacement）

【当前定位】
1. 这是一个独立项目，不依赖也不修改其他现有 Mod。
2. 当前版本的目标，是复用游戏 `LTLocalization.CheckTextFont` 这条运行时字体替换链。
3. 现阶段仅覆盖 `UnityEngine.UI.Text`；`UILabel / UIFont / TMP_Text` 暂未接入。

【工程结构】
1. `LongYinFontReplacement.csproj`
   - 参照旁边 `LongYinCustomKungfuSkill` 的 SDK 风格工程。
2. `Main.cs`
   - 包含 MelonMod 入口、配置加载、字体解析和 Harmony 补丁。
3. `README.txt`
   - 当前使用说明。

【配置目录】
1. Mod 首次启动后会自动创建：
   - `UserData\LongYinFontReplacement`
   - `UserData\LongYinFontReplacement\Bundles`
   - `UserData\LongYinFontReplacement\FontMappings.txt`

【配置格式】
1. `FontMappings.txt` 每行格式：
   - `原字体名|资源类型|参数1|参数2|行距倍率`
2. `资源类型` 支持：
   - `os`
   - `bundle`
3. 参数说明：
   - `os`：`参数1=系统字体名称`，`参数2` 留空
   - `bundle`：`参数1=相对 UserData\LongYinFontReplacement 的 AssetBundle 路径`，`参数2=Bundle 内字体资源名`

【示例】
1. 系统字体：
   - `汇文明朝体|os|Microsoft YaHei||0.80`
   - `SourceHanSansCN-Regular|os|Microsoft YaHei||0.80`
2. AssetBundle：
   - `汇文明朝体|bundle|Bundles\MainFont.bundle|MainFont|0.80`

【当前补丁点】
1. `LTLocalization.CheckTextFont`
   - 前置接管，命中自定义映射时直接替换字体。
2. `LTLocalization.AddText`
   - 后置补一次，避免追加文本后漏替换。
3. `LTLocalization.Init`
   - 初始化完成后批量扫描一次已加载文本。
4. `LTLocalization.ManualSetLanguage`
   - 切语言后重新扫描一次。
5. `OnSceneWasInitialized`
   - 场景初始化后批量扫描一次。

【与 PDF 教程的区别】
1. PDF 方案是直接修改 `resources.assets` 内的 `Font` 资源。
2. 当前工程是不改原版资源文件，而是在运行时接管字体替换流程。
3. 优点是回退方便、维护成本低；缺点是当前还不是全字体系统覆盖。

【后续建议】
1. 第二阶段接入 `UILabel / UIFont / NGUIFont`。
2. 第三阶段接入 `TMP_Text / TMP_FontAsset`。
3. 真正投入使用时，建议优先使用 AssetBundle 方式而不是长期依赖系统字体名。
