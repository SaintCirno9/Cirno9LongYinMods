using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppSpine;
using Il2CppSpine.Unity;
using UnityEngine;

namespace LongYinPlayerSpeSkeleton
{
    /// <summary>
    /// 这里保留的是早期“手动接管 SpeSkeleton”的实现，仅作为归档参考。
    /// 当前运行中的方案已经切换为：
    /// 1. 强制玩家进入原版 UseSpeSkeleton 分支
    /// 2. 将原版加载的 SpeHero/0 资源重定向到配置的人物
    /// 因此这个文件中的方法不再被主流程调用。
    /// </summary>
    internal static class ManualSpeSkeletonArchive
    {
        private const string CustomSkeletonObjectName = "SpeSkeleton";
        private const string LegacyCustomSkeletonObjectName = "PlayerSpeSkeleton";
        private const string DefaultAnimationName = "idle";

        /// <summary>
        /// 旧版手工接管时用于避免重复刷“资源不存在”警告。
        /// 该缓存只留作归档逻辑自用。
        /// </summary>
        private static readonly HashSet<string> MissingResourceWarnings = new(StringComparer.Ordinal);

        /// <summary>
        /// 旧版入口：直接在补丁内创建或复用 SpeSkeleton 节点，并手动设置布局与动画。
        /// 现在已经停用，仅保留给后续排查历史行为时参考。
        /// </summary>
        internal static bool TryApplyPlayerSpeSkeletonLegacy(HeroData heroData, Transform targetSkeletonParent)
        {
            if (heroData == null || targetSkeletonParent == null)
            {
                if (Main.VerboseLog)
                {
                    Main.Log.Msg($"跳过旧版手动特殊立绘：heroData={(heroData == null ? "null" : "ok")}, targetSkeletonParent={(targetSkeletonParent == null ? "null" : "ok")}");
                }

                return false;
            }

            if (!Main.TryResolveConfiguredSpeHero(out SpeHeroLookupEntry speHeroEntry, out string resolveReason))
            {
                if (Main.VerboseLog)
                {
                    Main.Log.Msg($"旧版手动特殊立绘无法解析配置：{resolveReason}");
                }

                return false;
            }

            string resourcePath = $"Skeleton/SpeHero/{speHeroEntry.Id}/skeleton_SkeletonData";
            if (Main.VerboseLog)
            {
                Main.Log.Msg($"旧版手动特殊立绘尝试加载资源：hero={heroData.heroName}, heroID={heroData.heroID}, speHero={speHeroEntry.Name}, speHeroId={speHeroEntry.Id}, path={resourcePath}");
            }

            UnityEngine.Object loadedObject = Resources.Load(resourcePath);
            SkeletonDataAsset skeletonDataAsset = loadedObject != null ? loadedObject.TryCast<SkeletonDataAsset>() : null;
            if (skeletonDataAsset == null)
            {
                if (MissingResourceWarnings.Add(resourcePath))
                {
                    Main.Log.Warning($"旧版手动特殊立绘未找到资源：{resourcePath}");
                }

                return false;
            }

            try
            {
                Transform existingSpeSkeletonTransform = targetSkeletonParent.Find(CustomSkeletonObjectName);
                bool forceSiblingLayout = UseSiblingLayoutLegacy(targetSkeletonParent);
                SkeletonGraphic skeletonGraphic = GetOrCreateSkeletonGraphicLegacy(targetSkeletonParent, skeletonDataAsset);
                if (skeletonGraphic == null)
                {
                    if (Main.VerboseLog)
                    {
                        Main.Log.Msg("旧版手动特殊立绘创建或复用 SkeletonGraphic 失败。");
                    }

                    return false;
                }

                if (forceSiblingLayout)
                {
                    if (Main.VerboseLog)
                    {
                        Main.Log.Msg("旧版手动特殊立绘：当前容器使用卡片/头像布局模式。");
                    }

                    CopyLayoutFromSiblingLegacy(targetSkeletonParent, skeletonGraphic.transform);
                }
                else if (existingSpeSkeletonTransform == null)
                {
                    ApplySpeSkeletonLayoutLegacy(heroData, targetSkeletonParent, skeletonGraphic.transform);
                }
                else if (Main.VerboseLog)
                {
                    Main.Log.Msg("旧版手动特殊立绘：检测到原版 SpeSkeleton 节点，保留其原始布局。");
                }

                SetOnlyCustomSkeletonActiveLegacy(targetSkeletonParent, skeletonGraphic.transform);
                skeletonGraphic.gameObject.SetActive(true);
                skeletonGraphic.Initialize(true);

                if (forceSiblingLayout)
                {
                    CopyLayoutFromSiblingLegacy(targetSkeletonParent, skeletonGraphic.transform);
                }
                else
                {
                    skeletonGraphic.MatchRectTransformWithBounds();
                }

                AnimationState animationState = skeletonGraphic.AnimationState;
                if (animationState != null)
                {
                    animationState.SetAnimation(0, DefaultAnimationName, true);
                }

                if (Main.VerboseLog)
                {
                    Main.Log.Msg($"旧版手动特殊立绘已生效：hero={heroData.heroName}, speHero={speHeroEntry.Name}, speHeroId={speHeroEntry.Id}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Main.Log.Error($"旧版手动特殊立绘执行失败：{ex}");
                return false;
            }
        }

        /// <summary>
        /// 旧版恢复逻辑：重新显示原有骨骼节点，隐藏我们手工创建的 SpeSkeleton。
        /// </summary>
        internal static void RestoreOriginalSkeletonDisplayLegacy(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return;
            }

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic == null)
                {
                    continue;
                }

                bool isCustomSkeleton =
                    string.Equals(child.gameObject.name, CustomSkeletonObjectName, StringComparison.Ordinal) ||
                    string.Equals(child.gameObject.name, LegacyCustomSkeletonObjectName, StringComparison.Ordinal);
                bool shouldBeActive = !isCustomSkeleton;
                if (child.gameObject.activeSelf != shouldBeActive)
                {
                    child.gameObject.SetActive(shouldBeActive);
                }
            }
        }

