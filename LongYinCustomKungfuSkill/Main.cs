using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinCustomKungfuSkill.Main), "LongYinCustomKungfuSkill", "0.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomKungfuSkill
{
    // ─── CSV 行解析结果 ─────────────────────────────────────────────────────────

    /// <summary>自定义 CSV 中的一条原始记录，只保留加载和日志所需的最少信息。</summary>
    internal sealed class CustomSkillRow
    {
        /// <summary>配置期 ID，仅用于校验和日志。</summary>
        public int ConfigID { get; init; }

        /// <summary>功法名称。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>是否为召唤技能。</summary>
        public bool IsSummonSkill { get; init; }
    }

    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(Array.Empty<CustomSkillRow>(), string.Empty);

        public LoadedConfigData(CustomSkillRow[] rows, string loaderCsv)
        {
            Rows = rows;
            LoaderCsv = loaderCsv;
        }

        public CustomSkillRow[] Rows { get; }

        public string LoaderCsv { get; }
    }

    // ─── Mod 入口 ────────────────────────────────────────────────────────────────

    public class Main : MelonMod
    {
        internal static readonly string ConfigDir  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomKungfuSkill");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "KungfuSkillData.csv");

        // CSV 模板：使用与原版 KungFuData.csv 一致的表头
        private static readonly string DefaultCsv =
            "id,类别,等级,名字,描述,基础威力,经验系数,修炼效果,运功效果,威力系数,修炼需求,攻击范围,伤害范围,使用特效,内力消耗,所属势力,使用目标,攻击架势,防御架势,召唤ID,使用次数,动作,子弹,特效,使用武器,伤害顺序,角色位移,Trail,隐藏\r\n" +
            "1001,刀法,0,飞雪连天刀,拔刀，劈刀，收刀，只此最简单的动作，却是一切刀法之基,10,1.1,刀法1,,体质2;刀法1;内力0.1,,\"4,0,0\",\"5,1,2\",暴击0.04,5,-1,0,心14,心10,,,skill2_1,,三连剑影小-Target-Grid-Happen,刀,Same,FALSE,0,0\r\n" +
            "1002,内功,0,九阳神功,呼出浊气，吸入清气，一吐一纳之间，内息造化而成,,0.9,内功1;经脉1,生命上限20;内力上限20;内功4;智力4;经脉4,,,,,,0,-1,0,,,,,,,,,Same,FALSE,0,0\r\n";

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            EnsureConfigExists();
            LOG.Msg($"LongYinCustomKungfuSkill 已加载，配置文件：{ConfigPath}");
            LOG.Msg("调试热键：F7 给主角添加飞雪连天刀，F8 添加九阳神功。");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                TryAddSkillToPlayer("飞雪连天刀");
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                TryAddSkillToPlayer("九阳神功");
            }
        }

        internal static void EnsureConfigExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
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

        internal static void TryAddSkillToPlayer(string skillName)
        {
            try
            {
                WorldData worldData = GameController.Instance?.worldData;
                HeroData player = worldData?.Player();
                if (player == null)
                {
                    LOG.Warning($"调试添加功法失败：当前还拿不到主角对象。skillName={skillName}");
                    return;
                }

                KungfuSkillData dataBase = FindSkillDataByName(skillName);
                if (dataBase == null)
                {
                    LOG.Warning($"调试添加功法失败：全局数据库中不存在 skillName={skillName}。");
                    return;
                }

                int skillID = dataBase.skillID;
                KungfuSkillLvData existed = player.FindSkill(skillID);
                if (existed != null)
                {
                    LOG.Msg($"主角已拥有功法 skillID={skillID}，名称={dataBase.name}");
                    return;
                }

                var newSkill = new KungfuSkillLvData(skillID)
                {
                    belongHeroID = player.heroID,
                };

                player.GetSkill(newSkill, true, false);
                KungfuSkillLvData added = player.FindSkill(skillID);
                if (added == null)
                {
                    LOG.Warning($"主角添加功法后仍未找到：skillID={skillID}，名称={dataBase.name}");
                    return;
                }

                LOG.Msg($"已通过 HeroData.GetSkill 给主角添加功法：skillID={skillID}，名称={dataBase.name}");
            }
            catch (Exception ex)
            {
                LOG.Error($"调试添加主角功法异常：skillName={skillName}，异常：{ex}");
            }
        }

        internal static KungfuSkillData FindSkillDataByName(string skillName)
        {
            GameDataController controller = GameDataController.Instance;
            if (controller == null || string.IsNullOrWhiteSpace(skillName))
            {
                return null;
            }

            int skillID = controller.GetSkillID(skillName);
            List<KungfuSkillData> skillList = controller.kungfuSkillDataBase;
            if (skillList == null || skillID < 0 || skillID >= skillList.Count)
            {
                return null;
            }

            return skillList[skillID];
        }
    }

    // ─── HarmonyPatch：在开始菜单初始化后注入自定义功法 ──────────────────────────

    [HarmonyPatch(typeof(StartMenuController), nameof(StartMenuController.Start))]
    public class KungfuSkillDatabasePatch
    {
        // 必要列名组：仅支持原版 KungFuData.csv 表头
        private static readonly string[] RequiredColumns =
        {
            "id",
            "名字",
            "类别",
            "等级",
        };

        [HarmonyPostfix]
        public static void Postfix()
        {
            // 获取游戏技能数据库列表
            List<KungfuSkillData> skillList = GameDataController.Instance?.kungfuSkillDataBase;
            if (skillList is not { Count: > 0 })
            {
                Main.LOG.Warning("KungfuSkill 数据库未就绪，已跳过自定义功法注册。");
                return;
            }

            int before = skillList.Count;
            foreach (KungfuSkillData skill in BuildCustomSkills())
            {
                RegisterSkill(skillList, skill);
            }

            Main.LOG.Msg($"KungfuSkill 数据库处理完成：{before} -> {skillList.Count}");
        }

        // ── 读取 CSV 并构建 KungfuSkillData 列表 ──

        private static System.Collections.Generic.List<KungfuSkillData> BuildCustomSkills()
        {
            LoadedConfigData configData = LoadConfigData();
            var result = new System.Collections.Generic.List<KungfuSkillData>();
            if (configData.Rows.Length == 0)
            {
                return result;
            }

            if (TryBuildCustomSkillsWithNativeLoader(configData, result))
            {
                return result;
            }

            Main.LOG.Error("原生 LoadSkillData 加载失败，已跳过本次自定义功法注册。");
            return result;
        }

        private static bool TryBuildCustomSkillsWithNativeLoader(LoadedConfigData configData, System.Collections.Generic.List<KungfuSkillData> result)
        {
            GameDataController controller = GameDataController.Instance;
            if (controller == null)
            {
                return false;
            }

            try
            {
                LTCSVLoader loader = BuildNativeLoader(configData.LoaderCsv);

                int rowCount = loader.GetRow();
                if (rowCount <= 1)
                {
                    Main.LOG.Warning("原生加载器读取自定义功法 CSV 后没有可用数据行。");
                    return false;
                }

                if (rowCount != configData.Rows.Length + 1)
                {
                    Main.LOG.Warning($"原生加载器行数与定义数量不一致：rowCount={rowCount}，rows={configData.Rows.Length}。");
                    return false;
                }

                for (int rowIndex = 1; rowIndex < rowCount; rowIndex++)
                {
                    CustomSkillRow row = configData.Rows[rowIndex - 1];
                    KungfuSkillData data = controller.LoadSkillData(loader, rowIndex, row.IsSummonSkill);
                    if (data == null)
                    {
                        Main.LOG.Warning($"原生 LoadSkillData 返回 null：rowIndex={rowIndex}，ID={row.ConfigID}，名称={row.Name}");
                        return false;
                    }

                    result.Add(data);
                }

                Main.LOG.Msg($"已通过原生 LoadSkillData 加载 {result.Count} 条自定义功法。");
                return true;
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"原生 LoadSkillData 加载异常：{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        private static LTCSVLoader BuildNativeLoader(string csv)
        {
            var loader = new LTCSVLoader();
            loader.ReadMultiLine(csv);
            return loader;
        }

        // ── 从 CSV 解析最少元数据，并把原始文本直接交给 LTCSVLoader ──

        private static LoadedConfigData LoadConfigData()
        {
            Main.EnsureConfigExists();

            if (!File.Exists(Main.ConfigPath))
            {
                Main.LOG.Warning($"配置文件不存在：{Main.ConfigPath}");
                return LoadedConfigData.Empty;
            }

            try
            {
                string[] lines = ReadLinesWithRetry(Main.ConfigPath);
                if (lines.Length <= 1)
                {
                    Main.LOG.Warning("配置文件为空，未加载任何功法。");
                    return LoadedConfigData.Empty;
                }

                string[] headers = SplitCsvLine(lines[0]);

                // 检查必要列是否齐全
                if (!ValidateHeaders(headers))
                {
                    Main.LOG.Error("配置文件表头缺少必要列，已中止加载。");
                    return LoadedConfigData.Empty;
                }

                int idCol = FindColumnIndex(headers, "id");
                int nameCol = FindColumnIndex(headers, "名字");
                int summonIdCol = FindColumnIndex(headers, "召唤ID");

                var rows = new System.Collections.Generic.List<CustomSkillRow>();
                var seenIDs = new System.Collections.Generic.HashSet<int>();
                var loaderLines = new System.Collections.Generic.List<string> { lines[0] };

                for (int row = 1; row < lines.Length; row++)
                {
                    string line = lines[row].Trim();
                    // 跳过空行和注释行（# 开头）
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] cells = SplitCsvLine(line);
                    CustomSkillRow parsedRow = ParseRow(cells, row + 1, idCol, nameCol, summonIdCol);
                    if (parsedRow == null)
                    {
                        continue;
                    }

                    if (!seenIDs.Add(parsedRow.ConfigID))
                    {
                        Main.LOG.Warning($"第 {row + 1} 行：SkillID={parsedRow.ConfigID} 重复，已跳过。");
                        continue;
                    }

                    rows.Add(parsedRow);
                    loaderLines.Add(line);
                }

                Main.LOG.Msg($"从 CSV 加载了 {rows.Count} 条自定义功法定义。");
                return new LoadedConfigData(rows.ToArray(), string.Join("\r\n", loaderLines) + "\r\n");
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"读取配置文件时发生异常：{ex}");
                return LoadedConfigData.Empty;
            }
        }

        // ── 将技能注册进数据库；已存在同名技能则跳过，并分配运行时 skillID ──

        private static void RegisterSkill(List<KungfuSkillData> skillList, KungfuSkillData skill)
        {
            if (skill == null)
            {
                return;
            }

            string skillName = skill.name;
            for (int i = 0; i < skillList.Count; i++)
            {
                if (skillList[i] != null && string.Equals(skillList[i].name, skillName, StringComparison.Ordinal))
                {
                    Main.LOG.Msg($"KungfuSkill 名称={skillName} 已存在，跳过注册。");
                    return;
                }
            }

            int runtimeSkillID = skillList.Count;
            skill.skillID = runtimeSkillID;
            skillList.Add(skill);
            RegisterSkillInGroupedDatabase(skill);
            RegisterSkillInSummonDatabase(skill);
            Main.LOG.Msg($"已注册自定义功法：runtimeID={skill.skillID}，名称={skill.name}");
        }

        private static void RegisterSkillInGroupedDatabase(KungfuSkillData skill)
        {
            List<List<List<KungfuSkillData>>> grouped = GameDataController.Instance?.kungfuSkillDataList;
            if (grouped == null || grouped.Count == 0)
            {
                return;
            }

            int outerCount = grouped.Count;
            int innerCount = grouped[0]?.Count ?? 0;
            if (innerCount == 0)
            {
                return;
            }

            bool outerLooksLikeType = outerCount >= 9 && innerCount < 9;
            int outerIndex = outerLooksLikeType ? skill.type : skill.rareLv;
            int innerIndex = outerLooksLikeType ? skill.rareLv : skill.type;

            if (outerIndex < 0 || outerIndex >= grouped.Count)
            {
                return;
            }

            List<List<KungfuSkillData>> innerGroups = grouped[outerIndex];
            if (innerGroups == null || innerIndex < 0 || innerIndex >= innerGroups.Count)
            {
                return;
            }

            List<KungfuSkillData> bucket = innerGroups[innerIndex];
            if (bucket == null)
            {
                bucket = new List<KungfuSkillData>();
                innerGroups[innerIndex] = bucket;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i] != null && string.Equals(bucket[i].name, skill.name, StringComparison.Ordinal))
                {
                    return;
                }
            }

            bucket.Add(skill);
        }

        private static void RegisterSkillInSummonDatabase(KungfuSkillData skill)
        {
            if (!skill.summonSkill)
            {
                return;
            }

            List<KungfuSkillData> summonList = GameDataController.Instance?.summonSkillDataBase;
            if (summonList == null)
            {
                return;
            }

            for (int i = 0; i < summonList.Count; i++)
            {
                if (summonList[i] != null && string.Equals(summonList[i].name, skill.name, StringComparison.Ordinal))
                {
                    return;
                }
            }

            summonList.Add(skill);
        }

        // ─── CSV 解析辅助 ────────────────────────────────────────────────────────

        private static int FindColumnIndex(string[] headers, string columnName)
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

        /// <summary>解析一行 CSV 数据为最少元数据；解析失败返回 null。</summary>
        private static CustomSkillRow ParseRow(string[] cells, int rowNum, int idCol, int nameCol, int summonIdCol)
        {
            try
            {
                int skillID = ParseInt(GetCell(cells, idCol), -1);
                if (skillID <= 0)
                {
                    Main.LOG.Warning($"第 {rowNum} 行：ID 无效（值={skillID}），已跳过。");
                    return null;
                }

                if (skillID >= 10000)
                {
                    Main.LOG.Warning($"第 {rowNum} 行：ID={skillID} 大于等于 10000。实测这类 ID 可能被部分逻辑误判到召唤功法数据，建议改用小于 10000 的空闲 ID。");
                }

                string name = GetCell(cells, nameCol);
                if (string.IsNullOrWhiteSpace(name))
                {
                    Main.LOG.Warning($"第 {rowNum} 行：名称为空，已跳过。");
                    return null;
                }

                return new CustomSkillRow
                {
                    ConfigID = skillID,
                    Name = name,
                    IsSummonSkill = ParseInt(GetCell(cells, summonIdCol), 0) > 0,
                };
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"第 {rowNum} 行解析异常：{ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private static bool ValidateHeaders(string[] headers)
        {
            var headerSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string h in headers)
            {
                headerSet.Add(h.Trim('\uFEFF', ' ', '\t'));
            }

            bool allPresent = true;
            foreach (string column in RequiredColumns)
            {
                if (!headerSet.Contains(column))
                {
                    Main.LOG.Error($"配置文件缺少必要列：\"{column}\"");
                    allPresent = false;
                }
            }

            return allPresent;
        }

        // 带重试的文件读取，规避编辑器占用
        private static string[] ReadLinesWithRetry(string path, int maxAttempts = 5)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    string content = reader.ReadToEnd();
                    return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
            }

            // 最后一次不捕获，让上层收到异常
            return File.ReadAllLines(path, Encoding.UTF8);
        }

        /// <summary>支持引号转义的 RFC 4180 CSV 行拆分。</summary>
        private static string[] SplitCsvLine(string line)
        {
            var cells = new System.Collections.Generic.List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    // 双引号转义（""）
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
                Main.LOG.Warning($"CSV 行引号未正确闭合：{line}");
            }

            return cells.ToArray();
        }

        private static string GetCell(string[] cells, int index)
        {
            return index >= 0 && index < cells.Length ? cells[index].Trim() : string.Empty;
        }

        private static int ParseInt(string text, int fallback)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    }

}
