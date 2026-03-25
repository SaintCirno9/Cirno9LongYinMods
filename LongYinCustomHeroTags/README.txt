LongYinCustomHeroTags

功能：
- 从 UserData/LongYinCustomHeroTags/HeroTagData.csv 读取自定义天赋
- 游戏加载时自动注册这些天赋

安装：
1. 将 LongYinCustomHeroTags.dll 放到游戏 Mods 目录
2. 进入游戏一次后，Mod 会自动生成配置文件：
   UserData/LongYinCustomHeroTags/HeroTagData.csv

配置说明：
- 配置文件编码建议使用 UTF-8
- 百分比类效果请使用小数格式
- 例如：
  0.3 = 30%
  1.0 = 100%
  3.0 = 300%

当前配置列：
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

影响目标可用值：
- Enemy
- SelfTeam
- Self
- TeamMate
- EmptyGridSummon
- EmptyGridJump

说明：
- 原版参考数据见“原版HeroTagData.csv”
- Mod 读取的是 UserData 目录下自动生成的 HeroTagData.csv
