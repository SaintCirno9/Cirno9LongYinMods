using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FoodAutomation
{
    [HarmonyPatch(typeof(ItemUseMenuController), nameof(ItemUseMenuController.Show))]
    internal static class ItemUseMenuControllerShowPatch
    {
        private static void Postfix(ItemUseMenuController __instance, HeroData _sourceHero)
        {
            try
            {
                FoodUseAllButtons.RemoveInjectedButtons(__instance);

                if (!FoodUseAllButtons.ShouldInject(__instance, _sourceHero))
                {
                    return;
                }

                FoodUseAllButtons.Inject(__instance, _sourceHero);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 注入“使用全部”按钮时发生异常：{ex}");
            }
        }
    }

    internal static class FoodUseAllButtons
    {
        private const string ButtonNamePrefix = "FoodAutomation_UseAll_";
        private const string OriginalButtonTextPath = "Text";
        private const int MaxBatchIterations = 9999;
        private const int InjectedButtonFontSize = 15;

        internal static bool ShouldInject(ItemUseMenuController menuController, HeroData sourceHero)
        {
            if (menuController == null || sourceHero == null || menuController.targetItem == null)
            {
                return false;
            }

            if (menuController.targetItem.type != ItemType.Med && menuController.targetItem.type != ItemType.Food)
            {
                return false;
            }

            HeroDetailController detailController = HeroDetailController.Instance;
            if (detailController == null || detailController.heroDetailPanel == null || !detailController.heroDetailPanel.activeInHierarchy)
            {
                return false;
            }

            HeroData nowShowHero = detailController.nowShowHero;
            return nowShowHero != null && sourceHero.heroID == nowShowHero.heroID;
        }

        internal static void Inject(ItemUseMenuController menuController, HeroData sourceHero)
        {
            ItemUseChoiceButtonController[] choiceButtons = menuController.itemUseMenuPanel?.GetComponentsInChildren<ItemUseChoiceButtonController>(true);
            if (choiceButtons == null || choiceButtons.Length == 0)
            {
                Main.Log.Warning("[FoodAutomation] 当前食物弹窗里没有找到原始使用按钮，跳过“使用全部”注入。");
                return;
            }

            int injectedCount = 0;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                ItemUseChoiceButtonController choiceButton = choiceButtons[i];
                if (choiceButton == null || choiceButton.gameObject == null || choiceButton.transform.parent == null)
                {
                    continue;
                }

                HeroData targetHero = choiceButton.targetHero;
                if (targetHero == null)
                {
                    continue;
                }

                CreateUseAllButton(menuController, sourceHero, targetHero, choiceButton, injectedCount);
                injectedCount++;
            }
        }

        internal static void RemoveInjectedButtons(ItemUseMenuController menuController)
        {
            Transform root = menuController?.itemUseMenuPanel?.transform;
            if (root == null)
            {
                return;
            }

            List<GameObject> staleButtons = new();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms == null)
            {
                return;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child != null && child.name.StartsWith(ButtonNamePrefix, StringComparison.Ordinal))
                {
                    staleButtons.Add(child.gameObject);
                }
            }

            for (int i = 0; i < staleButtons.Count; i++)
            {
                UnityEngine.Object.Destroy(staleButtons[i]);
            }
        }

        private static void CreateUseAllButton(
            ItemUseMenuController menuController,
            HeroData sourceHero,
            HeroData targetHero,
            ItemUseChoiceButtonController templateChoiceButton,
            int index)
        {
            Transform parent = templateChoiceButton.transform.parent;
            GameObject prefab = menuController?.itemUseChoiceButtonPrefab;
            if (prefab == null)
            {
                Main.Log.Warning("[FoodAutomation] 原始按钮 prefab 丢失，跳过“使用全部”按钮注入。");
                return;
            }

            // 这里必须克隆原始 prefab，不能克隆已经显示在场景里的活按钮实例。
            // 之前直接复制 templateChoiceButton.gameObject 时，会把原按钮当前的运行时状态、
            // 组件引用和生命周期一起带过去，导致菜单打开阶段重放原按钮副作用，污染 HeroData。
            GameObject buttonObject = UnityEngine.Object.Instantiate(prefab, parent) as GameObject;
            if (buttonObject == null)
            {
                return;
            }

            buttonObject.name = $"{ButtonNamePrefix}{targetHero.heroID}";
            ItemUseChoiceButtonController choiceButtonController = buttonObject.GetComponent<ItemUseChoiceButtonController>();
            if (choiceButtonController == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                Main.Log.Warning("[FoodAutomation] 新按钮缺少 ItemUseChoiceButtonController，跳过注入。");
                return;
            }

            choiceButtonController.targetHero = targetHero;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                return;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() =>
            {
                try
                {
                    StartBatchUseSameFood(menuController, sourceHero, targetHero);
                }
                catch (Exception ex)
                {
                    Main.Log.Error($"[FoodAutomation] 执行“使用全部”按钮时发生异常：{ex}");
                }
            }));

            buttonObject.transform.SetSiblingIndex(templateChoiceButton.transform.GetSiblingIndex() + index + 1);
            SetButtonLabel(buttonObject, BuildUseAllLabel(targetHero));
            buttonObject.SetActive(true);
        }

        private static void StartBatchUseSameFood(ItemUseMenuController menuController, HeroData sourceHero, HeroData targetHero)
        {
            if (menuController == null || sourceHero == null || targetHero == null || menuController.targetItem == null)
            {
                return;
            }

            if (!ShouldInject(menuController, sourceHero))
            {
                return;
            }

            ItemIdentity identity = ItemIdentity.FromItem(menuController.targetItem);
            if (!identity.IsValid)
            {
                return;
            }

            ItemListData itemListData = sourceHero.itemListData;
            if (itemListData?.allItem == null)
            {
                return;
            }

            menuController.Hide();
            MelonCoroutines.Start(BatchUseSameFoodCoroutine(sourceHero, targetHero, identity));
        }

        private static IEnumerator BatchUseSameFoodCoroutine(HeroData sourceHero, HeroData targetHero, ItemIdentity identity)
        {
            if (sourceHero == null || targetHero == null || !identity.IsValid)
            {
                yield break;
            }

            yield return null;

            for (int i = 0; i < MaxBatchIterations; i++)
            {
                ItemListData itemListData = sourceHero.itemListData;
                if (itemListData?.allItem == null)
                {
                    yield break;
                }

                ItemData nextItem = FindNextMatchingItem(itemListData, identity);
                if (nextItem == null)
                {
                    yield break;
                }

                targetHero.UseMedFood(nextItem, false, true, sourceHero);
                yield return null;
            }
        }

        private static ItemData FindNextMatchingItem(ItemListData itemListData, ItemIdentity identity)
        {
            var items = itemListData?.allItem;
            if (items == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemData candidate = items[i];
                if (identity.Matches(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string BuildUseAllLabel(HeroData targetHero)
        {
            string heroName = targetHero?.heroName;
            if (string.IsNullOrWhiteSpace(heroName))
            {
                heroName = targetHero?.Name(false) ?? "目标";
            }

            return $"{heroName}使用全部";
        }

        private static void SetButtonLabel(GameObject buttonObject, string label)
        {
            Text labelText = FindButtonLabelText(buttonObject);
            if (labelText == null)
            {
                return;
            }

            labelText.text = label;
            labelText.fontSize = InjectedButtonFontSize;
        }

        private static Text FindButtonLabelText(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return null;
            }

            Transform labelTransform = buttonObject.transform.Find(OriginalButtonTextPath);
            Text labelText = labelTransform?.GetComponent<Text>();
            if (labelText != null)
            {
                return labelText;
            }

            return buttonObject.GetComponentInChildren<Text>(true);
        }

        private readonly struct ItemIdentity
        {
            private ItemIdentity(bool isValid, ItemType type, int itemId, int subType)
            {
                IsValid = isValid;
                Type = type;
                ItemId = itemId;
                SubType = subType;
            }

            internal bool IsValid { get; }
            private ItemType Type { get; }
            private int ItemId { get; }
            private int SubType { get; }

            internal static ItemIdentity FromItem(ItemData item)
            {
                if (item == null)
                {
                    return default;
                }

                return new ItemIdentity(true, item.type, item.itemID, item.subType);
            }

            internal bool Matches(ItemData item)
            {
                // 这里按 type + subType + itemID 归并同类食物，不再区分品质等级。
                return item != null &&
                       item.type == Type &&
                       item.itemID == ItemId &&
                       item.subType == SubType;
            }
        }
    }
}
