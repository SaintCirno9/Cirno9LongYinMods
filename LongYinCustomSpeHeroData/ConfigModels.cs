using System;
using System.IO;
using UnityEngine;

namespace LongYinCustomSpeHeroData
{
    internal sealed class PortraitConfigOrigin
    {
        public PortraitConfigOrigin(string sourceName, string csvPath, string portraitDir, string relativePortraitDir)
        {
            SourceName = sourceName ?? string.Empty;
            CsvPath = csvPath ?? string.Empty;
            PortraitDir = portraitDir ?? string.Empty;
            RelativePortraitDir = relativePortraitDir ?? string.Empty;
        }

        public string SourceName { get; }

        public string CsvPath { get; }

        public string PortraitDir { get; }

        public string RelativePortraitDir { get; }
    }

    internal sealed class ConfigSource
    {
        public ConfigSource(string sourceName, string directoryPath, string relativeDirectoryPath, int order)
        {
            SourceName = sourceName ?? string.Empty;
            DirectoryPath = directoryPath ?? string.Empty;
            RelativeDirectoryPath = relativeDirectoryPath ?? string.Empty;
            Order = order;
        }

        public string SourceName { get; }

        public string DirectoryPath { get; }

        public string RelativeDirectoryPath { get; }

        public int Order { get; }

        public string PortraitDir => Path.Combine(DirectoryPath, "Portraits");

        public string RelativePortraitDir => Path.Combine(RelativeDirectoryPath, "Portraits");

        public string GetCsvPath(string fileName)
        {
            return Path.Combine(DirectoryPath, fileName);
        }

        public PortraitConfigOrigin CreatePortraitOrigin(string csvPath)
        {
            return new PortraitConfigOrigin(SourceName, csvPath, PortraitDir, RelativePortraitDir);
        }
    }

    internal sealed class PortraitSpriteCacheEntry
    {
        public PortraitSpriteCacheEntry(string imagePath, DateTime writeTimeUtc, Texture2D texture, Sprite sprite)
        {
            ImagePath = imagePath ?? string.Empty;
            WriteTimeUtc = writeTimeUtc;
            Texture = texture;
            Sprite = sprite;
        }

        public string ImagePath { get; }

        public DateTime WriteTimeUtc { get; }

        public Texture2D Texture { get; }

        public Sprite Sprite { get; }
    }

    internal sealed class PortraitSceneLayoutOverride
    {
        public PortraitSceneLayoutOverride(float? scaleMultiplier, float? offsetX, float? offsetY)
        {
            ScaleMultiplier = scaleMultiplier;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public float? ScaleMultiplier { get; }

        public float? OffsetX { get; }

        public float? OffsetY { get; }

    }

    internal sealed class PortraitLayoutConfig
    {
        public PortraitLayoutConfig(
            string heroName,
            PortraitSceneLayoutOverride fallback,
            PortraitConfigOrigin origin)
        {
            Fallback = fallback ?? new PortraitSceneLayoutOverride(null, null, null);
            Origin = origin;
            HeroName = heroName ?? string.Empty;
        }

        public string HeroName { get; }

        public PortraitSceneLayoutOverride Fallback { get; }

        public PortraitConfigOrigin Origin { get; }
    }

    internal sealed class LoadedPortraitLayoutData
    {
        public static readonly LoadedPortraitLayoutData Empty = new(
            new System.Collections.Generic.Dictionary<int, PortraitLayoutConfig>(),
            new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal),
            string.Empty);

        public LoadedPortraitLayoutData(
            System.Collections.Generic.Dictionary<int, PortraitLayoutConfig> layoutsByHeroId,
            System.Collections.Generic.Dictionary<string, int> layoutHeroIdsByName,
            string cacheSignature)
        {
            LayoutsByHeroId = layoutsByHeroId ?? new System.Collections.Generic.Dictionary<int, PortraitLayoutConfig>();
            LayoutHeroIdsByName = layoutHeroIdsByName ?? new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
            CacheSignature = cacheSignature ?? string.Empty;
        }

        public System.Collections.Generic.Dictionary<int, PortraitLayoutConfig> LayoutsByHeroId { get; }

        public System.Collections.Generic.Dictionary<string, int> LayoutHeroIdsByName { get; }

        public string CacheSignature { get; }
    }
}
