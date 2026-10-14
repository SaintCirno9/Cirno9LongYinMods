using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;

namespace NpcHpProbe
{
    internal sealed class HeroSnapshot
    {
        internal HeroSnapshot(HeroData hero)
        {
            HeroId = hero.heroID;
            HeroName = hero.heroName ?? "<null>";
            Hp = hero.hp;
            MaxHp = hero.maxhp;
            RealMaxHp = hero.realMaxHp;
            BelongForceId = hero.belongForceID;
            HeroForceLv = hero.heroForceLv;
            AreaId = hero.atAreaID;
            Dead = hero.dead;
            Hide = hero.hide;
            Rest = hero.rest;
            CureType = hero.cureType;
            ManageAiHour = hero.manageAiHour;
            DailyAiManaged = hero.dailyAIManaged;
            IsLeader = hero.isLeader;
            ForceJobType = hero.forceJobType;
            ForceJobId = hero.forceJobID;
            ForceName = ProbeRuntime.SafeGetForceName(hero);

            HeroAIData aiData = hero.heroAIData;
            if (aiData != null)
            {
                AiStuffType = aiData.aiStuffType.ToString();
                AiStuffTarget = aiData.aiStuffTarget ?? "<null>";
                AiPassiveTarget = aiData.isPassiveTarget;
                AiKeepWorkingTimeLeft = aiData.keepWorkingTimeLeft;
                AiBigMapTargetId = aiData.bigMapTargetID;
            }
            else
            {
                AiStuffType = "<null>";
                AiStuffTarget = "<null>";
                AiPassiveTarget = false;
                AiKeepWorkingTimeLeft = -1;
                AiBigMapTargetId = -1;
            }
        }

        internal int HeroId { get; }
        internal string HeroName { get; }
        internal float Hp { get; }
        internal float MaxHp { get; }
        internal float RealMaxHp { get; }
        internal int BelongForceId { get; }
        internal int HeroForceLv { get; }
        internal int AreaId { get; }
        internal bool Dead { get; }
        internal bool Hide { get; }
        internal bool Rest { get; }
        internal int CureType { get; }
        internal float ManageAiHour { get; }
        internal bool DailyAiManaged { get; }
        internal bool IsLeader { get; }
        internal int ForceJobType { get; }
        internal int ForceJobId { get; }
        internal string ForceName { get; }
        internal string AiStuffType { get; }
        internal string AiStuffTarget { get; }
        internal bool AiPassiveTarget { get; }
        internal int AiKeepWorkingTimeLeft { get; }
        internal int AiBigMapTargetId { get; }
    }

    internal static class ProbeRuntime
    {
        [ThreadStatic]
        private static int _recoverFlowDepth;

        internal static bool InRecoverFlow => _recoverFlowDepth > 0;

        internal static void EnterRecoverFlow()
        {
            _recoverFlowDepth++;
        }

        internal static void ExitRecoverFlow()
        {
            if (_recoverFlowDepth > 0)
            {
                _recoverFlowDepth--;
            }
        }

        internal static bool ShouldTrack(HeroData hero)
        {
            if (hero == null)
            {
                return false;
            }

            if (NpcHpProbeConfig.TrackPlayer.Value)
            {
                return true;
            }

            return hero.heroID != 0;
        }

        internal static HeroSnapshot Capture(HeroData hero)
        {
            return new HeroSnapshot(hero);
        }

        internal static bool ShouldLogVerboseFlow(HeroData hero)
        {
            return NpcHpProbeConfig.VerboseLog.Value && ShouldTrack(hero);
        }

        internal static bool ShouldCaptureAiException(HeroData hero)
        {
            return NpcHpProbeConfig.LogAiExceptions.Value && ShouldTrack(hero);
        }

        internal static string SafeGetForceName(HeroData hero)
        {
            if (hero == null)
            {
                return "<null>";
            }

            if (hero.belongForceID < 0)
            {
                return "<none>";
            }

            try
            {
                ForceData force = hero.GetForce();
                return force?.forceName ?? "<null>";
            }
            catch (Exception ex)
            {
                return $"<error:{ex.GetType().Name}>";
            }
        }

