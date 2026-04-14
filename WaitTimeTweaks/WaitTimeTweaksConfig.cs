using MelonLoader;
using MelonLoader.Utils;

namespace WaitTimeTweaks
{
    internal static class WaitTimeTweaksConfig
    {
        private const int DefaultMaxWaitDays = 360;
        private const int MinMaxWaitDays = 31;
        private const int MaxMaxWaitDays = 9999;
        private const int DefaultSliderWidth = 720;
        private const int MinSliderWidth = 360;
        private const int MaxSliderWidth = 4000;

        private static MelonPreferences_Category _category = null!;
        private static bool _initialized;

        internal static string ConfigFilePath { get; } = $"{MelonEnvironment.UserDataDirectory}\\WaitTimeTweaks.cfg";

        internal static MelonPreferences_Entry<bool> EnableMod { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> MaxWaitDays { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> SliderWidth { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> VerboseLog { get; private set; } = null!;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _category = MelonPreferences.CreateCategory("WaitTimeTweaks", "WaitTimeTweaks");
            _category.SetFilePath(ConfigFilePath);

            EnableMod = _category.CreateEntry("EnableMod", true, description: "是否启用等待滑条扩展");
            MaxWaitDays = _category.CreateEntry("MaxWaitDays", DefaultMaxWaitDays, description: "等待滑条允许选择的最大天数");
            SliderWidth = _category.CreateEntry("SliderWidth", DefaultSliderWidth, description: "等待滑条的显示宽度");
            VerboseLog = _category.CreateEntry("VerboseLog", false, description: "是否输出详细日志");

            NormalizeValues();
            Save();
        }

        internal static void Save()
        {
            if (_initialized)
            {
                _category.SaveToFile();
            }
        }

        private static void NormalizeValues()
        {
            if (MaxWaitDays.Value < MinMaxWaitDays || MaxWaitDays.Value > MaxMaxWaitDays)
            {
                MaxWaitDays.Value = DefaultMaxWaitDays;
            }

            if (SliderWidth.Value < MinSliderWidth || SliderWidth.Value > MaxSliderWidth)
            {
                SliderWidth.Value = DefaultSliderWidth;
            }
        }
    }
}
