LongYinAttrReinforceMod 工程

说明：
1. 本目录是整理后的独立模组项目目录。
2. 旧目录 Mod Source\LongYinAttrReinforceMod 仍保留，作为 dnSpy 反编译参考，不再建议继续在其上维护。
3. 当前逻辑采用 GameController.CountHeroData 后置修正：
   - 在原版总数值结算完成后，额外注入六维换算得到的生命上限和内力上限增量。
   - 在进入 CountHeroData 前，会先记录当前 hp/realMaxHp 与 mana/realMaxMana 的真实比例。
   - 然后按原版公式重新计算 maxhp、realMaxHp、maxMana、realMaxMana。
   - 最后按新的 realMaxHp、realMaxMana 回填当前 hp、mana，尽量保持原有真实血蓝比例，避免过天或重复结算时出现异常掉段。
4. 当前配置文件路径仍为：
   UserData\LongYinAttrReinforceMod\rules.json