        internal static bool IsBroken(HeroSnapshot snapshot)
        {
            if (float.IsNaN(snapshot.Hp) || float.IsNaN(snapshot.MaxHp) || float.IsNaN(snapshot.RealMaxHp))
            {
                return true;
            }

            if (float.IsInfinity(snapshot.Hp) || float.IsInfinity(snapshot.MaxHp) || float.IsInfinity(snapshot.RealMaxHp))
            {
                return true;
            }

            return snapshot.Hp <= 0f || snapshot.MaxHp <= 0f || snapshot.RealMaxHp <= 0f;
        }

        internal static bool IsZeroHp(HeroSnapshot snapshot)
        {
            return snapshot.Hp <= 0f;
        }

        internal static bool IsBelowRecoverThreshold(HeroSnapshot snapshot)
        {
            if (!NpcHpProbeConfig.TrackUnhealedHeroes.Value)
            {
                return false;
            }

            if (snapshot.RealMaxHp <= 0f)
            {
                return true;
            }

            float ratio = snapshot.Hp / snapshot.RealMaxHp;
            return ratio <= NpcHpProbeConfig.UnhealedHpRatioThreshold.Value;
        }

        internal static bool MissingHp(HeroSnapshot snapshot)
        {
            return snapshot.RealMaxHp > 0f && snapshot.Hp < snapshot.RealMaxHp - 0.01f;
        }

        internal static bool HpIncreased(HeroSnapshot before, HeroSnapshot after)
        {
            return after.Hp > before.Hp + 0.01f;
        }

        internal static bool Changed(HeroSnapshot before, HeroSnapshot after)
        {
            return before.Hp != after.Hp
                || before.MaxHp != after.MaxHp
                || before.RealMaxHp != after.RealMaxHp
                || before.Hide != after.Hide
                || before.Rest != after.Rest
                || before.CureType != after.CureType
                || before.BelongForceId != after.BelongForceId
                || before.AreaId != after.AreaId;
        }

        internal static string Format(HeroSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "<null hero>";
            }

            return
                $"heroID={snapshot.HeroId}, heroName={snapshot.HeroName}, " +
                $"hp={FormatFloat(snapshot.Hp)}, maxhp={FormatFloat(snapshot.MaxHp)}, realMaxHp={FormatFloat(snapshot.RealMaxHp)}, " +
                $"belongForceID={snapshot.BelongForceId}, forceName={snapshot.ForceName}, heroForceLv={snapshot.HeroForceLv}, " +
                $"forceJobType={snapshot.ForceJobType}, forceJobID={snapshot.ForceJobId}, isLeader={snapshot.IsLeader}, " +
                $"atAreaID={snapshot.AreaId}, manageAiHour={FormatFloat(snapshot.ManageAiHour)}, dailyAIManaged={snapshot.DailyAiManaged}, " +
                $"dead={snapshot.Dead}, hide={snapshot.Hide}, rest={snapshot.Rest}, cureType={snapshot.CureType}, " +
                $"aiStuffType={snapshot.AiStuffType}, aiStuffTarget={snapshot.AiStuffTarget}, aiPassiveTarget={snapshot.AiPassiveTarget}, " +
                $"aiKeepWorkingTimeLeft={snapshot.AiKeepWorkingTimeLeft}, aiBigMapTargetID={snapshot.AiBigMapTargetId}";
        }

        internal static string FormatException(Exception exception)
        {
            if (exception == null)
            {
                return "exception=<null>";
            }

            string message = string.IsNullOrWhiteSpace(exception.Message) ? "<empty>" : exception.Message.Replace('\r', ' ').Replace('\n', ' ');
            return $"exceptionType={exception.GetType().FullName}, exceptionMessage={message}";
        }

        internal static void LogWarning(string title, HeroSnapshot before, HeroSnapshot after, string detail = null, bool includeStackTrace = false)
        {
            var builder = new StringBuilder();
            builder.Append(title);
            builder.Append(" | after: ");
            builder.Append(Format(after));

            if (before != null)
            {
                builder.Append(" | before: ");
                builder.Append(Format(before));
            }

            if (!string.IsNullOrEmpty(detail))
            {
                builder.Append(" | ");
                builder.Append(detail);
            }

            Main.Log.Warning(builder.ToString());

            if (includeStackTrace && NpcHpProbeConfig.CaptureStackTraceOnAnomaly.Value)
            {
                Main.Log.Warning(Environment.StackTrace);
            }
        }

