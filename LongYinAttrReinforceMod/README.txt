LongYinAttrReinforceMod 工程

说明：
1. 本目录是整理后的独立模组项目目录。
2. 旧目录 Mod Source\LongYinAttrReinforceMod 仍保留，作为 dnSpy 反编译参考，不再建议继续在其上维护。
3. 当前逻辑采用 GameController.CountHeroData 后置修正：
   - 在原版总数值结算完成后，额外注入六维换算得到的生命上限和内力上限增量。
   - 然后按原版公式重新计算 maxhp、realMaxHp、maxMana、realMaxMana，并对当前 hp/mana 做 clamp。
4. 当前配置文件路径仍为：
   UserData\LongYinAttrReinforceMod\rules.json
