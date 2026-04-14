using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(ForcePermissionTweaks.Main), "ForcePermissionTweaks", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace ForcePermissionTweaks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static bool VerboseLog => ForcePermissionRuntime.Config.VerboseLog;

        public override void OnInitializeMelon()
        {
            ForcePermissionRuntime.Initialize(ForcePermissionConfigStore.Load(Log));

            Log.Msg(
                $"ForcePermissionTweaks 已加载。配置文件：{ForcePermissionConfigStore.ConfigPathDisplay}。建筑按钮覆盖={FormatEnabled(ForcePermissionRuntime.Config.EnableBuildButtonOverride)}，建筑显示模式={ForcePermissionConfigStore.FormatVisibilityMode(ForcePermissionRuntime.Config.BuildButtonVisibilityMode)}，建筑最低职位={ForcePermissionConfigStore.FormatRoleRequirement(ForcePermissionRuntime.Config.BuildButtonMinimumRole)}，藏经阁覆盖={FormatEnabled(ForcePermissionRuntime.Config.EnableSpeBookStorageOverride)}，藏经阁显示模式={ForcePermissionConfigStore.FormatVisibilityMode(ForcePermissionRuntime.Config.SpeBookStorageVisibilityMode)}，藏经阁最低职位={ForcePermissionConfigStore.FormatRoleRequirement(ForcePermissionRuntime.Config.SpeBookStorageMinimumRole)}，VerboseLog={FormatEnabled(VerboseLog)}。");
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLog)
            {
                Log.Msg(message);
            }
        }

        private static string FormatEnabled(bool value)
        {
            return value ? "开" : "关";
        }
    }

    internal static class ForcePermissionRuntime
    {
        private static readonly HashSet<string> SpeBookStorageCallFunctions = new(StringComparer.Ordinal)
        {
            "ManageBookStore",
            "ShowBookStore",
            "ChooseReadBook",
            "ChooseReadBookMoney",
            "ShowBookWriter",
            "ShowBookWriterSelf",
            "StartSpeBookStorage"
        };

        private static ForcePermissionConfig _config = ForcePermissionConfig.CreateDefault();
        private static bool _missingButtonWarningShown;
        private static bool _patchErrorShown;

        internal static ForcePermissionConfig Config => _config;

        internal static void Initialize(ForcePermissionConfig config)
        {
            _config = config ?? ForcePermissionConfig.CreateDefault();
            _missingButtonWarningShown = false;
            _patchErrorShown = false;
        }

        internal static void ApplyBuildButtonOverride(AreaBuildController controller)
        {
            if (!_config.EnableBuildButtonOverride || controller?.buildModeButton == null)
            {
                return;
            }

            try
            {
                GameObject buildModeButton = controller.buildModeButton;
                Button button = buildModeButton.GetComponent<Button>();
                if (button == null)
                {
                    if (!_missingButtonWarningShown)
                    {
                        _missingButtonWarningShown = true;
                        Main.Log.Warning("未能在建筑管理按钮对象上找到 Button 组件，已跳过按钮权限覆盖。");
                    }

                    return;
                }

                HeroData player = GameController.Instance?.worldData?.Player();
                AreaData areaData = AreaController.Instance?.areaData;

                WorldData worldData = GameController.Instance?.worldData;
                ForceData playerForce = player == null ? null : worldData?.GetHeroForce(player.heroID);

                bool shouldShow = ResolveBuildButtonVisibility(buildModeButton, player, playerForce, areaData);
                if (buildModeButton.activeSelf != shouldShow)
                {
                    buildModeButton.SetActive(shouldShow);
                }

                if (!shouldShow)
                {
                    return;
                }

                bool shouldInteract = ResolveBuildButtonInteractable(button, player);
                if (button.interactable != shouldInteract)
                {
                    button.interactable = shouldInteract;
                }

                Main.LogVerbose(
                    $"建筑管理按钮权限已覆盖：show={shouldShow}，interactable={shouldInteract}，areaForce={areaData?.belongForceID ?? -1}，playerForce={player?.belongForceID ?? -1}。");
            }
            catch (Exception ex)
            {
                if (!_patchErrorShown)
                {
                    _patchErrorShown = true;
                    Main.Log.Error($"覆盖建筑管理按钮权限失败：{ex}");
                }
            }
        }

        private static bool ResolveBuildButtonVisibility(GameObject buttonObject, HeroData player, ForceData playerForce, AreaData areaData)
        {
            switch (_config.BuildButtonVisibilityMode)
            {
                case BuildButtonVisibilityMode.Original:
                    return buttonObject.activeSelf;
                case BuildButtonVisibilityMode.PlayerForceAreaOnly:
                    return player != null &&
                        areaData != null &&
                        player.belongForceID > 0 &&
                        areaData.belongForceID == player.belongForceID;
                case BuildButtonVisibilityMode.PlayerForceAndServantForce:
                    return IsPlayerOrServantForceArea(player, playerForce, areaData);
                case BuildButtonVisibilityMode.PlayerForceServantAndAllyForce:
                    return IsPlayerServantOrAllyForceArea(player, playerForce, areaData);
                case BuildButtonVisibilityMode.Always:
                    return true;
                default:
                    return buttonObject.activeSelf;
            }
        }

        private static bool IsPlayerOrServantForceArea(HeroData player, ForceData playerForce, AreaData areaData)
        {
            if (player == null || areaData == null || player.belongForceID <= 0)
            {
                return false;
            }

            int areaForceId = areaData.belongForceID;
            if (areaForceId == player.belongForceID)
            {
                return true;
            }

            if (playerForce?.servantForce == null)
            {
                return false;
            }

            for (int i = 0; i < playerForce.servantForce.Count; i++)
            {
                if (playerForce.servantForce[i] == areaForceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlayerServantOrAllyForceArea(HeroData player, ForceData playerForce, AreaData areaData)
        {
            if (IsPlayerOrServantForceArea(player, playerForce, areaData))
            {
                return true;
            }

            if (player == null || playerForce?.allyForce == null || areaData == null)
            {
                return false;
            }

            int areaForceId = areaData.belongForceID;
            for (int i = 0; i < playerForce.allyForce.Count; i++)
            {
                if (playerForce.allyForce[i] == areaForceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ResolveBuildButtonInteractable(Button button, HeroData player)
        {
            ForceRoleRequirement requirement = _config.BuildButtonMinimumRole;
            if (requirement == ForceRoleRequirement.Original)
            {
                return button.interactable;
            }

            return MeetsRoleRequirement(player, requirement);
        }

        private static bool MeetsRoleRequirement(HeroData player, ForceRoleRequirement requirement)
        {
            if (player == null)
            {
                return false;
            }

            if (requirement == ForceRoleRequirement.Leader)
            {
                return player.isLeader;
            }

            if (player.isLeader)
            {
                return true;
            }

            return player.heroForceLv >= (int)requirement;
        }

        internal static void ApplySpeBookStorageOverride(BuildingUIController controller)
        {
            if (!_config.EnableSpeBookStorageOverride || controller?.buildingData == null)
            {
                return;
            }

            try
            {
                List<AreaBuildingChoice> choices = FindTargetBuildingChoices(controller.buildingData);
                if (choices.Count == 0)
                {
                    return;
                }

                HeroData player = GameController.Instance?.worldData?.Player();
                WorldData worldData = GameController.Instance?.worldData;
                ForceData playerForce = player == null ? null : worldData?.GetHeroForce(player.heroID);
                AreaData areaData = AreaController.Instance?.areaData;
                bool buildingAvailable = controller.buildingData.BuildingAvailable();
                int updatedCount = 0;

                for (int i = 0; i < choices.Count; i++)
                {
                    AreaBuildingChoice choice = choices[i];
                    bool originalVisible = EvaluateChoiceCondition(choice.mainCondition, choice.justNeedOneCondition, controller.buildingData);
                    bool originalRoleAllowed = EvaluateChoiceCondition(choice.subCondition, choice.justNeedOneCondition, controller.buildingData);
                    bool shouldShow = ResolveSpeBookStorageVisibility(player, playerForce, areaData, originalVisible);
                    bool roleAllowed = ResolveSpeBookStorageInteractable(player, originalRoleAllowed);
                    bool shouldInteract = shouldShow && buildingAvailable && roleAllowed;

                    GameObject buttonObject = FindBuildingChoiceButton(controller.buildingButtonGrid, choice.callFuc);
                    if (buttonObject == null && shouldShow)
                    {
                        buttonObject = CreateBuildingChoiceButton(controller, choice);
                    }

                    if (buttonObject == null)
                    {
                        continue;
                    }

                    if (buttonObject.activeSelf != shouldShow)
                    {
                        buttonObject.SetActive(shouldShow);
                    }

                    if (shouldShow)
                    {
                        Button button = buttonObject.GetComponent<Button>();
                        if (button != null && button.interactable != shouldInteract)
                        {
                            button.interactable = shouldInteract;
                        }

                        UpdateBuildingChoiceDetail(buttonObject, choice, roleAllowed, originalRoleAllowed);
                    }

                    updatedCount++;
                    Main.LogVerbose(
                        $"藏经阁按钮权限已覆盖：callFuc={choice.callFuc}，show={shouldShow}，interactable={shouldInteract}，originalVisible={originalVisible}，originalRoleAllowed={originalRoleAllowed}。");
                }

                if (updatedCount > 0)
                {
                    RepositionBuildingButtonGrid(controller.buildingButtonGrid);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error($"覆盖藏经阁按钮权限失败：{ex}");
            }
        }

        private static bool ResolveSpeBookStorageVisibility(HeroData player, ForceData playerForce, AreaData areaData, bool originalVisible)
        {
            switch (_config.SpeBookStorageVisibilityMode)
            {
                case BuildButtonVisibilityMode.Original:
                    return originalVisible;
                case BuildButtonVisibilityMode.PlayerForceAreaOnly:
                    return player != null &&
                        areaData != null &&
                        player.belongForceID > 0 &&
                        areaData.belongForceID == player.belongForceID;
                case BuildButtonVisibilityMode.PlayerForceAndServantForce:
                    return IsPlayerOrServantForceArea(player, playerForce, areaData);
                case BuildButtonVisibilityMode.PlayerForceServantAndAllyForce:
                    return IsPlayerServantOrAllyForceArea(player, playerForce, areaData);
                case BuildButtonVisibilityMode.Always:
                    return true;
                default:
                    return originalVisible;
            }
        }

        private static bool ResolveSpeBookStorageInteractable(HeroData player, bool originalRoleAllowed)
        {
            return _config.SpeBookStorageMinimumRole == ForceRoleRequirement.Original
                ? originalRoleAllowed
                : MeetsRoleRequirement(player, _config.SpeBookStorageMinimumRole);
        }

        private static bool EvaluateChoiceCondition(Il2CppSystem.Collections.Generic.List<string> condition, bool justNeedOneCondition, AreaBuildingData buildingData)
        {
            if (condition == null || condition.Count == 0)
            {
                return true;
            }

            return GameController.MeetCondition(condition, justNeedOneCondition, buildingData);
        }

        private static List<AreaBuildingChoice> FindTargetBuildingChoices(AreaBuildingData buildingData)
        {
            List<AreaBuildingChoice> targetChoices = new();
            AreaBuildingDataBase dataBase = buildingData?.DataBase();
            Il2CppSystem.Collections.Generic.List<AreaBuildingChoice> choices = dataBase?.areaBuildingChoices;
            if (choices == null)
            {
                return targetChoices;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                AreaBuildingChoice choice = choices[i];
                if (choice != null && IsSpeBookStorageChoice(choice.callFuc))
                {
                    targetChoices.Add(choice);
                }
            }

            return targetChoices;
        }

        private static bool IsSpeBookStorageChoice(string callFunction)
        {
            return !string.IsNullOrEmpty(callFunction) && SpeBookStorageCallFunctions.Contains(callFunction);
        }

        private static GameObject FindBuildingChoiceButton(GameObject buttonGrid, string callFunction)
        {
            Transform gridTransform = buttonGrid?.transform;
            if (gridTransform == null)
            {
                return null;
            }

            for (int i = 0; i < gridTransform.childCount; i++)
            {
                Transform child = gridTransform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                BuildingButtonController buttonController = child.GetComponent<BuildingButtonController>();
                if (buttonController?.areaBuildingChoice != null &&
                    string.Equals(buttonController.areaBuildingChoice.callFuc, callFunction, StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static GameObject CreateBuildingChoiceButton(BuildingUIController controller, AreaBuildingChoice choice)
        {
            if (controller == null || choice == null)
            {
                return null;
            }

            Il2CppSystem.Collections.Generic.List<string> originalMainCondition = choice.mainCondition;

            try
            {
                choice.mainCondition = new Il2CppSystem.Collections.Generic.List<string>();
                controller.CreateBuildingButton(choice);
            }
            finally
            {
                choice.mainCondition = originalMainCondition;
            }

            return FindBuildingChoiceButton(controller.buildingButtonGrid, choice.callFuc);
        }

        private static void UpdateBuildingChoiceDetail(GameObject buttonObject, AreaBuildingChoice choice, bool roleAllowed, bool originalRoleAllowed)
        {
            SimpleDetailText detailText = buttonObject?.GetComponent<SimpleDetailText>();
            if (detailText == null || choice == null)
            {
                return;
            }

            if (roleAllowed)
            {
                detailText.text = choice.describe ?? string.Empty;
                return;
            }

            if (_config.SpeBookStorageMinimumRole == ForceRoleRequirement.Original)
            {
                detailText.text = originalRoleAllowed
                    ? choice.describe ?? string.Empty
                    : GameController.GetConditionDescribe(choice.subCondition) ?? string.Empty;
                return;
            }

            detailText.text = BuildRoleRequirementText(_config.SpeBookStorageMinimumRole);
        }

        private static string BuildRoleRequirementText(ForceRoleRequirement requirement)
        {
            return requirement == ForceRoleRequirement.Leader
                ? "需要担任掌门。"
                : $"需要门派职位达到：{ForcePermissionConfigStore.FormatRoleRequirement(requirement)}。";
        }

        private static void RepositionBuildingButtonGrid(GameObject buttonGrid)
        {
            UIGrid grid = buttonGrid?.GetComponent<UIGrid>();
            if (grid != null)
            {
                grid.repositionNow = true;
            }
        }
    }

    [HarmonyPatch(typeof(AreaBuildController), nameof(AreaBuildController.Update))]
    internal static class AreaBuildController_Update_Patch
    {
        private static void Postfix(AreaBuildController __instance)
        {
            ForcePermissionRuntime.ApplyBuildButtonOverride(__instance);
        }
    }

    [HarmonyPatch(typeof(BuildingUIController), nameof(BuildingUIController.GenerateBuildingButton))]
    internal static class BuildingUIController_GenerateBuildingButton_Patch
    {
        private static void Postfix(BuildingUIController __instance)
        {
            ForcePermissionRuntime.ApplySpeBookStorageOverride(__instance);
        }
    }
}
