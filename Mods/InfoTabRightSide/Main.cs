using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(InfoTabRightSide.Main), "InfoTabRightSide", "1.2.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace InfoTabRightSide
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            InfoTabRightSideRuntime.Initialize(InfoTabRightSideConfigStore.Load(Log));

            Log.Msg(
                $"InfoTabRightSide 已加载。配置文件：{InfoTabRightSideConfigStore.ConfigPathDisplay}。启用={FormatEnabled(InfoTabRightSideRuntime.Enabled)}，水平偏移={InfoTabRightSideRuntime.OffsetX:0.##}，垂直偏移={InfoTabRightSideRuntime.OffsetY:0.##}。");
        }

        public override void OnLateUpdate()
        {
            InfoTabRightSideRuntime.OnLateUpdate();
        }

        private static string FormatEnabled(bool value)
        {
            return value ? "开" : "关";
        }
    }

    internal static class InfoTabRightSideRuntime
    {
        private const float RightMargin = 24f;
        private const float SlideDistance = 500f;
        private const float SlideDuration = 0.25f;
        private const float ReferenceUiHeight = 1080f;

        private static readonly List<PopupAnimationState> ActiveAnimations = new();

        private static InfoTabRightSideConfig _config = InfoTabRightSideConfig.CreateDefault();
        private static InfoController _trackedController;
        private static Transform _trackedListTransform;
        private static RectTransform _trackedListRect;
        private static Vector2 _originalAnchorMin;
        private static Vector2 _originalAnchorMax;
        private static Vector2 _originalPivot;
        private static Vector2 _originalAnchoredPosition;
        private static Vector2 _originalSizeDelta;
        private static Vector3 _originalLocalPosition;
        private static int _lastPopupCount;

        internal static bool Enabled => _config.Enabled;

        internal static float OffsetX => _config.OffsetX;

        internal static float OffsetY => _config.OffsetY;

        internal static void Initialize(InfoTabRightSideConfig config)
        {
            _config = config ?? InfoTabRightSideConfig.CreateDefault();
            ResetRuntimeState();
        }

        internal static void ResetRuntimeState()
        {
            _trackedController = null;
            _trackedListTransform = null;
            _trackedListRect = null;
            _lastPopupCount = 0;
            ActiveAnimations.Clear();
        }

        internal static void HandleInfoControllerAwake()
        {
            ResetRuntimeState();
        }

        internal static void HandleInfoControllerUpdate(InfoController controller)
        {
            if (!Enabled || controller?.popInfoList == null)
            {
                return;
            }

            _trackedController = controller;

            if (!TrackListIfNeeded(controller.popInfoList))
            {
                return;
            }

            ApplyContainerPosition();
            RegisterNewPopups();
        }

        internal static void OnLateUpdate()
        {
            if (!Enabled)
            {
                ActiveAnimations.Clear();
                return;
            }

            if (_trackedListTransform != null)
            {
                ApplyContainerPosition();
            }

            if (ActiveAnimations.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            for (int i = ActiveAnimations.Count - 1; i >= 0; i--)
            {
                PopupAnimationState state = ActiveAnimations[i];
                if (state.ContentTransform == null)
                {
                    ActiveAnimations.RemoveAt(i);
                    continue;
                }

                float progress = Mathf.Clamp01((now - state.StartTime) / SlideDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 5f);
                Vector3 position = state.ContentTransform.localPosition;
                position.x = Mathf.Lerp(SlideDistance, 0f, eased);
                position.y = state.TargetY;
                position.z = state.TargetZ;
                state.ContentTransform.localPosition = position;

                if (progress >= 1f)
                {
                    ActiveAnimations.RemoveAt(i);
                }
            }
        }

        private static bool TrackListIfNeeded(GameObject popInfoList)
        {
            Transform listTransform = popInfoList.transform;
            if (listTransform == null)
            {
                return false;
            }

            if (listTransform == _trackedListTransform)
            {
                return true;
            }

            _trackedListTransform = listTransform;
            _trackedListRect = popInfoList.GetComponent<RectTransform>();
            _lastPopupCount = 0;
            ActiveAnimations.Clear();

            if (_trackedListRect != null)
            {
                _originalAnchorMin = _trackedListRect.anchorMin;
                _originalAnchorMax = _trackedListRect.anchorMax;
                _originalPivot = _trackedListRect.pivot;
                _originalAnchoredPosition = _trackedListRect.anchoredPosition;
                _originalSizeDelta = _trackedListRect.sizeDelta;
            }
            else
            {
                _originalLocalPosition = _trackedListTransform.localPosition;
            }

            return true;
        }

        private static void ApplyContainerPosition()
        {
            if (_trackedListRect != null)
            {
                float autoOffsetX = ResolveAspectScaledHalfWidth();
                _trackedListRect.anchorMin = new Vector2(1f, _originalAnchorMin.y);
                _trackedListRect.anchorMax = new Vector2(1f, _originalAnchorMax.y);
                _trackedListRect.pivot = new Vector2(1f, _originalPivot.y);
                _trackedListRect.anchoredPosition = new Vector2(
                    autoOffsetX - RightMargin + OffsetX,
                    _originalAnchoredPosition.y + OffsetY);
                _trackedListRect.sizeDelta = _originalSizeDelta;
                return;
            }

            if (_trackedListTransform == null)
            {
                return;
            }

            Vector3 position = _originalLocalPosition;
            position.x = Mathf.Abs(_originalLocalPosition.x) + OffsetX;
            position.y = _originalLocalPosition.y + OffsetY;
            _trackedListTransform.localPosition = position;
        }

        private static float ResolveAspectScaledHalfWidth()
        {
            if (Screen.height <= 0)
            {
                return ReferenceUiHeight * 0.5f;
            }

            return ReferenceUiHeight * Screen.width / (float)Screen.height * 0.5f;
        }

        private static void RegisterNewPopups()
        {
            if (_trackedListTransform == null)
            {
                return;
            }

            int currentCount = _trackedListTransform.childCount;
            if (currentCount < _lastPopupCount)
            {
                _lastPopupCount = currentCount;
                return;
            }

            int addedCount = currentCount - _lastPopupCount;
            for (int i = 0; i < addedCount && i < currentCount; i++)
            {
                Transform popupRoot = _trackedListTransform.GetChild(i);
                PreparePopupAnimation(popupRoot);
            }

            _lastPopupCount = currentCount;
        }

        internal static bool TryHandlePopupClick(GameObject clickedObject)
        {
            if (!Enabled || clickedObject == null || _trackedListTransform == null)
            {
                return false;
            }

            Transform clickedTransform = clickedObject.transform;
            if (clickedTransform == null || !clickedTransform.IsChildOf(_trackedListTransform))
            {
                return false;
            }

            ConsumeCurrentUiClick();
            ClearAllPopups();
            return true;
        }

        private static void ConsumeCurrentUiClick()
        {
            // 这里故意不做点击穿透：右侧提示区需要自己吃掉这次点击，否则清空时对象被销毁后，
            // 同一帧点击还可能继续落到底层地图或其他 UI。后续如果出现“吞点”或“点了没反应”，
            // 优先检查这个回调里的 touch 清理逻辑，以及弹窗区域是否仍在 popInfoList 子节点下。
            var currentTouch = UICamera.currentTouch;
            if (currentTouch != null)
            {
                currentTouch.clickNotification = UICamera.ClickNotification.None;
                currentTouch.pressed = null;
                currentTouch.dragged = null;
            }

            UICamera.hoveredObject = null;
        }

        private static void ClearAllPopups()
        {
            if (_trackedController?.newInfoTabDatas != null)
            {
                _trackedController.newInfoTabDatas.Clear();
            }

            if (_trackedListTransform != null)
            {
                for (int i = _trackedListTransform.childCount - 1; i >= 0; i--)
                {
                    Transform popup = _trackedListTransform.GetChild(i);
                    if (popup != null)
                    {
                        UnityEngine.Object.Destroy(popup.gameObject);
                    }
                }
            }

            ActiveAnimations.Clear();
            _lastPopupCount = 0;
        }

        private static void PreparePopupAnimation(Transform popupRoot)
        {
            Transform contentTransform = ResolvePopupContentTransform(popupRoot);
            if (contentTransform == null)
            {
                return;
            }

            RectTransform popupRootRect = popupRoot.GetComponent<RectTransform>();
            if (popupRootRect != null)
            {
                popupRootRect.anchorMin = new Vector2(1f, popupRootRect.anchorMin.y);
                popupRootRect.anchorMax = new Vector2(1f, popupRootRect.anchorMax.y);
                popupRootRect.pivot = new Vector2(1f, popupRootRect.pivot.y);
            }

            RectTransform contentRect = contentTransform.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                Vector2 anchoredPosition = contentRect.anchoredPosition;
                contentRect.anchorMin = new Vector2(1f, contentRect.anchorMin.y);
                contentRect.anchorMax = new Vector2(1f, contentRect.anchorMax.y);
                contentRect.pivot = new Vector2(1f, contentRect.pivot.y);
                contentRect.anchoredPosition = new Vector2(0f, anchoredPosition.y);
            }

            Vector3 position = contentTransform.localPosition;
            position.x = SlideDistance;
            contentTransform.localPosition = position;

            ActiveAnimations.Add(
                new PopupAnimationState(
                    contentTransform,
                    Time.unscaledTime,
                    position.y,
                    position.z));
        }

        private static Transform ResolvePopupContentTransform(Transform popupRoot)
        {
            if (popupRoot == null)
            {
                return null;
            }

            if (popupRoot.childCount <= 0)
            {
                return popupRoot;
            }

            return popupRoot.GetChild(0);
        }

        private sealed class PopupAnimationState
        {
            internal PopupAnimationState(Transform contentTransform, float startTime, float targetY, float targetZ)
            {
                ContentTransform = contentTransform;
                StartTime = startTime;
                TargetY = targetY;
                TargetZ = targetZ;
            }

            internal Transform ContentTransform { get; }

            internal float StartTime { get; }

            internal float TargetY { get; }

            internal float TargetZ { get; }
        }
    }

    [HarmonyPatch(typeof(InfoController), "Awake")]
    internal static class InfoController_Awake_Patch
    {
        private static void Postfix()
        {
            InfoTabRightSideRuntime.HandleInfoControllerAwake();
        }
    }

    [HarmonyPatch(typeof(InfoController), "Update")]
    internal static class InfoController_Update_Patch
    {
        private static void Postfix(InfoController __instance)
        {
            InfoTabRightSideRuntime.HandleInfoControllerUpdate(__instance);
        }
    }

    [HarmonyPatch(typeof(PopInfoTabController), "OnClick")]
    internal static class PopInfoTabController_OnClick_Patch
    {
        private static bool Prefix(PopInfoTabController __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            return !InfoTabRightSideRuntime.TryHandlePopupClick(__instance.gameObject);
        }
    }
}
