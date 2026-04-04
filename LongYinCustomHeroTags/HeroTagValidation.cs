using System;
using System.Globalization;
using System.IO;

namespace LongYinCustomHeroTags
{
    internal static class HeroTagValidation
    {
        internal static System.Collections.Generic.List<CustomHeroTagDefinition> BuildValidatedDefinitions(
            System.Collections.Generic.List<CustomHeroTagRow> rows)
        {
            var definitions = new System.Collections.Generic.List<CustomHeroTagDefinition>();
            foreach (CustomHeroTagRow row in rows)
            {
                if (TryBuildDefinition(row, out CustomHeroTagDefinition definition))
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        internal static bool IsRowValidForDeferredMerge(CustomHeroTagRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                return false;
            }

            if (row.HasIdColumn
                && (!int.TryParse(row.IdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue) || idValue < 0))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.RawEffectText) || !CanSplitEffectText(row.RawEffectText))
            {
                return false;
            }

            if (!CanSplitListField(row.RequirementText))
            {
                return false;
            }

            if (!CanSplitListField(row.ReplaceText))
            {
                return false;
            }

            return true;
        }

        internal static bool ValidateHeaders(string[] headers, bool hasIdColumn, string[] requiredColumns)
        {
            bool valid = true;
            if (!hasIdColumn)
            {
                Main.LOG.Warning("当前配置未包含 id/编号 列，将按旧版配置兼容读取为新增天赋。");
            }

            foreach (string requiredColumn in requiredColumns)
            {
                if (ContainsHeader(headers, requiredColumn))
                {
                    continue;
                }

                Main.LOG.Error($"天赋配置文件缺少必需列：{requiredColumn}");
                valid = false;
            }

            return valid;
        }

        internal static void LogCsvFormatHints(string headerLine, string[] headers, string filePath)
        {
            if (headers.Length == 1 && headerLine.Contains(';'))
            {
                Main.LOG.Warning($"配置文件看起来被保存成了分号分隔的 CSV，请保持列分隔符为英文逗号：{filePath}");
            }

            if (LooksLikeWrongEncoding(headerLine))
            {
                Main.LOG.Warning($"配置文件表头出现乱码，建议将 {Path.GetFileName(filePath)} 另存为 UTF-8 with BOM。");
            }

            Main.LOG.Warning($"如果你用 Excel 或 WPS 编辑过该文件，请检查 CSV 分隔符和表头是否被改动：{filePath}");
        }

        internal static void ValidateCrossReferences(
            System.Collections.Generic.List<CustomHeroTagDefinition> definitions,
            System.Collections.Generic.HashSet<string> validNames,
            System.Collections.Generic.Dictionary<int, string> vanillaSameMeanings)
        {
            foreach (CustomHeroTagDefinition definition in definitions)
            {
                ValidateRequirementReferences(definition.Name, definition.Requirements, validNames);
                ValidateReplaceReferences(definition.Name, definition.ReplaceTags, validNames);
                ValidateSameMeaningConflict(definition, vanillaSameMeanings);
            }
        }

        private static bool TryBuildDefinition(CustomHeroTagRow row, out CustomHeroTagDefinition definition)
        {
            definition = null;
            if (row.HasExtraCells)
            {
                Main.LOG.Warning($"{row.SourceName} 第 {row.RowNumber} 行的列数多于表头，多余内容将被忽略。");
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                Main.LOG.Warning($"已跳过 {row.SourceName} 第 {row.RowNumber} 行：名称不能为空。");
                return false;
            }

            int? parsedId = null;
            if (row.HasIdColumn)
            {
                if (!int.TryParse(row.IdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue) || idValue < 0)
                {
                    Main.LOG.Warning($"已跳过 {row.SourceName} 第 {row.RowNumber} 行：id 必须是大于等于 0 的整数。");
                    return false;
                }

                parsedId = idValue;
            }

            int value = HeroTagResourcePatch.ParseInt(row.ValueText, 1);

            int order = HeroTagResourcePatch.ParseInt(row.OrderText, 0);
            if (order < 0)
            {
                Main.LOG.Warning($"{row.SourceName} 第 {row.RowNumber} 行的展示顺序为负数，已改用 0。");
                order = 0;
            }

            if (!TrySplitEffectText(row.RawEffectText, row.SourceName, row.RowNumber, out _))
            {
                Main.LOG.Warning($"已跳过 {row.SourceName} 第 {row.RowNumber} 行：效果为空或格式无效。");
                return false;
            }

            if (!TrySplitListField(row.RequirementText, "领悟需求", row.SourceName, row.RowNumber, out string[] requirements))
            {
                return false;
            }

            if (!TrySplitListField(row.ReplaceText, "替换", row.SourceName, row.RowNumber, out string[] replaceTags))
            {
                return false;
            }

            string sameMeaning = row.SameMeaning;
            if (string.IsNullOrWhiteSpace(sameMeaning))
            {
                sameMeaning = row.Name;
            }

            string category = row.Category;
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "高级";
            }

