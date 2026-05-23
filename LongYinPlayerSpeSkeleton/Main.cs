using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSpine;
using Il2CppSpine.Unity;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(LongYinPlayerSpeSkeleton.Main), "LongYinPlayerSpeSkeleton", "1.3.1", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinPlayerSpeSkeleton
{
    internal enum PlayerSkeletonRenderMode
    {
        Skeleton = 0,
        StaticImage = 1
    }

    internal sealed class SpeHeroLookupEntry
    {
        public SpeHeroLookupEntry(int id, string name, bool hasSpecialSkeleton)
        {
            Id = id;
            Name = name ?? string.Empty;
            HasSpecialSkeleton = hasSpecialSkeleton;
        }

        public int Id { get; }

        public string Name { get; }

        public bool HasSpecialSkeleton { get; }
    }

    public sealed partial class Main : MelonMod
    {
        internal sealed class SetSkeletonGraphicPatchState
        {
            public bool EnteredPlayerSpeContext { get; set; }

            public GameObject TemporaryGameObjectControllerObject { get; set; }

            public bool HandledByStaticImage { get; set; }
        }

        private const string SpeHeroResourcePath = "GameData/SpeHeroData";

        private static readonly HashSet<string> SpeHeroResolutionWarnings = new(StringComparer.Ordinal);
        private const string CharacterCreationToggleButtonName = "LongYinPlayerSpeSkeleton_CharacterCreationToggle";
        private const string HudToggleButtonName = "LongYinPlayerSpeSkeleton_HudToggle";

        private static string _cachedSpeHeroCsvText = string.Empty;
        private static Dictionary<string, SpeHeroLookupEntry> _cachedSpeHeroByName = new(StringComparer.OrdinalIgnoreCase);
        private static SpeHeroLookupEntry _resolvedSpeHeroEntry;
        private static string _resolvedSpeHeroReason = "尚未解析 PlayerSpeHeroName。";

        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            ReloadConfig(forceLog: true);
            Log.Msg("LongYinPlayerSpeSkeleton 已加载。");
            Log.Msg($"请在 {ConfigPathDisplay} 中设置 PlayerSpeHeroName，例如 白云天。");
            Log.Msg("已挂接 HeroData.SetSkeletonGraphic(...) 补丁。");
        }

        internal static bool ShouldReplacePlayerSkeleton(HeroData heroData)
        {
            return GetSkipReason(heroData, null) == null;
        }

        internal static bool ShouldTraceSpeHeroResourcePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(path.Trim(), SpeHeroResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetSkipReason(HeroData heroData, Transform targetSkeletonParent)
        {
            if (!Enabled)
            {
                return "Enabled=false";
            }

            if (heroData == null)
            {
                return "heroData=null";
            }

            if (heroData.isSummon)
            {
                return "isSummon=true";
            }

            if (heroData.heroID != 0)
            {
                return $"当前 heroID 不是 0：targetID={heroData.heroID}";
            }

            if (!ReplaceInCharacterCreation && IsCharacterCreationContext(targetSkeletonParent))
            {
                return "ReplaceInCharacterCreation=false，当前处于创建人物界面。";
            }

            if (RenderMode == PlayerSkeletonRenderMode.StaticImage)
            {
                return null;
            }

            return TryResolveConfiguredSpeHero(out _, out string reason) ? null : reason;
        }

        internal static void SetEnabledFromUi(bool enabled)
        {
            SetBoolConfigValue("Enabled", enabled);
            RefreshAfterUiToggle();
        }

        internal static bool IsCharacterCreationReplacementActive()
        {
            return Enabled && ReplaceInCharacterCreation;
        }

        internal static void SetReplaceInCharacterCreationFromUi(bool enabled)
        {
            if (enabled && !Enabled)
            {
                SetBoolConfigValue("Enabled", true);
            }

            _replaceInCharacterCreation = enabled;
            RefreshAfterUiToggle();
        }

        private static void RefreshAfterUiToggle()
        {
            try
            {
                StartMenuController startMenuController = StartMenuController.Instance;
                if (startMenuController != null)
                {
                    UpdateCharacterCreationToggleButtonLabel(startMenuController);
                    startMenuController.ResetPlayerSkeleton();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"刷新创建人物立绘失败：{ex.GetType().Name} - {ex.Message}");
            }

            try
            {
                HudController hudController = HudController.Instance;
                if (hudController != null)
                {
                    hudController.RefreshHeroSkeleton();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"刷新 HUD 玩家立绘失败：{ex.GetType().Name} - {ex.Message}");
            }

            try
            {
                HeroDetailController heroDetailController = HeroDetailController.Instance;
                if (IsHeroDetailPanelReady(heroDetailController))
                {
                    RefreshHeroDetailFace(heroDetailController);

                    HudController hudController = HudController.Instance;
                    if (hudController != null)
                    {
                        EnsureHudToggleButton(hudController);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"刷新人物详情立绘失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static void LogOriginalSpeSkeletonState(HeroData heroData, Transform targetSkeletonParent)
        {
            if (!VerboseLog || heroData == null || targetSkeletonParent == null)
            {
                return;
            }

            if (heroData.heroID == 0 || heroData.isSummon)
            {
                return;
            }

            bool useSpeSkeleton;
            try
            {
                useSpeSkeleton = heroData.UseSpeSkeleton();
            }
            catch (Exception ex)
            {
                Log.Warning($"读取原版特殊立绘状态失败：{ex.GetType().Name} - {ex.Message}");
                return;
            }

            if (!useSpeSkeleton)
            {
                return;
            }

            Log.Msg($"原版特殊立绘诊断：hero={heroData.heroName}, heroID={heroData.heroID}, childCount={targetSkeletonParent.childCount}");
            for (int i = 0; i < targetSkeletonParent.childCount; i++)
            {
                Transform child = targetSkeletonParent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                GameObject gameObject = child.gameObject;
                SkeletonGraphic skeletonGraphic = child.GetComponent<SkeletonGraphic>();
                string graphicState = skeletonGraphic == null ? "no-graphic" : "has-graphic";
                Vector3 position = child.localPosition;
                Vector3 scale = child.localScale;
                string animationName = GetCurrentAnimationName(skeletonGraphic);
                Log.Msg(
                    $"  child[{i}] name={gameObject.name}, active={gameObject.activeSelf}, {graphicState}, " +
                    $"pos=({position.x}, {position.y}, {position.z}), scale=({scale.x}, {scale.y}, {scale.z}), anim={animationName}");
            }
        }

        private static string GetCurrentAnimationName(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null)
            {
                return "n/a";
            }

            try
            {
                AnimationState animationState = skeletonGraphic.AnimationState;
                if (animationState == null)
                {
                    return "null";
                }

                TrackEntry trackEntry = animationState.GetCurrent(0);
                if (trackEntry == null || trackEntry.Animation == null || trackEntry.Animation.Name == null)
                {
                    return "none";
                }

                return trackEntry.Animation.Name;
            }
            catch
            {
                return "error";
            }
        }

        internal static bool TryResolveConfiguredSpeHero(out SpeHeroLookupEntry speHeroEntry, out string reason)
        {
            speHeroEntry = _resolvedSpeHeroEntry;
            reason = _resolvedSpeHeroReason;
            return speHeroEntry != null;
        }

        internal static bool ShouldForcePlayerUseSpeSkeleton(HeroData heroData, Transform targetSkeletonParent, out SpeHeroLookupEntry speHeroEntry, out string reason)
        {
            return OriginalManagedSpeSkeletonFlow.ShouldForcePlayerUseSpeSkeleton(heroData, targetSkeletonParent, out speHeroEntry, out reason);
        }

        internal static bool EnterPlayerSetSkeletonGraphicContext(HeroData heroData, Transform targetSkeletonParent)
        {
            return OriginalManagedSpeSkeletonFlow.EnterPlayerSetSkeletonGraphicContext(heroData, targetSkeletonParent);
        }

        internal static void ExitPlayerSetSkeletonGraphicContext(bool entered)
        {
            OriginalManagedSpeSkeletonFlow.ExitPlayerSetSkeletonGraphicContext(entered);
        }

        internal static int GetPlayerSetSkeletonGraphicContextDepth()
        {
            return OriginalManagedSpeSkeletonFlow.GetPlayerSetSkeletonGraphicContextDepth();
        }

        internal static bool TryRedirectPlayerSpeSkeletonPath(string originalPath, out string redirectedPath, out SpeHeroLookupEntry speHeroEntry)
        {
            return OriginalManagedSpeSkeletonFlow.TryRedirectPlayerSpeSkeletonPath(originalPath, out redirectedPath, out speHeroEntry);
        }

        internal static GameObject EnsureTemporaryGameObjectControllerForOriginalSpeSkeleton(Transform targetSkeletonParent)
        {
            return OriginalManagedSpeSkeletonFlow.EnsureTemporaryGameObjectControllerForOriginalSpeSkeleton(targetSkeletonParent);
        }

        internal static void CleanupTemporaryGameObjectController(GameObject temporaryControllerObject)
        {
            OriginalManagedSpeSkeletonFlow.CleanupTemporaryGameObjectController(temporaryControllerObject);
        }

        internal static void UpdateSpeHeroLookupFromTextAsset(TextAsset textAsset)
        {
            if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            {
                return;
            }

            UpdateSpeHeroLookupFromCsvText(textAsset.text);
        }

        private static void UpdateSpeHeroLookupFromCsvText(string csvTextRaw)
        {
            try
            {
                string csvText = csvTextRaw.Replace("\uFEFF", "");
                if (string.Equals(_cachedSpeHeroCsvText, csvText, StringComparison.Ordinal) &&
                    _cachedSpeHeroByName.Count > 0)
                {
                    return;
                }

                string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length <= 1)
                {
                    WarnSpeHeroResolutionOnce("捕获到的 GameData/SpeHeroData 没有可用数据行。");
                    return;
                }

                string[] headers = SplitCsvLine(lines[0]);
                int idColumnIndex = FindColumnIndex(headers, "id");
                int nameColumnIndex = FindColumnIndex(headers, "名字");
                int specialSkeletonColumnIndex = FindColumnIndex(headers, "特殊skeleton");
                if (idColumnIndex < 0 || nameColumnIndex < 0 || specialSkeletonColumnIndex < 0)
                {
                    WarnSpeHeroResolutionOnce("捕获到的 GameData/SpeHeroData 缺少必要列：id / 名字 / 特殊skeleton。");
                    return;
                }

                var byName = new Dictionary<string, SpeHeroLookupEntry>(StringComparer.OrdinalIgnoreCase);

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    string idText = GetCell(cells, idColumnIndex);
                    string nameText = GetCell(cells, nameColumnIndex);
                    string specialSkeletonText = GetCell(cells, specialSkeletonColumnIndex);
                    if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                    {
                        continue;
                    }

                    string normalizedName = NormalizeSpeHeroName(nameText);
                    if (normalizedName.Length == 0)
                    {
                        continue;
                    }

                    bool hasSpecialSkeleton =
                        string.Equals(specialSkeletonText, "1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(specialSkeletonText, "true", StringComparison.OrdinalIgnoreCase);

                    if (!hasSpecialSkeleton)
                    {
                        continue;
                    }

                    var entry = new SpeHeroLookupEntry(heroId, nameText.Trim(), hasSpecialSkeleton);
                    byName[normalizedName] = entry;
                }

                _cachedSpeHeroCsvText = csvText;
                _cachedSpeHeroByName = byName;
                SpeHeroResolutionWarnings.Clear();
                RefreshResolvedSpeHero();

                if (VerboseLog)
                {
                    Log.Msg($"已从 Resources.Load 捕获 GameData/SpeHeroData：可用特殊人物 {byName.Count} 条。");
                }
            }
            catch (Exception ex)
            {
                WarnSpeHeroResolutionOnce($"解析 GameData/SpeHeroData 失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static void WarnSpeHeroResolutionOnce(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (SpeHeroResolutionWarnings.Add(reason))
            {
                Log.Warning(reason);
            }
        }

        private static void RefreshResolvedSpeHero()
        {
            string configuredName = NormalizeSpeHeroName(PlayerSpeHeroName);
            if (configuredName.Length == 0)
            {
                _resolvedSpeHeroEntry = null;
                _resolvedSpeHeroReason = "PlayerSpeHeroName 为空。";
                return;
            }

            if (_cachedSpeHeroByName.Count == 0)
            {
                _resolvedSpeHeroEntry = null;
                _resolvedSpeHeroReason = "尚未捕获 GameData/SpeHeroData 资源，暂时无法按名字解析特殊人物。";
                return;
            }

            if (!_cachedSpeHeroByName.TryGetValue(configuredName, out SpeHeroLookupEntry entry))
            {
                _resolvedSpeHeroEntry = null;
                _resolvedSpeHeroReason = $"未在已缓存的可用特殊人物中找到名字为“{PlayerSpeHeroName}”的人物。";
                WarnSpeHeroResolutionOnce(_resolvedSpeHeroReason);
                return;
            }

            _resolvedSpeHeroEntry = entry;
            _resolvedSpeHeroReason = null;

            if (VerboseLog)
            {
                Log.Msg($"PlayerSpeHeroName 已解析成功：{entry.Name} -> ID={entry.Id}");
            }
        }

        private static string NormalizeSpeHeroName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Trim()
                .Trim('\u3000')
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace("。", string.Empty, StringComparison.Ordinal)
                .Replace("·", string.Empty, StringComparison.Ordinal)
                .Replace("•", string.Empty, StringComparison.Ordinal)
                .Replace("‧", string.Empty, StringComparison.Ordinal)
                .Replace("・", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("\t", string.Empty, StringComparison.Ordinal);
        }

        private static int FindColumnIndex(string[] headers, string columnName)
        {
            if (headers == null)
            {
                return -1;
            }

            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i].Trim('\uFEFF', ' ', '\t'), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetCell(string[] cells, int index)
        {
            return cells != null && index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        private static string[] SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];
                if (currentChar == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (currentChar == ',' && !inQuotes)
                {
                    cells.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(currentChar);
            }

            cells.Add(current.ToString());
            return cells.ToArray();
        }

        internal static string DescribeTransformContext(Transform targetSkeletonParent)
        {
            if (targetSkeletonParent == null)
            {
                return "targetSkeletonParent=null";
            }

            string path = BuildTransformPath(targetSkeletonParent);
            RectTransform rectTransform = targetSkeletonParent.TryCast<RectTransform>();
            string rectInfo = rectTransform == null
                ? "rect=n/a"
                : $"rect=({rectTransform.rect.width}, {rectTransform.rect.height}), anchored=({rectTransform.anchoredPosition3D.x}, {rectTransform.anchoredPosition3D.y}, {rectTransform.anchoredPosition3D.z})";

            string parentName = targetSkeletonParent.parent == null ? "<null>" : targetSkeletonParent.parent.gameObject.name;
            return $"path={path}, parent={parentName}, childCount={targetSkeletonParent.childCount}, {rectInfo}";
        }

        internal static bool IsCharacterCreationContext(Transform targetSkeletonParent)
        {
            string path = BuildTransformPath(targetSkeletonParent);
            return path.StartsWith("Canvas/StartMenu/", StringComparison.Ordinal);
        }

        internal static void EnsureCharacterCreationToggleButton(StartMenuController controller)
        {
            if (controller == null || controller.startMenu == null || controller.heroSkeleton == null)
            {
                return;
            }

            RectTransform referenceRect = controller.heroSkeleton.transform.TryCast<RectTransform>();
            RectTransform parentRect = referenceRect != null && referenceRect.parent != null
                ? referenceRect.parent.TryCast<RectTransform>()
                : null;
            if (referenceRect == null || parentRect == null)
            {
                return;
            }

            GameObject existingObject = GameObject.Find(CharacterCreationToggleButtonName);
            if (existingObject != null && existingObject.transform.parent != parentRect.transform)
            {
                UnityEngine.Object.Destroy(existingObject);
                existingObject = null;
            }

            Transform existing = existingObject != null ? existingObject.transform : parentRect.Find(CharacterCreationToggleButtonName);
            if (existing == null)
            {
                Text templateText = controller.evilText != null ? controller.evilText : controller.chaosText;
                if (templateText == null)
                {
                    return;
                }

                GameObject buttonObject = CreateCheckboxButton(
                    CharacterCreationToggleButtonName,
                    parentRect,
                    templateText,
                    new Vector2(220f, 32f),
                    () => SetReplaceInCharacterCreationFromUi(!IsCharacterCreationReplacementActive()));
                if (buttonObject == null)
                {
                    return;
                }

                existing = buttonObject.transform;
            }

            UpdateCharacterCreationToggleButtonLabel(controller);
            PositionCharacterCreationToggleButton(existing.TryCast<RectTransform>(), referenceRect, new Vector2(220f, 32f));
        }

        internal static void UpdateCharacterCreationToggleButtonLabel(StartMenuController controller)
        {
            if (controller?.heroSkeleton == null)
            {
                return;
            }

            Transform buttonTransform = FindUniqueButtonTransform(CharacterCreationToggleButtonName);
            if (buttonTransform == null)
            {
                return;
            }

            UpdateCheckboxButtonVisual(buttonTransform, "特殊立绘", IsCharacterCreationReplacementActive());
        }

        internal static void EnsureHudToggleButton(HudController controller)
        {
            if (controller == null || controller.heroFace == null)
            {
                return;
            }

            HeroDetailController heroDetailController = HeroDetailController.Instance;
            if (!IsPlayerHeroDetailReady(heroDetailController))
            {
                DestroyExistingButton(HudToggleButtonName);
                return;
            }

            RectTransform referenceRect = FindMainPortraitReferenceRect(controller);
            RectTransform parentRect = referenceRect != null && referenceRect.parent != null
                ? referenceRect.parent.TryCast<RectTransform>()
                : null;
            if (referenceRect == null || parentRect == null)
            {
                DestroyExistingButton(HudToggleButtonName);
                return;
            }

            GameObject existingObject = GameObject.Find(HudToggleButtonName);
            if (existingObject != null && existingObject.transform.parent != parentRect.transform)
            {
                UnityEngine.Object.Destroy(existingObject);
                existingObject = null;
            }

            Transform existing = existingObject != null ? existingObject.transform : parentRect.Find(HudToggleButtonName);
            if (existing == null)
            {
                Text templateText = controller.nameLabel != null ? controller.nameLabel : controller.moneyLabel;
                if (templateText == null)
                {
                    return;
                }

                GameObject buttonObject = CreateCheckboxButton(
                    HudToggleButtonName,
                    parentRect,
                    templateText,
                    new Vector2(180f, 28f),
                    () => SetEnabledFromUi(!Enabled));
                if (buttonObject == null)
                {
                    return;
                }

                existing = buttonObject.transform;
            }

            UpdateHudToggleButtonLabel(controller);
            PositionHeroDetailToggleButton(existing.TryCast<RectTransform>(), referenceRect, new Vector2(180f, 28f));
        }

        internal static void UpdateHudToggleButtonLabel(HudController controller)
        {
            if (controller == null)
            {
                return;
            }

            Transform buttonTransform = FindUniqueButtonTransform(HudToggleButtonName);
            if (buttonTransform == null)
            {
                return;
            }

            UpdateCheckboxButtonVisual(buttonTransform, "特殊立绘", Enabled);
        }

        private static GameObject CreateCheckboxButton(
            string name,
            RectTransform parent,
            Text templateText,
            Vector2 sizeDelta,
            Action onClick)
        {
            if (parent == null || templateText == null || onClick == null)
            {
                return null;
            }

            var buttonObject = new GameObject(name);
            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;

            Image hitArea = buttonObject.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            hitArea.raycastTarget = true;

            GameObject boxObject = new GameObject("Box");
            RectTransform boxRect = boxObject.AddComponent<RectTransform>();
            boxRect.SetParent(rectTransform, false);
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0f, 0f);
            boxRect.sizeDelta = new Vector2(18f, 18f);

            Image boxImage = boxObject.AddComponent<Image>();
            boxImage.color = new Color(0.90f, 0.90f, 0.90f, 0.92f);

            Outline boxOutline = boxObject.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
            boxOutline.effectDistance = new Vector2(1f, -1f);

            GameObject checkObject = new GameObject("Checkmark");
            RectTransform checkRect = checkObject.AddComponent<RectTransform>();
            checkRect.SetParent(boxRect, false);
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = Vector2.zero;
            checkRect.sizeDelta = new Vector2(18f, 18f);

            Text checkText = checkObject.AddComponent<Text>();
            checkText.font = templateText.font;
            checkText.fontSize = Math.Max(20, templateText.fontSize);
            checkText.alignment = TextAnchor.MiddleCenter;
            checkText.horizontalOverflow = HorizontalWrapMode.Overflow;
            checkText.verticalOverflow = VerticalWrapMode.Overflow;
            checkText.raycastTarget = false;
            checkText.text = "√";
            checkText.color = new Color(0.22f, 0.88f, 0.50f, 1f);

            GameObject labelObject = new GameObject("Label");
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.SetParent(rectTransform, false);
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(26f, -16f);
            labelRect.offsetMax = new Vector2(0f, 16f);

            Text labelText = labelObject.AddComponent<Text>();
            labelText.font = templateText.font;
            labelText.fontSize = Math.Max(18, templateText.fontSize);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.raycastTarget = false;

            Outline labelOutline = labelObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener((UnityAction)(() => onClick()));

            return buttonObject;
        }

        private static void UpdateCheckboxButtonVisual(Transform buttonTransform, string label, bool enabled)
        {
            if (buttonTransform == null)
            {
                return;
            }

            Transform labelTransform = buttonTransform.Find("Label");
            Text labelText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (labelText != null)
            {
                labelText.text = label;
                labelText.color = enabled ? new Color(0.96f, 0.96f, 0.96f, 1f) : new Color(0.78f, 0.78f, 0.78f, 1f);
            }

            Transform boxTransform = buttonTransform.Find("Box");
            Image boxImage = boxTransform != null ? boxTransform.GetComponent<Image>() : null;
            if (boxImage != null)
            {
                boxImage.color = enabled ? new Color(0.92f, 0.92f, 0.92f, 0.95f) : new Color(0.70f, 0.70f, 0.70f, 0.85f);
            }

            Transform checkTransform = boxTransform != null ? boxTransform.Find("Checkmark") : null;
            Text checkText = checkTransform != null ? checkTransform.GetComponent<Text>() : null;
            if (checkText != null)
            {
                checkText.text = enabled ? "√" : string.Empty;
            }
        }

        private static void PositionButtonAboveReference(RectTransform buttonRect, RectTransform referenceRect, Vector2 sizeDelta, float verticalOffset)
        {
            if (buttonRect == null || referenceRect == null)
            {
                return;
            }

            RectTransform parentRect = buttonRect.parent != null ? buttonRect.parent.TryCast<RectTransform>() : null;
            if (parentRect == null)
            {
                return;
            }

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = sizeDelta;
            buttonRect.localScale = Vector3.one;
            buttonRect.localRotation = Quaternion.identity;

            Vector3[] corners = new Vector3[4];
            referenceRect.GetWorldCorners(corners);
            Vector3 worldTopCenter = (corners[1] + corners[2]) * 0.5f;
            Vector3 localTopCenter = parentRect.InverseTransformPoint(worldTopCenter);

            buttonRect.localPosition = new Vector3(
                localTopCenter.x,
                localTopCenter.y + verticalOffset,
                0f);
        }

        private static void PositionCharacterCreationToggleButton(RectTransform buttonRect, RectTransform referenceRect, Vector2 sizeDelta)
        {
            if (buttonRect == null || referenceRect == null)
            {
                return;
            }

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = sizeDelta;
            buttonRect.localScale = Vector3.one;
            buttonRect.localRotation = Quaternion.identity;

            float width = Math.Max(buttonRect.rect.width, sizeDelta.x);
            float height = Math.Max(buttonRect.rect.height, sizeDelta.y);
            float contentCenterOffset = GetCheckboxContentCenterOffset(buttonRect, width);
            float referenceCenterX = referenceRect.localPosition.x + referenceRect.rect.width * (0.5f - referenceRect.pivot.x);
            float buttonCenterX = referenceCenterX + (width * 0.5f - contentCenterOffset) - 24f;
            float topY = referenceRect.localPosition.y + referenceRect.rect.height * (1f - referenceRect.pivot.y);

            buttonRect.localPosition = new Vector3(buttonCenterX, topY + height * 0.5f + 6f, 0f);
        }

        private static void PositionHeroDetailToggleButton(RectTransform buttonRect, RectTransform referenceRect, Vector2 sizeDelta)
        {
            if (buttonRect == null || referenceRect == null)
            {
                return;
            }

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = sizeDelta;
            buttonRect.localScale = Vector3.one;
            buttonRect.localRotation = Quaternion.identity;

            float width = Math.Max(buttonRect.rect.width, sizeDelta.x);
            float height = Math.Max(buttonRect.rect.height, sizeDelta.y);
            float leftX = referenceRect.localPosition.x - referenceRect.rect.width * referenceRect.pivot.x;
            float topY = referenceRect.localPosition.y + referenceRect.rect.height * (1f - referenceRect.pivot.y);
            buttonRect.localPosition = new Vector3(leftX + width * 0.5f + 20f, topY - height * 0.5f - 100f, 0f);
        }

        private static float GetCheckboxContentCenterOffset(RectTransform buttonRect, float fallbackWidth)
        {
            if (buttonRect == null)
            {
                return fallbackWidth * 0.5f;
            }

            const float checkboxWidth = 18f;
            const float spacing = 8f;
            Transform labelTransform = buttonRect.Find("Label");
            Text labelText = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            float labelWidth = labelText != null ? Math.Max(labelText.preferredWidth, 40f) : 72f;
            float contentWidth = checkboxWidth + spacing + labelWidth;
            return contentWidth * 0.5f;
        }

        private static RectTransform FindMainPortraitReferenceRect(HudController controller)
        {
            HeroDetailController heroDetailController = HeroDetailController.Instance;
            if (IsPlayerHeroDetailReady(heroDetailController))
            {
                Transform portraitTransform = heroDetailController.heroDetailPanel.transform.Find("Face") ??
                                             FindDescendantByName(heroDetailController.heroDetailPanel.transform, "Face") ??
                                             FindDescendantByName(heroDetailController.heroDetailPanel.transform, "LeftFace") ??
                                             FindDescendantByName(heroDetailController.heroDetailPanel.transform, "HeroSkeleton");
                RectTransform portraitRect = portraitTransform != null ? portraitTransform.TryCast<RectTransform>() : null;
                if (portraitRect != null)
                {
                    return portraitRect;
                }
            }

            return null;
        }

        private static bool IsHeroDetailPanelReady(HeroDetailController heroDetailController)
        {
            return heroDetailController != null &&
                   heroDetailController.heroDetailPanel != null &&
                   heroDetailController.heroDetailPanel.activeInHierarchy &&
                   heroDetailController.nowShowHero != null;
        }

        private static bool IsPlayerHeroDetailReady(HeroDetailController heroDetailController)
        {
            return IsHeroDetailPanelReady(heroDetailController) &&
                   heroDetailController.nowShowHero.heroID == 0;
        }

        private static void RefreshHeroDetailFace(HeroDetailController heroDetailController)
        {
            if (!IsHeroDetailPanelReady(heroDetailController))
            {
                return;
            }

            Transform faceTransform = heroDetailController.heroDetailPanel.transform.Find("Face") ??
                                      FindDescendantByName(heroDetailController.heroDetailPanel.transform, "Face");
            if (faceTransform == null)
            {
                return;
            }

            heroDetailController.nowShowHero.SetSkeletonGraphic(faceTransform, -99, -1);
        }

        private static void DestroyExistingButton(string buttonName)
        {
            GameObject buttonObject = GameObject.Find(buttonName);
            if (buttonObject != null)
            {
                UnityEngine.Object.Destroy(buttonObject);
            }
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (string.Equals(root.gameObject.name, targetName, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform result = FindDescendantByName(child, targetName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Transform FindUniqueButtonTransform(string buttonName)
        {
            GameObject buttonObject = GameObject.Find(buttonName);
            return buttonObject != null ? buttonObject.transform : null;
        }

        internal static string BuildTransformPath(Transform transform)
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

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.SetSkeletonGraphic), new[] { typeof(Transform), typeof(int), typeof(int) })]
    internal static class HeroDataSetSkeletonGraphicPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HeroData __instance, Transform targetSkeletonParent, int forceSkinID, int forceSkinLv, ref Main.SetSkeletonGraphicPatchState __state)
        {
            __state = new Main.SetSkeletonGraphicPatchState
            {
                EnteredPlayerSpeContext = false,
                HandledByStaticImage = false
            };

            if (Main.RenderMode == PlayerSkeletonRenderMode.StaticImage)
            {
                if (StaticImageSpeSkeletonFlow.TryApplyPlayerStaticImage(__instance, targetSkeletonParent))
                {
                    __state.HandledByStaticImage = true;
                    return false;
                }
            }
            else
            {
                StaticImageSpeSkeletonFlow.RestoreOriginalDisplay(targetSkeletonParent);
            }

            __state.EnteredPlayerSpeContext = Main.EnterPlayerSetSkeletonGraphicContext(__instance, targetSkeletonParent);
            if (__state.EnteredPlayerSpeContext)
            {
                __state.TemporaryGameObjectControllerObject = Main.EnsureTemporaryGameObjectControllerForOriginalSpeSkeleton(targetSkeletonParent);
            }

            if (Main.VerboseLog)
            {
                string heroName = __instance == null ? "null" : __instance.heroName;
                int heroId = __instance == null ? -1 : __instance.heroID;
                Main.Log.Msg(
                    $"进入 SetSkeletonGraphic 补丁：hero={heroName}, heroID={heroId}, forceSkinID={forceSkinID}, forceSkinLv={forceSkinLv}, " +
                    Main.DescribeTransformContext(targetSkeletonParent));
                if (__state.EnteredPlayerSpeContext && Main.TryResolveConfiguredSpeHero(out SpeHeroLookupEntry speHeroEntry, out _))
                {
                    Main.Log.Msg($"本次进入原版特殊立绘分支托管模式：playerSpeHero={speHeroEntry.Name}, speHeroId={speHeroEntry.Id}");
                }
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(HeroData __instance, Transform targetSkeletonParent, Main.SetSkeletonGraphicPatchState __state)
        {
            if (!(__state?.HandledByStaticImage ?? false))
            {
                Main.LogOriginalSpeSkeletonState(__instance, targetSkeletonParent);
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, HeroData __instance, Transform targetSkeletonParent, int forceSkinID, int forceSkinLv, Main.SetSkeletonGraphicPatchState __state)
        {
            try
            {
                if (__exception != null)
                {
                    Main.Log.Warning(
                        $"SetSkeletonGraphic 执行异常：{__exception.GetType().Name} - {__exception.Message}，" +
                        Main.DescribeTransformContext(targetSkeletonParent));
                }

                return __exception;
            }
            finally
            {
                Main.CleanupTemporaryGameObjectController(__state?.TemporaryGameObjectControllerObject);
                Main.ExitPlayerSetSkeletonGraphicContext(__state?.EnteredPlayerSpeContext == true);
            }
        }
    }

    [HarmonyPatch(typeof(StartMenuController), nameof(StartMenuController.ShowStartMenu))]
    internal static class StartMenuControllerShowStartMenuPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StartMenuController __instance)
        {
            Main.EnsureCharacterCreationToggleButton(__instance);
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Awake))]
    internal static class HudControllerAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HudController __instance)
        {
            Main.EnsureHudToggleButton(__instance);
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.RefreshHeroSkeleton))]
    internal static class HudControllerRefreshHeroSkeletonPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HudController __instance)
        {
            Main.EnsureHudToggleButton(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.RefreshHeroSkeleton))]
    internal static class HeroDetailControllerRefreshHeroSkeletonPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            HudController hudController = HudController.Instance;
            if (hudController != null)
            {
                Main.EnsureHudToggleButton(hudController);
            }
        }
    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Il2CppSystem.Type) })]
    internal static class ResourcesLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            if (!Main.ShouldTraceSpeHeroResourcePath(path))
            {
                return;
            }

            try
            {
                TextAsset textAsset = __result != null ? __result.TryCast<TextAsset>() : null;
                if (textAsset != null)
                {
                    Main.UpdateSpeHeroLookupFromTextAsset(textAsset);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Warning($"捕获 GameData/SpeHeroData 资源失败：{ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
