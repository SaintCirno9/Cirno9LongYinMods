using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

[assembly: MelonInfo(typeof(LongYinCustomHeroTags.Main), "LongYinCustomHeroTags", "1.0.1", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomHeroTags
{
    internal sealed class CustomHeroTagDefinition
    {
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

    public class Main : MelonMod
    {
        // 自定义天赋配置统一放在游戏目录下的 UserData，方便玩家直接修改。
        internal static readonly string ConfigDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomHeroTags");
        internal static readonly string ConfigFilePath = Path.Combine(ConfigDirectoryPath, "HeroTagData.csv");
        private static readonly string DefaultConfigContent =
            "名称,价值,影响目标,效果,同义组,反义组,可随机,展示顺序,领悟需求,替换,大类,显示箭头\r\n" +
            "剑胆琴心,5,Self,剑法威力+0.3;慧眼+0.3;破招+0.3;好感获取+1.0,剑胆琴心,,0,0,,,高级,FALSE\r\n" +
            "独孤求败,5,Self,急速+0.3;穿甲+0.3;破招+0.3;拆招+0.3;负面抗性+0.9,独孤求败,,0,0,,,高级,FALSE\r\n" +
            "浑心无字,5,Self,经验获取+3.0;护体真气+0.3;调息+0.3;吹内+0.3;反伤吸内+0.3,浑心无字,,0,0,,,高级,FALSE\r\n" +
            "白衣行化,5,Self,轻灵+0.3;飘逸+0.3;连绵+0.3;护足+10;移距+3,白衣行化,,0,0,,,高级,FALSE\r\n" +
            "大全千法,5,Self,拳掌距离+2;剑法距离+2;刀法距离+2;长兵距离+2;奇门距离+2;射术距离+2,大全千法,,0,0,,,高级,FALSE\r\n";

        // Mod 初始化时先确保配置文件存在，保证首启就能正常读取。
        public override void OnInitializeMelon()
        {
            EnsureConfigFileExists();
            LOG.Msg($"LongYinCustomHeroTags 已加载，配置文件：{ConfigFilePath}");
        }

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        // 如果配置文件不存在，就自动生成一份带默认天赋的示例 CSV。
        internal static void EnsureConfigFileExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectoryPath);
                if (File.Exists(ConfigFilePath))
                {
                    return;
                }

                // 首次运行自动落一份模板，避免用户还没配文件时 Mod 直接空跑。
                File.WriteAllText(ConfigFilePath, DefaultConfigContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                LOG.Msg($"已创建默认天赋配置文件：{ConfigFilePath}");
            }
            catch (Exception ex)
            {
                LOG.Error($"准备配置文件失败：{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(StartMenuController), nameof(StartMenuController.Start))]
    public class HeroTagDatabasePatch
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

        // 在开始菜单初始化时注册自定义天赋，确保创建人物时也能看到这些天赋。
        [HarmonyPostfix]
        public static void Postfix()
        {
            List<HeroTagDataBase> heroTags = GameDataController.Instance?.heroTagDataBase;
            if (heroTags is not { Count: > 0 })
            {
                Main.LOG.Msg("HeroTag 数据库为空，已跳过自定义天赋注册。");
                return;
            }

            int initialCount = heroTags.Count;
            foreach (HeroTagDataBase customTag in BuildCustomHeroTags(heroTags))
            {
                RegisterHeroTag(heroTags, customTag);
            }

            Main.LOG.Msg($"HeroTag 数据库处理完成，数量：{initialCount} -> {heroTags.Count}");
        }

        // 把配置文件里的天赋定义转换成运行时可注册的 HeroTagDataBase 列表。
        private static List<HeroTagDataBase> BuildCustomHeroTags(List<HeroTagDataBase> existingHeroTags)
        {
            List<HeroTagDataBase> customTags = new List<HeroTagDataBase>();

            foreach (CustomHeroTagDefinition definition in LoadCustomHeroTagDefinitions(existingHeroTags))
            {
                HeroTagDataBase tag = CreateHeroTag(
                    value: definition.Value,
                    name: definition.Name,
                    sameMeaning: definition.SameMeaning,
                    oppositeMeaning: definition.OppositeMeaning,
                    category: definition.Category,
                    order: definition.Order,
                    canRandom: definition.CanRandom,
                    showRightLine: definition.ShowRightLine,
                    effectTarget: definition.EffectTarget,
                    requirements: CreateStringList(definition.Requirements),
                    replaceTags: CreateStringList(definition.ReplaceTags),
                    rawEffectText: definition.RawEffectText);

                customTags.Add(tag);
            }

            return customTags;
        }

        // 从 UserData 下的 CSV 读取自定义天赋定义，并跳过空行或注释行。
        private static CustomHeroTagDefinition[] LoadCustomHeroTagDefinitions(List<HeroTagDataBase> existingHeroTags)
        {
            if (!File.Exists(Main.ConfigFilePath))
            {
                Main.LOG.Warning($"未找到天赋配置文件：{Main.ConfigFilePath}");
                return Array.Empty<CustomHeroTagDefinition>();
            }

            try
            {
                string[] lines = ReadAllLinesWithRetry(Main.ConfigFilePath);
                if (lines.Length <= 1)
                {
                    Main.LOG.Warning("天赋配置文件为空。");
                    return Array.Empty<CustomHeroTagDefinition>();
                }

                string[] headers = SplitCsvLine(lines[0]);
                for (int i = 0; i < headers.Length; i++)
                {
                    headers[i] = headers[i].Trim('\uFEFF', ' ', '\t');
                }

                if (!ValidateHeaders(headers))
                {
                    LogCsvFormatHints(lines[0], headers);
                    Main.LOG.Warning("天赋配置文件存在致命错误，本次未加载任何自定义天赋。");
                    return Array.Empty<CustomHeroTagDefinition>();
                }

                System.Collections.Generic.List<CustomHeroTagDefinition> definitions = new System.Collections.Generic.List<CustomHeroTagDefinition>();
                System.Collections.Generic.HashSet<string> seenNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int rowIndex = 1; rowIndex < lines.Length; rowIndex++)
                {
                    string line = lines[rowIndex].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    CustomHeroTagDefinition definition = ParseDefinition(headers, cells, rowIndex + 1);
                    if (definition != null)
                    {
                        if (!seenNames.Add(definition.Name))
                        {
                            Main.LOG.Warning($"已跳过第 {rowIndex + 1} 行：名称“{definition.Name}”重复。");
                            continue;
                        }

                        definitions.Add(definition);
                    }
                }

                ValidateCrossReferences(definitions, existingHeroTags);

                Main.LOG.Msg($"已从配置文件加载 {definitions.Count} 个自定义天赋。");
                return definitions.ToArray();
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"读取天赋配置文件失败：{ex}");
                return Array.Empty<CustomHeroTagDefinition>();
            }
        }

        // 以共享读取方式打开 CSV，并在短时间内重试几次，尽量规避编辑器或同步工具的瞬时占用。
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

            throw new IOException(
                $"Unable to read hero tag config after {maxAttempts} attempts. The file may still be locked by another process: {filePath}",
                lastIOException);
        }

        // 使用 ReadWrite 共享模式读取整份文本，避免普通只读打开也被其他程序误伤。
        private static string[] ReadAllLinesShared(string filePath)
        {
            using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine() ?? string.Empty);
            }

            return lines.ToArray();
        }

        // 校验 CSV 表头是否齐全，缺少关键列时直接视为致命错误。
        private static bool ValidateHeaders(string[] headers)
        {
            bool valid = true;
            foreach (string requiredColumn in RequiredColumns)
            {
                bool columnExists = false;
                foreach (string header in headers)
                {
                    if (string.Equals(header, requiredColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }

                if (columnExists)
                {
                    continue;
                }

                Main.LOG.Error($"天赋配置文件缺少必需列：{requiredColumn}");
                valid = false;
            }

            return valid;
        }

        // 当表头校验失败时，额外提示几种最常见的 CSV 格式问题，方便用户自查。
        private static void LogCsvFormatHints(string headerLine, string[] headers)
        {
            if (headers.Length == 1 && headerLine.Contains(';'))
            {
                Main.LOG.Warning("天赋配置文件看起来被保存成了分号分隔的 CSV，请保持列分隔符为英文逗号。");
            }

            if (LooksLikeWrongEncoding(headerLine))
            {
                Main.LOG.Warning("天赋配置文件可能不是 UTF-8 编码，请将 HeroTagData.csv 重新保存为 UTF-8。");
            }

            Main.LOG.Warning("如果你用 Excel 或 WPS 编辑过该文件，请检查 CSV 分隔符、编码和表头是否被改动。");
        }

        // 粗略识别常见乱码特征，命中时只做提示，不直接阻止加载。
        private static bool LooksLikeWrongEncoding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.Contains('�')
                || text.Contains("锘")
                || text.Contains("å")
                || text.Contains("ä")
                || text.Contains("æ")
                || text.Contains("ç")
                || text.Contains("é");
        }

        // 按列名解析一行 CSV，组装成内部使用的天赋定义对象。
        private static CustomHeroTagDefinition ParseDefinition(string[] headers, string[] cells, int rowNumber)
        {
            if (cells.Length > headers.Length)
            {
                Main.LOG.Warning($"第 {rowNumber} 行的列数多于表头，多余内容将被忽略。");
            }

            string name = GetCell(headers, cells, "名称");
            if (string.IsNullOrWhiteSpace(name))
            {
                Main.LOG.Warning($"已跳过第 {rowNumber} 行：名称不能为空。");
                return null;
            }

            int value = ParseInt(GetCell(headers, cells, "价值"), 1);
            if (value < 0)
            {
                Main.LOG.Warning($"已跳过第 {rowNumber} 行：价值不能为负数。");
                return null;
            }

            int order = ParseInt(GetCell(headers, cells, "展示顺序"), 0);
            if (order < 0)
            {
                Main.LOG.Warning($"第 {rowNumber} 行的展示顺序为负数，已改用 0。");
                order = 0;
            }

            string effectText = GetCell(headers, cells, "效果");
            if (!ValidateEffectText(effectText, rowNumber))
            {
                Main.LOG.Warning($"已跳过第 {rowNumber} 行：效果为空或格式无效。");
                return null;
            }

            string sameMeaning = GetCell(headers, cells, "同义组");
            if (string.IsNullOrWhiteSpace(sameMeaning))
            {
                sameMeaning = name;
            }

            string category = GetCell(headers, cells, "大类");
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "高级";
            }

            return new CustomHeroTagDefinition
            {
                Name = name,
                Value = value,
                EffectTarget = ParseSkillTargetType(GetCell(headers, cells, "影响目标")),
                RawEffectText = effectText,
                SameMeaning = sameMeaning,
                OppositeMeaning = GetCell(headers, cells, "反义组"),
                CanRandom = ParseBool(GetCell(headers, cells, "可随机")),
                Order = order,
                Requirements = SplitListCell(GetCell(headers, cells, "领悟需求")),
                ReplaceTags = SplitListCell(GetCell(headers, cells, "替换")),
                Category = category,
                ShowRightLine = ParseBool(GetCell(headers, cells, "显示箭头"), defaultValue: true),
            };
        }

        // 校验需求与替换引用是否指向合法天赋名，合法范围同时包含原版天赋和当前 CSV 自定义天赋。
        private static void ValidateCrossReferences(System.Collections.Generic.List<CustomHeroTagDefinition> definitions, List<HeroTagDataBase> existingHeroTags)
        {
            System.Collections.Generic.HashSet<string> names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // 收集原版天赋的同义组，用于检测自定义天赋是否与原版存在同义组冲突。
            System.Collections.Generic.HashSet<string> existingSameMeanings = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existingHeroTags != null)
            {
                foreach (HeroTagDataBase heroTag in existingHeroTags)
                {
                    if (heroTag == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(heroTag.name))
                    {
                        names.Add(heroTag.name);
                    }

                    if (!string.IsNullOrWhiteSpace(heroTag.sameMeaning))
                    {
                        existingSameMeanings.Add(heroTag.sameMeaning);
                    }
                }
            }

            foreach (CustomHeroTagDefinition definition in definitions)
            {
                names.Add(definition.Name);
            }

            foreach (CustomHeroTagDefinition definition in definitions)
            {
                ValidateReferenceList(definition.Name, "领悟需求", definition.Requirements, names);
                ValidateReferenceList(definition.Name, "替换", definition.ReplaceTags, names);

                // 同义组与原版重复时，游戏会把两个天赋视为互斥，导致其中一个无法点出。
                if (!string.IsNullOrWhiteSpace(definition.SameMeaning)
                    && existingSameMeanings.Contains(definition.SameMeaning))
                {
                    Main.LOG.Warning(
                        $"天赋 [{definition.Name}] 的同义组 [{definition.SameMeaning}] 与原版天赋存在冲突，" +
                        "不推荐这样配置：会导致原版或自定义天赋无法正常点出。");
                }
            }
        }

        // 对单个引用列表逐项检查，发现坏引用时只报警，不阻止整份配置继续加载。
        private static void ValidateReferenceList(string ownerName, string columnName, string[] references, System.Collections.Generic.HashSet<string> validNames)
        {
            foreach (string reference in references)
            {
                string normalizedReference = ExtractReferencedTagName(reference);
                if (string.IsNullOrWhiteSpace(normalizedReference))
                {
                    continue;
                }

                if (validNames.Contains(normalizedReference))
                {
                    continue;
                }

                Main.LOG.Warning($"天赋“{ownerName}”中的“{columnName}”引用不存在：\"{reference}\"。");
            }
        }

        // 兼容“天赋:xxx”这类写法，提取出真正的天赋名。对于属性条件（如“经脉100”），过滤掉以避免不必要的警告。
        private static string ExtractReferencedTagName(string reference)
        {
            string normalized = reference?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            int tagMarkerIndex = normalized.LastIndexOf("天赋:", StringComparison.Ordinal);
            if (tagMarkerIndex >= 0)
            {
                return normalized[(tagMarkerIndex + 3)..].Trim();
            }

            // 因为 SplitListCell 已经按 '+' 号正确拆分，传进来的直接是单个条件（如“经脉100”或“内功120”）
            // 启发式过滤：如果它没有“天赋:”前缀，并且以数字结尾，则通常是数值属性要求，不应作为天赋名强行验证
            if (normalized.Length > 0 && char.IsDigit(normalized[normalized.Length - 1]))
            {
                return string.Empty;
            }

            return normalized;
        }

        // 对效果列做基础校验：至少要有一条非空效果文本。
        private static bool ValidateEffectText(string effectText, int rowNumber)
        {
            // SplitListCell 已使用 RemoveEmptyEntries，返回的条目不会是空字符串。
            return SplitListCell(effectText).Length > 0;
        }


        // 解析一行 CSV，支持双引号包裹字段以及字段内逗号、双引号转义。
        private static string[] SplitCsvLine(string line)
        {
            System.Collections.Generic.List<string> cells = new System.Collections.Generic.List<string>();
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

        // 根据表头名称读取某一列的值，避免依赖固定列号。
        private static string GetCell(string[] headers, string[] cells, string columnName)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (!string.Equals(headers[i], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i >= cells.Length)
                {
                    return string.Empty;
                }

                return cells[i].Trim();
            }

            return string.Empty;
        }

        // 解析整数列，失败时回退到调用方传入的默认值。
        private static int ParseInt(string text, int defaultValue)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : defaultValue;
        }

        // 兼容 1/0、TRUE/FALSE、YES/NO 等常见布尔写法。
        private static bool ParseBool(string text, bool defaultValue = false)
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

        // 解析影响目标列，缺省时默认按 Self 处理。
        private static SkillTargetType ParseSkillTargetType(string text)
        {
            return Enum.TryParse(text, ignoreCase: true, out SkillTargetType effectTarget)
                ? effectTarget
                : SkillTargetType.Self;
        }

        // 用分号或竖线拆分列表列，适用于效果、需求、替换等多值字段。
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

        // 把普通字符串数组转成 Il2Cpp List<string>，方便直接塞给游戏数据结构。
        private static List<string> CreateStringList(string[] values)
        {
            List<string> list = new List<string>();
            if (values == null)
            {
                return list;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value);
                }
            }

            return list;
        }

        // 根据解析后的字段创建游戏实际使用的 HeroTagDataBase，并写入 buffData。
        private static HeroTagDataBase CreateHeroTag(
            int value,
            string name,
            string sameMeaning,
            string oppositeMeaning,
            string category,
            int order,
            bool canRandom,
            bool showRightLine,
            SkillTargetType effectTarget,
            List<string> requirements,
            List<string> replaceTags,
            string rawEffectText)
        {
            HeroSpeAddData buffData = null;
            if (!string.IsNullOrWhiteSpace(rawEffectText) && GameDataController.Instance != null)
            {
                try
                {
                    buffData = GameDataController.Instance.StringToSpeAddData(rawEffectText);
                }
                catch (Exception ex)
                {
                    Main.LOG.Warning($"解析效果文本失败：\"{rawEffectText}\"，错误：{ex.GetType().Name} - {ex.Message}");
                }
            }

            return new HeroTagDataBase
            {
                value = value,
                name = name,
                sameMeaning = sameMeaning,
                oppositeMeaning = oppositeMeaning,
                category = category,
                order = order,
                canRandom = canRandom,
                showRightLine = showRightLine,
                effectTarget = effectTarget,
                requirement = requirements ?? new List<string>(),
                replaceTag = replaceTags ?? new List<string>(),
                buffData = buffData ?? new HeroSpeAddData(),
            };
        }


        // 把天赋注册进数据库；若同名天赋已存在则跳过。
        private static void RegisterHeroTag(List<HeroTagDataBase> heroTags, HeroTagDataBase customTag)
        {
            if (customTag == null)
            {
                return;
            }

            for (int i = 0; i < heroTags.Count; i++)
            {
                HeroTagDataBase existingTag = heroTags[i];
                if (existingTag == null)
                {
                    continue;
                }

                if (existingTag.name == customTag.name)
                {
                    Main.LOG.Msg($"HeroTag 已存在，跳过注册：id={i}, 名称={customTag.name}");
                    return;
                }
            }

            heroTags.Add(customTag);
            Main.LOG.Msg($"已注册 HeroTag：{customTag.name}");
        }
    }
}
