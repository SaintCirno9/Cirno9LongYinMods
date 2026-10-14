using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(GovernTributeHelper.Main), "GovernTributeHelper", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace GovernTributeHelper
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("GovernTributeHelper 已加载。打开皇宫进贡选物面板后，会注入按品级批量进贡按钮。");
        }
    }

    [HarmonyPatch]
    internal static class TributeChoosePanelPatch
    {
        internal const string TributeChooseCallback = "GiveTreasureToGovernChoosen";

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> methods = new();
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(ChooseController)))
            {
                if (method.Name == nameof(ChooseController.ShowChoosePanel))
                {
                    methods.Add(method);
                }
            }

            return methods;
        }

        private static void Postfix(ChooseController __instance, object[] __args)
        {
            TributeQuickButtons.RemoveExisting(__instance);

            if (__instance == null || __args == null || __args.Length < 5)
            {
                return;
            }

            if (__args[0] is not ChooseType chooseType || chooseType != ChooseType.HeroItem)
            {
                return;
            }

            string sendResultFuc = __args[3]?.ToString() ?? string.Empty;
            if (!string.Equals(sendResultFuc, TributeChooseCallback, StringComparison.Ordinal))
            {
                return;
            }

            MelonCoroutines.Start(PrepareTributeChoosePanel(__instance));
        }

        private static IEnumerator PrepareTributeChoosePanel(ChooseController chooseController)
        {
            yield return null;
            yield return null;

            if (chooseController == null)
            {
                yield break;
            }

            TributeQuickButtons.Install(chooseController);
        }
    }

    internal static class TributeQuickButtons
    {
        private const int AllItemLevels = -1;
        private const string RootName = "TributeQuickButtons";
        private const string TemplateButtonName = "TypeFlitterAllOn";
        private const float ButtonStartX = 260f;
        private const float ButtonStartY = 317f;
        private const float ButtonSpacingX = 60f;
        private const float ButtonSpacingY = 40f;
        private const int ButtonColumns = 2;
        private static readonly HashSet<int> EnabledItemLevels = new() { 3, 4, 5 };

        internal static void Install(ChooseController chooseController)
        {
            if (chooseController?.itemList == null)
            {
                return;
            }

            RemoveExisting(chooseController);

            Transform itemListTransform = chooseController.itemList.transform;
            Transform choosePanelRoot = itemListTransform.parent;
            if (choosePanelRoot == null)
            {
                Main.Log.Warning("[贡品选物] 找不到 ChoosePanelRoot，跳过批量按钮注入。");
                return;
            }

            Transform templateTransform = choosePanelRoot.Find(TemplateButtonName);
            Button templateButton = templateTransform?.GetComponent<Button>();
            if (templateButton == null)
            {
                Main.Log.Warning($"[贡品选物] 找不到模板按钮 {TemplateButtonName}，跳过批量按钮注入。");
                return;
            }

            List<TributeButtonSpec> buttonSpecs = BuildButtonSpecs(itemListTransform);
            if (buttonSpecs.Count == 0)
            {
                Main.Log.Warning("[贡品选物] 没有找到物品品级筛选按钮，跳过批量按钮注入。");
                return;
            }

            GameObject rootObject = new(RootName);
            rootObject.layer = choosePanelRoot.gameObject.layer;
            RectTransform rootRect = rootObject.AddComponent<RectTransform>();
            rootRect.SetParent(choosePanelRoot, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < buttonSpecs.Count; i++)
            {
                TributeButtonSpec spec = buttonSpecs[i];
                Vector2 anchoredPosition = GetButtonPosition(i);
                CreateQuickButton(rootRect, templateButton, chooseController, spec, anchoredPosition);
            }

            Main.Log.Msg($"[贡品选物] 已在 ChoosePanelRoot 右侧空白区注入 {buttonSpecs.Count} 个按品级批量进贡按钮。");
        }

        internal static void RemoveExisting(ChooseController chooseController)
        {
            if (chooseController?.itemList == null)
            {
                return;
            }

            Transform existing = chooseController.itemList.transform.Find(RootName);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            Transform choosePanelRoot = chooseController.itemList.transform.parent;
            Transform existingOnRoot = choosePanelRoot?.Find(RootName);
            if (existingOnRoot != null)
            {
                UnityEngine.Object.Destroy(existingOnRoot.gameObject);
            }
        }

        private static List<TributeButtonSpec> BuildButtonSpecs(Transform itemListTransform)
        {
            List<TributeButtonSpec> specs = new();
            Transform itemLvFlitter = itemListTransform.Find("ItemFlitter/ItemLvFlitter");
            if (itemLvFlitter == null)
            {
                return specs;
            }

            for (int i = 0; i < itemLvFlitter.childCount; i++)
            {
                Transform flitter = itemLvFlitter.GetChild(i);
                if (flitter == null || !int.TryParse(flitter.name, out int rareLv))
                {
                    continue;
                }

                if (!EnabledItemLevels.Contains(rareLv))
                {
                    continue;
                }

                string label = ReadFilterLabel(flitter);
                string buttonText = GetButtonText(rareLv, label);
                Color textColor = ReadFilterTextColor(flitter, rareLv);
                specs.Add(new TributeButtonSpec(rareLv, label, buttonText, textColor));
            }

            specs.Add(new TributeButtonSpec(AllItemLevels, "全部", "贡全部", Color.black));

            return specs;
        }

        private static string GetButtonText(int rareLv, string fallbackLabel)
        {
            return rareLv switch
            {
                3 => "贡精良",
                4 => "贡完美",
                5 => "贡绝世",
                _ => fallbackLabel.Length <= 3 ? $"贡{fallbackLabel}" : fallbackLabel
            };
        }

        private static string ReadFilterLabel(Transform flitter)
        {
            if (flitter == null)
            {
                return "未知";
            }

            Transform labelTransform = flitter.Find("Label");
            if (labelTransform != null)
            {
                string label = NormalizeLabel(TryReadText(labelTransform.gameObject));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return $"品级{flitter.name}";
        }

        private static Color ReadFilterTextColor(Transform flitter, int rareLv)
        {
            if (flitter != null)
            {
                if (TryReadColor(flitter.gameObject, out Color textColor))
                {
                    return textColor;
                }
            }

            return rareLv switch
            {
                3 => new Color(0.66f, 0.37f, 0.93f, 1f),
                4 => new Color(1f, 0.66f, 0.06f, 1f),
                5 => new Color(1f, 0.12f, 0.2f, 1f),
                _ => Color.white
            };
        }

        private static string NormalizeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            return label.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        }

        private static Vector2 GetButtonPosition(int index)
        {
            int row = index / ButtonColumns;
            int column = index % ButtonColumns;
            return new Vector2(
                ButtonStartX + (column * ButtonSpacingX),
                ButtonStartY - (row * ButtonSpacingY));
        }

        private static void CreateQuickButton(
            RectTransform parent,
            Button templateButton,
            ChooseController chooseController,
            TributeButtonSpec spec,
            Vector2 anchoredPosition)
        {
            GameObject buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, parent) as GameObject;
            if (buttonObject == null)
            {
                return;
            }

            buttonObject.name = $"TributeQuick_{spec.ItemLevel}";
            buttonObject.SetActive(true);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = anchoredPosition;
                rectTransform.localScale = Vector3.one;
            }

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener((UnityAction)(() =>
            {
                TributeBatchActions.ContributeAllByItemLevel(chooseController, spec.ItemLevel, spec.Label);
            }));

            SetButtonText(buttonObject, spec.ButtonText, spec.TextColor);
        }

        private static void SetButtonText(GameObject buttonObject, string text, Color textColor)
        {
            if (buttonObject == null)
            {
                return;
            }

            // TODO: 如果后续游戏把按钮文本组件从当前类型替换掉，优先检查这里的反射写字逻辑。
            Text unityText = buttonObject.GetComponentInChildren<Text>(true);
            if (unityText != null)
            {
                unityText.text = text;
                unityText.color = textColor;
                return;
            }

            if (TrySetTextAndColor(buttonObject, text, textColor))
            {
                return;
            }

            Transform textTransform = buttonObject.transform.Find("Text");
            if (textTransform != null && TrySetTextAndColor(textTransform.gameObject, text, textColor))
            {
                return;
            }

            if (!TrySetTextAndColor(buttonObject, text, textColor))
            {
                Main.Log.Warning($"[贡品选物] 无法设置按钮文本：{buttonObject.name} -> {text}");
            }
        }

        private static string TryReadText(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            Component[] components = gameObject.GetComponents<Component>();
            if (components == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    PropertyInfo textProperty = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textProperty != null && textProperty.PropertyType == typeof(string))
                    {
                        return textProperty.GetValue(component) as string ?? string.Empty;
                    }

                    FieldInfo textField = component.GetType().GetField("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textField != null && textField.FieldType == typeof(string))
                    {
                        return textField.GetValue(component) as string ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Main.Log.Warning($"[贡品选物] 读取文本失败：{component.GetType().Name}，{ex.GetType().Name}");
                }
            }

            return string.Empty;
        }

        private static bool TrySetTextAndColor(GameObject gameObject, string text, Color textColor)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (TrySetTextAndColorOnObject(gameObject, text, textColor))
            {
                return true;
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && TrySetTextAndColorOnObject(child.gameObject, text, textColor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetTextAndColorOnObject(GameObject gameObject, string text, Color textColor)
        {
            Component[] components = gameObject.GetComponents<Component>();
            if (components == null)
            {
                return false;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    PropertyInfo textProperty = component.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textProperty != null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
                    {
                        textProperty.SetValue(component, text);
                        TrySetColorMember(component, textColor);
                        TryInvokeNoArg(component, "MarkAsChanged");
                        TryInvokeProcessText(component);
                        return true;
                    }

                    FieldInfo textField = component.GetType().GetField("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textField != null && textField.FieldType == typeof(string))
                    {
                        textField.SetValue(component, text);
                        TrySetColorMember(component, textColor);
                        TryInvokeNoArg(component, "MarkAsChanged");
                        TryInvokeProcessText(component);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Main.Log.Warning($"[贡品选物] 设置文本失败：{component.GetType().Name}，{ex.GetType().Name}");
                }
            }

            return false;
        }

        private static bool TryReadColor(GameObject gameObject, out Color color)
        {
            color = Color.white;
            if (gameObject == null)
            {
                return false;
            }

            if (TryReadColorOnObject(gameObject, out color))
            {
                return true;
            }

            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && TryReadColorOnObject(child.gameObject, out color))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadColorOnObject(GameObject gameObject, out Color color)
        {
            color = Color.white;
            Component[] components = gameObject.GetComponents<Component>();
            if (components == null)
            {
                return false;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                try
                {
                    PropertyInfo colorProperty = component.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                    if (colorProperty != null && colorProperty.PropertyType == typeof(Color))
                    {
                        object value = colorProperty.GetValue(component);
                        if (value is Color propertyColor)
                        {
                            color = propertyColor;
                            return true;
                        }
                    }

                    FieldInfo colorField = component.GetType().GetField("color", BindingFlags.Public | BindingFlags.Instance);
                    if (colorField != null && colorField.FieldType == typeof(Color))
                    {
                        object value = colorField.GetValue(component);
                        if (value is Color fieldColor)
                        {
                            color = fieldColor;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.Log.Warning($"[贡品选物] 读取颜色失败：{component.GetType().Name}，{ex.GetType().Name}");
                }
            }

            return false;
        }

        private static void TrySetColorMember(Component component, Color textColor)
        {
            try
            {
                PropertyInfo colorProperty = component.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                if (colorProperty != null && colorProperty.CanWrite && colorProperty.PropertyType == typeof(Color))
                {
                    colorProperty.SetValue(component, textColor);
                    return;
                }

                FieldInfo colorField = component.GetType().GetField("color", BindingFlags.Public | BindingFlags.Instance);
                if (colorField != null && colorField.FieldType == typeof(Color))
                {
                    colorField.SetValue(component, textColor);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"[贡品选物] 设置颜色失败：{component.GetType().Name}，{ex.GetType().Name}");
            }
        }

        private static void TryInvokeNoArg(Component component, string methodName)
        {
            try
            {
                MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                method?.Invoke(component, null);
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"[贡品选物] 调用 {component.GetType().Name}.{methodName} 失败：{ex.GetType().Name}");
            }
        }

        private static void TryInvokeProcessText(Component component)
        {
            try
            {
                MethodInfo method = component.GetType().GetMethod("ProcessText", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool), typeof(bool) }, null);
                method?.Invoke(component, new object[] { false, true });
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"[贡品选物] 调用 {component.GetType().Name}.ProcessText 失败：{ex.GetType().Name}");
            }
        }

        private sealed class TributeButtonSpec
        {
            internal TributeButtonSpec(int itemLevel, string label, string buttonText, Color textColor)
            {
                ItemLevel = itemLevel;
                Label = label;
                ButtonText = buttonText;
                TextColor = textColor;
            }

            internal int ItemLevel { get; }

            internal string Label { get; }

            internal string ButtonText { get; }

            internal Color TextColor { get; }
        }
    }

    internal static class TributeBatchActions
    {
        private const int AllItemLevels = -1;

        internal static void ContributeAllByItemLevel(ChooseController chooseController, int itemLevel, string itemLevelLabel)
        {
            if (chooseController == null)
            {
                return;
            }

            if (!string.Equals(chooseController.sendResultFuc, TributeChoosePanelPatch.TributeChooseCallback, StringComparison.Ordinal))
            {
                Main.Log.Warning("[贡品选物] 当前面板不是官员进贡面板，忽略批量进贡点击。");
                return;
            }

            if (!IsSupportedRewardParam(chooseController.sendResultParam))
            {
                Main.Log.Warning($"[贡品选物] 未识别的进贡参数：{chooseController.sendResultParam}");
                return;
            }

            HeroData player = GameController.Instance?.worldData?.Player();
            if (player == null)
            {
                Main.Log.Warning("[贡品选物] 找不到玩家 HeroData，无法执行批量进贡。");
                return;
            }

            List<MatchedItemEntry> matchedItems = CollectMatchedItems(chooseController, itemLevel);
            if (matchedItems.Count == 0)
            {
                string labelText = itemLevel == AllItemLevels ? "可批量进贡的物品" : $"可批量进贡的 {itemLevelLabel} 物品";
                Main.Log.Msg($"[贡品选物] 当前没有{labelText}。");
                return;
            }

            float totalGain = 0f;
            int itemCount = 0;

            for (int i = 0; i < matchedItems.Count; i++)
            {
                MatchedItemEntry entry = matchedItems[i];
                if (entry?.IconObject == null || entry.Item == null)
                {
                    continue;
                }

                totalGain += GetItemRewardValue(entry.Item, chooseController.sendResultParam);
                player.LoseItem(entry.Item, true);
                itemCount++;

                if (entry.IconObject != null)
                {
                    UnityEngine.Object.Destroy(entry.IconObject);
                }
            }

            ApplyReward(player, chooseController.sendResultParam, totalGain);
            RebuildGridLayout(chooseController);

            Main.Log.Msg(
                $"[贡品选物] 已批量进贡 {itemCount} 件 {itemLevelLabel} 物品，收益={totalGain:0.##}，sendResultParam={chooseController.sendResultParam}");
        }

        private static List<MatchedItemEntry> CollectMatchedItems(ChooseController chooseController, int itemLevel)
        {
            List<MatchedItemEntry> items = new();
            GameObject targetGrid = chooseController.targetGrid;
            if (targetGrid == null)
            {
                return items;
            }

            Transform gridTransform = targetGrid.transform;
            for (int i = 0; i < gridTransform.childCount; i++)
            {
                Transform child = gridTransform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!TryResolveItemData(child.gameObject, out ItemData item))
                {
                    continue;
                }

                if (itemLevel != AllItemLevels && item.itemLv != itemLevel)
                {
                    continue;
                }

                items.Add(new MatchedItemEntry(child.gameObject, item));
            }

            return items;
        }

        private static bool IsSupportedRewardParam(string sendResultParam)
        {
            return sendResultParam is "0" or "1" or "2";
        }

        private static float GetItemRewardValue(ItemData item, string sendResultParam)
        {
            if (item == null)
            {
                return 0f;
            }

            return sendResultParam switch
            {
                "0" => item.value * 0.05f,
                "1" => item.value * 0.05f,
                "2" => item.value * 0.1f,
                _ => 0f
            };
        }

        private static void ApplyReward(HeroData player, string sendResultParam, float totalGain)
        {
            if (player == null || totalGain <= 0f)
            {
                return;
            }

            switch (sendResultParam)
            {
                case "0":
                    player.ChangeResource(5, totalGain, true, true, -1, 1f);
                    return;
                case "1":
                    player.ChangeFame(totalGain, true);
                    return;
                case "2":
                    player.ChangeGovernContribution(totalGain, true);
                    return;
            }
        }

        private static bool TryResolveItemData(GameObject iconObject, out ItemData item)
        {
            item = null;
            if (iconObject == null)
            {
                return false;
            }

            try
            {
                ItemIconController icon = iconObject.GetComponent<ItemIconController>();
                if (icon?.itemData != null)
                {
                    item = icon.itemData;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"[贡品选物] 直接读取 ItemIconController 失败：{iconObject.name}，{ex.GetType().Name}");
            }

            Component[] components = iconObject.GetComponents<Component>();
            if (components == null)
            {
                return false;
            }

            for (int i = 0; i < components.Length; i++)
            {
                try
                {
                    ItemIconController icon = components[i].TryCast<ItemIconController>();
                    if (icon?.itemData != null)
                    {
                        item = icon.itemData;
                        return true;
                    }
                }
                catch
                {
                }

                if (TryReadItemDataFromComponent(components[i], out item))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadItemDataFromComponent(Component component, out ItemData item)
        {
            item = null;
            if (component == null)
            {
                return false;
            }

            Type componentType = component.GetType();

            try
            {
                FieldInfo itemField = componentType.GetField("itemData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (itemField != null)
                {
                    item = itemField.GetValue(component) as ItemData;
                    if (item != null)
                    {
                        return true;
                    }
                }

                PropertyInfo itemProperty = componentType.GetProperty("itemData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (itemProperty != null)
                {
                    item = itemProperty.GetValue(component, null) as ItemData;
                    if (item != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void RebuildGridLayout(ChooseController chooseController)
        {
            if (chooseController?.targetGrid == null)
            {
                return;
            }

            RectTransform gridRect = chooseController.targetGrid.GetComponent<RectTransform>();
            if (gridRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            }
        }

        private sealed class MatchedItemEntry
        {
            internal MatchedItemEntry(GameObject iconObject, ItemData item)
            {
                IconObject = iconObject;
                Item = item;
            }

            internal GameObject IconObject { get; }

            internal ItemData Item { get; }
        }
    }
}
