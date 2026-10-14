using System;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LongYinCustomSpeHeroData
{
    internal static class CustomNpcPortraitFlow
    {
        private const string StaticPortraitObjectName = "LongYinCustomNpcPortrait";
        private const string StaticPortraitContentObjectName = "Content";
        private const float BaseReferenceRectHeight = 784.4206f;

        internal static bool TryApplyCustomPortrait(HeroData heroData, Transform targetSkeletonParent)
        {
            if (heroData == null || targetSkeletonParent == null)
            {
                return false;
            }

            if (heroData.heroID == 0 || heroData.isSummon)
            {
                return false;
            }

            if (!Main.TryLoadCustomPortraitSprite(heroData, out Sprite sprite))
            {
                return false;
            }

            Image portraitRootImage = GetOrCreatePortraitImage(targetSkeletonParent);
            if (portraitRootImage == null)
            {
                Main.LOG.Warning($"自定义立绘模式创建根节点失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            RectTransform portraitRect = portraitRootImage.transform.TryCast<RectTransform>();
            RectTransform referenceSkeletonRect = FindReferenceSkeletonRect(targetSkeletonParent, portraitRect != null ? portraitRect.transform : null);
            if (referenceSkeletonRect == null)
            {
                HideNpcStaticPortraitOnly(targetSkeletonParent);
                return false;
            }

            CopyLayoutFromReference(referenceSkeletonRect, portraitRect);

            Image contentImage = GetOrCreateContentImage(portraitRootImage.transform);
            if (contentImage == null)
            {
                Main.LOG.Warning($"自定义立绘模式创建内容节点失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            portraitRootImage.sprite = null;
            portraitRootImage.color = new Color(1f, 1f, 1f, 0f);
            portraitRootImage.raycastTarget = false;
            portraitRootImage.enabled = false;
            portraitRootImage.gameObject.SetActive(true);

            ConfigureContentImage(heroData, targetSkeletonParent, portraitRect, contentImage, sprite);
            SetStaticPortraitVisible(targetSkeletonParent, referenceSkeletonRect, portraitRootImage.transform);
            Main.LogPortraitAppliedOnce(heroData, targetSkeletonParent);
            return true;
        }

        internal static void HideNpcStaticPortraitOnly(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return;
            }

            Transform portraitTransform = targetSkeletonParent.Find(StaticPortraitObjectName);
            if (portraitTransform != null && portraitTransform.gameObject.activeSelf)
            {
                portraitTransform.gameObject.SetActive(false);
            }
        }

        private static Image GetOrCreatePortraitImage(Transform targetSkeletonParent)
        {
            Transform existingTransform = targetSkeletonParent.Find(StaticPortraitObjectName);
            Image portraitImage = existingTransform != null ? existingTransform.GetComponent<Image>() : null;
            if (portraitImage != null)
            {
                return portraitImage;
            }

            var portraitObject = new GameObject(StaticPortraitObjectName);
            RectTransform rectTransform = portraitObject.AddComponent<RectTransform>();
            rectTransform.SetParent(targetSkeletonParent, false);
            return portraitObject.AddComponent<Image>();
        }

        private static Image GetOrCreateContentImage(Transform portraitRootTransform)
        {
            if (portraitRootTransform == null)
            {
                return null;
            }

            Transform existingTransform = portraitRootTransform.Find(StaticPortraitContentObjectName);
            Image contentImage = existingTransform != null ? existingTransform.GetComponent<Image>() : null;
            if (contentImage != null)
            {
                return contentImage;
            }

            var contentObject = new GameObject(StaticPortraitContentObjectName);
            RectTransform contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.SetParent(portraitRootTransform, false);
            return contentObject.AddComponent<Image>();
        }

        private static void SetStaticPortraitVisible(Transform targetSkeletonParent, RectTransform referenceSkeletonRect, Transform portraitTransform)
        {
            if (portraitTransform != null)
            {
                int siblingIndex = referenceSkeletonRect != null
                    ? Math.Min(referenceSkeletonRect.GetSiblingIndex() + 1, targetSkeletonParent.childCount - 1)
                    : targetSkeletonParent.childCount - 1;
                portraitTransform.SetSiblingIndex(siblingIndex);

                if (!portraitTransform.gameObject.activeSelf)
                {
                    portraitTransform.gameObject.SetActive(true);
                }
            }

            HideOriginalSkeletonGraphics(targetSkeletonParent, portraitTransform);
        }

        private static void HideOriginalSkeletonGraphics(Transform targetSkeletonParent, Transform portraitTransform)
        {
            if (targetSkeletonParent == null)
            {
                return;
            }

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == portraitTransform)
                {
                    continue;
                }

                HideOriginalSkeletonGraphics(child, portraitTransform);

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void CopyLayoutFromReference(RectTransform referenceSkeletonRect, RectTransform portraitRect)
        {
            if (portraitRect == null)
            {
                return;
            }

            if (referenceSkeletonRect != null)
            {
                portraitRect.anchorMin = referenceSkeletonRect.anchorMin;
                portraitRect.anchorMax = referenceSkeletonRect.anchorMax;
                portraitRect.pivot = referenceSkeletonRect.pivot;
                portraitRect.anchoredPosition3D = referenceSkeletonRect.anchoredPosition3D;
                portraitRect.sizeDelta = referenceSkeletonRect.sizeDelta;
                portraitRect.offsetMin = referenceSkeletonRect.offsetMin;
                portraitRect.offsetMax = referenceSkeletonRect.offsetMax;
                portraitRect.localScale = referenceSkeletonRect.localScale;
                portraitRect.localRotation = referenceSkeletonRect.localRotation;
                portraitRect.localPosition = referenceSkeletonRect.localPosition;
                portraitRect.SetSiblingIndex(referenceSkeletonRect.GetSiblingIndex());
                return;
            }

            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            portraitRect.anchoredPosition3D = Vector3.zero;
            portraitRect.localScale = Vector3.one;
            portraitRect.localRotation = Quaternion.identity;
        }

        private static RectTransform FindReferenceSkeletonRect(Transform targetSkeletonParent, Transform portraitTransform)
        {
            if (targetSkeletonParent == null)
            {
                return null;
            }

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == portraitTransform)
                {
                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic == null)
                {
                    continue;
                }

                RectTransform childRect = child.TryCast<RectTransform>();
                return childRect;
            }

            return null;
        }

        private static void ConfigureContentImage(HeroData heroData, Transform targetSkeletonParent, RectTransform portraitRect, Image contentImage, Sprite sprite)
        {
            if (portraitRect == null || contentImage == null || sprite == null)
            {
                return;
            }

            RectTransform contentRect = contentImage.transform.TryCast<RectTransform>();
            if (contentRect == null)
            {
                return;
            }

            contentImage.sprite = sprite;
            contentImage.raycastTarget = false;
            contentImage.color = Color.white;
            contentImage.enabled = true;

            EnsureRootMask(portraitRect, enabled: CanUseRootMask(portraitRect) && !IsDialoguePortraitContext(targetSkeletonParent));
            contentImage.preserveAspect = false;
            ApplyPortraitContentSize(heroData, targetSkeletonParent, portraitRect, contentRect, sprite);
        }

        private static void ApplyPortraitContentSize(HeroData heroData, Transform targetSkeletonParent, RectTransform portraitRect, RectTransform contentRect, Sprite sprite)
        {
            PortraitSceneLayoutOverride sceneLayout = GetFallbackLayoutOverride(heroData);

            Vector2 frameSize = GetReferenceRectSize(portraitRect);
            float fitScale = GetFitScale(frameSize, sprite);
            float scaleMultiplier = sceneLayout.ScaleMultiplier ?? 1f;
            Vector2 fittedSize = new Vector2(sprite.rect.width * fitScale, sprite.rect.height * fitScale) * scaleMultiplier;

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = fittedSize;
            contentRect.anchoredPosition3D = GetAutoAdjustedOffset(frameSize.y, sceneLayout);
            contentRect.localScale = ShouldMirrorToLeftFacing(targetSkeletonParent) ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            contentRect.localRotation = Quaternion.identity;
        }

        private static float GetFitScale(Vector2 frameSize, Sprite sprite)
        {
            if (sprite == null)
            {
                return 1f;
            }

            float frameHeight = Mathf.Max(1f, frameSize.y);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            return frameHeight / spriteHeight;
        }

        private static PortraitSceneLayoutOverride GetFallbackLayoutOverride(HeroData heroData)
        {
            if (!Main.TryGetPortraitLayoutConfig(heroData, out PortraitLayoutConfig config) || config == null)
            {
                return new PortraitSceneLayoutOverride(null, null, null);
            }

            return config.Fallback;
        }

        private static Vector3 GetAutoAdjustedOffset(float referenceHeight, PortraitSceneLayoutOverride sceneLayout)
        {
            float heightRatio = referenceHeight / BaseReferenceRectHeight;
            return new Vector3((sceneLayout.OffsetX ?? 0f) * heightRatio, (sceneLayout.OffsetY ?? 0f) * heightRatio, 0f);
        }

        private static bool ShouldMirrorToLeftFacing(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return false;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            return path.Contains("/HudPanel/FaceMask/Face", StringComparison.Ordinal) ||
                   path.Contains("/HeroDetailPanel/Face", StringComparison.Ordinal) ||
                   path.EndsWith("/BattleUIPanel/NowActiveHero/HeroSkeleton", StringComparison.Ordinal);
        }

        private static bool IsDialoguePortraitContext(Transform targetSkeletonParent)
        {
            return HasAncestorNamed(targetSkeletonParent, "PlotPanel", 8) ||
                   HasAncestorNamed(targetSkeletonParent, "TalkPanel", 8) ||
                   HasAncestorNamed(targetSkeletonParent, "HeroTalkUIPanel", 8);
        }

        private static Vector2 GetReferenceRectSize(RectTransform portraitRect)
        {
            if (portraitRect == null)
            {
                return Vector2.one;
            }

            return new Vector2(
                Mathf.Max(1f, Mathf.Abs(portraitRect.rect.width)),
                Mathf.Max(1f, Mathf.Abs(portraitRect.rect.height)));
        }

        private static void EnsureRootMask(RectTransform portraitRect, bool enabled)
        {
            if (portraitRect == null)
            {
                return;
            }

            RectMask2D mask = portraitRect.GetComponent<RectMask2D>();
            if (enabled)
            {
                if (mask == null)
                {
                    mask = portraitRect.gameObject.AddComponent<RectMask2D>();
                }

                mask.enabled = true;
                return;
            }

            if (mask != null)
            {
                mask.enabled = false;
            }
        }

        private static bool CanUseRootMask(RectTransform portraitRect)
        {
            if (portraitRect == null)
            {
                return false;
            }

            Vector3 scale = portraitRect.localScale;
            return scale.x > 0f && scale.y > 0f;
        }

        private static bool HasAncestorNamed(Transform transform, string name, int maxDepth)
        {
            Transform current = transform;
            for (int depth = 0; depth <= maxDepth && current != null; depth++)
            {
                if (HasName(current, name))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool HasName(Transform transform, string name)
        {
            return transform != null &&
                   transform.gameObject != null &&
                   string.Equals(transform.gameObject.name, name, StringComparison.Ordinal);
        }

    }
}
