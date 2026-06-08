using MelonLoader;

[assembly: MelonInfo(typeof(NpcHpProbe.Main), "NpcHpProbe", "1.2.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace NpcHpProbe
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            NpcHpProbeConfig.Initialize();

            Log.Msg("NpcHpProbe 已加载。");
            Log.Msg($"配置文件：{NpcHpProbeConfig.ConfigFilePath}");
            Log.Msg(
                $"监控开关：TrackPlayer={NpcHpProbeConfig.TrackPlayer.Value}, " +
                $"VerboseLog={NpcHpProbeConfig.VerboseLog.Value}, " +
                $"LogCountHeroData={NpcHpProbeConfig.LogCountHeroData.Value}, " +
                $"LogChangeHp={NpcHpProbeConfig.LogChangeHp.Value}, " +
                $"LogRecoverFlow={NpcHpProbeConfig.LogRecoverFlow.Value}, " +
                $"LogAiFlow={NpcHpProbeConfig.LogAiFlow.Value}, " +
                $"LogAiExceptions={NpcHpProbeConfig.LogAiExceptions.Value}, " +
                $"LogRecoverRate={NpcHpProbeConfig.LogRecoverRate.Value}, " +
                $"TrackUnhealedHeroes={NpcHpProbeConfig.TrackUnhealedHeroes.Value}, " +
                $"UnhealedHpRatioThreshold={NpcHpProbeConfig.UnhealedHpRatioThreshold.Value:0.##}, " +
                $"CaptureStackTraceOnAnomaly={NpcHpProbeConfig.CaptureStackTraceOnAnomaly.Value}");
        }
    }
}
