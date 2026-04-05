using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(LongYinAttrReinforceMod.Main), "LongYinAttrReinforceMod", "1.2.1", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinAttrReinforceMod
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            ReinforceConfig.Initialize();
            Log.Msg("LongYinAttrReinforceMod 已加载。");
            Log.Msg("配置文件：" + ReinforceConfig.ConfigFilePath);
            Log.Msg(ReinforceConfig.BuildSummary());
            Log.Msg("当前版本使用 CountHeroData 后置修正，额外叠加生命与内力上限结果。");
        }
    }

    internal sealed class PerStatRule
    {
        public float Hp { get; set; }

        public float Mana { get; set; }
    }

    internal sealed class ReinforceRules
    {
        public PerStatRule Str { get; set; } = new PerStatRule();

        public PerStatRule Agl { get; set; } = new PerStatRule();

        public PerStatRule Inte { get; set; } = new PerStatRule();

        public PerStatRule Wil { get; set; } = new PerStatRule();

        public PerStatRule Con { get; set; } = new PerStatRule();

        public PerStatRule Mag { get; set; } = new PerStatRule();

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraKeys { get; set; }

        public ReinforceRules NormalizeAliases()
        {
            if (ExtraKeys == null || ExtraKeys.Count == 0)
            {
                return this;
            }

            ApplyAlias("力道", value => Str = value);
            ApplyAlias("灵巧", value => Agl = value);
            ApplyAlias("智力", value => Inte = value);
            ApplyAlias("意志", value => Wil = value);
            ApplyAlias("体质", value => Con = value);
            ApplyAlias("经脉", value => Mag = value);
            return this;
        }

        public static ReinforceRules Default()
        {
            return new ReinforceRules
            {
                Str = new PerStatRule
                {
                    Hp = 0.35f,
                    Mana = 0f
                },
                Agl = new PerStatRule
                {
                    Hp = 0.2f,
                    Mana = 0f
                },
                Inte = new PerStatRule
                {
                    Hp = 0f,
                    Mana = 0.15f
                },
                Wil = new PerStatRule
                {
                    Hp = 0.35f,
                    Mana = 0.1f
                },
                Con = new PerStatRule
                {
                    Hp = 1f,
                    Mana = 0f
                },
                Mag = new PerStatRule
                {
                    Hp = 0f,
                    Mana = 0.4f
                }
            };
        }

        private void ApplyAlias(string alias, Action<PerStatRule> apply)
        {
            if (ExtraKeys == null || !ExtraKeys.TryGetValue(alias, out JsonElement valueElement))
            {
                return;
            }

            if (valueElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            try
            {
                PerStatRule value = valueElement.Deserialize<PerStatRule>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (value != null)
                {
                    apply(value);
                }
            }
            catch
            {
            }
        }
    }

    internal static class ReinforceConfig
    {
        private static readonly string ConfigDirPath = Path.Combine(AppContext.BaseDirectory, "UserData", "LongYinAttrReinforceMod");

        internal static readonly string ConfigFilePath = Path.Combine(ConfigDirPath, "rules.json");

        private static ReinforceRules _rules = ReinforceRules.Default();

        internal static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirPath);
            }
            catch
            {
            }

            if (!TryLoad(out _rules))
            {
                _rules = ReinforceRules.Default();
                TryWriteExample(_rules);
            }
            else
            {
                TryRewriteCanonicalChinese(_rules);
            }
        }

        internal static float HpPerPoint(string key)
        {
            return key switch
            {
                "Str" => _rules.Str.Hp,
                "Agl" => _rules.Agl.Hp,
                "Inte" => _rules.Inte.Hp,
                "Wil" => _rules.Wil.Hp,
                "Con" => _rules.Con.Hp,
                "Mag" => _rules.Mag.Hp,
                _ => 0f
            };
        }

        internal static float ManaPerPoint(string key)
        {
            return key switch
            {
                "Str" => _rules.Str.Mana,
                "Agl" => _rules.Agl.Mana,
                "Inte" => _rules.Inte.Mana,
                "Wil" => _rules.Wil.Mana,
                "Con" => _rules.Con.Mana,
                "Mag" => _rules.Mag.Mana,
                _ => 0f
            };
        }

        internal static string BuildSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("当前强化配置：");
            builder.AppendLine($" 力道：生命上限={_rules.Str.Hp} 内力上限={_rules.Str.Mana}");
            builder.AppendLine($" 灵巧：生命上限={_rules.Agl.Hp} 内力上限={_rules.Agl.Mana}");
            builder.AppendLine($" 智力：生命上限={_rules.Inte.Hp} 内力上限={_rules.Inte.Mana}");
            builder.AppendLine($" 意志：生命上限={_rules.Wil.Hp} 内力上限={_rules.Wil.Mana}");
            builder.AppendLine($" 体质：生命上限={_rules.Con.Hp} 内力上限={_rules.Con.Mana}");
            builder.AppendLine($" 经脉：生命上限={_rules.Mag.Hp} 内力上限={_rules.Mag.Mana}");
            return builder.ToString();
        }

        private static bool TryLoad(out ReinforceRules rules)
        {
            rules = ReinforceRules.Default();

            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    return false;
                }

                string text = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                ReinforceRules loaded = JsonSerializer.Deserialize<ReinforceRules>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (loaded == null)
                {
                    return false;
                }

                rules = loaded.NormalizeAliases();
                return true;
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Warning("读取强化配置失败：" + ex.GetType().Name + " - " + ex.Message);
                return false;
            }
        }

        private static void TryWriteExample(ReinforceRules rules)
        {
            try
            {
                WriteChineseRulesFile(rules, ConfigFilePath);
                Melon<Main>.Logger.Msg("已生成示例规则文件：" + ConfigFilePath);
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Warning("写入示例规则失败：" + ex.GetType().Name + " - " + ex.Message);
            }
        }

        private static void TryRewriteCanonicalChinese(ReinforceRules rules)
        {
            try
            {
                string content = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                if (content.Contains("\\u", StringComparison.Ordinal))
                {
                    WriteChineseRulesFile(rules, ConfigFilePath);
                    Melon<Main>.Logger.Msg("已将规则文件重写为中文键名格式。");
                }
            }
            catch
            {
            }
        }

        private static void WriteChineseRulesFile(ReinforceRules rules, string filePath)
        {
            Dictionary<string, PerStatRule> dictionary = new Dictionary<string, PerStatRule>
            {
                ["力道"] = rules.Str,
                ["灵巧"] = rules.Agl,
                ["智力"] = rules.Inte,
                ["意志"] = rules.Wil,
                ["体质"] = rules.Con,
                ["经脉"] = rules.Mag
            };

            string content = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

    }

    internal static class AttributeReinforceRuntime
    {
        private const float FloatEpsilon = 0.0001f;
        private const int MaxHpAddType = 57;
        private const int MaxManaAddType = 59;
        private static readonly (string Key, int Index)[] BaseAttrs =
        {
            ("Str", 0),
            ("Agl", 1),
            ("Inte", 2),
            ("Wil", 3),
            ("Con", 4),
            ("Mag", 5)
        };

        public static void ApplyReinforceToCountedHero(HeroData hero, ResourceStateSnapshot snapshot)
        {
            if (hero == null || hero.isSummon || hero.totalAddData == null)
            {
                return;
            }

            float extraMaxHp = CalculateExtraMaxHp(hero);
            float extraMaxMana = CalculateExtraMaxMana(hero);
            if (Math.Abs(extraMaxHp) <= FloatEpsilon && Math.Abs(extraMaxMana) <= FloatEpsilon)
            {
                return;
            }

            float vanillaHp = hero.hp;
            float vanillaMana = hero.mana;
            float vanillaRealMaxHp = hero.realMaxHp;
            float vanillaRealMaxMana = hero.realMaxMana;

            RecalculateHeroResource(hero, extraMaxHp, extraMaxMana);
            RestoreCurrentResourceByRatio(
                hero,
                snapshot,
                vanillaHp,
                vanillaMana,
                vanillaRealMaxHp,
                vanillaRealMaxMana);
        }

        private static float CalculateExtraMaxHp(HeroData hero)
        {
            return CalculateExtraBySelector(hero, ReinforceConfig.HpPerPoint);
        }

        private static float CalculateExtraMaxMana(HeroData hero)
        {
            return CalculateExtraBySelector(hero, ReinforceConfig.ManaPerPoint);
        }

        private static float CalculateExtraBySelector(HeroData hero, Func<string, float> selector)
        {
            if (hero == null || selector == null)
            {
                return 0f;
            }

            float total = 0f;
            foreach ((string key, int index) in BaseAttrs)
            {
                float attriValue = GetAttriValue(hero, index);
                total += attriValue * selector(key);
            }

            return total;
        }

        private static float GetAttriValue(HeroData hero, int index)
        {
            if (HasListIndex(hero.totalAttri, index))
            {
                return GetListValue(hero.totalAttri, index);
            }

            if (HasListIndex(hero.baseAttri, index))
            {
                return GetListValue(hero.baseAttri, index);
            }

            return GetListValue(hero.maxAttri, index);
        }

        private static void RecalculateHeroResource(HeroData hero, float extraMaxHp, float extraMaxMana)
        {
            float maxHpAdd = hero.totalAddData.Get(MaxHpAddType) + extraMaxHp;
            float maxManaAdd = hero.totalAddData.Get(MaxManaAddType) + extraMaxMana;
            float hpRate = 1f + GetListValue(hero.totalFightSkill, 2) * 0.01f;
            float manaRate = 1f + GetListValue(hero.totalFightSkill, 0) * 0.01f;

            hero.maxhp = Max(10f, hpRate * (GlobalData.HeroBaseHp + maxHpAdd));
            hero.realMaxHp = Max(10f, (1f - (hero.externalInjury * 2f + hero.poisonInjury) / 400f) * hero.maxhp);
            hero.hp = Clamp(hero.hp, 0f, hero.realMaxHp);

            hero.maxMana = Max(20f, manaRate * (GlobalData.HeroBaseMp + maxManaAdd));
            hero.realMaxMana = Max(20f, (1f - (hero.internalInjury * 2f + hero.poisonInjury) / 400f) * hero.maxMana);
            hero.mana = Clamp(hero.mana, 0f, hero.realMaxMana);
        }

        private static void RestoreCurrentResourceByRatio(
            HeroData hero,
            ResourceStateSnapshot snapshot,
            float vanillaHp,
            float vanillaMana,
            float vanillaRealMaxHp,
            float vanillaRealMaxMana)
        {
            if (hero == null || !snapshot.HasValue)
            {
                return;
            }

            hero.hp = RestoreCurrentValueByRatio(snapshot.Hp, snapshot.RealMaxHp, vanillaHp, vanillaRealMaxHp, hero.realMaxHp);
            hero.mana = RestoreCurrentValueByRatio(snapshot.Mana, snapshot.RealMaxMana, vanillaMana, vanillaRealMaxMana, hero.realMaxMana);
        }

        private static float RestoreCurrentValueByRatio(
            float oldCurrent,
            float oldMax,
            float vanillaCurrent,
            float vanillaMax,
            float newMax)
        {
            if (newMax <= FloatEpsilon)
            {
                return 0f;
            }

            if (oldMax > FloatEpsilon)
            {
                // 当重算后上限回落时，优先保留旧绝对值，只做裁剪。
                // 这样可以避免战斗结算等场景里因为上限先降后升，导致当前值被额外“补满”。
                if (newMax + FloatEpsilon < oldMax)
                {
                    return Clamp(oldCurrent, 0f, newMax);
                }

                float ratio = Clamp(oldCurrent / oldMax, 0f, 1f);
                return Clamp(newMax * ratio, 0f, newMax);
            }

            if (vanillaMax > FloatEpsilon)
            {
                float patchedDelta = newMax - vanillaMax;
                return Clamp(vanillaCurrent + patchedDelta, 0f, newMax);
            }

            return newMax;
        }

        public static void FillGeneratedHeroCurrentResources(HeroData hero)
        {
            if (hero == null || hero.isSummon)
            {
                return;
            }

            if (hero.realMaxHp > FloatEpsilon)
            {
                hero.hp = hero.realMaxHp;
            }
            else if (hero.maxhp > FloatEpsilon)
            {
                hero.hp = hero.maxhp;
            }

            if (hero.realMaxMana > FloatEpsilon)
            {
                hero.mana = hero.realMaxMana;
            }
            else if (hero.maxMana > FloatEpsilon)
            {
                hero.mana = hero.maxMana;
            }
        }

        public static void RefreshNewGamePlayerCurrentResources(GameController controller)
        {
            if (controller == null || controller.worldData == null)
            {
                return;
            }

            HeroData player;
            try
            {
                player = controller.worldData.Player();
            }
            catch
            {
                return;
            }

            if (player == null || player.isSummon)
            {
                return;
            }

            FillGeneratedHeroCurrentResources(player);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        private static int GetListCount(object list)
        {
            if (list == null)
            {
                return 0;
            }

            try
            {
                PropertyInfo property = list.GetType().GetProperty("Count");
                if (property == null)
                {
                    return 0;
                }

                return Convert.ToInt32(property.GetValue(list, null));
            }
            catch
            {
                return 0;
            }
        }

        private static bool HasListIndex(object list, int index)
        {
            return index >= 0 && index < GetListCount(list);
        }

        private static float GetListValue(object list, int index)
        {
            if (list == null)
            {
                return 0f;
            }

            if (index < 0 || index >= GetListCount(list))
            {
                return 0f;
            }

            try
            {
                PropertyInfo property = list.GetType().GetProperty("Item");
                if (property == null)
                {
                    return 0f;
                }

                object rawValue = property.GetValue(list, new object[] { index });
                if (rawValue is float value)
                {
                    return value;
                }

                return Convert.ToSingle(rawValue);
            }
            catch
            {
                return 0f;
            }
        }
    }

    internal readonly struct ResourceStateSnapshot
    {
        public ResourceStateSnapshot(
            float hp,
            float mana,
            float realMaxHp,
            float realMaxMana)
        {
            Hp = hp;
            Mana = mana;
            RealMaxHp = realMaxHp;
            RealMaxMana = realMaxMana;
            HasValue = true;
        }

        public float Hp { get; }

        public float Mana { get; }

        public float RealMaxHp { get; }

        public float RealMaxMana { get; }

        public bool HasValue { get; }
    }

    [HarmonyPatch(typeof(GameController), "CountHeroData")]
    internal static class ReinforceCountHeroDataPatch
    {
        [HarmonyPrefix]
        public static void Prefix(HeroData hero, out ResourceStateSnapshot __state)
        {
            if (hero == null)
            {
                __state = default;
                return;
            }

            __state = new ResourceStateSnapshot(
                hero.hp,
                hero.mana,
                hero.realMaxHp,
                hero.realMaxMana);
        }

        [HarmonyPostfix]
        public static void Postfix(HeroData hero, ResourceStateSnapshot __state)
        {
            AttributeReinforceRuntime.ApplyReinforceToCountedHero(hero, __state);
        }
    }

    [HarmonyPatch(typeof(GameController), "GenerateHeroData", new Type[]
    {
        typeof(string),
        typeof(int),
        typeof(int),
        typeof(float),
        typeof(HeroData),
        typeof(bool),
        typeof(SexLimit),
        typeof(bool),
        typeof(bool)
    })]
    internal static class ReinforceGenerateHeroDataPatch
    {
        [HarmonyPostfix]
        public static void Postfix(HeroData __result)
        {
            AttributeReinforceRuntime.FillGeneratedHeroCurrentResources(__result);
        }
    }

    [HarmonyPatch(typeof(GameController), "StartNewGameSetting")]
    internal static class ReinforceStartNewGameSettingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            AttributeReinforceRuntime.RefreshNewGamePlayerCurrentResources(__instance);
        }
    }

}
