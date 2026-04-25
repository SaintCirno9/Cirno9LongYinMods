using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LongYinPlayerSpeSkeleton
{
    /// <summary>
    /// 静态图片模式：
    /// 在 UI 立绘容器中手动隐藏原有 SkeletonGraphic，并改为显示一张外部图片。
    /// </summary>
    internal static class StaticImageSpeSkeletonFlow
    {
        private const string StaticPortraitObjectName = "PlayerSpeStaticPortrait";
        private const string StaticPortraitContentObjectName = "Content";

        private static readonly HashSet<string> MissingImageWarnings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> GenericPortraitContextLogs = new(StringComparer.Ordinal);
        private static Vector2 _baseReferenceRectSize = Vector2.zero;
        private static string _cachedImagePath;
        private static DateTime _cachedImageWriteTimeUtc = DateTime.MinValue;
        private static Sprite _cachedSprite;
        private static Texture2D _cachedTexture;

        internal static void NotifyConfigReloaded()
        {
            MissingImageWarnings.Clear();
            GenericPortraitContextLogs.Clear();
            _baseReferenceRectSize = Vector2.zero;
        }

        internal static bool TryApplyPlayerStaticImage(HeroData heroData, Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return false;
            }

            string skipReason = Main.GetSkipReason(heroData, targetSkeletonParent);
            if (skipReason != null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                return false;
            }

            if (!TryLoadConfiguredSprite(out Sprite sprite, out string reason))
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                WarnMissingImageOnce(reason);
                return false;
            }

            Image portraitRootImage = GetOrCreatePortraitImage(targetSkeletonParent);
            if (portraitRootImage == null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                Main.Log.Warning($"静态图片模式创建 Image 失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            RectTransform portraitRect = portraitRootImage.transform.TryCast<RectTransform>();
            string referenceLayoutInfo = CopyLayoutFromReference(targetSkeletonParent, portraitRect);
            CaptureBaseReferenceRectSize(targetSkeletonParent, portraitRect);

            Image contentImage = GetOrCreateContentImage(portraitRootImage.transform);
            if (contentImage == null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                Main.Log.Warning($"静态图片模式创建内容 Image 失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            portraitRootImage.sprite = null;
            portraitRootImage.color = new Color(1f, 1f, 1f, 0f);
            portraitRootImage.raycastTarget = false;
            portraitRootImage.enabled = false;
            portraitRootImage.gameObject.SetActive(true);

            ConfigureContentImage(targetSkeletonParent, portraitRect, contentImage, sprite, referenceLayoutInfo);

            SetOnlyStaticPortraitVisible(targetSkeletonParent, portraitRootImage.transform);

            if (Main.VerboseLog)
            {
                Main.Log.Msg(
                    $"玩家静态立绘已生效：hero={heroData?.heroName}, image={GetDisplayImagePath()}, " +
                    Main.DescribeTransformContext(targetSkeletonParent));
            }

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

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic != null && !child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        private static void WarnMissingImageOnce(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (MissingImageWarnings.Add(reason))
            {
                Main.Log.Warning(reason);
            }
        }

        private static bool TryLoadConfiguredSprite(out Sprite sprite, out string reason)
        {
            sprite = null;
            string imagePath = ResolveImagePath();
            if (imagePath == null)
            {
                reason = "静态图片模式已启用，但 StaticImagePath 为空。";
                return false;
            }

            if (!File.Exists(imagePath))
            {
                reason = $"静态图片不存在：{GetDisplayImagePath()}";
                return false;
            }

            DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
            if (_cachedSprite != null &&
                string.Equals(_cachedImagePath, imagePath, StringComparison.OrdinalIgnoreCase) &&
                lastWriteTimeUtc == _cachedImageWriteTimeUtc)
            {
                sprite = _cachedSprite;
                reason = null;
                return true;
            }

            try
            {
                byte[] pngBytes = File.ReadAllBytes(imagePath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, pngBytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    reason = $"读取静态图片失败：{GetDisplayImagePath()}";
                    return false;
                }

                texture.name = $"LongYinPlayerSpePortrait_{Path.GetFileNameWithoutExtension(imagePath)}";
                texture.wrapMode = TextureWrapMode.Clamp;
                var rect = new Rect(0f, 0f, texture.width, texture.height);
                Sprite loadedSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                loadedSprite.name = texture.name;

                ReleaseCachedSprite();
                _cachedImagePath = imagePath;
                _cachedImageWriteTimeUtc = lastWriteTimeUtc;
                _cachedTexture = texture;
                _cachedSprite = loadedSprite;
                sprite = loadedSprite;
                reason = null;
                return true;
            }
            catch (Exception ex)
            {
                reason = $"加载静态图片异常：{GetDisplayImagePath()}，{ex.GetType().Name} - {ex.Message}";
                return false;
            }
        }

        private static void ReleaseCachedSprite()
        {
            if (_cachedSprite != null)
            {
                UnityEngine.Object.Destroy(_cachedSprite);
                _cachedSprite = null;
            }

            if (_cachedTexture != null)
            {
                UnityEngine.Object.Destroy(_cachedTexture);
                _cachedTexture = null;
            }
        }

        private static string ResolveImagePath()
        {
            string configuredPath = Main.StaticImagePath;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.GetFullPath(Path.Combine(Main.ConfigDir, configuredPath));
        }

        private static string GetDisplayImagePath()
        {
            string configuredPath = Main.StaticImagePath;
            return string.IsNullOrWhiteSpace(configuredPath)
                ? "<空>"
                : configuredPath;
        }

        private static Image GetOrCreatePortraitImage(Transform targetSkeletonParent)
        {
            Transform existingTransform = targetSkeletonParent.Find(StaticPortraitObjectName);
            Image portraitImage = existingTransform != null ? existingTransform.GetComponent<Image>() : null;
            bool reused = portraitImage != null;
            if (portraitImage == null)
            {
                var portraitObject = new GameObject(StaticPortraitObjectName);
                RectTransform rectTransform = portraitObject.AddComponent<RectTransform>();
                rectTransform.SetParent(targetSkeletonParent, false);
                portraitImage = portraitObject.AddComponent<Image>();
            }

            if (Main.VerboseLog)
            {
                Main.Log.Msg(reused ? "静态图片模式：复用已有 PlayerSpeStaticPortrait 节点。" : "静态图片模式：已创建新的 PlayerSpeStaticPortrait 节点。");
            }

            return portraitImage;
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
            contentImage = contentObject.AddComponent<Image>();
            return contentImage;
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

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic != null && child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static string CopyLayoutFromReference(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            if (portraitRect == null)
            {
                return "reference=n/a";
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
                return BuildReferenceLayoutInfo(child, sourceRect);
            }

            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            portraitRect.anchoredPosition3D = Vector3.zero;
            portraitRect.localScale = Vector3.one;
            portraitRect.localRotation = Quaternion.identity;
            return "reference=<fallback>";
        }

        /// <summary>
        /// 外层容器沿用原节点布局，真正显示图片的子节点统一使用通用肖像配置。
        /// </summary>
        private static void ConfigureContentImage(Transform targetSkeletonParent, RectTransform portraitRect, Image contentImage, Sprite sprite, string referenceLayoutInfo)
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

            EnsureRootMask(portraitRect, enabled: CanUseRootMask(portraitRect));
            contentImage.preserveAspect = false;
            StaticImageSceneConfig sceneConfig = Main.GetStaticImageSceneConfig(targetSkeletonParent);
            LogGenericPortraitContext(targetSkeletonParent, portraitRect, referenceLayoutInfo, sceneConfig);
            ApplyPortraitContentSize(targetSkeletonParent, portraitRect, contentRect, sprite, sceneConfig);
        }

        private static string BuildReferenceLayoutInfo(Transform sourceTransform, RectTransform sourceRect)
        {
            if (sourceTransform == null || sourceRect == null)
            {
                return "reference=n/a";
            }

            Vector3 localPosition = sourceRect.localPosition;
            Vector3 localScale = sourceRect.localScale;
            return $"reference={sourceTransform.gameObject.name}, referenceRect=({sourceRect.rect.width}, {sourceRect.rect.height}), referenceAnchored=({sourceRect.anchoredPosition3D.x}, {sourceRect.anchoredPosition3D.y}, {sourceRect.anchoredPosition3D.z}), referenceLocal=({localPosition.x}, {localPosition.y}, {localPosition.z}), referenceScale=({localScale.x}, {localScale.y}, {localScale.z})";
        }

        private static void LogGenericPortraitContext(Transform targetSkeletonParent, RectTransform portraitRect, string referenceLayoutInfo, StaticImageSceneConfig sceneConfig)
        {
            if (!Main.VerboseLog || targetSkeletonParent == null)
            {
                return;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            if (!GenericPortraitContextLogs.Add(path))
            {
                return;
            }

            string rootRectInfo = portraitRect == null
                ? "rootRect=n/a"
                : $"rootRect=({portraitRect.rect.width}, {portraitRect.rect.height}), rootAnchored=({portraitRect.anchoredPosition3D.x}, {portraitRect.anchoredPosition3D.y}, {portraitRect.anchoredPosition3D.z})";
            string effectiveConfigInfo = BuildEffectiveConfigInfo(targetSkeletonParent, portraitRect, sceneConfig);

            Main.Log.Msg(
                $"静态图片模式走统一肖像布局：{Main.DescribeTransformContext(targetSkeletonParent)}, {rootRectInfo}, {referenceLayoutInfo}, config={sceneConfig}, {effectiveConfigInfo}");
            LogAncestorMaskContext(targetSkeletonParent);
        }

        private static void LogAncestorMaskContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return;
            }

            Transform current = targetSkeletonParent;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                RectTransform rectTransform = current.TryCast<RectTransform>();
                RectMask2D rectMask = current.GetComponent<RectMask2D>();
                Mask mask = current.GetComponent<Mask>();
                if (rectTransform != null && (rectMask != null || mask != null))
                {
                    Main.Log.Msg(
                        $"  ancestorMask[{depth}] name={current.gameObject.name}, rect=({rectTransform.rect.width}, {rectTransform.rect.height}), anchored=({rectTransform.anchoredPosition3D.x}, {rectTransform.anchoredPosition3D.y}, {rectTransform.anchoredPosition3D.z}), rectMask={(rectMask != null && rectMask.enabled)}, mask={(mask != null && mask.enabled)}");
                }

                current = current.parent;
            }
        }

        private static string BuildEffectiveConfigInfo(Transform targetSkeletonParent, RectTransform portraitRect, StaticImageSceneConfig sceneConfig)
        {
            if (portraitRect == null)
            {
                return "effective=n/a";
            }

            Vector3 rootScale = portraitRect.localScale;
            Vector3 autoOffset = GetAutoAdjustedOffset(targetSkeletonParent, portraitRect, sceneConfig);
            float effectiveOffsetX = autoOffset.x * rootScale.x;
            float effectiveOffsetY = autoOffset.y * rootScale.y;
            Vector2 baseSize = _baseReferenceRectSize == Vector2.zero
                ? GetReferenceRectSize(portraitRect)
                : _baseReferenceRectSize;
            return $"baseReferenceRect=({baseSize.x}, {baseSize.y}), effectiveOffset=({effectiveOffsetX}, {effectiveOffsetY}), effectiveScale=({sceneConfig.InsetRatio * Mathf.Abs(rootScale.x)}, {sceneConfig.InsetRatio * Mathf.Abs(rootScale.y)})";
        }

        private static void ApplyPortraitContentSize(Transform targetSkeletonParent, RectTransform portraitRect, RectTransform contentRect, Sprite sprite, StaticImageSceneConfig sceneConfig)
        {
            if (portraitRect == null || contentRect == null || sprite == null)
            {
                return;
            }

            Vector2 frameSize = GetContentFrameSize(targetSkeletonParent, portraitRect);
            float frameWidth = frameSize.x;
            float frameHeight = frameSize.y;
            float spriteWidth = Mathf.Max(1f, sprite.rect.width);
            float spriteHeight = Mathf.Max(1f, sprite.rect.height);
            float coverScale = Mathf.Max(frameWidth / spriteWidth, frameHeight / spriteHeight);
            Vector2 coveredSize = new Vector2(spriteWidth * coverScale, spriteHeight * coverScale) * sceneConfig.InsetRatio;

            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = coveredSize;
            contentRect.anchoredPosition3D = GetAutoAdjustedOffset(targetSkeletonParent, portraitRect, sceneConfig);
            contentRect.localScale = ShouldMirrorToLeftFacing(targetSkeletonParent) ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            contentRect.localRotation = Quaternion.identity;
        }

        private static Vector3 GetAutoAdjustedOffset(Transform targetSkeletonParent, RectTransform portraitRect, StaticImageSceneConfig sceneConfig)
        {
            if (portraitRect == null)
            {
                return sceneConfig.OffsetVector3;
            }

            Vector2 baseSize = _baseReferenceRectSize == Vector2.zero
                ? GetReferenceRectSize(portraitRect)
                : _baseReferenceRectSize;
            float referenceHeight = GetOffsetReferenceHeight(targetSkeletonParent, portraitRect);
            float heightRatio = referenceHeight / Mathf.Max(1f, baseSize.y);
            float uniformRatio = heightRatio;
            return new Vector3(sceneConfig.OffsetX * uniformRatio, sceneConfig.OffsetY * uniformRatio, 0f);
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
                return GetHeroIconBaseVisibleHeight(targetSkeletonParent);
            }

            return GetLayoutReferenceSize(targetSkeletonParent, portraitRect).y;
        }

        private static float GetHeroIconBaseVisibleHeight(Transform targetSkeletonParent)
        {
            if (IsBattleTeamHeroIconContext(targetSkeletonParent))
            {
                return 150f;
            }

            RectTransform targetRect = targetSkeletonParent != null ? targetSkeletonParent.TryCast<RectTransform>() : null;
            return targetRect == null ? 150f : Mathf.Max(1f, Mathf.Abs(targetRect.rect.height));
        }

        private static Vector2 GetLayoutReferenceSize(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            Vector2 referenceSize = GetReferenceRectSize(portraitRect);
            if (!IsBattleNowActiveHeroContext(targetSkeletonParent) || _baseReferenceRectSize == Vector2.zero)
            {
                return referenceSize;
            }

            return new Vector2(
                Mathf.Min(referenceSize.x, _baseReferenceRectSize.x),
                Mathf.Min(referenceSize.y, _baseReferenceRectSize.y));
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

        private static bool IsHeroIconContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return false;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            return path.EndsWith("/HeroIcon(Clone)/Back", StringComparison.Ordinal);
        }

        private static bool IsBattleTeamHeroIconContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return false;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            return path.Contains("/BattleTeamHeroIcon(Clone)/", StringComparison.Ordinal) &&
                   path.EndsWith("/HeroIcon(Clone)/Back", StringComparison.Ordinal);
        }

        private static bool IsBattleNowActiveHeroContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return false;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            return path.EndsWith("/BattleUIPanel/NowActiveHero/HeroSkeleton", StringComparison.Ordinal);
        }

        private static void CaptureBaseReferenceRectSize(Transform targetSkeletonParent, RectTransform portraitRect)
        {
            if (targetSkeletonParent == null || portraitRect == null)
            {
                return;
            }

            string path = Main.BuildTransformPath(targetSkeletonParent);
            if (!path.Contains("/HeroDetailPanel/Face", StringComparison.Ordinal) &&
                !path.Contains("/HudPanel/FaceMask/Face", StringComparison.Ordinal))
            {
                return;
            }

            _baseReferenceRectSize = GetReferenceRectSize(portraitRect);
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

    }
}
