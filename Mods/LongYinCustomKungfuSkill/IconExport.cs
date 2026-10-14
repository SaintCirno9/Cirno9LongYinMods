using System;
using System.Globalization;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LongYinCustomKungfuSkill
{
    internal static class IconExportManager
    {
        internal static readonly string IconExportConfigPath = Path.Combine(Main.ConfigDir, "LongYinCustomKungfuSkill.cfg");
        internal static readonly string VanillaIconExportDir = Path.Combine(Main.ConfigDir, "原版Icons");
        internal static bool HasExportedVanillaSkillIcons { get; set; }
        internal static bool IsExportingVanillaSkillIcons { get; set; }
        private static bool _cachedEnableDebugIconExport;
        private static DateTime _cachedConfigWriteTimeUtc = DateTime.MinValue;

        internal static void EnsureConfigExists()
        {
            try
            {
                if (!File.Exists(IconExportConfigPath))
                {
                    File.WriteAllText(IconExportConfigPath, "EnableExportVanillaIcons=false\r\n");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"准备配置文件 LongYinCustomKungfuSkill.cfg 失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        internal static bool IsDebugIconExportEnabled()
        {
            EnsureConfigExists();

            try
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(IconExportConfigPath);
                if (_cachedConfigWriteTimeUtc == lastWriteTimeUtc)
                {
                    return _cachedEnableDebugIconExport;
                }

                _cachedConfigWriteTimeUtc = lastWriteTimeUtc;
                _cachedEnableDebugIconExport = false;

                foreach (string rawLine in File.ReadAllLines(IconExportConfigPath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line[..separatorIndex].Trim();
                    string value = line[(separatorIndex + 1)..].Trim();
                    if (string.Equals(key, "EnableExportVanillaIcons", StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedEnableDebugIconExport =
                            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
                        break;
                    }
                }

                return _cachedEnableDebugIconExport;
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"读取配置文件 LongYinCustomKungfuSkill.cfg 失败：{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        internal static void ExportVanillaSkillIcons(TextureController textureController)
        {
            if (HasExportedVanillaSkillIcons || IsExportingVanillaSkillIcons)
            {
                return;
            }

            if (textureController == null || Main.VanillaKungFuMaxId < 0)
            {
                return;
            }

            IsExportingVanillaSkillIcons = true;
            try
            {
                Directory.CreateDirectory(VanillaIconExportDir);
                int exportedCount = 0;
                for (int skillId = 0; skillId <= Main.VanillaKungFuMaxId; skillId++)
                {
                    Sprite sprite = textureController.LoadAtlasSprite("IconAtlas", skillId.ToString(CultureInfo.InvariantCulture));
                    if (sprite == null)
                    {
                        continue;
                    }

                    string outputPath = Path.Combine(VanillaIconExportDir, $"{skillId}.png");
                    if (!TryExportSpriteToPng(sprite, outputPath))
                    {
                        continue;
                    }

                    exportedCount++;
                }

                HasExportedVanillaSkillIcons = true;
                Main.LOG.Msg(System.ConsoleColor.Cyan, $"原版功法图标导出完成，共导出 {exportedCount} 张到：{VanillaIconExportDir}");
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"导出原版功法图标异常：{ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                IsExportingVanillaSkillIcons = false;
            }
        }

        internal static bool TryExportSpriteToPng(Sprite sprite, string outputPath)
        {
            if (sprite == null || string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            string spriteName = "<unknown>";
            GameObject cameraObject = null;
            GameObject spriteObject = null;
            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D outputTexture = null;

            try
            {
                spriteName = sprite.name;
                Rect spriteRect = sprite.rect;
                int width = Mathf.RoundToInt(spriteRect.width);
                int height = Mathf.RoundToInt(spriteRect.height);
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                cameraObject = new GameObject("LongYinIconExportCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.nearClipPlane = -10f;
                camera.farClipPlane = 10f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = 1 << 31;
                camera.orthographicSize = sprite.bounds.extents.y > 0f ? sprite.bounds.extents.y : height / 200f;

                spriteObject = new GameObject("LongYinIconExportSprite");
                spriteObject.layer = 31;
                SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = Color.white;
                spriteObject.transform.position = Vector3.zero;

                renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTexture;
                camera.transform.position = new Vector3(0f, 0f, -1f);
                camera.Render();

                outputTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                RenderTexture.active = renderTexture;
                outputTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                outputTexture.Apply();

                byte[] pngBytes = ImageConversion.EncodeToPNG(outputTexture);
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    return false;
                }

                File.WriteAllBytes(outputPath, pngBytes);
                return true;
            }
            catch (Exception ex)
            {
                Main.LOG.Warning($"导出功法图标失败：sprite={spriteName}，{ex.GetType().Name} - {ex.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }

                if (outputTexture != null)
                {
                    UnityEngine.Object.Destroy(outputTexture);
                }

                if (spriteObject != null)
                {
                    UnityEngine.Object.Destroy(spriteObject);
                }

                if (cameraObject != null)
                {
                    UnityEngine.Object.Destroy(cameraObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TextureController), nameof(TextureController.LoadAtlasSprite))]
    public static class TextureControllerIconExportPatch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (!IconExportManager.IsDebugIconExportEnabled())
            {
                return;
            }

            if (!IconExportManager.HasExportedVanillaSkillIcons && !IconExportManager.IsExportingVanillaSkillIcons)
            {
                IconExportManager.ExportVanillaSkillIcons(TextureController.Instance);
            }
        }
    }
}
