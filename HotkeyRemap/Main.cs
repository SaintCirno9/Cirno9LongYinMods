using MelonLoader;
using HarmonyLib;
using System;
using System.Reflection;

[assembly: MelonInfo(typeof(HotkeyRemap.Main), "HotkeyRemap", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace HotkeyRemap
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static HotkeyRemapConfig Config { get; private set; } = null!;

        public override void OnInitializeMelon()
        {
            Config = HotkeyRemapConfigStore.Load(Log);
            BattleHotkeyListHelper.TryApplyConfiguredLists();
            HudHeroDetailNativePatch.TryInstall(Log);
            LogSettingMenuPatchTarget();

            Log.Msg($"HotkeyRemap 已加载。配置文件：{HotkeyRemapConfigStore.ConfigPathDisplay}");
            Log.Msg(
                $"全局映射={Config.EnableGlobalRemap}，剧情推进映射={Config.EnablePlotAdvanceRemap}，UI 导航映射={Config.EnableUiNavigationRemap}，详细日志={Config.VerboseLog}");
        }

        public override void OnUpdate()
        {
            HotkeySettingsMenuUi.HandleInput();
        }

        public override void OnDeinitializeMelon()
        {
            HudHeroDetailNativePatch.TryRestore(Log);
        }

        private static void LogSettingMenuPatchTarget()
        {
            try
            {
                Type type = AccessTools.TypeByName("SettingMenuController");
                MethodBase method = type == null ? null : AccessTools.Method(type, "ShowSettingMenu");
                Log.Msg($"设置菜单补丁目标：type={(type == null ? "<null>" : type.FullName)}，method={(method == null ? "<null>" : method.Name)}");
            }
            catch (Exception ex)
            {
                Log.Warning($"检查设置菜单补丁目标失败：{ex.Message}");
            }
        }
    }
}
