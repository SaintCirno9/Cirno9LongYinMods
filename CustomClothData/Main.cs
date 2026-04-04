using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CustomClothData.Main), "CustomClothData", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace CustomClothData
{
    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(false, string.Empty, 0, new HashSet<int>());

        public LoadedConfigData(bool valid, string csvText, int rowCount, HashSet<int> skinIds)
        {
            Valid = valid;
            CsvText = csvText;
            RowCount = rowCount;
            SkinIds = skinIds ?? new HashSet<int>();
        }

        public bool Valid { get; }

        public string CsvText { get; }

        public int RowCount { get; }

        public HashSet<int> SkinIds { get; }
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

    public sealed class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "CustomClothData");
        internal static readonly string ClothConfigPath = Path.Combine(ConfigDir, "SkinDataBase.csv");
        internal static readonly string VanillaExportPath = Path.Combine(ConfigDir, "Vanilla_SkinDataBase.csv");

        private const string SkinDataResourcePath = "GameData/SkinDataBase";
        private const string ClothHeader = "id,名字,加成效果,DLC";
        private static readonly string DefaultClothCsv =
            ClothHeader + "\r\n" +
            "-10,邪道服,伤害0.02;生命上限200,-1\r\n" +
            "-9,杀手服,速度0.02;生命上限200,-1\r\n" +
            "-8,强盗服,暴击0.02;生命上限200,-1\r\n" +
            "-7,游侠服,命中0.02;生命上限200,-1\r\n" +
            "-6,才子服,学识4;生命上限200,-1\r\n" +
            "-5,官差服,压制0.02;生命上限200,-1\r\n" +
            "-4,杂役服,采伐4;生命上限200,-1\r\n" +
            "-3,商人服,口才4;生命上限200,-1\r\n" +
            "-2,农民服,木植4;生命上限200,-1\r\n" +
            "-1,常服,经验获取0.02;生命上限200,-1\r\n" +
            "0,锦袍,意志2;生命上限200,-1\r\n" +
            "1,夜行衣,射术2;生命上限200,-1\r\n" +
            "2,医袍,医术4;生命上限200,-1\r\n" +
            "3,百衲衣,拳掌2;生命上限200,-1\r\n" +
            "4,戎装,长兵2;生命上限200,-1\r\n" +
            "5,法服,智力2;生命上限200,-1\r\n" +
            "6,甲胄,护甲1;生命上限200,-1\r\n" +
            "7,避毒服,毒术4;生命上限200,-1\r\n" +
            "8,铁衣,力道2;生命上限200,-1\r\n" +
            "9,襕衫,闪避0.02;生命上限200,-1\r\n" +
            "10,袈裟,体质2;生命上限200,-1\r\n" +
            "11,道袍,内功2;生命上限200,-1\r\n" +
            "12,胡裘,刀法2;生命上限200,-1\r\n" +
            "13,鹤氅,剑法2;生命上限200,-1\r\n" +
            "14,瑶衣,轻功2;生命上限200,-1\r\n" +
            "15,奇装,奇门2;生命上限200,-1\r\n" +
            "16,匠服,灵巧2;生命上限200,-1\r\n" +
            "17,火浣布,锻造4;生命上限200,-1\r\n" +
            "18,羌姆袍,绝技2;生命上限200,-1\r\n" +
            "19,貂裘,经脉2;生命上限200,-1\r\n" +
            "-100,白马红妆,伤害0.02;生命上限200,0\r\n" +
            "-101,风起青萍,速度0.02;生命上限200,0\r\n";

        private static readonly string[] RequiredHeaders = { "id", "名字", "加成效果", "DLC" };
        private static LoadedConfigData _cachedConfig = LoadedConfigData.Empty;
        private static DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;
        private static bool _exportedVanillaSkinData;

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            LOG.Msg($"CustomClothData 已加载，配置目录：{ConfigDir}");
            LOG.Msg("已确认服装数据资源入口：GameData/SkinDataBase");
        }

        internal static void EnsureConfigExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);

                if (!File.Exists(ClothConfigPath))
                {
                    File.WriteAllText(ClothConfigPath, DefaultClothCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置：{ClothConfigPath}");
                }
                else
                {
                    EnsureCsvHeaderExists(ClothConfigPath, ClothHeader);
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
                string content = ReadAllTextShared(path);
                string normalized = content.TrimStart('\uFEFF', '\r', '\n');
                if (HasExpectedHeaderLine(normalized, header))
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

        internal static bool ShouldPatchSkinData(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/').Trim();
            return string.Equals(normalized, SkinDataResourcePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "SkinDataBase", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("/SkinDataBase", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(SkinDataResourcePath, StringComparison.OrdinalIgnoreCase);
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
                LOG.Msg($"已导出原版服装表：{exportPath}");
            }
            catch (Exception ex)
            {
                LOG.Warning($"导出原版服装表失败：{exportPath}，{ex.GetType().Name} - {ex.Message}");
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

        internal static LoadedConfigData LoadClothConfig()
        {
            EnsureConfigExists();

            if (!File.Exists(ClothConfigPath))
            {
                LOG.Warning($"配置文件不存在：{ClothConfigPath}");
                return LoadedConfigData.Empty;
            }

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(ClothConfigPath);
                if (_cachedConfig.Valid && _cachedConfigWriteTimeUtc == lastWriteTimeUtc)
                {
                    return _cachedConfig;
                }

                string[] lines = ReadAllLinesShared(ClothConfigPath);
                if (lines.Length <= 1)
                {
                    LOG.Warning($"配置文件为空：{ClothConfigPath}");
                    _cachedConfig = LoadedConfigData.Empty;
                    _cachedConfigWriteTimeUtc = lastWriteTimeUtc;
                    return _cachedConfig;
                }

                string[] headers = SplitCsvLine(lines[0]);
                for (int i = 0; i < headers.Length; i++)
                {
                    headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                }

                if (!ValidateHeaders(headers, RequiredHeaders, ClothConfigPath))
                {
                    _cachedConfig = LoadedConfigData.Empty;
                    _cachedConfigWriteTimeUtc = lastWriteTimeUtc;
                    return _cachedConfig;
                }

                int rowCount = 0;
                int idColumnIndex = FindColumnIndex(headers, "id");
                var skinIds = new HashSet<int>();
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
                    if (int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skinId))
                    {
                        skinIds.Add(skinId);
                    }
                }

                _cachedConfig = new LoadedConfigData(true, string.Join("\r\n", lines), rowCount, skinIds);
                _cachedConfigWriteTimeUtc = lastWriteTimeUtc;
                return _cachedConfig;
            }
            catch (Exception ex)
            {
                LOG.Error($"读取配置文件失败：{ClothConfigPath}，{ex}");
                return LoadedConfigData.Empty;
            }
        }

        internal static MergeResult BuildMergedCsvText(string vanillaText, LoadedConfigData configData)
        {
            if (string.IsNullOrWhiteSpace(vanillaText) || !configData.Valid || configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.CsvText))
            {
                return MergeResult.NoChange;
            }

            string normalizedVanillaText = vanillaText.Replace("\uFEFF", string.Empty);
            string[] vanillaLines = normalizedVanillaText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.CsvText.Replace("\uFEFF", string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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

            var vanillaIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string id = GetCell(cells, vanillaIdCol);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    vanillaIds.Add(id);
                }
            }

            var customById = new Dictionary<string, string>(StringComparer.Ordinal);
            var customCellsById = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var ignoredNewIds = new List<string>();
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
                    LOG.Warning($"服装配置检测到空 id 行，已跳过：{line}");
                    continue;
                }

                if (!vanillaIds.Contains(id))
                {
                    ignoredNewIds.Add(id);
                    continue;
                }

                customById[id] = line;
                customCellsById[id] = cells;
            }

            if (ignoredNewIds.Count > 0)
            {
                string joinedIds = string.Join(", ", ignoredNewIds.Distinct(StringComparer.Ordinal));
                LOG.Warning($"当前版本已禁用新增服装条目。以下自定义 id 不在原版 SkinDataBase 中，已忽略：{joinedIds}");
            }

            if (customById.Count == 0)
            {
                return MergeResult.NoChange;
            }

            var outputLines = new List<string> { vanillaLines[0] };
            var matchedIds = new HashSet<string>(StringComparer.Ordinal);
            int replacedCount = 0;

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
                    if (customCellsById.TryGetValue(id, out string[] replacementCells))
                    {
                        outputLines.Add(MergeCsvRowByHeaders(vanillaHeaders, cells, customHeaders, replacementCells));
                    }
                    else
                    {
                        outputLines.Add(replacementLine);
                    }

                    matchedIds.Add(id);
                    replacedCount++;
                    continue;
                }

                outputLines.Add(line);
            }

            string mergedText = string.Join("\r\n", outputLines);
            bool changed = !string.Equals(mergedText, normalizedVanillaText, StringComparison.Ordinal);
            return changed
                ? new MergeResult(true, mergedText, replacedCount, 0)
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
            string normalizedColumnName = NormalizeHeaderName(columnName);
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(NormalizeHeaderName(headers[i]), normalizedColumnName, StringComparison.Ordinal))
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

        internal static ref bool GetSkinDataExportedFlagRef()
        {
            return ref _exportedVanillaSkinData;
        }

        private static bool ValidateHeaders(string[] headers, string[] requiredHeaders, string path)
        {
            bool valid = true;
            foreach (string requiredHeader in requiredHeaders)
            {
                if (headers.Any(header => string.Equals(NormalizeHeaderName(header), NormalizeHeaderName(requiredHeader), StringComparison.Ordinal)))
                {
                    continue;
                }

                LOG.Error($"配置文件缺少必要列：{requiredHeader}，文件：{path}");
                valid = false;
            }

            return valid;
        }

        private static bool HasExpectedHeaderLine(string text, string header)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string firstLine = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
            string[] actualHeaders = SplitCsvLine(firstLine);
            string[] expectedHeaders = SplitCsvLine(header);
            if (actualHeaders.Length != expectedHeaders.Length)
            {
                return false;
            }

            foreach (string expectedHeader in expectedHeaders)
            {
                if (!actualHeaders.Any(actualHeader => string.Equals(NormalizeHeaderName(actualHeader), NormalizeHeaderName(expectedHeader), StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            return true;
        }

        private static string MergeCsvRowByHeaders(string[] vanillaHeaders, string[] vanillaCells, string[] customHeaders, string[] customCells)
        {
            string[] mergedCells = new string[Math.Max(vanillaHeaders.Length, vanillaCells.Length)];
            for (int i = 0; i < mergedCells.Length; i++)
            {
                mergedCells[i] = i < vanillaCells.Length ? vanillaCells[i] : string.Empty;
            }

            for (int i = 0; i < vanillaHeaders.Length; i++)
            {
                int customColumnIndex = FindColumnIndex(customHeaders, vanillaHeaders[i]);
                if (customColumnIndex < 0 || customColumnIndex >= customCells.Length)
                {
                    continue;
                }

                mergedCells[i] = customCells[customColumnIndex];
            }

            return JoinCsvLine(mergedCells);
        }

        private static string JoinCsvLine(string[] cells)
        {
            return string.Join(",", cells.Select(EscapeCsvCell));
        }

        private static string EscapeCsvCell(string cell)
        {
            string value = cell ?? string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string NormalizeHeaderName(string header)
        {
            return (header ?? string.Empty).Trim('\uFEFF', ' ', '\t').ToLowerInvariant();
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
                        string textUtf8 = strictUtf8.GetString(bytes);
                        return textUtf8.Replace("\uFEFF", string.Empty);
                    }
                    catch (DecoderFallbackException)
                    {
                        LOG.Warning($"[{Path.GetFileName(path)}] 不符合标准 UTF-8 编码，已启动 GBK 容错解析。");
                        string textGbk = Encoding.GetEncoding("gbk").GetString(bytes);
                        return textGbk;
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

        private static string[] ReadAllLinesShared(string path, int maxAttempts = 5)
        {
            return ReadAllTextShared(path, maxAttempts).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Il2CppSystem.Type) })]
    public static class ResourcesLoadPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            if (!Main.ShouldPatchSkinData(path))
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

                Main.ExportVanillaCsvIfNeeded(Main.VanillaExportPath, textAsset.text, ref Main.GetSkinDataExportedFlagRef());
                LoadedConfigData configData = Main.LoadClothConfig();
                MergeResult mergeResult = Main.BuildMergedCsvText(textAsset.text, configData);
                if (!mergeResult.Changed)
                {
                    return;
                }

                TextAsset replacement = Main.CreateTextAsset(mergeResult.MergedText, textAsset.name);
                if (replacement != null)
                {
                    __result = replacement;
                    Main.LOG.Msg($"已注入 GameData/SkinDataBase：替换 {mergeResult.ReplacedCount} 条。");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"处理资源失败：path={path}，{ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
