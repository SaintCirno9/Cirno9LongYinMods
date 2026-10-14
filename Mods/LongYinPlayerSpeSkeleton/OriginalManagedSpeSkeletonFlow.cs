using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppSpine.Unity;
using UnityEngine;

namespace LongYinPlayerSpeSkeleton
{
    /// <summary>
    /// 原版托管模式：
    /// 1. 让玩家临时进入原版 UseSpeSkeleton 分支
    /// 2. 将原版访问的 SpeHero/0 资源重定向到配置人物
    /// 3. 必要时补建临时 GameObjectController，避免原版空引用
    /// </summary>
    internal static class OriginalManagedSpeSkeletonFlow
    {
        private static int _playerSetSkeletonGraphicContextDepth;
        private static readonly FieldInfo GameObjectControllerInstanceField =
            typeof(GameObjectController).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        internal static bool ShouldForcePlayerUseSpeSkeleton(HeroData heroData, Transform targetSkeletonParent, out SpeHeroLookupEntry speHeroEntry, out string reason)
        {
            speHeroEntry = null;
            reason = Main.GetSkipReason(heroData, targetSkeletonParent);
            if (reason != null)
            {
                return false;
            }

            return Main.TryResolveConfiguredSpeHero(out speHeroEntry, out reason);
        }

        internal static bool EnterPlayerSetSkeletonGraphicContext(HeroData heroData, Transform targetSkeletonParent)
        {
            if (!ShouldForcePlayerUseSpeSkeleton(heroData, targetSkeletonParent, out _, out _))
            {
                return false;
            }

            _playerSetSkeletonGraphicContextDepth++;
            return true;
        }

        internal static void ExitPlayerSetSkeletonGraphicContext(bool entered)
        {
            if (!entered)
            {
                return;
            }

            if (_playerSetSkeletonGraphicContextDepth > 0)
            {
                _playerSetSkeletonGraphicContextDepth--;
            }
        }

        internal static int GetPlayerSetSkeletonGraphicContextDepth()
        {
            return _playerSetSkeletonGraphicContextDepth;
        }

        internal static bool TryRedirectPlayerSpeSkeletonPath(string originalPath, out string redirectedPath, out SpeHeroLookupEntry speHeroEntry)
        {
            redirectedPath = originalPath;
            speHeroEntry = null;

            if (_playerSetSkeletonGraphicContextDepth <= 0)
            {
                return false;
            }

            if (!string.Equals(originalPath, "Skeleton/SpeHero/0/skeleton_SkeletonData", StringComparison.Ordinal))
            {
                return false;
            }

            if (!Main.TryResolveConfiguredSpeHero(out speHeroEntry, out _))
            {
                return false;
            }

            redirectedPath = $"Skeleton/SpeHero/{speHeroEntry.Id}/skeleton_SkeletonData";
            return true;
        }

        internal static GameObject EnsureTemporaryGameObjectControllerForOriginalSpeSkeleton(Transform targetSkeletonParent)
        {
            if (GameObjectController.Instance != null)
            {
                return null;
            }

            Material fallbackMaterial = FindFallbackSkeletonGraphicMaterial(targetSkeletonParent);
            if (fallbackMaterial == null)
            {
                if (Main.VerboseLog)
                {
                    Main.Log.Warning("当前没有可复用的 SkeletonGraphic 材质，无法为原版特殊立绘分支补建临时 GameObjectController。");
                }

                return null;
            }

            var proxyObject = new GameObject("LongYinPlayerSpeSkeleton_TempGameObjectController");
            GameObjectController proxyController = proxyObject.AddComponent<GameObjectController>();
            proxyController.skeletonGraphicDefault = fallbackMaterial;
            proxyController.spineDefaultGraphicMaterial = fallbackMaterial;
            GameObjectControllerInstanceField?.SetValue(null, proxyController);

            if (Main.VerboseLog)
            {
                Main.Log.Msg($"已补建临时 GameObjectController：material={fallbackMaterial.name}, context={Main.DescribeTransformContext(targetSkeletonParent)}");
            }

            return proxyObject;
        }

        internal static void CleanupTemporaryGameObjectController(GameObject temporaryControllerObject)
        {
            if (temporaryControllerObject == null)
            {
                return;
            }

            try
            {
                GameObjectController currentInstance = GameObjectController.Instance;
                GameObjectController temporaryController = temporaryControllerObject.GetComponent<GameObjectController>();
                if (currentInstance != null && temporaryController != null && currentInstance == temporaryController)
                {
                    GameObjectControllerInstanceField?.SetValue(null, null);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"清理临时 GameObjectController 失败：{ex.GetType().Name} - {ex.Message}");
            }

            UnityEngine.Object.Destroy(temporaryControllerObject);
        }

        private static Material FindFallbackSkeletonGraphicMaterial(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return null;
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

                Material material = childGraphic.material;
                if (material != null)
                {
                    return material;
                }

                Material renderingMaterial = childGraphic.materialForRendering;
                if (renderingMaterial != null)
                {
                    return renderingMaterial;
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.UseSpeSkeleton))]
    internal static class HeroDataUseSpeSkeletonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroData __instance, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            if (OriginalManagedSpeSkeletonFlow.GetPlayerSetSkeletonGraphicContextDepth() <= 0)
            {
                return;
            }

            if (!OriginalManagedSpeSkeletonFlow.ShouldForcePlayerUseSpeSkeleton(__instance, null, out SpeHeroLookupEntry speHeroEntry, out _))
            {
                return;
            }

            __result = true;
            if (Main.VerboseLog)
            {
                Main.Log.Msg($"强制玩家走原版 UseSpeSkeleton 分支：hero={__instance.heroName}, speHero={speHeroEntry.Name}, speHeroId={speHeroEntry.Id}");
            }
        }
    }

    [HarmonyPatch]
    internal static class ResourcesLoadStringPatch
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo method in typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(method.Name, nameof(Resources.Load), StringComparison.Ordinal) || method.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    return method;
                }
            }

            return null;
        }

        [HarmonyPrefix]
        private static void Prefix(ref string path)
        {
            if (!OriginalManagedSpeSkeletonFlow.TryRedirectPlayerSpeSkeletonPath(path, out string redirectedPath, out SpeHeroLookupEntry speHeroEntry))
            {
                return;
            }

            if (Main.VerboseLog)
            {
                Main.Log.Msg($"重定向玩家特殊立绘资源：from={path}, to={redirectedPath}, speHero={speHeroEntry.Name}, speHeroId={speHeroEntry.Id}");
            }

            path = redirectedPath;
        }
    }
}
