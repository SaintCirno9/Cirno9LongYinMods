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
    [HarmonyPatch(typeof(TradeUIController))]
    internal static class TradeUIControllerShowTradeUIPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TradeUIController.ShowTradeUI), typeof(TradeUIType), typeof(ItemListData), typeof(ItemListData), typeof(bool))]
        private static void Postfix(TradeUIController __instance, TradeUIType targetType, ItemListData leftItemList, ItemListData rightItemList)
        {
            try
            {
                TradeBulkBuyButtons.RemoveInjectedButtons(__instance);

                if (!TradeBulkBuyButtons.ShouldInject(__instance, targetType))
                {
                    return;
                }

                TradeBulkBuyButtons.Inject(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 注入“批量购买上限食物”按钮时发生异常：{ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            nameof(TradeUIController.ShowTradeUI),
            typeof(TradeUIType),
            typeof(ItemListType),
            typeof(ItemListData),
            typeof(ItemListData),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(float),
            typeof(float))]
        private static void PostfixExtended(
            TradeUIController __instance,
            TradeUIType targetType,
            ItemListData rightItemList)
        {
            try
            {
                TradeBulkBuyButtons.RemoveInjectedButtons(__instance);

                if (!TradeCapFoodBulkBuyService.ShouldInjectExtended(__instance, (int)targetType, rightItemList))
                {
                    return;
                }

                TradeBulkBuyButtons.Inject(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 注入“批量购买上限食物”按钮时发生异常：{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(TradeUIController), nameof(TradeUIController.HideTradeUI))]
    internal static class TradeUIControllerHideTradeUIPatch
    {
        private static void Postfix(TradeUIController __instance)
        {
            TradeBulkBuyButtons.RemoveInjectedButtons(__instance);
        }
    }

    internal static class TradeBulkBuyButtons
    {
        private const string ButtonNamePrefix = "FoodAutomation_TradeBulkBuy_";
        private const string SureButtonName = "SureButton";
        private const string CancelButtonName = "CancelButton";
        private const float ButtonGap = 30f;
        private const int InjectedButtonFontSize = 14;

        internal static bool ShouldInject(TradeUIController tradeUIController)
        {
            return TradeCapFoodBulkBuyService.ShouldInject(tradeUIController, tradeUIController?.tradeUIType ?? default);
        }

        internal static bool ShouldInject(TradeUIController tradeUIController, TradeUIType targetType)
        {
            return TradeCapFoodBulkBuyService.ShouldInject(tradeUIController, targetType);
        }

        internal static void Inject(TradeUIController tradeUIController)
        {
            Button sureButton = TradeUiButtonUtility.FindButton(tradeUIController.tradeUI, SureButtonName);
            if (sureButton == null)
            {
                Main.Log.Warning("[FoodAutomation] 交易界面中未找到 SureButton，跳过批量购买按钮注入。");
                return;
            }

            Button cancelButton = TradeUiButtonUtility.FindButton(tradeUIController.tradeUI, CancelButtonName);
            string buttonLabel = TradeCapFoodBulkBuyService.GetButtonLabel(tradeUIController);
            string buttonKey = TradeCapFoodBulkBuyService.GetButtonKey(tradeUIController);
            Button button = TradeUiButtonUtility.CreateButtonFromTemplate(
                sureButton,
                $"{ButtonNamePrefix}{buttonKey}",
                buttonLabel,
                InjectedButtonFontSize,
                (UnityAction)(() =>
                {
                    try
                    {
                        TradeCapFoodBulkBuyService.TryBulkBuyCapFoods(tradeUIController);
                    }
                    catch (Exception ex)
                    {
                        Main.Log.Error($"[FoodAutomation] 执行“{buttonLabel}”时发生异常：{ex}");
                    }
                }));
            if (button == null)
            {
                return;
            }

            PositionButton(button.gameObject, sureButton, cancelButton);
        }

        internal static void RemoveInjectedButtons(TradeUIController tradeUIController)
        {
            TradeUiButtonUtility.RemoveButtonsByPrefix(tradeUIController?.tradeUI, ButtonNamePrefix);
        }

        private static void PositionButton(GameObject buttonObject, Button sureButton, Button cancelButton)
        {
            RectTransform sureRect = sureButton?.GetComponent<RectTransform>();
            RectTransform buttonRect = buttonObject?.GetComponent<RectTransform>();
            if (sureRect == null || buttonRect == null)
            {
                return;
            }

            RectTransform anchorRect = sureRect;
            if (cancelButton != null)
            {
                RectTransform cancelRect = cancelButton.GetComponent<RectTransform>();
                if (cancelRect != null)
                {
                    anchorRect = cancelRect;
                }
            }

            float buttonWidth = TradeUiButtonUtility.GetPreferredButtonWidth(buttonObject, sureRect.sizeDelta.x);
            buttonRect.sizeDelta = new Vector2(buttonWidth, sureRect.sizeDelta.y);
            buttonRect.anchorMin = sureRect.anchorMin;
            buttonRect.anchorMax = sureRect.anchorMax;
            buttonRect.pivot = sureRect.pivot;

            buttonRect.anchoredPosition = new Vector2(
                anchorRect.anchoredPosition.x + ((anchorRect.sizeDelta.x + buttonRect.sizeDelta.x) * 0.5f) + ButtonGap,
                anchorRect.anchoredPosition.y);
        }
    }

    internal static class TradeCapFoodBulkBuyService
    {
        private const int MaxBulkBuyIterations = 512;
        private const int ForceStorageTradeUiTypeValue = 2;
        private const int GovernTradeUiTypeValue = 4;

        internal static bool ShouldInject(TradeUIController tradeUIController, TradeUIType targetType)
        {
            if (tradeUIController == null || tradeUIController.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return false;
            }

            if (!IsSupportedTradeUiType(targetType))
            {
                return false;
            }

            ItemListData merchantItems = tradeUIController.rightList?.targetItemList;
            if (merchantItems?.allItem == null || merchantItems.allItem.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < merchantItems.allItem.Count; i++)
            {
                if (FoodAutomationItemUtility.IsCapBoostFood(merchantItems.allItem[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldInjectExtended(TradeUIController tradeUIController, int targetTypeValue, ItemListData rightItemList)
        {
            if (tradeUIController == null || tradeUIController.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return false;
            }

            if (!IsSupportedTradeUiTypeValue(targetTypeValue, rightItemList))
            {
                return false;
            }

            ItemListData merchantItems = tradeUIController.rightList?.targetItemList ?? rightItemList;
            if (merchantItems?.allItem == null || merchantItems.allItem.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < merchantItems.allItem.Count; i++)
            {
                if (FoodAutomationItemUtility.IsCapBoostFood(merchantItems.allItem[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static string GetButtonLabel(TradeUIController tradeUIController)
        {
            return IsForceStorageTrade(tradeUIController) ? "批量换加上限食物" : "批量买加上限食物";
        }

        internal static string GetButtonKey(TradeUIController tradeUIController)
        {
            return IsForceStorageTrade(tradeUIController) ? "ForceStorage" : "Shop";
        }

        internal static void TryBulkBuyCapFoods(TradeUIController tradeUIController)
        {
            if (tradeUIController == null || tradeUIController.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return;
            }

            MelonCoroutines.Start(BulkBuyCapFoodsCoroutine(tradeUIController));
        }

        private static IEnumerator BulkBuyCapFoodsCoroutine(TradeUIController tradeUIController)
        {
            if (tradeUIController == null)
            {
                yield break;
            }

            HashSet<ItemIdentity> blockedItems = new();
            int initialQueuedCount = GetQueuedItemCount(tradeUIController);
            bool addedAny = false;

            for (int iteration = 0; iteration < MaxBulkBuyIterations; iteration++)
            {
                ItemIconController nextIcon = FindNextTargetIcon(tradeUIController, blockedItems);
                if (nextIcon == null)
                {
                    break;
                }

                ItemData item = nextIcon.itemData;
                ItemIdentity identity = ItemIdentity.FromItem(item);
                int beforeQueuedCount = GetQueuedItemCount(tradeUIController);
                int beforeDeltaMoney = tradeUIController.deltaMoney;

                nextIcon.OnClick();

                int afterQueuedCount = GetQueuedItemCount(tradeUIController);
                int afterDeltaMoney = tradeUIController.deltaMoney;
                bool changed = afterQueuedCount != beforeQueuedCount || afterDeltaMoney != beforeDeltaMoney;
                if (!changed)
                {
                    blockedItems.Add(identity);
                    continue;
                }

                addedAny = true;
                yield return null;
            }

            // 统一复用原版交易结算，银钱和功绩两套扣费都由游戏自身处理。
            if (addedAny && GetQueuedItemCount(tradeUIController) > initialQueuedCount)
            {
                tradeUIController.SureButtonClicked();
            }
        }

        private static bool IsSupportedTradeUiType(TradeUIType targetType)
        {
            return IsSupportedTradeUiTypeValue((int)targetType, null);
        }

        private static bool IsSupportedTradeUiTypeValue(int targetTypeValue, ItemListData rightItemList)
        {
            if (targetTypeValue == ForceStorageTradeUiTypeValue)
            {
                return true;
            }

            if (targetTypeValue == GovernTradeUiTypeValue)
            {
                return false;
            }

            if (targetTypeValue == (int)TradeUIType.Shop)
            {
                return true;
            }

            return rightItemList != null && rightItemList.forceID >= 0;
        }

        private static bool IsForceStorageTrade(TradeUIController tradeUIController)
        {
            return tradeUIController != null && (int)tradeUIController.tradeUIType == ForceStorageTradeUiTypeValue;
        }

        private static ItemIconController FindNextTargetIcon(TradeUIController tradeUIController, HashSet<ItemIdentity> blockedItems)
        {
            ItemListController merchantList = tradeUIController?.rightList;
            if (merchantList?.itemGrid == null)
            {
                return null;
            }

            ItemIconController[] icons = merchantList.itemGrid.GetComponentsInChildren<ItemIconController>(true);
            if (icons == null)
            {
                return null;
            }

            for (int i = 0; i < icons.Length; i++)
            {
                ItemIconController icon = icons[i];
                if (icon == null || icon.gameObject == null || !icon.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ItemData item = icon?.itemData;
                if (!FoodAutomationItemUtility.IsCapBoostFood(item))
                {
                    continue;
                }

                ItemIdentity identity = ItemIdentity.FromItem(item);
                if (!identity.IsValid || blockedItems.Contains(identity))
                {
                    continue;
                }

                return icon;
            }

            return null;
        }

        private static int GetQueuedItemCount(TradeUIController tradeUIController)
        {
            return tradeUIController?.rightOutList?.targetItemList?.allItem?.Count ?? 0;
        }

        private readonly struct ItemIdentity : IEquatable<ItemIdentity>
        {
            private ItemIdentity(bool isValid, ItemType type, int itemId, int subType, int itemLv, int rareLv)
            {
                IsValid = isValid;
                Type = type;
                ItemId = itemId;
                SubType = subType;
                ItemLv = itemLv;
                RareLv = rareLv;
            }

            internal bool IsValid { get; }
            private ItemType Type { get; }
            private int ItemId { get; }
            private int SubType { get; }
            private int ItemLv { get; }
            private int RareLv { get; }

            internal static ItemIdentity FromItem(ItemData item)
            {
                if (item == null)
                {
                    return default;
                }

                return new ItemIdentity(true, item.type, item.itemID, item.subType, item.itemLv, item.rareLv);
            }

            public bool Equals(ItemIdentity other)
            {
                return IsValid == other.IsValid &&
                       Type == other.Type &&
                       ItemId == other.ItemId &&
                       SubType == other.SubType &&
                       ItemLv == other.ItemLv &&
                       RareLv == other.RareLv;
            }

            public override bool Equals(object obj)
            {
                return obj is ItemIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine((int)Type, ItemId, SubType, ItemLv, RareLv, IsValid);
            }
        }
    }
}
