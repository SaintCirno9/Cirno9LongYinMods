using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MelonLoader;

namespace InfoTabRightSide
{
    internal sealed class InfoTabRightSideConfig
    {
        internal const float DefaultOffsetX = -80f;

        internal bool Enabled { get; set; } = true;

        internal float OffsetX { get; set; } = DefaultOffsetX;

        internal float OffsetY { get; set; }

        internal static InfoTabRightSideConfig CreateDefault()
        {
            return new InfoTabRightSideConfig();
        }
    }

    internal static class InfoTabRightSideConfigStore
    {
        private const string ConfigFileName = "InfoTabRightSide.cfg";

        internal const string ConfigPathDisplay = @"UserData\InfoTabRightSide.cfg";

        internal static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", ConfigFileName));

        internal static InfoTabRightSideConfig Load(MelonLogger.Instance log)
        {
            InfoTabRightSideConfig config = InfoTabRightSideConfig.CreateDefault();
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
                config = InfoTabRightSideConfig.CreateDefault();
                shouldRewrite = true;
            }

            if (shouldRewrite)
            {
                WriteConfig(config, log);
            }

            return config;
        }

        private static bool TryReadConfig(IEnumerable<string> lines, InfoTabRightSideConfig config, MelonLogger.Instance log)
        {
            bool seenEnabled = false;
            bool seenOffsetX = false;
            bool seenOffsetY = false;
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

                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool enabled))
                    {
                        config.Enabled = enabled;
                        seenEnabled = true;
                    }
                    else
                    {
                        log.Warning($"Enabled 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("OffsetX", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(value, out float offsetX))
                    {
                        config.OffsetX = offsetX;
                        seenOffsetX = true;
                    }
                    else
                    {
                        log.Warning($"OffsetX 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else if (key.Equals("OffsetY", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseFloat(value, out float offsetY))
                    {
                        config.OffsetY = offsetY;
                        seenOffsetY = true;
                    }
                    else
                    {
                        log.Warning($"OffsetY 配置无效，将回退默认值：{value}");
                        isValid = false;
                    }
                }
                else
                {
                    log.Warning($"发现未知配置项，将保留其余有效配置并重写文件：{key}");
                    isValid = false;
                }
            }

            return isValid && seenEnabled && seenOffsetX && seenOffsetY;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result);
        }

        private static void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteConfig(InfoTabRightSideConfig config, MelonLogger.Instance log)
        {
            try
            {
                EnsureDirectoryExists();

                string[] lines =
                {
                    "# InfoTabRightSide 配置文件",
                    "# Enabled: 是否启用右侧提示列和右侧滑入动画。",
                    "# OffsetX: 水平偏移，正数向右，负数向左。当前默认值适配常见 16:9 分辨率。",
                    "# OffsetY: 垂直偏移，正数向上，负数向下。",
                    "# 缺失项、未知项或非法值都会回退默认值并重写本文件。",
                    $"Enabled={config.Enabled.ToString().ToLowerInvariant()}",
                    $"OffsetX={config.OffsetX.ToString(CultureInfo.InvariantCulture)}",
                    $"OffsetY={config.OffsetY.ToString(CultureInfo.InvariantCulture)}",
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
