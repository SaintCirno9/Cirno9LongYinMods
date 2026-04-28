using HarmonyLib;
using Il2Cpp;
using Il2CppCollections = Il2CppSystem.Collections.Generic;
using MelonLoader;
using System;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(DualCultivation.Main), "DualCultivation", "1.1.2", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace DualCultivation
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            DualCultivationConfig.Initialize();

            Log.Msg("DualCultivation 已加载。");
            Log.Msg($"配置文件：{DualCultivationConfig.ConfigFilePath}");
            Log.Msg(
                $"启用：{DualCultivationConfig.EnableMod.Value}, " +
                $"好感要求：{DualCultivationConfig.RequiredFavor.Value}, " +
                $"每月次数：{DualCultivationConfig.MonthlyLimit.Value}, " +
                $"关系要求：{DualCultivationConfig.RelationRequirement.Value}");
        }
    }

    internal static class DualCultivationFlow
    {
        internal const string StartMarker = "DualCultivation.Start";
        internal const string ConfirmMarker = "DualCultivation.Confirm";
        internal const string CancelMarker = "DualCultivation.Cancel";

        private const string WorkName = "双修";
        private const string MonthlyCooldownText = "（本月已双修）";
        private const string AvailableHintText = "与对方双修一日，提升双方生命与内力上限";

        private static PendingSession? _pendingSession;
        private static SinglePlotData _returnPlot;
        private static bool _dualCultivationWorking;
        private static readonly Random Random = new();

        internal static void TryAddStartChoice(PlotController controller)
        {
            if (!DualCultivationConfig.EnableMod.Value || controller == null)
            {
                return;
            }

            SinglePlotData currentPlot = controller.nowSinglePlot;
            Il2CppCollections.List<SinglePlotChoiceData> choices = currentPlot?.choices;
            if (choices == null)
            {
                return;
            }

            if (!IsStudyMenu(choices))
            {
                return;
            }

            HeroData player = ResolvePlayer(controller);
            HeroData target = controller.targetInteractHero;
            if (!CanShowStartChoice(player, target))
            {
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                SinglePlotChoiceData choice = choices[i];
                if (choice != null && choice.callFuc == StartMarker)
                {
                    return;
                }
            }

            SinglePlotChoiceData startChoice = CreateChoice(DualCultivationConfig.StartChoiceText.Value, StartMarker);
            if (MonthlyLimitReached(player, target))
            {
                startChoice.describe = MonthlyCooldownText;
            }

            int insertIndex = choices.Count > 0 ? choices.Count - 1 : choices.Count;
            choices.Insert(insertIndex, startChoice);
            currentPlot.choices = choices;

            VerboseLog($"已为 {target.heroName} 添加双修入口。");
        }

        private static bool IsStudyMenu(Il2CppCollections.List<SinglePlotChoiceData> choices)
        {
            bool hasStudyFight = false;
            bool hasStudySkill = false;
            bool hasStudyLivingSkill = false;
            bool hasStudyNewSkill = false;

            for (int i = 0; i < choices.Count; i++)
            {
                SinglePlotChoiceData choice = choices[i];
                if (choice == null)
                {
                    continue;
                }

                hasStudyFight |= choice.callFuc == nameof(PlotController.StudyFightWithNPC);
                hasStudySkill |= choice.callFuc == nameof(PlotController.StudySkillWithNPC);
                hasStudyLivingSkill |= choice.callFuc == nameof(PlotController.ChooseStudyLivingSkillWithNPC) ||
                    choice.callFuc == nameof(PlotController.StudyLivingSkillWithNPC);
                hasStudyNewSkill |= choice.callFuc == nameof(PlotController.StudyNewSkillWithNPC);
            }

            return hasStudyFight && hasStudySkill && hasStudyLivingSkill && hasStudyNewSkill;
        }

        internal static bool HandleChoice(SinglePlotChoiceData choiceData)
        {
            if (choiceData == null)
            {
                return true;
            }

            if (choiceData.callFuc == StartMarker)
            {
                ShowConfirmPlot(PlotController.Instance);
                return false;
            }

            if (choiceData.callFuc == ConfirmMarker)
            {
                StartDualCultivationWork(PlotController.Instance);
                return false;
            }

            if (choiceData.callFuc == CancelMarker)
            {
                ReturnToStudyMenu(PlotController.Instance);
                return false;
            }

            return true;
        }

        internal static void RefreshStartChoiceState(PlotInteractController controller)
        {
            if (controller?.choiceData == null || controller.choiceData.callFuc != StartMarker)
            {
                return;
            }

            HeroData player = ResolvePlayer(PlotController.Instance);
            HeroData target = PlotController.Instance?.targetInteractHero;
            if (!MonthlyLimitReached(player, target))
            {
                controller.choiceData.describe = AvailableHintText;
                SetStartChoiceSubText(controller, string.Empty);
                return;
            }

            controller.choiceData.describe = MonthlyCooldownText;
            Button button = controller.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
            }

            SetStartChoiceSubText(controller, MonthlyCooldownText);
        }

        internal static void TrackWorkState(WorkingUIController controller)
        {
            if (controller == null || _pendingSession == null)
            {
                _dualCultivationWorking = false;
                return;
            }

            if (controller.working && controller.workName == WorkName)
            {
                _dualCultivationWorking = true;
                return;
            }

            if (_dualCultivationWorking && !controller.working)
            {
                FinishDualCultivationWork();
            }
        }

        internal static void FinishDualCultivationWork()
        {
            if (_pendingSession == null)
            {
                return;
            }

            PendingSession session = _pendingSession.Value;
            _pendingSession = null;
            _dualCultivationWorking = false;

            WorldData worldData = GameController.Instance?.worldData;
            HeroData player = worldData?.GetHero(session.PlayerId);
            HeroData target = worldData?.GetHero(session.TargetId);
            if (player == null || target == null)
            {
                return;
            }

            player.ChangeMaxHp(session.PlayerHpGain, true);
            player.ChangeMaxMana(session.PlayerManaGain, true);
            target.ChangeMaxHp(session.TargetHpGain, true);
            target.ChangeMaxMana(session.TargetManaGain, true);
            PlotController.Instance?.PlotChangeHeroFavor(target, DualCultivationConfig.FavorGain.Value, 100f, 0f, true);

            ShowResultPlot(target);
            VerboseLog($"双修完成：player={session.PlayerId}, target={session.TargetId}, hp={session.PlayerHpGain}, mana={session.PlayerManaGain}");
        }

        private static void ShowConfirmPlot(PlotController controller)
        {
            HeroData player = ResolvePlayer(controller);
            HeroData target = controller?.targetInteractHero;
            if (!CanStart(player, target))
            {
                return;
            }

            RewardPreview preview = CalculateReward(player, target);
            _returnPlot = controller.nowSinglePlot;
            Il2CppCollections.List<SinglePlotChoiceData> choices = new();
            choices.Add(CreateChoice(DualCultivationConfig.ConfirmChoiceText.Value, ConfirmMarker));
            choices.Add(CreateChoice(DualCultivationConfig.CancelChoiceText.Value, CancelMarker));

            string prompt = FormatText(ChooseText(DualCultivationConfig.PromptText.Value), player, target, preview);
            string rewardPreview = FormatText(DualCultivationConfig.RewardPreviewText.Value, player, target, preview);
            SinglePlotData plot = new()
            {
                plotText = $"{prompt}\n{rewardPreview}",
                choices = choices,
                plotTarget = PlotTargetHeroType.Keep,
                backPic = DualCultivationConfig.BackPic.Value
            };

            controller.ChangePlot(plot);
        }

        private static void StartDualCultivationWork(PlotController controller)
        {
            HeroData player = ResolvePlayer(controller);
            HeroData target = controller?.targetInteractHero;
            if (!CanStart(player, target))
            {
                return;
            }

            RewardPreview preview = CalculateReward(player, target);
            _pendingSession = new PendingSession(
                player.heroID,
                target.heroID,
                preview.HpGain,
                preview.ManaGain,
                preview.TargetHpGain,
                preview.TargetManaGain);
            MarkCultivatedThisMonth(player, target);
            _returnPlot = null;

            controller?.HideInteractUIImmediate();
            WorkingUIController.Instance?.StartWorking(WorkName, DualCultivationConfig.WorkDays.Value, string.Empty, string.Empty, string.Empty, string.Empty, true);
            _dualCultivationWorking = true;
        }

        private static void ReturnToStudyMenu(PlotController controller)
        {
            SinglePlotData returnPlot = _returnPlot;
            _returnPlot = null;
            if (controller != null && returnPlot != null)
            {
                controller.ChangePlot(returnPlot);
                return;
            }

            controller?.HideInteractUI();
        }

        private static void ShowResultPlot(HeroData target)
        {
            HeroData player = GameController.Instance?.worldData?.Player();
            Il2CppCollections.List<SinglePlotChoiceData> choices = new();
            choices.Add(CreateChoice(DualCultivationConfig.ResultChoiceText.Value, "HideInteractUI"));

            SinglePlotData plot = new()
            {
                plotText = FormatText(ChooseText(DualCultivationConfig.ResultText.Value), player, target, default),
                choices = choices,
                plotSource = PlotTargetHeroType.HeroID,
                sourceName = player?.heroID.ToString() ?? string.Empty,
                plotTarget = PlotTargetHeroType.HeroID,
                targetName = target.heroID.ToString(),
                backPic = DualCultivationConfig.BackPic.Value
            };

            PlotController.Instance?.AddPlot(plot);
        }

        private static bool CanStart(HeroData player, HeroData target)
        {
            if (!CanShowStartChoice(player, target))
            {
                return false;
            }

            if (MonthlyLimitReached(player, target))
            {
                return false;
            }

            return true;
        }

        private static bool CanShowStartChoice(HeroData player, HeroData target)
        {
            if (player == null || target == null || player.heroID == target.heroID)
            {
                return false;
            }

            if (target.Favor(false) < DualCultivationConfig.RequiredFavor.Value)
            {
                return false;
            }

            if (!MeetRelationRequirement(player, target))
            {
                return false;
            }

            return true;
        }

        private static bool MonthlyLimitReached(HeroData player, HeroData target)
        {
            return GetMonthlyUseCount(player, target) >= DualCultivationConfig.MonthlyLimit.Value;
        }

        private static int GetMonthlyUseCount(HeroData player, HeroData target)
        {
            if (player == null || target == null)
            {
                return 0;
            }

            WorldData worldData = GameController.Instance?.worldData;
            TimeData worldTime = worldData?.worldTime;
            if (worldData?.plotHappened == null || worldTime == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < DualCultivationConfig.MonthlyLimit.Value; i++)
            {
                int recordKey = GetMonthlyRecordKey(player, target, i);
                if (!worldData.plotHappened.ContainsKey(recordKey))
                {
                    continue;
                }

                TimeData recordTime = worldData.plotHappened[recordKey];
                if (recordTime != null && recordTime.year == worldTime.year && recordTime.month == worldTime.month)
                {
                    count++;
                }
            }

            return count;
        }

        private static void MarkCultivatedThisMonth(HeroData player, HeroData target)
        {
            WorldData worldData = GameController.Instance?.worldData;
            TimeData worldTime = worldData?.worldTime;
            if (worldData?.plotHappened == null || worldTime == null)
            {
                return;
            }

            for (int i = 0; i < DualCultivationConfig.MonthlyLimit.Value; i++)
            {
                int recordKey = GetMonthlyRecordKey(player, target, i);
                if (!worldData.plotHappened.ContainsKey(recordKey))
                {
                    worldData.plotHappened[recordKey] = new TimeData(worldTime.year, worldTime.month, 1);
                    return;
                }

                TimeData recordTime = worldData.plotHappened[recordKey];
                if (recordTime == null || recordTime.year != worldTime.year || recordTime.month != worldTime.month)
                {
                    worldData.plotHappened[recordKey] = new TimeData(worldTime.year, worldTime.month, 1);
                    return;
                }
            }
        }

        private static int GetMonthlyRecordKey(HeroData player, HeroData target, int index)
        {
            return unchecked(((int)0xDCA00000 ^ (player.heroID * 397) ^ target.heroID) + index);
        }

        private static bool MeetRelationRequirement(HeroData player, HeroData target)
        {
            int relationRequirement = DualCultivationConfig.RelationRequirement.Value;
            if (relationRequirement == 0)
            {
                return true;
            }

            if (target.Lover == player.heroID || player.Lover == target.heroID)
            {
                return true;
            }

            return relationRequirement == 2 && (target.HavePrelover(player.heroID) || player.HavePrelover(target.heroID));
        }

        private static HeroData ResolvePlayer(PlotController controller)
        {
            return controller?.sourceInteractHero ?? GameController.Instance?.worldData?.Player();
        }

        private static SinglePlotChoiceData CreateChoice(string text, string callFuc)
        {
            return new SinglePlotChoiceData
            {
                choiceText = text,
                callFuc = callFuc,
                describe = callFuc == StartMarker ? AvailableHintText : string.Empty,
                requirements = new Il2CppCollections.List<PlotChoiceRequirement>(),
                relations = new Il2CppCollections.List<RelationRequirementType>(),
                costResource = new Il2CppCollections.List<ResourceData>()
            };
        }

        private static void SetStartChoiceSubText(PlotInteractController controller, string text)
        {
            Text[] texts = controller.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text targetText = texts[i];
                if (targetText != null && targetText.text != controller.choiceData.choiceText)
                {
                    targetText.text = text;
                    return;
                }
            }
        }

        private static RewardPreview CalculateReward(HeroData player, HeroData target)
        {
            float rate = (float)DualCultivationConfig.RewardRate.Value;
            return new RewardPreview(
                Math.Min(target.realMaxHp * rate, DualCultivationConfig.MaxHpGain.Value),
                Math.Min(target.realMaxMana * rate, DualCultivationConfig.MaxManaGain.Value),
                Math.Min(player.realMaxHp * rate, DualCultivationConfig.MaxHpGain.Value),
                Math.Min(player.realMaxMana * rate, DualCultivationConfig.MaxManaGain.Value));
        }

        private static string FormatText(string text, HeroData player, HeroData target, RewardPreview preview)
        {
            string displayName = GetTargetCallPlayerName(player, target);
            return text
                .Replace("{称呼}", displayName)
                .Replace("{HpGain}", preview.HpGain.ToString("0"))
                .Replace("{ManaGain}", preview.ManaGain.ToString("0"))
                .Replace("{TargetHpGain}", preview.TargetHpGain.ToString("0"))
                .Replace("{TargetManaGain}", preview.TargetManaGain.ToString("0"));
        }

        private static string ChooseText(string text)
        {
            string[] parts = text.Split('|');
            if (parts.Length == 0)
            {
                return text;
            }

            return parts[Random.Next(parts.Length)].Trim();
        }

        private static string GetTargetCallPlayerName(HeroData player, HeroData target)
        {
            if (player == null)
            {
                return "你";
            }

            if (target == null)
            {
                return player.heroName;
            }

            return GameController.Instance?.GetHeroName(target, player) ?? player.heroName;
        }

        private static void VerboseLog(string message)
        {
            if (DualCultivationConfig.VerboseLog.Value)
            {
                Main.Log.Msg(message);
            }
        }

        private readonly struct RewardPreview
        {
            internal RewardPreview(float hpGain, float manaGain, float targetHpGain, float targetManaGain)
            {
                HpGain = hpGain;
                ManaGain = manaGain;
                TargetHpGain = targetHpGain;
                TargetManaGain = targetManaGain;
            }

            internal float HpGain { get; }

            internal float ManaGain { get; }

            internal float TargetHpGain { get; }

            internal float TargetManaGain { get; }
        }

        private readonly struct PendingSession
        {
            internal PendingSession(int playerId, int targetId, float playerHpGain, float playerManaGain, float targetHpGain, float targetManaGain)
            {
                PlayerId = playerId;
                TargetId = targetId;
                PlayerHpGain = playerHpGain;
                PlayerManaGain = playerManaGain;
                TargetHpGain = targetHpGain;
                TargetManaGain = targetManaGain;
            }

            internal int PlayerId { get; }

            internal int TargetId { get; }

            internal float PlayerHpGain { get; }

            internal float PlayerManaGain { get; }

            internal float TargetHpGain { get; }

            internal float TargetManaGain { get; }
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.ChangePlot), typeof(string))]
    internal static class PlotController_ChangePlot_String_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(PlotController __instance)
        {
            DualCultivationFlow.TryAddStartChoice(__instance);
        }
    }

    [HarmonyPatch(typeof(PlotInteractController), nameof(PlotInteractController.OnClick))]
    internal static class PlotInteractController_OnClick_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(PlotInteractController __instance)
        {
            return DualCultivationFlow.HandleChoice(__instance?.choiceData);
        }
    }

    [HarmonyPatch(typeof(PlotInteractController), nameof(PlotInteractController.Update))]
    internal static class PlotInteractController_Update_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(PlotInteractController __instance)
        {
            DualCultivationFlow.RefreshStartChoiceState(__instance);
        }
    }

    [HarmonyPatch(typeof(WorkingUIController), nameof(WorkingUIController.Update))]
    internal static class WorkingUIController_Update_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(WorkingUIController __instance)
        {
            DualCultivationFlow.TrackWorkState(__instance);
        }
    }
}
