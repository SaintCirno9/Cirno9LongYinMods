using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;

namespace ForceOverflowDividend
{
    internal sealed class ForceOverflowConfig
    {
        internal const int DefaultMoneyOverflowMoneyRatePercent = 100;
        internal const int DefaultOtherOverflowMoneyRatePercent = 10;

        internal bool Enabled { get; set; } = true;

        internal bool VerboseLog { get; set; }

        internal int FallbackOtherResourceRatePercent { get; set; } = DefaultOtherOverflowMoneyRatePercent;

        internal Dictionary<int, int> ResourceOverflowMoneyRatePercentById { get; } = new();

        internal static ForceOverflowConfig CreateDefault()
        {
            var config = new ForceOverflowConfig();
            config.EnsureResourceRateCoverage(1);
            return config;
        }

        internal int GetOverflowMoneyRatePercent(int resourceId)
        {
            if (ResourceOverflowMoneyRatePercentById.TryGetValue(resourceId, out int ratePercent))
            {
                return ratePercent;
            }

            return GetDefaultOverflowMoneyRatePercent(resourceId, FallbackOtherResourceRatePercent);
        }

        internal void SetOverflowMoneyRatePercent(int resourceId, int ratePercent)
        {
            if (resourceId < 0)
            {
                return;
            }

            ResourceOverflowMoneyRatePercentById[resourceId] = Math.Max(0, ratePercent);
        }

        internal bool EnsureResourceRateCoverage(int resourceCount)
        {
            bool changed = false;

            for (int i = 0; i < resourceCount; i++)
            {
                if (ResourceOverflowMoneyRatePercentById.ContainsKey(i))
                {
                    continue;
                }

                ResourceOverflowMoneyRatePercentById[i] = GetDefaultOverflowMoneyRatePercent(i, FallbackOtherResourceRatePercent);
                changed = true;
            }

            return changed;
        }

        private static int GetDefaultOverflowMoneyRatePercent(int resourceId, int fallbackOtherRatePercent)
        {
            return resourceId == 0
                ? DefaultMoneyOverflowMoneyRatePercent
                : Math.Max(0, fallbackOtherRatePercent);
        }
    }

    internal static class ForceOverflowConfigStore
    {
        private const string ConfigFileName = "ForceOverflowDividend.cfg";

        internal const string ConfigPathDisplay = @"UserData\ForceOverflowDividend.cfg";

        internal static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", ConfigFileName));

        internal static ForceOverflowConfig Load(MelonLogger.Instance log)
        {
            ForceOverflowConfig config = ForceOverflowConfig.CreateDefault();
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
                config = ForceOverflowConfig.CreateDefault();
                shouldRewrite = true;
            }

            if (shouldRewrite)
            {
                WriteConfig(config, log);
            }

            return config;
        }

