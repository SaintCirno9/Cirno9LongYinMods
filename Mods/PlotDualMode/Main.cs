using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(PlotDualMode.Main), "PlotDualMode", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace PlotDualMode
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("PlotDualMode 已加载，对话自动与快进可同时开启。");
        }
    }

    internal sealed class PlotButtonVisualSnapshot
    {
        internal Color ImageColor { get; set; } = Color.white;

        internal string Text { get; set; } = string.Empty;
    }

    internal sealed class PlotButtonToggleState
    {
        internal bool NeedRestore { get; set; }

        internal PlotButtonVisualSnapshot VisualSnapshot { get; set; } = new();
    }

    internal static class PlotButtonStateHelper
    {
        private const string TextNodeName = "Text";

        internal static bool DesiredAutoEnabled { get; private set; }

        internal static bool DesiredSkipEnabled { get; private set; }

        private static PlotButtonVisualSnapshot AutoEnabledSnapshot { get; set; } = new();

        private static PlotButtonVisualSnapshot SkipEnabledSnapshot { get; set; } = new();

        internal static PlotButtonVisualSnapshot Capture(GameObject button)
        {
            if (button == null)
            {
                return new PlotButtonVisualSnapshot();
            }

            Image image = button.GetComponent<Image>();
            Text text = button.transform.Find(TextNodeName)?.GetComponent<Text>();
            return new PlotButtonVisualSnapshot
            {
                ImageColor = image != null ? image.color : Color.white,
                Text = text != null ? text.text : string.Empty
            };
        }

        internal static void Restore(GameObject button, PlotButtonVisualSnapshot snapshot)
        {
            if (button == null || snapshot == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = snapshot.ImageColor;
            }

            Text text = button.transform.Find(TextNodeName)?.GetComponent<Text>();
            if (text != null)
            {
                text.text = snapshot.Text;
            }
        }

        internal static void UpdateDesiredAuto(PlotController controller)
        {
            DesiredAutoEnabled = controller != null && controller.plotAutoing;
            if (DesiredAutoEnabled)
            {
                AutoEnabledSnapshot = Capture(controller.plotAutoButton);
            }
        }

        internal static void UpdateDesiredSkip(PlotController controller)
        {
            DesiredSkipEnabled = controller != null && controller.plotSkipping;
            if (DesiredSkipEnabled)
            {
                SkipEnabledSnapshot = Capture(controller.plotSkipButton);
            }
        }

        internal static void RestoreDesiredStates(PlotController controller)
        {
            if (controller == null || !controller.plotHappen)
            {
                return;
            }

            if (DesiredAutoEnabled && !controller.plotAutoing)
            {
                controller.plotAutoing = true;
                controller.plotBlockTime = 0.5f;
                Restore(controller.plotAutoButton, AutoEnabledSnapshot);
            }

            if (DesiredSkipEnabled && !controller.plotSkipping)
            {
                controller.plotSkipping = true;
                controller.plotSkipTime = 0f;
                Restore(controller.plotSkipButton, SkipEnabledSnapshot);
            }
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.AutoPlotButtonClicked))]
    internal static class PlotControllerAutoPlotButtonClickedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlotController __instance, out PlotButtonToggleState __state)
        {
            __state = new PlotButtonToggleState
            {
                NeedRestore = __instance.plotSkipping,
                VisualSnapshot = PlotButtonStateHelper.Capture(__instance.plotSkipButton)
            };
        }

        [HarmonyPostfix]
        private static void Postfix(PlotController __instance, PlotButtonToggleState __state)
        {
            if (!__state.NeedRestore || !__instance.plotAutoing)
            {
                PlotButtonStateHelper.UpdateDesiredAuto(__instance);
                PlotButtonStateHelper.UpdateDesiredSkip(__instance);
                return;
            }

            __instance.plotSkipping = true;
            __instance.plotSkipTime = 0f;
            PlotButtonStateHelper.Restore(__instance.plotSkipButton, __state.VisualSnapshot);
            PlotButtonStateHelper.UpdateDesiredAuto(__instance);
            PlotButtonStateHelper.UpdateDesiredSkip(__instance);
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.SkipPlotButtonClicked))]
    internal static class PlotControllerSkipPlotButtonClickedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlotController __instance, out PlotButtonToggleState __state)
        {
            __state = new PlotButtonToggleState
            {
                NeedRestore = __instance.plotAutoing,
                VisualSnapshot = PlotButtonStateHelper.Capture(__instance.plotAutoButton)
            };
        }

        [HarmonyPostfix]
        private static void Postfix(PlotController __instance, PlotButtonToggleState __state)
        {
            if (!__state.NeedRestore || !__instance.plotSkipping)
            {
                PlotButtonStateHelper.UpdateDesiredAuto(__instance);
                PlotButtonStateHelper.UpdateDesiredSkip(__instance);
                return;
            }

            __instance.plotAutoing = true;
            __instance.plotBlockTime = 0.5f;
            PlotButtonStateHelper.Restore(__instance.plotAutoButton, __state.VisualSnapshot);
            PlotButtonStateHelper.UpdateDesiredAuto(__instance);
            PlotButtonStateHelper.UpdateDesiredSkip(__instance);
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.Update))]
    internal static class PlotControllerUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlotController __instance)
        {
            PlotButtonStateHelper.RestoreDesiredStates(__instance);
        }
    }
}
