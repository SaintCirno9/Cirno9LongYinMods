using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PotentialCapUnlock.Main), "PotentialCapUnlock", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace PotentialCapUnlock
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static bool EnableCapOverride => PotentialCapRuntime.Config.EnableCapOverride;

        internal static bool VerboseLog => PotentialCapRuntime.Config.VerboseLog;

        public override void OnInitializeMelon()
        {
            PotentialCapRuntime.Initialize(PotentialCapConfigStore.Load(Log));

            Log.Msg(
                $"PotentialCapUnlock 已加载。配置文件：{PotentialCapConfigStore.ConfigPath}。硬上限修改={FormatEnabled(EnableCapOverride)}，六维潜力上限={PotentialCapRuntime.ConfiguredAttriCap}，武学潜力上限={PotentialCapRuntime.ConfiguredFightSkillCap}，技艺潜力上限={PotentialCapRuntime.ConfiguredLivingSkillCap}，VerboseLog={FormatEnabled(VerboseLog)}。");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            PotentialCapRuntime.EnsureCapsApplied();
            LogVerbose($"场景加载后已检查潜力上限配置：scene={sceneName}，buildIndex={buildIndex}。");
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLog)
            {
                Log.Msg(message);
            }
        }

        private static string FormatEnabled(bool value)
        {
            return value ? "开" : "关";
        }
    }

    internal static class PotentialCapRuntime
    {
        private const int VanillaAttriCap = 120;
        private const int VanillaFightSkillCap = 120;
        private const int VanillaLivingSkillCap = 100;

        private static PotentialCapConfig _config = PotentialCapConfig.CreateDefault();

        internal static PotentialCapConfig Config => _config;

        internal static bool IsCapOverrideEnabled => _config.EnableCapOverride;

        internal static int ConfiguredAttriCap => SanitizeCap(_config.AttriCap, PotentialCapConfig.DefaultAttriCap);

        internal static int ConfiguredFightSkillCap => SanitizeCap(_config.FightSkillCap, PotentialCapConfig.DefaultFightSkillCap);

        internal static int ConfiguredLivingSkillCap => SanitizeCap(_config.LivingSkillCap, PotentialCapConfig.DefaultLivingSkillCap);

        internal static void Initialize(PotentialCapConfig config)
        {
            _config = config ?? PotentialCapConfig.CreateDefault();
        }

        internal static void EnsureCapsApplied()
        {
            int attriCap = IsCapOverrideEnabled ? ConfiguredAttriCap : VanillaAttriCap;
            int fightSkillCap = IsCapOverrideEnabled ? ConfiguredFightSkillCap : VanillaFightSkillCap;
            int livingSkillCap = IsCapOverrideEnabled ? ConfiguredLivingSkillCap : VanillaLivingSkillCap;

            bool changed =
                GlobalData.HeroMaxAttriNum != attriCap ||
                GlobalData.HeroMaxFightSkillNum != fightSkillCap ||
                GlobalData.HeroMaxLivingSkillNum != livingSkillCap;

            if (!changed)
            {
                return;
            }

            GlobalData.HeroMaxAttriNum = attriCap;
            GlobalData.HeroMaxFightSkillNum = fightSkillCap;
            GlobalData.HeroMaxLivingSkillNum = livingSkillCap;

            if (IsCapOverrideEnabled)
            {
                Main.LogVerbose(
                    $"已覆盖原版潜力上限：六维={GlobalData.HeroMaxAttriNum}，武学={GlobalData.HeroMaxFightSkillNum}，技艺={GlobalData.HeroMaxLivingSkillNum}。");
                return;
            }

            Main.LogVerbose(
                $"硬上限修改已关闭，已恢复原版潜力上限：六维={GlobalData.HeroMaxAttriNum}，武学={GlobalData.HeroMaxFightSkillNum}，技艺={GlobalData.HeroMaxLivingSkillNum}。");
        }

        private static int SanitizeCap(int value, int fallback)
        {
            return value > 0 ? value : fallback;
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetMaxAttri), new[] { typeof(int) })]
    internal static class HeroData_GetMaxAttri_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetMaxFightSkill), new[] { typeof(int) })]
    internal static class HeroData_GetMaxFightSkill_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetMaxLivingSkill), new[] { typeof(int) })]
    internal static class HeroData_GetMaxLivingSkill_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.RefreshMaxAttriAndSkill))]
    internal static class HeroData_RefreshMaxAttriAndSkill_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeMaxAttri), new[] { typeof(int), typeof(int), typeof(bool) })]
    internal static class HeroData_ChangeMaxAttri_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeMaxFightSkill), new[] { typeof(int), typeof(int), typeof(bool) })]
    internal static class HeroData_ChangeMaxFightSkill_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeMaxLivingSkill), new[] { typeof(int), typeof(int), typeof(bool) })]
    internal static class HeroData_ChangeMaxLivingSkill_Patch
    {
        private static void Prefix()
        {
            PotentialCapRuntime.EnsureCapsApplied();
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.FreshNowHeroDetail), new[] { typeof(HeroData), typeof(bool) })]
    internal static class HeroDetailController_FreshNowHeroDetail_Patch
    {
        private static void Postfix(HeroDetailController __instance)
        {
            if (__instance?.heroDetailPanel != null)
            {
                UiBarPatchHelper.AdjustHeroDetailBars(__instance.heroDetailPanel.transform);
            }
        }
    }

    [HarmonyPatch(typeof(StartMenuController), nameof(StartMenuController.RefreshPlayerAttri))]
    internal static class StartMenuController_RefreshPlayerAttri_Patch
    {
        private static void Postfix(StartMenuController __instance)
        {
            if (__instance?.attriRoot != null)
            {
                UiBarPatchHelper.AdjustUniformBars(__instance.attriRoot.transform, UiBarVisualKind.AttriOrFight);
            }
        }
    }
}
