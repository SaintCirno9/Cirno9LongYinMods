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

[assembly: MelonInfo(typeof(LongYinCustomKungfuSkill.Main), "LongYinCustomKungfuSkill", "2.4.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomKungfuSkill
{
    // ─── CSV 行解析结果 ─────────────────────────────────────────────────────────

    internal sealed class LoadedConfigData
    {
        public static readonly LoadedConfigData Empty = new(
            0,
            string.Empty,
            new System.Collections.Generic.List<LoadedSkillRow>(),
            new System.Collections.Generic.Dictionary<string, SkillConfigOrigin>(StringComparer.Ordinal),
            string.Empty);

        public LoadedConfigData(
            int rowCount,
            string loaderCsv,
            System.Collections.Generic.List<LoadedSkillRow> rows,
            System.Collections.Generic.Dictionary<string, SkillConfigOrigin> skillOriginsById,
            string cacheSignature)
        {
            RowCount = rowCount;
            LoaderCsv = loaderCsv;
            Rows = rows;
            SkillOriginsById = skillOriginsById;
            CacheSignature = cacheSignature;
        }

        public int RowCount { get; }

        public string LoaderCsv { get; }

        public System.Collections.Generic.List<LoadedSkillRow> Rows { get; }

        public System.Collections.Generic.Dictionary<string, SkillConfigOrigin> SkillOriginsById { get; }

        public string CacheSignature { get; }
    }

    internal sealed class ConfigSource
    {
        public ConfigSource(string sourceName, string csvPath, string iconDir, string relativeIconDir, int order)
        {
            SourceName = sourceName;
            CsvPath = csvPath;
            IconDir = iconDir;
            RelativeIconDir = relativeIconDir;
            Order = order;
        }

        public string SourceName { get; }

        public string CsvPath { get; }

        public string IconDir { get; }

        public string RelativeIconDir { get; }

        public int Order { get; }
    }

    internal sealed class SkillConfigOrigin
    {
        public SkillConfigOrigin(string sourceName, string iconDir, string relativeIconDir, string csvPath)
        {
            SourceName = sourceName;
            IconDir = iconDir;
            RelativeIconDir = relativeIconDir;
            CsvPath = csvPath;
        }

        public string SourceName { get; }

        public string IconDir { get; }

        public string RelativeIconDir { get; }

        public string CsvPath { get; }
    }

    internal sealed class LoadedSkillRow
    {
        public LoadedSkillRow(int originalSkillId, string normalizedLine, SkillConfigOrigin origin)
        {
            OriginalSkillId = originalSkillId;
            NormalizedLine = normalizedLine;
            Origin = origin;
        }

        public int OriginalSkillId { get; }

        public string NormalizedLine { get; }

        public SkillConfigOrigin Origin { get; }
    }

    internal sealed class RuntimeSkillAssetBinding
    {
        public RuntimeSkillAssetBinding(string sourceSkillId, SkillConfigOrigin origin)
        {
            SourceSkillId = sourceSkillId;
            Origin = origin;
        }

        public string SourceSkillId { get; }

        public SkillConfigOrigin Origin { get; }
    }

    // ─── Mod 入口 ────────────────────────────────────────────────────────────────

    public class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomKungfuSkill");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "KungfuSkillData.csv");
        internal static readonly string VanillaExportPath = Path.Combine(ConfigDir, "Vanilla_KungfuSkillData.csv");
        internal static readonly string IconDir = Path.Combine(ConfigDir, "Icons");
        internal static readonly string DataDir = Path.Combine(ConfigDir, "Data");
        internal static readonly string SamplePackDir = Path.Combine(DataDir, "000");
        internal static readonly string SamplePackConfigPath = Path.Combine(SamplePackDir, "KungfuSkillData.csv");
        internal static readonly string SamplePackIconDir = Path.Combine(SamplePackDir, "Icons");
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
        internal static readonly System.Collections.Generic.Dictionary<string, RuntimeSkillAssetBinding> RuntimeSkillAssetBindings = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.Dictionary<string, Sprite> CustomSkillIconCache = new(StringComparer.Ordinal);
        internal static readonly System.Collections.Generic.HashSet<string> CheckedCustomSkillIconIds = new(StringComparer.Ordinal);
        private static LoadedConfigData _cachedConfigData = LoadedConfigData.Empty;
        private static DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;
        private static bool _legacyStructureWarningShown;

        private static readonly string SampleCsv =
            "id,类别,等级,名字,描述,基础威力,经验系数,修炼效果,运功效果,威力系数,修炼需求,攻击范围,伤害范围,使用特效,内力消耗,所属势力,使用目标,攻击架势,防御架势,召唤ID,使用次数,动作,子弹,特效,使用武器,伤害顺序,角色位移,Trail,隐藏\r\n" +
            "10000,拳掌,5,太祖不短拳,相传本朝太祖所创的基础拳法,10,1.1,拳掌1,,力道2;拳掌1;内力0.3,,\"1,1,1\",\"0,0,0\",伤害0.04,5,0,0,胸15,胸10,,,skill0_0,,打击5-Target-Grid-Happen,拳,Same,FALSE,0,1\r\n" +
            "10001,内功,5,太祖吐纳法,呼出浊气，吸入清气，一吐一纳之间，内息造化而成,,0.9,内功1;经脉1,生命上限20;内力上限20;内功4;智力4;经脉4,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "10002,轻功,5,太祖飞毛腿,源于古代递送紧急军情“羽檄”之兵士的步法,,1,灵巧1,轻功4;灵巧4;速度0.04,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "10003,绝技,5,太祖铁布衫,基本硬功之一，修习者周身致密坚韧，能够抵御些许来袭之力道,,1,绝技1,体质4;护甲2;减伤2,,,,,,0,0,0,,,,,,,,,Same,FALSE,0,1\r\n" +
            "10004,剑法,5,太祖御剑术,击、刺、格、洗四种基础剑招的汇总口诀,10,1.1,剑法1,,灵巧2;剑法1;内力0.1,,\"4,0,0\",\"4,1,2\",命中0.04,5,0,0,手14,手10,,,skill1_1,,劈砍-Target-Grid-Happen,剑,Same,FALSE,0,1\r\n" +
            "10005,刀法,5,太祖拔刀功,拔刀，劈刀，收刀，只此最简单的动作，却是一切刀法之基,10,1.1,刀法1,,体质2;刀法1;内力0.1,,\"4,0,0\",\"5,1,2\",暴击0.04,5,0,0,心14,心10,,,skill2_1,,三连剑影小-Target-Grid-Happen,刀,Same,FALSE,0,1\r\n" +
            "10006,长兵,5,太祖长棍法,相传本朝太祖所创的基础棍法,10,1.1,长兵1,,意志2;长兵1;内力0.1,,\"1,1,1\",\"7,0,0\",连击0.04,5,0,0,头15,头10,,,skill3_1,,打击5-Target-Grid-Happen,长兵,Same,FALSE,0,1\r\n" +
            "10007,奇门,5,太祖三路鞭,分为上中下三路的基本鞭法,10,1.1,奇门1,,智力2;奇门1;内力0.1,,\"3,1,1\",\"7,0,0\",震脑0.04,5,0,0,头14,头6,,,skill4_3,,乱刃-Target-Grid-Happen,鞭,Same,FALSE,0,1\r\n" +
            "10008,射术,5,太祖飞镖诀,一门使用飞镖的基础手法,10,1.1,射术1,,灵巧2;射术1;内力0.1,,\"0,2,3\",\"0,0,0\",流血0.04,5,0,0,腹20,腹3,,,skill5_0,1-----0.9,三连劈砍-Target-Grid-Happen,,Same,FALSE,0,1\r\n";

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            LOG.Msg($"LongYinCustomKungfuSkill 已加载，配置目录：{ConfigDir}");
        }

        internal static void EnsureConfigExists()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                Directory.CreateDirectory(DataDir);
                IconExportManager.EnsureConfigExists();

                bool hasLegacyCsv = File.Exists(ConfigPath);
                bool hasLegacyIcons = Directory.Exists(IconDir);
                if (hasLegacyCsv || hasLegacyIcons)
                {
                    Directory.CreateDirectory(SamplePackDir);
                    Directory.CreateDirectory(SamplePackIconDir);
                    if (!_legacyStructureWarningShown)
                    {
                        _legacyStructureWarningShown = true;
                        LOG.Warning(
                            "检测到旧版根目录配置结构，当前仍会兼容读取。新版建议路径：" +
                            " CSV -> UserData\\LongYinCustomKungfuSkill\\Data\\000\\KungfuSkillData.csv；" +
                            " 图标 -> UserData\\LongYinCustomKungfuSkill\\Data\\000\\Icons");
                    }
                }
                else if (CollectConfigSources().Count == 0)
                {
                    Directory.CreateDirectory(SamplePackDir);
                    Directory.CreateDirectory(SamplePackIconDir);
                    File.WriteAllText(SamplePackConfigPath, SampleCsv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    LOG.Msg($"未检测到任何功法配置，已自动生成示例配置：UserData\\LongYinCustomKungfuSkill\\Data\\000\\KungfuSkillData.csv");
                    LOG.Msg("已同时创建示例图标目录：UserData\\LongYinCustomKungfuSkill\\Data\\000\\Icons");
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
            RuntimeSkillAssetBindings.Clear();
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

            var bindingKeysToRemove = RuntimeSkillAssetBindings.Keys
                .Where(key => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) && skillId >= firstMissingId)
                .ToList();

            foreach (string key in bindingKeysToRemove)
            {
                RuntimeSkillAssetBindings.Remove(key);
            }
        }

        internal static void RegisterInjectedCustomSkillId(int skillId)
        {
            if (skillId >= 0)
            {
                InjectedCustomSkillIds.Add(skillId);
            }
        }

        internal static void BindConfiguredSkillIcon(string runtimeSkillId, string sourceSkillId, SkillConfigOrigin origin)
        {
            if (string.IsNullOrWhiteSpace(runtimeSkillId) || string.IsNullOrWhiteSpace(sourceSkillId) || origin == null)
            {
                return;
            }

            RuntimeSkillAssetBindings[runtimeSkillId] = new RuntimeSkillAssetBinding(sourceSkillId, origin);
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

        internal static bool IsInjectedCustomSkillId(int skillId)
        {
            return skillId >= 0 && (InjectedCustomSkillIds.Contains(skillId) || skillId > VanillaKungFuMaxId);
        }

        internal static void AddCustomSkillsToJianghuBookPool(GameDataController gameData)
        {
            if (gameData?.kungfuSkillDataList == null || InjectedCustomSkillIds.Count == 0)
            {
                return;
            }

            int addedCount = 0;
            foreach (int skillId in InjectedCustomSkillIds.OrderBy(id => id))
            {
                KungfuSkillData skillData = gameData.GetSkillDataBase(skillId);
                if (skillData == null || skillData.hide || skillData.belongForceID == -1 || IsBigForceId(gameData, skillData.belongForceID))
                {
                    continue;
                }

                int rareLv = Math.Clamp(skillData.rareLv, 0, 5);
                List<KungfuSkillData> jianghuPool = gameData.kungfuSkillDataList[0][rareLv];
                if (jianghuPool == null || ContainsSkillId(jianghuPool, skillData.skillID))
                {
                    continue;
                }

                jianghuPool.Add(skillData);
                addedCount++;
            }

            if (addedCount > 0)
            {
                LOG.Msg($"已将 {addedCount} 门非大门派自定义功法加入江湖秘籍随机池。");
            }
        }

        private static bool IsBigForceId(GameDataController gameData, int forceId)
        {
            if (gameData?.bigForceIDList == null)
            {
                return false;
            }

            for (int i = 0; i < gameData.bigForceIDList.Count; i++)
            {
                if (gameData.bigForceIDList[i] == forceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSkillId(List<KungfuSkillData> skills, int skillId)
        {
            if (skills == null)
            {
                return false;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                KungfuSkillData skill = skills[i];
                if (skill != null && skill.skillID == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetFallbackIconIdForSkill(KungfuSkillData skillData, out int fallbackSkillId)
        {
            fallbackSkillId = -1;
            if (skillData == null)
            {
                return false;
            }

            string skillId = skillData.skillID.ToString(CultureInfo.InvariantCulture);
            if (CustomSkillIdToIconId.TryGetValue(skillId, out string mappedIconId) &&
                int.TryParse(mappedIconId, NumberStyles.Integer, CultureInfo.InvariantCulture, out fallbackSkillId) &&
                fallbackSkillId >= 0)
            {
                return true;
            }

            string skillTypeName = skillData.type switch
            {
                0 => "内功",
                1 => "轻功",
                2 => "绝技",
                3 => "拳掌",
                4 => "剑法",
                5 => "刀法",
                6 => "长兵",
                7 => "奇门",
                8 => "射术",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(skillTypeName) &&
                VanillaSkillIdsByType.TryGetValue(skillTypeName, out System.Collections.Generic.List<int> skillIds) &&
                skillIds.Count > 0)
            {
                fallbackSkillId = skillIds[0];
                return true;
            }

            return false;
        }

        internal static System.Collections.Generic.List<ConfigSource> CollectConfigSources()
        {
            var sources = new System.Collections.Generic.List<ConfigSource>();

            if (File.Exists(ConfigPath))
            {
                sources.Add(new ConfigSource(
                    "根目录",
                    ConfigPath,
                    IconDir,
                    Path.Combine("UserData", "LongYinCustomKungfuSkill", "Icons"),
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

                string csvPath = Path.Combine(dir, "KungfuSkillData.csv");
                if (!File.Exists(csvPath))
                {
                    continue;
                }

                sources.Add(new ConfigSource(
                    $"Data/{dirName}",
                    csvPath,
                    Path.Combine(dir, "Icons"),
                    Path.Combine("UserData", "LongYinCustomKungfuSkill", "Data", dirName, "Icons"),
                    order));
            }

            return sources;
        }

        internal static string BuildConfigCacheSignature(System.Collections.Generic.IEnumerable<ConfigSource> sources)
        {
            if (sources == null)
            {
                return string.Empty;
            }

            return string.Join(
                "|",
                sources.Select(source =>
                {
                    var fileInfo = new FileInfo(source.CsvPath);
                    return $"{source.Order}:{source.CsvPath}:{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
                }));
        }

        internal static void ResetCustomIconLookupState()
        {
            CustomSkillIconCache.Clear();
            CheckedCustomSkillIconIds.Clear();
        }

        internal static System.Collections.Generic.IEnumerable<(string FullPath, string DisplayPath)> GetCandidateIconPaths(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                yield break;
            }

            var yielded = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!RuntimeSkillAssetBindings.TryGetValue(skillId, out RuntimeSkillAssetBinding binding) || binding.Origin == null)
            {
                yield break;
            }

            string sourceSkillId = string.IsNullOrWhiteSpace(binding.SourceSkillId) ? skillId : binding.SourceSkillId;
            if (!string.IsNullOrWhiteSpace(binding.Origin.IconDir))
            {
                string fullPath = Path.Combine(binding.Origin.IconDir, $"{sourceSkillId}.png");
                if (yielded.Add(fullPath))
                {
                    yield return (fullPath, Path.Combine(binding.Origin.RelativeIconDir, $"{sourceSkillId}.png"));
                }
            }

            string rootFullPath = Path.Combine(IconDir, $"{sourceSkillId}.png");
            if (yielded.Add(rootFullPath))
            {
                yield return (rootFullPath, Path.Combine("UserData", "LongYinCustomKungfuSkill", "Icons", $"{sourceSkillId}.png"));
            }

            if (!string.Equals(sourceSkillId, skillId, StringComparison.Ordinal))
            {
                string legacyRootFullPath = Path.Combine(IconDir, $"{skillId}.png");
                if (yielded.Add(legacyRootFullPath))
                {
                    yield return (legacyRootFullPath, Path.Combine("UserData", "LongYinCustomKungfuSkill", "Icons", $"{skillId}.png"));
                }
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

            if (CustomSkillIconCache.TryGetValue(skillId, out Sprite cachedSprite))
            {
                if (cachedSprite != null)
                {
                    sprite = cachedSprite;
                    return true;
                }
                else
                {
                    CustomSkillIconCache.Remove(skillId);
                    CheckedCustomSkillIconIds.Remove(skillId);
                }
            }

            if (CheckedCustomSkillIconIds.Contains(skillId))
            {
                return false;
            }

            (string FullPath, string DisplayPath)? foundIcon = null;
            foreach ((string FullPath, string DisplayPath) candidate in GetCandidateIconPaths(skillId))
            {
                if (File.Exists(candidate.FullPath))
                {
                    foundIcon = candidate;
                    break;
                }
            }

            if (!foundIcon.HasValue)
            {
                CheckedCustomSkillIconIds.Add(skillId);
                return false;
            }

            try
            {
                byte[] pngBytes = File.ReadAllBytes(foundIcon.Value.FullPath);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, pngBytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    CheckedCustomSkillIconIds.Add(skillId);
                    LOG.Warning($"读取自定义图标失败：skillID={skillId}, path={foundIcon.Value.DisplayPath}");
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
                LOG.Warning($"加载自定义图标异常：skillID={skillId}, path={foundIcon.Value.DisplayPath}，{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        internal static bool HasCustomSkillIconFile(string skillId)
        {
            return !string.IsNullOrWhiteSpace(skillId) &&
                   GetCandidateIconPaths(skillId).Any(candidate => File.Exists(candidate.FullPath));
        }

        internal static string GetCustomSkillIconPath(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return string.Empty;
            }

            foreach ((string FullPath, string DisplayPath) candidate in GetCandidateIconPaths(skillId))
            {
                if (File.Exists(candidate.FullPath))
                {
                    return candidate.DisplayPath;
                }
            }

            return string.Empty;
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

            try
            {
                System.Collections.Generic.List<ConfigSource> sources = CollectConfigSources();
                if (sources.Count == 0)
                {
                    LOG.Warning($"未找到任何配置文件：{ConfigDir}");
                    return LoadedConfigData.Empty;
                }

                string cacheSignature = BuildConfigCacheSignature(sources);
                if (_cachedConfigData.RowCount >= 0 &&
                    string.Equals(_cachedConfigData.CacheSignature, cacheSignature, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(_cachedConfigData.LoaderCsv))
                {
                    return _cachedConfigData;
                }

                ResetCustomIconLookupState();

                string[] canonicalHeaders = null;
                string canonicalHeaderLine = null;
                var loaderLines = new System.Collections.Generic.List<string>();
                var rows = new System.Collections.Generic.List<LoadedSkillRow>();
                var skillOriginsById = new System.Collections.Generic.Dictionary<string, SkillConfigOrigin>(StringComparer.Ordinal);
                int rowCount = 0;

                foreach (ConfigSource source in sources)
                {
                    string[] lines = ReadAllLinesShared(source.CsvPath);
                    if (lines.Length <= 1)
                    {
                        LOG.Warning($"配置文件为空，已跳过：{source.CsvPath}");
                        continue;
                    }

                    string[] headers = SplitCsvLine(lines[0]);
                    if (!ValidateHeaders(headers))
                    {
                        LOG.Error($"配置文件表头缺少必要列，已跳过：{source.CsvPath}");
                        continue;
                    }

                    if (canonicalHeaders == null)
                    {
                        canonicalHeaders = headers;
                        canonicalHeaderLine = BuildCsvLine(canonicalHeaders);
                        loaderLines.Add(canonicalHeaderLine);
                    }

                    var columnMap = new ConfigColumnMap(headers);
                    var seenIdsInSource = new System.Collections.Generic.HashSet<int>();

                    for (int row = 1; row < lines.Length; row++)
                    {
                        string line = lines[row].Trim();
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        {
                            continue;
                        }

                        string[] cells = SplitCsvLine(line);
                        if (!ConfigRowValidator.TryValidateRow(cells, row + 1, source.SourceName, columnMap, out int parsedSkillId))
                        {
                            continue;
                        }

                        if (!seenIdsInSource.Add(parsedSkillId))
                        {
                            LOG.Warning($"[{source.SourceName}] 第 {row + 1} 行：功法 ID={parsedSkillId} 重复，已跳过。");
                            continue;
                        }

                        rowCount++;
                        string skillId = parsedSkillId.ToString(CultureInfo.InvariantCulture);
                        string normalizedLine = NormalizeCsvRow(canonicalHeaders, headers, cells);
                        SkillConfigOrigin origin = new(
                            source.SourceName,
                            source.IconDir,
                            source.RelativeIconDir,
                            source.CsvPath);
                        loaderLines.Add(normalizedLine);
                        rows.Add(new LoadedSkillRow(parsedSkillId, normalizedLine, origin));
                        skillOriginsById[skillId] = origin;
                    }
                }

                if (canonicalHeaders == null || loaderLines.Count <= 1)
                {
                    LOG.Warning("所有配置文件均为空或无有效数据，未加载任何功法。");
                    LoadedConfigData emptyData = new(
                        0,
                        string.Empty,
                        new System.Collections.Generic.List<LoadedSkillRow>(),
                        new System.Collections.Generic.Dictionary<string, SkillConfigOrigin>(StringComparer.Ordinal),
                        cacheSignature);
                    _cachedConfigData = emptyData;
                    _cachedConfigWriteTimeUtc = DateTime.UtcNow;
                    return emptyData;
                }

                LOG.Msg($"已从 {sources.Count} 份配置来源加载 {rowCount} 条功法配置。");
                LoadedConfigData loadedData = new(
                    rowCount,
                    string.Join("\r\n", loaderLines) + "\r\n",
                    rows,
                    skillOriginsById,
                    cacheSignature);
                _cachedConfigData = loadedData;
                _cachedConfigWriteTimeUtc = DateTime.UtcNow;
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
            if (configData.RowCount == 0 || configData.Rows.Count == 0)
            {
                return null;
            }

            string[] vanillaLines = vanillaText.Replace("\uFEFF", "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (vanillaLines.Length == 0)
            {
                return null;
            }

            string[] vanillaHeaders = Main.SplitCsvLine(vanillaLines[0]);
            int vanillaIdCol = Main.FindColumnIndex(vanillaHeaders, "id");
            int vanillaTypeCol = Main.FindColumnIndex(vanillaHeaders, "类别");
            if (vanillaIdCol < 0)
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

            var overrideById = new System.Collections.Generic.Dictionary<string, LoadedSkillRow>(StringComparer.Ordinal);
            var appendedCustomRows = new System.Collections.Generic.List<LoadedSkillRow>();
            foreach (LoadedSkillRow row in configData.Rows)
            {
                string originalIdText = row.OriginalSkillId.ToString(CultureInfo.InvariantCulture);
                if (row.OriginalSkillId >= 0 && row.OriginalSkillId <= vanillaMaxId)
                {
                    if (overrideById.ContainsKey(originalIdText))
                    {
                        Main.LOG.Warning($"原版功法 ID={originalIdText} 在多个配置中重复定义，已使用后加载的配置覆盖先前定义。");
                    }

                    overrideById[originalIdText] = row;
                }
                else
                {
                    appendedCustomRows.Add(row);
                }
            }

            if (overrideById.Count == 0 && appendedCustomRows.Count == 0)
            {
                return null;
            }

            var outputLines = new System.Collections.Generic.List<string> { vanillaLines[0] };
            var appendedCustomSkillLogs = new System.Collections.Generic.List<(
                string SkillType,
                string RuntimeSkillId,
                string SourceSkillId,
                string SourceName,
                string IconId,
                bool HasCustomIcon,
                string CustomIconPath)>();

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
                if (!string.IsNullOrWhiteSpace(id) && overrideById.TryGetValue(id, out LoadedSkillRow replacementRow))
                {
                    Main.BindConfiguredSkillIcon(id, id, replacementRow.Origin);
                    LogMergedRowChanges(vanillaHeaders, line, replacementRow.NormalizedLine, id);
                    outputLines.Add(replacementRow.NormalizedLine);
                }
                else
                {
                    outputLines.Add(line);
                }
            }

            int nextInjectedSkillId = vanillaMaxId + 1;
            foreach (LoadedSkillRow appendedRow in appendedCustomRows)
            {
                string runtimeSkillId = nextInjectedSkillId.ToString(CultureInfo.InvariantCulture);
                string[] cells = Main.SplitCsvLine(appendedRow.NormalizedLine);
                if (vanillaIdCol >= cells.Length)
                {
                    continue;
                }

                cells[vanillaIdCol] = runtimeSkillId;
                string runtimeLine = Main.BuildCsvLine(cells);
                outputLines.Add(runtimeLine);
                Main.RegisterInjectedCustomSkillId(nextInjectedSkillId);
                Main.BindConfiguredSkillIcon(
                    runtimeSkillId,
                    appendedRow.OriginalSkillId.ToString(CultureInfo.InvariantCulture),
                    appendedRow.Origin);

                // TODO：后续若要支持新增功法之间通过召唤ID等字段互相引用，需要在这里同步重写相关引用字段。

                string typeStr = Main.GetCell(cells, vanillaTypeCol);
                string iconId = null;
                bool hasCustomIcon = Main.HasCustomSkillIconFile(runtimeSkillId);
                string customIconPath = hasCustomIcon ? Main.GetCustomSkillIconPath(runtimeSkillId) : string.Empty;
                iconId = Main.GetSuggestedIconIdForType(typeStr);
                if (!hasCustomIcon && !string.IsNullOrWhiteSpace(iconId))
                {
                    Main.CustomSkillIdToIconId[runtimeSkillId] = iconId;
                }

                appendedCustomSkillLogs.Add((
                    typeStr,
                    runtimeSkillId,
                    appendedRow.OriginalSkillId.ToString(CultureInfo.InvariantCulture),
                    appendedRow.Origin?.SourceName ?? "未知来源",
                    iconId,
                    hasCustomIcon,
                    customIconPath));
                nextInjectedSkillId++;
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
                    .Where(x => int.TryParse(x.RuntimeSkillId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId) && skillId < firstMissingId)
                    .ToList();

            if (visibleAppendedLogs.Count > 0)
            {
                foreach (var group in visibleAppendedLogs.GroupBy(x => string.IsNullOrWhiteSpace(x.SkillType) ? "未分类" : x.SkillType))
                {
                    Main.LOG.Msg(ConsoleColor.Cyan, $"新增功法[{group.Key}]：");
                    foreach (var item in group)
                    {
                        string idText = item.RuntimeSkillId == item.SourceSkillId
                            ? $"skillID={item.RuntimeSkillId}"
                            : $"skillID={item.RuntimeSkillId} (源ID={item.SourceSkillId})";
                        string iconFileName = string.IsNullOrWhiteSpace(item.CustomIconPath)
                            ? string.Empty
                            : Path.GetFileName(item.CustomIconPath);

                        if (item.HasCustomIcon)
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - {idText}，来源={item.SourceName}，自定义图标={iconFileName}");
                        }
                        else if (string.IsNullOrWhiteSpace(item.IconId))
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - {idText}，来源={item.SourceName}");
                        }
                        else
                        {
                            Main.LOG.Msg(ConsoleColor.Cyan, $"  - {idText}，来源={item.SourceName}，分配图标ID={item.IconId}");
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

    [HarmonyPatch(typeof(GameDataController), nameof(GameDataController.LoadAllGameData))]
    public static class GameDataControllerLoadAllGameDataPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameDataController __instance)
        {
            Main.AddCustomSkillsToJianghuBookPool(__instance);
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
                skillId < Main.VanillaKungFuTotalCount &&
                !Main.RuntimeSkillAssetBindings.ContainsKey(spriteName))
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

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.GetItemIconName))]
    public static class ItemDataGetItemIconNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemData __instance, ref string __result)
        {
            if (__instance == null || string.IsNullOrWhiteSpace(__result))
            {
                return;
            }

            if ((int)__instance.type == 3)
            {

                if (__instance.bookData != null)
                {
                    KungfuSkillData skillData = __instance.bookData.DataBase();
                    if (skillData != null)
                    {
                        if (__result.StartsWith("book_", StringComparison.Ordinal))
                        {
                            string[] parts = __result.Split('_');
                            if (parts.Length == 3 && skillData.belongForceID > 19)
                            {
                                __result = $"book_-1_{skillData.type}_{parts[2]}";
                            }
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameDataController), nameof(GameDataController.FindBookTypeIconDataBase))]
    public static class GameDataControllerFindBookTypeIconDataBasePatch
    {
        [ThreadStatic]
        private static bool _isResolvingFallback;

        [HarmonyPrefix]
        public static bool Prefix(GameDataController __instance, ItemData targetItem, ref BookTypeIconData __result)
        {
            if (__instance == null || targetItem?.bookData == null || _isResolvingFallback)
            {
                return true;
            }

            KungfuSkillData skillData = targetItem.bookData.DataBase();
            if (skillData == null)
            {
                return true;
            }

            // 原版覆盖功法也可能把所属势力改到高位索引，这时同样需要绕过游戏原生查表。
            if (skillData.belongForceID <= 19)
            {
                return true;
            }

            string skillId = skillData.skillID.ToString(CultureInfo.InvariantCulture);
            if (!Main.TryGetFallbackIconIdForSkill(skillData, out int fallbackSkillId))
            {
                Main.LOG.Warning(
                    $"[秘籍类别角标回退] 未找到可用的原版回退目标：skillID={skillId}, skillType={skillData.type}");
                __result = null;
                return false;
            }

            if (fallbackSkillId < 0 || fallbackSkillId > Main.VanillaKungFuMaxId)
            {
                Main.LOG.Warning(
                    $"[秘籍类别角标回退] 回退目标超出原版范围：skillID={skillId}, fallbackSkillID={fallbackSkillId}, vanillaMaxID={Main.VanillaKungFuMaxId}");
                __result = null;
                return false;
            }

            int safeRareLv = targetItem.rareLv;
            if (safeRareLv < 0 || safeRareLv > 5)
            {
                int originalRareLv = safeRareLv;
                safeRareLv = Math.Clamp(safeRareLv, 0, 5);
                Main.LOG.Warning(
                    $"[秘籍类别角标回退] 书籍等级超出范围，已自动修正：skillID={skillId}, fallbackSkillID={fallbackSkillId}, rareLv={originalRareLv} -> {safeRareLv}");
            }

            try
            {
                _isResolvingFallback = true;

                ItemData fallbackItem = new(ItemType.Book);
                fallbackItem.SetBookData(fallbackSkillId, safeRareLv);
                BookTypeIconData fallbackIconData = __instance.FindBookTypeIconDataBase(fallbackItem);
                if (fallbackIconData != null)
                {
                    __result = fallbackIconData;
                    return false;
                }

                Main.LOG.Warning(
                    $"[秘籍类别角标回退] 未找到原版角标配置：skillID={skillId}, fallbackSkillID={fallbackSkillId}");
                __result = null;
                return false;
            }
            catch (Exception ex)
            {
                Main.LOG.Warning(
                    $"[秘籍类别角标回退] 处理失败：skillID={skillId}, fallbackSkillID={fallbackSkillId}，{ex.GetType().Name} - {ex.Message}");
                __result = null;
                return false;
            }
            finally
            {
                _isResolvingFallback = false;
            }
        }
    }
}