        internal static void LogVerbose(string title, HeroSnapshot before, HeroSnapshot after, string detail = null)
        {
            if (!NpcHpProbeConfig.VerboseLog.Value)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append(title);
            builder.Append(" | after: ");
            builder.Append(Format(after));

            if (before != null)
            {
                builder.Append(" | before: ");
                builder.Append(Format(before));
            }

            if (!string.IsNullOrEmpty(detail))
            {
                builder.Append(" | ");
                builder.Append(detail);
            }

            Main.Log.Msg(builder.ToString());
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
            {
                return "NaN";
            }

            if (float.IsPositiveInfinity(value))
            {
                return "+Inf";
            }

            if (float.IsNegativeInfinity(value))
            {
                return "-Inf";
            }

            return value.ToString("0.###");
        }
    }

    internal sealed class CountHeroDataState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.CountHeroData), new[] { typeof(HeroData) })]
    internal static class CountHeroDataPatch
    {
        private static void Prefix(HeroData hero, out CountHeroDataState __state)
        {
            if (!NpcHpProbeConfig.LogCountHeroData.Value || !ProbeRuntime.ShouldLogVerboseFlow(hero))
            {
                __state = new CountHeroDataState();
                return;
            }

            __state = new CountHeroDataState
            {
                Active = true,
                Before = ProbeRuntime.Capture(hero)
            };
        }

        private static void Postfix(HeroData hero, CountHeroDataState __state)
        {
            if (!__state.Active || hero == null)
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(hero);
            if (ProbeRuntime.IsBroken(after))
            {
                ProbeRuntime.LogWarning("CountHeroData 后发现异常角色数值", __state.Before, after, includeStackTrace: true);
                return;
            }

            if (ProbeRuntime.Changed(__state.Before, after))
            {
                ProbeRuntime.LogVerbose("CountHeroData 完成", __state.Before, after);
            }
        }
    }

    internal sealed class RecoverState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
        internal bool Rest { get; init; }
        internal bool NeedRefresh { get; init; }
        internal float ExtraRate { get; init; }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.ManageHeroAutoRecover), new[] { typeof(HeroData), typeof(bool), typeof(bool), typeof(float) })]
    internal static class ManageHeroAutoRecoverPatch
    {
        private static void Prefix(HeroData hero, bool rest, bool needRefresh, float extraRate, out RecoverState __state)
        {
            ProbeRuntime.EnterRecoverFlow();

            if (!NpcHpProbeConfig.LogRecoverFlow.Value || !ProbeRuntime.ShouldLogVerboseFlow(hero))
            {
                __state = new RecoverState();
                return;
            }

            __state = new RecoverState
            {
                Active = true,
                Before = ProbeRuntime.Capture(hero),
                Rest = rest,
                NeedRefresh = needRefresh,
                ExtraRate = extraRate
            };
        }

        private static void Postfix(HeroData hero, RecoverState __state)
        {
            if (!__state.Active || hero == null)
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(hero);
            string detail = $"rest={__state.Rest}, needRefresh={__state.NeedRefresh}, extraRate={__state.ExtraRate:0.###}";

            if (ProbeRuntime.IsBroken(after))
            {
                ProbeRuntime.LogWarning("ManageHeroAutoRecover 后发现异常角色数值", __state.Before, after, detail, includeStackTrace: true);
                return;
            }

            if (ProbeRuntime.IsBelowRecoverThreshold(__state.Before)
                && ProbeRuntime.MissingHp(__state.Before)
                && !ProbeRuntime.HpIncreased(__state.Before, after))
            {
                ProbeRuntime.LogWarning("ManageHeroAutoRecover 执行后未回血", __state.Before, after, detail, includeStackTrace: true);
                return;
            }

            if (ProbeRuntime.Changed(__state.Before, after))
            {
                ProbeRuntime.LogVerbose("ManageHeroAutoRecover 完成", __state.Before, after, detail);
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            ProbeRuntime.ExitRecoverFlow();

            if (__exception != null && NpcHpProbeConfig.VerboseLog.Value && NpcHpProbeConfig.LogRecoverFlow.Value)
            {
                Main.Log.Warning($"ManageHeroAutoRecover 抛出异常：{__exception}");
            }

            return __exception;
        }
    }

    internal sealed class AiState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
        internal bool ConsiderAiHour { get; init; }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.ManageOneAI), new[] { typeof(HeroData), typeof(bool) })]
    internal static class ManageOneAiPatch
    {
        private static void Prefix(HeroData heroData, bool considerAIHour, out AiState __state)
        {
            if (!NpcHpProbeConfig.LogAiFlow.Value || !ProbeRuntime.ShouldLogVerboseFlow(heroData))
            {
                __state = new AiState();
                return;
            }

            __state = new AiState
            {
                Active = true,
                Before = ProbeRuntime.Capture(heroData),
                ConsiderAiHour = considerAIHour
            };

            if (ProbeRuntime.IsBelowRecoverThreshold(__state.Before))
            {
                ProbeRuntime.LogWarning(
                    "残血 NPC 进入 ManageOneAI",
                    __state.Before,
                    __state.Before,
                    $"considerAIHour={considerAIHour}");
            }
        }

        private static void Postfix(HeroData heroData, AiState __state)
        {
            if (!__state.Active || heroData == null)
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(heroData);
            string detail = $"considerAIHour={__state.ConsiderAiHour}";

            if (ProbeRuntime.IsBelowRecoverThreshold(__state.Before)
                && ProbeRuntime.MissingHp(__state.Before)
                && !ProbeRuntime.HpIncreased(__state.Before, after))
            {
                ProbeRuntime.LogWarning("残血 NPC 离开 ManageOneAI 后仍未恢复", __state.Before, after, detail);
                return;
            }

            if (ProbeRuntime.Changed(__state.Before, after))
            {
                ProbeRuntime.LogVerbose("ManageOneAI 完成", __state.Before, after, detail);
            }
        }
    }

    [HarmonyPatch(typeof(AIController), nameof(AIController.ManageAIOneDay), new[] { typeof(HeroData) })]
    internal static class ManageAiOneDayPatch
    {
        private static void Prefix(HeroData hero, out AiState __state)
        {
            bool captureException = ProbeRuntime.ShouldCaptureAiException(hero);
            bool logVerboseFlow = NpcHpProbeConfig.LogAiFlow.Value && ProbeRuntime.ShouldLogVerboseFlow(hero);
            if (!captureException && !logVerboseFlow)
            {
                __state = new AiState();
                return;
            }

            __state = new AiState
            {
                Active = true,
                Before = ProbeRuntime.Capture(hero),
                ConsiderAiHour = false
            };

            if (logVerboseFlow && ProbeRuntime.IsBelowRecoverThreshold(__state.Before))
            {
                ProbeRuntime.LogWarning("残血 NPC 进入 ManageAIOneDay", __state.Before, __state.Before);
            }
        }

        private static void Postfix(HeroData hero, AiState __state)
        {
            if (!__state.Active || hero == null || !NpcHpProbeConfig.LogAiFlow.Value || !ProbeRuntime.ShouldLogVerboseFlow(hero))
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(hero);
            if (ProbeRuntime.IsBelowRecoverThreshold(__state.Before)
                && ProbeRuntime.MissingHp(__state.Before)
                && !ProbeRuntime.HpIncreased(__state.Before, after))
            {
                ProbeRuntime.LogWarning("残血 NPC 离开 ManageAIOneDay 后仍未恢复", __state.Before, after);
                return;
            }

            if (ProbeRuntime.Changed(__state.Before, after))
            {
                ProbeRuntime.LogVerbose("ManageAIOneDay 完成", __state.Before, after);
            }
        }

        private static Exception Finalizer(HeroData hero, AiState __state, Exception __exception)
        {
            if (__exception == null || !NpcHpProbeConfig.LogAiExceptions.Value)
            {
                return null;
            }

            HeroSnapshot before = __state?.Before;
            HeroSnapshot after = hero != null && ProbeRuntime.ShouldTrack(hero) ? ProbeRuntime.Capture(hero) : before;
            ProbeRuntime.LogWarning(
                "ManageAIOneDay 抛出异常",
                before,
                after,
                ProbeRuntime.FormatException(__exception),
                includeStackTrace: true);
            return __exception;
        }
    }

    internal sealed class FinishAiStuffState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
    }

    [HarmonyPatch(typeof(AIController), nameof(AIController.FinishAIStuff), new[] { typeof(HeroData) })]
    internal static class FinishAiStuffPatch
    {
        private static void Prefix(HeroData hero, out FinishAiStuffState __state)
        {
            bool captureException = ProbeRuntime.ShouldCaptureAiException(hero);
            bool logVerboseFlow = NpcHpProbeConfig.LogAiFlow.Value && ProbeRuntime.ShouldLogVerboseFlow(hero);
            if (!captureException && !logVerboseFlow)
            {
                __state = new FinishAiStuffState();
                return;
            }

            __state = new FinishAiStuffState
            {
                Active = true,
                Before = ProbeRuntime.Capture(hero)
            };
        }

        private static void Postfix(HeroData hero, FinishAiStuffState __state, bool __result)
        {
            if (!__state.Active || hero == null || !NpcHpProbeConfig.LogAiFlow.Value || !ProbeRuntime.ShouldLogVerboseFlow(hero))
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(hero);
            ProbeRuntime.LogVerbose("FinishAIStuff 完成", __state.Before, after, $"result={__result}");
        }

        private static Exception Finalizer(HeroData hero, FinishAiStuffState __state, Exception __exception)
        {
            if (__exception == null || !NpcHpProbeConfig.LogAiExceptions.Value)
            {
                return null;
            }

            HeroSnapshot before = __state?.Before;
            HeroSnapshot after = hero != null && ProbeRuntime.ShouldTrack(hero) ? ProbeRuntime.Capture(hero) : before;
            ProbeRuntime.LogWarning(
                "FinishAIStuff 抛出异常",
                before,
                after,
                ProbeRuntime.FormatException(__exception),
                includeStackTrace: true);
            return __exception;
        }
    }

    internal sealed class RecoverRateState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
        internal float BaseRecoverRate { get; init; }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetRecoverRate), new[] { typeof(float) })]
    internal static class HeroDataGetRecoverRatePatch
    {
        private static void Prefix(HeroData __instance, float baseRecoverRate, out RecoverRateState __state)
        {
            if (!NpcHpProbeConfig.LogRecoverRate.Value || !ProbeRuntime.ShouldLogVerboseFlow(__instance))
            {
                __state = new RecoverRateState();
                return;
            }

            __state = new RecoverRateState
            {
                Active = true,
                Before = ProbeRuntime.Capture(__instance),
                BaseRecoverRate = baseRecoverRate
            };
        }

        private static void Postfix(HeroData __instance, float __result, RecoverRateState __state)
        {
            if (!__state.Active || __instance == null)
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(__instance);
            string detail = $"baseRecoverRate={__state.BaseRecoverRate:0.######}, result={__result:0.######}";

            if (ProbeRuntime.IsBelowRecoverThreshold(__state.Before) && __result <= 0f)
            {
                ProbeRuntime.LogWarning("残血 NPC 计算出的恢复率非正", __state.Before, after, detail, includeStackTrace: true);
                return;
            }

            if (NpcHpProbeConfig.VerboseLog.Value && ProbeRuntime.IsBelowRecoverThreshold(__state.Before))
            {
                ProbeRuntime.LogVerbose("残血 NPC 计算恢复率", __state.Before, after, detail);
            }
        }
    }

    internal sealed class ForceLvDescribeState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
        internal bool FullName { get; init; }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetHeroForceLvDescribe), new[] { typeof(bool) })]
    internal static class HeroDataGetHeroForceLvDescribePatch
    {
        private static void Prefix(HeroData __instance, bool fullName, out ForceLvDescribeState __state)
        {
            bool captureException = ProbeRuntime.ShouldCaptureAiException(__instance);
            bool logVerboseFlow = NpcHpProbeConfig.LogAiFlow.Value && ProbeRuntime.ShouldLogVerboseFlow(__instance);
            if (!captureException && !logVerboseFlow)
            {
                __state = new ForceLvDescribeState();
                return;
            }

            __state = new ForceLvDescribeState
            {
                Active = true,
                Before = ProbeRuntime.Capture(__instance),
                FullName = fullName
            };
        }

        private static void Postfix(HeroData __instance, string __result, ForceLvDescribeState __state)
        {
            if (!__state.Active || __instance == null || !NpcHpProbeConfig.LogAiFlow.Value || !ProbeRuntime.ShouldLogVerboseFlow(__instance))
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(__instance);
            ProbeRuntime.LogVerbose(
                "GetHeroForceLvDescribe 完成",
                __state.Before,
                after,
                $"fullName={__state.FullName}, result={__result ?? "<null>"}");
        }

        private static Exception Finalizer(HeroData __instance, ForceLvDescribeState __state, Exception __exception)
        {
            if (__exception == null || !NpcHpProbeConfig.LogAiExceptions.Value)
            {
                return null;
            }

            HeroSnapshot before = __state?.Before;
            HeroSnapshot after = __instance != null && ProbeRuntime.ShouldTrack(__instance) ? ProbeRuntime.Capture(__instance) : before;
            ProbeRuntime.LogWarning(
                "GetHeroForceLvDescribe 抛出异常",
                before,
                after,
                $"{ProbeRuntime.FormatException(__exception)}, fullName={__state?.FullName}",
                includeStackTrace: true);
            return __exception;
        }
    }

    internal sealed class ChangeHpState
    {
        internal bool Active { get; init; }
        internal HeroSnapshot Before { get; init; }
        internal float Num { get; init; }
        internal bool UseRecoverRate { get; init; }
        internal bool NoDead { get; init; }
        internal bool NeedRefresh { get; init; }
        internal bool ShowInfo { get; init; }
        internal bool FromRecoverFlow { get; init; }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeHp), new[] { typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class HeroDataChangeHpPatch
    {
        private static void Prefix(
            HeroData __instance,
            float num,
            bool useRecoverRate,
            bool noDead,
            bool needRefresh,
            bool showInfo,
            out ChangeHpState __state)
        {
            if (!NpcHpProbeConfig.LogChangeHp.Value || !ProbeRuntime.ShouldLogVerboseFlow(__instance))
            {
                __state = new ChangeHpState();
                return;
            }

            __state = new ChangeHpState
            {
                Active = true,
                Before = ProbeRuntime.Capture(__instance),
                Num = num,
                UseRecoverRate = useRecoverRate,
                NoDead = noDead,
                NeedRefresh = needRefresh,
                ShowInfo = showInfo,
                FromRecoverFlow = ProbeRuntime.InRecoverFlow
            };
        }

        private static void Postfix(HeroData __instance, ChangeHpState __state)
        {
            if (!__state.Active || __instance == null)
            {
                return;
            }

            HeroSnapshot after = ProbeRuntime.Capture(__instance);
            bool droppedToZero = __state.Before.Hp > 0f && after.Hp <= 0f;
            string detail =
                $"num={__state.Num:0.###}, useRecoverRate={__state.UseRecoverRate}, noDead={__state.NoDead}, " +
                $"needRefresh={__state.NeedRefresh}, showInfo={__state.ShowInfo}, fromRecoverFlow={__state.FromRecoverFlow}";

            if (ProbeRuntime.IsBroken(after) || droppedToZero)
            {
                ProbeRuntime.LogWarning("HeroData.ChangeHp 后发现异常角色数值", __state.Before, after, detail, includeStackTrace: true);
                return;
            }

            if (__state.FromRecoverFlow || ProbeRuntime.Changed(__state.Before, after))
            {
                ProbeRuntime.LogVerbose("HeroData.ChangeHp 完成", __state.Before, after, detail);
            }
        }
    }
}
