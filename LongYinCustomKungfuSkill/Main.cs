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
using Il2CppSystem.Collections.Generic;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinCustomKungfuSkill.Main), "LongYinCustomKungfuSkill", "2.0.1", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomKungfuSkill
{
    // ─── CSV 行解析结果 ─────────────────────────────────────────────────────────

    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(0, string.Empty);

        public LoadedConfigData(int rowCount, string loaderCsv)
        {
            RowCount = rowCount;
            LoaderCsv = loaderCsv;
        }

        public int RowCount { get; }

        public string LoaderCsv { get; }
    }

    // ─── Mod 入口 ────────────────────────────────────────────────────────────────

    public class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomKungfuSkill");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "KungfuSkillData.csv");
        internal static readonly string VanillaExportPath = Path.Combine(ConfigDir, "vanilla_KungfuSkillData.csv");
        internal static readonly string IconDir = Path.Combine(ConfigDir, "Icons");
        internal static int VanillaKungFuMaxId { get; set; } = -1;
        internal static int VanillaKungFuTotalCount { get; set; } = 0;

        private const string TargetResourcePath = "GameData/KungFuData";
        private const string SpeHeroResourcePath = "GameData/SpeHeroData";
        private static readonly System.Collections.Generic.HashSet<int> InjectedCustomSkillIds = new();
        private static readonly System.Collections.Generic.Dictionary<string, string> RenamedVanillaSkillNames = new(StringComparer.Ordinal);

        /// <summary>自定义技能 ID 到重定向图标 ID 的映射。</summary>
        internal static readonly System.Collections.Generic.Dictionary<string, string> CustomSkillIdToIconId = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> VanillaSkillIdsByType = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.Dictionary<string, int> CustomSkillIconAssignCountByType = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.Dictionary<string, Sprite> CustomSkillIconCache = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.HashSet<string> CheckedCustomSkillIconIds = new(StringComparer.Ordinal);
        private static LoadedConfigData _cachedConfigData = LoadedConfigData.Empty;
        private static DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;

        // CSV 模板：默认包含一条原版覆盖数据和多条不同类型的新增功法测试数据
        private static readonly string DefaultCsv =
            "id,类别,等级,名字,描述,基础威力,经验系数,修炼效果,运功效果,威力系数,修炼需求,攻击范围,伤害范围,使用特效,内力消耗,所属势力,使用目标,攻击架势,防御架势,召唤ID,使用次数,动作,子弹,特效,使用武器,伤害顺序,角色位移,Trail,隐藏\r\n" +
            "9,拳掌,5,太祖长拳·改,相传本朝太祖所创的基础拳法,10,1.1,拳掌1,,力道2;拳掌1;内力0.3,,\"0,0,4\",\"0,0,1\",伤害0.04,5,-1,0,胸15,胸10,,,skill0_0,,打击5-Target-Grid-Happen,拳,Same,FALSE,0,1\r\n" +
            "996,拳掌,5,太祖不短拳,相传本朝太祖所创的基础拳法,10,1.1,拳掌1,,力道2;拳掌1;内力0.3,,\"1,1,1\",\"0,0,0\",伤害0.04,5,0,0,胸15,胸10,,,skill0_0,,打击5-Target-Grid-Happen,拳,Same,FALSE,0,1\r\n" +
            "997,内功,5,太祖吐纳法,呼出浊气，吸入清气，一吐一纳之间，内息造化而成,,0.9,内功1;经脉1,生命上限20;内力上限20;内功4;智力4;经脉4,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "998,轻功,5,太祖飞毛腿,源于古代递送紧急军情“羽檄”之兵士的步法,,1,灵巧1,轻功4;灵巧4;速度0.04,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "999,绝技,5,太祖铁布衫,基本硬功之一，修习者周身致密坚韧，能够抵御些许来袭之力道,,1,绝技1,体质4;护甲2;减伤2,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "1000,剑法,5,太祖御剑术,击、刺、格、洗四种基础剑招的汇总口诀,10,1.1,剑法1,,灵巧2;剑法1;内力0.1,,\"4,0,0\",\"4,1,2\",命中0.04,5,0,0,手14,手10,,,skill1_1,,劈砍-Target-Grid-Happen,剑,Same,FALSE,0,1\r\n" +
            "1001,刀法,5,太祖拔刀功,拔刀，劈刀，收刀，只此最简单的动作，却是一切刀法之基,10,1.1,刀法1,,体质2;刀法1;内力0.1,,\"4,0,0\",\"5,1,2\",暴击0.04,5,0,0,心14,心10,,,skill2_1,,三连剑影小-Target-Grid-Happen,刀,Same,FALSE,0,1\r\n" +
            "1002,长兵,5,太祖长棍法,相传本朝太祖所创的基础棍法,10,1.1,长兵1,,意志2;长兵1;内力0.1,,\"1,1,1\",\"7,0,0\",连击0.04,5,0,0,头15,头10,,,skill3_1,,打击5-Target-Grid-Happen,长兵,Same,FALSE,0,1\r\n" +
            "1003,奇门,5,太祖三路鞭,分为上中下三路的基本鞭法,10,1.1,奇门1,,智力2;奇门1;内力0.1,,\"3,1,1\",\"7,0,0\",震脑0.04,5,0,0,头14,头6,,,skill4_3,,乱刃-Target-Grid-Happen,鞭,Same,FALSE,0,1\r\n" +
            "1004,射术,5,太祖飞镖诀,一门使用飞镖的基础手法,10,1.1,射术1,,灵巧2;射术1;内力0.1,,\"0,2,3\",\"0,0,0\",流血0.04,5,0,0,腹20,腹3,,,skill5_0,1-----0.9,三连劈砍-Target-Grid-Happen,,Same,FALSE,0,1\r\n";

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            LOG.Msg($"LongYinCustomKungfuSkill 已加载，配置文件：{ConfigPath}");
        }

        internal static void EnsureConfigExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                Directory.CreateDirectory(IconDir);
                IconExportManager.EnsureConfigExists();
                if (!File.Exists(ConfigPath))
                {
                    File.WriteAllText(ConfigPath, DefaultCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"已生成默认配置文件：{ConfigPath}");
                }
            }
            catch (Exception ex)
            {
                LOG.Error($"准备配置文件失败：{ex}");
            }
        }

        internal static bool ShouldTraceResourcePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(path.Trim(), TargetResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldTraceSpeHeroResourcePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(path.Trim(), SpeHeroResourcePath, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ResetInjectedCustomSkillIds()
        {
            InjectedCustomSkillIds.Clear();
        }

        internal static void TrimInjectedCustomSkillState(int firstMissingId)
        {
            if (firstMissingId < 0)
            {
                return;
            }

            InjectedCustomSkillIds.RemoveWhere(skillId => skillId >= firstMissingId);

            var keysToRemove = CustomSkillIdToIconId.Keys
                .Where(key => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) && skillId >= firstMissingId)
                .ToList();

            foreach (string key in keysToRemove)
            {
                CustomSkillIdToIconId.Remove(key);
            }
        }

        internal static void RegisterInjectedCustomSkillId(int skillId)
        {
            if (skillId >= 0)
            {
                InjectedCustomSkillIds.Add(skillId);
            }
        }

        internal static void ResetVanillaSkillIdsByType()
        {
            VanillaSkillIdsByType.Clear();
            CustomSkillIconAssignCountByType.Clear();
        }

        internal static void RegisterVanillaSkillType(string skillType, int skillId)
        {
            if (string.IsNullOrWhiteSpace(skillType) || skillId < 0)
            {
                return;
            }

            if (!VanillaSkillIdsByType.TryGetValue(skillType, out System.Collections.Generic.List<int> skillIds))
            {
                skillIds = new System.Collections.Generic.List<int>();
                VanillaSkillIdsByType[skillType] = skillIds;
            }

            if (!skillIds.Contains(skillId))
            {
                skillIds.Add(skillId);
            }
        }

        internal static string GetSuggestedIconIdForType(string skillType)
        {
            if (!string.IsNullOrWhiteSpace(skillType) &&
                VanillaSkillIdsByType.TryGetValue(skillType, out System.Collections.Generic.List<int> skillIds) &&
                skillIds.Count > 0)
            {
                CustomSkillIconAssignCountByType.TryGetValue(skillType, out int assignedCount);
                int iconSkillId = skillIds[assignedCount % skillIds.Count];
                CustomSkillIconAssignCountByType[skillType] = assignedCount + 1;
                return iconSkillId.ToString(CultureInfo.InvariantCulture);
            }

            return "0";
        }

        internal static void ExportVanillaSkillsFromResource(TextAsset textAsset)
        {
            if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(VanillaExportPath, textAsset.text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                LOG.Msg($"已将【实时提取】的完整版原版功法数据释放到：{VanillaExportPath}");
                if (IconExportManager.IsDebugIconExportEnabled())
                {
                    LOG.Msg($"已在配置文件 LongYinCustomKungfuSkill.cfg 中开启原版功法图标导出。请进入一次游戏世界或载入一次存档，触发图标加载后会导出到：{IconExportManager.VanillaIconExportDir}");
                }
                else
                {
                    LOG.Msg("如需导出原版功法图标，可在配置文件 LongYinCustomKungfuSkill.cfg 中将 EnableExportVanillaIcons 改为 true。");
                }
            }
            catch (Exception ex)
            {
                LOG.Warning($"释放原版功法模板失败：{ex.GetType().Name} - {ex.Message}");
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

        internal static bool TryLoadCustomSkillIconSprite(string skillId, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return false;
            }

            if (CustomSkillIconCache.TryGetValue(skillId, out Sprite cachedSprite) && cachedSprite != null)
            {
                sprite = cachedSprite;
                return true;
            }

            if (CheckedCustomSkillIconIds.Contains(skillId))
            {
                return false;
            }

            string iconPath = Path.Combine(IconDir, $"{skillId}.png");
            if (!File.Exists(iconPath))
            {
                CheckedCustomSkillIconIds.Add(skillId);
                return false;
            }

            try
            {
                byte[] pngBytes = File.ReadAllBytes(iconPath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, pngBytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    CheckedCustomSkillIconIds.Add(skillId);
                    LOG.Warning($"读取自定义图标失败：skillID={skillId}, path={iconPath}");
                    return false;
                }

                texture.name = $"LongYinCustomSkillIcon_{skillId}";
                texture.wrapMode = TextureWrapMode.Clamp;
                var rect = new Rect(0f, 0f, texture.width, texture.height);
                var pivot = new Vector2(0.5f, 0.5f);
                Sprite loadedSprite = Sprite.Create(texture, rect, pivot, 100f);
                loadedSprite.name = texture.name;
                CustomSkillIconCache[skillId] = loadedSprite;
                CheckedCustomSkillIconIds.Add(skillId);
                sprite = loadedSprite;
                return true;
            }
            catch (Exception ex)
            {
                CheckedCustomSkillIconIds.Add(skillId);
                LOG.Warning($"加载自定义图标异常：skillID={skillId}, path={iconPath}，{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        internal static bool HasCustomSkillIconFile(string skillId)
        {
            return !string.IsNullOrWhiteSpace(skillId) &&
                   File.Exists(Path.Combine(IconDir, $"{skillId}.png"));
        }

        internal static string GetCustomSkillIconPath(string skillId)
        {
            return string.IsNullOrWhiteSpace(skillId)
                ? string.Empty
                : Path.Combine("UserData", "LongYinCustomKungfuSkill", "Icons", $"{skillId}.png");
        }

        internal static void UpdateRenamedVanillaSkillNames(string vanillaText)
        {
            RenamedVanillaSkillNames.Clear();

            if (string.IsNullOrWhiteSpace(vanillaText))
            {
                return;
            }

            LoadedConfigData configData = LoadConfigData();
            if (configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.LoaderCsv))
            {
                return;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.LoaderCsv.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length <= 1 || customLines.Length <= 1)
            {
                return;
            }

            string[] vanillaHeaders = SplitCsvLine(vanillaLines[0]);
            string[] customHeaders = SplitCsvLine(customLines[0]);
            int vanillaIdCol = FindColumnIndex(vanillaHeaders, "id");
            int vanillaNameCol = FindColumnIndex(vanillaHeaders, "名字");
            int customIdCol = FindColumnIndex(customHeaders, "id");
            int customNameCol = FindColumnIndex(customHeaders, "名字");
            if (vanillaIdCol < 0 || vanillaNameCol < 0 || customIdCol < 0 || customNameCol < 0)
            {
                return;
            }

            var vanillaNameById = new System.Collections.Generic.Dictionary<int, string>();
            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (int.TryParse(GetCell(cells, vanillaIdCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId))
                {
                    string skillName = GetCell(cells, vanillaNameCol);
                    if (!string.IsNullOrWhiteSpace(skillName))
                    {
                        vanillaNameById[skillId] = skillName;
                    }
                }
            }

            for (int i = 1; i < customLines.Length; i++)
            {
                string line = customLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                if (!int.TryParse(GetCell(cells, customIdCol), NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId))
                {
                    continue;
                }

                if (!vanillaNameById.TryGetValue(skillId, out string vanillaName) || string.IsNullOrWhiteSpace(vanillaName))
                {
                    continue;
                }

                string customName = GetCell(cells, customNameCol);
                if (string.IsNullOrWhiteSpace(customName) || string.Equals(customName, vanillaName, StringComparison.Ordinal))
                {
                    continue;
                }

                RenamedVanillaSkillNames[vanillaName] = customName;
            }
        }

        internal static string BuildPatchedSpeHeroText(string speHeroText)
        {
            if (string.IsNullOrWhiteSpace(speHeroText) || RenamedVanillaSkillNames.Count == 0)
            {
                return null;
            }

            string replacedText = speHeroText;
            foreach (var pair in RenamedVanillaSkillNames)
            {
                string newText = replacedText.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
                if (!string.Equals(newText, replacedText, StringComparison.Ordinal))
                {
                    LOG.Msg(System.ConsoleColor.Cyan, $"SpeHeroData 已同步功法名：{pair.Key} -> {pair.Value}");
                    replacedText = newText;
                }
            }

            return string.Equals(replacedText, speHeroText, StringComparison.Ordinal) ? null : replacedText;
        }

        internal static LoadedConfigData LoadConfigData()
        {
            EnsureConfigExists();

            if (!File.Exists(ConfigPath))
            {
                LOG.Warning($"配置文件不存在：{ConfigPath}");
                return LoadedConfigData.Empty;
            }

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
                if (_cachedConfigData.RowCount >= 0 &&
                    _cachedConfigWriteTimeUtc == lastWriteTimeUtc &&
                    !string.IsNullOrWhiteSpace(_cachedConfigData.LoaderCsv))
                {
                    return _cachedConfigData;
                }

                string[] lines = ReadAllLinesShared(ConfigPath);
                if (lines.Length <= 1)
                {
                    LOG.Warning("配置文件为空，未加载任何功法。");
                    return LoadedConfigData.Empty;
                }

                string[] headers = SplitCsvLine(lines[0]);
                if (!ValidateHeaders(headers))
                {
                    LOG.Error("配置文件表头缺少必要列，已中止加载。");
                    return LoadedConfigData.Empty;
                }

                int idCol = FindColumnIndex(headers, "id");
                int nameCol = FindColumnIndex(headers, "名字");
                int typeCol = FindColumnIndex(headers, "类别");
                int summonIdCol = FindColumnIndex(headers, "召唤ID");

                var seenIDs = new System.Collections.Generic.HashSet<int>();
                var loaderLines = new System.Collections.Generic.List<string> { lines[0] };
                int rowCount = 0;

                for (int row = 1; row < lines.Length; row++)
                {
                    string line = lines[row].Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    int parsedSkillId = ParseRow(cells, row + 1, idCol, nameCol);
                    if (parsedSkillId < 0)
                    {
                        continue;
                    }

                    if (!seenIDs.Add(parsedSkillId))
                    {
                        LOG.Warning($"第 {row + 1} 行：功法 ID={parsedSkillId} 重复，已跳过。");
                        continue;
                    }

                    rowCount++;
                    loaderLines.Add(line);
                }

                LOG.Msg($"从 CSV 加载了 {rowCount} 条覆盖配置。");
                LoadedConfigData loadedData = new(rowCount, string.Join("\r\n", loaderLines) + "\r\n");
                _cachedConfigData = loadedData;
                _cachedConfigWriteTimeUtc = lastWriteTimeUtc;
                return loadedData;
            }
            catch (Exception ex)
            {
                LOG.Error($"读取配置文件时发生异常：{ex}");
                return LoadedConfigData.Empty;
            }
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

        internal static string[] SplitCsvLine(string line)
        {
            var cells = new System.Collections.Generic.List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
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

                if (c == ',' && !inQuotes)
                {
                    cells.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            cells.Add(current.ToString());

            if (inQuotes)
            {
                LOG.Warning($"CSV 行引号未正确闭合：{line}");
            }

            return cells.ToArray();
        }

        internal static bool ValidateContinuousSkillIds(System.Collections.Generic.List<string> outputLines, int idCol, out string errorMessage, out int firstMissingId)
        {
            errorMessage = null;
            firstMissingId = -1;
            if (outputLines == null || outputLines.Count <= 1 || idCol < 0)
            {
                return true;
            }

            var sortedIds = new System.Collections.Generic.List<int>();
            for (int i = 1; i < outputLines.Count; i++)
            {
                string line = outputLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string idText = GetCell(cells, idCol);
                if (int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId))
                {
                    sortedIds.Add(skillId);
                }
            }

            if (sortedIds.Count <= 1)
            {
                return true;
            }

            sortedIds.Sort();
            var missingIds = new System.Collections.Generic.List<int>();
            for (int i = 1; i < sortedIds.Count; i++)
            {
                int previousId = sortedIds[i - 1];
                int currentId = sortedIds[i];
                if (currentId == previousId)
                {
                    firstMissingId = currentId;
                    errorMessage = $"功法 ID 不允许重复，检测到重复 ID：{currentId}。重复 ID {currentId} 之后的功法均未注入。";
                    return false;
                }

                if (currentId == previousId + 1)
                {
                    continue;
                }

                for (int missingId = previousId + 1; missingId < currentId; missingId++)
                {
                    missingIds.Add(missingId);
                    if (missingIds.Count >= 16)
                    {
                        break;
                    }
                }

                if (missingIds.Count >= 16)
                {
                    break;
                }
            }

            if (missingIds.Count == 0)
            {
                return true;
            }

            firstMissingId = missingIds[0];
            errorMessage = $"功法 ID 必须连续，检测到缺失 ID：{string.Join(", ", missingIds)}";
            if (missingIds.Count >= 16)
            {
                errorMessage += " ...";
            }

            errorMessage += $"。缺失 ID {firstMissingId} 之后的功法均未注入。";

            return false;
        }

        internal static System.Collections.Generic.List<string> TrimOutputLinesBeforeMissingId(System.Collections.Generic.List<string> outputLines, int idCol, int firstMissingId)
        {
            if (outputLines == null || outputLines.Count == 0 || idCol < 0 || firstMissingId < 0)
            {
                return outputLines;
            }

            var trimmedLines = new System.Collections.Generic.List<string> { outputLines[0] };
            for (int i = 1; i < outputLines.Count; i++)
            {
                string line = outputLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] cells = SplitCsvLine(line);
                string idText = GetCell(cells, idCol);
                if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) || skillId < firstMissingId)
                {
                    trimmedLines.Add(line);
                }
            }

            return trimmedLines;
        }

        internal static string GetCell(string[] cells, int index)
        {
            return index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        private static int ParseRow(string[] cells, int rowNum, int idCol, int nameCol)
        {
            try
            {
                int skillID = ParseInt(GetCell(cells, idCol), -1);
                if (skillID < 0)
                {
                    LOG.Warning($"第 {rowNum} 行：功法 ID 无效，已跳过。");
                    return -1;
                }

                string name = GetCell(cells, nameCol);
                if (string.IsNullOrWhiteSpace(name))
                {
                    LOG.Warning($"第 {rowNum} 行：名称为空，已跳过。");
                    return -1;
                }

                return skillID;
            }
            catch (Exception ex)
            {
                LOG.Error($"第 {rowNum} 行解析异常：{ex.GetType().Name} - {ex.Message}");
                return -1;
            }
        }

        private static bool ValidateHeaders(string[] headers)
        {
            string[] requiredColumns = { "id", "名字", "类别", "等级" };
            var headerSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string h in headers)
            {
                headerSet.Add(h.Trim('\uFEFF', ' ', '\t'));
            }

            bool allPresent = true;
            foreach (string column in requiredColumns)
            {
                if (!headerSet.Contains(column))
                {
                    LOG.Error($"配置文件缺少必要列：\"{column}\"");
                    allPresent = false;
                }
            }

            return allPresent;
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
                        LOG.Warning($"[{Path.GetFileName(path)}] 不符合标准 UTF-8 编码，已启动 GBK 容错解析...");
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

        private static int ParseInt(string text, int fallback)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

    }

    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), new[] { typeof(string), typeof(Il2CppSystem.Type) })]
    public static class ResourcesLoadPatch
    {
        private static bool _exportedVanillaKungFuData = false;

        [HarmonyPostfix]
        public static void Postfix(string path, Il2CppSystem.Type systemTypeInstance, ref UnityEngine.Object __result)
        {
            bool isKungFuData = Main.ShouldTraceResourcePath(path);
            bool isSpeHeroData = Main.ShouldTraceSpeHeroResourcePath(path);
            if (!isKungFuData && !isSpeHeroData)
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

                if (isKungFuData)
                {
                    if (!_exportedVanillaKungFuData)
                    {
                        Main.ExportVanillaSkillsFromResource(textAsset);
                        _exportedVanillaKungFuData = true;
                    }

                    Main.UpdateRenamedVanillaSkillNames(textAsset.text);
                    string mergedText = BuildMergedKungFuText(textAsset.text);
                    if (!string.IsNullOrWhiteSpace(mergedText) && !string.Equals(mergedText, textAsset.text, StringComparison.Ordinal))
                    {
                        TextAsset replacement = Main.CreateTextAsset(mergedText, textAsset.name);
                        if (replacement != null)
                        {
                            __result = replacement;
                            Main.LOG.Msg("已将自定义功法配置直接注入 GameData/KungFuData。");
                        }
                        else
                        {
                            Main.LOG.Warning("创建替换用 TextAsset 失败，已跳过资源级功法注入。");
                        }
                    }

                    return;
                }

                if (isSpeHeroData)
                {
                    string patchedText = Main.BuildPatchedSpeHeroText(textAsset.text);
                    if (!string.IsNullOrWhiteSpace(patchedText) && !string.Equals(patchedText, textAsset.text, StringComparison.Ordinal))
                    {
                        TextAsset replacement = Main.CreateTextAsset(patchedText, textAsset.name);
                        if (replacement != null)
                        {
                            __result = replacement;
                            Main.LOG.Msg("已将重命名后的原版功法名称同步注入 GameData/SpeHeroData。");
                        }
                        else
                        {
                            Main.LOG.Warning("创建替换用 TextAsset 失败，已跳过 GameData/SpeHeroData 注入。");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"Resources.Load 结果转 TextAsset 失败：path={path}，异常：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static string BuildMergedKungFuText(string vanillaText)
        {
            if (string.IsNullOrWhiteSpace(vanillaText))
            {
                return null;
            }

            LoadedConfigData configData = Main.LoadConfigData();
            if (configData.RowCount == 0 || string.IsNullOrWhiteSpace(configData.LoaderCsv))
            {
                return null;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] customLines = configData.LoaderCsv.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length == 0 || customLines.Length <= 1)
            {
                return null;
            }

            string[] vanillaHeaders = Main.SplitCsvLine(vanillaLines[0]);
            string[] customHeaders = Main.SplitCsvLine(customLines[0]);
            int vanillaIdCol = Main.FindColumnIndex(vanillaHeaders, "id");
            int vanillaTypeCol = Main.FindColumnIndex(vanillaHeaders, "类别");
            int customIdCol = Main.FindColumnIndex(customHeaders, "id");
            if (vanillaIdCol < 0 || customIdCol < 0)
            {
                return null;
            }

            Main.ResetInjectedCustomSkillIds();
            Main.ResetVanillaSkillIdsByType();
            Main.CustomSkillIdToIconId.Clear();

            int vanillaMaxId = -1;
            int vanillaTotalCount = 0;
            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string vanillaLine = vanillaLines[i];
                if (string.IsNullOrWhiteSpace(vanillaLine))
                {
                    continue;
                }

                string[] vanillaCells = Main.SplitCsvLine(vanillaLine);
                string vanillaIdText = Main.GetCell(vanillaCells, vanillaIdCol);
                if (int.TryParse(vanillaIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVanillaId))
                {
                    vanillaTotalCount++;
                    Main.RegisterVanillaSkillType(Main.GetCell(vanillaCells, vanillaTypeCol), parsedVanillaId);
                    if (parsedVanillaId > vanillaMaxId)
                    {
                        vanillaMaxId = parsedVanillaId;
                    }
                }
            }

            Main.VanillaKungFuMaxId = vanillaMaxId;
            Main.VanillaKungFuTotalCount = vanillaTotalCount;

            var customById = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < customLines.Length; i++)
            {
                string line = customLines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] cells = Main.SplitCsvLine(line);
                string id = Main.GetCell(cells, customIdCol);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    customById[id] = line;
                }
            }

            if (customById.Count == 0)
            {
                return null;
            }

            var outputLines = new System.Collections.Generic.List<string> { vanillaLines[0] };
            var matchedIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var appendedCustomSkillLogs = new System.Collections.Generic.List<(string SkillType, string SkillId, string IconId, bool HasCustomIcon, string CustomIconPath)>();

            for (int i = 1; i < vanillaLines.Length; i++)
            {
                string line = vanillaLines[i];
                
                // 【修改点】：如果是空行，直接跳过，千万别 Add 到 outputLines 里！
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; 
                }

                string[] cells = Main.SplitCsvLine(line);
                string id = Main.GetCell(cells, vanillaIdCol);
                if (!string.IsNullOrWhiteSpace(id) && customById.TryGetValue(id, out string replacementLine))
                {
                    LogMergedRowChanges(vanillaHeaders, line, replacementLine, id);
                    outputLines.Add(replacementLine);
                    matchedIds.Add(id);
                }
                else
                {
                    outputLines.Add(line);
                }
            }
            foreach (string id in customById.Keys
                .OrderBy(key => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId) ? parsedId : int.MaxValue)
                .ThenBy(key => key, StringComparer.Ordinal))
            {
                if (!matchedIds.Contains(id))
                {
                    outputLines.Add(customById[id]);
                    string typeStr = string.Empty;
                    string iconId = null;
                    bool hasCustomIcon = Main.HasCustomSkillIconFile(id);
                    string customIconPath = hasCustomIcon ? Main.GetCustomSkillIconPath(id) : string.Empty;

                    if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int customSkillId))
                    {
                        Main.RegisterInjectedCustomSkillId(customSkillId);

                        // 根据功法类型分配图标
                        string[] cells = Main.SplitCsvLine(customById[id]);
                        int customTypeCol = Main.FindColumnIndex(customHeaders, "类别");
                        typeStr = Main.GetCell(cells, customTypeCol);
                        iconId = Main.GetSuggestedIconIdForType(typeStr);
                        if (!hasCustomIcon && !string.IsNullOrWhiteSpace(iconId))
                        {
                            Main.CustomSkillIdToIconId[id] = iconId;
                        }
                    }

                    appendedCustomSkillLogs.Add((typeStr, id, iconId, hasCustomIcon, customIconPath));
                }
            }

            bool hasContinuousIds = Main.ValidateContinuousSkillIds(outputLines, vanillaIdCol, out string idContinuityError, out int firstMissingId);
            if (!hasContinuousIds)
            {
                Main.LOG.Error($"功法数据合并失败：{idContinuityError}");
                outputLines = Main.TrimOutputLinesBeforeMissingId(outputLines, vanillaIdCol, firstMissingId);
                Main.TrimInjectedCustomSkillState(firstMissingId);
            }

            var visibleAppendedLogs = hasContinuousIds
                ? appendedCustomSkillLogs
                : appendedCustomSkillLogs
                    .Where(x => int.TryParse(x.SkillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) && skillId < firstMissingId)
                    .ToList();

            if (visibleAppendedLogs.Count > 0)
            {
                foreach (var group in visibleAppendedLogs.GroupBy(x => string.IsNullOrWhiteSpace(x.SkillType) ? "未分类" : x.SkillType))
                {
                    Main.LOG.Msg(ConsoleColor.Cyan, $"新增功法[{group.Key}]：");
                    foreach (var item in group)
                    {
                        if (item.HasCustomIcon)
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - skillID={item.SkillId} 检测到自定义图标：{item.CustomIconPath}");
                        }
                        else if (string.IsNullOrWhiteSpace(item.IconId))
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - skillID={item.SkillId}");
                        }
                        else
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - skillID={item.SkillId} 分配图标 ID={item.IconId}");
                        }
                    }
                }
            }

            return string.Join("\r\n", outputLines);
        }

        private static void LogMergedRowChanges(string[] headers, string vanillaLine, string customLine, string skillId)
        {
            string[] vanillaCells = Main.SplitCsvLine(vanillaLine);
            string[] customCells = Main.SplitCsvLine(customLine);
            int cols = Math.Min(headers.Length, Math.Min(vanillaCells.Length, customCells.Length));
            var changes = new System.Collections.Generic.List<string>();

            for (int i = 0; i < cols; i++)
            {
                if (string.Equals(headers[i], "id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string oldVal = vanillaCells[i];
                string newVal = customCells[i];
                if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                {
                    changes.Add($"{headers[i]}: {oldVal} -> {newVal}");
                }
            }

            if (changes.Count == 0)
            {
                Main.LOG.Msg($"功法 skillID={skillId} 未检测到实际字段变更。");
                return;
            }
            Main.LOG.Msg(ConsoleColor.DarkYellow, $"功法 skillID={skillId} 将应用以下变更：");
            foreach (string change in changes)
            {
                Main.LOG.Msg(ConsoleColor.DarkYellow, $"  - {change}");
            }
        }
    }

    [HarmonyPatch(typeof(TextureController), nameof(TextureController.LoadAtlasSprite))]
    public static class TextureControllerLoadAtlasSpritePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string spriteName, ref Sprite __result)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return true;
            }

            if (int.TryParse(spriteName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) &&
                skillId >= 0 &&
                skillId < Main.VanillaKungFuTotalCount)
            {
                return true;
            }

            if (Main.TryLoadCustomSkillIconSprite(spriteName, out Sprite customSprite))
            {
                __result = customSprite;
                return false;
            }

            if (Main.CustomSkillIdToIconId.TryGetValue(spriteName, out string mappedIconId))
            {
                spriteName = mappedIconId;
            }

            return true;
        }
    }

}
