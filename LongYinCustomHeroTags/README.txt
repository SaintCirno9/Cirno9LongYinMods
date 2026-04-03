LongYinCustomHeroTags

功能：
- 通过 `Resources.Load` Patch 直接注入 `HeroTagData` 资源
- 支持覆盖原版天赋与新增自定义天赋
- 支持旧版单文件配置与新版多目录配置并存
- 首次命中资源时自动导出原版 `Vanilla_HeroTagData.csv`

安装：
1. 将 `LongYinCustomHeroTags.dll` 放到游戏 `Mods` 目录
2. 进入游戏一次后，Mod 会自动准备配置目录：
   - 新版：`UserData/LongYinCustomHeroTags/Data/000/HeroTagData.csv`
   - 旧版兼容：`UserData/LongYinCustomHeroTags/HeroTagData.csv`
   - 原版导出：`UserData/LongYinCustomHeroTags/Vanilla_HeroTagData.csv`

配置目录规则：
1. 新版推荐把配置放到 `UserData/LongYinCustomHeroTags/Data/<数字目录>/HeroTagData.csv`
2. 目录名必须能转换成数字，例如：`000`、`100`、`200`
3. Mod 会按数字从小到大读取这些目录，后加载的配置会覆盖先加载的同 id 原版天赋
4. 为兼容旧版本，根目录下已有的 `HeroTagData.csv` 仍会继续读取
5. 首次启动若没有任何配置，Mod 只会自动创建 `Data/000/HeroTagData.csv`
6. 同一个 `id` 在后加载配置里再次出现时，即使名称相同，也会按“后者覆盖前者”处理

配置列：
- id
- 名称
- 价值
- 影响目标
- 效果
- 同义组
- 反义组
- 可随机
- 展示顺序
- 领悟需求
- 替换
- 大类
- 显示箭头

id 规则：
1. `id` 命中原版天赋编号时：覆盖该原版天赋
2. `id` 大于原版最大编号时：视为新增天赋
3. 多个配置文件里的新增天赋即使写了重复 id，运行时也会自动顺延分配新的实际 id
4. 旧版无 `id` 列的配置文件仍可读取，但会全部按“新增天赋”处理
5. 如果不同天赋使用了相同名称但不是同一个 `id`，Mod 会输出警告并跳过后者，避免生成歧义数据

替换规则：
1. `替换` 列保持与原版一致，按 **天赋名称列表** 填写
2. 多个名称用 `;`、`|` 或 `+` 分隔都可以
3. 为兼容过渡版本，若这里写的是数字 id，Mod 会尽量自动换算回最终名称
4. 如果引用的名称或 id 在最终结果里不存在，该项会被忽略并输出警告

影响目标可用值：
- Enemy
- SelfTeam
- Self
- TeamMate
- EmptyGridSummon
- EmptyGridJump

数值说明：
- 配置文件编码建议使用 UTF-8
- 百分比类效果请使用小数格式
- 例如：
  - `0.3 = 30%`
  - `1.0 = 100%`
  - `3.0 = 300%`

说明：
- 实际应以运行时导出的 `Vanilla_HeroTagData.csv` 为准
- 若你想改原版天赋，建议先从导出的原版表里复制对应行，再保持 id 不变进行修改
- 若你想新增天赋，直接填写一个大于原版最大编号的 id 即可
- 覆盖原版天赋时，Mod 会保留原版表中未识别的额外列，降低游戏后续扩列带来的兼容风险
- Mod 会在日志中输出配置源、覆盖数量、新增数量、运行时 id 分配与替换引用处理信息
