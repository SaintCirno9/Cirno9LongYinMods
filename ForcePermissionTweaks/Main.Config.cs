using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace ForcePermissionTweaks
{
    internal enum BuildButtonVisibilityMode
    {
        Original = 0,
        PlayerForceAreaOnly = 1,
        PlayerForceAndServantForce = 2,
        PlayerForceServantAndAllyForce = 3,
        Always = 4
    }

    internal enum ForceRoleRequirement
    {
        Original = -1,
        OuterDisciple = 0,
        BeginnerDisciple = 1,
        FormalDisciple = 2,
        DirectDisciple = 3,
        Elder = 4,
        ViceLeader = 5,
        Leader = 6
    }

    internal sealed class ForcePermissionConfig
    {
        internal bool EnableBuildButtonOverride { get; set; } = true;

        internal BuildButtonVisibilityMode BuildButtonVisibilityMode { get; set; } = BuildButtonVisibilityMode.PlayerForceAndServantForce;

        internal ForceRoleRequirement BuildButtonMinimumRole { get; set; } = ForceRoleRequirement.DirectDisciple;

        internal bool EnableSpeBookStorageOverride { get; set; } = true;

        internal BuildButtonVisibilityMode SpeBookStorageVisibilityMode { get; set; } = BuildButtonVisibilityMode.PlayerForceAndServantForce;

        internal ForceRoleRequirement SpeBookStorageMinimumRole { get; set; } = ForceRoleRequirement.ViceLeader;

        internal bool IgnoreCenterBuildingDiscipleCountForUpgrade { get; set; } = true;

        internal bool VerboseLog { get; set; }

        internal static ForcePermissionConfig CreateDefault()
        {
            return new ForcePermissionConfig();
        }
    }

    internal static class ForcePermissionConfigStore
    {
        private const string ConfigFileName = "ForcePermissionTweaks.cfg";

        internal const string ConfigPathDisplay = @"UserData\ForcePermissionTweaks.cfg";

        internal static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", ConfigFileName));

        private static readonly Dictionary<string, BuildButtonVisibilityMode> VisibilityModeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Original"] = BuildButtonVisibilityMode.Original,
            ["PlayerForceAreaOnly"] = BuildButtonVisibilityMode.PlayerForceAreaOnly,
            ["PlayerForceAndServantForce"] = BuildButtonVisibilityMode.PlayerForceAndServantForce,
            ["PlayerForceServantAndAllyForce"] = BuildButtonVisibilityMode.PlayerForceServantAndAllyForce,
            ["Always"] = BuildButtonVisibilityMode.Always
        };

        private static readonly Dictionary<string, ForceRoleRequirement> RoleRequirementMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Original"] = ForceRoleRequirement.Original,
            ["外门弟子"] = ForceRoleRequirement.OuterDisciple,
            ["入门弟子"] = ForceRoleRequirement.BeginnerDisciple,
            ["正式弟子"] = ForceRoleRequirement.FormalDisciple,
            ["亲传弟子"] = ForceRoleRequirement.DirectDisciple,
            ["长老"] = ForceRoleRequirement.Elder,
            ["副掌门"] = ForceRoleRequirement.ViceLeader,
            ["掌门"] = ForceRoleRequirement.Leader
        };

        internal static ForcePermissionConfig Load(MelonLogger.Instance log)
        {
            ForcePermissionConfig config = ForcePermissionConfig.CreateDefault();
            bool shouldRewrite = false;

            try
            {
                EnsureDirectoryExists();

                if (!File.Exists(ConfigPath))
                {
                    shouldRewrite = true;
                }
                else
                {
                    shouldRewrite = !TryReadConfig(File.ReadAllLines(ConfigPath), config, log);
                }
            }
            catch (Exception ex)
            {
                log.Warning($"读取配置失败，将回退默认配置并重写文件：{ex.Message}");
                config = ForcePermissionConfig.CreateDefault();
                shouldRewrite = true;
            }

            if (shouldRewrite)
            {
                WriteConfig(config, log);
            }

            return config;
        }

        internal static string FormatVisibilityMode(BuildButtonVisibilityMode mode)
        {
            foreach (KeyValuePair<string, BuildButtonVisibilityMode> pair in VisibilityModeMap)
            {
                if (pair.Value == mode)
                {
                    return pair.Key;
                }
            }

            return nameof(BuildButtonVisibilityMode.PlayerForceAndServantForce);
        }

        internal static string FormatRoleRequirement(ForceRoleRequirement role)
        {
            foreach (KeyValuePair<string, ForceRoleRequirement> pair in RoleRequirementMap)
            {
                if (pair.Value == role)
                {
                    return pair.Key;
                }
            }

            return "亲传弟子";
        }

        private static bool TryReadConfig(IEnumerable<string> lines, ForcePermissionConfig config, MelonLogger.Instance log)
        {
            bool seenEnableBuildButtonOverride = false;
            bool seenBuildButtonVisibilityMode = false;
            bool seenBuildButtonMinimumRole = false;
            bool seenEnableSpeBookStorageOverride = false;
            bool seenSpeBookStorageVisibilityMode = false;
            bool seenSpeBookStorageMinimumRole = false;
            bool seenIgnoreCenterBuildingDiscipleCountForUpgrade = false;
            bool seenVerboseLog = false;
            bool isValid = true;

            foreach (string rawLine in lines)
            {
                string line = rawLine?.Trim() ?? string.Empty;
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
                {
                    log.Warning($"发现无效配置行，将按默认值重写：{line}");
                    isValid = false;
                    continue;
                }

                string key = line[..separatorIndex].Trim();
                string value = line[(separatorIndex + 1)..].Trim();

                if (key.Equals("EnableBuildButtonOverride", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.EnableBuildButtonOverride = parsed;
                        seenEnableBuildButtonOverride = true;
                    }
                    else
                    {
                        log.Warning($"EnableBuildButtonOverride 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("BuildButtonVisibilityMode", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseVisibilityMode(value, out BuildButtonVisibilityMode parsed))
                    {
                        config.BuildButtonVisibilityMode = parsed;
                        seenBuildButtonVisibilityMode = true;
                    }
                    else
                    {
                        log.Warning($"BuildButtonVisibilityMode 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("BuildButtonMinimumRole", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseRoleRequirement(value, out ForceRoleRequirement parsed))
                    {
                        config.BuildButtonMinimumRole = parsed;
                        seenBuildButtonMinimumRole = true;
                    }
                    else
                    {
                        log.Warning($"BuildButtonMinimumRole 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("EnableSpeBookStorageOverride", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.EnableSpeBookStorageOverride = parsed;
                        seenEnableSpeBookStorageOverride = true;
                    }
                    else
                    {
                        log.Warning($"EnableSpeBookStorageOverride 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("SpeBookStorageVisibilityMode", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseVisibilityMode(value, out BuildButtonVisibilityMode parsed))
                    {
                        config.SpeBookStorageVisibilityMode = parsed;
                        seenSpeBookStorageVisibilityMode = true;
                    }
                    else
                    {
                        log.Warning($"SpeBookStorageVisibilityMode 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("SpeBookStorageMinimumRole", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseRoleRequirement(value, out ForceRoleRequirement parsed))
                    {
                        config.SpeBookStorageMinimumRole = parsed;
                        seenSpeBookStorageMinimumRole = true;
                    }
                    else
                    {
                        log.Warning($"SpeBookStorageMinimumRole 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("IgnoreCenterBuildingDiscipleCountForUpgrade", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.IgnoreCenterBuildingDiscipleCountForUpgrade = parsed;
                        seenIgnoreCenterBuildingDiscipleCountForUpgrade = true;
                    }
                    else
                    {
                        log.Warning($"IgnoreCenterBuildingDiscipleCountForUpgrade 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("VerboseLog", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.VerboseLog = parsed;
                        seenVerboseLog = true;
                    }
                    else
                    {
                        log.Warning($"VerboseLog 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else
                {
                    log.Warning($"发现未知配置项，将保留其余有效配置并重写文件：{key}");
                    isValid = false;
                }
            }

            return isValid &&
                seenEnableBuildButtonOverride &&
                seenBuildButtonVisibilityMode &&
                seenBuildButtonMinimumRole &&
                seenEnableSpeBookStorageOverride &&
                seenSpeBookStorageVisibilityMode &&
                seenSpeBookStorageMinimumRole &&
                seenIgnoreCenterBuildingDiscipleCountForUpgrade &&
                seenVerboseLog;
        }

        private static bool TryParseVisibilityMode(string value, out BuildButtonVisibilityMode mode)
        {
            return VisibilityModeMap.TryGetValue(value, out mode);
        }

        private static bool TryParseRoleRequirement(string value, out ForceRoleRequirement role)
        {
            return RoleRequirementMap.TryGetValue(value, out role);
        }

        private static void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteConfig(ForcePermissionConfig config, MelonLogger.Instance log)
        {
            try
            {
                EnsureDirectoryExists();

                string[] lines =
                {
                    "# ForcePermissionTweaks 配置文件",
                    "# 原版建筑管理按钮规则：仅玩家所属势力区域显示，亲传弟子及以上可点击。",
                    "# 原版藏经阁相关按钮规则：沿用建筑选项原始条件；本 Mod 可单独覆盖显示和点击要求。",
                    "# EnableBuildButtonOverride: 是否启用建筑管理按钮权限覆盖。",
                    "# BuildButtonVisibilityMode: Original / PlayerForceAreaOnly / PlayerForceAndServantForce / PlayerForceServantAndAllyForce / Always。",
                    "# BuildButtonMinimumRole: Original / 外门弟子 / 入门弟子 / 正式弟子 / 亲传弟子 / 长老 / 副掌门 / 掌门。",
                    "# EnableSpeBookStorageOverride: 是否启用藏经阁相关按钮权限覆盖。",
                    "# SpeBookStorageVisibilityMode: 与 BuildButtonVisibilityMode 使用相同合法值。",
                    "# SpeBookStorageMinimumRole: 与 BuildButtonMinimumRole 使用相同合法值。",
                    "# IgnoreCenterBuildingDiscipleCountForUpgrade: 是否忽略门派正厅升级所需弟子数。",
                    "# 非法值、缺失项或未知项会回退默认值并重写本文件。",
                    "# VerboseLog: 是否输出详细日志。",
                    $"EnableBuildButtonOverride={config.EnableBuildButtonOverride.ToString().ToLowerInvariant()}",
                    $"BuildButtonVisibilityMode={FormatVisibilityMode(config.BuildButtonVisibilityMode)}",
                    $"BuildButtonMinimumRole={FormatRoleRequirement(config.BuildButtonMinimumRole)}",
                    $"EnableSpeBookStorageOverride={config.EnableSpeBookStorageOverride.ToString().ToLowerInvariant()}",
                    $"SpeBookStorageVisibilityMode={FormatVisibilityMode(config.SpeBookStorageVisibilityMode)}",
                    $"SpeBookStorageMinimumRole={FormatRoleRequirement(config.SpeBookStorageMinimumRole)}",
                    $"IgnoreCenterBuildingDiscipleCountForUpgrade={config.IgnoreCenterBuildingDiscipleCountForUpgrade.ToString().ToLowerInvariant()}",
                    $"VerboseLog={config.VerboseLog.ToString().ToLowerInvariant()}",
                };

                File.WriteAllLines(ConfigPath, lines);
            }
            catch (Exception ex)
            {
                log.Error($"写入配置失败：{ConfigPath}，{ex}");
            }
        }
    }
}
