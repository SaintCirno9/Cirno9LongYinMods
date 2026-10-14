using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinCustomHeroTags.Main), "LongYinCustomHeroTags", "2.0.3", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomHeroTags
{
    internal sealed class ConfigSource
    {
        public ConfigSource(string sourceName, string csvPath, int order)
        {
            SourceName = sourceName;
            CsvPath = csvPath;
            Order = order;
        }

        public string SourceName { get; }

        public string CsvPath { get; }

        public int Order { get; }
    }

    internal sealed class CustomHeroTagDefinition
    {
        public int? SourceId { get; init; }

        public string SourceName { get; init; } = string.Empty;

        public int Value { get; init; }

        public string Name { get; init; } = string.Empty;

        public string SameMeaning { get; init; } = string.Empty;

        public string OppositeMeaning { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public int Order { get; init; }

        public bool CanRandom { get; init; }

        public bool ShowRightLine { get; init; }

        public SkillTargetType EffectTarget { get; init; } = SkillTargetType.Self;

        public string[] Requirements { get; init; } = Array.Empty<string>();

        public string[] ReplaceTags { get; init; } = Array.Empty<string>();

        public string RawEffectText { get; init; } = string.Empty;
    }

    internal sealed class CustomHeroTagRow
    {
        public string SourceName { get; init; } = string.Empty;

        public int RowNumber { get; init; }

        public bool HasIdColumn { get; init; }

        public bool HasExtraCells { get; init; }

        public string IdText { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string ValueText { get; init; } = string.Empty;

        public string EffectTargetText { get; init; } = string.Empty;

        public string RawEffectText { get; init; } = string.Empty;

        public string SameMeaning { get; init; } = string.Empty;

        public string OppositeMeaning { get; init; } = string.Empty;

        public string CanRandomText { get; init; } = string.Empty;

        public string OrderText { get; init; } = string.Empty;

        public string RequirementText { get; init; } = string.Empty;

        public string ReplaceText { get; init; } = string.Empty;

        public string[] Requirements { get; init; } = Array.Empty<string>();

        public string[] ReplaceTags { get; init; } = Array.Empty<string>();

        public string Category { get; init; } = string.Empty;

        public string ShowRightLineText { get; init; } = string.Empty;
    }

    public class Main : MelonMod
    {
        internal static readonly string ConfigDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomHeroTags");
        internal static readonly string LegacyConfigFilePath = Path.Combine(ConfigDirectoryPath, "HeroTagData.csv");
        internal static readonly string DataDirectoryPath = Path.Combine(ConfigDirectoryPath, "Data");
        internal static readonly string DefaultConfigDirectoryPath = Path.Combine(DataDirectoryPath, "000");
        internal static readonly string DefaultConfigFilePath = Path.Combine(DefaultConfigDirectoryPath, "HeroTagData.csv");
        internal static readonly string VanillaExportPath = Path.Combine(ConfigDirectoryPath, "Vanilla_HeroTagData.csv");
        private const string SpeHeroResourcePath = "GameData/SpeHeroData";
        internal static readonly System.Collections.Generic.Dictionary<string, string> RenamedVanillaHeroTagNames =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly string DefaultConfigContent =
            "id,名称,价值,影响目标,效果,同义组,反义组,可随机,展示顺序,领悟需求,替换,大类,显示箭头\r\n" +
            "1000,剑胆琴心,5,Self,剑法威力+0.3;慧眼+0.3;破招+0.3;好感获取+1.0,剑胆琴心,,0,0,,,高级,FALSE\r\n" +
            "1001,独孤求败,5,Self,急速+0.3;穿甲+0.3;破招+0.3;拆招+0.3;负面抗性+0.9,独孤求败,,0,0,,,高级,FALSE\r\n" +
            "1002,浑心无字,5,Self,经验获取+3.0;护体真气+0.3;调息+0.3;吹内+0.3;反伤吸内+0.3,浑心无字,,0,0,,,高级,FALSE\r\n" +
            "1003,白衣行化,5,Self,轻灵+0.3;飘逸+0.3;连绵+0.3;护足+10;移距+3,白衣行化,,0,0,,,高级,FALSE\r\n" +
            "1004,大全千法,5,Self,拳掌距离+2;剑法距离+2;刀法距离+2;长兵距离+2;奇门距离+2;射术距离+2,大全千法,,0,0,,,高级,FALSE\r\n";

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigFileExists();
            LOG.Msg($"LongYinCustomHeroTags 已加载，配置目录：{ConfigDirectoryPath}");
        }

        internal static void EnsureConfigFileExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectoryPath);
                Directory.CreateDirectory(DataDirectoryPath);

                bool hasLegacyConfig = File.Exists(LegacyConfigFilePath);
                bool hasDataConfig = CollectConfigSources().Any(source => string.Equals(source.CsvPath, LegacyConfigFilePath, StringComparison.OrdinalIgnoreCase) == false);
                if (hasLegacyConfig || hasDataConfig)
                {
                    return;
                }

                Directory.CreateDirectory(DefaultConfigDirectoryPath);
                File.WriteAllText(DefaultConfigFilePath, DefaultConfigContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                LOG.Msg($"已创建默认天赋配置文件：{DefaultConfigFilePath}");
            }
            catch (Exception ex)
            {
                LOG.Error($"准备配置文件失败：{ex}");
            }
        }

        internal static System.Collections.Generic.List<ConfigSource> CollectConfigSources()
        {
            var sources = new System.Collections.Generic.List<ConfigSource>();

            if (File.Exists(LegacyConfigFilePath))
            {
                sources.Add(new ConfigSource("旧版根目录", LegacyConfigFilePath, 0));
            }

            if (!Directory.Exists(DataDirectoryPath))
            {
                return sources;
            }

            foreach (string dir in Directory.GetDirectories(DataDirectoryPath)
                         .OrderBy(path => int.TryParse(Path.GetFileName(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out int order) ? order : int.MaxValue)
                         .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
                {
                    continue;
                }

                string csvPath = Path.Combine(dir, "HeroTagData.csv");
                if (!File.Exists(csvPath))
                {
                    continue;
                }

                sources.Add(new ConfigSource($"Data/{dirName}", csvPath, order));
            }

            return sources;
        }

        internal static bool ShouldTraceHeroTagResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/').Trim();
            return string.Equals(normalized, "GameData/HeroTagData", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "HeroTagData", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("/HeroTagData", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("GameData/HeroTagData", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldTraceSpeHeroResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/').Trim();
            return string.Equals(normalized, SpeHeroResourcePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "SpeHeroData", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("/SpeHeroData", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(SpeHeroResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ExportVanillaHeroTagsFromResource(TextAsset textAsset)
        {
            if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigDirectoryPath);
                File.WriteAllText(VanillaExportPath, textAsset.text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                LOG.Msg($"已将【实时提取】的完整版原版天赋数据释放到：{VanillaExportPath}");
            }
            catch (Exception ex)
            {
                LOG.Warning($"释放原版天赋模板失败：{ex.GetType().Name} - {ex.Message}");
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

        internal static void UpdateRenamedVanillaHeroTagNames(
            System.Collections.Generic.Dictionary<int, string> vanillaNamesById,
            System.Collections.Generic.List<CustomHeroTagDefinition> configRows,
            int vanillaMaxId)
        {
            RenamedVanillaHeroTagNames.Clear();
            foreach (CustomHeroTagDefinition row in configRows)
            {
                if (!row.SourceId.HasValue || row.SourceId.Value < 0 || row.SourceId.Value > vanillaMaxId)
                {
                    continue;
                }

                if (!vanillaNamesById.TryGetValue(row.SourceId.Value, out string vanillaName) || string.IsNullOrWhiteSpace(vanillaName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Name) || string.Equals(vanillaName, row.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                RenamedVanillaHeroTagNames[vanillaName] = row.Name;
            }
        }
    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Il2CppSystem.Type) })]
    public static class HeroTagResourcePatch
    {
        private static readonly string[] RequiredColumns =
        {
            "名称",
            "价值",
            "影响目标",
            "效果",
            "同义组",
            "反义组",
            "可随机",
            "展示顺序",
            "领悟需求",
            "替换",
            "大类",
            "显示箭头",
        };

        private static bool _exportedVanillaHeroTagData;

        [HarmonyPostfix]
        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            bool isHeroTagData = Main.ShouldTraceHeroTagResourcePath(path);
            bool isSpeHeroData = Main.ShouldTraceSpeHeroResourcePath(path);
            if (!isHeroTagData && !isSpeHeroData)
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

                if (isHeroTagData)
                {
                    if (!_exportedVanillaHeroTagData)
                    {
                        Main.ExportVanillaHeroTagsFromResource(textAsset);
                        _exportedVanillaHeroTagData = true;
                    }

                    string mergedText = BuildMergedHeroTagText(textAsset.text);
                    if (string.IsNullOrWhiteSpace(mergedText) || string.Equals(mergedText, textAsset.text, StringComparison.Ordinal))
                    {
                        return;
                    }

                    TextAsset replacement = Main.CreateTextAsset(mergedText, textAsset.name);
                    if (replacement == null)
                    {
                        Main.LOG.Warning("创建替换用 TextAsset 失败，已跳过 HeroTagData 资源注入。");
                        return;
                    }

                    __result = replacement;
                    Main.LOG.Msg($"已将自定义天赋配置直接注入资源：{path}");
                    return;
                }

                string patchedSpeHeroText = BuildPatchedSpeHeroText(textAsset.text);
                if (string.IsNullOrWhiteSpace(patchedSpeHeroText) || string.Equals(patchedSpeHeroText, textAsset.text, StringComparison.Ordinal))
                {
                    return;
                }

                TextAsset speHeroReplacement = Main.CreateTextAsset(patchedSpeHeroText, textAsset.name);
                if (speHeroReplacement == null)
                {
                    Main.LOG.Warning("创建替换用 TextAsset 失败，已跳过 SpeHeroData 标签同步。");
                    return;
                }

                __result = speHeroReplacement;
                Main.LOG.Msg("已将重命名后的天赋名称同步注入 GameData/SpeHeroData 的标签列。");
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"Resources.Load 处理资源失败：path={path}，异常：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static string BuildMergedHeroTagText(string vanillaText)
        {
            if (string.IsNullOrWhiteSpace(vanillaText))
            {
                return null;
            }

            string[] vanillaLines = SplitResourceLines(vanillaText);
            if (vanillaLines.Length == 0)
            {
                return null;
            }

            string[] vanillaHeaders = SplitCsvLine(vanillaLines[0]);
            int vanillaIdCol = FindColumnIndex(vanillaHeaders, "编号", "id");
            int vanillaNameCol = FindColumnIndex(vanillaHeaders, "名称");
            int vanillaSameMeaningCol = FindColumnIndex(vanillaHeaders, "同义组");
            if (vanillaIdCol < 0)
            {
                Main.LOG.Warning("原版 HeroTagData 缺少编号/id列，已跳过注入。");
                return null;
            }

            int vanillaMaxId = -1;
            var validNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vanillaIds = new System.Collections.Generic.HashSet<int>();
            var vanillaNamesById = new System.Collections.Generic.Dictionary<int, string>();
            var vanillaSameMeanings = new System.Collections.Generic.Dictionary<int, string>();
            for (int i = 1; i < vanillaLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(vanillaLines[i]))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(vanillaLines[i]);
                if (!int.TryParse(GetCell(cells, vanillaIdCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    continue;
                }

                vanillaIds.Add(id);
                if (id > vanillaMaxId)
                {
                    vanillaMaxId = id;
                }

                string name = GetCell(cells, vanillaNameCol);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    validNames.Add(name);
                    vanillaNamesById[id] = name;
                }

                string sameMeaning = GetCell(cells, vanillaSameMeaningCol);
                if (!string.IsNullOrWhiteSpace(sameMeaning))
                {
                    vanillaSameMeanings[id] = sameMeaning;
                }
            }

            System.Collections.Generic.List<CustomHeroTagDefinition> configRows = LoadCustomHeroTagDefinitions(validNames, vanillaSameMeanings);
            Main.UpdateRenamedVanillaHeroTagNames(vanillaNamesById, configRows, vanillaMaxId);
            if (configRows.Count == 0)
            {
                return null;
            }

            var overrideById = new System.Collections.Generic.Dictionary<int, CustomHeroTagDefinition>();
            var appendedRows = new System.Collections.Generic.List<CustomHeroTagDefinition>();
            foreach (CustomHeroTagDefinition row in configRows)
            {
                if (row.SourceId.HasValue && row.SourceId.Value >= 0 && row.SourceId.Value <= vanillaMaxId)
                {
                    overrideById[row.SourceId.Value] = row;
                }
                else
                {
                    appendedRows.Add(row);
                }
            }

            var finalLines = new System.Collections.Generic.List<string> { vanillaLines[0] };
            var finalTagNames = BuildFinalTagNameSet(vanillaIds, vanillaNamesById, overrideById, appendedRows);
            var replaceNameByNumericId = BuildReplaceNameMap(vanillaIds, vanillaNamesById, overrideById, appendedRows, vanillaMaxId);
            var renamedTagNames = new System.Collections.Generic.Dictionary<string, string>(Main.RenamedVanillaHeroTagNames, StringComparer.Ordinal);

            int overrideCount = 0;
            int syncedVanillaReferenceRows = 0;
            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (!int.TryParse(GetCell(cells, vanillaIdCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    finalLines.Add(line);
                    continue;
                }

                if (!overrideById.TryGetValue(id, out CustomHeroTagDefinition replacement))
                {
                    string patchedVanillaLine = BuildPatchedVanillaReferenceLine(
                        vanillaHeaders,
                        cells,
                        vanillaNameCol,
                        replaceNameByNumericId,
                        finalTagNames,
                        renamedTagNames);
                    finalLines.Add(patchedVanillaLine ?? line);
                    if (patchedVanillaLine != null)
                    {
                        syncedVanillaReferenceRows++;
                    }

                    continue;
                }

                string originalName = GetCell(cells, vanillaNameCol);
                finalLines.Add(BuildOutputLine(vanillaHeaders, cells, replacement, id, replaceNameByNumericId, finalTagNames, renamedTagNames));
                Main.LOG.Msg(
                    $"覆盖原版天赋：id={id}，原版={originalName}，新配置={replacement.Name}，source={replacement.SourceName}");
                overrideCount++;
            }

            int nextInjectedId = vanillaMaxId + 1;
            foreach (CustomHeroTagDefinition row in appendedRows)
            {
                int runtimeId = nextInjectedId++;
                finalLines.Add(BuildOutputLine(vanillaHeaders, null, row, runtimeId, replaceNameByNumericId, finalTagNames, renamedTagNames));
                Main.LOG.Msg($"新增天赋注入：source={row.SourceName}, 名称={row.Name}, runtimeId={runtimeId}{(row.SourceId.HasValue ? $", sourceId={row.SourceId.Value}" : ", legacyNoId=true")}");
            }

            if (syncedVanillaReferenceRows > 0)
            {
                Main.LOG.Msg($"已同步 {syncedVanillaReferenceRows} 条原版天赋中的领悟需求/替换引用。");
            }

            Main.LOG.Msg($"HeroTag 资源合并完成：覆盖 {overrideCount} 条，新增 {appendedRows.Count} 条。");
            return string.Join("\r\n", finalLines) + "\r\n";
        }

        private static string BuildPatchedSpeHeroText(string speHeroText)
        {
            if (string.IsNullOrWhiteSpace(speHeroText) || Main.RenamedVanillaHeroTagNames.Count == 0)
            {
                return null;
            }

            string[] lines = SplitResourceLines(speHeroText);
            if (lines.Length == 0)
            {
                return null;
            }

            string[] headers = SplitCsvLine(lines[0]);
            int tagCol = FindColumnIndex(headers, "标签");
            int nameCol = FindColumnIndex(headers, "名字", "名称");
            if (tagCol < 0)
            {
                Main.LOG.Warning("SpeHeroData 缺少“标签”列，已跳过同步。");
                return null;
            }

            var finalLines = new System.Collections.Generic.List<string> { lines[0] };
            int changedRows = 0;
            int replacedTags = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string tagCell = GetCell(cells, tagCol);
                if (string.IsNullOrWhiteSpace(tagCell))
                {
                    finalLines.Add(line);
                    continue;
                }

                string[] tags = SplitSpeHeroTagCell(tagCell);
                bool rowChanged = false;
                for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
                {
                    string tag = tags[tagIndex];
                    if (!Main.RenamedVanillaHeroTagNames.TryGetValue(tag, out string renamedTag))
                    {
                        continue;
                    }

                    tags[tagIndex] = renamedTag;
                    replacedTags++;
                    rowChanged = true;
                }

                if (!rowChanged)
                {
                    finalLines.Add(line);
                    continue;
                }

                changedRows++;
                cells[tagCol] = string.Join(";", tags);
                finalLines.Add(BuildCsvLine(cells));

                string heroName = GetCell(cells, nameCol);
                if (!string.IsNullOrWhiteSpace(heroName))
                {
                    Main.LOG.Msg($"SpeHeroData 标签已同步：角色={heroName}");
                }
            }

            if (changedRows == 0)
            {
                return null;
            }

            Main.LOG.Msg($"SpeHeroData 标签同步完成：修改 {changedRows} 行，替换 {replacedTags} 处标签引用。");
            return string.Join("\r\n", finalLines) + "\r\n";
        }

        private static System.Collections.Generic.List<CustomHeroTagDefinition> LoadCustomHeroTagDefinitions(
            System.Collections.Generic.HashSet<string> vanillaNames,
            System.Collections.Generic.Dictionary<int, string> vanillaSameMeanings)
        {
            var rawRows = new System.Collections.Generic.List<CustomHeroTagRow>();
            System.Collections.Generic.List<ConfigSource> sources = Main.CollectConfigSources();
            if (sources.Count == 0)
            {
                Main.LOG.Warning("未找到任何天赋配置文件，已跳过注入。");
                return new System.Collections.Generic.List<CustomHeroTagDefinition>();
            }

            foreach (ConfigSource source in sources)
            {
                Main.LOG.Msg($"读取天赋配置源：{source.SourceName} -> {source.CsvPath}");
                string[] lines;
                try
                {
                    lines = ReadAllLinesWithRetry(source.CsvPath);
                }
                catch (Exception ex)
                {
                    Main.LOG.Warning($"读取配置源失败：{source.CsvPath}，{ex.GetType().Name} - {ex.Message}");
                    continue;
                }

                if (lines.Length <= 1)
                {
                    continue;
                }

                string[] headers = SplitCsvLine(lines[0]);
                for (int i = 0; i < headers.Length; i++)
                {
                    headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                }

                bool hasIdColumn = FindColumnIndex(headers, "id", "编号") >= 0;
                if (!HeroTagValidation.ValidateHeaders(headers, hasIdColumn, RequiredColumns))
                {
                    HeroTagValidation.LogCsvFormatHints(lines[0], headers, source.CsvPath);
                    continue;
                }

                for (int rowIndex = 1; rowIndex < lines.Length; rowIndex++)
                {
                    string line = lines[rowIndex].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    rawRows.Add(ParseDefinitionRow(headers, cells, rowIndex + 1, source.SourceName, hasIdColumn));
                }
            }

            System.Collections.Generic.List<CustomHeroTagRow> mergedRows = MergeDefinitionRows(rawRows);
            System.Collections.Generic.List<CustomHeroTagDefinition> definitions = HeroTagValidation.BuildValidatedDefinitions(mergedRows);
            var allValidNames = new System.Collections.Generic.HashSet<string>(vanillaNames, StringComparer.OrdinalIgnoreCase);
            foreach (CustomHeroTagDefinition definition in definitions)
            {
                allValidNames.Add(definition.Name);
            }

            HeroTagValidation.ValidateCrossReferences(definitions, allValidNames, vanillaSameMeanings);
            Main.LOG.Msg($"已从配置文件加载 {definitions.Count} 个自定义天赋定义。");
            return definitions;
        }

        private static CustomHeroTagRow ParseDefinitionRow(string[] headers, string[] cells, int rowNumber, string sourceName, bool hasIdColumn)
        {
            return new CustomHeroTagRow
            {
                SourceName = sourceName,
                RowNumber = rowNumber,
                HasIdColumn = hasIdColumn,
                HasExtraCells = cells.Length > headers.Length,
                IdText = GetCell(headers, cells, "id", "编号"),
                Name = GetCell(headers, cells, "名称"),
                ValueText = GetCell(headers, cells, "价值"),
                EffectTargetText = GetCell(headers, cells, "影响目标"),
                RawEffectText = GetCell(headers, cells, "效果"),
                SameMeaning = GetCell(headers, cells, "同义组"),
                OppositeMeaning = GetCell(headers, cells, "反义组"),
                CanRandomText = GetCell(headers, cells, "可随机"),
                OrderText = GetCell(headers, cells, "展示顺序"),
                RequirementText = GetCell(headers, cells, "领悟需求"),
                ReplaceText = GetCell(headers, cells, "替换"),
                Requirements = SplitListCell(GetCell(headers, cells, "领悟需求")),
                ReplaceTags = SplitListCell(GetCell(headers, cells, "替换")),
                Category = GetCell(headers, cells, "大类"),
                ShowRightLineText = GetCell(headers, cells, "显示箭头"),
            };
        }

        private static System.Collections.Generic.List<CustomHeroTagRow> MergeDefinitionRows(System.Collections.Generic.List<CustomHeroTagRow> rawRows)
        {
            var mergedRows = new System.Collections.Generic.List<CustomHeroTagRow>();
            foreach (CustomHeroTagRow row in rawRows)
            {
                int existingByIdIndex = FindRowIndexById(mergedRows, row);
                int existingByNameIndex = FindRowIndexByName(mergedRows, row.Name);
                bool hasParsedId = TryGetRowSourceId(row, out int sourceId);
                bool canOverrideExisting = HeroTagValidation.IsRowValidForDeferredMerge(row);

                if (existingByIdIndex >= 0)
                {
                    if (!canOverrideExisting)
                    {
                        mergedRows.Add(row);
                        continue;
                    }

                    if (existingByNameIndex >= 0 && existingByNameIndex != existingByIdIndex)
                    {
                        Main.LOG.Warning(
                            $"已跳过 {row.SourceName} 第 {row.RowNumber} 行：名称“{row.Name}”已被其他 id 使用，无法覆盖 sourceId={sourceId}。");
                        continue;
                    }

                    CustomHeroTagRow previousRow = mergedRows[existingByIdIndex];
                    mergedRows[existingByIdIndex] = row;
                    Main.LOG.Msg(
                        $"配置覆盖：{row.SourceName} 覆盖 {previousRow.SourceName}，sourceId={sourceId}，名称={row.Name}");
                    continue;
                }

                if (existingByNameIndex >= 0)
                {
                    if (!canOverrideExisting)
                    {
                        mergedRows.Add(row);
                        continue;
                    }

                    CustomHeroTagRow previousRow = mergedRows[existingByNameIndex];
                    if (hasParsedId && TryGetRowSourceId(previousRow, out int existingSourceId) && existingSourceId != sourceId)
                    {
                        Main.LOG.Warning(
                            $"已跳过 {row.SourceName} 第 {row.RowNumber} 行：名称“{row.Name}”已被其他 id 使用，无法按名称覆盖 sourceId={sourceId}。");
                        continue;
                    }

                    CustomHeroTagRow mergedRow = CreateNameOverrideRow(previousRow, row);
                    mergedRows[existingByNameIndex] = mergedRow;
                    if (!row.HasIdColumn && mergedRow.HasIdColumn)
                    {
                        Main.LOG.Msg(
                            $"配置按名称覆盖并沿用 id：{row.SourceName} 覆盖 {previousRow.SourceName}，名称={row.Name}，sourceId={mergedRow.IdText}");
                    }
                    else
                    {
                        Main.LOG.Msg(
                            $"配置按名称覆盖：{row.SourceName} 覆盖 {previousRow.SourceName}，名称={row.Name}");
                    }

                    continue;
                }

                mergedRows.Add(row);
            }

            return mergedRows;
        }

        private static CustomHeroTagRow CreateNameOverrideRow(CustomHeroTagRow previousRow, CustomHeroTagRow currentRow)
        {
            if (currentRow.HasIdColumn || previousRow == null || !previousRow.HasIdColumn)
            {
                return currentRow;
            }

            return new CustomHeroTagRow
            {
                SourceName = currentRow.SourceName,
                RowNumber = currentRow.RowNumber,
                HasIdColumn = true,
                HasExtraCells = currentRow.HasExtraCells,
                IdText = previousRow.IdText,
                Name = currentRow.Name,
                ValueText = currentRow.ValueText,
                EffectTargetText = currentRow.EffectTargetText,
                RawEffectText = currentRow.RawEffectText,
                SameMeaning = currentRow.SameMeaning,
                OppositeMeaning = currentRow.OppositeMeaning,
                CanRandomText = currentRow.CanRandomText,
                OrderText = currentRow.OrderText,
                RequirementText = currentRow.RequirementText,
                ReplaceText = currentRow.ReplaceText,
                Requirements = currentRow.Requirements,
                ReplaceTags = currentRow.ReplaceTags,
                Category = currentRow.Category,
                ShowRightLineText = currentRow.ShowRightLineText,
            };
        }

        private static int FindRowIndexById(System.Collections.Generic.List<CustomHeroTagRow> rows, CustomHeroTagRow candidate)
        {
            if (!TryGetRowSourceId(candidate, out int sourceId))
            {
                return -1;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (TryGetRowSourceId(rows[i], out int existingId) && existingId == sourceId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindRowIndexByName(System.Collections.Generic.List<CustomHeroTagRow> rows, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return -1;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] SplitResourceLines(string text)
        {
            string[] lines = text.Replace("\uFEFF", string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int end = lines.Length;
            while (end > 0 && string.IsNullOrEmpty(lines[end - 1]))
            {
                end--;
            }

            if (end < lines.Length)
            {
                Array.Resize(ref lines, end);
            }

            return lines;
        }

        private static string[] ReadAllLinesWithRetry(string filePath)
        {
            const int maxAttempts = 5;
            const int retryDelayMilliseconds = 200;

            IOException lastIOException = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return ReadAllLinesShared(filePath);
                }
                catch (IOException ex)
                {
                    lastIOException = ex;
                    if (attempt >= maxAttempts)
                    {
                        break;
                    }

                    Thread.Sleep(retryDelayMilliseconds);
                }
            }

            throw new IOException($"Unable to read hero tag config after {maxAttempts} attempts: {filePath}", lastIOException);
        }

        private static string[] ReadAllLinesShared(string filePath)
        {
            byte[] rawBytes;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                rawBytes = new byte[stream.Length];
                stream.Read(rawBytes, 0, rawBytes.Length);
            }

            Encoding encoding = DetectEncoding(rawBytes, out int bomLength);
            string fullText = encoding.GetString(rawBytes, bomLength, rawBytes.Length - bomLength);
            string[] lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            int end = lines.Length;
            while (end > 0 && string.IsNullOrEmpty(lines[end - 1]))
            {
                end--;
            }

            if (end < lines.Length)
            {
                Array.Resize(ref lines, end);
            }

            return lines;
        }

        private static Encoding DetectEncoding(byte[] bytes, out int bomLength)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                bomLength = 3;
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            }

            bomLength = 0;
            try
            {
                Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                strictUtf8.GetString(bytes);
                return strictUtf8;
            }
            catch (DecoderFallbackException)
            {
            }

            try
            {
                Encoding gbk = Encoding.GetEncoding(936);
                Main.LOG.Warning("配置文件编码检测：GBK（非 UTF-8），已自动兼容读取。建议将文件重新保存为 UTF-8。");
                return gbk;
            }
            catch
            {
                Main.LOG.Warning("配置文件编码检测：GBK 代码页不可用，回退到系统默认编码。");
                return Encoding.Default;
            }
        }

        private static string[] SplitCsvLine(string line)
        {
            var cells = new System.Collections.Generic.List<string>();
            StringBuilder current = new StringBuilder();
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
            if (inQuotes)
            {
                Main.LOG.Warning($"检测到 CSV 行内引号未正确闭合：{line}");
            }

            return cells.ToArray();
        }

        private static int FindColumnIndex(string[] headers, params string[] columnNames)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                foreach (string columnName in columnNames)
                {
                    if (string.Equals(headers[i], columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string GetCell(string[] headers, string[] cells, params string[] columnNames)
        {
            int index = FindColumnIndex(headers, columnNames);
            return GetCell(cells, index);
        }

        private static string GetCell(string[] cells, int index)
        {
            if (index < 0 || index >= cells.Length)
            {
                return string.Empty;
            }

            return cells[index].Trim();
        }

        internal static int ParseInt(string text, int defaultValue)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : defaultValue;
        }

        internal static bool ParseBool(string text, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            string normalized = text.ToUpperInvariant();
            return normalized switch
            {
                "1" => true,
                "TRUE" => true,
                "YES" => true,
                "Y" => true,
                "0" => false,
                "FALSE" => false,
                "NO" => false,
                "N" => false,
                _ => defaultValue,
            };
        }

        internal static SkillTargetType ParseSkillTargetType(string text)
        {
            return Enum.TryParse(text, ignoreCase: true, out SkillTargetType effectTarget)
                ? effectTarget
                : SkillTargetType.Self;
        }

        private static string[] SplitListCell(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            string[] values = text.Split(new[] { ';', '|', '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Trim();
            }

            return values;
        }

        private static string[] SplitSpeHeroTagCell(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            string[] values = text.Split(new[] { ';', '|', '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Trim();
            }

            return values;
        }

        private static string BuildOutputLine(
            string[] vanillaHeaders,
            string[] baseCells,
            CustomHeroTagDefinition definition,
            int finalId,
            System.Collections.Generic.Dictionary<int, string> replaceNameByNumericId,
            System.Collections.Generic.HashSet<string> finalTagNames,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames)
        {
            string requirements = BuildRequirementCell(definition.Requirements, renamedTagNames);
            string replaceTags = BuildReplaceTagCell(definition, replaceNameByNumericId, finalTagNames, renamedTagNames);
            string[] cells = CreateOutputCells(vanillaHeaders.Length, baseCells);
            for (int i = 0; i < vanillaHeaders.Length; i++)
            {
                string header = vanillaHeaders[i];
                if (TryGetOutputValue(header, definition, finalId, requirements, replaceTags, out string outputValue))
                {
                    cells[i] = outputValue;
                }
            }

            return BuildCsvLine(cells);
        }

        private static string[] CreateOutputCells(int targetLength, string[] baseCells)
        {
            string[] cells = new string[targetLength];
            if (baseCells == null)
            {
                return cells;
            }

            int copyLength = Math.Min(targetLength, baseCells.Length);
            Array.Copy(baseCells, cells, copyLength);
            return cells;
        }

        private static bool TryGetOutputValue(
            string header,
            CustomHeroTagDefinition definition,
            int finalId,
            string requirements,
            string replaceTags,
            out string outputValue)
        {
            if (string.Equals(header, "编号", StringComparison.OrdinalIgnoreCase) || string.Equals(header, "id", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = finalId.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (string.Equals(header, "名称", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.Name;
                return true;
            }

            if (string.Equals(header, "价值", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.Value.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (string.Equals(header, "影响目标", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.EffectTarget.ToString();
                return true;
            }

            if (string.Equals(header, "效果", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.RawEffectText;
                return true;
            }

            if (string.Equals(header, "同义组", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.SameMeaning;
                return true;
            }

            if (string.Equals(header, "反义组", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.OppositeMeaning;
                return true;
            }

            if (string.Equals(header, "可随机", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.CanRandom ? "1" : "0";
                return true;
            }

            if (string.Equals(header, "展示顺序", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.Order.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (string.Equals(header, "领悟需求", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = requirements;
                return true;
            }

            if (string.Equals(header, "替换", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = replaceTags;
                return true;
            }

            if (string.Equals(header, "大类", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.Category;
                return true;
            }

            if (string.Equals(header, "显示箭头", StringComparison.OrdinalIgnoreCase))
            {
                outputValue = definition.ShowRightLine ? "TRUE" : "FALSE";
                return true;
            }

            outputValue = string.Empty;
            return false;
        }

        private static System.Collections.Generic.HashSet<string> BuildFinalTagNameSet(
            System.Collections.Generic.HashSet<int> vanillaIds,
            System.Collections.Generic.Dictionary<int, string> vanillaNamesById,
            System.Collections.Generic.Dictionary<int, CustomHeroTagDefinition> overrideById,
            System.Collections.Generic.List<CustomHeroTagDefinition> appendedRows)
        {
            var finalNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (int vanillaId in vanillaIds)
            {
                if (overrideById.TryGetValue(vanillaId, out CustomHeroTagDefinition overrideDefinition))
                {
                    finalNames.Add(overrideDefinition.Name);
                    continue;
                }

                if (vanillaNamesById.TryGetValue(vanillaId, out string vanillaName) && !string.IsNullOrWhiteSpace(vanillaName))
                {
                    finalNames.Add(vanillaName);
                }
            }

            foreach (CustomHeroTagDefinition row in appendedRows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name))
                {
                    finalNames.Add(row.Name);
                }
            }

            return finalNames;
        }

        private static System.Collections.Generic.Dictionary<int, string> BuildReplaceNameMap(
            System.Collections.Generic.HashSet<int> vanillaIds,
            System.Collections.Generic.Dictionary<int, string> vanillaNamesById,
            System.Collections.Generic.Dictionary<int, CustomHeroTagDefinition> overrideById,
            System.Collections.Generic.List<CustomHeroTagDefinition> appendedRows,
            int vanillaMaxId)
        {
            var replaceNameByNumericId = new System.Collections.Generic.Dictionary<int, string>();
            foreach (int vanillaId in vanillaIds)
            {
                if (overrideById.TryGetValue(vanillaId, out CustomHeroTagDefinition overrideDefinition))
                {
                    replaceNameByNumericId[vanillaId] = overrideDefinition.Name;
                    continue;
                }

                if (vanillaNamesById.TryGetValue(vanillaId, out string vanillaName) && !string.IsNullOrWhiteSpace(vanillaName))
                {
                    replaceNameByNumericId[vanillaId] = vanillaName;
                }
            }

            foreach (CustomHeroTagDefinition row in appendedRows)
            {
                if (!row.SourceId.HasValue || row.SourceId.Value <= vanillaMaxId)
                {
                    continue;
                }

                if (replaceNameByNumericId.TryGetValue(row.SourceId.Value, out string existingName))
                {
                    Main.LOG.Warning($"新增天赋源 id={row.SourceId.Value} 在多个配置中重复出现，数字替换引用将指向最后加载的名称。旧值={existingName}，新值={row.Name}。");
                }

                replaceNameByNumericId[row.SourceId.Value] = row.Name;
            }

            return replaceNameByNumericId;
        }

        private static string BuildReplaceTagCell(
            CustomHeroTagDefinition definition,
            System.Collections.Generic.Dictionary<int, string> replaceNameByNumericId,
            System.Collections.Generic.HashSet<string> finalTagNames,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames)
        {
            if (definition.ReplaceTags == null || definition.ReplaceTags.Length == 0)
            {
                return string.Empty;
            }

            var resolved = new System.Collections.Generic.List<string>();
            foreach (string replaceTag in definition.ReplaceTags)
            {
                string normalizedReference = NormalizeReplaceReference(replaceTag);
                if (string.IsNullOrWhiteSpace(normalizedReference))
                {
                    continue;
                }

                if (int.TryParse(normalizedReference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int replaceId))
                {
                    if (!replaceNameByNumericId.TryGetValue(replaceId, out string mappedName))
                    {
                        Main.LOG.Warning($"天赋“{definition.Name}”的替换目标 id={replaceId} 在最终结果中不存在，已忽略。");
                        continue;
                    }

                    resolved.Add(mappedName);
                    continue;
                }

                normalizedReference = RenameTagNameIfNeeded(normalizedReference, renamedTagNames, out _);
                if (!finalTagNames.Contains(normalizedReference))
                {
                    Main.LOG.Warning($"天赋“{definition.Name}”的替换目标“{replaceTag}”在最终结果中不存在，已忽略。");
                    continue;
                }

                resolved.Add(normalizedReference);
            }

            return string.Join(";", resolved.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string BuildRequirementCell(
            string[] requirements,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames)
        {
            if (requirements == null || requirements.Length == 0)
            {
                return string.Empty;
            }

            var resolved = new System.Collections.Generic.List<string>();
            foreach (string requirement in requirements)
            {
                string rewrittenRequirement = RewriteRequirementReference(requirement, renamedTagNames, out _);
                if (!string.IsNullOrWhiteSpace(rewrittenRequirement))
                {
                    resolved.Add(rewrittenRequirement);
                }
            }

            return string.Join("+", resolved);
        }

        private static string BuildPatchedVanillaReferenceLine(
            string[] vanillaHeaders,
            string[] cells,
            int vanillaNameCol,
            System.Collections.Generic.Dictionary<int, string> replaceNameByNumericId,
            System.Collections.Generic.HashSet<string> finalTagNames,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames)
        {
            if (renamedTagNames == null || renamedTagNames.Count == 0 || cells == null)
            {
                return null;
            }

            int requirementCol = FindColumnIndex(vanillaHeaders, "领悟需求");
            int replaceCol = FindColumnIndex(vanillaHeaders, "替换");
            if (requirementCol < 0 && replaceCol < 0)
            {
                return null;
            }

            bool changed = false;
            string[] patchedCells = CreateOutputCells(vanillaHeaders.Length, cells);
            string ownerName = GetCell(cells, vanillaNameCol);

            if (requirementCol >= 0)
            {
                string[] requirements = SplitListCell(GetCell(cells, requirementCol));
                if (TryBuildPatchedRequirementCell(requirements, renamedTagNames, out string patchedRequirementCell))
                {
                    patchedCells[requirementCol] = patchedRequirementCell;
                    changed = true;
                }
            }

            if (replaceCol >= 0)
            {
                string[] replaceTags = SplitListCell(GetCell(cells, replaceCol));
                if (TryBuildPatchedReplaceCell(ownerName, replaceTags, replaceNameByNumericId, finalTagNames, renamedTagNames, out string patchedReplaceCell))
                {
                    patchedCells[replaceCol] = patchedReplaceCell;
                    changed = true;
                }
            }

            return changed ? BuildCsvLine(patchedCells) : null;
        }

        private static bool TryBuildPatchedRequirementCell(
            string[] requirements,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames,
            out string patchedRequirementCell)
        {
            patchedRequirementCell = string.Empty;
            if (requirements == null || requirements.Length == 0)
            {
                return false;
            }

            bool changed = false;
            var resolved = new System.Collections.Generic.List<string>();
            foreach (string requirement in requirements)
            {
                string rewrittenRequirement = RewriteRequirementReference(requirement, renamedTagNames, out bool referenceChanged);
                if (string.IsNullOrWhiteSpace(rewrittenRequirement))
                {
                    continue;
                }

                resolved.Add(rewrittenRequirement);
                changed |= referenceChanged;
            }

            if (!changed)
            {
                return false;
            }

            patchedRequirementCell = string.Join("+", resolved);
            return true;
        }

        private static bool TryBuildPatchedReplaceCell(
            string ownerName,
            string[] replaceTags,
            System.Collections.Generic.Dictionary<int, string> replaceNameByNumericId,
            System.Collections.Generic.HashSet<string> finalTagNames,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames,
            out string patchedReplaceCell)
        {
            patchedReplaceCell = string.Empty;
            if (replaceTags == null || replaceTags.Length == 0)
            {
                return false;
            }

            bool changed = false;
            var resolved = new System.Collections.Generic.List<string>();
            foreach (string replaceTag in replaceTags)
            {
                string normalizedReference = NormalizeReplaceReference(replaceTag);
                if (string.IsNullOrWhiteSpace(normalizedReference))
                {
                    continue;
                }

                if (int.TryParse(normalizedReference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int replaceId))
                {
                    if (!replaceNameByNumericId.TryGetValue(replaceId, out string mappedName))
                    {
                        continue;
                    }

                    resolved.Add(mappedName);
                    continue;
                }

                string renamedReference = RenameTagNameIfNeeded(normalizedReference, renamedTagNames, out bool referenceChanged);
                if (!finalTagNames.Contains(renamedReference))
                {
                    Main.LOG.Warning($"原版天赋“{ownerName}”中的“替换”引用不存在：\"{replaceTag}\"。");
                    continue;
                }

                resolved.Add(renamedReference);
                changed |= referenceChanged;
            }

            if (!changed)
            {
                return false;
            }

            patchedReplaceCell = string.Join(";", resolved.Distinct(StringComparer.OrdinalIgnoreCase));
            return true;
        }

        private static string RewriteRequirementReference(
            string requirement,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames,
            out bool changed)
        {
            string normalized = requirement?.Trim() ?? string.Empty;
            changed = false;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            int tagMarkerIndex = normalized.LastIndexOf("天赋:", StringComparison.Ordinal);
            if (tagMarkerIndex >= 0)
            {
                string prefix = normalized[..(tagMarkerIndex + 3)];
                string tagName = normalized[(tagMarkerIndex + 3)..].Trim();
                string renamedTagName = RenameTagNameIfNeeded(tagName, renamedTagNames, out changed);
                return string.IsNullOrWhiteSpace(renamedTagName) ? normalized : prefix + renamedTagName;
            }

            return RenameTagNameIfNeeded(normalized, renamedTagNames, out changed);
        }

        private static string RenameTagNameIfNeeded(
            string tagName,
            System.Collections.Generic.IReadOnlyDictionary<string, string> renamedTagNames,
            out bool changed)
        {
            string normalized = tagName?.Trim() ?? string.Empty;
            changed = false;
            if (string.IsNullOrWhiteSpace(normalized)
                || renamedTagNames == null
                || renamedTagNames.Count == 0
                || !renamedTagNames.TryGetValue(normalized, out string renamedTagName)
                || string.IsNullOrWhiteSpace(renamedTagName)
                || string.Equals(normalized, renamedTagName, StringComparison.Ordinal))
            {
                return normalized;
            }

            changed = true;
            return renamedTagName;
        }

        private static string NormalizeReplaceReference(string replaceTag)
        {
            string normalized = replaceTag?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (normalized.StartsWith("天赋:", StringComparison.Ordinal))
            {
                return normalized[3..].Trim();
            }

            return normalized;
        }

        internal static bool TryGetRowSourceId(CustomHeroTagRow row, out int sourceId)
        {
            sourceId = -1;
            return row.HasIdColumn
                && int.TryParse(row.IdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceId)
                && sourceId >= 0;
        }

        private static string BuildCsvLine(string[] cells)
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
    }
}
