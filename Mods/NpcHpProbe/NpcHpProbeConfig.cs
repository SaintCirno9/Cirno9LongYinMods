using MelonLoader;
using MelonLoader.Utils;

namespace NpcHpProbe
{
    internal static class NpcHpProbeConfig
    {
        private static MelonPreferences_Category _category = null!;
        private static bool _initialized;

        internal static string ConfigFilePath { get; } = $"{MelonEnvironment.UserDataDirectory}\\NpcHpProbe.cfg";

        internal static MelonPreferences_Entry<bool> TrackPlayer { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> VerboseLog { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogCountHeroData { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogChangeHp { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogRecoverFlow { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogAiFlow { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogAiExceptions { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> LogRecoverRate { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> TrackUnhealedHeroes { get; private set; } = null!;
        internal static MelonPreferences_Entry<float> UnhealedHpRatioThreshold { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> CaptureStackTraceOnAnomaly { get; private set; } = null!;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _category = MelonPreferences.CreateCategory("NpcHpProbe", "NpcHpProbe");
            _category.SetFilePath(ConfigFilePath);

            TrackPlayer = _category.CreateEntry("TrackPlayer", false, description: "是否连玩家一起监控");
            VerboseLog = _category.CreateEntry("VerboseLog", false, description: "是否输出详细调试日志");
            LogCountHeroData = _category.CreateEntry("LogCountHeroData", false, description: "是否记录 CountHeroData 详细异常");
            LogChangeHp = _category.CreateEntry("LogChangeHp", false, description: "是否记录 ChangeHp 详细异常");
            LogRecoverFlow = _category.CreateEntry("LogRecoverFlow", false, description: "是否记录日常回血流程详细异常");
            LogAiFlow = _category.CreateEntry("LogAiFlow", false, description: "是否记录 NPC 日常 AI 详细流程");
            LogAiExceptions = _category.CreateEntry("LogAiExceptions", true, description: "是否记录关键 AI 异常及对应角色");
            LogRecoverRate = _category.CreateEntry("LogRecoverRate", false, description: "是否记录恢复率详细异常");
            TrackUnhealedHeroes = _category.CreateEntry("TrackUnhealedHeroes", false, description: "是否追踪未满血但没有恢复的角色");
            UnhealedHpRatioThreshold = _category.CreateEntry("UnhealedHpRatioThreshold", 0.95f, description: "只跟踪生命比例不高于该阈值的角色");
            CaptureStackTraceOnAnomaly = _category.CreateEntry("CaptureStackTraceOnAnomaly", true, description: "异常时是否附带托管堆栈");

            if (UnhealedHpRatioThreshold.Value <= 0f || UnhealedHpRatioThreshold.Value > 1f)
            {
                UnhealedHpRatioThreshold.Value = 0.95f;
            }

            Save();
        }

        internal static void Save()
        {
            if (!_initialized)
            {
                return;
            }

            _category.SaveToFile();
        }
    }
}
