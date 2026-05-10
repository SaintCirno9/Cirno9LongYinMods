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
        private const string PlayerStaticPortraitObjectName = "PlayerSpeStaticPortrait";
        private const float BaseReferenceRectWidth = 507.03943f;
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
                RestoreOriginalDisplay(targetSkeletonParent);
                return false;
            }

            Image portraitRootImage = GetOrCreatePortraitImage(targetSkeletonParent);
            if (portraitRootImage == null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                Main.LOG.Warning($"自定义立绘模式创建根节点失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            RectTransform portraitRect = portraitRootImage.transform.TryCast<RectTransform>();
            CopyLayoutFromReference(targetSkeletonParent, portraitRect);

            Image contentImage = GetOrCreateContentImage(portraitRootImage.transform);
            if (contentImage == null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                Main.LOG.Warning($"自定义立绘模式创建内容节点失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            portraitRootImage.sprite = null;
            portraitRootImage.color = new Color(1f, 1f, 1f, 0f);
            portraitRootImage.raycastTarget = false;
            portraitRootImage.enabled = false;
            portraitRootImage.gameObject.SetActive(true);

            ConfigureContentImage(heroData, targetSkeletonParent, portraitRect, contentImage, sprite);
            SetOnlyStaticPortraitVisible(targetSkeletonParent, portraitRootImage.transform);
            Main.LogPortraitAppliedOnce(heroData, targetSkeletonParent);
            return true;
        }

        internal static void RestoreOriginalDisplay(Transform targetSkeletonParent)
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

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == portraitTransform)
                {
                    continue;
                }

                if (string.Equals(child.gameObject.name, PlayerStaticPortraitObjectName, StringComparison.Ordinal))
                {
                    if (child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                    }

                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic != null && !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }
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

        private static void SetOnlyStaticPortraitVisible(Transform targetSkeletonParent, Transform portraitTransform)
        {
            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child == portraitTransform)
                {
                    if (!child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(true);
                    }

                    continue;
                }

                if (string.Equals(child.gameObject.name, PlayerStaticPortraitObjectName, StringComparison.Ordinal))
                {
                    if (child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                    }

                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void CopyLayoutFromReference(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            if (portraitRect == null)
            {
                return;
            }

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == portraitRect.transform)
                {
                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic == null)
                {
                    continue;
                }

                RectTransform sourceRect = child.TryCast<RectTransform>();
                if (sourceRect == null)
                {
                    continue;
                }

                portraitRect.anchorMin = sourceRect.anchorMin;
                portraitRect.anchorMax = sourceRect.anchorMax;
                portraitRect.pivot = sourceRect.pivot;
                portraitRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                portraitRect.sizeDelta = sourceRect.sizeDelta;
                portraitRect.offsetMin = sourceRect.offsetMin;
                portraitRect.offsetMax = sourceRect.offsetMax;
                portraitRect.localScale = sourceRect.localScale;
                portraitRect.localRotation = sourceRect.localRotation;
                portraitRect.localPosition = sourceRect.localPosition;
                portraitRect.SetSiblingIndex(sourceRect.GetSiblingIndex());
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
            PortraitSceneLayoutOverride sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.Fallback);
            if (IsBattleNowActiveHeroContext(targetSkeletonParent))
            {
                sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.BattlePortrait);
            }
            else if (IsBattleTeamHeroIconContext(targetSkeletonParent))
            {
                sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.BattleCard);
            }
            else if (IsHeroDetailContext(targetSkeletonParent))
            {
                sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.HeroDetail);
            }
            else if (IsCardPortraitContext(targetSkeletonParent))
            {
                sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.Card);
            }

            Vector2 frameSize = GetContentFrameSize(targetSkeletonParent, portraitRect);
            float frameWidth = frameSize.x;
            float frameHeight = frameSize.y;
            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float coverScale = Mathf.Max(frameWidth / spriteWidth, frameHeight / spriteHeight);
            float scaleMultiplier = sceneLayout.ScaleMultiplier ?? 1f;
            Vector2 coveredSize = new Vector2(spriteWidth * coverScale, spriteHeight * coverScale) * scaleMultiplier;

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = coveredSize;
            contentRect.anchoredPosition3D = GetAutoAdjustedOffset(targetSkeletonParent, portraitRect, sceneLayout);
            contentRect.localScale = ShouldMirrorToLeftFacing(targetSkeletonParent) ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            contentRect.localRotation = Quaternion.identity;
        }

        private static PortraitSceneLayoutOverride GetSceneLayoutOverride(HeroData heroData, SceneKind sceneKind)
        {
            if (!Main.TryGetPortraitLayoutConfig(heroData, out PortraitLayoutConfig config) || config == null)
            {
                return new PortraitSceneLayoutOverride(null, null, null);
            }

            return sceneKind switch
            {
                SceneKind.HeroDetail => config.Fallback.ApplyAdjustment(config.HeroDetail),
                SceneKind.Card => config.Fallback.ApplyAdjustment(config.Card),
                SceneKind.BattleCard => config.Fallback.ApplyAdjustment(config.BattleCard),
                SceneKind.BattlePortrait => config.Fallback.ApplyAdjustment(config.BattlePortrait),
                _ => config.Fallback
            };
        }

        private static Vector3 GetAutoAdjustedOffset(Transform targetSkeletonParent, RectTransform portraitRect, PortraitSceneLayoutOverride sceneLayout)
        {
            float referenceHeight = GetOffsetReferenceHeight(targetSkeletonParent, portraitRect);
            float heightRatio = referenceHeight / BaseReferenceRectHeight;
            return new Vector3((sceneLayout.OffsetX ?? 0f) * heightRatio, (sceneLayout.OffsetY ?? 0f) * heightRatio, 0f);
        }

        private static Vector2 GetContentFrameSize(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            return GetLayoutReferenceSize(targetSkeletonParent, portraitRect);
        }

        private static float GetOffsetReferenceHeight(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            RectTransform targetRect = targetSkeletonParent != null ? targetSkeletonParent.TryCast<RectTransform>() : null;
            if (IsHeroIconContext(targetSkeletonParent) && targetRect != null)
            {
                return IsBattleTeamHeroIconContext(targetSkeletonParent)
                    ? 150f
                    : Mathf.Max(1f, Mathf.Abs(targetRect.rect.height));
            }

            return GetLayoutReferenceSize(targetSkeletonParent, portraitRect).y;
        }

        private static Vector2 GetLayoutReferenceSize(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            Vector2 referenceSize = GetReferenceRectSize(portraitRect);
            if (!IsBattleNowActiveHeroContext(targetSkeletonParent))
            {
                return referenceSize;
            }

            return new Vector2(
                Mathf.Min(referenceSize.x, BaseReferenceRectWidth),
                Mathf.Min(referenceSize.y, BaseReferenceRectHeight));
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

        private enum SceneKind
        {
            HeroDetail,
            Card,
            BattleCard,
            BattlePortrait,
            Fallback
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

        private static bool IsHeroDetailContext(Transform targetSkeletonParent)
        {
            return HasName(targetSkeletonParent, "Face") &&
                   HasAncestorNamed(targetSkeletonParent, "HeroDetailPanel", 4);
        }

        private static bool IsCardPortraitContext(Transform targetSkeletonParent)
        {
            return IsHeroIconContext(targetSkeletonParent);
        }

        private static bool IsHeroIconContext(Transform targetSkeletonParent)
        {
            if (!HasName(targetSkeletonParent, "Back"))
            {
                return false;
            }

            if (HasAncestorNamed(targetSkeletonParent, "HeroIcon(Clone)", 2))
            {
                return true;
            }

            if (!IsSmallBackPortraitContext(targetSkeletonParent))
            {
                return false;
            }

            Transform parent = targetSkeletonParent.parent;
            Transform grandParent = parent != null ? parent.parent : null;
            Transform greatGrandParent = grandParent != null ? grandParent.parent : null;
            return HasName(grandParent, "Content") && HasName(greatGrandParent, "Viewport");
        }

        private static bool IsBattleTeamHeroIconContext(Transform targetSkeletonParent)
        {
            return IsHeroIconContext(targetSkeletonParent) &&
                   HasAncestorNamed(targetSkeletonParent, "BattleTeamHeroIcon(Clone)", 8);
        }

        private static bool IsSmallBackPortraitContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null || !string.Equals(targetSkeletonParent.gameObject.name, "Back", StringComparison.Ordinal))
            {
                return false;
            }

            RectTransform rectTransform = targetSkeletonParent.TryCast<RectTransform>();
            if (rectTransform == null)
            {
                return false;
            }

            float width = Mathf.Abs(rectTransform.rect.width);
            float height = Mathf.Abs(rectTransform.rect.height);
            return width > 0f && width <= 180f && height > 0f && height <= 220f;
        }

        private static bool IsBattleNowActiveHeroContext(Transform targetSkeletonParent)
        {
            return HasName(targetSkeletonParent, "HeroSkeleton") &&
                   HasAncestorNamed(targetSkeletonParent, "NowActiveHero", 4) &&
                   HasAncestorNamed(targetSkeletonParent, "BattleUIPanel", 8);
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
