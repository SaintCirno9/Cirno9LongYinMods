using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinCustomKungfuSkill.Main), "LongYinCustomKungfuSkill", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomKungfuSkill
{
    // ─── CSV 行解析结果 ─────────────────────────────────────────────────────────

    /// <summary>自定义 CSV 中的一条原始记录，只保留加载和日志所需的最少信息。</summary>
    internal sealed class CustomSkillRow
    {
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
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomKungfuSkill");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "KungfuSkillData.csv");

        // CSV 模板：名字列为主键，必须与游戏内功法名称完全一致
        // 示例：将原版太祖长拳的等级改为5
        private static readonly string DefaultCsv =
            "id,类别,等级,名字,描述,基础威力,经验系数,修炼效果,运功效果,威力系数,修炼需求,攻击范围,伤害范围,使用特效,内力消耗,所属势力,使用目标,攻击架势,防御架势,召唤ID,使用次数,动作,子弹,特效,使用武器,伤害顺序,角色位移,Trail,隐藏\r\n" +
            "9,拳掌,5,太祖长拳Pro,相传本朝太祖所创的基础拳法,10,1.1,拳掌1,,力道2;拳掌1;内力0.3,,\"0,0,4\",\"0,0,1\",伤害0.04,5,-1,0,胸15,胸10,,,skill0_0,,打击5-Target-Grid-Happen,拳,Same,FALSE,0,0\r\n";

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

    }

    // ─── HarmonyPatch：在开始菜单初始化后按名称覆盖原版功法属性 ─────────────────

    [HarmonyPatch(typeof(StartMenuController), nameof(StartMenuController.Start))]
    public class KungfuSkillDatabasePatch
    {
        // 防重复：MelonLoader 只加载一次 Mod，静态字段不随场景切换重置
        private static bool _initialized = false;

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
            if (_initialized)
            {
                Main.LOG.Msg("KungfuSkill 已在本次游戏会话中初始化，跳过重复覆盖。");
                return;
            }

            List<KungfuSkillData> skillList = GameDataController.Instance?.kungfuSkillDataBase;
            if (skillList is not { Count: > 0 })
            {
                Main.LOG.Warning("KungfuSkill 数据库未就绪，已跳过功法属性覆盖。");
                return;
            }

            // 先导出原版数据（覆盖前），方便玩家查看并制作修改 CSV
            ExportVanillaSkills(skillList);

            int count = OverrideExistingSkillsById(skillList);
            Main.LOG.Msg($"KungfuSkill 覆盖处理完成，共覆盖 {count} 条原版功法属性。");
            _initialized = true;
        }

        // ── 释放原版功法数据模块到 CSV ──

        private static readonly string ExportPath = Path.Combine(Main.ConfigDir, "vanilla_KungfuSkillData.csv");

        private static void ExportVanillaSkills(List<KungfuSkillData> skillList)
        {
            try
            {
                Directory.CreateDirectory(Main.ConfigDir);

                // 优先尝试从游戏里提取实时的 TextAsset（保证跨版本最新）
                // 感谢 Asset Studio 解包提供的路径名
                UnityEngine.TextAsset liveAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("gamedata/kungfudata");
                if (liveAsset != null && !string.IsNullOrEmpty(liveAsset.text))
                {
                    File.WriteAllText(ExportPath, liveAsset.text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    Main.LOG.Msg($"已将【实时提取】的完整版原版功法数据释放到：{ExportPath}");
                }
                else
                {
                    Main.LOG.Warning("未能提取游戏中实时的配置。");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"释放原版功法模板失败：{ex.GetType().Name} - {ex.Message}");
            }
        }


        // ── 按名称覆盖原版功法属性 ──

        private static int OverrideExistingSkillsById(List<KungfuSkillData> skillList)
        {
            LoadedConfigData configData = LoadConfigData();
            if (configData.Rows.Length == 0)
            {
                return 0;
            }

            GameDataController controller = GameDataController.Instance;
            if (controller == null)
            {
                return 0;
            }

            try
            {
                LTCSVLoader loader = BuildNativeLoader(configData.LoaderCsv);
                int rowCount = loader.GetRow();

                if (rowCount <= 1)
                {
                    Main.LOG.Warning("原生加载器读取 CSV 后没有可用数据行。");
                    return 0;
                }

                if (rowCount != configData.Rows.Length + 1)
                {
                    Main.LOG.Warning($"原生加载器行数与定义不一致：rowCount={rowCount}，rows={configData.Rows.Length}。");
                    return 0;
                }

                int successCount = 0;
                for (int rowIndex = 1; rowIndex < rowCount; rowIndex++)
                {
                    CustomSkillRow row = configData.Rows[rowIndex - 1];

                    // 按 ID 查找数据库中的原版条目
                    KungfuSkillData target = FindInSkillListById(skillList, row.ConfigID);
                    if (target == null)
                    {
                        Main.LOG.Warning($"覆盖跳过：数据库中不存在 ID={row.ConfigID} 的功法，请检查 CSV 是否准确映射。");
                        continue;
                    }

                    // 用原生加载器加载出新属性对象
                    KungfuSkillData newData = controller.LoadSkillData(loader, rowIndex, row.IsSummonSkill);
                    if (newData == null)
                    {
                        Main.LOG.Warning($"覆盖跳过：LoadSkillData 返回 null，名称='{row.Name}'。");
                        continue;
                    }

                    // 保留原始 skillID，反射覆盖其余所有公共属性
                    int originalSkillID = target.skillID;
                    CopyPublicProperties(newData, target);
                    target.skillID = originalSkillID;

                    successCount++;
                    Main.LOG.Msg($"已覆盖原版功法：skillID={target.skillID}，名称={target.name}");
                    CompareAndLogCsvChanges(target.skillID.ToString());
                }

                return successCount;
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"覆盖功法属性时发生异常：{ex.GetType().Name} - {ex.Message}");
                return 0;
            }
        }

        /// <summary>通过反射将 src 的所有可写公共实例属性复制到 dst（Il2Cpp 类型成员是属性而非字段）。</summary>
        private static void CopyPublicProperties(KungfuSkillData src, KungfuSkillData dst)
        {
            System.Type type = typeof(KungfuSkillData);
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite || prop.Name == "skillID")
                {
                    continue;
                }

                try
                {
                    object value = prop.GetValue(src);
                    prop.SetValue(dst, value);
                }
                catch
                {
                    // 忽略个别不可写或类型不兼容的属性
                }
            }
        }

        private static void CompareAndLogCsvChanges(string skillIdStr)
        {
            try
            {
                if (!File.Exists(Main.ConfigPath) || !File.Exists(ExportPath)) return;

                string[] customLines = ReadAllLinesShared(Main.ConfigPath);
                string[] vanillaLines = ReadAllLinesShared(ExportPath);
                if (customLines.Length < 1 || vanillaLines.Length < 1) return;

                var headers = SplitCsvLine(vanillaLines[0]);
                int idCol = Math.Max(0, Array.IndexOf(headers, "id"));
                var customRow = customLines.Skip(1).Select(SplitCsvLine).FirstOrDefault(r => r.Length > idCol && r[idCol] == skillIdStr);
                var vanillaRow = vanillaLines.Skip(1).Select(SplitCsvLine).FirstOrDefault(r => r.Length > idCol && r[idCol] == skillIdStr);

                if (customRow == null || vanillaRow == null) return;

                List<string> changes = new List<string>();
                int cols = Math.Min(headers.Length, Math.Min(customRow.Length, vanillaRow.Length));
                for (int i = 0; i < cols; i++)
                {
                    if (headers[i] == "id") continue; // 忽略 ID 报错
                    if (customRow[i] != vanillaRow[i])
                    {
                        changes.Add($"{headers[i]}: {vanillaRow[i]} -> {customRow[i]}");
                    }
                }

                if (changes.Count > 0)
                {
                    Main.LOG.Msg("  -> CSV修改项：");
                    for (int j = 0; j < changes.Count; j++)
                    {
                        Main.LOG.Msg($"        {changes[j]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"比较 CSV 变动时出现异常：{ex.Message}");
            }
        }



        private static KungfuSkillData FindInSkillListById(List<KungfuSkillData> skillList, int id)
        {
            for (int i = 0; i < skillList.Count; i++)
            {
                KungfuSkillData skill = skillList[i];
                if (skill != null && skill.skillID == id)
                {
                    return skill;
                }
            }

            return null;
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
                string[] lines = ReadAllLinesShared(Main.ConfigPath);
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
                // 以功法 ID 为主键去重
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
                        Main.LOG.Warning($"第 {row + 1} 行：功法 ID={parsedRow.ConfigID} 重复，已跳过。");
                        continue;
                    }

                    rows.Add(parsedRow);
                    loaderLines.Add(line);
                }

                Main.LOG.Msg($"从 CSV 加载了 {rows.Count} 条覆盖配置。");
                return new LoadedConfigData(rows.ToArray(), string.Join("\r\n", loaderLines) + "\r\n");
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"读取配置文件时发生异常：{ex}");
                return LoadedConfigData.Empty;
            }
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

        // 带重试的文件读取，支持 UTF-8 与 GBK 编码回退，规避编辑器占用
        private static string[] ReadAllLinesShared(string path, int maxAttempts = 5)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    byte[] bytes;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            bytes = ms.ToArray();
                        }
                    }

                    try
                    {
                        // 尝试以严格模式读取 UTF-8，如果不符合标准则抛出异常以便回退
                        Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                        string textUtf8 = strictUtf8.GetString(bytes);
                        return textUtf8.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    }
                    catch (DecoderFallbackException)
                    {
                        Main.LOG.Warning($"[{Path.GetFileName(path)}] 不符合标准 UTF-8 编码，可能是由于系统自带 Excel 直接保存导致，已启动 GBK 容错解析...");
                        string textGbk = Encoding.GetEncoding("gbk").GetString(bytes);
                        return textGbk.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    }
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
            }

            // 最后一次如果不成功让其自然抛出异常，依然附带 ReadWrite 权限以尽量提供最高容忍度
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            }
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
