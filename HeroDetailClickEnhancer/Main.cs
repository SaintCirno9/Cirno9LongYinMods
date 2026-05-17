using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(HeroDetailClickEnhancer.Main), "HeroDetailClickEnhancer", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace HeroDetailClickEnhancer
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("HeroDetailClickEnhancer 已加载。");
        }
    }

    internal static class HeroDetailClickService
    {
        private static HeroData _remoteInteractHero;

        internal static void OpenHeroDetail(HeroData heroData)
        {
            if (heroData == null)
            {
                return;
            }

            HeroDetailController controller = HeroDetailController.Instance;
            if (controller == null)
            {
                return;
            }

            _remoteInteractHero = heroData;
            controller.ShowHeroDetail(heroData);
            EnableRemoteTalkButton(controller);
        }

        internal static void AddNameClick(Transform root, HeroData heroData)
        {
            if (root == null || heroData == null)
            {
                return;
            }

            Transform nameTransform = root.Find("Name");
            if (nameTransform == null)
            {
                return;
            }

            GameObject nameObject = nameTransform.gameObject;
            Button button = nameObject.GetComponent<Button>();
            if (button == null)
            {
                button = nameObject.AddComponent<Button>();
            }

            Text text = nameObject.GetComponent<Text>();
            if (text != null)
            {
                text.raycastTarget = true;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() => OpenHeroDetail(heroData)));
        }

        internal static void EnableHeroIconDetail(HeroIconController controller)
        {
            if (controller == null || controller.heroData == null || controller.heroIconType != HeroIconType.show)
            {
                return;
            }

            Button button = controller.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            HeroData heroData = controller.heroData;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() => OpenHeroDetail(heroData)));
        }

        internal static void EnableForceDetailHeroIcons(ForceDetailController controller)
        {
            if (controller?.forceHeroGrid == null)
            {
                return;
            }

            HeroIconController[] controllers = controller.forceHeroGrid.GetComponentsInChildren<HeroIconController>(true);
            foreach (HeroIconController iconController in controllers)
            {
                EnableHeroIconDetail(iconController, true);
            }
        }

        internal static void TryRefreshRemoteTalkButton(HeroDetailController controller)
        {
            if (controller == null || _remoteInteractHero == null || controller.mainShowHero != _remoteInteractHero)
            {
                return;
            }

            EnableRemoteTalkButton(controller);
        }

        internal static void ClearRemoteInteractHero(HeroDetailController controller)
        {
            if (controller != null && controller.mainShowHero == _remoteInteractHero)
            {
                _remoteInteractHero = null;
            }
        }

        internal static bool TryStartRemoteInteraction(HeroDetailController controller)
        {
            if (controller == null || _remoteInteractHero == null || controller.nowShowHero != _remoteInteractHero)
            {
                return false;
            }

            HeroData heroData = _remoteInteractHero;
            HeroSearchController.Instance?.HideHeroSearch();
            ForceHeroSettingController.Instance?.HideForceHeroSettingUI();
            ForceDetailController.Instance?.HideForceDetail();
            controller.UnshowHeroDetail();
            PlotController.Instance?.ShowHeroInteractUI(heroData);
            return true;
        }

        private static void EnableHeroIconDetail(HeroIconController controller, bool allowDetailType)
        {
            if (controller == null || controller.heroData == null)
            {
                return;
            }

            if (controller.heroIconType != HeroIconType.show && (!allowDetailType || controller.heroIconType != HeroIconType.detail))
            {
                return;
            }

            Button button = controller.GetComponent<Button>();
            if (button == null)
            {
                button = controller.gameObject.AddComponent<Button>();
            }

            HeroData heroData = controller.heroData;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() => OpenHeroDetail(heroData)));
        }

        private static void EnableRemoteTalkButton(HeroDetailController controller)
        {
            Transform talkButtonTransform = controller?.heroDetailPanel?.transform?.Find("TalkButton");
            if (talkButtonTransform == null)
            {
                return;
            }

            GameObject talkButtonObject = talkButtonTransform.gameObject;
            talkButtonObject.SetActive(true);

            Button button = talkButtonObject.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = true;
            }
        }
    }

    [HarmonyPatch(typeof(HeroSearchController), nameof(HeroSearchController.AddHeroIcon))]
    internal static class HeroSearchControllerAddHeroIconPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroSearchController __instance)
        {
            if (__instance?.temp == null)
            {
                return;
            }

            HeroIconController controller = __instance.temp.GetComponent<HeroIconController>();
            HeroDetailClickService.EnableHeroIconDetail(controller);
        }
    }

    [HarmonyPatch(typeof(ForceDetailController), nameof(ForceDetailController.ShowForceDetail))]
    internal static class ForceDetailControllerShowForceDetailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ForceDetailController __instance)
        {
            HeroDetailClickService.EnableForceDetailHeroIcons(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroAISettingTabController), nameof(HeroAISettingTabController.Generate))]
    internal static class HeroAISettingTabControllerGeneratePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroAISettingTabController __instance)
        {
            HeroDetailClickService.AddNameClick(__instance?.transform, __instance?.targetHero);
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.FreshNowHeroDetail))]
    internal static class HeroDetailControllerFreshNowHeroDetailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroDetailController __instance)
        {
            HeroDetailClickService.TryRefreshRemoteTalkButton(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.UnshowHeroDetail))]
    internal static class HeroDetailControllerUnshowHeroDetailPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroDetailController __instance)
        {
            HeroDetailClickService.ClearRemoteInteractHero(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.TalkButtonClicked))]
    internal static class HeroDetailControllerTalkButtonClickedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HeroDetailController __instance)
        {
            return !HeroDetailClickService.TryStartRemoteInteraction(__instance);
        }
    }
}
