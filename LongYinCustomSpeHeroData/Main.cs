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

[assembly: MelonInfo(typeof(LongYinCustomSpeHeroData.Main), "LongYinCustomSpeHeroData", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomSpeHeroData
{
    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(false, string.Empty, 0, new HashSet<int>());

        public LoadedConfigData(bool valid, string csvText, int rowCount, HashSet<int> heroIds)
        {
            Valid = valid;
            CsvText = csvText;
            RowCount = rowCount;
            HeroIds = heroIds ?? new HashSet<int>();
        }

        public bool Valid { get; }

        public string CsvText { get; }

        public int RowCount { get; }

        public HashSet<int> HeroIds { get; }
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
        internal static readonly string ModConfigPath = Path.Combine(ConfigDir, "LongYinCustomSpeHeroData.cfg");
        internal static readonly string SpeHeroConfigPath = Path.Combine(ConfigDir, "SpeHeroData.csv");
        internal static readonly string SpeHeroFaceConfigPath = Path.Combine(ConfigDir, "SpeHeroFaceData.csv");
        internal static readonly string VanillaSpeHeroExportPath = Path.Combine(ConfigDir, "Vanilla_SpeHeroData.csv");
        internal static readonly string VanillaSpeHeroFaceExportPath = Path.Combine(ConfigDir, "Vanilla_SpeHeroFaceData.csv");

        private const string SpeHeroResourcePath = "GameData/SpeHeroData";
        private const string SpeHeroFaceResourcePath = "GameData/SpeHeroFaceData";
        private const string RecruitAllSpeHeroesConfigKey = "EnableRecruitAllSpeHeroes";

        private const string SpeHeroHeader = "id,名字,性别,门派,武学流派,等级,实力等级,性格,资质,年龄,立场,擅长武学,擅长技艺,绝学,标签,绰号,隐藏,出师角色,关系设定,皮肤ID,特殊skeleton";
        private const string SpeHeroFaceHeader = "id,名字,脸,眼,眉,发,嘴,鼻,发后,男胡,杂";
        private const string DefaultModConfigText =
            "# 是否允许所有特殊人物可招募\r\n" +
            "# true=允许，false=保持游戏原版逻辑\r\n" +
            "EnableRecruitAllSpeHeroes=true\r\n";

        private static readonly string DefaultSpeHeroCsv =
            SpeHeroHeader + "\r\n" +
            "171,姜.映潭,男,仙霞派,默认,副掌门,-1,稳重,聪颖,38,平常/善良,内功/剑法,医术/学识,无影乱剑,领袖;御剑;内家;正直;仁慈,,0,0,朋友:2;亲属:7;仇人:65,-1,0\r\n";

        private static readonly string DefaultSpeHeroFaceCsv =
            SpeHeroFaceHeader + "\r\n" +
            "171,姜.映潭,1,10,10,10,10,10,10,0,-1\r\n";

        private static LoadedConfigData _cachedSpeHeroConfig = LoadedConfigData.Empty;
        private static DateTime _cachedSpeHeroConfigWriteTimeUtc = DateTime.MinValue;
        private static LoadedConfigData _cachedSpeHeroFaceConfig = LoadedConfigData.Empty;
        private static DateTime _cachedSpeHeroFaceConfigWriteTimeUtc = DateTime.MinValue;
        private static bool _cachedEnableRecruitAllSpeHeroes = true;
        private static DateTime _cachedModConfigWriteTimeUtc = DateTime.MinValue;
        private static bool _exportedVanillaSpeHeroData;
        private static bool _exportedVanillaSpeHeroFaceData;

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

                if (!File.Exists(ModConfigPath))
                {
                    File.WriteAllText(ModConfigPath, DefaultModConfigText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置：{ModConfigPath}");
                }

                if (!File.Exists(SpeHeroConfigPath))
                {
                    File.WriteAllText(SpeHeroConfigPath, DefaultSpeHeroCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置：{SpeHeroConfigPath}");
                }
                else
                {
                    EnsureCsvHeaderExists(SpeHeroConfigPath, SpeHeroHeader);
                }

                if (!File.Exists(SpeHeroFaceConfigPath))
                {
                    File.WriteAllText(SpeHeroFaceConfigPath, DefaultSpeHeroFaceCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置：{SpeHeroFaceConfigPath}");
                }
                else
                {
                    EnsureCsvHeaderExists(SpeHeroFaceConfigPath, SpeHeroFaceHeader);
                }
            }
            catch (Exception ex)
            {
                LOG.Error($"准备配置文件失败：{ex}");
            }
        }

        internal static void EnsureCsvHeaderExists(string path, string header)
        {
            try
            {
                string content = File.ReadAllText(path);
                string normalized = content.TrimStart('\uFEFF', '\r', '\n');
                if (normalized.StartsWith(header, StringComparison.Ordinal))
                {
                    return;
                }

                string fixedContent = header + "\r\n" + normalized;
                File.WriteAllText(path, fixedContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                LOG.Warning($"检测到 CSV 表头缺失，已自动修复：{path}");
            }
            catch (Exception ex)
            {
                LOG.Warning($"自动修复 CSV 表头失败：{path}，{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static bool IsRecruitAllSpeHeroesEnabled()
        {
            EnsureConfigExists();

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(ModConfigPath);
                if (_cachedModConfigWriteTimeUtc == lastWriteTimeUtc)
                {
                    return _cachedEnableRecruitAllSpeHeroes;
                }

                _cachedModConfigWriteTimeUtc = lastWriteTimeUtc;
                _cachedEnableRecruitAllSpeHeroes = true;

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
                        break;
                    }
                }

                return _cachedEnableRecruitAllSpeHeroes;
            }
            catch (Exception ex)
            {
                LOG.Warning($"读取配置文件 LongYinCustomSpeHeroData.cfg 失败：{ex.GetType().Name} - {ex.Message}");
                return true;
            }
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

        internal static void ExportVanillaCsvIfNeeded(string exportPath, string text, ref bool exportedFlag)
        {
            if (exportedFlag || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(exportPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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
                SpeHeroConfigPath,
                new[] { "id", "名字", "性别" },
                ref _cachedSpeHeroConfig,
                ref _cachedSpeHeroConfigWriteTimeUtc);
        }

        internal static LoadedConfigData LoadSpeHeroFaceConfig()
        {
            return LoadConfig(
                SpeHeroFaceConfigPath,
                new[] { "id", "名字" },
                ref _cachedSpeHeroFaceConfig,
                ref _cachedSpeHeroFaceConfigWriteTimeUtc);
        }

        private static LoadedConfigData LoadConfig(
            string path,
            string[] requiredHeaders,
            ref LoadedConfigData cachedConfig,
            ref DateTime cachedWriteTimeUtc)
        {
            EnsureConfigExists();

            if (!File.Exists(path))
            {
                LOG.Warning($"配置文件不存在：{path}");
                return LoadedConfigData.Empty;
            }

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
                if (cachedConfig.Valid && cachedWriteTimeUtc == lastWriteTimeUtc)
                {
                    return cachedConfig;
                }

                string[] lines = ReadAllLinesShared(path);
                if (lines.Length <= 1)
                {
                    LOG.Warning($"配置文件为空：{path}");
                    cachedConfig = LoadedConfigData.Empty;
                    cachedWriteTimeUtc = lastWriteTimeUtc;
                    return cachedConfig;
                }

                string[] headers = SplitCsvLine(lines[0]);
                for (int i = 0; i < headers.Length; i++)
                {
                    headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                }

                if (!ValidateHeaders(headers, requiredHeaders, path))
                {
                    cachedConfig = LoadedConfigData.Empty;
                    cachedWriteTimeUtc = lastWriteTimeUtc;
                    return cachedConfig;
                }

                int rowCount = 0;
                int idColumnIndex = FindColumnIndex(headers, "id");
                var heroIds = new HashSet<int>();
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    rowCount++;

                    if (idColumnIndex < 0)
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    string idText = GetCell(cells, idColumnIndex);
                    if (int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int heroId))
                    {
                        heroIds.Add(heroId);
                    }
                }

                cachedConfig = new LoadedConfigData(true, string.Join("\r\n", lines), rowCount, heroIds);
                cachedWriteTimeUtc = lastWriteTimeUtc;
                return cachedConfig;
            }
            catch (Exception ex)
            {
                LOG.Error($"读取配置文件失败：{path}，{ex}");
                return LoadedConfigData.Empty;
            }
        }

        internal static MergeResult BuildMergedCsvText(string vanillaText, LoadedConfigData configData, string tableName)
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
                        string textUtf8 = strictUtf8.GetString(bytes);
                        return textUtf8.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    }
                    catch (DecoderFallbackException)
                    {
                        LOG.Warning($"[{Path.GetFileName(path)}] 不符合标准 UTF-8 编码，已启动 GBK 容错解析。");
                        string textGbk = Encoding.GetEncoding("gbk").GetString(bytes);
                        return textGbk.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    }
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
            }

            using var finalStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(finalStream, Encoding.UTF8);
            return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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
                    Main.ExportVanillaCsvIfNeeded(Main.VanillaSpeHeroExportPath, textAsset.text, ref Main.GetSpeHeroExportedFlagRef());
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

                Main.ExportVanillaCsvIfNeeded(Main.VanillaSpeHeroFaceExportPath, textAsset.text, ref Main.GetSpeHeroFaceExportedFlagRef());
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
