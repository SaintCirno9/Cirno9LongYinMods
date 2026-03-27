《龙胤立志传》功法属性自定义 Mod (LongYinCustomKungfuSkill)

【安装说明】
1. 本 Mod 依赖于 MelonLoader 运行环境。请确保你的游戏已经安装了 MelonLoader。
2. 将本压缩包中的 `LongYinCustomKungfuSkill.dll` 文件解压并放入游戏根目录下的 `Mods` 文件夹中（路径示例：`LongYinLiZhiZhuan\Mods\LongYinCustomKungfuSkill.dll`）。

【使用说明】
1. 安装好 Mod 后，第一次启动游戏时，它会在游戏根目录自动建立 `UserData\LongYinCustomKungfuSkill` 文件夹。
2. 文件夹内会生成两份表格、一个图标文件夹，以及一个导出开关配置文件：
   - `vanilla_KungfuSkillData.csv`：这是游戏原版的功法数据大全，仅供参考，你在里面的修改不会生效。
   - `KungfuSkillData.csv`：这就是让你进行修改的自定义表格。文件里自带了多条“太祖XXX”测试示例，可直接参考。
   - `Icons`：可选的自定义图标文件夹。若要给某个新增功法指定图标，把对应的 PNG 放进这里即可。
   - `LongYinCustomKungfuSkill.cfg`：控制是否导出原版功法图标的配置文件，默认是关闭导出。
3. `KungfuSkillData.csv` 中自带的那些“太祖XXX”新增测试功法，默认都已设置为【隐藏】；一般情况下它们不会直接出现在游戏里，主要是作为新增功法写法示例供你参考。
4. 由于这些隐藏测试功法不会正常刷出对应秘籍，所以就算你用修改器去检索秘籍物品，找不到也是正常现象。
5. 修改【原版功法】时：从 `vanilla_KungfuSkillData.csv` 中找到你想改的功法，把那一整行完整复制到 `KungfuSkillData.csv` 中，然后保持 `id` 不变，再修改其它字段。
6. 新增【自定义功法】时：直接在 `KungfuSkillData.csv` 末尾继续添加新行，并使用新的 `id`。
7. 关键规则：
   - Mod 完全认准 `id` 去寻找并替换功法。
   - 原版功法如果只是改属性，`id` 必须保持原样。
   - 新增功法的 `id` 必须连续，**中间不能跳号**。
   - 一旦检测到缺失的 `id`，Mod 会在控制台报错，并且**只注入缺失点之前的新增功法**。
8. 等级得在 `0-5` 之间，`5` 就是最高级的绝世功法。
9. 自定义图标用法：
   - 在 `UserData\LongYinCustomKungfuSkill\Icons` 中放入 PNG 文件。
   - 文件名必须和功法 `id` 一致，例如：`996.png`。
   - 若检测到对应 PNG，Mod 会优先使用这张自定义图标。
   - 若没有对应 PNG，Mod 会自动从原版同类型功法里挑一个图标进行分配。
10. 原版图标导出用法：
   - 打开 `UserData\LongYinCustomKungfuSkill\LongYinCustomKungfuSkill.cfg`。
   - 将 `EnableExportVanillaIcons=false` 改为 `EnableExportVanillaIcons=true`。
   - 进入游戏并触发图标加载后，Mod 会把原版功法图标导出到 `UserData\LongYinCustomKungfuSkill\原版Icons`。
   - 这个功能默认关闭，仅用于调试或取原版图标素材。
11. 保存表格后再次启动游戏，你的新属性或新增功法就会在游戏中自动生效。
12. （可选验证）如果你想确认是否生效，可以查看 MelonLoader 控制台窗口：
   - 修改原版功法时，会输出字段变更日志。
   - 新增功法时，会按类型整块输出，并显示是“检测到自定义图标”还是“分配图标 ID=XXX”。
   - 如果 `id` 不连续，会明确提示缺失的 `id`，以及缺失点之后的功法未注入。


【注意事项】
1. **文件编码乱码预防**：使用老版本 Windows 的 Excel 直接保存 CSV 可能会导致编码被改写为 GBK 从而让 Mod 报错。如果你用 Excel 修改并保存了表格，请在此后右键该 CSV 文件 -> 选择“打开方式”为“记事本” -> 点击左上角“文件”-“另存为”，并在弹出窗口的右下角编码格式确认选择为 **带有 BOM 的 UTF-8**（或 UTF-8）后覆盖保存一次。
2. 建议玩家直接使用 **Notepad++** 或 **VS Code** 这类专业的文本编辑器来修改 CSV 表格里的数据，这样可以绝对避免因编码突变导致的系统崩溃。
3. 如果你已经生成过 `KungfuSkillData.csv`，后续升级 Mod 时不会自动覆盖你的旧配置；想使用新版示例时，请自行备份后再手动替换。
