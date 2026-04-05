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

[assembly: MelonInfo(typeof(LongYinAttrReinforceMod.Main), "LongYinAttrReinforceMod", "1.3.0", "SaintCirno9", null)]
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
            Log.Msg("当前版本通过 HeroSpeAddData.Get 注入六维额外生命与内力上限。");
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
        internal const int MaxHpAddType = 57;
        internal const int MaxManaAddType = 59;
        private static readonly (string Key, int Index)[] BaseAttrs =
        {
            ("Str", 0),
            ("Agl", 1),
            ("Inte", 2),
            ("Wil", 3),
            ("Con", 4),
            ("Mag", 5)
        };

        public static bool TryApplyExtraResourceGet(HeroData hero, int type, ref float value)
        {
            if (hero == null || hero.isSummon)
            {
                return false;
            }

            float extraValue = type switch
            {
                MaxHpAddType => CalculateExtraBySelector(hero, ReinforceConfig.HpPerPoint),
                MaxManaAddType => CalculateExtraBySelector(hero, ReinforceConfig.ManaPerPoint),
                _ => 0f
            };

            if (Math.Abs(extraValue) <= FloatEpsilon)
            {
                return false;
            }

            value += extraValue;
            return true;
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
    }

    internal static class HeroAddDataOwnershipRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<ulong, HeroData> OwnerByAddDataPointer = new Dictionary<ulong, HeroData>();

        [ThreadStatic]
        private static HeroData _currentCountHero;

        internal static void EnterCountHeroData(HeroData hero)
        {
            _currentCountHero = hero;
            Register(hero);
        }

        internal static void ExitCountHeroData(HeroData hero)
        {
            if (ReferenceEquals(_currentCountHero, hero))
            {
                _currentCountHero = null;
            }
        }

        internal static void Register(HeroData hero)
        {
            if (hero == null)
            {
                return;
            }

            RegisterAddData(hero.baseAddData, hero);
            RegisterAddData(hero.totalAddData, hero);
        }

        internal static HeroData Resolve(HeroSpeAddData addData)
        {
            if (addData == null)
            {
                return null;
            }

            HeroData currentHero = _currentCountHero;
            if (BelongsToHero(addData, currentHero))
            {
                return currentHero;
            }

            ulong pointerValue = GetPointerValue(addData);
            if (pointerValue == 0)
            {
                return null;
            }

            lock (Sync)
            {
                if (OwnerByAddDataPointer.TryGetValue(pointerValue, out HeroData owner) && owner != null)
                {
                    return owner;
                }
            }

            return null;
        }

        private static void RegisterAddData(HeroSpeAddData addData, HeroData hero)
        {
            ulong pointerValue = GetPointerValue(addData);
            if (pointerValue == 0)
            {
                return;
            }

            lock (Sync)
            {
                OwnerByAddDataPointer[pointerValue] = hero;
            }
        }

        private static bool BelongsToHero(HeroSpeAddData addData, HeroData hero)
        {
            if (addData == null || hero == null)
            {
                return false;
            }

            return ReferenceEquals(addData, hero.baseAddData) || ReferenceEquals(addData, hero.totalAddData);
        }

        private static ulong GetPointerValue(Il2CppSystem.Object obj)
        {
            if (obj == null || obj.Pointer == IntPtr.Zero)
            {
                return 0;
            }

            return unchecked((ulong)obj.Pointer.ToInt64());
        }
    }

    [HarmonyPatch(typeof(GameController), "CountHeroData")]
    internal static class ReinforceCountHeroDataPatch
    {
        [HarmonyPrefix]
        public static void Prefix(HeroData hero)
        {
            HeroAddDataOwnershipRegistry.EnterCountHeroData(hero);
        }

        [HarmonyFinalizer]
        public static void Finalizer(HeroData hero)
        {
            HeroAddDataOwnershipRegistry.ExitCountHeroData(hero);
        }
    }

    [HarmonyPatch(typeof(HeroSpeAddData), "Get", new Type[]
    {
        typeof(int)
    })]
    internal static class ReinforceHeroSpeAddDataGetPatch
    {
        // 当前实现优先选择 Get 路线：
        // 1. totalAddData.Get(57/59) 能直接影响 CountHeroData 末段的生命与内力上限公式；
        // 2. baseAddData.Get(57/59) 也会一起反映六维带来的基础额外值。
        //
        // 另一套备选方案是在 CountHeroData 内部拦截 HeroSpeAddData__op_Addition(totalAddData, baseAddData)：
        // 只在 baseAddData 并入 totalAddData 的那一步，把六维加成并入合并结果。
        // 那条线更偏“基础层合并”语义，也有不错的扩展性；
        // 但它天然覆盖不到 baseAddData.Get(57/59) 这类直接读取，所以当前版本仍以 Get 方案为主。
        [HarmonyPostfix]
        public static void Postfix(HeroSpeAddData __instance, int speAddDataType, ref float __result)
        {
            if (speAddDataType != AttributeReinforceRuntime.MaxHpAddType &&
                speAddDataType != AttributeReinforceRuntime.MaxManaAddType)
            {
                return;
            }

            HeroData hero = HeroAddDataOwnershipRegistry.Resolve(__instance);
            if (hero == null)
            {
                return;
            }

            AttributeReinforceRuntime.TryApplyExtraResourceGet(hero, speAddDataType, ref __result);
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
            HeroAddDataOwnershipRegistry.Register(__result);
            AttributeReinforceRuntime.FillGeneratedHeroCurrentResources(__result);
        }
    }

    [HarmonyPatch(typeof(GameController), "StartNewGameSetting")]
    internal static class ReinforceStartNewGameSettingPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameController __instance)
        {
            // 先停用新开档主角的当前血蓝同步，观察仅靠 Get 注入时的原版表现。
            // AttributeReinforceRuntime.RefreshNewGamePlayerCurrentResources(__instance);
            if (__instance != null && __instance.worldData != null)
            {
                try
                {
                    HeroAddDataOwnershipRegistry.Register(__instance.worldData.Player());
                }
                catch
                {
                }
            }
        }
    }

}