        private static bool TryReadConfig(IEnumerable<string> lines, ForceOverflowConfig config, MelonLogger.Instance log)
        {
            bool seenEnabled = false;
            bool seenVerboseLog = false;
            bool seenAnyResourceRate = false;
            bool isValid = true;
            int legacyOtherRatePercent = ForceOverflowConfig.DefaultOtherOverflowMoneyRatePercent;
            bool seenLegacyOtherRatePercent = false;

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

                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsed))
                    {
                        config.Enabled = parsed;
                        seenEnabled = true;
                    }
                    else
                    {
                        log.Warning($"Enabled 配置无效，将回退默认值：{value}");
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
                else if (key.StartsWith("ResourceRate.", StringComparison.OrdinalIgnoreCase))
                {
                    string idText = key["ResourceRate.".Length..].Trim();
                    if (!int.TryParse(idText, out int resourceId) || resourceId < 0)
                    {
                        log.Warning($"资源比例配置项无效，将按默认值重写：{key}");
                        isValid = false;
                        continue;
                    }

                    if (TryParseNonNegativeInt(value, out int parsed))
                    {
                        config.SetOverflowMoneyRatePercent(resourceId, parsed);
                        seenAnyResourceRate = true;
                    }
                    else
                    {
                        log.Warning($"{key} 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("NonMoneyOverflowMoneyRatePercent", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseNonNegativeInt(value, out int parsed))
                    {
                        legacyOtherRatePercent = parsed;
                        seenLegacyOtherRatePercent = true;
                    }
                    else
                    {
                        log.Warning($"旧版 NonMoneyOverflowMoneyRatePercent 配置无效，将回退默认值：{value}");
                    }

                    isValid = false;
                }
                else
                {
                    log.Warning($"发现未知配置项，将保留其余有效配置并重写文件：{key}");
                    isValid = false;
                }
            }

            if (seenLegacyOtherRatePercent)
            {
                config.FallbackOtherResourceRatePercent = legacyOtherRatePercent;
            }

            if (!seenAnyResourceRate)
            {
                config.EnsureResourceRateCoverage(1);
                isValid = false;
            }

            return isValid && seenEnabled && seenVerboseLog;
        }

        internal static void SyncKnownResourceRates(ForceOverflowConfig config, MelonLogger.Instance log)
        {
            int managedResourceCount = GetManagedResourceCount();
            if (config == null || managedResourceCount <= 0)
            {
                return;
            }

            if (config.EnsureResourceRateCoverage(managedResourceCount))
            {
                WriteConfig(config, log);
            }
        }

        private static bool TryParseNonNegativeInt(string value, out int parsed)
        {
            return int.TryParse(value, out parsed) && parsed >= 0;
        }

        private static void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteConfig(ForceOverflowConfig config, MelonLogger.Instance log)
        {
            try
            {
                EnsureDirectoryExists();

                config.EnsureResourceRateCoverage(1);

                var lines = new List<string>
                {
                    "# ForceOverflowDividend 配置文件",
                    "# 每次门派资源变动后立即检查资源溢出。",
                    "# 除最后一项威望外，其他资源都可以单独配置溢出后折现为金钱的比例。",
                    "# ResourceRate.<id> 表示该资源溢出部分按基础价值折现为金钱的百分比，允许为大于等于 0 的整数。",
                    "# 默认规则为：钱资源 100%，其他资源 10%。如果首次生成时资源表尚未加载，后续进入游戏后会自动补齐全部可处理资源项。",
                    "# VerboseLog 表示是否输出详细过程日志。",
                    $"Enabled={config.Enabled.ToString().ToLowerInvariant()}",
                    $"VerboseLog={config.VerboseLog.ToString().ToLowerInvariant()}",
                    string.Empty,
                    "# 资源折现比例",
                };

                int managedResourceCount = GetManagedResourceCount();
                if (managedResourceCount > 0)
                {
                    config.EnsureResourceRateCoverage(managedResourceCount);
                }

                var resourceIds = new SortedSet<int>(config.ResourceOverflowMoneyRatePercentById.Keys);
                if (managedResourceCount > 0)
                {
                    for (int i = 0; i < managedResourceCount; i++)
                    {
                        resourceIds.Add(i);
                    }
                }

                foreach (int resourceId in resourceIds)
                {
                    string resourceName = GetResourceName(resourceId);
                    lines.Add($"# {resourceId}: {resourceName}");
                    lines.Add($"ResourceRate.{resourceId}={config.GetOverflowMoneyRatePercent(resourceId)}");
                }

                File.WriteAllLines(ConfigPath, lines);
            }
            catch (Exception ex)
            {
                log.Error($"写入配置失败：{ConfigPath}，{ex}");
            }
        }

        private static string GetResourceName(int resourceId)
        {
            if (GlobalData.ResourceName != null &&
                resourceId >= 0 &&
                resourceId < GlobalData.ResourceName.Count)
            {
                return GlobalData.ResourceName[resourceId] ?? $"资源{resourceId}";
            }

            return $"资源{resourceId}";
        }

        private static int GetManagedResourceCount()
        {
            if (GlobalData.ResourceName == null || GlobalData.ResourceName.Count <= 0)
            {
                return 0;
            }

            // 最后一项是威望，不参与本 mod 的溢出折现和配置生成。
            return Math.Max(0, GlobalData.ResourceName.Count - 1);
        }
    }
}
