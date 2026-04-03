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

        private static readonly HashSet<string> MissingImageWarnings = new(StringComparer.OrdinalIgnoreCase);
        private static string _cachedImagePath;
        private static DateTime _cachedImageWriteTimeUtc = DateTime.MinValue;
        private static Sprite _cachedSprite;
        private static Texture2D _cachedTexture;

        internal static void NotifyConfigReloaded()
        {
            MissingImageWarnings.Clear();
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

            Image portraitImage = GetOrCreatePortraitImage(targetSkeletonParent);
            if (portraitImage == null)
            {
                RestoreOriginalDisplay(targetSkeletonParent);
                Main.Log.Warning($"静态图片模式创建 Image 失败：{Main.DescribeTransformContext(targetSkeletonParent)}");
                return false;
            }

            RectTransform portraitRect = portraitImage.transform.TryCast<RectTransform>();
            CopyLayoutFromReference(targetSkeletonParent, portraitRect);
            portraitImage.sprite = sprite;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            portraitImage.color = Color.white;
            portraitImage.enabled = true;
            portraitImage.gameObject.SetActive(true);

            SetOnlyStaticPortraitVisible(targetSkeletonParent, portraitImage.transform);

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
    }
}
