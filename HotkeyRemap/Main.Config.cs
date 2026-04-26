using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace HotkeyRemap
{
    internal sealed class HotkeyRemapConfig
    {
        internal bool EnableGlobalRemap { get; set; } = true;

        internal bool EnablePlotAdvanceRemap { get; set; } = true;

        internal bool EnableUiNavigationRemap { get; set; }

        internal bool VerboseLog { get; set; }

        internal KeyCode QuickSave { get; set; } = KeyCode.F5;

        internal KeyCode QuickLoad { get; set; } = KeyCode.F9;

        internal KeyCode MissionToggle { get; set; } = KeyCode.Tab;

        internal KeyCode BigMapRest { get; set; } = KeyCode.Space;

        internal KeyCode PlotAdvance { get; set; } = KeyCode.Space;

        internal KeyCode HeroDetail { get; set; } = KeyCode.Q;

        internal KeyCode LeaveCity { get; set; } = KeyCode.E;

        internal KeyCode ShowMap { get; set; } = KeyCode.M;

        internal KeyCode BattleAutoToggle { get; set; } = KeyCode.F2;

        internal KeyCode BattleActionFastW { get; set; } = KeyCode.W;

        internal KeyCode BattleActionFastE { get; set; } = KeyCode.E;

        internal KeyCode BattleActionFastS { get; set; } = KeyCode.S;

        internal KeyCode BattleActionFastD { get; set; } = KeyCode.D;

        internal KeyCode BattleActionFastX { get; set; } = KeyCode.X;

        internal KeyCode BattleActionFastC { get; set; } = KeyCode.C;

        internal KeyCode StudyUp { get; set; } = KeyCode.W;

        internal KeyCode StudyDown { get; set; } = KeyCode.S;

        internal KeyCode StudyLeft { get; set; } = KeyCode.A;

        internal KeyCode StudyRight { get; set; } = KeyCode.D;

        internal KeyCode UiSubmit { get; set; } = KeyCode.Return;

        internal KeyCode UiCancel { get; set; } = KeyCode.Escape;

        internal static HotkeyRemapConfig CreateDefault()
        {
            return new HotkeyRemapConfig();
        }
    }

    internal static class HotkeyRemapConfigStore
    {
        private const string ConfigFileName = "HotkeyRemap.cfg";

        internal const string ConfigPathDisplay = @"UserData\HotkeyRemap.cfg";

        internal static readonly string ConfigPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", ConfigFileName));

        private static readonly Dictionary<string, KeyCode> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Enter"] = KeyCode.Return,
            ["Esc"] = KeyCode.Escape,
            ["PgUp"] = KeyCode.PageUp,
            ["PgDn"] = KeyCode.PageDown
        };

        internal static HotkeyRemapConfig Load(MelonLogger.Instance log)
        {
            HotkeyRemapConfig config = HotkeyRemapConfig.CreateDefault();
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
                config = HotkeyRemapConfig.CreateDefault();
                shouldRewrite = true;
            }

            if (shouldRewrite)
            {
                WriteConfig(config, log);
            }

            return config;
        }

        internal static void Save(HotkeyRemapConfig config, MelonLogger.Instance log)
        {
            WriteConfig(config, log);
        }

        private static bool TryReadConfig(IEnumerable<string> lines, HotkeyRemapConfig config, MelonLogger.Instance log)
        {
            bool seenEnableGlobalRemap = false;
            bool seenEnablePlotAdvanceRemap = false;
            bool seenEnableUiNavigationRemap = false;
            bool seenVerboseLog = false;
            bool seenQuickSave = false;
            bool seenQuickLoad = false;
            bool seenMissionToggle = false;
            bool seenBigMapRest = false;
            bool seenPlotAdvance = false;
            bool seenHeroDetail = false;
            bool seenLeaveCity = false;
            bool seenShowMap = false;
            bool seenBattleAutoToggle = false;
            bool seenBattleActionFastW = false;
            bool seenBattleActionFastE = false;
            bool seenBattleActionFastS = false;
            bool seenBattleActionFastD = false;
            bool seenBattleActionFastX = false;
            bool seenBattleActionFastC = false;
            bool seenStudyUp = false;
            bool seenStudyDown = false;
            bool seenStudyLeft = false;
            bool seenStudyRight = false;
            bool seenUiSubmit = false;
            bool seenUiCancel = false;
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

                switch (key)
                {
                    case "EnableGlobalRemap":
                        isValid &= TryAssignBool(value, parsed => config.EnableGlobalRemap = parsed, "EnableGlobalRemap", log, ref seenEnableGlobalRemap);
                        break;
                    case "EnablePlotAdvanceRemap":
                        isValid &= TryAssignBool(value, parsed => config.EnablePlotAdvanceRemap = parsed, "EnablePlotAdvanceRemap", log, ref seenEnablePlotAdvanceRemap);
                        break;
                    case "EnableUiNavigationRemap":
                        isValid &= TryAssignBool(value, parsed => config.EnableUiNavigationRemap = parsed, "EnableUiNavigationRemap", log, ref seenEnableUiNavigationRemap);
                        break;
                    case "VerboseLog":
                        isValid &= TryAssignBool(value, parsed => config.VerboseLog = parsed, "VerboseLog", log, ref seenVerboseLog);
                        break;
                    case "QuickSave":
                        isValid &= TryAssignKey(value, parsed => config.QuickSave = parsed, "QuickSave", log, ref seenQuickSave);
                        break;
                    case "QuickLoad":
                        isValid &= TryAssignKey(value, parsed => config.QuickLoad = parsed, "QuickLoad", log, ref seenQuickLoad);
                        break;
                    case "MissionToggle":
                        isValid &= TryAssignKey(value, parsed => config.MissionToggle = parsed, "MissionToggle", log, ref seenMissionToggle);
                        break;
                    case "BigMapRest":
                        isValid &= TryAssignKey(value, parsed => config.BigMapRest = parsed, "BigMapRest", log, ref seenBigMapRest);
                        break;
                    case "PlotAdvance":
                        isValid &= TryAssignKey(value, parsed => config.PlotAdvance = parsed, "PlotAdvance", log, ref seenPlotAdvance);
                        break;
                    case "HeroDetail":
                        isValid &= TryAssignKey(value, parsed => config.HeroDetail = parsed, "HeroDetail", log, ref seenHeroDetail);
                        break;
                    case "LeaveCity":
                        isValid &= TryAssignKey(value, parsed => config.LeaveCity = parsed, "LeaveCity", log, ref seenLeaveCity);
                        break;
                    case "ShowMap":
                        isValid &= TryAssignKey(value, parsed => config.ShowMap = parsed, "ShowMap", log, ref seenShowMap);
                        break;
                    case "BattleAutoToggle":
                        isValid &= TryAssignKey(value, parsed => config.BattleAutoToggle = parsed, "BattleAutoToggle", log, ref seenBattleAutoToggle);
                        break;
                    case "BattleActionFastW":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastW = parsed, "BattleActionFastW", log, ref seenBattleActionFastW);
                        break;
                    case "BattleActionFastE":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastE = parsed, "BattleActionFastE", log, ref seenBattleActionFastE);
                        break;
                    case "BattleActionFastS":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastS = parsed, "BattleActionFastS", log, ref seenBattleActionFastS);
                        break;
                    case "BattleActionFastD":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastD = parsed, "BattleActionFastD", log, ref seenBattleActionFastD);
                        break;
                    case "BattleActionFastX":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastX = parsed, "BattleActionFastX", log, ref seenBattleActionFastX);
                        break;
                    case "BattleActionFastC":
                        isValid &= TryAssignKey(value, parsed => config.BattleActionFastC = parsed, "BattleActionFastC", log, ref seenBattleActionFastC);
                        break;
                    case "StudyUp":
                        isValid &= TryAssignKey(value, parsed => config.StudyUp = parsed, "StudyUp", log, ref seenStudyUp);
                        break;
                    case "StudyDown":
                        isValid &= TryAssignKey(value, parsed => config.StudyDown = parsed, "StudyDown", log, ref seenStudyDown);
                        break;
                    case "StudyLeft":
                        isValid &= TryAssignKey(value, parsed => config.StudyLeft = parsed, "StudyLeft", log, ref seenStudyLeft);
                        break;
                    case "StudyRight":
                        isValid &= TryAssignKey(value, parsed => config.StudyRight = parsed, "StudyRight", log, ref seenStudyRight);
                        break;
                    case "UiSubmit":
                        isValid &= TryAssignKey(value, parsed => config.UiSubmit = parsed, "UiSubmit", log, ref seenUiSubmit);
                        break;
                    case "UiCancel":
                        isValid &= TryAssignKey(value, parsed => config.UiCancel = parsed, "UiCancel", log, ref seenUiCancel);
                        break;
                    default:
                        log.Warning($"发现未知配置项，将保留其余有效配置并重写文件：{key}");
                        isValid = false;
                        break;
                }
            }

            return isValid
                && seenEnableGlobalRemap
                && seenEnablePlotAdvanceRemap
                && seenEnableUiNavigationRemap
                && seenVerboseLog
                && seenQuickSave
                && seenQuickLoad
                && seenMissionToggle
                && seenBigMapRest
                && seenPlotAdvance
                && seenHeroDetail
                && seenLeaveCity
                && seenShowMap
                && seenBattleAutoToggle
                && seenBattleActionFastW
                && seenBattleActionFastE
                && seenBattleActionFastS
                && seenBattleActionFastD
                && seenBattleActionFastX
                && seenBattleActionFastC
                && seenStudyUp
                && seenStudyDown
                && seenStudyLeft
                && seenStudyRight
                && seenUiSubmit
                && seenUiCancel;
        }

        private static bool TryAssignBool(
            string value,
            Action<bool> assign,
            string key,
            MelonLogger.Instance log,
            ref bool seenFlag)
        {
            if (bool.TryParse(value, out bool parsed))
            {
                assign(parsed);
                seenFlag = true;
                return true;
            }

            log.Warning($"{key} 配置无效，将回退默认值：{value}");
            return false;
        }

        private static bool TryAssignKey(
            string value,
            Action<KeyCode> assign,
            string key,
            MelonLogger.Instance log,
            ref bool seenFlag)
        {
            if (TryParseKeyCode(value, out KeyCode parsed))
            {
                assign(parsed);
                seenFlag = true;
                return true;
            }

            log.Warning($"{key} 配置无效，将回退默认值：{value}");
            return false;
        }

        private static bool TryParseKeyCode(string value, out KeyCode parsed)
        {
            string normalized = value.Trim();
            if (KeyAliases.TryGetValue(normalized, out parsed))
            {
                return true;
            }

            return Enum.TryParse(normalized, true, out parsed);
        }

        private static void EnsureDirectoryExists()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void WriteConfig(HotkeyRemapConfig config, MelonLogger.Instance log)
        {
            try
            {
                EnsureDirectoryExists();

                string[] lines =
                {
                    "# HotkeyRemap 配置文件",
                    "# 本文件当前用于维护默认键位和后续热键接入的统一格式。",
                    "# 所有按键都使用 Unity KeyCode 名称，例如 F5、F9、Tab、Space、Return、Escape、W。",
                    "# 目前提供 Enter、Esc、PgUp、PgDn 这几个常见别名。",
                    "# 缺失项、未知项或非法值都会回退默认值并重写本文件。",
                    $"EnableGlobalRemap={config.EnableGlobalRemap.ToString().ToLowerInvariant()}",
                    $"EnablePlotAdvanceRemap={config.EnablePlotAdvanceRemap.ToString().ToLowerInvariant()}",
                    $"EnableUiNavigationRemap={config.EnableUiNavigationRemap.ToString().ToLowerInvariant()}",
                    $"VerboseLog={config.VerboseLog.ToString().ToLowerInvariant()}",
                    $"QuickSave={config.QuickSave}",
                    $"QuickLoad={config.QuickLoad}",
                    $"MissionToggle={config.MissionToggle}",
                    $"BigMapRest={config.BigMapRest}",
                    $"PlotAdvance={config.PlotAdvance}",
                    $"HeroDetail={config.HeroDetail}",
                    $"LeaveCity={config.LeaveCity}",
                    $"ShowMap={config.ShowMap}",
                    $"BattleAutoToggle={config.BattleAutoToggle}",
                    $"BattleActionFastW={config.BattleActionFastW}",
                    $"BattleActionFastE={config.BattleActionFastE}",
                    $"BattleActionFastS={config.BattleActionFastS}",
                    $"BattleActionFastD={config.BattleActionFastD}",
                    $"BattleActionFastX={config.BattleActionFastX}",
                    $"BattleActionFastC={config.BattleActionFastC}",
                    $"StudyUp={config.StudyUp}",
                    $"StudyDown={config.StudyDown}",
                    $"StudyLeft={config.StudyLeft}",
                    $"StudyRight={config.StudyRight}",
                    $"UiSubmit={config.UiSubmit}",
                    $"UiCancel={config.UiCancel}",
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