        /// <summary>
        /// 旧版创建节点逻辑：找现有 SpeSkeleton，没有就新建一个。
        /// </summary>
        private static SkeletonGraphic GetOrCreateSkeletonGraphicLegacy(Transform targetSkeletonParent, SkeletonDataAsset skeletonDataAsset)
        {
            Transform existingTransform = targetSkeletonParent.Find(CustomSkeletonObjectName);
            Transform legacyTransform = targetSkeletonParent.Find(LegacyCustomSkeletonObjectName);
            if (existingTransform == null && legacyTransform != null)
            {
                existingTransform = legacyTransform;
            }

            SkeletonGraphic skeletonGraphic = existingTransform != null ? existingTransform.GetComponent<SkeletonGraphic>() : null;
            bool reused = skeletonGraphic != null;

            if (skeletonGraphic == null)
            {
                Material material = GameObjectController.Instance != null ? GameObjectController.Instance.skeletonGraphicDefault : null;
                skeletonGraphic = SkeletonGraphic.NewSkeletonGraphicGameObject(skeletonDataAsset, targetSkeletonParent, material);
                if (skeletonGraphic == null)
                {
                    return null;
                }

                skeletonGraphic.gameObject.name = CustomSkeletonObjectName;
            }
            else if (!string.Equals(skeletonGraphic.gameObject.name, CustomSkeletonObjectName, StringComparison.Ordinal))
            {
                skeletonGraphic.gameObject.name = CustomSkeletonObjectName;
            }

            skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
            if (Main.VerboseLog)
            {
                Main.Log.Msg(reused ? "旧版手动特殊立绘：复用已有 SpeSkeleton 节点。" : "旧版手动特殊立绘：已创建新的 SpeSkeleton 节点。");
            }

            return skeletonGraphic;
        }

