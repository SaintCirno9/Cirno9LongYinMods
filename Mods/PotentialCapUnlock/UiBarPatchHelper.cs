using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace PotentialCapUnlock
{
    internal enum UiBarVisualKind
    {
        Unknown = 0,
        AttriOrFight = 1,
        Living = 2,
    }

    internal static class UiBarPatchHelper
    {
        private const float BarWidthScale = 1.5f;
        private const float AttrAndFightMaxVisualWidth = 120f * BarWidthScale;
        private const float LivingMaxVisualWidth = 100f * BarWidthScale;

        internal static void AdjustHeroDetailBars(Transform root)
        {
            if (root == null)
            {
                return;
            }

            AdjustBarsRecursive(root, UiBarVisualKind.Unknown);
        }

        internal static void AdjustUniformBars(Transform root, UiBarVisualKind visualKind)
        {
            if (root == null)
            {
                return;
            }

            AdjustBarsRecursive(root, visualKind);
        }

        private static void AdjustBarsRecursive(Transform current, UiBarVisualKind inheritedKind)
        {
            UiBarVisualKind currentKind = ResolveVisualKind(current, inheritedKind);
            AdjustSingleRow(current, currentKind);

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
            {
                AdjustBarsRecursive(current.GetChild(i), currentKind);
            }
        }

        private static UiBarVisualKind ResolveVisualKind(Transform current, UiBarVisualKind inheritedKind)
        {
            if (current == null)
            {
                return inheritedKind;
            }

            if (string.Equals(current.name, "LivingSkills", StringComparison.Ordinal))
            {
                return UiBarVisualKind.Living;
            }

            if (string.Equals(current.name, "Attris", StringComparison.Ordinal) ||
                string.Equals(current.name, "FightSkills", StringComparison.Ordinal))
            {
                return UiBarVisualKind.AttriOrFight;
            }

            return inheritedKind;
        }

        private static void AdjustSingleRow(Transform row, UiBarVisualKind visualKind)
        {
            if (!TryGetRowParts(row, out RectTransform maxBarRect, out RectTransform barRect, out Text valueText))
            {
                return;
            }

            if (!TryReadDisplayedValues(valueText, out float baseNum, out float maxNum))
            {
                return;
            }

            float maxVisualWidth = GetTargetVisualWidth(visualKind);
            float visualMax = Mathf.Max(0f, maxNum);
            float scaledMaxWidth = Mathf.Min(visualMax * BarWidthScale, maxVisualWidth);
            float scaledBaseWidth = 0f;

            if (visualMax > 0f)
            {
                float ratio = Mathf.Clamp01(baseNum / visualMax);
                scaledBaseWidth = scaledMaxWidth * ratio;
            }

            // 保留原版文本逻辑，只把超出可视区域的条形按比例压回固定宽度。
            SetBarWidth(maxBarRect, scaledMaxWidth);
            SetBarWidth(barRect, scaledBaseWidth);
        }

        private static bool TryGetRowParts(Transform row, out RectTransform maxBarRect, out RectTransform barRect, out Text valueText)
        {
            maxBarRect = null;
            barRect = null;
            valueText = null;

            if (row == null)
            {
                return false;
            }

            int childCount = row.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = row.GetChild(i);
                string childName = child.name;

                if ((childName == "BarBack" || childName == "MaxBar") && maxBarRect == null)
                {
                    maxBarRect = child.GetComponent<RectTransform>();
                    continue;
                }

                if ((childName == "BaseBar" || childName == "Bar") && barRect == null)
                {
                    barRect = child.GetComponent<RectTransform>();
                    continue;
                }

                if ((childName == "NumValueText" || childName == "Num") && valueText == null)
                {
                    valueText = child.GetComponent<Text>();
                }
            }

            return maxBarRect != null && barRect != null;
        }

        private static bool TryReadDisplayedValues(Text valueText, out float baseNum, out float maxNum)
        {
            baseNum = 0f;
            maxNum = 0f;
            if (valueText == null || string.IsNullOrWhiteSpace(valueText.text))
            {
                return false;
            }

            return TryParseFirstTwoNumbers(valueText.text, out baseNum, out maxNum);
        }

        private static float GetTargetVisualWidth(UiBarVisualKind visualKind)
        {
            return visualKind == UiBarVisualKind.Living ? LivingMaxVisualWidth : AttrAndFightMaxVisualWidth;
        }

        private static void SetBarWidth(RectTransform rect, float width)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 size = rect.sizeDelta;
            size.x = width;
            rect.sizeDelta = size;
        }

        private static bool TryParseFirstTwoNumbers(string text, out float first, out float second)
        {
            first = 0f;
            second = 0f;
            int count = 0;
            int tokenStart = -1;

            for (int i = 0; i <= text.Length; i++)
            {
                bool isNumberChar = i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == '-');
                if (isNumberChar)
                {
                    if (tokenStart < 0)
                    {
                        tokenStart = i;
                    }

                    continue;
                }

                if (tokenStart < 0)
                {
                    continue;
                }

                if (!TryAssignNumber(text, tokenStart, i - tokenStart, ref first, ref second, ref count))
                {
                    tokenStart = -1;
                    continue;
                }

                tokenStart = -1;
                if (count >= 2)
                {
                    return true;
                }
            }

            if (count < 2)
            {
                return false;
            }

            return true;
        }

        private static bool TryAssignNumber(string text, int start, int length, ref float first, ref float second, ref int count)
        {
            if (length <= 0)
            {
                return false;
            }

            if (!float.TryParse(text.AsSpan(start, length), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return false;
            }

            if (count == 0)
            {
                first = parsed;
                count = 1;
                return true;
            }

            if (count == 1)
            {
                second = parsed;
                count = 2;
                return true;
            }

            return true;
        }
    }
}
