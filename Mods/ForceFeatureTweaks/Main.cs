using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(ForceFeatureTweaks.Main), "ForceFeatureTweaks", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace ForceFeatureTweaks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("ForceFeatureTweaks 已加载。");
        }
    }

    internal static class ForceFeatureRuntime
    {
        private const int EightTrigramsForceId = 22;
        private const int HaiShaForceId = 23;
        private const int SwordExpType = 183;
        private const int QimenExpType = 186;
        private const int BoxingExpType = 182;
        private const int JuejiExpType = 181;

        private const float OriginalExpBonus = 0.2f;
        private const float ReplacedExpBonus = 1.5f;

        internal static void AdjustHeroForceFeature(HeroData hero)
        {
            if (hero == null || hero.totalAddData == null)
            {
                return;
            }

            HeroSpeAddData totalAddData = hero.totalAddData;

            if (hero.HaveForceFunction(EightTrigramsForceId))
            {
                totalAddData.Change(QimenExpType, -OriginalExpBonus);
                totalAddData.Change(SwordExpType, ReplacedExpBonus);
            }

            if (hero.HaveForceFunction(HaiShaForceId))
            {
                totalAddData.Change(JuejiExpType, -OriginalExpBonus);
                totalAddData.Change(BoxingExpType, ReplacedExpBonus);
            }
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.CountHeroData))]
    internal static class GameController_CountHeroData_Patch
    {
        private static void Postfix(HeroData hero)
        {
            ForceFeatureRuntime.AdjustHeroForceFeature(hero);
        }
    }
}