        /// <summary>
        /// 旧版判定：这些容器没有沿用原版大立绘布局，所以当时使用 sibling 复制方案兜底。
        /// </summary>
        private static bool UseSiblingLayoutLegacy(Transform targetSkeletonParent)
        {
            string path = BuildTransformPathLegacy(targetSkeletonParent);
            return path.StartsWith("Canvas/StartMenu/", StringComparison.Ordinal) ||
                   path.StartsWith("Canvas/HudPanel/FaceMask/Face", StringComparison.Ordinal);
        }

        /// <summary>
        /// 旧版逻辑：确保只显示手工创建的骨骼节点。
        /// </summary>
        private static void SetOnlyCustomSkeletonActiveLegacy(Transform targetSkeletonParent, Transform customSkeletonTransform)
        {
            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic == null)
                {
                    continue;
                }

                bool shouldBeActive = child == customSkeletonTransform;
                if (child.gameObject.activeSelf != shouldBeActive)
                {
                    child.gameObject.SetActive(shouldBeActive);
                }
            }
        }

        /// <summary>
        /// 旧版大立绘布局：模仿原版特殊人物立绘的坐标与缩放。
        /// </summary>
        private static void ApplySpeSkeletonLayoutLegacy(HeroData heroData, Transform targetSkeletonParent, Transform customSkeletonTransform)
        {
            if (customSkeletonTransform == null)
            {
                return;
            }

            float yOffset = heroData != null && heroData.isFemale ? -255f : -270f;
            customSkeletonTransform.localPosition = new Vector3(0f, yOffset, 0f);
            customSkeletonTransform.localRotation = Quaternion.identity;

            float uniformScale = 1f;
            if (targetSkeletonParent != null && targetSkeletonParent.childCount > 0)
            {
                Transform firstChild = targetSkeletonParent.GetChild(0);
                if (firstChild != null)
                {
                    uniformScale = firstChild.localScale.x;
                }
            }

            customSkeletonTransform.localScale = Vector3.one * uniformScale;
        }

        /// <summary>
        /// 旧版卡片/头像布局：直接复制同层第一个 SkeletonGraphic 的位置与 RectTransform。
        /// </summary>
        private static void CopyLayoutFromSiblingLegacy(Transform targetSkeletonParent, Transform customSkeletonTransform)
        {
            if (customSkeletonTransform == null)
            {
                return;
            }

            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null || child == customSkeletonTransform)
                {
                    continue;
                }

                SkeletonGraphic childGraphic = child.GetComponent<SkeletonGraphic>();
                if (childGraphic == null)
                {
                    continue;
                }

                customSkeletonTransform.localPosition = child.localPosition;
                customSkeletonTransform.localRotation = child.localRotation;
                customSkeletonTransform.localScale = child.localScale;

                RectTransform sourceRect = child.TryCast<RectTransform>();
                RectTransform targetRect = customSkeletonTransform.TryCast<RectTransform>();
                if (sourceRect != null && targetRect != null)
                {
                    targetRect.anchorMin = sourceRect.anchorMin;
                    targetRect.anchorMax = sourceRect.anchorMax;
                    targetRect.pivot = sourceRect.pivot;
                    targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                    targetRect.sizeDelta = sourceRect.sizeDelta;
                    targetRect.offsetMin = sourceRect.offsetMin;
                    targetRect.offsetMax = sourceRect.offsetMax;
                }

                if (Main.VerboseLog)
                {
                    Main.Log.Msg($"旧版手动特殊立绘按 sibling 复制布局：source={child.gameObject.name}");
                }

                return;
            }

            customSkeletonTransform.localPosition = Vector3.zero;
            customSkeletonTransform.localRotation = Quaternion.identity;
            customSkeletonTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// 旧版辅助方法：生成 Transform 完整路径，便于按容器识别不同 UI 场景。
        /// </summary>
        private static string BuildTransformPathLegacy(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.gameObject.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
