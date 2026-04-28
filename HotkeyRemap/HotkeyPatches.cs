using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace HotkeyRemap
{
    internal static class HotkeyRemapService
    {
        internal static bool TryMapGlobalKey(KeyCode original, out KeyCode mapped)
        {
            HotkeyRemapConfig config = Main.Config;
            mapped = original;

            if (!config.EnableGlobalRemap)
            {
                return false;
            }

            switch (original)
            {
                case KeyCode.F5:
                    mapped = config.QuickSave;
                    return true;
                case KeyCode.F9:
                    mapped = config.QuickLoad;
                    return true;
                case KeyCode.Tab:
                    mapped = config.MissionToggle;
                    return true;
                case KeyCode.Space:
                    mapped = config.BigMapRest;
                    return true;
                case KeyCode.I:
                    mapped = config.ShowMap;
                    return true;
                case KeyCode.O:
                    mapped = config.SectMenu;
                    return true;
                case KeyCode.P:
                    mapped = config.Roster;
                    return true;
                case KeyCode.B:
                    mapped = config.ReturnSect;
                    return true;
                case KeyCode.N:
                    mapped = config.FocusSelf;
                    return true;
                case KeyCode.M:
                    mapped = config.HorseSprint;
                    return true;
                case KeyCode.F2:
                    mapped = config.BattleAutoToggle;
                    return true;
                case KeyCode.W:
                    mapped = config.StudyUp;
                    return true;
                case KeyCode.S:
                    mapped = config.StudyDown;
                    return true;
                case KeyCode.A:
                    mapped = config.StudyLeft;
                    return true;
                case KeyCode.D:
                    mapped = config.StudyRight;
                    return true;
                default:
                    return false;
            }
        }

        internal static bool ShouldHandlePlotAdvance(PlotController controller)
        {
            return Main.Config.EnablePlotAdvanceRemap
                && controller != null
                && controller.plotHappen
                && controller.plotTextShowing
                && controller.plotBlockTime <= 0f
                && Main.Config.PlotAdvance != KeyCode.Space;
        }

        internal static void TryHandleHudHotkeys(HudController controller)
        {
            if (!Main.Config.EnableGlobalRemap || controller == null)
            {
                return;
            }

            TryHandleHudHeroDetailHotkey(controller);
            TryHandleLeaveCityHotkey();
        }

        private static void TryHandleHudHeroDetailHotkey(HudController controller)
        {
            KeyCode mappedKey = Main.Config.HeroDetail;
            if (mappedKey == KeyCode.Q || !Input.GetKeyDown(mappedKey))
            {
                return;
            }

            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"检测到人物详情热键：{mappedKey}。");
            }

            GameObject heroFace = controller.heroFace;
            if (heroFace == null)
            {
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Warning("人物头像对象为空，无法处理人物详情热键。");
                }

                return;
            }

            HeroDetailController heroDetailController = HeroDetailController.Instance;
            if (heroDetailController?.heroDetailPanel != null && heroDetailController.heroDetailPanel.activeSelf)
            {
                if (TradeUIController.Instance?.tradeUI != null && TradeUIController.Instance.tradeUI.activeSelf)
                {
                    return;
                }

                if (ItemUseMenuController.Instance?.itemUseMenuPanel != null && ItemUseMenuController.Instance.itemUseMenuPanel.activeSelf)
                {
                    return;
                }

                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg("人物详情热键将关闭当前人物详情。");
                }

                heroDetailController.UnshowHeroDetail();
                return;
            }

            if (!heroFace.activeInHierarchy)
            {
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null || gameController.HaveSpeUI(true))
            {
                return;
            }

            ShowHeroDetail showHeroDetail = heroFace.GetComponent<ShowHeroDetail>();
            if (showHeroDetail == null)
            {
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Warning("人物头像上未找到 ShowHeroDetail 组件。");
                }

                return;
            }

            try
            {
                if (Main.Config.VerboseLog)
                {
                    Main.Log.Msg("人物详情热键将打开主角详情。");
                }

                showHeroDetail.OnClick();
            }
            catch (System.Exception ex)
            {
                Main.Log.Warning($"人物详情热键执行失败，已忽略本次输入：{ex.Message}");
            }
        }

        private static void TryHandleLeaveCityHotkey()
        {
            KeyCode mappedKey = Main.Config.LeaveCity;
            if (mappedKey == KeyCode.None || !Input.GetKeyDown(mappedKey))
            {
                return;
            }

            if (GlobalData.IsInputing())
            {
                return;
            }

            if (BigMapController.Instance?.bigmapUIPanel != null && BigMapController.Instance.bigmapUIPanel.activeSelf)
            {
                return;
            }

            if (GameController.Instance?.HaveSpeUI(true) == true)
            {
                return;
            }

            if (TradeUIController.Instance?.tradeUI != null && TradeUIController.Instance.tradeUI.activeSelf)
            {
                return;
            }

            if (ItemUseMenuController.Instance?.itemUseMenuPanel != null && ItemUseMenuController.Instance.itemUseMenuPanel.activeSelf)
            {
                return;
            }

            if (HeroDetailController.Instance?.heroDetailPanel != null && HeroDetailController.Instance.heroDetailPanel.activeSelf)
            {
                return;
            }

            AreaController areaController = AreaController.Instance;
            if (areaController?.areaData == null)
            {
                return;
            }

            if (Main.Config.VerboseLog)
            {
                Main.Log.Msg($"检测到离开城市热键：{mappedKey}。");
            }

            areaController.ReturnBigMapButtonClicked();
        }
    }

    internal static class BattleHotkeyListHelper
    {
        internal static void TryApplyConfiguredLists()
        {
            if (!Main.Config.EnableGlobalRemap)
            {
                return;
            }

            try
            {
                if (!IsBattleActionListConfigured())
                {
                    BattleController.BattleActionFastKey = BuildList(
                        Main.Config.BattleActionFastW,
                        Main.Config.BattleActionFastE,
                        Main.Config.BattleActionFastS,
                        Main.Config.BattleActionFastD,
                        Main.Config.BattleActionFastX,
                        Main.Config.BattleActionFastC);

                    if (Main.Config.VerboseLog)
                    {
                        Main.Log.Msg("已应用战斗动作快捷键映射。");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Main.Log.Warning($"应用战斗动作快捷键失败：{ex.Message}");
            }
        }

        private static bool IsBattleActionListConfigured()
        {
            List<KeyCode> current = BattleController.BattleActionFastKey;
            if (current == null || current.Count != 6)
            {
                return false;
            }

            return current[0] == Main.Config.BattleActionFastW
                && current[1] == Main.Config.BattleActionFastE
                && current[2] == Main.Config.BattleActionFastS
                && current[3] == Main.Config.BattleActionFastD
                && current[4] == Main.Config.BattleActionFastX
                && current[5] == Main.Config.BattleActionFastC;
        }

        private static List<KeyCode> BuildList(params KeyCode[] keys)
        {
            List<KeyCode> result = new();
            foreach (KeyCode key in keys)
            {
                result.Add(key);
            }

            return result;
        }
    }

    [HarmonyPatch(typeof(GlobalData), nameof(GlobalData.GetKeyDown))]
    internal static class GlobalDataGetKeyDownPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!HotkeyRemapService.TryMapGlobalKey(key, out KeyCode mappedKey) || mappedKey == key)
            {
                return true;
            }

            __result = !GlobalData.IsInputing() && Input.GetKeyDown(mappedKey);
            return false;
        }
    }

    [HarmonyPatch(typeof(GlobalData), nameof(GlobalData.GetKeyUp))]
    internal static class GlobalDataGetKeyUpPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!HotkeyRemapService.TryMapGlobalKey(key, out KeyCode mappedKey) || mappedKey == key)
            {
                return true;
            }

            __result = !GlobalData.IsInputing() && Input.GetKeyUp(mappedKey);
            return false;
        }
    }

    [HarmonyPatch(typeof(GlobalData), nameof(GlobalData.GetKey))]
    internal static class GlobalDataGetKeyPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (!HotkeyRemapService.TryMapGlobalKey(key, out KeyCode mappedKey) || mappedKey == key)
            {
                return true;
            }

            __result = !GlobalData.IsInputing() && Input.GetKey(mappedKey);
            return false;
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.RunBattle))]
    internal static class BattleControllerRunBattlePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            BattleHotkeyListHelper.TryApplyConfiguredLists();
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.Update))]
    internal static class PlotControllerUpdateRemapPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlotController __instance)
        {
            if (!HotkeyRemapService.ShouldHandlePlotAdvance(__instance))
            {
                return;
            }

            if (Input.GetKeyDown(Main.Config.PlotAdvance))
            {
                __instance.PlotBackgroundClicked();
            }
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Update))]
    internal static class HudControllerUpdateHeroDetailHotkeyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HudController __instance)
        {
            HotkeyRemapService.TryHandleHudHotkeys(__instance);
        }
    }
}
