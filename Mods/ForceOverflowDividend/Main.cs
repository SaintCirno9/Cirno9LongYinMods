using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ForceOverflowDividend.Main), "ForceOverflowDividend", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace ForceOverflowDividend
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        internal static bool VerboseLog => ForceOverflowRuntime.Config.VerboseLog;

        public override void OnInitializeMelon()
        {
            ForceOverflowRuntime.Initialize(ForceOverflowConfigStore.Load(Log));

            Log.Msg(
                $"ForceOverflowDividend 已加载。配置文件：{ForceOverflowConfigStore.ConfigPathDisplay}。启用={FormatEnabled(ForceOverflowRuntime.Config.Enabled)}，资源折现比例可通过 ResourceRate.<id> 单独配置，默认钱=100%，其他=10%，VerboseLog={FormatEnabled(VerboseLog)}。");
        }

        internal static void LogVerbose(string message)
        {
            if (VerboseLog)
            {
                Log.Msg(message);
            }
        }

        private static string FormatEnabled(bool value)
        {
            return value ? "开" : "关";
        }
    }

    internal static class ForceOverflowRuntime
    {
        private static ForceOverflowConfig _config = ForceOverflowConfig.CreateDefault();
        private static int[] _trackedResourceIds = Array.Empty<int>();
        private static bool _resourceLookupWarningShown;

        internal static ForceOverflowConfig Config => _config;

        internal static void Initialize(ForceOverflowConfig config)
        {
            _config = config ?? ForceOverflowConfig.CreateDefault();
            _trackedResourceIds = Array.Empty<int>();
            _resourceLookupWarningShown = false;
        }

        internal static ResourceChangeContext CreateContext(ForceData force, string triggerText)
        {
            if (!_config.Enabled || force == null)
            {
                return null;
            }

            RefreshTrackedResourceIds();

            if (_trackedResourceIds.Length == 0)
            {
                if (!_resourceLookupWarningShown)
                {
                    _resourceLookupWarningShown = true;
                    Main.Log.Warning("未能读取 GlobalData.ResourceName，已暂时跳过门派资源溢出折现。");
                }

                return null;
            }

            return new ResourceChangeContext
            {
                TriggerText = triggerText,
                BeforeValues = CaptureTrackedResourceValues(force)
            };
        }

        internal static void RecordPositiveDelta(ResourceChangeContext context, int resourceId, float delta)
        {
            if (context == null || delta <= 0.0001f || !IsTrackedResourceId(resourceId))
            {
                return;
            }

            if (context.PositiveDeltas.TryGetValue(resourceId, out float current))
            {
                context.PositiveDeltas[resourceId] = current + delta;
            }
            else
            {
                context.PositiveDeltas[resourceId] = delta;
            }
        }

        internal static void HandleForceResourceChanged(ForceData force, ResourceChangeContext context)
        {
            if (!_config.Enabled || force == null || context == null || context.PositiveDeltas.Count == 0)
            {
                return;
            }

            WorldData worldData = GameController.Instance?.worldData;
            bool isPlayerForce = IsPlayerForce(worldData, force);
            int playerWeight = isPlayerForce ? GetPlayerPopulationWeight(worldData) : 0;

            if (!TryProcessExpectedOverflow(force, worldData, context, out int convertedMoney, out string detailText))
            {
                return;
            }

            Main.LogVerbose(
                $"门派资源溢出已立刻结算：门派={SafeForceName(force)}，触发={context.TriggerText}，主角权重={playerWeight}，折现金额={convertedMoney}，{detailText}。结算后资源={BuildForceTrackedResourceSnapshot(force)}。");
        }

        private static bool TryProcessExpectedOverflow(ForceData force, WorldData worldData, ResourceChangeContext context, out int convertedMoney, out string detailText)
        {
            convertedMoney = 0;
            detailText = string.Empty;

            if (force.resourceStore == null || force.resourceStoreMax == null)
            {
                return false;
            }

            var detailParts = new List<string>();
            bool foundOverflow = false;

            for (int i = 0; i < _trackedResourceIds.Length; i++)
            {
                int resourceId = _trackedResourceIds[i];
                if (!context.PositiveDeltas.TryGetValue(resourceId, out float positiveDelta) ||
                    positiveDelta <= 0.0001f ||
                    !context.BeforeValues.TryGetValue(resourceId, out float beforeValue) ||
                    !TryGetListValue(force.resourceStoreMax, resourceId, out float maxValue))
                {
                    continue;
                }

                float overflow = beforeValue + positiveDelta - maxValue;
                if (overflow <= 0.0001f)
                {
                    continue;
                }

                foundOverflow = true;
                int resourceMoney = ConvertOverflowToMoney(resourceId, overflow);
                convertedMoney += resourceMoney;

                detailParts.Add($"{GetResourceName(resourceId)} 原值 {beforeValue:0.##}，本次增加 {positiveDelta:0.##}，超出上限 {overflow:0.##}，按 {GetOverflowMoneyRatePercent(resourceId)}% 折现 {resourceMoney} 钱");
            }

            if (!foundOverflow)
            {
                return false;
            }

            List<HeroData> members = CollectEligibleMembers(force);
            if (members.Count == 0)
            {
                detailText = "检测到溢出，但未找到可发放成员";
                Main.Log.Warning($"门派 {SafeForceName(force)} 检测到资源溢出，但没有可领取折现的门派成员。");
                return true;
            }

            int totalPopulationWeight = GetTotalPopulationWeight(members);
            DistributeMoney(worldData, members, convertedMoney);

            if (detailParts.Count > 0)
            {
                detailParts.Add($"参与分配人数 {members.Count}，总占用人口 {totalPopulationWeight}");
            }

            detailText = string.Join("；", detailParts);
            return true;
        }

        private static void DistributeMoney(WorldData worldData, List<HeroData> members, int totalMoney)
        {
            if (totalMoney <= 0 || members.Count == 0)
            {
                return;
            }

            HeroData player = worldData?.Player();
            int totalWeight = GetTotalPopulationWeight(members);
            if (totalWeight <= 0)
            {
                return;
            }

            var distributions = new List<MemberDistribution>(members.Count);
            int assignedMoney = 0;

            for (int i = 0; i < members.Count; i++)
            {
                HeroData hero = members[i];
                if (hero == null)
                {
                    continue;
                }

                int weight = GetHeroPopulationWeight(hero);
                double exactShare = (double)totalMoney * weight / totalWeight;
                int payout = (int)Math.Floor(exactShare);
                assignedMoney += payout;

                distributions.Add(new MemberDistribution
                {
                    Hero = hero,
                    Weight = weight,
                    Payout = payout,
                    Remainder = exactShare - payout
                });
            }

            distributions.Sort(static (a, b) =>
            {
                int remainderCompare = b.Remainder.CompareTo(a.Remainder);
                if (remainderCompare != 0)
                {
                    return remainderCompare;
                }

                int weightCompare = b.Weight.CompareTo(a.Weight);
                if (weightCompare != 0)
                {
                    return weightCompare;
                }

                return a.Hero.heroID.CompareTo(b.Hero.heroID);
            });

            int leftoverMoney = totalMoney - assignedMoney;
            for (int i = 0; i < leftoverMoney && i < distributions.Count; i++)
            {
                distributions[i].Payout++;
            }

            for (int i = 0; i < distributions.Count; i++)
            {
                MemberDistribution distribution = distributions[i];
                int payout = distribution.Payout;
                if (payout <= 0)
                {
                    continue;
                }

                bool showInfo = player != null && distribution.Hero.heroID == player.heroID;
                distribution.Hero.ChangeMoney(payout, showInfo);

                Main.LogVerbose(
                    $"已按人口权重发放资源溢出折现：hero={distribution.Hero.heroName}，forceLv={distribution.Hero.heroForceLv}，populationWeight={distribution.Weight}，money={payout}");
            }
        }

        private static List<HeroData> CollectEligibleMembers(ForceData force)
        {
            var result = new List<HeroData>();
            var seenHeroIds = new HashSet<int>();
            var ownHeros = force.GetOwnHeros();

            if (ownHeros != null)
            {
                for (int i = 0; i < ownHeros.Count; i++)
                {
                    HeroData hero = ownHeros[i];
                    if (!IsEligibleMember(hero) || !seenHeroIds.Add(hero.heroID))
                    {
                        continue;
                    }

                    result.Add(hero);
                }
            }

            if (result.Count == 0)
            {
                HeroData leader = force.GetLeader();
                if (IsEligibleMember(leader))
                {
                    result.Add(leader);
                }
            }

            return result;
        }

        private static bool IsEligibleMember(HeroData hero)
        {
            return hero != null && !hero.dead;
        }

        private static int GetTotalPopulationWeight(List<HeroData> members)
        {
            int totalWeight = 0;
            for (int i = 0; i < members.Count; i++)
            {
                totalWeight += GetHeroPopulationWeight(members[i]);
            }

            return totalWeight;
        }

        private static int GetHeroPopulationWeight(HeroData hero)
        {
            if (hero == null)
            {
                return 0;
            }

            // 门派弟子占用人口按 1/2/4/8/16/32 递增，超过图示上限时按 32 处理。
            int clampedLevel = Mathf.Clamp(hero.heroForceLv, 0, 5);
            return 1 << clampedLevel;
        }

        private static int GetPlayerPopulationWeight(WorldData worldData)
        {
            HeroData player = worldData?.Player();
            return GetHeroPopulationWeight(player);
        }

        private static int ConvertOverflowToMoney(int resourceId, float overflow)
        {
            float unitValue = 1f;
            if (GlobalData.ResourceValue != null &&
                resourceId >= 0 &&
                resourceId < GlobalData.ResourceValue.Count)
            {
                unitValue = Mathf.Max(0f, GlobalData.ResourceValue[resourceId]);
            }

            float totalValue = overflow * unitValue;
            return Mathf.FloorToInt(totalValue * GetOverflowMoneyRatePercent(resourceId) / 100f);
        }

        private static void RefreshTrackedResourceIds()
        {
            if (GlobalData.ResourceName == null || GlobalData.ResourceName.Count == 0)
            {
                _trackedResourceIds = Array.Empty<int>();
                return;
            }

            ForceOverflowConfigStore.SyncKnownResourceRates(_config, Main.Log);

            // 最后一项是威望，不参与本 mod 的溢出折现。
            int resourceCount = Math.Max(0, GlobalData.ResourceName.Count - 1);
            var ids = new int[resourceCount];
            for (int i = 0; i < resourceCount; i++)
            {
                ids[i] = i;
            }

            _trackedResourceIds = ids;
        }

        private static int GetOverflowMoneyRatePercent(int resourceId)
        {
            return _config.GetOverflowMoneyRatePercent(resourceId);
        }

        internal static bool ShouldInspectResourceChange(int resourceId, float delta)
        {
            return delta > 0.0001f && IsTrackedResourceId(resourceId);
        }

        private static string BuildForceTrackedResourceSnapshot(ForceData force)
        {
            if (force == null || _trackedResourceIds.Length == 0)
            {
                return "(无可用资源快照)";
            }

            var parts = new List<string>(_trackedResourceIds.Length);
            for (int i = 0; i < _trackedResourceIds.Length; i++)
            {
                int resourceId = _trackedResourceIds[i];
                if (!TryGetListValue(force.resourceStore, resourceId, out float storedValue) ||
                    !TryGetListValue(force.resourceStoreMax, resourceId, out float maxValue))
                {
                    continue;
                }

                parts.Add($"{GetResourceName(resourceId)}={storedValue:0.##}/{maxValue:0.##}");
            }

            return parts.Count > 0 ? string.Join("，", parts) : "(无可用资源快照)";
        }

        private static Dictionary<int, float> CaptureTrackedResourceValues(ForceData force)
        {
            var result = new Dictionary<int, float>();
            if (force?.resourceStore == null)
            {
                return result;
            }

            for (int i = 0; i < _trackedResourceIds.Length; i++)
            {
                int resourceId = _trackedResourceIds[i];
                if (TryGetListValue(force.resourceStore, resourceId, out float value))
                {
                    result[resourceId] = value;
                }
            }

            return result;
        }

        private static bool IsTrackedResourceId(int resourceId)
        {
            RefreshTrackedResourceIds();
            for (int i = 0; i < _trackedResourceIds.Length; i++)
            {
                if (_trackedResourceIds[i] == resourceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetListValue(Il2CppSystem.Collections.Generic.List<float> values, int index, out float value)
        {
            value = 0f;
            if (values == null || index < 0 || index >= values.Count)
            {
                return false;
            }

            value = values[index];
            return true;
        }

        private static bool IsPlayerForce(WorldData worldData, ForceData force)
        {
            HeroData player = worldData?.Player();
            return player != null && force != null && player.belongForceID == force.forceID;
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

        private static string SafeForceName(ForceData force)
        {
            try
            {
                return force?.GetForceName() ?? "(未知门派)";
            }
            catch
            {
                return "(未知门派)";
            }
        }
    }

    internal sealed class ResourceChangeContext
    {
        internal string TriggerText { get; init; } = string.Empty;

        internal Dictionary<int, float> BeforeValues { get; init; } = new();

        internal Dictionary<int, float> PositiveDeltas { get; } = new();
    }

    internal sealed class MemberDistribution
    {
        internal HeroData Hero { get; init; }

        internal int Weight { get; init; }

        internal int Payout { get; set; }

        internal double Remainder { get; init; }
    }

    [HarmonyPatch(typeof(ForceData), nameof(ForceData.ChangeResource), new[] { typeof(int), typeof(float), typeof(bool), typeof(bool) })]
    internal static class ForceData_ChangeResource_Int_Patch
    {
        private static void Prefix(ForceData __instance, int id, float num, ref ResourceChangeContext __state)
        {
            if (ForceOverflowRuntime.ShouldInspectResourceChange(id, num))
            {
                __state = ForceOverflowRuntime.CreateContext(__instance, $"单项资源变化：resourceId={id}，delta={num:0.##}");
                ForceOverflowRuntime.RecordPositiveDelta(__state, id, num);
            }
        }

        private static void Postfix(ForceData __instance, ResourceChangeContext __state)
        {
            try
            {
                ForceOverflowRuntime.HandleForceResourceChanged(__instance, __state);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"处理资源变动后的门派资源溢出折现失败：{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ForceData), nameof(ForceData.ChangeResource), new[] { typeof(Il2CppSystem.Collections.Generic.List<float>), typeof(bool), typeof(bool) })]
    internal static class ForceData_ChangeResource_ListFloat_Patch
    {
        private static void Prefix(ForceData __instance, Il2CppSystem.Collections.Generic.List<float> resourceList, ref ResourceChangeContext __state)
        {
            if (resourceList == null)
            {
                return;
            }

            ResourceChangeContext context = ForceOverflowRuntime.CreateContext(__instance, "批量资源变化：List<float>");
            if (context == null)
            {
                return;
            }

            for (int i = 0; i < resourceList.Count; i++)
            {
                ForceOverflowRuntime.RecordPositiveDelta(context, i, resourceList[i]);
            }

            if (context.PositiveDeltas.Count > 0)
            {
                __state = context;
            }
        }

        private static void Postfix(ForceData __instance, ResourceChangeContext __state)
        {
            try
            {
                ForceOverflowRuntime.HandleForceResourceChanged(__instance, __state);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"处理批量资源变动后的门派资源溢出折现失败：{ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ForceData), nameof(ForceData.ChangeResource), new[] { typeof(Il2CppSystem.Collections.Generic.List<ResourceData>), typeof(bool), typeof(bool) })]
    internal static class ForceData_ChangeResource_ListResourceData_Patch
    {
        private static void Prefix(ForceData __instance, Il2CppSystem.Collections.Generic.List<ResourceData> resourceList, ref ResourceChangeContext __state)
        {
            if (resourceList == null)
            {
                return;
            }

            ResourceChangeContext context = ForceOverflowRuntime.CreateContext(__instance, "批量资源变化：List<ResourceData>");
            if (context == null)
            {
                return;
            }

            for (int i = 0; i < resourceList.Count; i++)
            {
                ResourceData resource = resourceList[i];
                if (resource != null)
                {
                    ForceOverflowRuntime.RecordPositiveDelta(context, resource.resourceType, resource.resourceNum);
                }
            }

            if (context.PositiveDeltas.Count > 0)
            {
                __state = context;
            }
        }

        private static void Postfix(ForceData __instance, ResourceChangeContext __state)
        {
            try
            {
                ForceOverflowRuntime.HandleForceResourceChanged(__instance, __state);
            }
            catch (Exception ex)
            {
                Main.Log.Error($"处理资源对象列表变动后的门派资源溢出折现失败：{ex}");
            }
        }
    }
}
