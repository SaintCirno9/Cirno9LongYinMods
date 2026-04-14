using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace WaitTimeTweaks
{
    internal static class WaitUiRuntime
    {
        private const float SliderMinValue = 1f;
        private const float ParentPadding = 48f;
        private const string DaySuffix = "天";

        internal static void Apply(WaitUIController controller)
        {
            if (!WaitTimeTweaksConfig.EnableMod.Value || controller?.waitTimeSlider == null)
            {
                return;
            }

            Slider slider = controller.waitTimeSlider;

            slider.minValue = SliderMinValue;
            slider.maxValue = WaitTimeTweaksConfig.MaxWaitDays.Value;
            slider.wholeNumbers = true;

            if (slider.value < SliderMinValue)
            {
                slider.value = SliderMinValue;
            }
            else if (slider.value > slider.maxValue)
            {
                slider.value = slider.maxValue;
            }

            ResizeSlider(slider);
            RefreshText(controller);

            Main.LogVerbose(
                $"已调整等待滑条：name={slider.name}，value={slider.value}，min={slider.minValue}，max={slider.maxValue}，width={WaitTimeTweaksConfig.SliderWidth.Value}。");
        }

        internal static void RefreshText(WaitUIController controller)
        {
            if (!WaitTimeTweaksConfig.EnableMod.Value || controller?.waitTimeSlider == null || controller.waitTimeText == null)
            {
                return;
            }

            int dayValue = Mathf.RoundToInt(controller.waitTimeSlider.value);
            if (dayValue < 1)
            {
                return;
            }

            string dayText = GlobalData.ConvertNumToChinese(dayValue);
            if (string.IsNullOrWhiteSpace(dayText))
            {
                dayText = dayValue.ToString();
            }

            controller.waitTimeText.text = $"{dayText}{DaySuffix}";
        }

        private static void ResizeSlider(Slider slider)
        {
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            if (sliderRect == null)
            {
                return;
            }

            float targetWidth = WaitTimeTweaksConfig.SliderWidth.Value;
            SetWidth(sliderRect, targetWidth);

            RectTransform parentRect = sliderRect.parent != null ? sliderRect.parent.GetComponent<RectTransform>() : null;
            if (parentRect != null && parentRect.rect.width > 0f && parentRect.rect.width < targetWidth + ParentPadding)
            {
                SetWidth(parentRect, targetWidth + ParentPadding);
            }
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            if (rect == null)
            {
                return;
            }

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }

    [HarmonyPatch(typeof(WaitUIController), nameof(WaitUIController.ShowWaitUI))]
    internal static class WaitUIController_ShowWaitUI_Patch
    {
        private static void Postfix(WaitUIController __instance)
        {
            WaitUiRuntime.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(WaitUIController), nameof(WaitUIController.RefreshWaitUI))]
    internal static class WaitUIController_RefreshWaitUI_Patch
    {
        private static void Postfix(WaitUIController __instance)
        {
            WaitUiRuntime.RefreshText(__instance);
        }
    }
}
