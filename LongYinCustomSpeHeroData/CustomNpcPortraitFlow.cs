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

            if (IsHeroDetailContext(targetSkeletonParent))
            {
                EnsureRootMask(portraitRect, enabled: true);
                contentImage.preserveAspect = false;
                ApplyHeroDetailContentSize(heroData, portraitRect, contentRect, sprite);
                return;
            }

            if (IsCardPortraitContext(targetSkeletonParent))
            {
                EnsureRootMask(portraitRect, enabled: true);
                contentImage.preserveAspect = false;
                ApplyCardContentSize(heroData, portraitRect, contentRect, sprite);
                return;
            }

            EnsureRootMask(portraitRect, enabled: false);
            contentImage.preserveAspect = true;
            ApplyFallbackContentSize(heroData, portraitRect, contentRect, sprite);
        }

        private static void ApplyHeroDetailContentSize(HeroData heroData, RectTransform portraitRect, RectTransform contentRect, Sprite sprite)
        {
            float frameWidth = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.width));
            float frameHeight = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.height));
            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float coverScale = Mathf.Max(frameWidth / spriteWidth, frameHeight / spriteHeight);
            PortraitSceneLayoutOverride sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.HeroDetail);
            float scaleMultiplier = sceneLayout.ScaleMultiplier ?? 1f;
            Vector2 coveredSize = new Vector2(spriteWidth * coverScale, spriteHeight * coverScale) * scaleMultiplier;
            float offsetX = sceneLayout.OffsetX ?? 0f;
            float offsetY = sceneLayout.OffsetY ?? -90f;

            contentRect.anchorMin = new Vector2(0.5f, 1f);
            contentRect.anchorMax = new Vector2(0.5f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = coveredSize;
            contentRect.anchoredPosition3D = new Vector3(offsetX, offsetY, 0f);
            contentRect.localScale = Vector3.one;
            contentRect.localRotation = Quaternion.identity;
        }

        private static void ApplyCardContentSize(HeroData heroData, RectTransform portraitRect, RectTransform contentRect, Sprite sprite)
        {
            float frameWidth = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.width));
            float frameHeight = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.height));
            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float coverScale = Mathf.Max(frameWidth / spriteWidth, frameHeight / spriteHeight);
            PortraitSceneLayoutOverride sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.Card);
            float insetRatio = sceneLayout.ScaleMultiplier ?? 0.9f;
            float offsetX = sceneLayout.OffsetX ?? -10f;
            float offsetY = sceneLayout.OffsetY ?? -20f;
            Vector2 coveredSize = new Vector2(spriteWidth * coverScale, spriteHeight * coverScale) * insetRatio;

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = coveredSize;
            contentRect.anchoredPosition3D = new Vector3(offsetX, offsetY, 0f);
            contentRect.localScale = Vector3.one;
            contentRect.localRotation = Quaternion.identity;
        }

        private static void ApplyFallbackContentSize(HeroData heroData, RectTransform portraitRect, RectTransform contentRect, Sprite sprite)
        {
            PortraitSceneLayoutOverride sceneLayout = GetSceneLayoutOverride(heroData, SceneKind.Fallback);
            if (sceneLayout.ScaleMultiplier.HasValue || sceneLayout.OffsetX.HasValue || sceneLayout.OffsetY.HasValue)
            {
                float frameWidth = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.width));
                float frameHeight = Mathf.Max(1f, Mathf.Abs(portraitRect.rect.height));
                float spriteWidth = Mathf.Max(1f, sprite.rect.width);
                float spriteHeight = Mathf.Max(1f, sprite.rect.height);
                float fitScale = Mathf.Min(frameWidth / spriteWidth, frameHeight / spriteHeight);
                float scaleMultiplier = sceneLayout.ScaleMultiplier ?? 1f;
                Vector2 fittedSize = new Vector2(spriteWidth * fitScale, spriteHeight * fitScale) * scaleMultiplier;

                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.sizeDelta = fittedSize;
                contentRect.anchoredPosition3D = new Vector3(sceneLayout.OffsetX ?? 0f, sceneLayout.OffsetY ?? 0f, 0f);
                contentRect.localScale = Vector3.one;
                contentRect.localRotation = Quaternion.identity;
                return;
            }

            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.anchoredPosition3D = Vector3.zero;
            contentRect.localScale = Vector3.one;
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
                SceneKind.HeroDetail => MergeLayout(config.Fallback, config.HeroDetail),
                SceneKind.Card => MergeLayout(config.Fallback, config.Card),
                _ => config.Fallback
            };
        }

        private static PortraitSceneLayoutOverride MergeLayout(PortraitSceneLayoutOverride fallback, PortraitSceneLayoutOverride sceneSpecific)
        {
            return new PortraitSceneLayoutOverride(
                sceneSpecific?.ScaleMultiplier ?? fallback?.ScaleMultiplier,
                sceneSpecific?.OffsetX ?? fallback?.OffsetX,
                sceneSpecific?.OffsetY ?? fallback?.OffsetY);
        }

        private enum SceneKind
        {
            HeroDetail,
            Card,
            Fallback
        }

        private static void EnsureRootMask(RectTransform portraitRect, bool enabled)
        {
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

        private static bool IsHeroDetailContext(Transform targetSkeletonParent)
        {
            return targetSkeletonParent != null &&
                   string.Equals(targetSkeletonParent.gameObject.name, "Face", StringComparison.Ordinal) &&
                   targetSkeletonParent.parent != null &&
                   string.Equals(targetSkeletonParent.parent.gameObject.name, "HeroDetailPanel", StringComparison.Ordinal);
        }

        private static bool IsCardPortraitContext(Transform targetSkeletonParent)
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
            if (!(width > 0f && width <= 180f && height > 0f && height <= 220f))
            {
                return false;
            }

            Transform cardRoot = targetSkeletonParent.parent;
            Transform content = cardRoot != null ? cardRoot.parent : null;
            Transform viewport = content != null ? content.parent : null;

            return cardRoot != null &&
                   content != null &&
                   viewport != null &&
                   string.Equals(content.gameObject.name, "Content", StringComparison.Ordinal) &&
                   string.Equals(viewport.gameObject.name, "Viewport", StringComparison.Ordinal);
        }

    }
}
