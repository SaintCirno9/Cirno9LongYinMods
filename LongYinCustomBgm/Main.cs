using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.Networking;

[assembly: MelonInfo(typeof(LongYinCustomBgm.Main), "LongYinCustomBgm", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomBgm
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        public bool ReplaceFightLose { get; set; } = true;

        public bool VerboseLogging { get; set; } = false;

        public string FightLoseFile { get; set; } = @"Audio\FightLose.ogg";
    }

    public class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomBgm");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "LongYinCustomBgm.cfg");
        internal static readonly string AudioDir = Path.Combine(ConfigDir, "Audio");
        internal static readonly MelonLogger.Instance Log = Melon<Main>.Logger;

        private static ModConfig _config = new();
        private static AudioClip _customFightLoseClip;
        private static bool _clipLoadStarted;
        private static bool _clipUnavailableLogged;
        private static bool _pendingFightLoseSceneBgm;
        private static int _playerTeamId = -1;
        private static string _lastLoadAudioLog = string.Empty;

        public override void OnInitializeMelon()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EnsureConfigExists();
            LoadConfig();
            Log.Msg($"LongYinCustomBgm 已加载，配置目录：{ConfigDir}");
        }

        internal static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(AudioDir);

            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(
                    ConfigPath,
                    string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            "# LongYinCustomBgm 配置文件",
                            "# Enabled: 是否启用整个 Mod",
                            "# ReplaceFightLose: 是否尝试替换战败音乐 FightLose",
                            "# VerboseLogging: 是否输出详细日志",
                            @"# FightLoseFile: 相对 UserData\LongYinCustomBgm 的音频路径，推荐使用 ogg",
                            "Enabled=true",
                            "ReplaceFightLose=true",
                            "VerboseLogging=false",
                            @"FightLoseFile=Audio\FightLose.ogg"
                        }),
                    new UTF8Encoding(true));
            }
        }

        internal static void LoadConfig()
        {
            var loaded = new ModConfig();
            foreach (string rawLine in File.ReadAllLines(ConfigPath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int splitIndex = line.IndexOf('=');
                if (splitIndex <= 0)
                {
                    continue;
                }

                string key = line[..splitIndex].Trim();
                string value = line[(splitIndex + 1)..].Trim();

                switch (key)
                {
                    case "Enabled":
                        loaded.Enabled = ParseBool(value, loaded.Enabled);
                        break;
                    case "ReplaceFightLose":
                        loaded.ReplaceFightLose = ParseBool(value, loaded.ReplaceFightLose);
                        break;
                    case "VerboseLogging":
                        loaded.VerboseLogging = ParseBool(value, loaded.VerboseLogging);
                        break;
                    case "FightLoseFile":
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            loaded.FightLoseFile = value;
                        }
                        break;
                }
            }

            _config = loaded;
            Log.Msg(
                $"配置已加载：Enabled={_config.Enabled}, ReplaceFightLose={_config.ReplaceFightLose}, " +
                $"VerboseLogging={_config.VerboseLogging}, FightLoseFile={_config.FightLoseFile}");
        }

        internal static void RecordPlayerTeamId(int teamId, string caller)
        {
            _playerTeamId = teamId;
            if (_config.VerboseLogging)
            {
                Log.Msg($"[LongYinCustomBgm] 记录玩家队伍：caller={caller}, teamId={teamId}");
            }
        }

        internal static void RecordBattleResult(BattleController controller, string caller)
        {
            if (!ShouldReplaceFightLose() || controller == null)
            {
                return;
            }

            int playerTeamId = TryResolvePlayerTeamId(controller);
            int winTeamId = controller.winTeamID;
            bool havePlayer = controller.havePlayer;
            bool playerLost = havePlayer && playerTeamId >= 0 && winTeamId >= 0 && playerTeamId != winTeamId;

            _pendingFightLoseSceneBgm = playerLost;
            if (_config.VerboseLogging)
            {
                Log.Msg(
                    $"[LongYinCustomBgm] 战斗结算：caller={caller}, havePlayer={havePlayer}, " +
                    $"playerTeamId={playerTeamId}, winTeamId={winTeamId}, playerLost={playerLost}");
            }

            if (playerLost)
            {
                TryEnsureCustomClipLoaded($"{caller}/预加载");
            }
        }

        internal static void TryReplacePendingSceneBgm(string path, ref AudioClip clip)
        {
            if (!_pendingFightLoseSceneBgm || !ShouldReplaceFightLose() || !IsSceneBgmPath(path))
            {
                return;
            }

            _pendingFightLoseSceneBgm = false;
            if (!TryEnsureCustomClipLoaded("LoadAudio/战败结算"))
            {
                return;
            }

            clip = _customFightLoseClip;
            Log.Msg($"[LongYinCustomBgm] 已将场景音乐替换为战败自定义音频：path={path}");
        }

        internal static bool ShouldReplaceFightLose()
        {
            return _config.Enabled && _config.ReplaceFightLose;
        }

        internal static bool TryEnsureCustomClipLoaded(string caller)
        {
            if (!ShouldReplaceFightLose())
            {
                return false;
            }

            string fullPath = ResolveClipPath();
            if (_customFightLoseClip != null)
            {
                return true;
            }

            if (!File.Exists(fullPath))
            {
                if (!_clipUnavailableLogged)
                {
                    _clipUnavailableLogged = true;
                    Log.Warning($"[{caller}] 未找到自定义战败音乐文件：{fullPath}");
                }

                return false;
            }

            if (_config.VerboseLogging)
            {
                Log.Msg($"[LongYinCustomBgm] 开始加载自定义战败音乐：caller={caller}, path={fullPath}");
            }

            if (!_clipLoadStarted)
            {
                _clipLoadStarted = true;
                MelonCoroutines.Start(LoadCustomClipCoroutine(fullPath));
            }

            return _customFightLoseClip != null;
        }

        private static IEnumerator LoadCustomClipCoroutine(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Log.Warning($"未找到自定义战败音乐文件：{fullPath}");
                _clipLoadStarted = false;
                yield break;
            }

            AudioType audioType = DetectAudioType(fullPath);
            string url = "file:///" + fullPath.Replace("\\", "/");
            WWW www = new(url);

            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Log.Error($"加载自定义战败音乐失败：{www.error}");
                www.Dispose();
                _clipLoadStarted = false;
                yield break;
            }

            try
            {
                AudioClip loadedClip = www.GetAudioClip(false, false, audioType);
                if (!TryAssignCustomFightLoseClip(loadedClip, fullPath, "WWW"))
                {
                    Log.Warning($"通过 WWW 加载的自定义战败音乐内容为空：{fullPath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"解析自定义战败音乐失败：{ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                www.Dispose();
                _clipLoadStarted = false;
            }
        }

        private static bool TryAssignCustomFightLoseClip(AudioClip loadedClip, string fullPath, string loaderName)
        {
            if (loadedClip == null)
            {
                return false;
            }

            _customFightLoseClip = loadedClip;
            _customFightLoseClip.name = "FightLose_Custom";
            _clipUnavailableLogged = false;
            Log.Msg($"自定义战败音乐加载完成：{fullPath}，加载方式：{loaderName}");
            return true;
        }

        private static bool IsSceneBgmPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.Contains("/Area", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/BigMap", StringComparison.OrdinalIgnoreCase);
        }

        private static int TryResolvePlayerTeamId(BattleController controller)
        {
            if (controller?.playerBattleUnit?.battleTeam != null)
            {
                return controller.playerBattleUnit.battleTeam.ID;
            }

            return _playerTeamId;
        }

        private static string ResolveClipPath()
        {
            string relativePath = _config.FightLoseFile.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ConfigDir, relativePath));
        }

        private static AudioType DetectAudioType(string fullPath)
        {
            string extension = Path.GetExtension(fullPath)?.ToLowerInvariant();
            return extension switch
            {
                ".wav" => AudioType.WAV,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.OGGVORBIS
            };
        }

        private static bool ParseBool(string text, bool fallback)
        {
            if (bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }

            return text.Trim() switch
            {
                "1" => true,
                "0" => false,
                _ => fallback
            };
        }

        internal static void LogLoadAudioCall(string path, AudioClip clip)
        {
            if (!_config.VerboseLogging)
            {
                return;
            }

            string normalizedPath = path ?? "<null>";
            string clipName = clip?.name ?? "<null>";
            string logLine = $"path={normalizedPath}, clip={clipName}";
            if (string.Equals(_lastLoadAudioLog, logLine, StringComparison.Ordinal))
            {
                return;
            }

            _lastLoadAudioLog = logLine;
            Log.Msg($"[LongYinCustomBgm] LoadAudio: {logLine}");
        }
    }

    [HarmonyPatch(typeof(BGMController), nameof(BGMController.LoadAudio))]
    internal static class Patch_BGMController_LoadAudio
    {
        [HarmonyPostfix]
        private static void Postfix(string path, ref AudioClip __result)
        {
            Main.TryReplacePendingSceneBgm(path, ref __result);
            Main.LogLoadAudioCall(path, __result);
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.SetHavePlayer))]
    internal static class Patch_BattleController_SetHavePlayer
    {
        [HarmonyPostfix]
        private static void Postfix(int teamID)
        {
            Main.RecordPlayerTeamId(teamID, "SetHavePlayer");
        }
    }

    [HarmonyPatch(typeof(BattleController), nameof(BattleController.BattleRealEnd))]
    internal static class Patch_BattleController_BattleRealEnd
    {
        [HarmonyPostfix]
        private static void Postfix(BattleController __instance)
        {
            Main.RecordBattleResult(__instance, "BattleRealEnd");
        }
    }
}
