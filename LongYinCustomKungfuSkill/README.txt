LongYinCustomKungfuSkill

目的：
- 这是一个用于“添加自定义 KungfuSkill”的 Mod 项目骨架

当前状态：
- 已建立基础工程结构
- 已配置 MelonLoader / 游戏程序集引用
- 已配置构建后自动打包 zip
- 尚未实现自定义功法的读取、注册和注入逻辑

当前文件说明：
- LongYinCustomKungfuSkill.csproj：项目文件与打包配置
- Main.cs：最小可编译 Mod 入口
- .vscode/tasks.json：VS Code 构建任务

后续可扩展方向：
- 从 UserData/LongYinCustomKungfuSkill 读取 KungfuSkill 配置
- 解析自定义 KungfuSkill 数据
- 在游戏加载流程中注册 KungfuSkill
- 补充示例配置文件与原版参考数据
