using MelonLoader;

[assembly: MelonInfo(typeof(FoodAutomation.Main), "FoodAutomation", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace FoodAutomation
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            FoodAutomationConfig.Initialize();
            Log.Msg("FoodAutomation 已加载。");
        }
    }
}
