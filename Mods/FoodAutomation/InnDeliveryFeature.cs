using System;
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
    internal static class InnDeliveryTradeUIPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TradeUIController.ShowTradeUI), typeof(TradeUIType), typeof(ItemListData), typeof(ItemListData), typeof(bool))]
        private static void Postfix(TradeUIController __instance, TradeUIType targetType, ItemListData leftItemList, ItemListData rightItemList)
        {
            try
            {
                InnDeliveryButtons.RemoveInjectedButtons(__instance);
                bool shouldInject = InnDeliveryService.ShouldInjectUi(__instance, targetType, rightItemList);

                if (!shouldInject)
                {
                    return;
                }

                InnDeliveryButtons.Inject(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 注入自动购买按钮时发生异常：{ex}");
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
        private static void PostfixExtended(TradeUIController __instance, TradeUIType targetType, ItemListData rightItemList)
        {
            try
            {
                InnDeliveryButtons.RemoveInjectedButtons(__instance);
                bool shouldInject = InnDeliveryService.ShouldInjectUi(__instance, targetType, rightItemList);

                if (!shouldInject)
                {
                    return;
                }

                InnDeliveryButtons.Inject(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 注入自动购买按钮时发生异常：{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(TradeUIController), nameof(TradeUIController.HideTradeUI))]
    internal static class InnDeliveryHideTradeUIPatch
    {
        private static void Postfix(TradeUIController __instance)
        {
            InnDeliveryButtons.RemoveInjectedButtons(__instance);
            InnDeliveryService.ClearCurrentUiSession();
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.ChangeMonth))]
    internal static class InnDeliveryChangeMonthPatch
    {
        private static void Postfix(GameController __instance)
        {
            try
            {
                InnDeliveryService.TryAutoRun(__instance);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 执行月度自动购买全图加上限食物时发生异常：{ex}");
            }
        }
    }

    internal static class InnDeliveryButtons
    {
        private const string ButtonNamePrefix = "FoodAutomation_InnDelivery_";
        private const string SureButtonName = "SureButton";
        private const float TopRowOffsetY = -78f;
        private const float ButtonGap = 20f;
        private const int ButtonFontSize = 14;

        internal static void Inject(TradeUIController tradeUIController)
        {
            Button templateButton = TradeUiButtonUtility.FindButton(tradeUIController?.tradeUI, SureButtonName);
            Transform rootTransform = tradeUIController?.tradeUI?.transform;
            if (templateButton == null)
            {
                Main.Log.Warning("[FoodAutomation] 酒馆交易界面中未找到 SureButton，跳过酒馆外卖按钮注入。");
                return;
            }
            if (rootTransform == null)
            {
                Main.Log.Warning("[FoodAutomation] 酒馆交易界面缺少根节点，跳过酒馆外卖按钮注入。");
                return;
            }

            Button toggleButton = TradeUiButtonUtility.CreateButtonFromTemplate(
                templateButton,
                rootTransform,
                ButtonNamePrefix + "Toggle",
                GetToggleLabel(),
                ButtonFontSize,
                (UnityAction)(() => OnToggleClicked(tradeUIController)));
            Button reserveButton = TradeUiButtonUtility.CreateButtonFromTemplate(
                templateButton,
                rootTransform,
                ButtonNamePrefix + "Reserve",
                GetReserveLabel(),
                ButtonFontSize,
                (UnityAction)(() => OnReserveClicked(tradeUIController)));
            Button testButton = TradeUiButtonUtility.CreateButtonFromTemplate(
                templateButton,
                rootTransform,
                ButtonNamePrefix + "Test",
                "购买全图加上限食物",
                ButtonFontSize,
                (UnityAction)(() => OnTestClicked(tradeUIController)));

            if (toggleButton == null || reserveButton == null || testButton == null)
            {
                return;
            }

            PositionTopRow(templateButton, toggleButton, reserveButton, testButton);
            InnDeliveryService.MarkCurrentUiSession();
        }

        internal static void RemoveInjectedButtons(TradeUIController tradeUIController)
        {
            TradeUiButtonUtility.RemoveButtonsByPrefix(tradeUIController?.tradeUI, ButtonNamePrefix);
        }

        internal static void Refresh(TradeUIController tradeUIController)
        {
            RemoveInjectedButtons(tradeUIController);
            if (tradeUIController == null || tradeUIController.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return;
            }

            if (!InnDeliveryService.ShouldKeepUiVisible(tradeUIController) &&
                !InnDeliveryService.ShouldShowOnCurrentUi(tradeUIController, tradeUIController.rightList?.targetItemList))
            {
                return;
            }

            Inject(tradeUIController);
        }

        private static void OnToggleClicked(TradeUIController tradeUIController)
        {
            FoodAutomationConfig.ToggleInnDelivery();
            Refresh(tradeUIController);
        }

        private static void OnReserveClicked(TradeUIController tradeUIController)
        {
            FoodAutomationConfig.CycleInnDeliveryReserve();
            Refresh(tradeUIController);
        }

        private static void OnTestClicked(TradeUIController tradeUIController)
        {
            try
            {
                InnDeliveryResult result = InnDeliveryService.RunOnce(showInfo: true, manualTrigger: true);
                RefreshTradeUi(tradeUIController);
                if (result.PurchasedItemCount <= 0)
                {
                    return;
                }

                TradeBulkBuyButtons.RemoveInjectedButtons(tradeUIController);
                if (TradeBulkBuyButtons.ShouldInject(tradeUIController))
                {
                    TradeBulkBuyButtons.Inject(tradeUIController);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error($"[FoodAutomation] 执行测试全图加上限食物购买时发生异常：{ex}");
            }
        }

        private static void RefreshTradeUi(TradeUIController tradeUIController)
        {
            if (tradeUIController?.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return;
            }

            ItemListController leftList = tradeUIController.leftList;
            ItemListController rightList = tradeUIController.rightList;
            leftList?.RefreshItemList(leftList.targetItemList, true);
            rightList?.RefreshItemList(rightList.targetItemList, true);
            tradeUIController.FreshResourceLabel();
            Refresh(tradeUIController);
        }

        private static string GetToggleLabel()
        {
            return FoodAutomationConfig.EnableInnDelivery.Value ? "自动购买全图加上限食物:开" : "自动购买全图加上限食物:关";
        }

        private static string GetReserveLabel()
        {
            return $"保留银钱:{FoodAutomationItemUtility.FormatMoneyShort(FoodAutomationConfig.InnDeliveryReserveMoney.Value)}";
        }

        private static void PositionTopRow(Button templateButton, Button toggleButton, Button reserveButton, Button testButton)
        {
            RectTransform templateRect = templateButton.GetComponent<RectTransform>();
            RectTransform toggleRect = toggleButton.GetComponent<RectTransform>();
            RectTransform reserveRect = reserveButton.GetComponent<RectTransform>();
            RectTransform testRect = testButton.GetComponent<RectTransform>();
            if (templateRect == null || toggleRect == null || reserveRect == null || testRect == null)
            {
                return;
            }

            float templateHeight = templateRect.sizeDelta.y;
            float toggleWidth = TradeUiButtonUtility.GetPreferredButtonWidth(toggleButton.gameObject, templateRect.sizeDelta.x);
            float reserveWidth = TradeUiButtonUtility.GetPreferredButtonWidth(reserveButton.gameObject, templateRect.sizeDelta.x);
            float testWidth = TradeUiButtonUtility.GetPreferredButtonWidth(testButton.gameObject, templateRect.sizeDelta.x);

            ConfigureTopButtonRect(toggleRect, toggleWidth, templateHeight);
            ConfigureTopButtonRect(reserveRect, reserveWidth, templateHeight);
            ConfigureTopButtonRect(testRect, testWidth, templateHeight);

            float totalWidth = toggleWidth + reserveWidth + testWidth + (ButtonGap * 2f);
            float startX = -totalWidth * 0.5f;

            toggleRect.anchoredPosition = new Vector2(startX + (toggleWidth * 0.5f), TopRowOffsetY);
            reserveRect.anchoredPosition = new Vector2(startX + toggleWidth + ButtonGap + (reserveWidth * 0.5f), TopRowOffsetY);
            testRect.anchoredPosition = new Vector2(startX + toggleWidth + reserveWidth + (ButtonGap * 2f) + (testWidth * 0.5f), TopRowOffsetY);
        }

        private static void ConfigureTopButtonRect(RectTransform buttonRect, float width, float height)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = new Vector2(width, height);
        }
    }

    internal static class InnDeliveryService
    {
        // 外门门派仓库批量购买诊断日志前缀。
        // 如后续需要继续排查“是否识别到门派仓库 / 为什么没有买入”，可恢复下方对应的 Main.Log.Msg。
        // private const string DiagPrefix = "[FoodAutomation][InnDeliveryDiag]";
        private const int TownMarketBuildingId = 16;
        private const int TownTavernBuildingId = 39;
        private const int DujiangyanBuildingId = 48;
        private const int ForceMoneyResourceId = 0;
        private const float OtherForceStorageRequireFavor = 40f;
        private const float OtherForceStorageBaseFavor = 50f;
        private const float OtherForceStorageFavorToLevelRate = 0.1f;
        private static int _lastAutoRunMonthKey = -1;
        private static bool _currentInnTradeUiSession;
        private static ItemListData _currentVisibleShopItemList;

        internal static void MarkCurrentUiSession()
        {
            _currentInnTradeUiSession = true;
        }

        internal static void ClearCurrentUiSession()
        {
            _currentInnTradeUiSession = false;
            _currentVisibleShopItemList = null;
        }

        internal static bool ShouldKeepUiVisible(TradeUIController tradeUIController)
        {
            return _currentInnTradeUiSession &&
                   tradeUIController != null &&
                   tradeUIController.tradeUI != null &&
                   tradeUIController.tradeUI.activeInHierarchy;
        }

        internal static void RememberVisibleShop(ItemListData shopItemList)
        {
            _currentVisibleShopItemList = shopItemList;
        }

        internal static bool IsTownDeliveryShop(ItemListData shopItemList)
        {
            if (shopItemList == null)
            {
                return false;
            }

            WorldData worldData = GameController.Instance?.worldData;
            nint targetPointer = GetNativePointer(shopItemList);
            if (worldData?.Areas == null || targetPointer == 0)
            {
                return false;
            }

            HashSet<int> townAreaIds = CollectTownAreaIds(worldData);
            if (townAreaIds.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < worldData.Areas.Count; i++)
            {
                AreaData area = worldData.Areas[i];
                if (area == null || !townAreaIds.Contains(area.areaID) || area.areaTiles == null)
                {
                    continue;
                }

                for (int j = 0; j < area.areaTiles.Count; j++)
                {
                    AreaTileData tile = area.areaTiles[j];
                    AreaBuildingData building = tile?.building;
                    if (building == null || !IsSupportedDeliveryBuilding(building.buildingID) || building.shopItemList == null)
                    {
                        continue;
                    }

                    if (GetNativePointer(building.shopItemList) == targetPointer)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool IsInnShop(ItemListData shopItemList)
        {
            if (shopItemList == null)
            {
                return false;
            }

            WorldData worldData = GameController.Instance?.worldData;
            nint targetPointer = GetNativePointer(shopItemList);
            if (worldData?.Inns == null || targetPointer == 0)
            {
                return false;
            }

            for (int i = 0; i < worldData.Inns.Count; i++)
            {
                InnData inn = worldData.Inns[i];
                if (GetNativePointer(inn?.shopItemList) == targetPointer)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldShowOnCurrentUi(TradeUIController tradeUIController, ItemListData shopItemList)
        {
            return ShouldShowOnCurrentUi(tradeUIController, tradeUIController?.tradeUIType ?? default, shopItemList);
        }

        internal static bool ShouldShowOnCurrentUi(TradeUIController tradeUIController, TradeUIType targetType, ItemListData shopItemList)
        {
            if (tradeUIController == null || tradeUIController.tradeUI == null || !tradeUIController.tradeUI.activeInHierarchy)
            {
                return false;
            }

            if (targetType == TradeUIType.Shop &&
                (IsTownDeliveryShop(shopItemList) || IsInnShop(shopItemList)))
            {
                return true;
            }

            return IsOtherForceStorageShop(shopItemList);
        }

        internal static bool ShouldInjectUi(TradeUIController tradeUIController, TradeUIType targetType, ItemListData shopItemList)
        {
            if (!ShouldShowOnCurrentUi(tradeUIController, targetType, shopItemList))
            {
                return false;
            }

            RememberVisibleShop(shopItemList);
            return true;
        }

        internal static void TryAutoRun(GameController gameController)
        {
            if (!FoodAutomationConfig.EnableInnDelivery.Value)
            {
                return;
            }

            int monthKey = GetCurrentMonthKey(gameController);
            if (monthKey <= 0 || monthKey == _lastAutoRunMonthKey)
            {
                return;
            }

            _lastAutoRunMonthKey = monthKey;
            RunOnce(showInfo: true, manualTrigger: false);
        }

        internal static InnDeliveryResult RunOnce(bool showInfo, bool manualTrigger, bool ignoreReserve = false)
        {
            GameController gameController = GameController.Instance;
            WorldData worldData = gameController?.worldData;
            HeroData player = worldData?.Player();
            if (worldData?.Areas == null || player?.itemListData == null)
            {
                return InnDeliveryResult.Empty;
            }

            int reserveMoney = Math.Max(0, FoodAutomationConfig.InnDeliveryReserveMoney.Value);
            int startMoney = player.itemListData.money;
            // Main.Log.Msg($"{DiagPrefix} RunOnce: mode={(manualTrigger ? "manual" : "auto")}, ignoreReserve={ignoreReserve}, startMoney={startMoney}, reserveMoney={reserveMoney}");
            if (!ignoreReserve && startMoney <= reserveMoney)
            {
                // Main.Log.Msg($"{DiagPrefix} RunOnce: 跳过执行，当前银钱未超过保留阈值。");
                if (showInfo)
                {
                    ShowResult(result: InnDeliveryResult.Empty, reserveMoney, manualTrigger, "当前银钱未超过保留阈值");
                }

                return InnDeliveryResult.Empty;
            }

            InnDeliveryResult result = new(startMoney);
            HashSet<nint> processedLists = new();
            HashSet<int> townAreaIds = CollectTownAreaIds(worldData);
            HashSet<nint> processedBuildings = new();
            for (int i = 0; i < worldData.Areas.Count; i++)
            {
                AreaData area = worldData.Areas[i];
                if (area == null || !townAreaIds.Contains(area.areaID) || area.areaTiles == null)
                {
                    continue;
                }

                for (int j = 0; j < area.areaTiles.Count; j++)
                {
                    AreaTileData tile = area.areaTiles[j];
                    AreaBuildingData building = tile?.building;
                    if (building == null || building.shopItemList == null || !IsSupportedDeliveryBuilding(building.buildingID))
                    {
                        continue;
                    }

                    nint buildingPointer = GetNativePointer(building);
                    if (buildingPointer != 0 && !processedBuildings.Add(buildingPointer))
                    {
                        continue;
                    }

                    string sourceName = GetTownShopSourceName(area, tile, building);
                    result = PurchaseFromShopList(player, building.shopItemList, reserveMoney, ignoreReserve, sourceName, result, processedLists);
                }
            }

            result = PurchaseFromInnShops(worldData, player, reserveMoney, ignoreReserve, result, processedLists);
            result = PurchaseFromOtherForceStorages(worldData, player, reserveMoney, ignoreReserve, result, processedLists);

            if (showInfo)
            {
                ShowResult(result, reserveMoney, manualTrigger, null);
            }

            if (result.PurchasedItemCount > 0)
            {
                Main.Log.Msg($"[FoodAutomation] 全图加上限食物购买完成：购入 {result.PurchasedItemCount} 件，访问 {result.VisitedSourceCount} 个来源，花费 {result.TotalSpent}。");
            }
            else
            {
                // Main.Log.Msg($"{DiagPrefix} RunOnce: 本次未购入任何目标食物。");
            }

            return result;
        }

        private static string GetTownShopSourceName(AreaData area, AreaTileData tile, AreaBuildingData building)
        {
            string areaName = area?.areaName ?? "<null>";
            string buildingName = GetBuildingName(building);
            int row = tile?.row ?? -1;
            int column = tile?.column ?? -1;
            int buildingId = building?.buildingID ?? -1;
            return $"建筑商店[{area?.areaID ?? -1}:{areaName}/{buildingName}#{buildingId}@{row},{column}]";
        }

        private static string GetForceStorageSourceName(ForceData force)
        {
            if (force == null)
            {
                return "门派仓库[<null>]";
            }

            string forceName = force.forceName ?? "<null>";
            return $"门派仓库[{force.forceID}:{forceName}]";
        }

        private static string GetInnSourceName(InnData inn)
        {
            if (inn == null)
            {
                return "客栈[<null>]";
            }

            string innName = inn.innName ?? "<null>";
            int nearAreaId = (inn.nearAreaID != null && inn.nearAreaID.Count > 0) ? inn.nearAreaID[0] : -1;
            return $"客栈[{inn.id}:{innName}@nearArea={nearAreaId}]";
        }

        private static bool IsSupportedDeliveryBuilding(int buildingId)
        {
            return buildingId == TownMarketBuildingId ||
                   buildingId == TownTavernBuildingId ||
                   buildingId == DujiangyanBuildingId;
        }

        private static bool IsOtherForceStorageShop(ItemListData shopItemList)
        {
            WorldData worldData = GameController.Instance?.worldData;
            HeroData player = worldData?.Player();
            return TryGetOtherForceStorageOwner(worldData, player, shopItemList, out _, out _);
        }

        private static HashSet<int> CollectTownAreaIds(WorldData worldData)
        {
            HashSet<int> result = new();
            AddAreaIds(worldData?.cityAreaID, result);
            AddAreaIds(worldData?.villageAreaID, result);
            return result;
        }

        private static void AddAreaIds(Il2CppSystem.Collections.Generic.List<int> areaIds, HashSet<int> target)
        {
            if (areaIds == null || target == null)
            {
                return;
            }

            for (int i = 0; i < areaIds.Count; i++)
            {
                target.Add(areaIds[i]);
            }
        }

        private static string GetBuildingName(AreaBuildingData building)
        {
            try
            {
                string name = building?.DataBase()?.name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch
            {
            }

            return $"建筑";
        }

        private static int CalculateBuyPrice(HeroData player, ItemData item)
        {
            if (player == null || item == null)
            {
                return 0;
            }

            float tradeRate = player.GetTradeValueRate(true);
            int price = Mathf.RoundToInt(item.value * tradeRate);
            return Mathf.Max(price, 1);
        }

        private static int CalculateForceStorageBuyPrice(HeroData player, ItemData item, ItemListData forceStorage)
        {
            if (player == null || item == null || forceStorage == null)
            {
                return 0;
            }

            float tradeRate = player.GetTradeValueRate(true);
            float discount = player.GetForceStorageDiscount(forceStorage);
            int price = Mathf.RoundToInt(item.value * tradeRate * discount);
            return Mathf.Max(price, 1);
        }

        private static bool CanCarryItem(HeroData player, ItemData item)
        {
            ItemListData inventory = player?.itemListData;
            if (inventory == null || item == null || inventory.maxWeight <= 0f)
            {
                return true;
            }

            return inventory.weight + item.weight <= inventory.maxWeight + 0.001f;
        }

        private static int GetCurrentMonthKey(GameController gameController)
        {
            TimeData time = gameController?.worldData?.worldTime;
            if (time == null)
            {
                return -1;
            }

            return (time.year * 100) + time.month;
        }

        private static void ShowResult(InnDeliveryResult result, int reserveMoney, bool manualTrigger, string fallbackMessage)
        {
            GameController gameController = GameController.Instance;
            if (result.PurchasedItemCount > 0)
            {
                string successPrefix = manualTrigger ? "购买成功" : "自动购买加上限食物成功";
                string message = $"{successPrefix}：买入{result.PurchasedItemCount}件，花费{result.TotalSpent}，剩余{result.EndMoney}";
                ShowInfoTab(message);
                if (manualTrigger)
                {
                    gameController?.ShowTextOnMouse(message);
                }

                return;
            }

            string failurePrefix = manualTrigger ? "购买失败" : "自动购买加上限食物失败";
            string failureMessage;
            if (!string.IsNullOrEmpty(fallbackMessage))
            {
                failureMessage = $"{failurePrefix}：{fallbackMessage}";
            }
            else
            {
                failureMessage = $"{failurePrefix}：未购入目标食物，保留银钱{FoodAutomationItemUtility.FormatMoneyShort(reserveMoney)}";
            }

            ShowInfoTab(failureMessage);
            if (manualTrigger)
            {
                gameController?.ShowTextOnMouse(failureMessage);
            }
        }

        private static void ShowInfoTab(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                InfoController infoController = InfoController.Instance;
                infoController?.AddInfoTab(message);
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"[FoodAutomation] 发送信息提示失败：{ex.Message}");
            }
        }

        private static InnDeliveryResult PurchaseFromShopList(HeroData player, ItemListData shopItemList, int reserveMoney, bool ignoreReserve, string sourceName, InnDeliveryResult result, HashSet<nint> processedLists)
        {
            nint pointer = GetNativePointer(shopItemList);
            if (pointer == 0 || !processedLists.Add(pointer))
            {
                return result;
            }

            if (shopItemList.allItem == null || shopItemList.allItem.Count == 0)
            {
                return result;
            }

            List<ItemData> candidates = new();
            for (int j = 0; j < shopItemList.allItem.Count; j++)
            {
                ItemData item = shopItemList.allItem[j];
                if (FoodAutomationItemUtility.IsCapBoostFood(item))
                {
                    candidates.Add(item);
                }
            }

            if (candidates.Count == 0)
            {
                return result;
            }

            bool boughtFromThisShop = false;
            for (int j = 0; j < candidates.Count; j++)
            {
                ItemData item = candidates[j];
                int currentMoney = player.itemListData.money;
                int price = CalculateBuyPrice(player, item);
                bool reserveBlocked = !ignoreReserve && currentMoney - price < reserveMoney;
                bool carryBlocked = !CanCarryItem(player, item);
                if (price <= 0 || reserveBlocked || carryBlocked)
                {
                    continue;
                }

                // 这里直接走数据层搬运，避免为了远程采购再去驱动一遍交易 UI 的排队和确认逻辑。
                shopItemList.LoseItem(item, false);
                player.ChangeMoney(-price, false);
                player.GetItem(item, false, false);

                result = result.WithPurchase(price);
                boughtFromThisShop = true;
            }

            if (boughtFromThisShop)
            {
                result = result.WithSourceVisited();
            }

            return result;
        }

        private static InnDeliveryResult PurchaseFromOtherForceStorages(WorldData worldData, HeroData player, int reserveMoney, bool ignoreReserve, InnDeliveryResult result, HashSet<nint> processedLists)
        {
            if (worldData?.Forces == null || player == null)
            {
                return result;
            }

            // Main.Log.Msg($"{DiagPrefix} 开始扫描外门门派仓库：forceCount={worldData.Forces.Count}, playerBelongForce={player.belongForceID}, playerServantForce={player.servantForceID}, heroForceLv={player.heroForceLv}");
            for (int i = 0; i < worldData.Forces.Count; i++)
            {
                ForceData force = worldData.Forces[i];
                if (!TryGetOtherForceStorageMaxItemLv(player, force, out int maxItemLv))
                {
                    continue;
                }

                ItemListData forceStorage = force?.forceStorage;
                if (forceStorage == null)
                {
                    continue;
                }

                string sourceName = GetForceStorageSourceName(force);
                // Main.Log.Msg($"{DiagPrefix} 命中外门门派仓库：source={sourceName}, maxItemLv={maxItemLv}, itemCount={forceStorage.allItem?.Count ?? 0}");
                result = PurchaseFromForceStorage(player, force, forceStorage, maxItemLv, reserveMoney, ignoreReserve, sourceName, result, processedLists);
            }

            return result;
        }

        private static InnDeliveryResult PurchaseFromInnShops(WorldData worldData, HeroData player, int reserveMoney, bool ignoreReserve, InnDeliveryResult result, HashSet<nint> processedLists)
        {
            if (worldData?.Inns == null || player == null)
            {
                return result;
            }

            for (int i = 0; i < worldData.Inns.Count; i++)
            {
                InnData inn = worldData.Inns[i];
                ItemListData shopItemList = inn?.shopItemList;
                if (shopItemList == null)
                {
                    continue;
                }

                string sourceName = GetInnSourceName(inn);
                result = PurchaseFromShopList(player, shopItemList, reserveMoney, ignoreReserve, sourceName, result, processedLists);
            }

            return result;
        }

        private static bool TryGetOtherForceStorageOwner(WorldData worldData, HeroData player, ItemListData forceStorage, out ForceData ownerForce, out int maxItemLv)
        {
            ownerForce = null;
            maxItemLv = 0;
            if (worldData?.Forces == null || player == null || forceStorage == null)
            {
                return false;
            }

            nint targetPointer = GetNativePointer(forceStorage);
            if (targetPointer == 0)
            {
                return false;
            }

            for (int i = 0; i < worldData.Forces.Count; i++)
            {
                ForceData force = worldData.Forces[i];
                if (!TryGetOtherForceStorageMaxItemLv(player, force, out int currentMaxItemLv))
                {
                    continue;
                }

                if (GetNativePointer(force?.forceStorage) != targetPointer)
                {
                    continue;
                }

                ownerForce = force;
                maxItemLv = currentMaxItemLv;
                return true;
            }

            return false;
        }

        private static bool TryGetOtherForceStorageMaxItemLv(HeroData player, ForceData force, out int maxItemLv)
        {
            maxItemLv = 0;
            if (player == null || force == null || force.forceStorage == null)
            {
                return false;
            }

            int forceId = force.forceID;
            if (forceId < 0)
            {
                // Main.Log.Msg($"{DiagPrefix} 跳过门派仓库：forceID<0, forceName={force?.forceName ?? "<null>"}");
                return false;
            }

            if (player.belongForceID == forceId || player.servantForceID == forceId)
            {
                // Main.Log.Msg($"{DiagPrefix} 跳过门派仓库：source={GetForceStorageSourceName(force)}, reason=本门或附属门派");
                return false;
            }

            if (player.belongForceID < 0)
            {
                maxItemLv = Math.Max(0, player.heroForceLv);
                // Main.Log.Msg($"{DiagPrefix} 外门门派仓库可购买：source={GetForceStorageSourceName(force)}, reason=玩家无门派, maxItemLv={maxItemLv}");
                return true;
            }

            float favor = force.GetForceFavor(player.belongForceID);
            if (favor <= OtherForceStorageRequireFavor)
            {
                // Main.Log.Msg($"{DiagPrefix} 跳过门派仓库：source={GetForceStorageSourceName(force)}, reason=势力好感不足, favor={favor:0.##}, require>{OtherForceStorageRequireFavor:0.##}");
                return false;
            }

            int calculatedLv = (int)((favor - OtherForceStorageBaseFavor) * OtherForceStorageFavorToLevelRate);
            maxItemLv = Math.Max(0, calculatedLv);
            // Main.Log.Msg($"{DiagPrefix} 外门门派仓库可购买：source={GetForceStorageSourceName(force)}, favor={favor:0.##}, maxItemLv={maxItemLv}");
            return true;
        }

        private static InnDeliveryResult PurchaseFromForceStorage(HeroData player, ForceData force, ItemListData forceStorage, int maxItemLv, int reserveMoney, bool ignoreReserve, string sourceName, InnDeliveryResult result, HashSet<nint> processedLists)
        {
            nint pointer = GetNativePointer(forceStorage);
            if (pointer == 0 || !processedLists.Add(pointer))
            {
                // Main.Log.Msg($"{DiagPrefix} 跳过门派仓库采购：source={sourceName}, reason=列表为空或已处理");
                return result;
            }

            if (forceStorage.allItem == null || forceStorage.allItem.Count == 0)
            {
                // Main.Log.Msg($"{DiagPrefix} 跳过门派仓库采购：source={sourceName}, reason=仓库为空");
                return result;
            }

            List<ItemData> candidates = new();
            for (int i = 0; i < forceStorage.allItem.Count; i++)
            {
                ItemData item = forceStorage.allItem[i];
                if (item == null || item.itemLv > maxItemLv)
                {
                    continue;
                }

                if (FoodAutomationItemUtility.IsCapBoostFood(item))
                {
                    candidates.Add(item);
                }
            }

            if (candidates.Count == 0)
            {
                // Main.Log.Msg($"{DiagPrefix} 门派仓库无可购目标：source={sourceName}, maxItemLv={maxItemLv}");
                return result;
            }

            // Main.Log.Msg($"{DiagPrefix} 门派仓库候选食物：source={sourceName}, maxItemLv={maxItemLv}, candidates={DescribeItems(candidates)}");
            bool boughtFromThisSource = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                ItemData item = candidates[i];
                int currentMoney = player.itemListData.money;
                int price = CalculateForceStorageBuyPrice(player, item, forceStorage);
                bool reserveBlocked = !ignoreReserve && currentMoney - price < reserveMoney;
                bool carryBlocked = !CanCarryItem(player, item);
                if (price <= 0 || reserveBlocked || carryBlocked)
                {
                    // Main.Log.Msg($"{DiagPrefix} 门派仓库跳过食物：source={sourceName}, item={DescribeItem(item)}, price={price}, currentMoney={currentMoney}, reserveBlocked={reserveBlocked}, carryBlocked={carryBlocked}, ignoreReserve={ignoreReserve}");
                    continue;
                }

                player.ChangeMoney(-price, false);
                force?.ChangeResource(ForceMoneyResourceId, price, false, false);
                forceStorage.LoseItem(item, false);
                player.GetItem(item, false, false);

                // Main.Log.Msg($"{DiagPrefix} 门派仓库购入成功：source={sourceName}, item={DescribeItem(item)}, price={price}, moneyAfter={player.itemListData.money}");
                result = result.WithPurchase(price);
                boughtFromThisSource = true;
            }

            if (boughtFromThisSource)
            {
                result = result.WithSourceVisited();
            }
            else
            {
                // Main.Log.Msg($"{DiagPrefix} 门派仓库最终未购入：source={sourceName}");
            }

            return result;
        }

        private static string DescribeItems(List<ItemData> items)
        {
            if (items == null || items.Count == 0)
            {
                return "<empty>";
            }

            List<string> parts = new(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                parts.Add(DescribeItem(items[i]));
            }

            return string.Join(" | ", parts);
        }

        private static string DescribeItem(ItemData item)
        {
            if (item == null)
            {
                return "<null>";
            }

            string itemName = item.Name(false) ?? $"item#{item.itemID}";
            return $"{itemName}(id={item.itemID}, type={item.type}, subType={item.subType}, lv={item.itemLv}, value={item.value})";
        }

        private static nint GetNativePointer(ItemListData itemListData)
        {
            try
            {
                if (itemListData == null)
                {
                    return 0;
                }

                return (nint)itemListData.Pointer;
            }
            catch
            {
                return 0;
            }
        }

        private static nint GetNativePointer(AreaBuildingData buildingData)
        {
            try
            {
                if (buildingData == null)
                {
                    return 0;
                }

                return (nint)buildingData.Pointer;
            }
            catch
            {
                return 0;
            }
        }
    }

    internal readonly struct InnDeliveryResult
    {
        internal static InnDeliveryResult Empty => new(0);

        internal InnDeliveryResult(int startMoney, int totalSpent = 0, int purchasedItemCount = 0, int visitedSourceCount = 0)
        {
            StartMoney = startMoney;
            TotalSpent = totalSpent;
            PurchasedItemCount = purchasedItemCount;
            VisitedSourceCount = visitedSourceCount;
        }

        internal int StartMoney { get; }
        internal int TotalSpent { get; }
        internal int PurchasedItemCount { get; }
        internal int VisitedSourceCount { get; }
        internal int EndMoney => StartMoney - TotalSpent;

        internal InnDeliveryResult WithPurchase(int price)
        {
            return new InnDeliveryResult(StartMoney, TotalSpent + price, PurchasedItemCount + 1, VisitedSourceCount);
        }

        internal InnDeliveryResult WithSourceVisited()
        {
            return new InnDeliveryResult(StartMoney, TotalSpent, PurchasedItemCount, VisitedSourceCount + 1);
        }
    }
}
