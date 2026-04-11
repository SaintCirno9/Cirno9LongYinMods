using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(InfoTabRightSide.Main), "InfoTabRightSide", "1.0.0", "SaintCirno9", null)]
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

        private static readonly List<PopupAnimationState> ActiveAnimations = new();

        private static InfoTabRightSideConfig _config = InfoTabRightSideConfig.CreateDefault();
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
                float autoOffsetX = Screen.width * 0.5f;
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
}
