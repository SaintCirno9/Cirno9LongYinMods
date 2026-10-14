using MelonLoader;

[assembly: MelonInfo(typeof(NoGenderLimit.Main), "NoGenderLimit", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace NoGenderLimit
{
    public class Main : MelonMod
    {
        public static readonly MelonLogger.Instance Log = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("NoGenderLimit 已加载。");
        }
    }
}
