using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using MelonLoader.NativeUtils;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(LongYinFontReplacement.Main), "LongYinFontReplacement", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinFontReplacement
{
    internal sealed class FontReplacementRule
    {
        public FontReplacementRule(string originalFontName, string sourcePath, string assetName, float lineSpacingScale, float fontSizeScale)
        {
            OriginalFontName = originalFontName;
            SourcePath = sourcePath;
            AssetName = assetName;
            LineSpacingScale = lineSpacingScale;
            FontSizeScale = fontSizeScale;
        }

        public string OriginalFontName { get; }

        public string SourcePath { get; }

        public string AssetName { get; }

        public float LineSpacingScale { get; }

        public float FontSizeScale { get; }
    }

    internal sealed class NativeFontMemory
    {
        public NativeFontMemory(IntPtr buffer, int size, string sourcePath, string hash, string[] familyNames)
        {
            Buffer = buffer;
            Size = size;
            SourcePath = sourcePath;
            Hash = hash;
            FamilyNames = familyNames;
        }

        public IntPtr Buffer { get; }

        public int Size { get; }

        public string SourcePath { get; }

        public string Hash { get; }

        public string[] FamilyNames { get; }
    }

    public class Main : MelonMod
    {
        private const float DefaultLineSpacingScale = 1.0f;
        private const long FontLoadDynamicFaceRva = 0xC69560;

        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinFontReplacement");
        internal static readonly string FontDir = Path.Combine(ConfigDir, "Fonts");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "FontMappings.txt");

        internal static FontReplacementRule DefaultRule;
        internal static readonly Dictionary<string, FontReplacementRule> OverrideRules = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, Font> CachedFonts = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, NativeFontMemory> CachedNativeFontMemories = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, string[]> CachedFontNameLists = new(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<int> CachedFontInstanceIds = new();
        internal static readonly List<Font> PreservedRuntimeFonts = new();
        internal static readonly List<NativeFontMemory> PreservedNativeFontMemories = new();
        internal static readonly Dictionary<int, float> OriginalLineSpacings = new();
        internal static readonly Dictionary<int, int> OriginalFontSizes = new();
        internal static readonly HashSet<string> WarnedMessages = new(StringComparer.OrdinalIgnoreCase);

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        private static NativeHook<FontLoadDynamicFaceDelegate> FontLoadDynamicFaceHook;
        private static FontLoadDynamicFaceDelegate FontLoadDynamicFaceDetourDelegate;
        private static string ActiveDelayedScanKey;

        [ThreadStatic]
        private static NativeFontMemory PendingNativeFontMemory;

        [ThreadStatic]
        private static bool ApplyingTextFont;

        public override void OnInitializeMelon()
        {
            EnsureConfigExists();
            ReloadConfig();
            InstallNativeFontMemoryHook();
            HarmonyInstance.PatchAll();
            LOG.Msg($"LongYinFontReplacement 已加载，配置目录：{ConfigDir}");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            TryApplyToAllLoadedTexts($"场景初始化：{sceneName}");
            StartDelayedSceneScans(sceneName);
        }

        internal static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(FontDir);

            if (File.Exists(ConfigPath))
            {
                return;
            }

            string sampleConfig =
                "# 字体替换配置，编码请保持 UTF-8\r\n" +
                "# 全局通用字体：default|字体文件路径|字体族名|行距倍率|字号倍率\r\n" +
                "# 特定字体覆盖：override|原字体名|字体文件路径|字体族名|行距倍率|字号倍率\r\n" +
                "# 字体文件路径相对 UserData\\LongYinFontReplacement，字体族名可留空自动读取。\r\n" +
                "# 行距倍率会乘到原组件行距上：1.00=保持原版，1.20=加大 20%，0.80=压缩 20%。原版不同 UI 可能本来就是 1、0.9、0.8 或 0.75。\r\n" +
                "# 字号倍率会乘到原组件字号上：1.00=保持原版，0.85=缩小 15%。笔画外扩或字面偏大的字体建议 0.80-0.90。\r\n" +
                "# 当前版本先接管 LTLocalization 的 UnityEngine.UI.Text 链路。\r\n" +
                "default|Fonts\\庞门正道细线体.ttf||1.00|1.00\r\n" +
                "# override|字体家AI造字剑客|Fonts\\庞门正道细线体.ttf||1.00|1.00\r\n" +
                "# override|SourceHanSerifCN-Bold|Fonts\\庞门正道细线体.ttf||1.00|1.00\r\n" +
                "# override|LiberationSans|Fonts\\庞门正道细线体.ttf||1.00|1.00\r\n";

            File.WriteAllText(ConfigPath, sampleConfig);
        }

        internal static void ReloadConfig()
        {
            DefaultRule = null;
            OverrideRules.Clear();
            CachedFonts.Clear();
            CachedNativeFontMemories.Clear();
            CachedFontNameLists.Clear();
            CachedFontInstanceIds.Clear();
            WarnedMessages.Clear();

            if (!File.Exists(ConfigPath))
            {
                LOG.Warning($"未找到字体替换配置文件：{ConfigPath}");
                return;
            }

            int overrideCount = 0;
            foreach (string rawLine in File.ReadAllLines(ConfigPath))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split('|');

                string ruleType = parts[0].Trim();
                bool isDefaultRule = string.Equals(ruleType, "default", StringComparison.OrdinalIgnoreCase);
                bool isOverrideRule = string.Equals(ruleType, "override", StringComparison.OrdinalIgnoreCase);
                if (!isDefaultRule && !isOverrideRule)
                {
                    WarnOnce($"未知的配置规则类型：{ruleType}");
                    continue;
                }

                if (isDefaultRule && parts.Length != 5)
                {
                    WarnOnce($"全局字体配置行格式不正确：{line}");
                    continue;
                }

                if (isOverrideRule && parts.Length != 6)
                {
                    WarnOnce($"特定字体覆盖配置行格式不正确：{line}");
                    continue;
                }

                string originalFontName = isDefaultRule ? "default" : NormalizeFontName(parts[1]);
                int sourcePathIndex = isDefaultRule ? 1 : 2;
                string sourcePath = parts[sourcePathIndex].Trim();
                string assetName = parts[sourcePathIndex + 1].Trim();
                string lineSpacingText = parts[sourcePathIndex + 2].Trim();
                string fontSizeText = parts[sourcePathIndex + 3].Trim();

                if ((isOverrideRule && string.IsNullOrWhiteSpace(originalFontName)) ||
                    string.IsNullOrWhiteSpace(sourcePath))
                {
                    WarnOnce($"配置行缺少必要字段：{line}");
                    continue;
                }

                if (!float.TryParse(lineSpacingText, NumberStyles.Float, CultureInfo.InvariantCulture, out float lineSpacingScale))
                {
                    lineSpacingScale = DefaultLineSpacingScale;
                }

                if (!float.TryParse(fontSizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fontSizeScale))
                {
                    fontSizeScale = 1.0f;
                }

                FontReplacementRule rule = new FontReplacementRule(
                    originalFontName,
                    sourcePath,
                    assetName,
                    lineSpacingScale,
                    fontSizeScale);

                if (isDefaultRule)
                {
                    DefaultRule = rule;
                }
                else
                {
                    OverrideRules[originalFontName] = rule;
                    overrideCount++;
                }
            }

            LOG.Msg($"已加载字体替换配置：全局通用字体={(DefaultRule == null ? "无" : "有")}，特定覆盖 {overrideCount} 条。");
        }

        internal static bool TryApplyToText(Text targetText)
        {
            if (ApplyingTextFont)
            {
                return false;
            }

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
                return ApplyDefaultFontToText(targetText);
            }

            if (IsCurrentFontAlreadyReplacement(currentFont))
            {
                return ApplyDefaultStyleToText(targetText, currentFont);
            }

            if (!TryResolveRule(currentFont.name, out FontReplacementRule rule))
            {
                return false;
            }

            try
            {
                ApplyingTextFont = true;

                Font replacementFont = ResolveFont(rule);
                if (replacementFont == null)
                {
                    WarnOnce($"无法加载替换字体：{rule.OriginalFontName}，当前字体={GetUnityFontName(currentFont)}，文件={Path.GetFullPath(Path.Combine(ConfigDir, rule.SourcePath))}");
                    return false;
                }

                if (!CanRenderText(replacementFont, targetText.text, targetText.fontSize, targetText.fontStyle, rule))
                {
                    return false;
                }

                int textId = targetText.GetInstanceID();
                if (!OriginalLineSpacings.ContainsKey(textId))
                {
                    OriginalLineSpacings[textId] = targetText.lineSpacing;
                }

                if (!OriginalFontSizes.ContainsKey(textId))
                {
                    OriginalFontSizes[textId] = targetText.fontSize;
                }

                if (targetText.font != replacementFont)
                {
                    targetText.font = replacementFont;
                }

                targetText.lineSpacing = OriginalLineSpacings[textId] * rule.LineSpacingScale;
                targetText.fontSize = Math.Max(1, (int)Math.Round(OriginalFontSizes[textId] * rule.FontSizeScale));
                targetText.SetAllDirty();
                return true;
            }
            finally
            {
                ApplyingTextFont = false;
            }
        }

        private static bool ApplyDefaultFontToText(Text targetText)
        {
            if (DefaultRule == null)
            {
                return false;
            }

            Font replacementFont = ResolveFont(DefaultRule);
            if (replacementFont == null ||
                !CanRenderText(replacementFont, targetText.text, targetText.fontSize, targetText.fontStyle, DefaultRule))
            {
                return false;
            }

            int textId = targetText.GetInstanceID();
            if (!OriginalLineSpacings.ContainsKey(textId))
            {
                OriginalLineSpacings[textId] = targetText.lineSpacing;
            }

            if (!OriginalFontSizes.ContainsKey(textId))
            {
                OriginalFontSizes[textId] = targetText.fontSize;
            }

            targetText.font = replacementFont;
            targetText.lineSpacing = OriginalLineSpacings[textId] * DefaultRule.LineSpacingScale;
            targetText.fontSize = Math.Max(1, (int)Math.Round(OriginalFontSizes[textId] * DefaultRule.FontSizeScale));
            targetText.SetAllDirty();
            return true;
        }

        private static bool ApplyDefaultStyleToText(Text targetText, Font replacementFont)
        {
            if (DefaultRule == null)
            {
                return false;
            }

            int textId = targetText.GetInstanceID();
            if (!OriginalLineSpacings.ContainsKey(textId))
            {
                OriginalLineSpacings[textId] = targetText.lineSpacing;
            }

            if (!OriginalFontSizes.ContainsKey(textId))
            {
                OriginalFontSizes[textId] = targetText.fontSize;
            }

            int targetFontSize = Math.Max(1, (int)Math.Round(OriginalFontSizes[textId] * DefaultRule.FontSizeScale));
            float targetLineSpacing = OriginalLineSpacings[textId] * DefaultRule.LineSpacingScale;
            if (targetText.fontSize == targetFontSize &&
                Math.Abs(targetText.lineSpacing - targetLineSpacing) < 0.001f)
            {
                return false;
            }

            targetText.font = replacementFont;
            targetText.lineSpacing = targetLineSpacing;
            targetText.fontSize = targetFontSize;
            targetText.SetAllDirty();
            return true;
        }

        private static bool IsCurrentFontAlreadyReplacement(Font font)
        {
            foreach (Font cachedFont in CachedFonts.Values)
            {
                if (ReferenceEquals(cachedFont, font))
                {
                    return true;
                }
            }

            try
            {
                int fontInstanceId = font.GetInstanceID();
                if (!CachedFontInstanceIds.Contains(fontInstanceId))
                {
                    return false;
                }

                if (CanGenerateProbeGlyph(font))
                {
                    return true;
                }

                CachedFontInstanceIds.Remove(fontInstanceId);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanRenderText(Font font, string text, int fontSize, FontStyle fontStyle, FontReplacementRule rule)
        {
            try
            {
                string visibleText = StripRichTextTags(text);
                font.RequestCharactersInTexture(visibleText, fontSize, fontStyle);

                foreach (char character in visibleText)
                {
                    if (char.IsWhiteSpace(character) || char.IsControl(character))
                    {
                        continue;
                    }

                    if (!font.HasCharacter(character))
                    {
                        WarnOnce($"替换字体缺少字符“{character}”，跳过包含该字符的文本：{rule.OriginalFontName}");
                        return false;
                    }

                    CharacterInfo characterInfo;
                    if (!font.GetCharacterInfo(character, out characterInfo, fontSize, fontStyle))
                    {
                        WarnOnce($"替换字体无法生成字符“{character}”的字形，跳过包含该字符的文本：{rule.OriginalFontName}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                WarnOnce($"检查字体字符失败：{rule.OriginalFontName}，{ex.Message}");
                return false;
            }
        }

        internal static void TryApplyToAllLoadedTexts(string source)
        {
            try
            {
                int replacedCount = 0;
                Text[] texts = Resources.FindObjectsOfTypeAll<Text>();
                foreach (Text targetText in texts)
                {
                    if (TryApplyToText(targetText))
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

        internal static void StartDelayedSceneScans(string sceneName)
        {
            if (string.Equals(ActiveDelayedScanKey, sceneName, StringComparison.Ordinal))
            {
                return;
            }

            ActiveDelayedScanKey = sceneName;
            MelonCoroutines.Start(DelayedSceneScans(sceneName));
        }

        private static IEnumerator DelayedSceneScans(string sceneName)
        {
            float[] delays = { 0.5f, 1.0f, 2.0f, 4.0f };
            foreach (float delay in delays)
            {
                yield return new WaitForSeconds(delay);
                TryApplyToAllLoadedTexts($"场景延迟扫描：{sceneName}+{delay.ToString("0.0", CultureInfo.InvariantCulture)}s");
            }

            if (string.Equals(ActiveDelayedScanKey, sceneName, StringComparison.Ordinal))
            {
                ActiveDelayedScanKey = null;
            }
        }

        private static void InstallNativeFontMemoryHook()
        {
            try
            {
                IntPtr unityPlayer = GetModuleHandle("UnityPlayer.dll");
                if (unityPlayer == IntPtr.Zero)
                {
                    WarnOnce("无法安装 TTF 内存字体加载入口：未找到 UnityPlayer.dll 模块。");
                    return;
                }

                IntPtr loadDynamicFaceAddress = IntPtr.Add(unityPlayer, (int)FontLoadDynamicFaceRva);

                FontLoadDynamicFaceDetourDelegate = FontLoadDynamicFaceDetour;

                FontLoadDynamicFaceHook = new NativeHook<FontLoadDynamicFaceDelegate>(
                    loadDynamicFaceAddress,
                    Marshal.GetFunctionPointerForDelegate(FontLoadDynamicFaceDetourDelegate));

                FontLoadDynamicFaceHook.Attach();
                LOG.Msg($"已安装 TTF 内存字体加载入口：LoadDynamicFace=0x{loadDynamicFaceAddress.ToInt64():X}");
            }
            catch (Exception ex)
            {
                WarnOnce($"安装 TTF 内存字体加载入口失败：{ex.Message}");
            }
        }

        private static void FontLoadDynamicFaceDetour(IntPtr fontResource)
        {
            InjectPendingNativeFontMemory(fontResource);
            FontLoadDynamicFaceHook.Trampoline(fontResource);
        }

        private static void InjectPendingNativeFontMemory(IntPtr fontResource)
        {
            if (fontResource == IntPtr.Zero || PendingNativeFontMemory == null)
            {
                return;
            }

            NativeFontMemory memory = PendingNativeFontMemory;
            PendingNativeFontMemory = null;

            try
            {
                // Unity 会在后续原生加载流程读取这两项，日志只保留失败，避免每个字体实例刷屏。
                Marshal.WriteIntPtr(fontResource, 0x80, memory.Buffer);
                Marshal.WriteInt32(fontResource, 0x90, memory.Size);
            }
            catch (Exception ex)
            {
                WarnOnce($"注入 TTF 内存失败：{memory.SourcePath}，{ex.Message}");
            }
        }

        private static bool TryResolveRule(string currentFontName, out FontReplacementRule rule)
        {
            string normalizedFontName = NormalizeFontName(currentFontName);
            if (OverrideRules.TryGetValue(normalizedFontName, out rule))
            {
                return true;
            }

            rule = DefaultRule;
            return rule != null;
        }

        private static Font ResolveFont(FontReplacementRule rule)
        {
            string cacheKey = $"{Path.GetFullPath(Path.Combine(ConfigDir, rule.SourcePath))}:{rule.AssetName}";
            if (CachedFonts.TryGetValue(cacheKey, out Font cachedFont))
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }

                RemoveCachedFont(cacheKey, cachedFont);
            }

            Font loadedFont = LoadFontFromTtf(rule);
            if (loadedFont != null)
            {
                CachedFonts[cacheKey] = loadedFont;
                try
                {
                    CachedFontInstanceIds.Add(loadedFont.GetInstanceID());
                }
                catch
                {
                }
            }

            return loadedFont;
        }

        private static void RemoveCachedFont(string cacheKey, Font font)
        {
            CachedFonts.Remove(cacheKey);
            try
            {
                if (font != null)
                {
                    CachedFontInstanceIds.Remove(font.GetInstanceID());
                }
            }
            catch
            {
            }
        }

        private static Font LoadFontFromTtf(FontReplacementRule rule)
        {
            string fontPath = Path.GetFullPath(Path.Combine(ConfigDir, rule.SourcePath));
            if (!File.Exists(fontPath))
            {
                WarnOnce($"未找到 TTF 字体文件：{fontPath}");
                return null;
            }

            try
            {
                NativeFontMemory memory = ResolveNativeFontMemory(fontPath);
                if (memory == null)
                {
                    return null;
                }

                List<string> fontNames = ResolveFontNames(rule, fontPath, memory);
                return TryCreateDynamicFontFromTtfMemory(fontNames, memory);
            }
            catch (Exception ex)
            {
                WarnOnce($"加载 TTF 字体失败：{fontPath}，{ex.Message}");
                return null;
            }
        }

        private static Font TryCreateDynamicFontFromTtfMemory(List<string> fontNames, NativeFontMemory memory)
        {
            if (fontNames.Count == 0)
            {
                WarnOnce($"无法读取 TTF 字体名称：{memory.SourcePath}");
                return null;
            }

            foreach (string fontName in fontNames)
            {
                Font font = new Font();
                PendingNativeFontMemory = memory;
                try
                {
                    Font.Internal_CreateDynamicFont(font, new Il2CppStringArray(new[] { fontName }), 16);
                }
                finally
                {
                    PendingNativeFontMemory = null;
                }

                PreserveRuntimeFont(font);

                bool probeOk = CanGenerateProbeGlyph(font);
                if (probeOk)
                {
                    LOG.Msg($"已创建内存 TTF 动态字体：请求={fontName}，sha256={memory.Hash}");
                    return font;
                }
            }

            WarnOnce($"内存 TTF 动态字体中文测试字形失败：{memory.SourcePath}");
            return null;
        }

        private static List<string> ResolveFontNames(FontReplacementRule rule, string fontPath, NativeFontMemory memory)
        {
            string cacheKey = $"{fontPath}:{rule.AssetName}";
            if (CachedFontNameLists.TryGetValue(cacheKey, out string[] cachedNames))
            {
                return new List<string>(cachedNames);
            }

            List<string> fontNames = string.IsNullOrWhiteSpace(rule.AssetName)
                ? new List<string>(memory.FamilyNames)
                : new List<string>();
            AddUniqueFontName(fontNames, rule.AssetName);
            AddUniqueFontName(fontNames, Path.GetFileNameWithoutExtension(fontPath));
            CachedFontNameLists[cacheKey] = fontNames.ToArray();
            return fontNames;
        }

        private static NativeFontMemory ResolveNativeFontMemory(string fontPath)
        {
            if (CachedNativeFontMemories.TryGetValue(fontPath, out NativeFontMemory cachedMemory))
            {
                return cachedMemory;
            }

            byte[] data = File.ReadAllBytes(fontPath);
            if (data.Length <= 0)
            {
                WarnOnce($"TTF 字体文件大小异常：{fontPath}");
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, buffer, data.Length);

            string hash = Convert.ToHexString(SHA256.HashData(data));
            string[] familyNames = ReadTtfFamilyNames(data).ToArray();
            NativeFontMemory memory = new NativeFontMemory(buffer, data.Length, fontPath, hash, familyNames);
            CachedNativeFontMemories[fontPath] = memory;
            PreservedNativeFontMemories.Add(memory);
            LOG.Msg($"已读取并缓存 TTF 字体内存：size={memory.Size}，sha256={memory.Hash}，文件={memory.SourcePath}");
            return memory;
        }

        private static void PreserveRuntimeFont(Font font)
        {
            try
            {
                font.hideFlags = HideFlags.DontUnloadUnusedAsset;
                PreservedRuntimeFonts.Add(font);
            }
            catch
            {
            }
        }

        private static List<string> ReadTtfFamilyNames(byte[] data)
        {
            List<string> names = new List<string>();
            if (data.Length < 12)
            {
                return names;
            }

            string signature = Encoding.ASCII.GetString(data, 0, 4);
            if (signature == "ttcf")
            {
                uint fontCount = ReadUInt32BigEndian(data, 8);
                for (int i = 0; i < fontCount; i++)
                {
                    int offsetRecord = 12 + i * 4;
                    if (offsetRecord + 4 > data.Length)
                    {
                        break;
                    }

                    int faceOffset = (int)ReadUInt32BigEndian(data, offsetRecord);
                    ReadTtfFamilyNamesFromFace(data, faceOffset, names);
                }

                return names;
            }

            ReadTtfFamilyNamesFromFace(data, 0, names);
            return names;
        }

        private static void ReadTtfFamilyNamesFromFace(byte[] data, int faceOffset, List<string> names)
        {
            if (faceOffset < 0 || faceOffset + 12 > data.Length)
            {
                return;
            }

            ushort tableCount = ReadUInt16BigEndian(data, faceOffset + 4);
            for (int i = 0; i < tableCount; i++)
            {
                int tableRecordOffset = faceOffset + 12 + i * 16;
                if (tableRecordOffset + 16 > data.Length)
                {
                    return;
                }

                string tag = Encoding.ASCII.GetString(data, tableRecordOffset, 4);
                if (tag != "name")
                {
                    continue;
                }

                int rawNameTableOffset = (int)ReadUInt32BigEndian(data, tableRecordOffset + 8);
                int nameTableOffset = ResolveTtfTableOffset(data, rawNameTableOffset, faceOffset);
                if (nameTableOffset + 6 > data.Length)
                {
                    return;
                }

                ushort nameCount = ReadUInt16BigEndian(data, nameTableOffset + 2);
                ushort stringOffset = ReadUInt16BigEndian(data, nameTableOffset + 4);
                List<string> fallbackNames = new List<string>();

                for (int j = 0; j < nameCount; j++)
                {
                    int nameRecordOffset = nameTableOffset + 6 + j * 12;
                    if (nameRecordOffset + 12 > data.Length)
                    {
                        break;
                    }

                    ushort platformId = ReadUInt16BigEndian(data, nameRecordOffset);
                    ushort encodingId = ReadUInt16BigEndian(data, nameRecordOffset + 2);
                    ushort nameId = ReadUInt16BigEndian(data, nameRecordOffset + 6);
                    ushort length = ReadUInt16BigEndian(data, nameRecordOffset + 8);
                    ushort offset = ReadUInt16BigEndian(data, nameRecordOffset + 10);
                    if (nameId != 1 && nameId != 4 && nameId != 6 && nameId != 16)
                    {
                        continue;
                    }

                    int textOffset = nameTableOffset + stringOffset + offset;
                    if (textOffset + length > data.Length)
                    {
                        continue;
                    }

                    string name = DecodeTtfName(data, textOffset, length, platformId, encodingId);
                    name = name.Trim('\0', ' ', '\r', '\n', '\t');
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (nameId == 16)
                    {
                        AddUniqueFontName(names, name);
                    }
                    else
                    {
                        AddUniqueFontName(fallbackNames, name);
                    }
                }

                foreach (string fallbackName in fallbackNames)
                {
                    AddUniqueFontName(names, fallbackName);
                }

                return;
            }
        }

        private static string DecodeTtfName(byte[] data, int offset, int length, ushort platformId, ushort encodingId)
        {
            if (platformId == 0 || platformId == 3 || (platformId == 2 && encodingId == 1))
            {
                return Encoding.BigEndianUnicode.GetString(data, offset, length);
            }

            return Encoding.UTF8.GetString(data, offset, length);
        }

        private static int ResolveTtfTableOffset(byte[] data, int rawOffset, int faceOffset)
        {
            if (rawOffset >= 0 && rawOffset + 6 <= data.Length)
            {
                return rawOffset;
            }

            int relativeOffset = faceOffset + rawOffset;
            if (relativeOffset >= 0 && relativeOffset + 6 <= data.Length)
            {
                return relativeOffset;
            }

            return data.Length;
        }

        private static void AddUniqueFontName(List<string> names, string fontName)
        {
            string normalizedFontName = NormalizeFontName(fontName);
            if (!IsUsableFontName(normalizedFontName))
            {
                return;
            }

            foreach (string name in names)
            {
                if (string.Equals(name, normalizedFontName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            names.Add(normalizedFontName);
        }

        private static bool IsUsableFontName(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName) || fontName.IndexOf('\uFFFD') >= 0)
            {
                return false;
            }

            foreach (char character in fontName)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanGenerateProbeGlyph(Font font)
        {
            try
            {
                const string probeText = "汉一A";
                font.RequestCharactersInTexture(probeText, 16, FontStyle.Normal);
                foreach (char character in probeText)
                {
                    if (!font.HasCharacter(character))
                    {
                        return false;
                    }

                    CharacterInfo characterInfo;
                    if (!font.GetCharacterInfo(character, out characterInfo, 16, FontStyle.Normal))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetUnityFontName(Font font)
        {
            try
            {
                return font.name;
            }
            catch
            {
                return "<读取失败>";
            }
        }

        private static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
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

        private static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            bool inTag = false;
            foreach (char character in text)
            {
                if (character == '<')
                {
                    inTag = true;
                    continue;
                }

                if (character == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static void WarnOnce(string message)
        {
            if (WarnedMessages.Add(message))
            {
                LOG.Warning(message);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FontLoadDynamicFaceDelegate(IntPtr fontResource);
    }

    [HarmonyPatch]
    internal static class LTLocalizationPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LTLocalization), "CheckTextFont")]
        private static bool CheckTextFontPrefix(Text targetText)
        {
            return !Main.TryApplyToText(targetText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "AddText")]
        private static void AddTextPostfix(Text targetText)
        {
            Main.TryApplyToText(targetText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "Init")]
        private static void InitPostfix()
        {
            Main.TryApplyToAllLoadedTexts("LTLocalization.Init");
            Main.StartDelayedSceneScans("LTLocalization.Init");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LTLocalization), "ManualSetLanguage")]
        private static void ManualSetLanguagePostfix()
        {
            Main.TryApplyToAllLoadedTexts("LTLocalization.ManualSetLanguage");
            Main.StartDelayedSceneScans("LTLocalization.ManualSetLanguage");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Text), "OnEnable")]
        private static void TextOnEnablePostfix(Text __instance)
        {
            Main.TryApplyToText(__instance);
        }
    }
}
