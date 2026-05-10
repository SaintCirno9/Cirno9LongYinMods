using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinCustomSpeHeroData.Main), "LongYinCustomSpeHeroData", "2.3.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomSpeHeroData
{
    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(
            false,
            string.Empty,
            0,
            string.Empty);

        public LoadedConfigData(
            bool valid,
            string csvText,
            int rowCount,
            string cacheSignature)
        {
            Valid = valid;
            CsvText = csvText ?? string.Empty;
            RowCount = rowCount;
            CacheSignature = cacheSignature ?? string.Empty;
        }

        public bool Valid { get; }

        public string CsvText { get; }

        public int RowCount { get; }

        public string CacheSignature { get; }
    }

    internal sealed class MergeResult
    {
        public static readonly MergeResult NoChange = new(false, null, 0, 0);

        public MergeResult(bool changed, string mergedText, int replacedCount, int appendedCount)
        {
            Changed = changed;
            MergedText = mergedText;
            ReplacedCount = replacedCount;
            AppendedCount = appendedCount;
        }

        public bool Changed { get; }

        public string MergedText { get; }

        public int ReplacedCount { get; }

        public int AppendedCount { get; }
    }

    public class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomSpeHeroData");
        internal static readonly string DataDir = Path.Combine(ConfigDir, "Data");
        internal static readonly string PortraitsDir = Path.Combine(ConfigDir, "Portraits");
        internal static readonly string ModConfigPath = Path.Combine(ConfigDir, "LongYinCustomSpeHeroData.cfg");
        internal static readonly string LegacySpeHeroConfigPath = Path.Combine(ConfigDir, "SpeHeroData.csv");
        internal static readonly string LegacySpeHeroFaceConfigPath = Path.Combine(ConfigDir, "SpeHeroFaceData.csv");
        internal static readonly string VanillaSpeHeroExportPath = Path.Combine(ConfigDir, "Vanilla_SpeHeroData.csv");
        internal static readonly string VanillaSpeHeroFaceExportPath = Path.Combine(ConfigDir, "Vanilla_SpeHeroFaceData.csv");
        internal static readonly string SamplePackDir = Path.Combine(DataDir, "000");
        internal static readonly string SamplePackPortraitsDir = Path.Combine(SamplePackDir, "Portraits");
        internal static readonly string SamplePackSpeHeroConfigPath = Path.Combine(SamplePackDir, "SpeHeroData.csv");
        internal static readonly string SamplePackSpeHeroFaceConfigPath = Path.Combine(SamplePackDir, "SpeHeroFaceData.csv");
        internal static readonly string LegacyPortraitLayoutConfigPath = Path.Combine(ConfigDir, "PortraitLayout.csv");
        internal static readonly string SamplePackPortraitLayoutConfigPath = Path.Combine(SamplePackDir, "PortraitLayout.csv");

        private const string SpeHeroFileName = "SpeHeroData.csv";
        private const string SpeHeroFaceFileName = "SpeHeroFaceData.csv";
        private const string PortraitLayoutFileName = "PortraitLayout.csv";
        private const string SpeHeroResourcePath = "GameData/SpeHeroData";
        private const string SpeHeroFaceResourcePath = "GameData/SpeHeroFaceData";
        private const string RecruitAllSpeHeroesConfigKey = "EnableRecruitAllSpeHeroes";
        private const string VerboseLogConfigKey = "EnableVerboseLog";

        private const string SpeHeroHeader = "id,名字,性别,门派,武学流派,等级,实力等级,性格,资质,年龄,立场,擅长武学,擅长技艺,绝学,标签,绰号,隐藏,出师角色,关系设定,皮肤ID,特殊skeleton";
        private const string SpeHeroFaceHeader = "id,名字,脸,眼,眉,发,嘴,鼻,发后,男胡,杂";
        private const string LegacyPortraitLayoutHeader = "id,通用缩放,通用偏移X,通用偏移Y,详情缩放,详情偏移X,详情偏移Y,卡片缩放,卡片偏移X,卡片偏移Y,战斗卡片缩放,战斗卡片偏移X,战斗卡片偏移Y,战斗立绘缩放,战斗立绘偏移X,战斗立绘偏移Y";
        private const string PortraitLayoutHeader = "id,名字,通用缩放,通用偏移X,通用偏移Y,详情缩放,详情偏移X,详情偏移Y,卡片缩放,卡片偏移X,卡片偏移Y,战斗卡片缩放,战斗卡片偏移X,战斗卡片偏移Y,战斗立绘缩放,战斗立绘偏移X,战斗立绘偏移Y";
        private const string DefaultModConfigText =
            "# 是否允许所有特殊人物可招募\r\n" +
            "# true=允许，false=保持游戏原版逻辑\r\n" +
            "EnableRecruitAllSpeHeroes=true\r\n" +
            "\r\n" +
            "# 是否输出详细调试日志\r\n" +
            "# true=输出立绘缓存命中、重载和路径信息，false=只输出关键日志\r\n" +
            "EnableVerboseLog=false\r\n";

        private static readonly string DefaultSpeHeroCsv =
            SpeHeroHeader + "\r\n" +
            "171,姜.映潭,男,仙霞派,默认,副掌门,-1,稳重,聪颖,38,平常/善良,内功/剑法,医术/学识,无影乱剑,领袖;御剑;内家;正直;仁慈,,0,0,朋友:2;亲属:7;仇人:65,-1,0\r\n";

        private static readonly string DefaultSpeHeroFaceCsv =
            SpeHeroFaceHeader + "\r\n" +
            "171,姜.映潭,1,10,10,10,10,10,10,0,-1\r\n";

        private static readonly string DefaultPortraitLayoutCsv =
            PortraitLayoutHeader + "\r\n" +
            "171,姜.映潭,1.00,20,-120,1.00,0,0,1.00,0,0,1.00,0,0,1.00,0,0\r\n";

        private static LoadedConfigData _cachedSpeHeroConfig = LoadedConfigData.Empty;
        private static LoadedConfigData _cachedSpeHeroFaceConfig = LoadedConfigData.Empty;
        private static bool _cachedEnableRecruitAllSpeHeroes = true;
        private static bool _cachedEnableVerboseLog;
        private static DateTime _cachedModConfigWriteTimeUtc = DateTime.MinValue;
        private static bool _exportedVanillaSpeHeroData;
        private static bool _exportedVanillaSpeHeroFaceData;
        private static int? _cachedVanillaMaxSpeHeroId;
        private static DateTime _cachedVanillaSpeHeroWriteTimeUtc = DateTime.MinValue;
        private static string _cachedPortraitPathSignature = string.Empty;
        private static Dictionary<int, string> _cachedPortraitPathsBySourceHeroId = new();
        private static LoadedPortraitLayoutData _cachedPortraitLayoutData = LoadedPortraitLayoutData.Empty;
        private static readonly Dictionary<int, int> SourceHeroIdToRuntimeHeroId = new();
        private static readonly Dictionary<int, int> RuntimeHeroIdToSourceHeroId = new();
        private static readonly Dictionary<int, PortraitSpriteCacheEntry> CachedPortraitSpritesByHeroId = new();
        private static readonly HashSet<string> PortraitWarnings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<int> PortraitAppliedLogHeroIds = new();
        private static readonly HashSet<string> CsvWriteWarnings = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> SpouseRelationWarnings = new(StringComparer.Ordinal);
        private static bool _legacyStructureWarningShown;

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            LOG.Msg($"LongYinCustomSpeHeroData 已加载，配置目录：{ConfigDir}");
        }

        internal static void EnsureConfigExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                Directory.CreateDirectory(DataDir);

                if (!File.Exists(ModConfigPath))
                {
                    File.WriteAllText(ModConfigPath, DefaultModConfigText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置：{ModConfigPath}");
                }
                else
                {
                    EnsureModConfigKeysExist();
                }

                bool hasLegacyConfig =
                    File.Exists(LegacySpeHeroConfigPath) ||
                    File.Exists(LegacySpeHeroFaceConfigPath) ||
                    Directory.Exists(PortraitsDir);

                if (hasLegacyConfig)
                {
                    Directory.CreateDirectory(SamplePackDir);
                    Directory.CreateDirectory(SamplePackPortraitsDir);

                    if (!_legacyStructureWarningShown)
                    {
                        _legacyStructureWarningShown = true;
                        LOG.Warning(
                            "检测到旧版根目录配置结构，当前版本仍会兼容读取。新版建议路径：" +
                            " CSV -> UserData\\LongYinCustomSpeHeroData\\Data\\000\\SpeHeroData.csv / SpeHeroFaceData.csv；" +
                            " 图片 -> UserData\\LongYinCustomSpeHeroData\\Data\\000\\Portraits");
                    }
                }
                else if (!Directory.Exists(SamplePackDir) && !HasAnyDataPackConfig())
                {
                    Directory.CreateDirectory(SamplePackDir);
                    Directory.CreateDirectory(SamplePackPortraitsDir);
                    File.WriteAllText(SamplePackSpeHeroConfigPath, DefaultSpeHeroCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    File.WriteAllText(SamplePackSpeHeroFaceConfigPath, DefaultSpeHeroFaceCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    File.WriteAllText(SamplePackPortraitLayoutConfigPath, DefaultPortraitLayoutCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"未检测到任何特殊人物配置，已自动生成示例配置：{SamplePackSpeHeroConfigPath}");
                    LOG.Msg($"已自动生成示例配置：{SamplePackSpeHeroFaceConfigPath}");
                    LOG.Msg($"已自动生成示例配置：{SamplePackPortraitLayoutConfigPath}");
                    LOG.Msg("已同时创建示例立绘目录：UserData\\LongYinCustomSpeHeroData\\Data\\000\\Portraits");
                }

                EnsureExistingCsvHeaders();
            }
            catch (Exception ex)
            {
                LOG.Error($"准备配置文件失败：{ex}");
            }
        }

        private static bool HasAnyDataPackConfig()
        {
            if (!Directory.Exists(DataDir))
            {
                return false;
            }

            foreach (string dir in Directory.GetDirectories(DataDir))
            {
                string dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(dir, SpeHeroFileName)) || File.Exists(Path.Combine(dir, SpeHeroFaceFileName)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureExistingCsvHeaders()
        {
            if (File.Exists(LegacySpeHeroConfigPath))
            {
                EnsureCsvHeaderExists(LegacySpeHeroConfigPath, SpeHeroHeader);
            }

            if (File.Exists(LegacySpeHeroFaceConfigPath))
            {
                EnsureCsvHeaderExists(LegacySpeHeroFaceConfigPath, SpeHeroFaceHeader);
            }

            if (File.Exists(LegacyPortraitLayoutConfigPath))
            {
                EnsurePortraitLayoutHeaderExists(LegacyPortraitLayoutConfigPath);
            }

            if (!Directory.Exists(DataDir))
            {
                return;
            }

            foreach (string dir in Directory.GetDirectories(DataDir))
            {
                string dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                string speHeroPath = Path.Combine(dir, SpeHeroFileName);
                if (File.Exists(speHeroPath))
                {
                    EnsureCsvHeaderExists(speHeroPath, SpeHeroHeader);
                }

                string speHeroFacePath = Path.Combine(dir, SpeHeroFaceFileName);
                if (File.Exists(speHeroFacePath))
                {
                    EnsureCsvHeaderExists(speHeroFacePath, SpeHeroFaceHeader);
                }

                string portraitLayoutPath = Path.Combine(dir, PortraitLayoutFileName);
                if (File.Exists(portraitLayoutPath))
                {
                    EnsurePortraitLayoutHeaderExists(portraitLayoutPath);
                }
            }
        }

        private static void EnsurePortraitLayoutHeaderExists(string path)
        {
            try
            {
                string content = ReadAllTextShared(path);
                string normalized = content.TrimStart('\uFEFF', '\r', '\n');
                string[] lines = normalized.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
                {
                    if (TryWriteAllTextUtf8(path, PortraitLayoutHeader + "\r\n" + normalized))
                    {
                        LOG.Warning($"检测到 CSV 表头缺失，已自动修复：{path}");
                    }

                    return;
                }

                string[] sourceHeaders = SplitCsvLine(lines[0]);
                for (int i = 0; i < sourceHeaders.Length; i++)
                {
                    sourceHeaders[i] = sourceHeaders[i].Trim('\uFEFF', ' ', '\t');
                }

                if (FindColumnIndex(sourceHeaders, "id") < 0)
                {
                    if (TryWriteAllTextUtf8(path, PortraitLayoutHeader + "\r\n" + normalized))
                    {
                        LOG.Warning($"检测到 CSV 表头缺失，已自动修复：{path}");
                    }

                    return;
                }

                string trimmedHeader = lines[0].Trim('\uFEFF');
                if (string.Equals(trimmedHeader, PortraitLayoutHeader, StringComparison.Ordinal))
                {
                    return;
                }

                string[] canonicalHeaders = SplitCsvLine(PortraitLayoutHeader);
                var outputLines = new List<string> { PortraitLayoutHeader };
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    {
                        outputLines.Add(line);
                        continue;
                    }

                    string[] sourceCells = SplitCsvLine(line);
                    string[] normalizedCells = new string[canonicalHeaders.Length];
                    for (int j = 0; j < canonicalHeaders.Length; j++)
                    {
                        int sourceIndex = FindColumnIndex(sourceHeaders, canonicalHeaders[j]);
                        normalizedCells[j] = sourceIndex >= 0
                            ? GetCell(sourceCells, sourceIndex)
                            : GetDefaultPortraitLayoutCell(canonicalHeaders[j]);
                    }

                    outputLines.Add(BuildCsvLine(normalizedCells));
                }

                if (TryWriteAllTextUtf8(path, string.Join("\r\n", outputLines) + "\r\n"))
                {
                    LOG.Warning($"已同步立绘布局 CSV 表头顺序：{path}");
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"自动修复立绘布局 CSV 表头失败：{path}，{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static string GetDefaultPortraitLayoutCell(string header)
        {
            if (header.EndsWith("缩放", StringComparison.Ordinal))
            {
                return "1.00";
            }

            if (header.Contains("偏移", StringComparison.Ordinal))
            {
                return "0";
            }

            return string.Empty;
        }

        internal static void EnsureCsvHeaderExists(string path, string header)
        {
            try
            {
                string content = ReadAllTextShared(path);
                string normalized = content.TrimStart('\uFEFF', '\r', '\n');
                if (normalized.StartsWith(header, StringComparison.Ordinal))
                {
                    return;
                }

                string fixedContent = header + "\r\n" + normalized;
                if (TryWriteAllTextUtf8(path, fixedContent))
                {
                    LOG.Warning($"检测到 CSV 表头缺失，已自动修复：{path}");
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"自动修复 CSV 表头失败：{path}，{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static bool IsRecruitAllSpeHeroesEnabled()
        {
            LoadModConfigIfNeeded();
            return _cachedEnableRecruitAllSpeHeroes;
        }

        internal static bool IsVerboseLogEnabled()
        {
            LoadModConfigIfNeeded();
            return _cachedEnableVerboseLog;
        }

        private static void LogVerbose(string message)
        {
            if (IsVerboseLogEnabled())
            {
                LOG.Msg(message);
            }
        }

        private static void LoadModConfigIfNeeded()
        {
            EnsureConfigExists();

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(ModConfigPath);
                if (_cachedModConfigWriteTimeUtc == lastWriteTimeUtc)
                {
                    return;
                }

                _cachedModConfigWriteTimeUtc = lastWriteTimeUtc;
                _cachedEnableRecruitAllSpeHeroes = true;
                _cachedEnableVerboseLog = false;

                foreach (string rawLine in File.ReadAllLines(ModConfigPath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();
                    if (string.Equals(key, RecruitAllSpeHeroesConfigKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedEnableRecruitAllSpeHeroes =
                            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.Equals(key, VerboseLogConfigKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedEnableVerboseLog =
                            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"读取配置文件 LongYinCustomSpeHeroData.cfg 失败：{ex.GetType().Name} - {ex.Message}");
                _cachedEnableRecruitAllSpeHeroes = true;
                _cachedEnableVerboseLog = false;
            }
        }

        private static void EnsureModConfigKeysExist()
        {
            try
            {
                string content = ReadAllTextShared(ModConfigPath);
                bool hasRecruitAllSpeHeroes = HasConfigKey(content, RecruitAllSpeHeroesConfigKey);
                bool hasVerboseLog = HasConfigKey(content, VerboseLogConfigKey);
                if (hasRecruitAllSpeHeroes && hasVerboseLog)
                {
                    return;
                }

                var builder = new StringBuilder(content.TrimEnd('\r', '\n'));
                if (builder.Length > 0)
                {
                    builder.Append("\r\n");
                }

                if (!hasRecruitAllSpeHeroes)
                {
                    builder.Append("\r\n");
                    builder.Append("# 是否允许所有特殊人物可招募\r\n");
                    builder.Append("# true=允许，false=保持游戏原版逻辑\r\n");
                    builder.Append("EnableRecruitAllSpeHeroes=true\r\n");
                }

                if (!hasVerboseLog)
                {
                    builder.Append("\r\n");
                    builder.Append("# 是否输出详细调试日志\r\n");
                    builder.Append("# true=输出立绘缓存命中、重载和路径信息，false=只输出关键日志\r\n");
                    builder.Append("EnableVerboseLog=false\r\n");
                }

                if (TryWriteAllTextUtf8(ModConfigPath, builder.ToString()))
                {
                    LOG.Msg($"已补全配置项：{ModConfigPath}");
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"补全配置文件 LongYinCustomSpeHeroData.cfg 失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static bool HasConfigKey(string content, string configKey)
        {
            foreach (string rawLine in content.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line[..separatorIndex].Trim();
                if (string.Equals(key, configKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldPatchSpeHero(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(path.Trim(), SpeHeroResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldPatchSpeHeroFace(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(path.Trim(), SpeHeroFaceResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ExportVanillaCsvIfNeeded(string exportPath, TextAsset textAsset, ref bool exportedFlag)
        {
            if (exportedFlag || textAsset == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigDir);
                byte[] bytes = textAsset.bytes;
                if (bytes != null && bytes.Length > 0)
                {
                    File.WriteAllBytes(exportPath, bytes);
                }
                else if (!string.IsNullOrWhiteSpace(textAsset.text))
                {
                    File.WriteAllText(exportPath, textAsset.text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                }
                else
                {
                    return;
                }

                exportedFlag = true;
                LOG.Msg($"已导出原版表：{exportPath}");
            }
            catch (Exception ex)
            {
                LOG.Warning($"导出原版表失败：{exportPath}，{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static TextAsset CreateTextAsset(string text, string name = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            IntPtr ptr = IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<TextAsset>.NativeClassPtr);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            var asset = new TextAsset(ptr);
            TextAsset.Internal_CreateInstance(asset, text);
            if (!string.IsNullOrWhiteSpace(name))
            {
                asset.name = name;
            }

            return asset;
        }

        internal static LoadedConfigData LoadSpeHeroConfig()
        {
            return LoadConfig(
                SpeHeroFileName,
                SpeHeroHeader,
                new[] { "id", "名字", "性别" },
                ref _cachedSpeHeroConfig);
        }

        internal static LoadedConfigData LoadSpeHeroFaceConfig()
        {
            return LoadConfig(
                SpeHeroFaceFileName,
                SpeHeroFaceHeader,
                new[] { "id", "名字" },
                ref _cachedSpeHeroFaceConfig);
        }

        internal static LoadedPortraitLayoutData LoadPortraitLayoutConfig()
        {
            EnsureConfigExists();

            try
            {
                List<ConfigSource> sources = CollectConfigSources();
                string cacheSignature = $"{BuildConfigCacheSignature(sources, PortraitLayoutFileName)}|{BuildConfigCacheSignature(sources, SpeHeroFileName)}";
                if (string.Equals(_cachedPortraitLayoutData.CacheSignature, cacheSignature, StringComparison.Ordinal))
                {
                    return _cachedPortraitLayoutData;
                }

                Dictionary<int, string> speHeroNamesById = BuildSpeHeroNamesById();
                var layoutsByHeroId = new Dictionary<int, PortraitLayoutConfig>();
                var layoutHeroIdsByName = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (ConfigSource source in sources)
                {
                    string path = source.GetCsvPath(PortraitLayoutFileName);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string[] lines = ReadAllLinesShared(path);
                    if (lines.Length <= 1)
                    {
                        continue;
                    }

                    string[] headers = SplitCsvLine(lines[0]);
                    for (int i = 0; i < headers.Length; i++)
                    {
                        headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                    }

                    if (!ValidateHeaders(headers, new[] { "id" }, path))
                    {
                        continue;
                    }

                    int idColumnIndex = FindColumnIndex(headers, "id");
                    var seenIdsInSource = new HashSet<int>();
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        {
                            continue;
                        }

                        string[] cells = SplitCsvLine(line);
                        string idText = GetCell(cells, idColumnIndex);
                        if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                        {
                            LOG.Warning($"[{source.SourceName}] 立绘布局第 {i + 1} 行 id 非法，已跳过：{line}");
                            continue;
                        }

                        if (!seenIdsInSource.Add(heroId))
                        {
                            LOG.Warning($"[{source.SourceName}] 立绘布局第 {i + 1} 行特殊人物 id={heroId} 重复，已跳过。");
                            continue;
                        }

                        string heroName = GetCell(cells, FindColumnIndex(headers, "名字"));
                        if (string.IsNullOrWhiteSpace(heroName) && speHeroNamesById.TryGetValue(heroId, out string speHeroName))
                        {
                            heroName = speHeroName;
                        }

                        layoutsByHeroId[heroId] = BuildPortraitLayoutConfig(headers, cells, source.CreatePortraitOrigin(path), heroName);
                        if (!string.IsNullOrWhiteSpace(heroName))
                        {
                            layoutHeroIdsByName[NormalizeHeroName(heroName)] = heroId;
                        }
                    }
                }

                _cachedPortraitLayoutData = new LoadedPortraitLayoutData(layoutsByHeroId, layoutHeroIdsByName, cacheSignature);
                return _cachedPortraitLayoutData;
            }
            catch (Exception ex)
            {
                LOG.Error($"读取立绘布局配置失败：{PortraitLayoutFileName}，{ex}");
                return LoadedPortraitLayoutData.Empty;
            }
        }

        private static Dictionary<int, string> BuildSpeHeroNamesById()
        {
            var namesById = new Dictionary<int, string>();
            LoadedConfigData speHeroConfig = LoadSpeHeroConfig();
            if (!speHeroConfig.Valid || string.IsNullOrWhiteSpace(speHeroConfig.CsvText))
            {
                return namesById;
            }

            string[] lines = speHeroConfig.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= 1)
            {
                return namesById;
            }

            string[] headers = SplitCsvLine(lines[0]);
            int idColumnIndex = FindColumnIndex(headers, "id");
            int nameColumnIndex = FindColumnIndex(headers, "名字");
            if (idColumnIndex < 0 || nameColumnIndex < 0)
            {
                return namesById;
            }

            foreach (string rawLine in lines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (int.TryParse(GetCell(cells, idColumnIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                {
                    string heroName = GetCell(cells, nameColumnIndex);
                    if (!string.IsNullOrWhiteSpace(heroName))
                    {
                        namesById[heroId] = heroName;
                    }
                }
            }

            return namesById;
        }

        internal static bool TryGetPortraitLayoutConfig(int heroId, out PortraitLayoutConfig config)
        {
            LoadedPortraitLayoutData data = LoadPortraitLayoutConfig();
            return data.LayoutsByHeroId.TryGetValue(heroId, out config);
        }

        internal static bool TryGetPortraitLayoutConfig(HeroData heroData, out PortraitLayoutConfig config)
        {
            return TryResolvePortraitLayout(heroData, out _, out config);
        }

        internal static bool TryResolvePortraitLayout(HeroData heroData, out int sourceHeroId, out PortraitLayoutConfig config)
        {
            sourceHeroId = -1;
            config = null;
            if (heroData == null)
            {
                return false;
            }

            LoadedPortraitLayoutData data = LoadPortraitLayoutConfig();
            if (RuntimeHeroIdToSourceHeroId.TryGetValue(heroData.heroID, out sourceHeroId) &&
                data.LayoutsByHeroId.TryGetValue(sourceHeroId, out config))
            {
                return true;
            }

            if (data.LayoutsByHeroId.TryGetValue(heroData.heroID, out config))
            {
                if (string.IsNullOrWhiteSpace(config.HeroName) ||
                    string.Equals(NormalizeHeroName(config.HeroName), NormalizeHeroName(heroData.heroName), StringComparison.Ordinal))
                {
                    sourceHeroId = heroData.heroID;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(heroData.heroName) &&
                IsRuntimeAddedSpeHeroId(heroData.heroID) &&
                data.LayoutHeroIdsByName.TryGetValue(NormalizeHeroName(heroData.heroName), out sourceHeroId) &&
                data.LayoutsByHeroId.TryGetValue(sourceHeroId, out config))
            {
                return true;
            }

            sourceHeroId = -1;
            return false;
        }

        private static bool IsRuntimeAddedSpeHeroId(int heroId)
        {
            int vanillaMaxSpeHeroId = GetVanillaMaxSpeHeroId();
            return vanillaMaxSpeHeroId >= 0 && heroId > vanillaMaxSpeHeroId;
        }

        private static int GetVanillaMaxSpeHeroId()
        {
            DateTime writeTimeUtc = File.Exists(VanillaSpeHeroExportPath)
                ? File.GetLastWriteTimeUtc(VanillaSpeHeroExportPath)
                : DateTime.MinValue;
            if (_cachedVanillaMaxSpeHeroId.HasValue && _cachedVanillaSpeHeroWriteTimeUtc == writeTimeUtc)
            {
                return _cachedVanillaMaxSpeHeroId.Value;
            }

            _cachedVanillaSpeHeroWriteTimeUtc = writeTimeUtc;
            _cachedVanillaMaxSpeHeroId = ReadMaxSpeHeroId(VanillaSpeHeroExportPath);
            return _cachedVanillaMaxSpeHeroId.Value;
        }

        private static int ReadMaxSpeHeroId(string path)
        {
            if (!File.Exists(path))
            {
                return -1;
            }

            try
            {
                string[] lines = ReadAllLinesShared(path);
                if (lines.Length <= 1)
                {
                    return -1;
                }

                string[] headers = SplitCsvLine(lines[0]);
                int idColumnIndex = FindColumnIndex(headers, "id");
                if (idColumnIndex < 0)
                {
                    return -1;
                }

                int maxId = -1;
                foreach (string rawLine in lines.Skip(1))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    if (int.TryParse(GetCell(cells, idColumnIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                    {
                        maxId = Math.Max(maxId, heroId);
                    }
                }

                return maxId;
            }
            catch (Exception ex)
            {
                LOG.Warning($"读取原版特殊人物最大 id 失败：{path}，{ex.GetType().Name} - {ex.Message}");
                return -1;
            }
        }

        internal static List<ConfigSource> CollectConfigSources()
        {
            var sources = new List<ConfigSource>();

            if (File.Exists(LegacySpeHeroConfigPath) || File.Exists(LegacySpeHeroFaceConfigPath) || File.Exists(LegacyPortraitLayoutConfigPath) || Directory.Exists(PortraitsDir))
            {
                sources.Add(new ConfigSource(
                    "根目录",
                    ConfigDir,
                    Path.Combine("UserData", "LongYinCustomSpeHeroData"),
                    0));
            }

            if (!Directory.Exists(DataDir))
            {
                return sources;
            }

            foreach (string dir in Directory.GetDirectories(DataDir)
                         .OrderBy(path => int.TryParse(Path.GetFileName(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) ? order : int.MaxValue)
                         .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
                {
                    continue;
                }

                bool hasSupportedCsv =
                    File.Exists(Path.Combine(dir, SpeHeroFileName)) ||
                    File.Exists(Path.Combine(dir, SpeHeroFaceFileName)) ||
                    File.Exists(Path.Combine(dir, PortraitLayoutFileName));
                if (!hasSupportedCsv)
                {
                    continue;
                }

                sources.Add(new ConfigSource(
                    $"Data/{dirName}",
                    dir,
                    Path.Combine("UserData", "LongYinCustomSpeHeroData", "Data", dirName),
                    order));
            }

            return sources;
        }

        private static string BuildConfigCacheSignature(IEnumerable<ConfigSource> sources, string fileName)
        {
            if (sources == null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                sources
                    .Select(source => source.GetCsvPath(fileName))
                    .Where(File.Exists)
                    .Select(path =>
                    {
                        var fileInfo = new FileInfo(path);
                        return $"{path}:{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
                    }));
        }

        private static LoadedConfigData LoadConfig(
            string fileName,
            string canonicalHeaderLine,
            string[] requiredHeaders,
            ref LoadedConfigData cachedConfig)
        {
            EnsureConfigExists();

            try
            {
                List<ConfigSource> sources = CollectConfigSources();
                string cacheSignature = BuildConfigCacheSignature(sources, fileName);
                if (cachedConfig.Valid &&
                    string.Equals(cachedConfig.CacheSignature, cacheSignature, StringComparison.Ordinal))
                {
                    return cachedConfig;
                }

                string[] canonicalHeaders = SplitCsvLine(canonicalHeaderLine);
                var rowsById = new Dictionary<int, string>();
                foreach (ConfigSource source in sources)
                {
                    string path = source.GetCsvPath(fileName);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string[] lines = ReadAllLinesShared(path);
                    if (lines.Length <= 1)
                    {
                        LOG.Warning($"配置文件为空，已跳过：{path}");
                        continue;
                    }

                    string[] headers = SplitCsvLine(lines[0]);
                    for (int i = 0; i < headers.Length; i++)
                    {
                        headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                    }

                    if (!ValidateHeaders(headers, requiredHeaders, path))
                    {
                        continue;
                    }

                    int idColumnIndex = FindColumnIndex(headers, "id");
                    var seenIdsInSource = new HashSet<int>();

                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        {
                            continue;
                        }

                        string[] cells = SplitCsvLine(line);
                        string idText = GetCell(cells, idColumnIndex);
                        if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                        {
                            LOG.Warning($"[{source.SourceName}] 第 {i + 1} 行 id 非法，已跳过：{line}");
                            continue;
                        }

                        if (!seenIdsInSource.Add(heroId))
                        {
                            LOG.Warning($"[{source.SourceName}] 第 {i + 1} 行特殊人物 id={heroId} 重复，已跳过。");
                            continue;
                        }

                        rowsById[heroId] = NormalizeCsvRow(canonicalHeaders, headers, cells);
                    }
                }

                if (rowsById.Count == 0)
                {
                    cachedConfig = new LoadedConfigData(
                        false,
                        string.Empty,
                        0,
                        cacheSignature);
                    return cachedConfig;
                }

                var outputLines = new List<string> { canonicalHeaderLine };
                outputLines.AddRange(rowsById.OrderBy(pair => pair.Key).Select(pair => pair.Value));
                cachedConfig = new LoadedConfigData(
                    true,
                    string.Join("\r\n", outputLines) + "\r\n",
                    rowsById.Count,
                    cacheSignature);
                return cachedConfig;
            }
            catch (Exception ex)
            {
                LOG.Error($"读取配置文件失败：{fileName}，{ex}");
                return LoadedConfigData.Empty;
            }
        }

        private static PortraitLayoutConfig BuildPortraitLayoutConfig(string[] headers, string[] cells, PortraitConfigOrigin origin, string heroName)
        {
            return new PortraitLayoutConfig(
                heroName,
                BuildPortraitSceneLayoutOverride(headers, cells, "详情"),
                BuildPortraitSceneLayoutOverride(headers, cells, "卡片"),
                BuildPortraitSceneLayoutOverride(headers, cells, "战斗卡片"),
                BuildPortraitSceneLayoutOverride(headers, cells, "战斗立绘"),
                BuildPortraitSceneLayoutOverride(headers, cells, "通用"),
                origin);
        }

        private static PortraitSceneLayoutOverride BuildPortraitSceneLayoutOverride(string[] headers, string[] cells, string prefix)
        {
            return new PortraitSceneLayoutOverride(
                ParseOptionalFloat(GetCell(cells, FindColumnIndex(headers, $"{prefix}缩放"))),
                ParseOptionalFloat(GetCell(cells, FindColumnIndex(headers, $"{prefix}偏移X"))),
                ParseOptionalFloat(GetCell(cells, FindColumnIndex(headers, $"{prefix}偏移Y"))));
        }

        private static float? ParseOptionalFloat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : null;
        }

        internal static MergeResult BuildMergedCsvText(string vanillaText, LoadedConfigData configData, string tableName)
        {
            if (string.Equals(tableName, "SpeHeroData", StringComparison.Ordinal))
            {
                return BuildMergedSpeHeroCsvText(vanillaText, configData, tableName);
            }

            if (string.Equals(tableName, "SpeHeroFaceData", StringComparison.Ordinal))
            {
                return BuildMergedSpeHeroFaceCsvText(vanillaText, configData, tableName);
            }

            if (string.IsNullOrWhiteSpace(vanillaText) || !configData.Valid || configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.CsvText))
            {
                return MergeResult.NoChange;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length == 0 || customLines.Length <= 1)
            {
                return MergeResult.NoChange;
            }

            string[] vanillaHeaders = SplitCsvLine(vanillaLines[0]);
            string[] customHeaders = SplitCsvLine(customLines[0]);
            int vanillaIdCol = FindColumnIndex(vanillaHeaders, "id");
            int customIdCol = FindColumnIndex(customHeaders, "id");
            if (vanillaIdCol < 0 || customIdCol < 0)
            {
                return MergeResult.NoChange;
            }

            var customById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string rawLine in customLines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, customIdCol);
                if (string.IsNullOrWhiteSpace(id))
                {
                    LOG.Warning($"{tableName} 检测到空 id 行，已跳过：{line}");
                    continue;
                }

                customById[id] = line;
            }

            if (customById.Count == 0)
            {
                return MergeResult.NoChange;
            }

            var outputLines = new List<string> { vanillaLines[0] };
            var matchedIds = new HashSet<string>(StringComparer.Ordinal);
            int replacedCount = 0;
            int appendedCount = 0;

            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, vanillaIdCol);
                if (!string.IsNullOrWhiteSpace(id) && customById.TryGetValue(id, out string replacementLine))
                {
                    outputLines.Add(replacementLine);
                    matchedIds.Add(id);
                    replacedCount++;
                    continue;
                }

                outputLines.Add(line);
            }

            foreach (string id in customById.Keys
                .Where(id => !matchedIds.Contains(id))
                .OrderBy(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId) ? parsedId : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal))
            {
                outputLines.Add(customById[id]);
                appendedCount++;
            }

            string mergedText = string.Join("\r\n", outputLines);
            bool changed = !string.Equals(mergedText, vanillaText.Replace("\uFEFF", ""), StringComparison.Ordinal);
            return changed
                ? new MergeResult(true, mergedText, replacedCount, appendedCount)
                : MergeResult.NoChange;
        }

        private static MergeResult BuildMergedSpeHeroCsvText(string vanillaText, LoadedConfigData configData, string tableName)
        {
            if (string.IsNullOrWhiteSpace(vanillaText) || !configData.Valid || configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.CsvText))
            {
                ResetRuntimeHeroIdMap();
                return MergeResult.NoChange;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length == 0 || customLines.Length <= 1)
            {
                ResetRuntimeHeroIdMap();
                return MergeResult.NoChange;
            }

            string[] vanillaHeaders = SplitCsvLine(vanillaLines[0]);
            string[] customHeaders = SplitCsvLine(customLines[0]);
            int vanillaIdCol = FindColumnIndex(vanillaHeaders, "id");
            int customIdCol = FindColumnIndex(customHeaders, "id");
            int customRelationCol = FindColumnIndex(customHeaders, "关系设定");
            if (vanillaIdCol < 0 || customIdCol < 0)
            {
                ResetRuntimeHeroIdMap();
                return MergeResult.NoChange;
            }

            int vanillaMaxId = -1;
            foreach (string rawLine in vanillaLines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (int.TryParse(GetCell(cells, vanillaIdCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVanillaId))
                {
                    vanillaMaxId = Math.Max(vanillaMaxId, parsedVanillaId);
                }
            }

            var overrideById = new Dictionary<string, string>(StringComparer.Ordinal);
            var appendedRows = new List<(int SourceHeroId, string Line)>();
            foreach (string rawLine in customLines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, customIdCol);
                if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceHeroId))
                {
                    LOG.Warning($"{tableName} 检测到非法 id 行，已跳过：{line}");
                    continue;
                }

                StripCustomSpouseRelation(cells, customRelationCol);
                line = BuildCsvLine(cells);
                if (sourceHeroId <= vanillaMaxId)
                {
                    overrideById[id] = line;
                }
                else
                {
                    appendedRows.Add((sourceHeroId, line));
                }
            }

            if (overrideById.Count == 0 && appendedRows.Count == 0)
            {
                ResetRuntimeHeroIdMap();
                return MergeResult.NoChange;
            }

            var outputLines = new List<string> { vanillaLines[0] };
            var matchedIds = new HashSet<string>(StringComparer.Ordinal);
            int replacedCount = 0;
            int appendedCount = 0;

            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, vanillaIdCol);
                if (!string.IsNullOrWhiteSpace(id) && overrideById.TryGetValue(id, out string replacementLine))
                {
                    outputLines.Add(replacementLine);
                    matchedIds.Add(id);
                    replacedCount++;
                    continue;
                }

                outputLines.Add(line);
            }

            ResetRuntimeHeroIdMap();
            int nextRuntimeHeroId = vanillaMaxId + 1;
            foreach ((int sourceHeroId, string line) in appendedRows
                         .OrderBy(row => row.SourceHeroId))
            {
                string[] cells = SplitCsvLine(line);
                if (vanillaIdCol >= cells.Length)
                {
                    continue;
                }

                int runtimeHeroId = nextRuntimeHeroId++;
                cells[vanillaIdCol] = runtimeHeroId.ToString(CultureInfo.InvariantCulture);
                StripCustomSpouseRelation(cells, customRelationCol);
                outputLines.Add(BuildCsvLine(cells));
                RegisterRuntimeHeroId(sourceHeroId, runtimeHeroId);
                appendedCount++;
            }

            string mergedText = string.Join("\r\n", outputLines);
            bool changed = !string.Equals(mergedText, vanillaText.Replace("\uFEFF", ""), StringComparison.Ordinal);
            return changed
                ? new MergeResult(true, mergedText, replacedCount, appendedCount)
                : MergeResult.NoChange;
        }

        private static void StripCustomSpouseRelation(string[] cells, int relationColumnIndex)
        {
            if (cells == null || relationColumnIndex < 0 || relationColumnIndex >= cells.Length)
            {
                return;
            }

            string relationText = cells[relationColumnIndex];
            if (string.IsNullOrWhiteSpace(relationText) || !relationText.Contains("夫妻", StringComparison.Ordinal))
            {
                return;
            }

            string[] keptRelations = relationText
                .Split(';')
                .Where(part => !IsCustomSpouseRelationPart(part))
                .ToArray();
            cells[relationColumnIndex] = string.Join(";", keptRelations);
        }

        private static bool IsCustomSpouseRelationPart(string relationPart)
        {
            string[] relationCells = (relationPart ?? string.Empty).Split(':');
            return relationCells.Length > 0 &&
                   string.Equals(relationCells[0].Trim(), "夫妻", StringComparison.Ordinal);
        }

        private static MergeResult BuildMergedSpeHeroFaceCsvText(string vanillaText, LoadedConfigData configData, string tableName)
        {
            if (string.IsNullOrWhiteSpace(vanillaText) || !configData.Valid || configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.CsvText))
            {
                return MergeResult.NoChange;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length == 0 || customLines.Length <= 1)
            {
                return MergeResult.NoChange;
            }

            string[] vanillaHeaders = SplitCsvLine(vanillaLines[0]);
            string[] customHeaders = SplitCsvLine(customLines[0]);
            int vanillaIdCol = FindColumnIndex(vanillaHeaders, "id");
            int customIdCol = FindColumnIndex(customHeaders, "id");
            if (vanillaIdCol < 0 || customIdCol < 0)
            {
                return MergeResult.NoChange;
            }

            EnsureRuntimeHeroIdMap(GetMaxCsvId(vanillaLines, vanillaIdCol));

            var customById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string rawLine in customLines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, customIdCol);
                if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceHeroId))
                {
                    LOG.Warning($"{tableName} 检测到非法 id 行，已跳过：{line}");
                    continue;
                }

                if (SourceHeroIdToRuntimeHeroId.TryGetValue(sourceHeroId, out int runtimeHeroId))
                {
                    cells[customIdCol] = runtimeHeroId.ToString(CultureInfo.InvariantCulture);
                    id = cells[customIdCol];
                    line = BuildCsvLine(cells);
                }

                customById[id] = line;
            }

            if (customById.Count == 0)
            {
                return MergeResult.NoChange;
            }

            var outputLines = new List<string> { vanillaLines[0] };
            var matchedIds = new HashSet<string>(StringComparer.Ordinal);
            int replacedCount = 0;
            int appendedCount = 0;

            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, vanillaIdCol);
                if (!string.IsNullOrWhiteSpace(id) && customById.TryGetValue(id, out string replacementLine))
                {
                    outputLines.Add(replacementLine);
                    matchedIds.Add(id);
                    replacedCount++;
                    continue;
                }

                outputLines.Add(line);
            }

            foreach (string id in customById.Keys
                .Where(id => !matchedIds.Contains(id))
                .OrderBy(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId) ? parsedId : int.MaxValue)
                .ThenBy(id => id, StringComparer.Ordinal))
            {
                outputLines.Add(customById[id]);
                appendedCount++;
            }

            string mergedText = string.Join("\r\n", outputLines);
            bool changed = !string.Equals(mergedText, vanillaText.Replace("\uFEFF", ""), StringComparison.Ordinal);
            return changed
                ? new MergeResult(true, mergedText, replacedCount, appendedCount)
                : MergeResult.NoChange;
        }

        private static void ResetRuntimeHeroIdMap()
        {
            SourceHeroIdToRuntimeHeroId.Clear();
            RuntimeHeroIdToSourceHeroId.Clear();
        }

        private static void RegisterRuntimeHeroId(int sourceHeroId, int runtimeHeroId)
        {
            SourceHeroIdToRuntimeHeroId[sourceHeroId] = runtimeHeroId;
            RuntimeHeroIdToSourceHeroId[runtimeHeroId] = sourceHeroId;
        }

        private static void EnsureRuntimeHeroIdMap(int fallbackVanillaMaxSpeHeroId)
        {
            if (SourceHeroIdToRuntimeHeroId.Count > 0 || RuntimeHeroIdToSourceHeroId.Count > 0)
            {
                return;
            }

            int vanillaMaxSpeHeroId = GetVanillaMaxSpeHeroId();
            if (vanillaMaxSpeHeroId < 0)
            {
                vanillaMaxSpeHeroId = fallbackVanillaMaxSpeHeroId;
            }

            if (vanillaMaxSpeHeroId < 0)
            {
                return;
            }

            LoadedConfigData speHeroConfig = LoadSpeHeroConfig();
            if (!speHeroConfig.Valid || string.IsNullOrWhiteSpace(speHeroConfig.CsvText))
            {
                return;
            }

            string[] lines = speHeroConfig.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= 1)
            {
                return;
            }

            string[] headers = SplitCsvLine(lines[0]);
            int idColumnIndex = FindColumnIndex(headers, "id");
            if (idColumnIndex < 0)
            {
                return;
            }

            int nextRuntimeHeroId = vanillaMaxSpeHeroId + 1;
            foreach (string rawLine in lines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (int.TryParse(GetCell(cells, idColumnIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceHeroId) &&
                    sourceHeroId > vanillaMaxSpeHeroId)
                {
                    RegisterRuntimeHeroId(sourceHeroId, nextRuntimeHeroId);
                    nextRuntimeHeroId++;
                }
            }
        }

        private static int GetMaxCsvId(string[] lines, int idColumnIndex)
        {
            if (lines == null || idColumnIndex < 0)
            {
                return -1;
            }

            int maxId = -1;
            foreach (string rawLine in lines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (int.TryParse(GetCell(cells, idColumnIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId))
                {
                    maxId = Math.Max(maxId, parsedId);
                }
            }

            return maxId;
        }

        internal static void ApplyCustomSpouseRelations(GameDataController gameData)
        {
            if (gameData?.SpeHeroDataBase == null)
            {
                return;
            }

            LoadedConfigData speHeroConfig = LoadSpeHeroConfig();
            if (!speHeroConfig.Valid || string.IsNullOrWhiteSpace(speHeroConfig.CsvText))
            {
                return;
            }

            string[] lines = speHeroConfig.CsvText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= 1)
            {
                return;
            }

            string[] headers = SplitCsvLine(lines[0]);
            int idColumnIndex = FindColumnIndex(headers, "id");
            int relationColumnIndex = FindColumnIndex(headers, "关系设定");
            if (idColumnIndex < 0 || relationColumnIndex < 0)
            {
                return;
            }

            Dictionary<int, HeroData> speHeroesById = BuildSpeHeroesById(gameData);
            var spouseByHeroId = new Dictionary<int, int>();
            foreach (string rawLine in lines.Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (!int.TryParse(GetCell(cells, idColumnIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceHeroId))
                {
                    continue;
                }

                int runtimeHeroId = GetRuntimeHeroId(sourceHeroId);
                if (!speHeroesById.ContainsKey(runtimeHeroId))
                {
                    WarnSpouseRelationOnce($"夫妻关系设定未找到来源角色：sourceID={sourceHeroId}, runtimeID={runtimeHeroId}");
                    continue;
                }

                foreach (int targetSourceHeroId in ParseSpouseTargetIds(GetCell(cells, relationColumnIndex)))
                {
                    int targetRuntimeHeroId = GetRuntimeHeroId(targetSourceHeroId);
                    if (targetRuntimeHeroId == runtimeHeroId)
                    {
                        WarnSpouseRelationOnce($"夫妻关系设定不能指向自身：sourceID={sourceHeroId}");
                        continue;
                    }

                    if (!speHeroesById.TryGetValue(targetRuntimeHeroId, out HeroData targetHero))
                    {
                        WarnSpouseRelationOnce($"夫妻关系设定未找到目标角色：sourceID={sourceHeroId}, targetSourceID={targetSourceHeroId}, targetRuntimeID={targetRuntimeHeroId}");
                        continue;
                    }

                    TryRegisterSpouseRelation(spouseByHeroId, runtimeHeroId, targetRuntimeHeroId, sourceHeroId, targetSourceHeroId);
                }
            }

            int appliedCount = 0;
            foreach (var pair in spouseByHeroId)
            {
                if (pair.Key > pair.Value)
                {
                    continue;
                }

                SetBaseSpouseRelation(speHeroesById[pair.Key], speHeroesById[pair.Value]);
                appliedCount++;
            }

            if (appliedCount > 0)
            {
                LOG.Msg($"已应用自定义夫妻关系设定 {appliedCount} 条。");
            }
        }

        private static Dictionary<int, HeroData> BuildSpeHeroesById(GameDataController gameData)
        {
            var heroesById = new Dictionary<int, HeroData>();
            for (int i = 0; i < gameData.SpeHeroDataBase.Count; i++)
            {
                HeroData heroData = gameData.SpeHeroDataBase[i];
                if (heroData != null)
                {
                    heroesById[heroData.heroID] = heroData;
                }
            }

            return heroesById;
        }

        private static int GetRuntimeHeroId(int sourceHeroId)
        {
            return SourceHeroIdToRuntimeHeroId.TryGetValue(sourceHeroId, out int runtimeHeroId)
                ? runtimeHeroId
                : sourceHeroId;
        }

        private static void TryRegisterSpouseRelation(Dictionary<int, int> spouseByHeroId, int runtimeHeroId, int targetRuntimeHeroId, int sourceHeroId, int targetSourceHeroId)
        {
            if (spouseByHeroId.TryGetValue(runtimeHeroId, out int existingTargetHeroId) && existingTargetHeroId != targetRuntimeHeroId)
            {
                WarnSpouseRelationOnce($"夫妻关系设定冲突，已跳过：sourceID={sourceHeroId}, targetSourceID={targetSourceHeroId}");
                return;
            }

            if (spouseByHeroId.TryGetValue(targetRuntimeHeroId, out int existingSourceHeroId) && existingSourceHeroId != runtimeHeroId)
            {
                WarnSpouseRelationOnce($"夫妻关系设定冲突，已跳过：sourceID={sourceHeroId}, targetSourceID={targetSourceHeroId}");
                return;
            }

            spouseByHeroId[runtimeHeroId] = targetRuntimeHeroId;
            spouseByHeroId[targetRuntimeHeroId] = runtimeHeroId;
        }

        private static IEnumerable<int> ParseSpouseTargetIds(string relationText)
        {
            if (string.IsNullOrWhiteSpace(relationText))
            {
                yield break;
            }

            foreach (string relationPart in relationText.Split(';'))
            {
                string[] relationCells = relationPart.Split(':');
                if (!IsCustomSpouseRelationPart(relationPart) || relationCells.Length != 2)
                {
                    continue;
                }

                foreach (string targetIdText in relationCells[1].Split('/'))
                {
                    if (int.TryParse(targetIdText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetHeroId))
                    {
                        yield return targetHeroId;
                    }
                }
            }
        }

        private static void SetBaseSpouseRelation(HeroData sourceHero, HeroData targetHero)
        {
            sourceHero.Lover = targetHero.heroID;
            targetHero.Lover = sourceHero.heroID;
            sourceHero.Friends?.Remove(targetHero.heroID);
            targetHero.Friends?.Remove(sourceHero.heroID);
            sourceHero.PreLovers?.Remove(targetHero.heroID);
            targetHero.PreLovers?.Remove(sourceHero.heroID);
        }

        private static void WarnSpouseRelationOnce(string message)
        {
            if (SpouseRelationWarnings.Add(message))
            {
                LOG.Warning(message);
            }
        }

        private static string NormalizeCsvRow(string[] canonicalHeaders, string[] sourceHeaders, string[] sourceCells)
        {
            string[] normalizedCells = new string[canonicalHeaders.Length];
            for (int i = 0; i < canonicalHeaders.Length; i++)
            {
                int sourceIndex = FindColumnIndex(sourceHeaders, canonicalHeaders[i]);
                normalizedCells[i] = GetCell(sourceCells, sourceIndex);
            }

            return BuildCsvLine(normalizedCells);
        }

        internal static string BuildCsvLine(string[] cells)
        {
            return string.Join(",", cells.Select(EscapeCsvCell));
        }

        private static string EscapeCsvCell(string value)
        {
            string cell = value ?? string.Empty;
            if (cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return $"\"{cell.Replace("\"", "\"\"")}\"";
            }

            return cell;
        }

        internal static string[] SplitCsvLine(string line)
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

        internal static int FindColumnIndex(string[] headers, string columnName)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i].Trim('\uFEFF', ' ', '\t'), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        internal static string GetCell(string[] cells, int index)
        {
            return index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        internal static string NormalizeHeroName(string value)
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

        internal static ref bool GetSpeHeroExportedFlagRef()
        {
            return ref _exportedVanillaSpeHeroData;
        }

        internal static ref bool GetSpeHeroFaceExportedFlagRef()
        {
            return ref _exportedVanillaSpeHeroFaceData;
        }

        internal static bool ShouldForceRecruitForHero(HeroData targetHero)
        {
            return targetHero != null &&
                   IsRecruitAllSpeHeroesEnabled() &&
                   targetHero.speHero &&
                   !targetHero.recruitAble;
        }

        private static void EnsurePortraitPathsCurrent()
        {
            LoadedPortraitLayoutData portraitLayoutConfig = LoadPortraitLayoutConfig();
            string pathSignature = portraitLayoutConfig.CacheSignature;
            if (string.Equals(pathSignature, _cachedPortraitPathSignature, StringComparison.Ordinal))
            {
                LogVerbose($"立绘路径缓存命中：signature={pathSignature}, 布局数={portraitLayoutConfig.LayoutsByHeroId.Count}, 图片路径数={_cachedPortraitPathsBySourceHeroId.Count}, Sprite缓存数={CachedPortraitSpritesByHeroId.Count}");
                return;
            }

            LogVerbose($"立绘路径缓存失效，准备重建：oldSignature={_cachedPortraitPathSignature}, newSignature={pathSignature}, 旧Sprite缓存数={CachedPortraitSpritesByHeroId.Count}");
            ResetPortraitSpriteCache();
            PortraitWarnings.Clear();
            PortraitAppliedLogHeroIds.Clear();

            var portraitPaths = new Dictionary<int, string>();
            foreach (var pair in portraitLayoutConfig.LayoutsByHeroId.OrderBy(pair => pair.Key))
            {
                string foundPath = FindExistingPortraitPath(pair.Key, pair.Value.Origin);
                if (!string.IsNullOrWhiteSpace(foundPath))
                {
                    portraitPaths[pair.Key] = foundPath;
                }
            }

            _cachedPortraitPathsBySourceHeroId = portraitPaths;
            _cachedPortraitPathSignature = pathSignature;
            LogVerbose($"立绘路径缓存已重建：布局数={portraitLayoutConfig.LayoutsByHeroId.Count}, 图片路径数={portraitPaths.Count}");
        }

        internal static bool TryLoadCustomPortraitSprite(HeroData heroData, out Sprite sprite)
        {
            sprite = null;
            EnsurePortraitPathsCurrent();
            if (heroData == null || heroData.heroID < 0 || !TryResolvePortraitLayout(heroData, out int sourceHeroId, out PortraitLayoutConfig layoutConfig))
            {
                LogVerbose($"立绘缓存跳过：未匹配布局，runtimeID={heroData?.heroID ?? -1}, heroName={heroData?.heroName ?? string.Empty}");
                return false;
            }

            PortraitConfigOrigin origin = layoutConfig.Origin;
            if (!_cachedPortraitPathsBySourceHeroId.TryGetValue(sourceHeroId, out string foundImagePath) || string.IsNullOrWhiteSpace(foundImagePath))
            {
                LogVerbose($"立绘缓存跳过：未找到图片路径，runtimeID={heroData.heroID}, sourceID={sourceHeroId}, heroName={heroData.heroName}");
                RemovePortraitSpriteCache(sourceHeroId);
                return false;
            }

            string displayPath = GetDisplayPortraitPath(sourceHeroId, origin, foundImagePath);
            if (string.IsNullOrWhiteSpace(displayPath))
            {
                displayPath = foundImagePath;
            }

            DateTime writeTimeUtc = File.GetLastWriteTimeUtc(foundImagePath);
            if (CachedPortraitSpritesByHeroId.TryGetValue(sourceHeroId, out PortraitSpriteCacheEntry cachedEntry) &&
                cachedEntry.Sprite != null &&
                string.Equals(cachedEntry.ImagePath, foundImagePath, StringComparison.OrdinalIgnoreCase) &&
                cachedEntry.WriteTimeUtc == writeTimeUtc)
            {
                LogVerbose($"立绘 Sprite 缓存命中：runtimeID={heroData.heroID}, sourceID={sourceHeroId}, path={displayPath}, Sprite缓存数={CachedPortraitSpritesByHeroId.Count}");
                sprite = cachedEntry.Sprite;
                return true;
            }

            LogVerbose($"立绘 Sprite 缓存未命中，准备读取图片：runtimeID={heroData.heroID}, sourceID={sourceHeroId}, path={displayPath}, writeTimeUtc={writeTimeUtc:O}");
            RemovePortraitSpriteCache(sourceHeroId);

            try
            {
                byte[] imageBytes = File.ReadAllBytes(foundImagePath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, imageBytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    WarnPortraitIssueOnce($"读取 NPC 自定义立绘失败：runtimeID={heroData.heroID}, sourceID={sourceHeroId}, path={displayPath}");
                    return false;
                }

                texture.name = $"LongYinCustomSpeHeroPortrait_{sourceHeroId}";
                texture.wrapMode = TextureWrapMode.Clamp;
                var rect = new Rect(0f, 0f, texture.width, texture.height);
                Sprite loadedSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                loadedSprite.name = texture.name;
                CachedPortraitSpritesByHeroId[sourceHeroId] = new PortraitSpriteCacheEntry(foundImagePath, writeTimeUtc, texture, loadedSprite);
                LogVerbose($"立绘 Sprite 已载入缓存：runtimeID={heroData.heroID}, sourceID={sourceHeroId}, size={texture.width}x{texture.height}, Sprite缓存数={CachedPortraitSpritesByHeroId.Count}");
                sprite = loadedSprite;
                return true;
            }
            catch (Exception ex)
            {
                WarnPortraitIssueOnce($"加载 NPC 自定义立绘异常：runtimeID={heroData.heroID}, sourceID={sourceHeroId}, path={displayPath}，{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        private static string FindExistingPortraitPath(int heroId, PortraitConfigOrigin origin)
        {
            foreach ((string FullPath, string DisplayPath) candidate in GetCandidatePortraitPaths(heroId, origin))
            {
                if (File.Exists(candidate.FullPath))
                {
                    return candidate.FullPath;
                }
            }

            return string.Empty;
        }

        private static string GetDisplayPortraitPath(int heroId, PortraitConfigOrigin origin, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return string.Empty;
            }

            foreach ((string FullPath, string DisplayPath) candidate in GetCandidatePortraitPaths(heroId, origin))
            {
                if (string.Equals(candidate.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.DisplayPath;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<(string FullPath, string DisplayPath)> GetCandidatePortraitPaths(int heroId, PortraitConfigOrigin origin)
        {
            string heroIdText = heroId.ToString(CultureInfo.InvariantCulture);
            string[] fileNames =
            {
                $"{heroIdText}.png",
                $"{heroIdText}.jpg",
                $"{heroIdText}.jpeg"
            };

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (origin != null && !string.IsNullOrWhiteSpace(origin.PortraitDir))
            {
                foreach (string fileName in fileNames)
                {
                    string fullPath = Path.Combine(origin.PortraitDir, fileName);
                    if (yielded.Add(fullPath))
                    {
                        string displayPath = string.IsNullOrWhiteSpace(origin.RelativePortraitDir)
                            ? fullPath
                            : Path.Combine(origin.RelativePortraitDir, fileName);
                        yield return (fullPath, displayPath);
                    }
                }
            }

            foreach (string fileName in fileNames)
            {
                string rootPath = Path.Combine(PortraitsDir, fileName);
                if (yielded.Add(rootPath))
                {
                    yield return (rootPath, Path.Combine("UserData", "LongYinCustomSpeHeroData", "Portraits", fileName));
                }
            }
        }

        private static void WarnPortraitIssueOnce(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && PortraitWarnings.Add(message))
            {
                LOG.Warning(message);
            }
        }

        private static void ResetPortraitSpriteCache()
        {
            foreach (int heroId in CachedPortraitSpritesByHeroId.Keys.ToList())
            {
                RemovePortraitSpriteCache(heroId);
            }
        }

        private static void RemovePortraitSpriteCache(int heroId)
        {
            if (!CachedPortraitSpritesByHeroId.TryGetValue(heroId, out PortraitSpriteCacheEntry cachedEntry))
            {
                return;
            }

            if (cachedEntry.Sprite != null)
            {
                UnityEngine.Object.Destroy(cachedEntry.Sprite);
            }

            if (cachedEntry.Texture != null)
            {
                UnityEngine.Object.Destroy(cachedEntry.Texture);
            }

            CachedPortraitSpritesByHeroId.Remove(heroId);
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

        internal static void LogPortraitAppliedOnce(HeroData heroData, Transform targetSkeletonParent)
        {
            if (heroData == null)
            {
                return;
            }

            if (PortraitAppliedLogHeroIds.Add(heroData.heroID))
            {
                LogVerbose($"已应用 NPC 自定义立绘：heroID={heroData.heroID}, heroName={heroData.heroName}, {DescribeTransformContext(targetSkeletonParent)}");
            }
        }

        private static bool ValidateHeaders(string[] headers, string[] requiredHeaders, string path)
        {
            bool valid = true;
            foreach (string requiredHeader in requiredHeaders)
            {
                if (headers.Any(header => string.Equals(header, requiredHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                LOG.Error($"配置文件缺少必要列：{requiredHeader}，文件：{path}");
                valid = false;
            }

            return valid;
        }

        private static string[] ReadAllLinesShared(string path, int maxAttempts = 5)
        {
            return ReadAllTextShared(path, maxAttempts).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        private static string ReadAllTextShared(string path, int maxAttempts = 5)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    byte[] bytes;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }

                    try
                    {
                        Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                        return strictUtf8.GetString(bytes).Replace("\uFEFF", "");
                    }
                    catch (DecoderFallbackException)
                    {
                        LOG.Warning($"[{Path.GetFileName(path)}] 不符合标准 UTF-8 编码，已启动 GBK 容错解析。");
                        return Encoding.GetEncoding("gbk").GetString(bytes);
                    }
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
            }

            using var finalStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(finalStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static bool TryWriteAllTextUtf8(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (CsvWriteWarnings.Add(path))
                {
                    LOG.Warning($"CSV 文件当前可能被 Excel 占用，已跳过自动写回：{path}，{ex.GetType().Name} - {ex.Message}");
                }

                return false;
            }
        }
    }

    [HarmonyPatch(typeof(GameDataController), nameof(GameDataController.LoadAllGameData))]
    public static class GameDataControllerLoadAllGameDataPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameDataController __instance)
        {
            Main.ApplyCustomSpouseRelations(__instance);
        }
    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Il2CppSystem.Type) })]
    public static class ResourcesLoadPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            bool isSpeHero = Main.ShouldPatchSpeHero(path);
            bool isSpeHeroFace = Main.ShouldPatchSpeHeroFace(path);
            if (!isSpeHero && !isSpeHeroFace)
            {
                return;
            }

            try
            {
                TextAsset textAsset = __result.TryCast<TextAsset>();
                if (textAsset == null)
                {
                    return;
                }

                if (isSpeHero)
                {
                    Main.ExportVanillaCsvIfNeeded(Main.VanillaSpeHeroExportPath, textAsset, ref Main.GetSpeHeroExportedFlagRef());
                    LoadedConfigData configData = Main.LoadSpeHeroConfig();
                    MergeResult mergeResult = Main.BuildMergedCsvText(textAsset.text, configData, "SpeHeroData");
                    if (!mergeResult.Changed)
                    {
                        return;
                    }

                    TextAsset replacement = Main.CreateTextAsset(mergeResult.MergedText, textAsset.name);
                    if (replacement != null)
                    {
                        __result = replacement;
                        Main.LOG.Msg($"已注入 GameData/SpeHeroData：替换 {mergeResult.ReplacedCount} 条，新增 {mergeResult.AppendedCount} 条。");
                    }

                    return;
                }

                Main.ExportVanillaCsvIfNeeded(Main.VanillaSpeHeroFaceExportPath, textAsset, ref Main.GetSpeHeroFaceExportedFlagRef());
                LoadedConfigData faceConfigData = Main.LoadSpeHeroFaceConfig();
                MergeResult faceMergeResult = Main.BuildMergedCsvText(textAsset.text, faceConfigData, "SpeHeroFaceData");
                if (!faceMergeResult.Changed)
                {
                    return;
                }

                TextAsset faceReplacement = Main.CreateTextAsset(faceMergeResult.MergedText, textAsset.name);
                if (faceReplacement != null)
                {
                    __result = faceReplacement;
                    Main.LOG.Msg($"已注入 GameData/SpeHeroFaceData：替换 {faceMergeResult.ReplacedCount} 条，新增 {faceMergeResult.AppendedCount} 条。");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"处理资源失败：path={path}，{ex.GetType().Name} - {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.SetSkeletonGraphic), new[] { typeof(Transform), typeof(int), typeof(int) })]
    internal static class HeroDataSetSkeletonGraphicPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(HeroData __instance, Transform targetSkeletonParent)
        {
            CustomNpcPortraitFlow.HideNpcStaticPortraitOnly(targetSkeletonParent);

            if (__instance == null || __instance.heroID == 0 || __instance.isSummon)
            {
                return true;
            }

            if (CustomNpcPortraitFlow.TryApplyCustomPortrait(__instance, targetSkeletonParent))
            {
                return false;
            }

            CustomNpcPortraitFlow.RestoreOriginalDisplay(targetSkeletonParent);
            return true;
        }
    }

    [HarmonyPatch(typeof(HeroDetailController), nameof(HeroDetailController.FreshNowHeroDetail), new[] { typeof(HeroData), typeof(bool) })]
    public class HeroDetailControllerFreshNowHeroDetailPatch
    {
        [HarmonyPrefix]
        public static void Prefix(HeroData targetHero)
        {
            if (Main.ShouldForceRecruitForHero(targetHero))
            {
                targetHero.recruitAble = true;
            }
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.AskHeroJoinForce), new[] { typeof(string) })]
    public class PlotControllerAskHeroJoinForcePatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlotController __instance)
        {
            HeroData targetHero = __instance?.targetInteractHero;
            if (Main.ShouldForceRecruitForHero(targetHero))
            {
                targetHero.recruitAble = true;
            }
        }
    }
}
