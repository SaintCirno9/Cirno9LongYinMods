using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(LongYinFontReplacement.Main), "LongYinFontReplacement", "0.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinFontReplacement
{
    internal enum FontSourceKind
    {
        Bundle,
        Os
    }

    internal sealed class FontReplacementRule
    {
        public FontReplacementRule(string originalFontName, FontSourceKind sourceKind, string sourcePath, string assetName, float lineSpacingScale)
        {
            OriginalFontName = originalFontName;
            SourceKind = sourceKind;
            SourcePath = sourcePath;
            AssetName = assetName;
            LineSpacingScale = lineSpacingScale;
        }

        public string OriginalFontName { get; }

        public FontSourceKind SourceKind { get; }

        public string SourcePath { get; }

        public string AssetName { get; }

        public float LineSpacingScale { get; }
    }

    public class Main : MelonMod
    {
        private const float DefaultLineSpacingScale = 0.8f;

        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinFontReplacement");
        internal static readonly string BundleDir = Path.Combine(ConfigDir, "Bundles");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "FontMappings.txt");

        internal static readonly Dictionary<string, FontReplacementRule> ReplacementRules = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, Font> CachedFonts = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, AssetBundle> CachedBundles = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<int, float> OriginalLineSpacings = new();
        internal static readonly HashSet<string> WarnedMessages = new(StringComparer.OrdinalIgnoreCase);

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            EnsureConfigExists();
            ReloadConfig();
            HarmonyInstance.PatchAll();
            LOG.Msg($"LongYinFontReplacement 已加载，配置目录：{ConfigDir}");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            TryApplyToAllLoadedTexts($"场景初始化：{sceneName}");
        }

        internal static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(BundleDir);

            if (File.Exists(ConfigPath))
            {
                return;
            }

            string sampleConfig =
                "# 字体替换配置，编码请保持 UTF-8\r\n" +
                "# 每行格式：原字体名|资源类型|参数1|参数2|行距倍率\r\n" +
                "# 资源类型：os 或 bundle\r\n" +
                "# os：参数1=系统字体名称，参数2留空\r\n" +
                "# bundle：参数1=相对 UserData\\LongYinFontReplacement 的 AssetBundle 路径，参数2=Bundle 内的 Font 资源名\r\n" +
                "# 当前版本先接管 LTLocalization 的 UnityEngine.UI.Text 链路。\r\n" +
                "汇文明朝体|os|Microsoft YaHei||0.80\r\n" +
                "SourceHanSansCN-Regular|os|Microsoft YaHei||0.80\r\n" +
                "SourceHanSansCN-Bold|os|Microsoft YaHei||0.80\r\n" +
                "SourceHanSerifCN-Medium|os|SimSun||0.80\r\n" +
                "SourceHanSerifCN-Bold|os|SimSun||0.80\r\n" +
                "字体家AI造字剑客|os|SimHei||0.80\r\n" +
                "字体家AI造字霸行|os|SimHei||0.80\r\n";

            File.WriteAllText(ConfigPath, sampleConfig);
        }

        internal static void ReloadConfig()
        {
            ReplacementRules.Clear();
            CachedFonts.Clear();
            WarnedMessages.Clear();

            if (!File.Exists(ConfigPath))
            {
                LOG.Warning($"未找到字体替换配置文件：{ConfigPath}");
                return;
            }

            int loadedCount = 0;
            foreach (string rawLine in File.ReadAllLines(ConfigPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split('|');
                if (parts.Length < 5)
                {
                    WarnOnce($"配置行格式不正确：{line}");
                    continue;
                }

                string originalFontName = NormalizeFontName(parts[0]);
                string sourceKindText = parts[1].Trim();
                string sourcePath = parts[2].Trim();
                string assetName = parts[3].Trim();
                string lineSpacingText = parts[4].Trim();

                if (string.IsNullOrWhiteSpace(originalFontName) ||
                    string.IsNullOrWhiteSpace(sourceKindText) ||
                    string.IsNullOrWhiteSpace(sourcePath))
                {
                    WarnOnce($"配置行缺少必要字段：{line}");
                    continue;
                }

                if (!TryParseSourceKind(sourceKindText, out FontSourceKind sourceKind))
                {
                    WarnOnce($"未知的字体来源类型：{sourceKindText}");
                    continue;
                }

                if (!float.TryParse(lineSpacingText, NumberStyles.Float, CultureInfo.InvariantCulture, out float lineSpacingScale))
                {
                    lineSpacingScale = DefaultLineSpacingScale;
                }

                ReplacementRules[originalFontName] = new FontReplacementRule(
                    originalFontName,
                    sourceKind,
                    sourcePath,
                    assetName,
                    lineSpacingScale);
                loadedCount++;
            }

            LOG.Msg($"已加载 {loadedCount} 条字体替换规则。");
        }

        internal static bool TryApplyToText(Text targetText, string source)
        {
            if (targetText == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetText.text))
            {
                return false;
            }

            Font currentFont = targetText.font;
            if (currentFont == null)
            {
                return false;
            }

            if (!TryGetRule(currentFont.name, out FontReplacementRule rule))
            {
                return false;
            }

            Font replacementFont = ResolveFont(rule);
            if (replacementFont == null)
            {
                WarnOnce($"无法加载替换字体：{rule.OriginalFontName}");
                return false;
            }

            int textId = targetText.GetInstanceID();
            if (!OriginalLineSpacings.ContainsKey(textId))
            {
                OriginalLineSpacings[textId] = targetText.lineSpacing;
            }

            if (targetText.font != replacementFont)
            {
                targetText.font = replacementFont;
            }

            targetText.lineSpacing = OriginalLineSpacings[textId] * rule.LineSpacingScale;
            return true;
        }

        internal static void TryApplyToAllLoadedTexts(string source)
        {
            try
            {
                int replacedCount = 0;
                Text[] texts = Resources.FindObjectsOfTypeAll<Text>();
                foreach (Text targetText in texts)
                {
                    if (TryApplyToText(targetText, source))
                    {
                        replacedCount++;
                    }
                }

                if (replacedCount > 0)
                {
                    LOG.Msg($"{source} 时替换了 {replacedCount} 个文本组件字体。");
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"{source} 时批量替换字体失败：{ex.Message}");
            }
        }

        private static bool TryGetRule(string currentFontName, out FontReplacementRule rule)
        {
            string normalizedFontName = NormalizeFontName(currentFontName);
            return ReplacementRules.TryGetValue(normalizedFontName, out rule);
        }

        private static Font ResolveFont(FontReplacementRule rule)
        {
            string cacheKey = $"{rule.SourceKind}:{rule.SourcePath}:{rule.AssetName}";
            if (CachedFonts.TryGetValue(cacheKey, out Font cachedFont) && cachedFont != null)
            {
                return cachedFont;
            }

            Font loadedFont = rule.SourceKind switch
            {
                FontSourceKind.Bundle => LoadFontFromBundle(rule),
                FontSourceKind.Os => LoadFontFromSystem(rule),
                _ => null
            };

            if (loadedFont != null)
            {
                CachedFonts[cacheKey] = loadedFont;
            }

            return loadedFont;
        }

        private static Font LoadFontFromBundle(FontReplacementRule rule)
        {
            string bundlePath = Path.Combine(ConfigDir, rule.SourcePath);
            if (!File.Exists(bundlePath))
            {
                WarnOnce($"未找到字体 AssetBundle：{bundlePath}");
                return null;
            }

            if (!CachedBundles.TryGetValue(bundlePath, out AssetBundle bundle) || bundle == null)
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    WarnOnce($"加载字体 AssetBundle 失败：{bundlePath}");
                    return null;
                }

                CachedBundles[bundlePath] = bundle;
            }

            if (string.IsNullOrWhiteSpace(rule.AssetName))
            {
                WarnOnce($"Bundle 规则缺少字体资源名：{rule.OriginalFontName}");
                return null;
            }

            Font font = bundle.LoadAsset<Font>(rule.AssetName);
            if (font == null)
            {
                WarnOnce($"Bundle 中未找到字体资源：{rule.AssetName}");
            }

            return font;
        }

        private static Font LoadFontFromSystem(FontReplacementRule rule)
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(rule.SourcePath, 16);
            }
            catch (Exception ex)
            {
                WarnOnce($"加载系统字体失败：{rule.SourcePath}，{ex.Message}");
                return null;
            }
        }

        private static bool TryParseSourceKind(string sourceKindText, out FontSourceKind sourceKind)
        {
            if (string.Equals(sourceKindText, "bundle", StringComparison.OrdinalIgnoreCase))
            {
                sourceKind = FontSourceKind.Bundle;
                return true;
            }

            if (string.Equals(sourceKindText, "os", StringComparison.OrdinalIgnoreCase))
            {
                sourceKind = FontSourceKind.Os;
                return true;
            }

            sourceKind = FontSourceKind.Bundle;
            return false;
        }

        private static string NormalizeFontName(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
            {
                return string.Empty;
            }

            string normalized = fontName.Trim();
            string extension = Path.GetExtension(normalized);
            if (string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase))
            {
                normalized = Path.GetFileNameWithoutExtension(normalized);
            }

            return normalized;
        }

        private static void WarnOnce(string message)
        {
            if (WarnedMessages.Add(message))
            {
                LOG.Warning(message);
            }
        }
    }

    [HarmonyPatch]
    internal static class LTLocalizationPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LTLocalization), "CheckTextFont")]
        private static bool CheckTextFontPrefix(Text targetText)
        {
            return !Main.TryApplyToText(targetText, "LTLocalization.CheckTextFont");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "AddText")]
        private static void AddTextPostfix(Text targetText)
        {
            Main.TryApplyToText(targetText, "LTLocalization.AddText");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "Init")]
        private static void InitPostfix()
        {
            Main.TryApplyToAllLoadedTexts("LTLocalization.Init");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "ManualSetLanguage")]
        private static void ManualSetLanguagePostfix()
        {
            Main.TryApplyToAllLoadedTexts("LTLocalization.ManualSetLanguage");
        }
    }
}