            definition = new CustomHeroTagDefinition
            {
                SourceId = parsedId,
                SourceName = row.SourceName,
                Name = row.Name,
                Value = value,
                EffectTarget = HeroTagResourcePatch.ParseSkillTargetType(row.EffectTargetText),
                RawEffectText = row.RawEffectText,
                SameMeaning = sameMeaning,
                OppositeMeaning = row.OppositeMeaning,
                CanRandom = HeroTagResourcePatch.ParseBool(row.CanRandomText),
                Order = order,
                Requirements = requirements,
                ReplaceTags = replaceTags,
                Category = category,
                ShowRightLine = HeroTagResourcePatch.ParseBool(row.ShowRightLineText, defaultValue: true),
            };
            return true;
        }

        private static bool ContainsHeader(string[] headers, string requiredColumn)
        {
            foreach (string header in headers)
            {
                if (string.Equals(header, requiredColumn, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

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

        private static bool CanSplitEffectText(string effectText)
        {
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            return !ContainsEmptyDelimitedItem(effectText, ';', '|');
        }

        private static bool CanSplitListField(string text)
        {
            return !ContainsEmptyDelimitedItem(text, ';', '|', '+');
        }

        private static bool TrySplitEffectText(
            string effectText,
            string sourceName,
            int rowNumber,
            out string[] values)
        {
            values = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            if (ContainsEmptyDelimitedItem(effectText, ';', '|'))
            {
                Main.LOG.Warning($"已跳过 {sourceName} 第 {rowNumber} 行：效果包含空项，请检查分隔符。");
                return false;
            }

            values = SplitAndTrim(effectText, ';', '|');
            return values.Length > 0;
        }

        private static bool TrySplitListField(
            string text,
            string columnName,
            string sourceName,
            int rowNumber,
            out string[] values)
        {
            values = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (ContainsEmptyDelimitedItem(text, ';', '|', '+'))
            {
                Main.LOG.Warning($"已跳过 {sourceName} 第 {rowNumber} 行：{columnName} 包含空项，请检查分隔符。");
                return false;
            }

            values = SplitAndTrim(text, ';', '|', '+');
            return true;
        }

        private static bool ContainsEmptyDelimitedItem(string text, params char[] delimiters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] values = text.Split(delimiters, StringSplitOptions.None);
            if (values.Length <= 1)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] SplitAndTrim(string text, params char[] delimiters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            string[] values = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].Trim();
            }

            return values;
        }

        private static void ValidateRequirementReferences(string ownerName, string[] requirements, System.Collections.Generic.HashSet<string> validNames)
        {
            foreach (string requirement in requirements)
            {
                string normalizedReference = ExtractReferencedTagName(requirement);
                if (string.IsNullOrWhiteSpace(normalizedReference))
                {
                    continue;
                }

                if (validNames.Contains(normalizedReference))
                {
                    continue;
                }

                Main.LOG.Warning($"天赋“{ownerName}”中的“领悟需求”引用不存在：\"{requirement}\"。");
            }
        }

        private static void ValidateReplaceReferences(string ownerName, string[] replaceTags, System.Collections.Generic.HashSet<string> validNames)
        {
            foreach (string replaceTag in replaceTags)
            {
                string normalizedReference = NormalizeReplaceReference(replaceTag);
                if (string.IsNullOrWhiteSpace(normalizedReference))
                {
                    continue;
                }

                if (int.TryParse(normalizedReference, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                if (validNames.Contains(normalizedReference))
                {
                    continue;
                }

                Main.LOG.Warning($"天赋“{ownerName}”中的“替换”引用不存在：\"{replaceTag}\"。");
            }
        }

        private static void ValidateSameMeaningConflict(
            CustomHeroTagDefinition definition,
            System.Collections.Generic.Dictionary<int, string> vanillaSameMeanings)
        {
            if (string.IsNullOrWhiteSpace(definition.SameMeaning))
            {
                return;
            }

            if (OverridesVanillaTagWithSameMeaning(definition, vanillaSameMeanings))
            {
                return;
            }

            foreach (System.Collections.Generic.KeyValuePair<int, string> pair in vanillaSameMeanings)
            {
                if (!string.Equals(pair.Value, definition.SameMeaning, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (definition.SourceId.HasValue && definition.SourceId.Value == pair.Key)
                {
                    return;
                }

                Main.LOG.Warning(
                    $"天赋 [{definition.Name}] 的同义组 [{definition.SameMeaning}] 与原版天赋存在冲突，不推荐这样配置：会导致原版或自定义天赋无法正常点出。");
                return;
            }
        }

        private static bool OverridesVanillaTagWithSameMeaning(
            CustomHeroTagDefinition definition,
            System.Collections.Generic.Dictionary<int, string> vanillaSameMeanings)
        {
            if (!definition.SourceId.HasValue)
            {
                return false;
            }

            return vanillaSameMeanings.TryGetValue(definition.SourceId.Value, out string originalSameMeaning)
                && string.Equals(originalSameMeaning, definition.SameMeaning, StringComparison.OrdinalIgnoreCase);
        }

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

            if (normalized.Length > 0 && char.IsDigit(normalized[normalized.Length - 1]))
            {
                return string.Empty;
            }

            return normalized;
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
    }
}
