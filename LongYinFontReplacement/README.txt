《龙胤立志传》字体替换 Mod（LongYinFontReplacement）

【功能】
1. 替换游戏内常见 UI 文字的字体。
2. 支持全局通用字体，也支持按原字体名单独覆盖。
3. 字体文件直接放在 `UserData\LongYinFontReplacement\Fonts`，不需要安装到系统。
4. 不修改游戏原版资源文件，删除 Mod 后即可回退原版显示。

【安装】
1. 将 DLL 包里的 `LongYinFontReplacement.dll` 放到游戏 `Mods` 目录。
2. 将 UserData 包解压到游戏根目录。
3. 确认文件位置类似下面这样：
   - `Mods\LongYinFontReplacement.dll`
   - `UserData\LongYinFontReplacement\FontMappings.txt`
   - `UserData\LongYinFontReplacement\Fonts\猫啃网糖圆体.ttf`
4. 启动游戏。如果 `UserData\LongYinFontReplacement` 不存在，Mod 会自动创建默认配置目录。

【配置文件】
配置文件位置：

```text
UserData\LongYinFontReplacement\FontMappings.txt
```

每行一条规则。以 `#` 开头的行是注释，不会生效。

全局通用字体：

```text
default|字体文件路径|字体族名|行距倍率|字号倍率
```

特定字体覆盖：

```text
override|原字体名|字体文件路径|字体族名|行距倍率|字号倍率
```

保留原版字体，不被 `default` 替换：

```text
preserve|原字体名
```

【字段说明】
1. `字体文件路径`
   - 相对 `UserData\LongYinFontReplacement`。
   - 例如 `Fonts\猫啃网糖圆体.ttf`。
2. `字体族名`
   - 通常可以留空，Mod 会尝试从 TTF 文件自动读取。
   - 如果自动读取失败，再填写字体内部族名。
3. `行距倍率`
   - 乘到原文字组件的行距上。
   - `1.00` 保持原版，`1.20` 加大 20%，`0.80` 压缩 20%。
4. `字号倍率`
   - 乘到原文字组件的字号上。
   - `1.00` 保持原版，`0.85` 缩小 15%。
5. `preserve`
   - 用于保留某个原版字体，不让它被 `default` 替换。
   - 如果同一个原字体名同时配置了 `override`，会优先使用 `override`。

【建议准备的原版字体名】
游戏内置字体种类较多。如果想尽量完整替换，建议按下面这些原版字体名准备映射。配置里的 `override` 不要求你的字体文件名和原版完全一致，但“原字体名”要和游戏原字体匹配。

中文常用字体：

```text
汇文明朝体
SourceHanSerifCN-Bold
SourceHanSerifCN-Medium
SourceHanSansCN-Bold
SourceHanSansCN-Regular
字体家AI造字剑客
字体家AI造字霸牙
```

英文 / 装饰字体：

```text
LiberationSans
Roboto-Bold
Electromagnetic Highway Sign
Oswald-Bold
Anton
Bangers
```

【常用字体用途】
1. 正文 / 侧边栏任务字体：`汇文明朝体`
2. 标题字体：`字体家AI造字剑客`
3. 剧情 CG 字体：`SourceHanSerifCN` 系列
4. 常规界面显示字体：`SourceHanSansCN` 系列

【示例】
全局替换为默认字体：

```text
default|Fonts\猫啃网糖圆体.ttf||1.00|1.00
```

只给某个原字体单独指定替换字体：

```text
override|SourceHanSerifCN-Bold|Fonts\SerifFont.ttf|SerifFont|1.00|1.00
```

字体偏大时缩小字号：

```text
default|Fonts\猫啃网糖圆体.ttf||1.00|0.85
```

文字行距太挤时增大行距：

```text
default|Fonts\猫啃网糖圆体.ttf||1.20|1.00
```

保留某个原版字体，不走全局默认替换：

```text
preserve|字体家AI造字霸牙
```

按原版字体名单独覆盖：

```text
override|汇文明朝体|Fonts\猫啃网糖圆体.ttf||1.00|1.00
override|SourceHanSerifCN-Bold|Fonts\猫啃网糖圆体.ttf||1.00|1.00
override|SourceHanSansCN-Regular|Fonts\猫啃网糖圆体.ttf||1.00|1.00
override|字体家AI造字剑客|Fonts\猫啃网糖圆体.ttf||1.00|1.00
override|LiberationSans|Fonts\猫啃网糖圆体.ttf||1.00|1.00
```

【常见问题】
1. 字体没有变化
   - 确认 DLL 在 `Mods` 目录下。
   - 确认 TTF 文件路径和 `FontMappings.txt` 里的路径一致。
   - 查看 `MelonLoader\Latest.log` 是否有 `LongYinFontReplacement 已加载`。
2. 有些文字没有替换
   - 当前版本主要覆盖常见 UI 文字，并不保证覆盖所有字体系统。
3. 文字缺字或显示异常
   - 换一个包含完整中文字符的 TTF 字体。
   - 如果字体字面偏大，优先把字号倍率调到 `0.80-0.90`。
4. 想恢复原版字体
   - 删除 `Mods\LongYinFontReplacement.dll`，或暂时移走该文件。
