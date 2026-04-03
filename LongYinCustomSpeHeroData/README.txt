《龙胤立志传》特殊角色数据自定义 Mod (LongYinCustomSpeHeroData)

【安装说明】
1. 本 Mod 依赖 MelonLoader 运行环境。
2. 构建后将 `LongYinCustomSpeHeroData.dll` 放入游戏目录下的 `Mods` 文件夹。

【使用说明】
1. 第一次启动游戏后，Mod 会在 `UserData\LongYinCustomSpeHeroData` 下生成以下文件：
   - `LongYinCustomSpeHeroData.cfg`
   - `SpeHeroData.csv`
   - `SpeHeroFaceData.csv`
   - `vanilla_SpeHeroData.csv`
   - `vanilla_SpeHeroFaceData.csv`
2. `LongYinCustomSpeHeroData.cfg` 用于控制是否允许所有特殊人物可招募：
   - 默认值为 `EnableRecruitAllSpeHeroes=true`
   - 设置为 `true` 时，会对运行时 `SpeHeroData` 中的全部特殊人物放开招募
   - 设置为 `false` 时，保持游戏原版招募逻辑
3. `SpeHeroData.csv` 对应 `GameData/SpeHeroData`。
4. `SpeHeroFaceData.csv` 对应 `GameData/SpeHeroFaceData`。
5. 配置文件中如果与原版 `id` 相同，则覆盖原条目；如果是新 `id`，则自动追加到表尾。
6. 修改保存后重新启动游戏即可生效。

【注意事项】
1. 建议使用 VS Code 或 Notepad++ 编辑 CSV，避免 Excel 改坏编码或分隔符。
2. Mod 已兼容 UTF-8 与 GBK 读取，但仍建议统一保存为 UTF-8。
