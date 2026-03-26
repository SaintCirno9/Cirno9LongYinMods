《龙胤立志传》功法属性自定义 Mod (LongYinCustomKungfuSkill)

【安装说明】
1. 本 Mod 依赖于 MelonLoader 运行环境。请确保你的游戏已经安装了 MelonLoader。
2. 将本压缩包中的 `LongYinCustomKungfuSkill.dll` 文件解压并放入游戏根目录下的 `Mods` 文件夹中（路径示例：`LongYinLiZhiZhuan\Mods\LongYinCustomKungfuSkill.dll`）。

【使用说明】
1. 安装好 Mod 后，第一次启动游戏时，它会在游戏根目录自动建立 `UserData\LongYinCustomKungfuSkill` 文件夹。
2. 文件夹内会生成两份表格：
   - `vanilla_KungfuSkillData.csv`：这是游戏原版的功法数据大全，仅供参考，你在里面的修改不会生效。
   - `KungfuSkillData.csv`：这就是让你进行修改的自定义表格。
3. 从原版表格 `vanilla_KungfuSkillData.csv` 中找到你想修改的功法，把那一行完整地复制，然后粘贴到 `KungfuSkillData.csv` 中（自带的那行“太祖长拳”修改示例如果你不需要可以直接删掉）。
4. 关键点：保证【id】列里的数字保持原样，Mod 完全认准 id 去寻找要修改的游戏功法本体。然后随意修改该行里面的其它数字或属性配置（例如：把基础威力改为 9999 ，或是把等级调成 5 等等）。
5. 等级得在0-5之间，5就是最高级的绝世功法。
6. 保存表格，再次启动游戏，你的新属性就会在游戏中自动替换原版功法并即刻生效了！
7. （可选验证）如果你想确认自己改成了没有，游戏启动时可以去瞄一眼背后黑色的 MelonLoader 控制台窗口，只要看到了 `已覆盖原版功法：名称=XXX` 这样的一行字，就说明 Mod 已经精确命中了！


【注意事项】
1. **文件编码乱码预防**：使用老版本 Windows 的 Excel 直接保存 CSV 可能会导致编码被改写为 GBK 从而让 Mod 报错。如果你用 Excel 修改并保存了表格，请在此后右键该 CSV 文件 -> 选择“打开方式”为“记事本” -> 点击左上角“文件”-“另存为”，并在弹出窗口的右下角编码格式确认选择为 **带有 BOM 的 UTF-8**（或 UTF-8）后覆盖保存一次。
2. 建议玩家直接使用 **Notepad++** 或 **VS Code** 这类专业的文本编辑器来修改 CSV 表格里的数据，这样可以绝对避免因编码突变导致的系统崩溃。
