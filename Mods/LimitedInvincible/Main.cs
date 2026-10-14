using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(LimitedInvincible.Main), "LimitedInvincible", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LimitedInvincible
{
    public sealed class Main : MelonMod
    {
        internal const int InvincibleBuffId = (int)HeroSpeAddDataType.invincible;
        internal const int DefaultHitsPerInvincibleCount = 3;

        private static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LimitedInvincible.cfg"));

        private static readonly object SyncRoot = new();
        private static readonly Dictionary<int, InvincibleCounterState> InvincibleStates = new();

        private static ModConfig Config = ModConfig.CreateDefault();

        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static bool EnableLimitedInvincible => Config.EnableLimitedInvincible;
        internal static bool EnableAiIgnoreInvincible => Config.EnableAiIgnoreInvincible;
        internal static bool VerboseLogEnabled => Config.VerboseLog;
        internal static int HitsPerInvincibleCount => Math.Max(1, Config.InvincibleHitsPerLayer);

        public override void OnInitializeMelon()
        {
            LoadConfig();
            AiIgnoreInvincibleNativePatch.TryInstall(Log);
            Log.Msg(
                $"LimitedInvincible 已加载。配置文件：{ConfigPath}。次数版无敌={FormatEnabled(EnableLimitedInvincible)}，每层抵挡次数={HitsPerInvincibleCount}，AI 无视无敌={FormatEnabled(EnableAiIgnoreInvincible)}，VerboseLog={FormatEnabled(VerboseLogEnabled)}。");
        }

        public override void OnDeinitializeMelon()
        {
            AiIgnoreInvincibleNativePatch.TryRestore(Log);
        }

        internal static void ClearRuntimeState()
        {
            lock (SyncRoot)
            {
                InvincibleStates.Clear();
            }
        }

        internal static void RegisterInvincible(HeroData hero, float rawValue)
        {
            if (!EnableLimitedInvincible || hero == null)
            {
                return;
            }

            int hitCount = ResolveInvincibleHitCount(rawValue);
            if (hitCount <= 0)
            {
                return;
            }

            int heroId = hero.heroID;
            lock (SyncRoot)
            {
                InvincibleStates[heroId] = new InvincibleCounterState
                {
                    RemainingHits = hitCount,
                    PendingClear = false,
                };
            }

            LogVerbose(
                $"已记录无敌次数：{GetHeroDisplayName(hero)}，次数={hitCount}，原版无敌参数={rawValue:0.###}（原版用于持续时间，这里仅作为次数换算依据）。");
        }

        internal static int ResolveInvincibleHitCount(float rawValue)
        {
            if (rawValue <= 0f)
            {
                return 0;
            }

            // rawValue 是原版 AddBuff 传入的无敌持续时间参数。
            // 这里按正整数层数处理，再乘每层可抵挡次数。
            int baseCount = Math.Max(1, (int)Math.Ceiling(rawValue));

            return baseCount * HitsPerInvincibleCount;
        }

        internal static bool TryConsumeInvincible(HeroData hero, out PendingInvincibleConsume consume)
        {
            consume = default;
            if (!EnableLimitedInvincible || hero == null)
            {
                return false;
            }

            float currentBuffValue = GetCurrentInvincibleBuffValue(hero);
            if (currentBuffValue <= 0f)
            {
                return false;
            }

            int heroId = hero.heroID;
            lock (SyncRoot)
            {
                if (!InvincibleStates.TryGetValue(heroId, out InvincibleCounterState state))
                {
                    state = new InvincibleCounterState
                    {
                        RemainingHits = ResolveInvincibleHitCount(currentBuffValue),
                        PendingClear = false,
                    };
                }

                if (state.PendingClear || state.RemainingHits <= 0)
                {
                    return false;
                }

                state.RemainingHits--;
                if (state.RemainingHits <= 0)
                {
                    state.RemainingHits = 0;
                    state.PendingClear = true;
                }

                InvincibleStates[heroId] = state;
                consume = new PendingInvincibleConsume
                {
                    Triggered = true,
                    RemainingHits = state.RemainingHits,
                    ShouldClearAfterCall = state.PendingClear,
                };
                return true;
            }
        }

        internal static void ClearInvincibleStateIfBuffExpired(HeroData hero)
        {
            if (hero == null)
            {
                return;
            }

            bool removed = false;
            lock (SyncRoot)
            {
                if (!EnableLimitedInvincible || GetCurrentInvincibleBuffValue(hero) <= 0f)
                {
                    removed = InvincibleStates.Remove(hero.heroID);
                }
            }

            if (removed && EnableLimitedInvincible)
            {
                LogVerbose($"无敌自然结束，已清除次数：{GetHeroDisplayName(hero)}。");
            }
        }

        internal static void FinalizeInvincibleConsume(HeroData hero)
        {
            if (!EnableLimitedInvincible || hero == null)
            {
                return;
            }

            bool shouldRemove;
            lock (SyncRoot)
            {
                shouldRemove = InvincibleStates.TryGetValue(hero.heroID, out InvincibleCounterState state) && state.PendingClear;
                if (shouldRemove)
                {
                    InvincibleStates.Remove(hero.heroID);
                }
            }

            if (!shouldRemove)
            {
                return;
            }

            RemoveInvincibleBuff(hero);
            LogVerbose($"无敌次数耗尽，已清除 buff：{GetHeroDisplayName(hero)}。");
        }

        internal static void RemoveInvincibleBuff(HeroData hero)
        {
            if (hero?.heroBuff?.heroSpeAddData == null)
            {
                return;
            }

            if (hero.heroBuff.heroSpeAddData.Remove(InvincibleBuffId))
            {
                MarkBuffDirty(hero);
            }
        }

        internal static string GetHeroDisplayName(HeroData hero)
        {
            if (hero == null)
            {
                return "未知角色";
            }

            string heroName = hero.heroName?.ToString();
            if (!string.IsNullOrWhiteSpace(heroName))
            {
                return heroName;
            }

            return $"heroID={hero.heroID}";
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLogEnabled)
            {
                Log.Msg(message);
            }
        }

        private static float GetCurrentInvincibleBuffValue(HeroData hero)
        {
            if (hero?.heroBuff?.heroSpeAddData == null)
            {
                return 0f;
            }

            return hero.heroBuff.heroSpeAddData.TryGetValue(InvincibleBuffId, out float value) ? value : 0f;
        }

        private static void MarkBuffDirty(HeroData hero)
        {
            if (hero == null)
            {
                return;
            }

            hero.heroDetailDirty = true;
            hero.heroBuffDirty = true;
        }

        private static void LoadConfig()
        {
            ModConfig loadedConfig = ModConfig.CreateDefault();
            bool rewriteConfig = !File.Exists(ConfigPath);

            if (!rewriteConfig)
            {
                try
                {
                    ParseConfig(File.ReadAllLines(ConfigPath), loadedConfig, out bool parseRequestedRewrite);
                    rewriteConfig = parseRequestedRewrite;
                }
                catch (Exception ex)
                {
                    Log.Warning($"读取配置失败，将回退为默认配置并重写文件：{ex.Message}");
                    loadedConfig = ModConfig.CreateDefault();
                    rewriteConfig = true;
                }
            }

            Config = loadedConfig;

            if (rewriteConfig)
            {
                SaveConfig();
                Log.Msg($"已写入默认或规范化后的配置文件：{ConfigPath}");
            }
        }

        private static void ParseConfig(string[] lines, ModConfig config, out bool rewriteConfig)
        {
            rewriteConfig = false;

            bool seenEnableLimitedInvincible = false;
            bool seenEnableAiIgnoreInvincible = false;
            bool seenVerboseLog = false;
            bool seenInvincibleHitsPerLayer = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    rewriteConfig = true;
                    continue;
                }

                string key = line[..separatorIndex].Trim();
                string valueText = line[(separatorIndex + 1)..].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "enablelimitedinvincible":
                    case "enable_limited_invincible":
                        if (!TryParseBool(valueText, out bool enableLimitedInvincible))
                        {
                            Log.Warning($"配置项 `{key}` 的值 `{valueText}` 无法识别，将使用当前配置值。");
                            rewriteConfig = true;
                            continue;
                        }

                        config.EnableLimitedInvincible = enableLimitedInvincible;
                        seenEnableLimitedInvincible = true;
                        break;

                    case "enableaiignoreinvincible":
                    case "enable_ai_ignore_invincible":
                        if (!TryParseBool(valueText, out bool enableAiIgnoreInvincible))
                        {
                            Log.Warning($"配置项 `{key}` 的值 `{valueText}` 无法识别，将使用当前配置值。");
                            rewriteConfig = true;
                            continue;
                        }

                        config.EnableAiIgnoreInvincible = enableAiIgnoreInvincible;
                        seenEnableAiIgnoreInvincible = true;
                        break;

                    case "verboselog":
                    case "verbose_log":
                        if (!TryParseBool(valueText, out bool verboseLog))
                        {
                            Log.Warning($"配置项 `{key}` 的值 `{valueText}` 无法识别，将使用当前配置值。");
                            rewriteConfig = true;
                            continue;
                        }

                        config.VerboseLog = verboseLog;
                        seenVerboseLog = true;
                        break;

                    case "invinciblehitsperlayer":
                    case "invincible_hits_per_layer":
                        if (!TryParsePositiveInt(valueText, out int invincibleHitsPerLayer))
                        {
                            Log.Warning($"配置项 `{key}` 的值 `{valueText}` 无法识别，将使用当前配置值。");
                            rewriteConfig = true;
                            continue;
                        }

                        config.InvincibleHitsPerLayer = Math.Max(1, invincibleHitsPerLayer);
                        seenInvincibleHitsPerLayer = true;
                        break;
                }
            }

            if (!seenEnableLimitedInvincible || !seenEnableAiIgnoreInvincible || !seenVerboseLog || !seenInvincibleHitsPerLayer)
            {
                rewriteConfig = true;
            }
        }

        private static void SaveConfig()
        {
            string configDirectory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            File.WriteAllText(ConfigPath, BuildConfigText(Config));
        }

        private static string BuildConfigText(ModConfig config)
        {
            return string.Join(
                       Environment.NewLine,
                       new[]
                       {
                           "# LimitedInvincible 配置文件",
                           "# true/false、1/0、on/off、yes/no 都可识别。",
                           "# EnableLimitedInvincible: 是否启用“无敌改为抵挡次数”逻辑。",
                           "# InvincibleHitsPerLayer: 每层无敌可抵挡多少次伤害，最小值为 1。",
                           "# EnableAiIgnoreInvincible: AI 规划时是否忽略目标无敌，不再因此放弃攻击。",
                           "# VerboseLog: 是否输出详细控制台日志。",
                           $"EnableLimitedInvincible={config.EnableLimitedInvincible.ToString().ToLowerInvariant()}",
                           $"InvincibleHitsPerLayer={Math.Max(1, config.InvincibleHitsPerLayer)}",
                           $"EnableAiIgnoreInvincible={config.EnableAiIgnoreInvincible.ToString().ToLowerInvariant()}",
                           $"VerboseLog={config.VerboseLog.ToString().ToLowerInvariant()}",
                       })
                   + Environment.NewLine;
        }

        private static bool TryParseBool(string valueText, out bool result)
        {
            switch (valueText.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "on":
                case "yes":
                    result = true;
                    return true;

                case "0":
                case "false":
                case "off":
                case "no":
                    result = false;
                    return true;

                default:
                    result = false;
                    return false;
            }
        }

        private static bool TryParsePositiveInt(string valueText, out int result)
        {
            if (int.TryParse(valueText.Trim(), out result) && result > 0)
            {
                return true;
            }

            result = 0;
            return false;
        }

        private static string FormatEnabled(bool value)
        {
            return value ? "开" : "关";
        }
    }

    internal sealed class ModConfig
    {
        public bool EnableLimitedInvincible { get; set; } = true;

        public int InvincibleHitsPerLayer { get; set; } = Main.DefaultHitsPerInvincibleCount;

        public bool EnableAiIgnoreInvincible { get; set; } = true;

        public bool VerboseLog { get; set; }

        public static ModConfig CreateDefault()
        {
            return new ModConfig();
        }
    }

    internal sealed class InvincibleCounterState
    {
        public int RemainingHits { get; set; }

        public bool PendingClear { get; set; }
    }

    internal struct PendingInvincibleConsume
    {
        public bool Triggered;
        public int RemainingHits;
        public bool ShouldClearAfterCall;
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.StartBattle))]
    internal static class BattleControllerStartBattlePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Main.ClearRuntimeState();
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.BattleRealEnd))]
    internal static class BattleControllerBattleRealEndPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            Main.ClearRuntimeState();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.AddBuff))]
    internal static class HeroDataAddBuffPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroData __instance, int id, float time)
        {
            if (__instance == null || id != Main.InvincibleBuffId)
            {
                return;
            }

            Main.RegisterInvincible(__instance, time);
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.ManageHeroBuffRound))]
    internal static class BattleControllerManageHeroBuffRoundPatch
    {
        [HarmonyPostfix]
        private static void Postfix(BattleUnit targetHero)
        {
            Main.ClearInvincibleStateIfBuffExpired(targetHero?.heroData);
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.ManageHeroBuffTime))]
    internal static class BattleControllerManageHeroBuffTimePatch
    {
        [HarmonyPostfix]
        private static void Postfix(BattleUnit targetHero)
        {
            Main.ClearInvincibleStateIfBuffExpired(targetHero?.heroData);
        }
    }

    [HarmonyPatch(typeof(BattleUnit), nameof(BattleUnit.ChangeHp))]
    internal static class BattleUnitChangeHpPatch
    {
        // TODO: 有玩家反馈部分召唤物造成的伤害不会消耗无敌次数。
        // 当前 Mod 只拦截 BattleUnit.ChangeHp / ChangeTrueHp，后续需要继续核实
        // 召唤物伤害是否存在绕过这两个入口的特殊扣血链路。
        [HarmonyPrefix]
        private static void Prefix(BattleUnit __instance, float num, ref PendingInvincibleConsume __state)
        {
            if (__instance == null || num >= 0f || !Main.TryConsumeInvincible(__instance.heroData, out __state))
            {
                return;
            }

            Main.LogVerbose($"无敌触发生命抵挡：{Main.GetHeroDisplayName(__instance.heroData)}，剩余 {__state.RemainingHits} 次。");
        }

        [HarmonyPostfix]
        private static void Postfix(BattleUnit __instance, PendingInvincibleConsume __state)
        {
            if (!__state.Triggered || !__state.ShouldClearAfterCall || __instance == null)
            {
                return;
            }

            Main.FinalizeInvincibleConsume(__instance.heroData);
            __instance.CheckInvincibleEffect();
        }
    }

    [HarmonyPatch(typeof(BattleUnit), nameof(BattleUnit.ChangeTrueHp))]
    internal static class BattleUnitChangeTrueHpPatch
    {
        [HarmonyPrefix]
        private static void Prefix(BattleUnit __instance, float num, ref PendingInvincibleConsume __state)
        {
            if (__instance == null || num >= 0f || !Main.TryConsumeInvincible(__instance.heroData, out __state))
            {
                return;
            }

            Main.LogVerbose($"无敌触发真实伤害抵挡：{Main.GetHeroDisplayName(__instance.heroData)}，剩余 {__state.RemainingHits} 次。");
        }

        [HarmonyPostfix]
        private static void Postfix(BattleUnit __instance, PendingInvincibleConsume __state)
        {
            if (!__state.Triggered || !__state.ShouldClearAfterCall || __instance == null)
            {
                return;
            }

            Main.FinalizeInvincibleConsume(__instance.heroData);
            __instance.CheckInvincibleEffect();
        }
    }
}
