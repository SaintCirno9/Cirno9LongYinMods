《龙胤立志传》字体替换 Mod（LongYinFontReplacement）

【当前定位】
1. 这是一个独立项目，不依赖也不修改其他现有 Mod。
2. 当前版本复用游戏 `LTLocalization.CheckTextFont` 这条运行时字体替换链。
3. 现阶段仅覆盖 `UnityEngine.UI.Text`；`UILabel / UIFont / TMP_Text` 暂未接入。
4. TTF 会以内存字体方式交给 Unity 加载，不需要把字体安装到系统，也不需要修改原版资源文件。

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
   - `UserData\LongYinFontReplacement\Fonts`
   - `UserData\LongYinFontReplacement\FontMappings.txt`

【配置格式】
1. `FontMappings.txt` 支持两类规则：
   - `default|字体文件路径|字体族名|行距倍率|字号倍率`
   - `override|原字体名|字体文件路径|字体族名|行距倍率|字号倍率`
2. 参数说明：
   - `default`：全局通用字体，所有未被 `override` 命中的字体都会使用它
   - `override`：特定原字体覆盖，只用于需要单独指定字体或行距的原字体名
   - `字体文件路径`：相对 `UserData\LongYinFontReplacement`，例如 `Fonts\庞门正道细线体.ttf`
   - `字体族名`：可留空自动读取；如果自动读取失败，再手动填写字体内部族名
   - `行距倍率`：乘到原组件行距上，`1.00` 表示保持原版；`1.20` 表示加大 20%；`0.80` 表示压缩 20%。原版不同 UI 可能本来就是 `1`、`0.9`、`0.8` 或 `0.75`。
   - `字号倍率`：乘到原组件字号上，`1.00` 表示保持原版；`0.85` 表示缩小 15%。笔画外扩或字面偏大的字体建议 `0.80-0.90`。

【示例】
1. 全局通用字体：
   - `default|Fonts\庞门正道细线体.ttf||1.00|1.00`
2. 特定字体覆盖：
   - `override|SourceHanSerifCN-Bold|Fonts\SerifFont.ttf|SerifFont|1.00|1.00`
3. 可注释保留的覆盖模板：
   - `# override|字体家AI造字剑客|Fonts\庞门正道细线体.ttf||1.00|1.00`
   - `# override|SourceHanSerifCN-Bold|Fonts\庞门正道细线体.ttf||1.00|1.00`
   - `# override|LiberationSans|Fonts\庞门正道细线体.ttf||1.00|1.00`

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
2. 当前工程是不改原版资源文件，而是在运行时接管字体替换流程，并把外部 TTF 注入到 Unity 动态字体的内存加载路径。
3. 优点是回退方便、维护成本低；缺点是当前还不是全字体系统覆盖。

【后续建议】
1. 第二阶段接入 `UILabel / UIFont / NGUIFont`。
2. 第三阶段接入 `TMP_Text / TMP_FontAsset`。
