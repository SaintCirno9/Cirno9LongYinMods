using System.Collections.Generic;

namespace LongYinCustomEvent
{
    internal sealed class WorldEventConfigFile
    {
        public int SchemaVersion { get; set; } = 2;
        public string Description { get; set; }
        public List<string> Notes { get; set; }
        public List<CustomWorldEventConfig> Events { get; set; } = new();
    }

    internal sealed class CustomWorldEventConfig
    {
        public string CopyFromEventName { get; set; }
        public string Name { get; set; }
        public TimeDataConfig StartTime { get; set; }
        public List<PlotSignRequirementConfig> PlotSignRequirements { get; set; }
        public int? RepeatType { get; set; }
        public int? RepeatDay { get; set; }
        public int? RepeatDayRandomRange { get; set; }
        public int? LastTime { get; set; }
        public bool? NoRandomDifficulty { get; set; }
        public int? ForceDifficulty { get; set; }
        public string StartCallPlot { get; set; }
        public int? EventRandomArea { get; set; }
        public EventDataConfig EventData { get; set; }
    }

    internal sealed class TimeDataConfig
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
    }

    internal sealed class PlotSignRequirementConfig
    {
        public int PlotSignRequirementType { get; set; }
        public int PlotSignId { get; set; }
    }

    internal sealed class EventDataConfig
    {
        public string EventName { get; set; }
        public string EventDescribe { get; set; }
        public List<int> EventAvailableAreaType { get; set; }
        public string SpriteName { get; set; }
        public bool? IsAreaEvent { get; set; }
        public int? ResourcePointId { get; set; }
        public List<int> AreaId { get; set; }
        public List<int> AreaMapTileId { get; set; }
        public BigMapPosConfig BigMapPos { get; set; }
        public int? NearAreaId { get; set; }
        public int? NearAreaDirection { get; set; }
        public bool? AutoDestroy { get; set; }
        public int? LeftTime { get; set; }
        public float? Difficulty { get; set; }
        public float? DifficultyRate { get; set; }
        public int? SpeTargetId { get; set; }
        public PlotDataConfig PlotData { get; set; }
        public ItemListDataConfig EventItemList { get; set; }
        public int? LeaderLimit { get; set; }
        public string EventOutTimeCallFuc { get; set; }
        public float? SeeRange { get; set; }
        public int? RandomSeed { get; set; }
        public bool? InaccuracyPosText { get; set; }
        public bool? NotImportant { get; set; }
    }

    internal sealed class BigMapPosConfig
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    internal sealed class PlotDataConfig
    {
        public string PlotName { get; set; }
        public bool? SpePlot { get; set; }
        public int? PlotId { get; set; }
        public List<PlotRandomHeroDataConfig> PlotRandomHero { get; set; }
        public bool? DifferentForce { get; set; }
        public int? TargetHeroId { get; set; }
        public string PlotCallFuc { get; set; }
        public bool? RandomStartPlot { get; set; }
        public List<SinglePlotDataConfig> PlotDatas { get; set; }
    }

    internal sealed class PlotRandomHeroDataConfig
    {
        public int? SpeHeroLimit { get; set; }
        public int? AreaLimit { get; set; }
        public List<int> AreaId { get; set; }
        public List<int> FavorRange { get; set; }
        public int? SexLimit { get; set; }
        public int? ForceLimit { get; set; }
        public List<int> ForceId { get; set; }
        public int? ForceLvLimit { get; set; }
        public float? ForceLv { get; set; }
        public int? LeaderLimit { get; set; }
    }

    internal sealed class SinglePlotDataConfig
    {
        public string PlotText { get; set; }
        public int? HeroFaceHightLightType { get; set; }
        public int? PlotSource { get; set; }
        public string SourceName { get; set; }
        public int? PlotTarget { get; set; }
        public string TargetName { get; set; }
        public List<SinglePlotChoiceConfig> Choices { get; set; }
        public string ClickCallFuc { get; set; }
        public bool? NoAutoJump { get; set; }
        public string BackPic { get; set; }
        public string BackBgm { get; set; }
        public string SoundEffect { get; set; }
        public bool? PlotShock { get; set; }
    }

    internal sealed class SinglePlotChoiceConfig
    {
        public string ChoiceText { get; set; }
        public string CallFuc { get; set; }
        public string CallParam { get; set; }
        public bool? InheritMissionRequirement { get; set; }
        public List<PlotChoiceRequirementConfig> Requirements { get; set; }
        public List<int> Relations { get; set; }
        public bool? AutoChangeCostByDifficulty { get; set; }
        public List<ResourceDataConfig> CostResource { get; set; }
        public string Describe { get; set; }
        public bool? DestroyEvent { get; set; }
        public int? PlayerInteractionTimeNeed { get; set; }
    }

    internal sealed class PlotChoiceRequirementConfig
    {
        public int RequireType { get; set; }
        public float RequireNum { get; set; }
        public bool? AutoChangeReuqireByDifficulty { get; set; }
    }

    internal sealed class ResourceDataConfig
    {
        public int ResourceType { get; set; }
        public float ResourceNum { get; set; }
    }

    internal sealed class ItemListDataConfig
    {
        public int? HeroId { get; set; }
        public int? ForceId { get; set; }
        public int? Money { get; set; }
        public float? Weight { get; set; }
        public float? MaxWeight { get; set; }
    }

    internal sealed class RandomEventConfigFile
    {
        public int SchemaVersion { get; set; } = 1;
        public string Description { get; set; }
        public List<string> Notes { get; set; }
        public List<CustomRandomEventConfig> Events { get; set; } = new();
    }

    internal sealed class CustomRandomEventConfig
    {
        public string CopyFromEventName { get; set; }
        public EventDataConfig EventData { get; set; }
    }
}
