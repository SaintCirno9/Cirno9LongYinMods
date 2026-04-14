using MelonLoader;

[assembly: MelonInfo(typeof(WaitTimeTweaks.Main), "WaitTimeTweaks", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace WaitTimeTweaks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static bool VerboseLog => WaitTimeTweaksConfig.VerboseLog.Value;

        public override void OnInitializeMelon()
        {
            WaitTimeTweaksConfig.Initialize();

            Log.Msg(
                $"WaitTimeTweaks 已加载。配置文件：{WaitTimeTweaksConfig.ConfigFilePath}。最大等待天数={WaitTimeTweaksConfig.MaxWaitDays.Value}，滑条宽度={WaitTimeTweaksConfig.SliderWidth.Value}，数字显示=中文，VerboseLog={(VerboseLog ? "开" : "关")}。");
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLog)
            {
                Log.Msg(message);
            }
        }
    }
}
