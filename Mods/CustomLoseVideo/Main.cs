using System;
using System.Collections;
using System.IO;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

[assembly: MelonInfo(typeof(CustomLoseVideo.Main), "CustomLoseVideo", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace CustomLoseVideo
{
    internal sealed class ModConfig
    {
        public bool Enabled { get; set; } = true;

        public bool VerboseLogging { get; set; } = false;

        public string VideoFile { get; set; } = @"Video\FightLose.mp4";

        public int VideoWidthPercent { get; set; } = 100;

        public int VideoHeightPercent { get; set; } = 100;
    }

    public sealed class Main : MelonMod
    {
        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "CustomLoseVideo");
        internal static readonly string ConfigPath = Path.Combine(ConfigDir, "CustomLoseVideo.cfg");
        internal static readonly string VideoDir = Path.Combine(ConfigDir, "Video");
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        private static ModConfig _config = new();
        private static bool _pendingFightLoseVideo;
        private static bool _playingVideo;
        private static bool _stopVideoRequested;
        private static int _playerTeamId = -1;
        private static GameObject _overlayObject;
        private static Canvas _overlayCanvas;
        private static RenderTexture _renderTexture;
        private static VideoPlayer _currentVideoPlayer;

        public override void OnInitializeMelon()
        {
            EnsureConfigExists();
            LoadConfig();
            Log.Msg($"CustomLoseVideo 已加载，配置目录：{ConfigDir}");
        }

        internal static void EnsureConfigExists()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(VideoDir);

            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(
                    ConfigPath,
                    string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            "# CustomLoseVideo 配置文件",
                            "# Enabled: 是否启用整个 Mod",
                            "# VerboseLogging: 是否输出详细日志",
                            @"# VideoFile: 相对 UserData\CustomLoseVideo 的战败视频路径，推荐使用 mp4",
                            "# VideoWidthPercent: 视频窗口宽度占屏幕宽度的百分比，范围 10-100",
                            "# VideoHeightPercent: 视频窗口高度占屏幕高度的百分比，范围 10-100",
                            "Enabled=true",
                            "VerboseLogging=false",
                            @"VideoFile=Video\FightLose.mp4",
                            "VideoWidthPercent=100",
                            "VideoHeightPercent=100"
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
                    case "VerboseLogging":
                        loaded.VerboseLogging = ParseBool(value, loaded.VerboseLogging);
                        break;
                    case "VideoFile":
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            loaded.VideoFile = value;
                        }
                        break;
                    case "VideoWidthPercent":
                        loaded.VideoWidthPercent = ParsePercent(value, loaded.VideoWidthPercent);
                        break;
                    case "VideoHeightPercent":
                        loaded.VideoHeightPercent = ParsePercent(value, loaded.VideoHeightPercent);
                        break;
                }
            }

            _config = loaded;
            Log.Msg(
                $"配置已加载：Enabled={_config.Enabled}, VerboseLogging={_config.VerboseLogging}, " +
                $"VideoFile={_config.VideoFile}, VideoWidthPercent={_config.VideoWidthPercent}, " +
                $"VideoHeightPercent={_config.VideoHeightPercent}");
        }

        internal static void RecordPlayerTeamId(int teamId, string caller)
        {
            _playerTeamId = teamId;
            if (_config.VerboseLogging)
            {
                Log.Msg($"[CustomLoseVideo] 记录玩家队伍：caller={caller}, teamId={teamId}");
            }
        }

        internal static void RecordBattleResult(BattleController controller, string caller)
        {
            if (!_config.Enabled || controller == null)
            {
                return;
            }

            int playerTeamId = TryResolvePlayerTeamId(controller);
            int winTeamId = controller.winTeamID;
            bool havePlayer = controller.havePlayer;
            bool playerLost = havePlayer && playerTeamId >= 0 && winTeamId >= 0 && playerTeamId != winTeamId;

            _pendingFightLoseVideo = playerLost;
            if (_config.VerboseLogging)
            {
                Log.Msg(
                    $"[CustomLoseVideo] 战斗结算：caller={caller}, havePlayer={havePlayer}, " +
                    $"playerTeamId={playerTeamId}, winTeamId={winTeamId}, playerLost={playerLost}");
            }
        }

        internal static void TryPlayPendingSceneVideo(string path)
        {
            if (!_pendingFightLoseVideo || !_config.Enabled || _playingVideo || !IsSceneBgmPath(path))
            {
                return;
            }

            _pendingFightLoseVideo = false;
            string fullPath = ResolveVideoPath();
            if (!File.Exists(fullPath))
            {
                Log.Warning($"未找到自定义战败视频文件：{fullPath}");
                return;
            }

            MelonCoroutines.Start(PlayVideoCoroutine(fullPath));
        }

        private static IEnumerator PlayVideoCoroutine(string fullPath)
        {
            _playingVideo = true;
            _stopVideoRequested = false;
            yield return null;

            VideoPlayer videoPlayer = null;
            try
            {
                CreateOverlay(out videoPlayer);
                videoPlayer.url = "file:///" + fullPath.Replace("\\", "/");
                videoPlayer.Prepare();

                float prepareTimeout = Time.realtimeSinceStartup + 10f;
                while (!videoPlayer.isPrepared && !_stopVideoRequested && Time.realtimeSinceStartup < prepareTimeout)
                {
                    yield return null;
                }

                if (_stopVideoRequested)
                {
                    yield break;
                }

                if (!videoPlayer.isPrepared)
                {
                    Log.Warning($"准备自定义战败视频超时：{fullPath}");
                    yield break;
                }

                if (_overlayCanvas != null)
                {
                    _overlayCanvas.enabled = true;
                }

                videoPlayer.Play();
                Log.Msg($"开始播放自定义战败视频：{fullPath}");

                float playTimeout = Time.realtimeSinceStartup + 3f;
                while (videoPlayer != null && !videoPlayer.isPlaying && !_stopVideoRequested && Time.realtimeSinceStartup < playTimeout)
                {
                    yield return null;
                }

                if (videoPlayer != null && videoPlayer.length > 0)
                {
                    float endTime = Time.realtimeSinceStartup + (float)videoPlayer.length + 0.5f;
                    while (videoPlayer != null && !_stopVideoRequested && Time.realtimeSinceStartup < endTime)
                    {
                        yield return null;
                    }
                }

                while (videoPlayer != null && videoPlayer.isPlaying && !_stopVideoRequested)
                {
                    yield return null;
                }
            }
            finally
            {
                DestroyOverlay();
                _playingVideo = false;
                _stopVideoRequested = false;
            }
        }

        private static void CreateOverlay(out VideoPlayer videoPlayer)
        {
            DestroyOverlay();

            _overlayObject = new GameObject("CustomLoseVideoOverlay");
            UnityEngine.Object.DontDestroyOnLoad(_overlayObject);

            var canvas = _overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvas.enabled = false;
            _overlayCanvas = canvas;

            _overlayObject.AddComponent<CanvasScaler>();
            _overlayObject.AddComponent<GraphicRaycaster>();

            int frameWidth = Math.Max(120, Screen.width * _config.VideoWidthPercent / 100);
            int frameHeight = Math.Max(120, Screen.height * _config.VideoHeightPercent / 100);
            int videoWidth = Math.Max(16, frameWidth - 16);
            int videoHeight = Math.Max(16, frameHeight - 46);

            var frameObject = new GameObject("Frame");
            frameObject.transform.SetParent(_overlayObject.transform, false);

            var frameRect = frameObject.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = Vector2.zero;
            frameRect.sizeDelta = new Vector2(frameWidth, frameHeight);

            var frameImage = frameObject.AddComponent<Image>();
            frameImage.color = new Color(0.03f, 0.025f, 0.02f, 0.96f);

            var frameOutline = frameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.72f, 0.55f, 0.28f, 0.95f);
            frameOutline.effectDistance = new Vector2(2f, -2f);

            CreateCloseButton(frameObject.transform);

            var rawImageObject = new GameObject("Video");
            rawImageObject.transform.SetParent(frameObject.transform, false);

            var rectTransform = rawImageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = new Vector2(0f, 8f);
            rectTransform.sizeDelta = new Vector2(videoWidth, videoHeight);

            var rawImage = rawImageObject.AddComponent<RawImage>();
            rawImage.color = Color.white;

            _renderTexture = new RenderTexture(videoWidth, videoHeight, 0);
            rawImage.texture = _renderTexture;

            videoPlayer = _overlayObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = _renderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _currentVideoPlayer = videoPlayer;
        }

        private static void CreateCloseButton(Transform parent)
        {
            var buttonObject = new GameObject("CloseButton");
            buttonObject.transform.SetParent(parent, false);

            var rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-8f, -6f);
            rectTransform.sizeDelta = new Vector2(32f, 28f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.06f, 0.05f, 0.95f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.24f, 0.06f, 0.05f, 0.95f);
            colors.highlightedColor = new Color(0.5f, 0.12f, 0.1f, 1f);
            colors.pressedColor = new Color(0.14f, 0.03f, 0.03f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)StopCurrentVideo);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "X";
        }

        private static void StopCurrentVideo()
        {
            _stopVideoRequested = true;
            if (_currentVideoPlayer != null)
            {
                _currentVideoPlayer.Stop();
            }
        }

        private static void DestroyOverlay()
        {
            _currentVideoPlayer = null;

            if (_overlayObject != null)
            {
                UnityEngine.Object.Destroy(_overlayObject);
                _overlayObject = null;
            }

            _overlayCanvas = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }
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

        private static string ResolveVideoPath()
        {
            string relativePath = _config.VideoFile.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ConfigDir, relativePath));
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

        private static int ParsePercent(string text, int fallback)
        {
            if (!int.TryParse(text, out int parsed))
            {
                return fallback;
            }

            return Math.Clamp(parsed, 10, 100);
        }
    }

    [HarmonyPatch(typeof(BGMController), nameof(BGMController.LoadAudio))]
    internal static class Patch_BGMController_LoadAudio
    {
        [HarmonyPostfix]
        private static void Postfix(string path)
        {
            Main.TryPlayPendingSceneVideo(path);
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
