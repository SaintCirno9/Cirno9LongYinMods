using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

[assembly: MelonInfo(typeof(LongYinCustomEvent.Main), "LongYinCustomEvent", "0.0.1", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinCustomEvent
{
    public class Main : MelonMod
    {
        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;

        internal static readonly string ConfigDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "LongYinCustomEvent");
        internal static readonly string TemplateDir = Path.Combine(ConfigDir, "WorldEventTemplates");
        internal static readonly string CustomConfigPath = Path.Combine(ConfigDir, "WorldEvent_Custom.json");
        internal static readonly string RandomCustomConfigPath = Path.Combine(ConfigDir, "RandomEvent_Custom.json");
        internal static readonly string RandomTemplateDir = Path.Combine(ConfigDir, "RandomEventTemplates");

        internal static readonly JsonSerializerSettings ConfigJsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public override void OnInitializeMelon()
        {
            EnsureConfigDirectory();
            LOG.Msg($"LongYinCustomEvent 已启动，配置目录：{ConfigDir}");
        }

        internal static void EnsureConfigDirectory()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                Directory.CreateDirectory(TemplateDir);
                Directory.CreateDirectory(RandomTemplateDir);
            }
            catch (Exception ex)
            {
                LOG.Error($"创建配置目录失败：{ex}");
            }
        }

    }

    [HarmonyPatch(typeof(WorldEventController), nameof(WorldEventController.Awake))]
    public static class InjectExternalEventsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(WorldEventController __instance)
        {
            if (__instance == null || __instance.worldEventDataBase == null || __instance.worldEventDataBase.Count == 0)
            {
                return;
            }

            Main.EnsureConfigDirectory();

            RegenerateTemplates(__instance);
            InjectCustomEvents(__instance);
        }

        private static void RegenerateTemplates(WorldEventController controller)
        {
            try
            {
                ClearTemplateDirectory();

                int generatedCount = 0;
                for (int index = 0; index < controller.worldEventDataBase.Count; index++)
                {
                    WorldEventDataBase sourceEvent = controller.worldEventDataBase[index];
                    if (sourceEvent == null)
                    {
                        continue;
                    }

                    WorldEventConfigFile template = CreateTemplateFile(sourceEvent);
                    string fileName = BuildTemplateFileName(index, sourceEvent);
                    string filePath = Path.Combine(Main.TemplateDir, fileName);
                    string templateJson = JsonConvert.SerializeObject(template, Main.ConfigJsonSettings);

                    File.WriteAllText(filePath, templateJson);
                    generatedCount++;
                }

                Main.LOG.Msg($"[世界事件模板] 已在目录中重建 {generatedCount} 份事件模板：{Main.TemplateDir}");
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[世界事件模板] 生成模板失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static void InjectCustomEvents(WorldEventController controller)
        {
            if (!File.Exists(Main.CustomConfigPath))
            {
                Main.LOG.Warning($"[世界事件注入] 未找到自定义配置文件，请在以下路径创建：{Main.CustomConfigPath}");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(Main.CustomConfigPath);
                List<WorldEventDataBase> customEvents = LoadSimplifiedConfig(controller, jsonContent);

                if (customEvents == null || customEvents.Count == 0)
                {
                    Main.LOG.Warning("[世界事件注入] 自定义配置存在，但没有可注入的事件数据。");
                    return;
                }

                int injectedCount = 0;
                foreach (WorldEventDataBase newEvent in customEvents)
                {
                    if (newEvent == null)
                    {
                        continue;
                    }

                    controller.worldEventDataBase.Add(newEvent);
                    injectedCount++;
                }

                if (injectedCount > 0)
                {
                    Main.LOG.Msg($"[世界事件注入] 成功注入 {injectedCount} 条外部事件。");
                }
                else
                {
                    Main.LOG.Warning("[世界事件注入] 未能注入任何事件，JSON 中的数据可能为空。");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[世界事件注入] 读取或解析自定义配置失败，请检查 JSON 格式：{ex}");
            }
        }

        private static List<WorldEventDataBase> LoadSimplifiedConfig(WorldEventController controller, string jsonContent)
        {
            WorldEventConfigFile configFile = JsonConvert.DeserializeObject<WorldEventConfigFile>(jsonContent, Main.ConfigJsonSettings);
            if (configFile?.Events == null || configFile.Events.Count == 0)
            {
                throw new JsonException("配置文件必须是包含 events 数组的新格式对象，且 events 不能为空。");
            }

            List<WorldEventDataBase> result = new();
            foreach (CustomWorldEventConfig config in configFile.Events)
            {
                if (config == null)
                {
                    continue;
                }

                result.Add(BuildCustomEvent(controller, config));
            }

            return result;
        }

        private static WorldEventDataBase BuildCustomEvent(WorldEventController controller, CustomWorldEventConfig config)
        {
            WorldEventDataBase resolvedBaseEvent = ResolveBaseEvent(controller, config.CopyFromEventName);
            WorldEventDataBase baseEvent = resolvedBaseEvent ?? controller.worldEventDataBase[0];
            if (!string.IsNullOrWhiteSpace(config.CopyFromEventName) && resolvedBaseEvent == null)
            {
                Main.LOG.Warning($"[世界事件注入] 未找到基础事件“{config.CopyFromEventName}”，已退回到第一条原版事件作为模板。");
            }

            WorldEventDataBase runtimeEvent = CloneWorldEvent(baseEvent);
            ApplyWorldEventConfig(runtimeEvent, config);
            return runtimeEvent;
        }

        private static void ClearTemplateDirectory()
        {
            Directory.CreateDirectory(Main.TemplateDir);

            foreach (string filePath in Directory.GetFiles(Main.TemplateDir, "*.json"))
            {
                File.Delete(filePath);
            }
        }

        private static WorldEventConfigFile CreateTemplateFile(WorldEventDataBase sourceEvent)
        {
            return new WorldEventConfigFile
            {
                SchemaVersion = 2,
                Description = $"这是事件“{sourceEvent.name}”的精简模板。推荐保留 copyFromEventName，只覆盖你真正想改的字段。",
                Notes = new List<string>
                {
                    "repeatType、eventRandomArea、plotSource、plotTarget、heroFaceHightLightType 等字段当前按游戏内部数值填写。",
                    "数组字段写 [] 代表主动清空；不写该字段则代表保留基础事件原值。"
                },
                Events = new List<CustomWorldEventConfig>
                {
                    CreateTemplateEvent(sourceEvent)
                }
            };
        }

        private static string BuildTemplateFileName(int index, WorldEventDataBase sourceEvent)
        {
            string rawName = string.IsNullOrWhiteSpace(sourceEvent?.name) ? $"Event{index + 1:D2}" : sourceEvent.name;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = rawName.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string safeName = new string(chars).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = $"Event{index + 1:D2}";
            }

            return $"{index + 1:D2}-{safeName}.json";
        }

        private static WorldEventDataBase ResolveBaseEvent(WorldEventController controller, string eventName)
        {
            if (controller?.worldEventDataBase == null || string.IsNullOrWhiteSpace(eventName))
            {
                return null;
            }

            foreach (WorldEventDataBase worldEvent in controller.worldEventDataBase)
            {
                if (worldEvent == null)
                {
                    continue;
                }

                if (string.Equals(worldEvent.name, eventName, StringComparison.OrdinalIgnoreCase))
                {
                    return worldEvent;
                }

                if (worldEvent.eventData != null && string.Equals(worldEvent.eventData.eventName, eventName, StringComparison.OrdinalIgnoreCase))
                {
                    return worldEvent;
                }
            }

            return null;
        }

        private static void ApplyWorldEventConfig(WorldEventDataBase target, CustomWorldEventConfig config)
        {
            if (target == null || config == null)
            {
                return;
            }

            if (config.Name != null)
            {
                target.name = config.Name;
            }

            if (config.StartTime != null)
            {
                target.startTime = new TimeData(config.StartTime.Year, config.StartTime.Month, config.StartTime.Day);
            }

            if (config.PlotSignRequirements != null)
            {
                target.plotSignRequirements = BuildPlotSignRequirements(config.PlotSignRequirements);
            }

            if (config.RepeatType.HasValue)
            {
                target.repeatType = (WorldEventRepeatType)config.RepeatType.Value;
            }

            if (config.RepeatDay.HasValue)
            {
                target.repeatDay = config.RepeatDay.Value;
            }

            if (config.RepeatDayRandomRange.HasValue)
            {
                target.repeatDayRandomRange = config.RepeatDayRandomRange.Value;
            }

            if (config.LastTime.HasValue)
            {
                target.lastTime = config.LastTime.Value;
            }

            if (config.NoRandomDifficulty.HasValue)
            {
                target.noRandomDifficulty = config.NoRandomDifficulty.Value;
            }

            if (config.ForceDifficulty.HasValue)
            {
                target.forceDifficulty = config.ForceDifficulty.Value;
            }

            if (config.StartCallPlot != null)
            {
                target.startCallPlot = config.StartCallPlot;
            }

            if (config.EventRandomArea.HasValue)
            {
                target.eventRandomArea = (WorldEventRandomArea)config.EventRandomArea.Value;
            }

            if (config.EventData != null)
            {
                target.eventData ??= new EventData();
                ApplyEventDataConfig(target.eventData, config.EventData);
            }
        }

        internal static void ApplyEventDataConfigPublic(EventData target, EventDataConfig config) => ApplyEventDataConfig(target, config);

        private static void ApplyEventDataConfig(EventData target, EventDataConfig config)
        {
            if (target == null || config == null)
            {
                return;
            }

            if (config.EventName != null)
            {
                target.eventName = config.EventName;
            }

            if (config.EventDescribe != null)
            {
                target.eventDescribe = config.EventDescribe;
            }

            if (config.EventAvailableAreaType != null)
            {
                target.eventAvailableAreaType = BuildEnumList<EventAvailableAreaType>(config.EventAvailableAreaType);
            }

            if (config.SpriteName != null)
            {
                target.spriteName = config.SpriteName;
            }

            if (config.IsAreaEvent.HasValue)
            {
                target.isAreaEvent = config.IsAreaEvent.Value;
            }

            if (config.ResourcePointId.HasValue)
            {
                target.resourcePointID = config.ResourcePointId.Value;
            }

            if (config.AreaId != null)
            {
                target.areaID = BuildIntList(config.AreaId);
            }

            if (config.AreaMapTileId != null)
            {
                target.areaMapTileID = BuildIntList(config.AreaMapTileId);
            }

            if (config.BigMapPos != null)
            {
                target.bigMapPos = new BigMapPos
                {
                    x = config.BigMapPos.X,
                    y = config.BigMapPos.Y
                };
            }

            if (config.NearAreaId.HasValue)
            {
                target.nearAreaID = config.NearAreaId.Value;
            }

            if (config.NearAreaDirection.HasValue)
            {
                target.nearAreaDirection = config.NearAreaDirection.Value;
            }

            if (config.AutoDestroy.HasValue)
            {
                target.autoDestroy = config.AutoDestroy.Value;
            }

            if (config.LeftTime.HasValue)
            {
                target.leftTime = config.LeftTime.Value;
            }

            if (config.Difficulty.HasValue)
            {
                target.difficulty = config.Difficulty.Value;
            }

            if (config.DifficultyRate.HasValue)
            {
                target.difficultyRate = config.DifficultyRate.Value;
            }

            if (config.SpeTargetId.HasValue)
            {
                target.speTargetID = config.SpeTargetId.Value;
            }

            if (config.PlotData != null)
            {
                target.plotData ??= new PlotData();
                ApplyPlotDataConfig(target.plotData, config.PlotData);
            }

            if (config.EventItemList != null)
            {
                target.eventItemList ??= new ItemListData();
                ApplyItemListConfig(target.eventItemList, config.EventItemList);
            }

            if (config.LeaderLimit.HasValue)
            {
                target.leaderLimit = (LeaderLimit)config.LeaderLimit.Value;
            }

            if (config.EventOutTimeCallFuc != null)
            {
                target.eventOutTimeCallFuc = config.EventOutTimeCallFuc;
            }

            if (config.SeeRange.HasValue)
            {
                target.seeRange = config.SeeRange.Value;
            }

            if (config.RandomSeed.HasValue)
            {
                target.randomSeed = config.RandomSeed.Value;
            }

            if (config.InaccuracyPosText.HasValue)
            {
                target.inaccuracyPosText = config.InaccuracyPosText.Value;
            }

            if (config.NotImportant.HasValue)
            {
                target.notImportant = config.NotImportant.Value;
            }
        }

        private static void ApplyPlotDataConfig(PlotData target, PlotDataConfig config)
        {
            if (target == null || config == null)
            {
                return;
            }

            if (config.PlotName != null)
            {
                target.plotName = config.PlotName;
            }

            if (config.SpePlot.HasValue)
            {
                target.spePlot = config.SpePlot.Value;
            }

            if (config.PlotId.HasValue)
            {
                target.plotID = config.PlotId.Value;
            }

            if (config.PlotRandomHero != null)
            {
                target.plotRandomHero = BuildPlotRandomHeroList(config.PlotRandomHero);
            }

            if (config.DifferentForce.HasValue)
            {
                target.differentForce = config.DifferentForce.Value;
            }

            if (config.TargetHeroId.HasValue)
            {
                target.targetHeroID = config.TargetHeroId.Value;
            }

            if (config.PlotCallFuc != null)
            {
                target.plotCallFuc = config.PlotCallFuc;
            }

            if (config.RandomStartPlot.HasValue)
            {
                target.randomStartPlot = config.RandomStartPlot.Value;
            }

            if (config.PlotDatas != null)
            {
                target.plotDatas = BuildSinglePlotList(config.PlotDatas);
            }
        }

        private static void ApplyItemListConfig(ItemListData target, ItemListDataConfig config)
        {
            if (target == null || config == null)
            {
                return;
            }

            if (config.HeroId.HasValue)
            {
                target.heroID = config.HeroId.Value;
            }

            if (config.ForceId.HasValue)
            {
                target.forceID = config.ForceId.Value;
            }

            if (config.Money.HasValue)
            {
                target.money = config.Money.Value;
            }

            if (config.Weight.HasValue)
            {
                target.weight = config.Weight.Value;
            }

            if (config.MaxWeight.HasValue)
            {
                target.maxWeight = config.MaxWeight.Value;
            }
        }

        private static Il2CppSystem.Collections.Generic.List<PlotSignRequirement> BuildPlotSignRequirements(List<PlotSignRequirementConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotSignRequirement> result = new();
            foreach (PlotSignRequirementConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                PlotSignRequirement runtimeItem = new()
                {
                    plotSignRequirementType = (PlotSignRequirementType)item.PlotSignRequirementType,
                    plotSignID = item.PlotSignId
                };
                result.Add(runtimeItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> BuildPlotRandomHeroList(List<PlotRandomHeroDataConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> result = new();
            foreach (PlotRandomHeroDataConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                PlotRandomHeroData runtimeItem = new();
                if (item.SpeHeroLimit.HasValue)
                {
                    runtimeItem.speHeroLimit = (SpeHeroLimit)item.SpeHeroLimit.Value;
                }

                if (item.AreaLimit.HasValue)
                {
                    runtimeItem.areaLimit = (AreaLimit)item.AreaLimit.Value;
                }

                if (item.AreaId != null)
                {
                    runtimeItem.areaID = BuildIntList(item.AreaId);
                }

                if (item.FavorRange != null)
                {
                    runtimeItem.favorRange = BuildIntList(item.FavorRange);
                }

                if (item.SexLimit.HasValue)
                {
                    runtimeItem.sexLimit = (SexLimit)item.SexLimit.Value;
                }

                if (item.ForceLimit.HasValue)
                {
                    runtimeItem.forceLimit = (ForceLimit)item.ForceLimit.Value;
                }

                if (item.ForceId != null)
                {
                    runtimeItem.forceID = BuildIntList(item.ForceId);
                }

                if (item.ForceLvLimit.HasValue)
                {
                    runtimeItem.forceLvLimit = (ForceLvLimit)item.ForceLvLimit.Value;
                }

                if (item.ForceLv.HasValue)
                {
                    runtimeItem.forceLv = item.ForceLv.Value;
                }

                if (item.LeaderLimit.HasValue)
                {
                    runtimeItem.leaderLimit = (LeaderLimit)item.LeaderLimit.Value;
                }

                result.Add(runtimeItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<SinglePlotData> BuildSinglePlotList(List<SinglePlotDataConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<SinglePlotData> result = new();
            foreach (SinglePlotDataConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                SinglePlotData runtimeItem = new();
                if (item.PlotText != null)
                {
                    runtimeItem.plotText = item.PlotText;
                }

                if (item.HeroFaceHightLightType.HasValue)
                {
                    runtimeItem.heroFaceHightLightType = (HeroFaceHightLightType)item.HeroFaceHightLightType.Value;
                }

                if (item.PlotSource.HasValue)
                {
                    runtimeItem.plotSource = (PlotTargetHeroType)item.PlotSource.Value;
                }

                if (item.SourceName != null)
                {
                    runtimeItem.sourceName = item.SourceName;
                }

                if (item.PlotTarget.HasValue)
                {
                    runtimeItem.plotTarget = (PlotTargetHeroType)item.PlotTarget.Value;
                }

                if (item.TargetName != null)
                {
                    runtimeItem.targetName = item.TargetName;
                }

                if (item.Choices != null)
                {
                    runtimeItem.choices = BuildChoiceList(item.Choices);
                }

                if (item.ClickCallFuc != null)
                {
                    runtimeItem.clickCallFuc = item.ClickCallFuc;
                }

                if (item.NoAutoJump.HasValue)
                {
                    runtimeItem.noAutoJump = item.NoAutoJump.Value;
                }

                if (item.BackPic != null)
                {
                    runtimeItem.backPic = item.BackPic;
                }

                if (item.BackBgm != null)
                {
                    runtimeItem.backBgm = item.BackBgm;
                }

                if (item.SoundEffect != null)
                {
                    runtimeItem.soundEffect = item.SoundEffect;
                }

                if (item.PlotShock.HasValue)
                {
                    runtimeItem.plotShock = item.PlotShock.Value;
                }

                result.Add(runtimeItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> BuildChoiceList(List<SinglePlotChoiceConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> result = new();
            foreach (SinglePlotChoiceConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                SinglePlotChoiceData runtimeItem = new();
                if (item.ChoiceText != null)
                {
                    runtimeItem.choiceText = item.ChoiceText;
                }

                if (item.CallFuc != null)
                {
                    runtimeItem.callFuc = item.CallFuc;
                }

                if (item.CallParam != null)
                {
                    runtimeItem.callParam = item.CallParam;
                }

                if (item.InheritMissionRequirement.HasValue)
                {
                    runtimeItem.inheritMissionRequirement = item.InheritMissionRequirement.Value;
                }

                if (item.Requirements != null)
                {
                    runtimeItem.requirements = BuildRequirementList(item.Requirements);
                }

                if (item.Relations != null)
                {
                    runtimeItem.relations = BuildEnumList<RelationRequirementType>(item.Relations);
                }

                if (item.AutoChangeCostByDifficulty.HasValue)
                {
                    runtimeItem.autoChangeCostByDifficulty = item.AutoChangeCostByDifficulty.Value;
                }

                if (item.CostResource != null)
                {
                    runtimeItem.costResource = BuildResourceList(item.CostResource);
                }

                if (item.Describe != null)
                {
                    runtimeItem.describe = item.Describe;
                }

                if (item.DestroyEvent.HasValue)
                {
                    runtimeItem.destroyEvent = item.DestroyEvent.Value;
                }

                if (item.PlayerInteractionTimeNeed.HasValue)
                {
                    runtimeItem.playerInteractionTimeNeed = (PlayerInteractionTimeType)item.PlayerInteractionTimeNeed.Value;
                }

                result.Add(runtimeItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> BuildRequirementList(List<PlotChoiceRequirementConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> result = new();
            foreach (PlotChoiceRequirementConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                PlotChoiceRequirement runtimeItem = new((ChoiceRequirementType)item.RequireType, item.RequireNum);

                if (item.AutoChangeReuqireByDifficulty.HasValue)
                {
                    runtimeItem.autoChangeReuqireByDifficulty = item.AutoChangeReuqireByDifficulty.Value;
                }

                result.Add(runtimeItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<ResourceData> BuildResourceList(List<ResourceDataConfig> source)
        {
            Il2CppSystem.Collections.Generic.List<ResourceData> result = new();
            foreach (ResourceDataConfig item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new ResourceData(item.ResourceType, item.ResourceNum));
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<int> BuildIntList(List<int> source)
        {
            Il2CppSystem.Collections.Generic.List<int> result = new();
            foreach (int item in source)
            {
                result.Add(item);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<TEnum> BuildEnumList<TEnum>(List<int> source) where TEnum : struct, Enum
        {
            Il2CppSystem.Collections.Generic.List<TEnum> result = new();
            foreach (int item in source)
            {
                result.Add((TEnum)Enum.ToObject(typeof(TEnum), item));
            }

            return result;
        }

        private static WorldEventDataBase CloneWorldEvent(WorldEventDataBase source)
        {
            if (source == null)
            {
                return new WorldEventDataBase();
            }

            return new WorldEventDataBase
            {
                name = source.name,
                startTime = CloneTimeData(source.startTime),
                plotSignRequirements = ClonePlotSignRequirements(source.plotSignRequirements),
                repeatType = source.repeatType,
                repeatDay = source.repeatDay,
                repeatDayRandomRange = source.repeatDayRandomRange,
                lastTime = source.lastTime,
                noRandomDifficulty = source.noRandomDifficulty,
                forceDifficulty = source.forceDifficulty,
                startCallPlot = source.startCallPlot,
                eventRandomArea = source.eventRandomArea,
                eventData = CloneEventData(source.eventData)
            };
        }

        private static TimeData CloneTimeData(TimeData source)
        {
            if (source == null)
            {
                return new TimeData(1, 1, 1);
            }

            return new TimeData(source.year, source.month, source.day);
        }

        internal static EventData CloneEventDataPublic(EventData source) => CloneEventData(source);

        private static EventData CloneEventData(EventData source)
        {
            if (source == null)
            {
                return new EventData();
            }

            return new EventData
            {
                eventName = source.eventName,
                eventDescribe = source.eventDescribe,
                eventAvailableAreaType = CloneList(source.eventAvailableAreaType),
                spriteName = source.spriteName,
                isAreaEvent = source.isAreaEvent,
                resourcePointID = source.resourcePointID,
                areaID = CloneList(source.areaID),
                areaMapTileID = CloneList(source.areaMapTileID),
                bigMapPos = CloneBigMapPos(source.bigMapPos),
                nearAreaID = source.nearAreaID,
                nearAreaDirection = source.nearAreaDirection,
                seen = source.seen,
                happened = source.happened,
                noticed = source.noticed,
                hovered = source.hovered,
                plotTargetEvent = source.plotTargetEvent,
                missionTargetEvent = source.missionTargetEvent,
                autoDestroy = source.autoDestroy,
                leftTime = source.leftTime,
                difficulty = source.difficulty,
                difficultyRate = source.difficultyRate,
                speTargetID = source.speTargetID,
                plotData = ClonePlotData(source.plotData),
                eventItemList = CloneItemListData(source.eventItemList),
                leaderLimit = source.leaderLimit,
                eventOutTimeCallFuc = source.eventOutTimeCallFuc,
                seeRange = source.seeRange,
                randomSeed = source.randomSeed,
                inaccuracyPosText = source.inaccuracyPosText,
                notImportant = source.notImportant
            };
        }

        private static BigMapPos CloneBigMapPos(BigMapPos source)
        {
            if (source == null)
            {
                return new BigMapPos();
            }

            return new BigMapPos
            {
                x = source.x,
                y = source.y
            };
        }

        private static PlotData ClonePlotData(PlotData source)
        {
            if (source == null)
            {
                return new PlotData();
            }

            return new PlotData
            {
                plotName = source.plotName,
                spePlot = source.spePlot,
                plotID = source.plotID,
                plotRandomHero = ClonePlotRandomHeroList(source.plotRandomHero),
                differentForce = source.differentForce,
                targetHeroID = source.targetHeroID,
                plotCallFuc = source.plotCallFuc,
                randomStartPlot = source.randomStartPlot,
                plotDatas = CloneSinglePlotDataList(source.plotDatas)
            };
        }

        private static ItemListData CloneItemListData(ItemListData source)
        {
            if (source == null)
            {
                return new ItemListData();
            }

            return new ItemListData
            {
                heroID = source.heroID,
                forceID = source.forceID,
                money = source.money,
                weight = source.weight,
                maxWeight = source.maxWeight,
                allItem = CloneList(source.allItem),
                itemTypeList = CloneNestedItemList(source.itemTypeList)
            };
        }

        private static Il2CppSystem.Collections.Generic.List<PlotSignRequirement> ClonePlotSignRequirements(Il2CppSystem.Collections.Generic.List<PlotSignRequirement> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotSignRequirement> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (PlotSignRequirement item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new PlotSignRequirement
                {
                    plotSignRequirementType = item.plotSignRequirementType,
                    plotSignID = item.plotSignID
                });
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> ClonePlotRandomHeroList(Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (PlotRandomHeroData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new PlotRandomHeroData
                {
                    speHeroLimit = item.speHeroLimit,
                    areaLimit = item.areaLimit,
                    areaID = CloneList(item.areaID),
                    favorRange = CloneList(item.favorRange),
                    sexLimit = item.sexLimit,
                    forceLimit = item.forceLimit,
                    forceID = CloneList(item.forceID),
                    forceLvLimit = item.forceLvLimit,
                    forceLv = item.forceLv,
                    leaderLimit = item.leaderLimit
                });
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<SinglePlotData> CloneSinglePlotDataList(Il2CppSystem.Collections.Generic.List<SinglePlotData> source)
        {
            Il2CppSystem.Collections.Generic.List<SinglePlotData> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (SinglePlotData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new SinglePlotData
                {
                    plotText = item.plotText,
                    heroFaceHightLightType = item.heroFaceHightLightType,
                    plotSource = item.plotSource,
                    sourceName = item.sourceName,
                    plotTarget = item.plotTarget,
                    targetName = item.targetName,
                    choices = CloneChoiceList(item.choices),
                    clickCallFuc = item.clickCallFuc,
                    noAutoJump = item.noAutoJump,
                    backPic = item.backPic,
                    backBgm = item.backBgm,
                    soundEffect = item.soundEffect,
                    plotShock = item.plotShock
                });
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> CloneChoiceList(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> source)
        {
            Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (SinglePlotChoiceData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new SinglePlotChoiceData
                {
                    choiceText = item.choiceText,
                    callFuc = item.callFuc,
                    callParam = item.callParam,
                    inited = item.inited,
                    inheritMissionRequirement = item.inheritMissionRequirement,
                    requirements = CloneRequirementList(item.requirements),
                    relations = CloneList(item.relations),
                    autoChangeCostByDifficulty = item.autoChangeCostByDifficulty,
                    costResource = CloneResourceList(item.costResource),
                    describe = item.describe,
                    destroyEvent = item.destroyEvent,
                    playerInteractionTimeNeed = item.playerInteractionTimeNeed
                });
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> CloneRequirementList(Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> source)
        {
            Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (PlotChoiceRequirement item in source)
            {
                if (item == null)
                {
                    continue;
                }

                PlotChoiceRequirement clonedItem = new(item.requireType, item.requireNum)
                {
                    autoChangeReuqireByDifficulty = item.autoChangeReuqireByDifficulty
                };
                result.Add(clonedItem);
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<ResourceData> CloneResourceList(Il2CppSystem.Collections.Generic.List<ResourceData> source)
        {
            Il2CppSystem.Collections.Generic.List<ResourceData> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (ResourceData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new ResourceData(item.resourceType, item.resourceNum));
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<ItemData>> CloneNestedItemList(Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<ItemData>> source)
        {
            Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<ItemData>> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (Il2CppSystem.Collections.Generic.List<ItemData> innerList in source)
            {
                result.Add(CloneList(innerList));
            }

            return result;
        }

        private static Il2CppSystem.Collections.Generic.List<T> CloneList<T>(Il2CppSystem.Collections.Generic.List<T> source)
        {
            Il2CppSystem.Collections.Generic.List<T> result = new();
            if (source == null)
            {
                return result;
            }

            foreach (T item in source)
            {
                result.Add(item);
            }

            return result;
        }

        private static CustomWorldEventConfig CreateTemplateEvent(WorldEventDataBase source)
        {
            CustomWorldEventConfig template = new()
            {
                CopyFromEventName = source.name,
                Name = $"{source.name}_自定义",
                StartTime = ToConfig(source.startTime),
                PlotSignRequirements = ToConfig(source.plotSignRequirements),
                RepeatType = (int)source.repeatType,
                RepeatDay = source.repeatDay,
                RepeatDayRandomRange = source.repeatDayRandomRange,
                LastTime = source.lastTime,
                NoRandomDifficulty = source.noRandomDifficulty,
                ForceDifficulty = source.forceDifficulty,
                StartCallPlot = source.startCallPlot,
                EventRandomArea = (int)source.eventRandomArea,
                EventData = ToConfig(source.eventData)
            };

            if (template.EventData != null && !string.IsNullOrWhiteSpace(template.EventData.EventName))
            {
                template.EventData.EventName = $"{template.EventData.EventName}_自定义";
            }

            return template;
        }

        private static TimeDataConfig ToConfig(TimeData source)
        {
            if (source == null)
            {
                return null;
            }

            return new TimeDataConfig
            {
                Year = source.year,
                Month = source.month,
                Day = source.day
            };
        }

        private static List<PlotSignRequirementConfig> ToConfig(Il2CppSystem.Collections.Generic.List<PlotSignRequirement> source)
        {
            if (source == null)
            {
                return null;
            }

            List<PlotSignRequirementConfig> result = new();
            foreach (PlotSignRequirement item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new PlotSignRequirementConfig
                {
                    PlotSignRequirementType = (int)item.plotSignRequirementType,
                    PlotSignId = item.plotSignID
                });
            }

            return result;
        }

        internal static EventDataConfig ToConfigPublic(EventData source) => ToConfig(source);

        private static EventDataConfig ToConfig(EventData source)
        {
            if (source == null)
            {
                return null;
            }

            return new EventDataConfig
            {
                EventName = source.eventName,
                EventDescribe = source.eventDescribe,
                EventAvailableAreaType = ToIntList(source.eventAvailableAreaType),
                SpriteName = source.spriteName,
                IsAreaEvent = source.isAreaEvent,
                ResourcePointId = source.resourcePointID,
                AreaId = ToIntList(source.areaID),
                AreaMapTileId = ToIntList(source.areaMapTileID),
                BigMapPos = source.bigMapPos == null ? null : new BigMapPosConfig
                {
                    X = source.bigMapPos.x,
                    Y = source.bigMapPos.y
                },
                NearAreaId = source.nearAreaID,
                NearAreaDirection = source.nearAreaDirection,
                AutoDestroy = source.autoDestroy,
                LeftTime = source.leftTime,
                Difficulty = source.difficulty,
                DifficultyRate = source.difficultyRate,
                SpeTargetId = source.speTargetID,
                PlotData = ToConfig(source.plotData),
                EventItemList = ToConfig(source.eventItemList),
                LeaderLimit = (int)source.leaderLimit,
                EventOutTimeCallFuc = source.eventOutTimeCallFuc,
                SeeRange = source.seeRange,
                RandomSeed = source.randomSeed,
                InaccuracyPosText = source.inaccuracyPosText,
                NotImportant = source.notImportant
            };
        }

        private static PlotDataConfig ToConfig(PlotData source)
        {
            if (source == null)
            {
                return null;
            }

            return new PlotDataConfig
            {
                PlotName = source.plotName,
                SpePlot = source.spePlot,
                PlotId = source.plotID,
                PlotRandomHero = ToConfig(source.plotRandomHero),
                DifferentForce = source.differentForce,
                TargetHeroId = source.targetHeroID,
                PlotCallFuc = source.plotCallFuc,
                RandomStartPlot = source.randomStartPlot,
                PlotDatas = ToConfig(source.plotDatas)
            };
        }

        private static List<PlotRandomHeroDataConfig> ToConfig(Il2CppSystem.Collections.Generic.List<PlotRandomHeroData> source)
        {
            if (source == null)
            {
                return null;
            }

            List<PlotRandomHeroDataConfig> result = new();
            foreach (PlotRandomHeroData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new PlotRandomHeroDataConfig
                {
                    SpeHeroLimit = (int)item.speHeroLimit,
                    AreaLimit = (int)item.areaLimit,
                    AreaId = ToIntList(item.areaID),
                    FavorRange = ToIntList(item.favorRange),
                    SexLimit = (int)item.sexLimit,
                    ForceLimit = (int)item.forceLimit,
                    ForceId = ToIntList(item.forceID),
                    ForceLvLimit = (int)item.forceLvLimit,
                    ForceLv = item.forceLv,
                    LeaderLimit = (int)item.leaderLimit
                });
            }

            return result;
        }

        private static List<SinglePlotDataConfig> ToConfig(Il2CppSystem.Collections.Generic.List<SinglePlotData> source)
        {
            if (source == null)
            {
                return null;
            }

            List<SinglePlotDataConfig> result = new();
            foreach (SinglePlotData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new SinglePlotDataConfig
                {
                    PlotText = item.plotText,
                    HeroFaceHightLightType = (int)item.heroFaceHightLightType,
                    PlotSource = (int)item.plotSource,
                    SourceName = item.sourceName,
                    PlotTarget = (int)item.plotTarget,
                    TargetName = item.targetName,
                    Choices = ToConfig(item.choices),
                    ClickCallFuc = item.clickCallFuc,
                    NoAutoJump = item.noAutoJump,
                    BackPic = item.backPic,
                    BackBgm = item.backBgm,
                    SoundEffect = item.soundEffect,
                    PlotShock = item.plotShock
                });
            }

            return result;
        }

        private static List<SinglePlotChoiceConfig> ToConfig(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> source)
        {
            if (source == null)
            {
                return null;
            }

            List<SinglePlotChoiceConfig> result = new();
            foreach (SinglePlotChoiceData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new SinglePlotChoiceConfig
                {
                    ChoiceText = item.choiceText,
                    CallFuc = item.callFuc,
                    CallParam = item.callParam,
                    InheritMissionRequirement = item.inheritMissionRequirement,
                    Requirements = ToConfig(item.requirements),
                    Relations = ToIntList(item.relations),
                    AutoChangeCostByDifficulty = item.autoChangeCostByDifficulty,
                    CostResource = ToConfig(item.costResource),
                    Describe = item.describe,
                    DestroyEvent = item.destroyEvent,
                    PlayerInteractionTimeNeed = (int)item.playerInteractionTimeNeed
                });
            }

            return result;
        }

        private static List<PlotChoiceRequirementConfig> ToConfig(Il2CppSystem.Collections.Generic.List<PlotChoiceRequirement> source)
        {
            if (source == null)
            {
                return null;
            }

            List<PlotChoiceRequirementConfig> result = new();
            foreach (PlotChoiceRequirement item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new PlotChoiceRequirementConfig
                {
                    RequireType = (int)item.requireType,
                    RequireNum = item.requireNum,
                    AutoChangeReuqireByDifficulty = item.autoChangeReuqireByDifficulty
                });
            }

            return result;
        }

        private static List<ResourceDataConfig> ToConfig(Il2CppSystem.Collections.Generic.List<ResourceData> source)
        {
            if (source == null)
            {
                return null;
            }

            List<ResourceDataConfig> result = new();
            foreach (ResourceData item in source)
            {
                if (item == null)
                {
                    continue;
                }

                result.Add(new ResourceDataConfig
                {
                    ResourceType = item.resourceType,
                    ResourceNum = item.resourceNum
                });
            }

            return result;
        }

        private static ItemListDataConfig ToConfig(ItemListData source)
        {
            if (source == null)
            {
                return null;
            }

            return new ItemListDataConfig
            {
                HeroId = source.heroID,
                ForceId = source.forceID,
                Money = source.money,
                Weight = source.weight,
                MaxWeight = source.maxWeight
            };
        }

        private static List<int> ToIntList<T>(Il2CppSystem.Collections.Generic.List<T> source)
        {
            if (source == null)
            {
                return null;
            }

            List<int> result = new();
            foreach (T item in source)
            {
                result.Add(Convert.ToInt32(item));
            }

            return result;
        }
    }

    [HarmonyPatch(typeof(RandomEventController), nameof(RandomEventController.Awake))]
    public static class InjectExternalRandomEventsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(RandomEventController __instance)
        {
            if (__instance == null || __instance.RandomEventDataBase == null || __instance.RandomEventDataBase.Count == 0)
            {
                return;
            }

            Main.EnsureConfigDirectory();

            RegenerateRandomTemplates(__instance);
            InjectCustomRandomEvents(__instance);
        }

        private static void RegenerateRandomTemplates(RandomEventController controller)
        {
            try
            {
                Directory.CreateDirectory(Main.RandomTemplateDir);
                foreach (string filePath in Directory.GetFiles(Main.RandomTemplateDir, "*.json"))
                {
                    File.Delete(filePath);
                }

                int generatedCount = 0;
                for (int index = 0; index < controller.RandomEventDataBase.Count; index++)
                {
                    EventData sourceEvent = controller.RandomEventDataBase[index];
                    if (sourceEvent == null)
                    {
                        continue;
                    }

                    RandomEventConfigFile template = new()
                    {
                        SchemaVersion = 1,
                        Description = $"这是随机事件\"{sourceEvent.eventName}\"的精简模板。推荐保留 copyFromEventName，只覆盖你真正想改的字段。",
                        Notes = new List<string>
                        {
                            "数组字段写 [] 代表主动清空；不写该字段则代表保留基础事件原值。"
                        },
                        Events = new List<CustomRandomEventConfig>
                        {
                            new()
                            {
                                CopyFromEventName = sourceEvent.eventName,
                                EventData = InjectExternalEventsPatch.ToConfigPublic(sourceEvent)
                            }
                        }
                    };

                    string rawName = string.IsNullOrWhiteSpace(sourceEvent.eventName) ? $"Event{index + 1:D2}" : sourceEvent.eventName;
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    char[] chars = rawName.ToCharArray();
                    for (int i = 0; i < chars.Length; i++)
                    {
                        if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                        {
                            chars[i] = '_';
                        }
                    }

                    string safeName = new string(chars).Trim();
                    if (string.IsNullOrWhiteSpace(safeName))
                    {
                        safeName = $"Event{index + 1:D2}";
                    }

                    string fileName = $"{index + 1:D2}-{safeName}.json";
                    string filePath = Path.Combine(Main.RandomTemplateDir, fileName);
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(template, Main.ConfigJsonSettings));
                    generatedCount++;
                }

                Main.LOG.Msg($"[随机事件模板] 已重建 {generatedCount} 份模板：{Main.RandomTemplateDir}");
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[随机事件模板] 生成失败：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static void InjectCustomRandomEvents(RandomEventController controller)
        {
            if (!File.Exists(Main.RandomCustomConfigPath))
            {
                Main.LOG.Warning($"[随机事件注入] 未找到自定义配置文件，请在以下路径创建：{Main.RandomCustomConfigPath}");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(Main.RandomCustomConfigPath);
                RandomEventConfigFile configFile = JsonConvert.DeserializeObject<RandomEventConfigFile>(jsonContent, Main.ConfigJsonSettings);

                if (configFile?.Events == null || configFile.Events.Count == 0)
                {
                    Main.LOG.Warning("[随机事件注入] 配置文件存在，但没有可注入的事件数据。");
                    return;
                }

                int injectedCount = 0;
                foreach (CustomRandomEventConfig config in configFile.Events)
                {
                    if (config?.EventData == null)
                    {
                        continue;
                    }

                    EventData baseEvent = ResolveBaseRandomEvent(controller, config.CopyFromEventName);
                    EventData runtimeEvent = CloneEventDataPublic(baseEvent);
                    InjectExternalEventsPatch.ApplyEventDataConfigPublic(runtimeEvent, config.EventData);
                    controller.RandomEventDataBase.Add(runtimeEvent);
                    injectedCount++;
                }

                if (injectedCount > 0)
                {
                    Main.LOG.Msg($"[随机事件注入] 成功注入 {injectedCount} 条随机事件。");
                }
                else
                {
                    Main.LOG.Warning("[随机事件注入] 未能注入任何事件。");
                }
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[随机事件注入] 读取或解析配置失败：{ex}");
            }
        }

        private static EventData ResolveBaseRandomEvent(RandomEventController controller, string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return controller.RandomEventDataBase[0];
            }

            foreach (EventData e in controller.RandomEventDataBase)
            {
                if (e != null && string.Equals(e.eventName, eventName, StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }

            Main.LOG.Warning($"[随机事件注入] 未找到基础事件\"{eventName}\"，已退回到第一条原版事件。");
            return controller.RandomEventDataBase[0];
        }

        private static EventData CloneEventDataPublic(EventData source)
        {
            return InjectExternalEventsPatch.CloneEventDataPublic(source);
        }
    }
}
