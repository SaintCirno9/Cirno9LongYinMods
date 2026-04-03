using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace LongYinPlayerSpeSkeleton
{
    internal readonly struct StaticImageSceneConfig
    {
        public StaticImageSceneConfig(float insetRatio, float offsetX, float offsetY)
        {
            InsetRatio = insetRatio;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public float InsetRatio { get; }

        public float OffsetX { get; }

        public float OffsetY { get; }

        public Vector3 OffsetVector3 => new(OffsetX, OffsetY, 0f);

        public override string ToString()
        {
            return $"InsetRatio={InsetRatio}, Offset=({OffsetX}, {OffsetY})";
        }
    }

    public sealed partial class Main
    {
        private const string HeroDetailStaticImageKeyPrefix = "StaticImageHeroDetail";
        private const string CharacterCreationStaticImageKeyPrefix = "StaticImageCharacterCreation";
        private const string HudAvatarStaticImageKeyPrefix = "StaticImageHudAvatar";
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinPlayerSpeSkeleton");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "LongYinPlayerSpeSkeleton.cfg");
        internal static readonly string PortraitsDir = Path.Combine(ConfigDir, "Portraits");
        private const string ConfigPathDisplay = "UserData\\LongYinPlayerSpeSkeleton\\LongYinPlayerSpeSkeleton.cfg";
        private static readonly StaticImageSceneConfig DefaultHeroDetailStaticImageConfig = new(1f, 0f, -90f);
        private static readonly StaticImageSceneConfig DefaultCharacterCreationStaticImageConfig = new(1f, 0f, -60f);
        private static readonly StaticImageSceneConfig DefaultHudAvatarStaticImageConfig = new(0.8f, 15f, -70f);

        private static bool _enabled = true;
        private static string _playerSpeHeroName = string.Empty;
        private static bool _replaceInCharacterCreation = true;
        private static PlayerSkeletonRenderMode _renderMode = PlayerSkeletonRenderMode.Skeleton;
        private static string _staticImagePath = @"Portraits\白云天.png";
        private static StaticImageSceneConfig _heroDetailStaticImageConfig = DefaultHeroDetailStaticImageConfig;
        private static StaticImageSceneConfig _characterCreationStaticImageConfig = DefaultCharacterCreationStaticImageConfig;
        private static StaticImageSceneConfig _hudAvatarStaticImageConfig = DefaultHudAvatarStaticImageConfig;
        private static bool _verboseLog;
        private static DateTime _configLastWriteTimeUtc = DateTime.MinValue;

        internal static bool Enabled
        {
            get
            {
                EnsureConfigLoaded();
                return _enabled;
            }
        }

        internal static string PlayerSpeHeroName
        {
            get
            {
                EnsureConfigLoaded();
                return _playerSpeHeroName ?? string.Empty;
            }
        }

        internal static bool VerboseLog
        {
            get
            {
                EnsureConfigLoaded();
                return _verboseLog;
            }
        }

        internal static bool ReplaceInCharacterCreation
        {
            get
            {
                EnsureConfigLoaded();
                return _replaceInCharacterCreation;
            }
        }

        internal static PlayerSkeletonRenderMode RenderMode
        {
            get
            {
                EnsureConfigLoaded();
                return _renderMode;
            }
        }

        internal static string StaticImagePath
        {
            get
            {
                EnsureConfigLoaded();
                return _staticImagePath ?? string.Empty;
            }
        }

        internal static StaticImageSceneConfig HeroDetailStaticImageConfig
        {
            get
            {
                EnsureConfigLoaded();
                return _heroDetailStaticImageConfig;
            }
        }

        internal static StaticImageSceneConfig CharacterCreationStaticImageConfig
        {
            get
            {
                EnsureConfigLoaded();
                return _characterCreationStaticImageConfig;
            }
        }

        internal static StaticImageSceneConfig HudAvatarStaticImageConfig
        {
            get
            {
                EnsureConfigLoaded();
                return _hudAvatarStaticImageConfig;
            }
        }

        internal static void EnsureConfigLoaded()
        {
            try
            {
                EnsureConfigExists();
                DateTime lastWriteTimeUtc = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : DateTime.MinValue;
                if (lastWriteTimeUtc != _configLastWriteTimeUtc)
                {
                    ReloadConfig(forceLog: false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"检查配置文件失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(PortraitsDir);
            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(
                    ConfigPath,
                    string.Join(Environment.NewLine, BuildDefaultConfigLines()),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }

            EnsureConfigKeyExists(
                "RenderMode",
                new[]
                {
                    "# RenderMode: 0=原版人物动态立绘，1=静态图片",
                    "RenderMode=0"
                });
            EnsureConfigKeyExists(
                "StaticImagePath",
                new[]
                {
                    "# StaticImagePath: 静态图片路径，可写绝对路径，也可写相对 UserData\\LongYinPlayerSpeSkeleton 的路径",
                    @"StaticImagePath=Portraits\白云天.png"
                });
            EnsureStaticImageSceneConfigKeys(HeroDetailStaticImageKeyPrefix, "人物详情界面", "1.0 表示按当前裁切框完整 cover", DefaultHeroDetailStaticImageConfig);
            EnsureStaticImageSceneConfigKeys(CharacterCreationStaticImageKeyPrefix, "创建人物界面", "1.0 表示按边框完整 fit", DefaultCharacterCreationStaticImageConfig);
            EnsureStaticImageSceneConfigKeys(HudAvatarStaticImageKeyPrefix, "左上角头像界面", "1.0 表示按头像框完整 cover", DefaultHudAvatarStaticImageConfig);
            RemoveConfigKey("ReplaceInCharacterCreation");
            RemoveConfigKey("StaticImageHeroDetailOverflowBias");
        }

        private static IEnumerable<string> BuildDefaultConfigLines()
        {
            var lines = new List<string>
            {
                "# LongYinPlayerSpeSkeleton 独立配置文件",
                string.Empty,
                "# Enabled: 是否启用玩家特殊立绘替换",
                "Enabled=true",
                string.Empty,
                "# PlayerSpeHeroName: 玩家要借用的特殊人物名字，必须是有特殊立绘的，仅当RenderMode=0时有效",
                "PlayerSpeHeroName=白云天",
                string.Empty,
                "# RenderMode: 0=原版人物动态立绘，1=静态图片",
                "RenderMode=0",
                string.Empty,
                "# StaticImagePath: 静态图片路径，可写绝对路径，也可写相对 UserData\\LongYinPlayerSpeSkeleton 的路径",
                @"StaticImagePath=Portraits\白云天.png",
                string.Empty
            };

            lines.AddRange(BuildStaticImageSceneConfigLines(HeroDetailStaticImageKeyPrefix, "人物详情界面", "1.0 表示按当前裁切框完整 cover", DefaultHeroDetailStaticImageConfig));
            lines.AddRange(BuildStaticImageSceneConfigLines(CharacterCreationStaticImageKeyPrefix, "创建人物界面", "1.0 表示按边框完整 fit", DefaultCharacterCreationStaticImageConfig));
            lines.AddRange(BuildStaticImageSceneConfigLines(HudAvatarStaticImageKeyPrefix, "左上角头像界面", "1.0 表示按头像框完整 cover", DefaultHudAvatarStaticImageConfig));
            lines.AddRange(
                new[]
                {
                    "# VerboseLog: 是否输出详细日志",
                    "VerboseLog=false",
                    string.Empty
                });

            return lines;
        }

        private static IEnumerable<string> BuildStaticImageSceneConfigLines(string keyPrefix, string sceneLabel, string insetDescription, StaticImageSceneConfig defaultConfig)
        {
            return new[]
            {
                $"# {keyPrefix}InsetRatio: {sceneLabel}静态图缩放比例，{insetDescription}",
                $"{keyPrefix}InsetRatio={FormatFloatForConfig(defaultConfig.InsetRatio)}",
                string.Empty,
                $"# {keyPrefix}OffsetX / {keyPrefix}OffsetY: {sceneLabel}静态图额外偏移",
                $"{keyPrefix}OffsetX={FormatFloatForConfig(defaultConfig.OffsetX)}",
                $"{keyPrefix}OffsetY={FormatFloatForConfig(defaultConfig.OffsetY)}",
                string.Empty
            };
        }

        private static void EnsureStaticImageSceneConfigKeys(string keyPrefix, string sceneLabel, string insetDescription, StaticImageSceneConfig defaultConfig)
        {
            EnsureConfigKeyExists(
                $"{keyPrefix}InsetRatio",
                new[]
                {
                    $"# {keyPrefix}InsetRatio: {sceneLabel}静态图缩放比例，{insetDescription}",
                    $"{keyPrefix}InsetRatio={FormatFloatForConfig(defaultConfig.InsetRatio)}"
                });
            EnsureConfigKeyGroupExists(
                new[] { $"{keyPrefix}OffsetX", $"{keyPrefix}OffsetY" },
                new[]
                {
                    $"# {keyPrefix}OffsetX / {keyPrefix}OffsetY: {sceneLabel}静态图额外偏移",
                    $"{keyPrefix}OffsetX={FormatFloatForConfig(defaultConfig.OffsetX)}",
                    $"{keyPrefix}OffsetY={FormatFloatForConfig(defaultConfig.OffsetY)}"
                });
        }

        private static void EnsureConfigKeyExists(string key, string[] defaultLines)
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(ConfigPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (trimmedLine.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var mergedLines = new List<string>(lines);
            if (mergedLines.Count > 0 && mergedLines[^1].Length != 0)
            {
                mergedLines.Add(string.Empty);
            }

            mergedLines.AddRange(defaultLines);
            mergedLines.Add(string.Empty);
            File.WriteAllLines(ConfigPath, mergedLines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void EnsureConfigKeyGroupExists(string[] keys, string[] defaultLines)
        {
            if (!File.Exists(ConfigPath) || keys == null || keys.Length == 0 || defaultLines == null || defaultLines.Length == 0)
            {
                return;
            }

            string[] lines = File.ReadAllLines(ConfigPath);
            var missingKeys = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                foreach (string key in keys)
                {
                    if (trimmedLine.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        missingKeys.Remove(key);
                    }
                }
            }

            if (missingKeys.Count == 0)
            {
                return;
            }

            var mergedLines = new List<string>(lines);
            if (mergedLines.Count > 0 && mergedLines[^1].Length != 0)
            {
                mergedLines.Add(string.Empty);
            }

            string commentLine = defaultLines[0].StartsWith("#", StringComparison.Ordinal) ? defaultLines[0] : null;
            if (!string.IsNullOrWhiteSpace(commentLine))
            {
                mergedLines.Add(commentLine);
            }

            for (int i = 0; i < defaultLines.Length; i++)
            {
                string defaultLine = defaultLines[i];
                if (string.IsNullOrWhiteSpace(defaultLine) || defaultLine.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = defaultLine.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = defaultLine.Substring(0, separatorIndex).Trim();
                if (missingKeys.Contains(key))
                {
                    mergedLines.Add(defaultLine);
                }
            }

            mergedLines.Add(string.Empty);
            File.WriteAllLines(ConfigPath, mergedLines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static void RemoveConfigKey(string key)
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(ConfigPath);
            var keptLines = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (trimmedLine.StartsWith("# " + key + ":", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                keptLines.Add(lines[i]);
            }

            if (keptLines.Count != lines.Length)
            {
                File.WriteAllLines(ConfigPath, keptLines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
        }

        private static void ReloadConfig(bool forceLog)
        {
            bool enabled = true;
            string playerSpeHeroName = string.Empty;
            PlayerSkeletonRenderMode renderMode = PlayerSkeletonRenderMode.Skeleton;
            string staticImagePath = @"Portraits\白云天.png";
            StaticImageSceneConfig heroDetailConfig = DefaultHeroDetailStaticImageConfig;
            StaticImageSceneConfig characterCreationConfig = DefaultCharacterCreationStaticImageConfig;
            StaticImageSceneConfig hudAvatarConfig = DefaultHudAvatarStaticImageConfig;
            bool verboseLog = false;

            foreach (string rawLine in File.ReadAllLines(ConfigPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line[(separatorIndex + 1)..].Trim();

                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsedEnabled))
                    {
                        enabled = parsedEnabled;
                    }
                }
                else if (key.Equals("PlayerSpeHeroName", StringComparison.OrdinalIgnoreCase))
                {
                    playerSpeHeroName = value.Trim();
                }
                else if (key.Equals("RenderMode", StringComparison.OrdinalIgnoreCase))
                {
                    renderMode = ParseRenderMode(value);
                }
                else if (key.Equals("StaticImagePath", StringComparison.OrdinalIgnoreCase))
                {
                    staticImagePath = value;
                }
                else if (TryApplyStaticImageSceneConfigValue(ref heroDetailConfig, HeroDetailStaticImageKeyPrefix, key, value))
                {
                }
                else if (TryApplyStaticImageSceneConfigValue(ref characterCreationConfig, CharacterCreationStaticImageKeyPrefix, key, value))
                {
                }
                else if (TryApplyStaticImageSceneConfigValue(ref hudAvatarConfig, HudAvatarStaticImageKeyPrefix, key, value))
                {
                }
                else if (key.Equals("VerboseLog", StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(value, out bool parsedVerboseLog))
                    {
                        verboseLog = parsedVerboseLog;
                    }
                }
            }

            _enabled = enabled;
            _playerSpeHeroName = playerSpeHeroName;
            _renderMode = renderMode;
            _staticImagePath = staticImagePath;
            _heroDetailStaticImageConfig = ClampStaticImageSceneConfig(heroDetailConfig);
            _characterCreationStaticImageConfig = ClampStaticImageSceneConfig(characterCreationConfig);
            _hudAvatarStaticImageConfig = ClampStaticImageSceneConfig(hudAvatarConfig);
            _verboseLog = verboseLog;
            _configLastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
            SpeHeroResolutionWarnings.Clear();
            RefreshResolvedSpeHero();
            StaticImageSpeSkeletonFlow.NotifyConfigReloaded();

            if (forceLog || _verboseLog)
            {
                Log.Msg(
                    System.ConsoleColor.Cyan,
                    $"已读取配置：Enabled={_enabled}, PlayerSpeHeroName={(_playerSpeHeroName.Length == 0 ? "<空>" : _playerSpeHeroName)}, RenderMode={(int)_renderMode}, StaticImagePath={(_staticImagePath.Length == 0 ? "<空>" : _staticImagePath)}, HeroDetail={_heroDetailStaticImageConfig}, CharacterCreation={_characterCreationStaticImageConfig}, HudAvatar={_hudAvatarStaticImageConfig}, VerboseLog={_verboseLog}");
            }
        }

        private static bool TryApplyStaticImageSceneConfigValue(ref StaticImageSceneConfig config, string keyPrefix, string key, string value)
        {
            if (key.Equals($"{keyPrefix}InsetRatio", StringComparison.OrdinalIgnoreCase))
            {
                config = new StaticImageSceneConfig(ParseFloatOrDefault(value, config.InsetRatio), config.OffsetX, config.OffsetY);
                return true;
            }

            if (key.Equals($"{keyPrefix}OffsetX", StringComparison.OrdinalIgnoreCase))
            {
                config = new StaticImageSceneConfig(config.InsetRatio, ParseFloatOrDefault(value, config.OffsetX), config.OffsetY);
                return true;
            }

            if (key.Equals($"{keyPrefix}OffsetY", StringComparison.OrdinalIgnoreCase))
            {
                config = new StaticImageSceneConfig(config.InsetRatio, config.OffsetX, ParseFloatOrDefault(value, config.OffsetY));
                return true;
            }

            return false;
        }

        private static StaticImageSceneConfig ClampStaticImageSceneConfig(StaticImageSceneConfig config)
        {
            return new StaticImageSceneConfig(Mathf.Clamp(config.InsetRatio, 0.1f, 2f), config.OffsetX, config.OffsetY);
        }

        private static string FormatFloatForConfig(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void SetBoolConfigValue(string key, bool value)
        {
            EnsureConfigExists();

            string[] lines = File.ReadAllLines(ConfigPath);
            bool replaced = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (trimmedLine.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key}={value.ToString().ToLowerInvariant()}";
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                var mergedLines = new List<string>(lines);
                if (mergedLines.Count > 0 && mergedLines[^1].Length != 0)
                {
                    mergedLines.Add(string.Empty);
                }

                mergedLines.Add($"{key}={value.ToString().ToLowerInvariant()}");
                lines = mergedLines.ToArray();
            }

            File.WriteAllLines(ConfigPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            ReloadConfig(forceLog: true);
        }

        private static float ParseFloatOrDefault(string value, float defaultValue)
        {
            if (float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsedInvariant))
            {
                return parsedInvariant;
            }

            if (float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out float parsedCurrent))
            {
                return parsedCurrent;
            }

            return defaultValue;
        }

        private static PlayerSkeletonRenderMode ParseRenderMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return PlayerSkeletonRenderMode.Skeleton;
            }

            string normalizedValue = value.Trim();
            if (normalizedValue.Equals("0", StringComparison.Ordinal))
            {
                return PlayerSkeletonRenderMode.Skeleton;
            }

            if (normalizedValue.Equals("1", StringComparison.Ordinal))
            {
                return PlayerSkeletonRenderMode.StaticImage;
            }

            if (normalizedValue.Equals("StaticImage", StringComparison.OrdinalIgnoreCase) ||
                normalizedValue.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
                normalizedValue.Equals("Static", StringComparison.OrdinalIgnoreCase))
            {
                return PlayerSkeletonRenderMode.StaticImage;
            }

            return PlayerSkeletonRenderMode.Skeleton;
        }
    }
}
