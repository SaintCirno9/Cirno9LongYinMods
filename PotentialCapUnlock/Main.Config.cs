using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace PotentialCapUnlock
{
    internal sealed class PotentialCapConfig
    {
        internal const int DefaultAttriCap = 1000;
        internal const int DefaultFightSkillCap = 1000;
        internal const int DefaultLivingSkillCap = 1000;

        internal bool EnableCapOverride { get; set; } = true;

        internal int AttriCap { get; set; } = DefaultAttriCap;

        internal int FightSkillCap { get; set; } = DefaultFightSkillCap;

        internal int LivingSkillCap { get; set; } = DefaultLivingSkillCap;

        internal bool VerboseLog { get; set; }

        internal static PotentialCapConfig CreateDefault()
        {
            return new PotentialCapConfig();
        }
    }

    internal static class PotentialCapConfigStore
    {
        private const string ConfigFileName = "PotentialCapUnlock.cfg";
        private const string ConfigPathDisplay = @"UserData\PotentialCapUnlock.cfg";

        internal static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", ConfigFileName));

        internal static PotentialCapConfig Load(MelonLogger.Instance log)
        {
            PotentialCapConfig config = PotentialCapConfig.CreateDefault();
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
                log.Warning($"读取配置失败，将回退为默认配置并重写文件：{ex.Message}");
                config = PotentialCapConfig.CreateDefault();
                shouldRewrite = true;
            }

            if (shouldRewrite)
            {
                WriteConfig(config, log);
            }

            return config;
        }

        private static bool TryReadConfig(IEnumerable<string> lines, PotentialCapConfig config, MelonLogger.Instance log)
        {
            bool seenEnableCapOverride = false;
            bool seenAttriCap = false;
            bool seenFightSkillCap = false;
            bool seenLivingSkillCap = false;
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

                if (key.Equals("EnableCapOverride", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.EnableCapOverride = parsed;
                        seenEnableCapOverride = true;
                    }
                    else
                    {
                        log.Warning($"EnableCapOverride 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("AttriCap", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParsePositiveInt(value, out int parsed))
                    {
                        config.AttriCap = parsed;
                        seenAttriCap = true;
                    }
                    else
                    {
                        log.Warning($"AttriCap 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("FightSkillCap", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParsePositiveInt(value, out int parsed))
                    {
                        config.FightSkillCap = parsed;
                        seenFightSkillCap = true;
                    }
                    else
                    {
                        log.Warning($"FightSkillCap 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("LivingSkillCap", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParsePositiveInt(value, out int parsed))
                    {
                        config.LivingSkillCap = parsed;
                        seenLivingSkillCap = true;
                    }
                    else
                    {
                        log.Warning($"LivingSkillCap 配置无效，将回退默认值：{value}");
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

            return isValid && seenEnableCapOverride && seenAttriCap && seenFightSkillCap && seenLivingSkillCap && seenVerboseLog;
        }

        private static bool TryParsePositiveInt(string value, out int parsed)
        {
            return int.TryParse(value, out parsed) && parsed > 0;
        }

        private static void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteConfig(PotentialCapConfig config, MelonLogger.Instance log)
        {
            try
            {
                EnsureDirectoryExists();

                string[] lines =
                {
                    "# PotentialCapUnlock 配置文件",
                    "# 原版 GlobalData::.cctor 中的默认上限为：六维 120、武学 120、技艺 100。",
                    "# EnableCapOverride: 是否启用硬上限修改；关闭后会恢复原版硬上限。",
                    "# 下面三个上限都必须是大于 0 的整数；非法值会回退为默认值并重写本文件。",
                    "# VerboseLog: 是否输出详细日志。",
                    $"EnableCapOverride={config.EnableCapOverride.ToString().ToLowerInvariant()}",
                    $"AttriCap={Math.Max(1, config.AttriCap)}",
                    $"FightSkillCap={Math.Max(1, config.FightSkillCap)}",
                    $"LivingSkillCap={Math.Max(1, config.LivingSkillCap)}",
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
