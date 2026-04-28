using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: MelonInfo(typeof(BattleSkillRuleTweaks.Main), "BattleSkillRuleTweaks", "1.3.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace BattleSkillRuleTweaks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        private const int BattleUnitAttackEndThisOffset = 0x20;
        private const int BattleControllerNowActiveUnitAttackedOffset = 0x121;
        internal static readonly MethodInfo GetAttackRangeGridsMethod = AccessTools.Method(typeof(BattleController), "GetAttackRangeGrids");

        public override void OnInitializeMelon()
        {
            PatchBattleUnitAttackEndMoveNext();
            PatchPlayBattleUnitMoveMoveNext();
            Log.Msg("BattleSkillRuleTweaks 已加载。");
        }

        private void PatchBattleUnitAttackEndMoveNext()
        {
            Type iteratorType = FindBattleUnitAttackEndIteratorType();
            MethodInfo moveNext = AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo postfix = AccessTools.Method(typeof(BattleController_BattleUnitAttackEnd_MoveNext_Patch), nameof(BattleController_BattleUnitAttackEnd_MoveNext_Patch.Postfix));

            if (iteratorType == null || moveNext == null || postfix == null)
            {
                Log.Error("未找到 BattleUnitAttackEnd 协程补丁入口。");
                LogIteratorTypes();
                return;
            }

            HarmonyInstance.Patch(moveNext, postfix: new HarmonyMethod(postfix));
        }

        private void PatchPlayBattleUnitMoveMoveNext()
        {
            Type iteratorType = FindBattleControllerIteratorType("PlayBattleUnitMove");
            MethodInfo moveNext = AccessTools.Method(iteratorType, "MoveNext");
            MethodInfo postfix = AccessTools.Method(typeof(BattleController_PlayBattleUnitMove_MoveNext_Patch), nameof(BattleController_PlayBattleUnitMove_MoveNext_Patch.Postfix));

            if (iteratorType == null || moveNext == null || postfix == null)
            {
                Log.Error("未找到 PlayBattleUnitMove 协程补丁入口。");
                LogIteratorTypes();
                return;
            }

            HarmonyInstance.Patch(moveNext, postfix: new HarmonyMethod(postfix));
        }

        private static Type FindBattleUnitAttackEndIteratorType()
        {
            return FindBattleControllerIteratorType("BattleUnitAttackEnd");
        }

        private static Type FindBattleControllerIteratorType(string name)
        {
            Type[] nestedTypes = typeof(BattleController).GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (Type nestedType in nestedTypes)
            {
                if (nestedType.Name.Contains(name))
                {
                    return nestedType;
                }
            }

            return null;
        }

        private static void LogIteratorTypes()
        {
            Type[] nestedTypes = typeof(BattleController).GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (Type nestedType in nestedTypes)
            {
                if (nestedType.Name.Contains("BattleUnitAttack") || nestedType.Name.Contains("FinishRound"))
                {
                    Log.Warning($"发现嵌套类型: {nestedType.FullName}");
                }
            }
        }

        internal static BattleController GetBattleUnitAttackEndController(object iteratorInstance)
        {
            if (iteratorInstance is not Il2CppObjectBase iteratorObject)
            {
                return null;
            }

            IntPtr controllerPtr = Marshal.ReadIntPtr(IntPtr.Add(iteratorObject.Pointer, BattleUnitAttackEndThisOffset));
            if (controllerPtr == IntPtr.Zero)
            {
                return null;
            }

            return new BattleController(controllerPtr);
        }

        internal static void SetNowActiveUnitAttacked(BattleController controller, bool value)
        {
            if (controller == null)
            {
                return;
            }

            Marshal.WriteByte(IntPtr.Add(controller.Pointer, BattleControllerNowActiveUnitAttackedOffset), value ? (byte)1 : (byte)0);
        }

        internal static bool GetNowActiveUnitAttacked(BattleController controller)
        {
            if (controller == null)
            {
                return false;
            }

            return Marshal.ReadByte(IntPtr.Add(controller.Pointer, BattleControllerNowActiveUnitAttackedOffset)) != 0;
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.FightReset))]
    internal static class HeroData_FightReset_Patch
    {
        private static void Postfix(HeroData __instance)
        {
            if (__instance == null)
            {
                return;
            }

            PrepareSkill(__instance.internalSkill);
            PrepareSkill(__instance.dodgeSkill);
            PrepareSkill(__instance.uniqueSkill);
        }

        private static void PrepareSkill(KungfuSkillLvData skill)
        {
            if (!BattleSkillUnlockHelper.IsSupportedSkill(skill))
            {
                return;
            }

            skill.cdTimeLeft = 0f;
            skill.activeTimeLeft = 0f;
            skill.useTime = 0;
            skill.power = skill.MaxPower();
            skill.skillIconDirty = true;
        }
    }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.CanUseSkill), new[] { typeof(KungfuSkillLvData) })]
    internal static class HeroData_CanUseSkill_Patch
    {
        private static void Postfix(HeroData __instance, KungfuSkillLvData targetSkill, ref bool __result)
        {
            if (__result || __instance == null || !BattleSkillUnlockHelper.IsSupportedSkill(targetSkill))
            {
                return;
            }

            if (targetSkill.useTime != 0)
            {
                return;
            }

            if (targetSkill.cdTimeLeft > 0f || targetSkill.activeTimeLeft > 0f)
            {
                return;
            }

            if (__instance.mana < targetSkill.GetManaCost())
            {
                return;
            }

            if (targetSkill.power < targetSkill.MaxPower())
            {
                return;
            }

            __result = true;
        }
    }

    internal static class BattleSkillUnlockHelper
    {
        internal static bool IsSupportedSkill(KungfuSkillLvData skill)
        {
            if (skill == null)
            {
                return false;
            }

            FightSkillType fightSkillType = (FightSkillType)skill.Type();
            return fightSkillType == FightSkillType.Internal ||
                   fightSkillType == FightSkillType.Dodge ||
                   fightSkillType == FightSkillType.Unique;
        }
    }

    internal static class BattleController_BattleUnitAttackEnd_MoveNext_Patch
    {
        public static void Postfix(object __instance, bool __result)
        {
            if (!__result || __instance == null)
            {
                return;
            }

            BattleController controller = Main.GetBattleUnitAttackEndController(__instance);
            if (controller == null || controller.nowActiveState != ActiveUnitBattleState.Finish)
            {
                return;
            }

            BattleUnit nowActiveUnit = controller.nowActiveUnit;
            if (nowActiveUnit == null)
            {
                return;
            }

            HeroData heroData = nowActiveUnit?.heroData;
            KungfuSkillLvData skill = heroData?.GetNowActiveSkill();
            if (!BattleSkillUnlockHelper.IsSupportedSkill(skill))
            {
                return;
            }

            if (!nowActiveUnit.playerControl)
            {
                TryContinueAiTurn(controller, nowActiveUnit, heroData, skill);
                return;
            }

            Main.SetNowActiveUnitAttacked(controller, false);
            controller.nowActiveState = ActiveUnitBattleState.WaitPlayerInput;
            heroData.SetNowActiveSkill(null);
        }

        internal static void ContinueAiTurnWithPlan(BattleController controller, HeroData heroData, AIBattlePlan plan, bool forceAttackState)
        {
            if (controller == null || heroData == null || plan == null)
            {
                return;
            }

            Main.SetNowActiveUnitAttacked(controller, false);
            controller.ResetAllAIPlans();
            controller.AIBattlePlans.Add(plan);
            controller.AIBattleChoiceList.Add(0);
            controller.AIBattleChoice = 0;
            controller.AIBattleAttackChoice = 0;
            controller.bestPlanValue = plan.bestPlanValue;

            if (plan.battleAttackPlan != null && plan.battleAttackPlan.Count > 0)
            {
                AIBattleAttackPlan attackPlan = plan.battleAttackPlan[0];
                heroData.nowActiveSkill = attackPlan.activeSkill;
                // Main.Log.Msg($"AI 后续计划选中技能: activeSkill={attackPlan.activeSkill}, moveTarget={DescribeGrid(plan.moveTarget)}, targetCount={GetTargetGridCount(attackPlan)}, firstTarget={DescribeFirstTargetGrid(attackPlan)}");
            }
            else
            {
                heroData.SetNowActiveSkill(null);
                // Main.Log.Msg($"AI 后续计划无攻击技能: plan={plan.battlePlanType}, moveTarget={DescribeGrid(plan.moveTarget)}");
            }

            if (forceAttackState)
            {
                controller.nowActiveState = ActiveUnitBattleState.ShowAttackingRage;
            }
            else if (controller.NowActiveUnitCanMove())
            {
                controller.nowActiveState = ActiveUnitBattleState.ShowMovingRage;
            }
            else
            {
                switch (plan.battlePlanType)
                {
                    case AIBattlePlanType.UseSkill:
                        controller.nowActiveState = ActiveUnitBattleState.ShowAttackingRage;
                        break;
                    case AIBattlePlanType.UseItem:
                        controller.nowActiveState = ActiveUnitBattleState.UseItem;
                        break;
                    case AIBattlePlanType.SelfCure:
                    case AIBattlePlanType.Posture:
                    case AIBattlePlanType.Rest:
                        controller.nowActiveState = ActiveUnitBattleState.Rest;
                        break;
                    default:
                        Main.Log.Warning($"AI 技能后拿到未处理的计划类型，保留原版结束回合: type={plan.battlePlanType}");
                        return;
                }
            }

            controller.AIWaitTime = 1f / controller.GetHalfBattleTimeScale();
            // Main.Log.Msg($"AI 技能后续行动: plan={plan.battlePlanType}, move={(controller.nowActiveState == ActiveUnitBattleState.ShowMovingRage)}, state={controller.nowActiveState}, best={plan.bestPlanValue}");
        }

        private static void TryContinueAiTurn(BattleController controller, BattleUnit nowActiveUnit, HeroData heroData, KungfuSkillLvData usedSkill)
        {
            AIBattlePlan currentGridAttackPlan = BuildCurrentGridAttackPlan(controller, nowActiveUnit);
            AIBattlePlan nextSupportedSkillPlan = BuildNextSupportedSkillPlan(controller, nowActiveUnit, heroData, usedSkill, currentGridAttackPlan != null);
            AIBattlePlan defaultAiPlan = nextSupportedSkillPlan == null && currentGridAttackPlan == null ? controller.GetValueableAIBattlePlan(AIBattlePlanType.Rest) : null;
            AIBattlePlan plan = nextSupportedSkillPlan ?? currentGridAttackPlan ?? defaultAiPlan;
            if (plan == null)
            {
                // Main.Log.Msg($"AI 技能后未生成后续计划，保留原版结束回合: type={(FightSkillType)usedSkill.Type()}");
                return;
            }

            // Main.Log.Msg($"AI 技能后选择后续计划: usedSkill={(FightSkillType)usedSkill.Type()}, nextBuff={nextSupportedSkillPlan != null}, currentAttack={currentGridAttackPlan != null}, defaultPlan={defaultAiPlan?.battlePlanType}, chosen={plan.battlePlanType}");
            ContinueAiTurnWithPlan(controller, heroData, plan, nextSupportedSkillPlan != null || currentGridAttackPlan != null);
        }

        internal static AIBattlePlan BuildCurrentGridAttackPlan(BattleController controller, BattleUnit nowActiveUnit)
        {
            if (controller == null || nowActiveUnit?.mapGrid == null)
            {
                return null;
            }

            AIBattlePlan plan = new AIBattlePlan(AIBattlePlanType.UseSkill)
            {
                moveTarget = nowActiveUnit.mapGrid
            };
            GenerateAttackOnlyPlan(controller, nowActiveUnit.heroData, plan);

            if (plan.battleAttackPlan == null || plan.battleAttackPlan.Count <= 0)
            {
                // Main.Log.Msg($"AI 当前格攻击计划排除: moveTarget={DescribeGrid(nowActiveUnit.mapGrid)}, reason=无攻击计划");
                return null;
            }

            AIBattleAttackPlan attackPlan = plan.battleAttackPlan[0];
            if (attackPlan == null)
            {
                // Main.Log.Msg($"AI 当前格攻击计划排除: moveTarget={DescribeGrid(nowActiveUnit.mapGrid)}, reason=空攻击计划");
                return null;
            }

            if (attackPlan.activeSkill < 3)
            {
                // Main.Log.Msg($"AI 当前格攻击计划排除: moveTarget={DescribeGrid(nowActiveUnit.mapGrid)}, reason=非攻击技能, activeSkill={attackPlan.activeSkill}, targetCount={GetTargetGridCount(attackPlan)}, firstTarget={DescribeFirstTargetGrid(attackPlan)}");
                return null;
            }

            if (!HasAvailableEnemyTarget(controller, attackPlan))
            {
                // Main.Log.Msg($"AI 当前格攻击计划排除: moveTarget={DescribeGrid(nowActiveUnit.mapGrid)}, reason=无可用敌方目标, activeSkill={attackPlan.activeSkill}, targetCount={GetTargetGridCount(attackPlan)}, firstTarget={DescribeFirstTargetGrid(attackPlan)}");
                // LogAttackTargetDetails(controller, attackPlan);
                return null;
            }

            // Main.Log.Msg($"AI 当前格攻击计划接受: moveTarget={DescribeGrid(nowActiveUnit.mapGrid)}, activeSkill={attackPlan.activeSkill}, value={attackPlan.planValue}, targetCount={GetTargetGridCount(attackPlan)}, firstTarget={DescribeFirstTargetGrid(attackPlan)}");
            return plan;
        }

        private static AIBattlePlan BuildAttackPlanFromGrid(BattleController controller, GridUnitData moveTarget)
        {
            if (controller == null || moveTarget == null)
            {
                return null;
            }

            AIBattlePlan plan = new AIBattlePlan(AIBattlePlanType.UseSkill)
            {
                moveTarget = moveTarget
            };
            GenerateAttackOnlyPlan(controller, controller.nowActiveUnit?.heroData, plan);

            if (plan.battleAttackPlan == null || plan.battleAttackPlan.Count <= 0)
            {
                // Main.Log.Msg($"AI 跳后攻击计划排除: moveTarget={DescribeGrid(moveTarget)}, raw=无攻击计划");
                return null;
            }

            AIBattleAttackPlan attackPlan = plan.battleAttackPlan[0];
            if (attackPlan == null || attackPlan.activeSkill < 3 || !HasAvailableEnemyTarget(controller, attackPlan))
            {
                // Main.Log.Msg($"AI 跳后攻击计划排除: moveTarget={DescribeGrid(moveTarget)}, raw={DescribePlanTargets(controller, plan)}");
                return null;
            }

            // Main.Log.Msg($"AI 跳后攻击计划接受: moveTarget={DescribeGrid(moveTarget)}, raw={DescribePlanTargets(controller, plan)}");
            return plan;
        }

        private static void GenerateAttackOnlyPlan(BattleController controller, HeroData heroData, AIBattlePlan plan)
        {
            if (controller == null || heroData == null || plan == null)
            {
                return;
            }

            SkillUseState internalState = new SkillUseState(heroData.internalSkill);
            SkillUseState dodgeState = new SkillUseState(heroData.dodgeSkill);
            SkillUseState uniqueState = new SkillUseState(heroData.uniqueSkill);

            try
            {
                BlockSkillForPlan(heroData.internalSkill);
                BlockSkillForPlan(heroData.dodgeSkill);
                BlockSkillForPlan(heroData.uniqueSkill);
                controller.GenerateAIPlanUseSkill(plan);
            }
            finally
            {
                internalState.Restore();
                dodgeState.Restore();
                uniqueState.Restore();
            }
        }

        private static void BlockSkillForPlan(KungfuSkillLvData skill)
        {
            if (BattleSkillUnlockHelper.IsSupportedSkill(skill))
            {
                skill.useTime = 1;
                skill.cdTimeLeft = 999999f;
                skill.activeTimeLeft = 999999f;
            }
        }

        private readonly struct SkillUseState
        {
            private readonly KungfuSkillLvData skill;
            private readonly int useTime;
            private readonly float cdTimeLeft;
            private readonly float activeTimeLeft;

            internal SkillUseState(KungfuSkillLvData skill)
            {
                this.skill = skill;
                useTime = skill == null ? 0 : skill.useTime;
                cdTimeLeft = skill == null ? 0f : skill.cdTimeLeft;
                activeTimeLeft = skill == null ? 0f : skill.activeTimeLeft;
            }

            internal void Restore()
            {
                if (skill == null)
                {
                    return;
                }

                skill.useTime = useTime;
                skill.cdTimeLeft = cdTimeLeft;
                skill.activeTimeLeft = activeTimeLeft;
            }
        }

        private static bool HasAvailableEnemyTarget(BattleController controller, AIBattleAttackPlan attackPlan)
        {
            if (controller == null || attackPlan?.attackTargetGridUnits == null || attackPlan.attackTargetGridUnits.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < attackPlan.attackTargetGridUnits.Count; i++)
            {
                GridUnitData targetGrid = attackPlan.attackTargetGridUnits[i];
                if (controller.HaveAvailableEnemyUnit(targetGrid))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetTargetGridCount(AIBattleAttackPlan attackPlan)
        {
            return attackPlan?.attackTargetGridUnits == null ? 0 : attackPlan.attackTargetGridUnits.Count;
        }

        private static string DescribeFirstTargetGrid(AIBattleAttackPlan attackPlan)
        {
            if (attackPlan?.attackTargetGridUnits == null || attackPlan.attackTargetGridUnits.Count <= 0)
            {
                return "null";
            }

            return DescribeGrid(attackPlan.attackTargetGridUnits[0]);
        }

        private static void LogAttackTargetDetails(BattleController controller, AIBattleAttackPlan attackPlan)
        {
            if (attackPlan?.attackTargetGridUnits == null)
            {
                return;
            }

            int currentTeamID = controller?.nowActiveUnit?.battleTeam == null ? -999 : controller.nowActiveUnit.battleTeam.ID;
            int logCount = attackPlan.attackTargetGridUnits.Count < 12 ? attackPlan.attackTargetGridUnits.Count : 12;
            for (int i = 0; i < logCount; i++)
            {
                GridUnitData targetGrid = attackPlan.attackTargetGridUnits[i];
                BattleUnit battleUnit = targetGrid?.battleUnit;
                int targetTeamID = battleUnit?.battleTeam == null ? -999 : battleUnit.battleTeam.ID;
                bool availableEnemy = controller != null && controller.HaveAvailableEnemyUnit(targetGrid);
                // Main.Log.Msg($"AI 当前格攻击目标明细: index={i}, grid={DescribeGrid(targetGrid)}, hasUnit={battleUnit != null}, targetAlive={battleUnit?.IsAlive}, targetTeam={targetTeamID}, currentTeam={currentTeamID}, availableEnemy={availableEnemy}");
            }

            if (attackPlan.attackTargetGridUnits.Count > logCount)
            {
                // Main.Log.Msg($"AI 当前格攻击目标明细: omitted={attackPlan.attackTargetGridUnits.Count - logCount}");
            }
        }

        internal static AIBattlePlan BuildNextSupportedSkillPlan(BattleController controller, BattleUnit nowActiveUnit, HeroData heroData, KungfuSkillLvData usedSkill, bool useDodgeInPlace)
        {
            if (nowActiveUnit?.mapGrid == null || !TryGetNextSupportedSkill(heroData, usedSkill, out int activeSkill))
            {
                return null;
            }

            if (activeSkill == 1)
            {
                if (useDodgeInPlace)
                {
                    // Main.Log.Msg($"AI 轻功原地释放: reason=已有后续攻击计划, current={DescribeGrid(nowActiveUnit.mapGrid)}");
                    return BuildDirectSkillPlan(activeSkill, nowActiveUnit.mapGrid, nowActiveUnit.mapGrid, 999999f);
                }

                // Main.Log.Msg($"AI 准备续用轻功: current={DescribeGrid(nowActiveUnit.mapGrid)}");
                AIBattlePlan dodgePlan = BuildDodgeSkillPlan(controller, nowActiveUnit, heroData.dodgeSkill);
                if (dodgePlan != null)
                {
                    // Main.Log.Msg($"AI 轻功计划生成成功: moveTarget={DescribeGrid(dodgePlan.moveTarget)}, current={DescribeGrid(nowActiveUnit.mapGrid)}, value={dodgePlan.bestPlanValue}");
                    return dodgePlan;
                }

                Main.Log.Warning($"AI 轻功计划生成失败: current={DescribeGrid(nowActiveUnit.mapGrid)}");
                return null;
            }

            return BuildDirectSkillPlan(activeSkill, nowActiveUnit.mapGrid, nowActiveUnit.mapGrid, 999999f);
        }

        private static AIBattlePlan BuildDirectSkillPlan(int activeSkill, GridUnitData moveTarget, GridUnitData targetGrid, float planValue)
        {
            AIBattleAttackPlan attackPlan = new AIBattleAttackPlan
            {
                planValue = planValue,
                activeSkill = activeSkill,
                attackTargetGridUnits = new Il2CppSystem.Collections.Generic.List<GridUnitData>()
            };
            attackPlan.attackTargetGridUnits.Add(targetGrid);

            AIBattlePlan plan = new AIBattlePlan(AIBattlePlanType.UseSkill)
            {
                bestPlanValue = attackPlan.planValue,
                moveTarget = moveTarget
            };
            plan.battleAttackPlan.Add(attackPlan);
            return plan;
        }

        internal static AIBattlePlan BuildDodgeSkillPlan(BattleController controller, BattleUnit nowActiveUnit, KungfuSkillLvData dodgeSkill)
        {
            if (controller == null || nowActiveUnit?.mapGrid == null || dodgeSkill == null)
            {
                return null;
            }

            Il2CppSystem.Collections.Generic.List<GridUnitData> targetGrids = BuildDodgeTargetGrids(controller, nowActiveUnit, dodgeSkill);
            if (targetGrids == null || targetGrids.Count <= 0)
            {
                // Main.Log.Msg($"AI 轻功候选为空: current={DescribeGrid(nowActiveUnit.mapGrid)}");
                return null;
            }

            GridUnitData bestAttackTarget = null;
            float bestAttackValue = float.MinValue;
            int bestAttackDistance = -1;
            int bestEnemyDistance = int.MaxValue;

            // Main.Log.Msg($"AI 轻功候选开始: current={DescribeGrid(nowActiveUnit.mapGrid)}, targetCount={targetGrids.Count}");
            for (int i = 0; i < targetGrids.Count; i++)
            {
                GridUnitData targetGrid = targetGrids[i];
                string invalidReason = GetInvalidDodgeTargetReason(nowActiveUnit.mapGrid, dodgeSkill, targetGrid);
                if (invalidReason != null)
                {
                    // Main.Log.Msg($"AI 轻功目标排除: index={i}, target={DescribeGrid(targetGrid)}, reason={invalidReason}");
                    continue;
                }

                int distance = nowActiveUnit.mapGrid.Distance(targetGrid);

                float planValue = GetDodgeTargetPlanValue(controller, targetGrid);
                int enemyDistance = GetNearestEnemyDistance(controller, targetGrid);
                // Main.Log.Msg($"AI 轻功目标评分: index={i}, target={DescribeGrid(targetGrid)}, distance={distance}, enemyDistance={enemyDistance}, value={planValue}");
                if (bestAttackTarget == null ||
                    planValue > bestAttackValue ||
                    planValue == bestAttackValue && enemyDistance < bestEnemyDistance ||
                    planValue == bestAttackValue && enemyDistance == bestEnemyDistance && targetGrid == nowActiveUnit.mapGrid && bestAttackTarget != nowActiveUnit.mapGrid)
                {
                    bestAttackTarget = targetGrid;
                    bestAttackValue = planValue;
                    bestAttackDistance = distance;
                    bestEnemyDistance = enemyDistance;
                    // Main.Log.Msg($"AI 轻功候选更新: index={i}, target={DescribeGrid(targetGrid)}, value={planValue}, distance={distance}, enemyDistance={enemyDistance}");
                }
            }

            bool useAttackTarget = bestAttackTarget != null;
            if (!useAttackTarget)
            {
                // Main.Log.Msg($"AI 轻功取消: reason=无合法评分点, current={DescribeGrid(nowActiveUnit.mapGrid)}");
                return null;
            }

            // Main.Log.Msg($"AI 轻功最终选点: target={DescribeGrid(bestAttackTarget)}, value={bestAttackValue}, distance={bestAttackDistance}, enemyDistance={bestEnemyDistance}");
            return BuildDirectSkillPlan(1, nowActiveUnit.mapGrid, bestAttackTarget, bestAttackValue);
        }

        private static float GetDodgeTargetPlanValue(BattleController controller, GridUnitData targetGrid)
        {
            AIBattlePlan followUpPlan = BuildAttackPlanFromGrid(controller, targetGrid);
            float attackValue = followUpPlan == null ? 0f : followUpPlan.bestPlanValue;
            return attackValue + GetSpeGridValueRate(targetGrid);
        }

        private static Il2CppSystem.Collections.Generic.List<GridUnitData> BuildDodgeTargetGrids(BattleController controller, BattleUnit nowActiveUnit, KungfuSkillLvData dodgeSkill)
        {
            if (controller?.attackRangeGridUnits == null || Main.GetAttackRangeGridsMethod == null)
            {
                return null;
            }

            controller.attackRangeGridUnits.Clear();
            Main.GetAttackRangeGridsMethod.Invoke(controller, new object[] { nowActiveUnit.heroData, dodgeSkill.DataBase(), nowActiveUnit.mapGrid, controller.attackRangeGridUnits });
            Il2CppSystem.Collections.Generic.List<GridUnitData> targetGrids = controller.RemoveAllUnemptyGrid(controller.attackRangeGridUnits);
            targetGrids.Add(nowActiveUnit.mapGrid);

            return targetGrids;
        }

        private static bool IsValidDodgeTargetGrid(GridUnitData currentGrid, KungfuSkillLvData dodgeSkill, GridUnitData targetGrid)
        {
            return GetInvalidDodgeTargetReason(currentGrid, dodgeSkill, targetGrid) == null;
        }

        private static string GetInvalidDodgeTargetReason(GridUnitData currentGrid, KungfuSkillLvData dodgeSkill, GridUnitData targetGrid)
        {
            if (targetGrid == null)
            {
                return "空格";
            }

            if (targetGrid != currentGrid && targetGrid.battleUnit != null)
            {
                return "已有单位";
            }

            if (targetGrid.obstale != null)
            {
                return "有障碍物";
            }

            if (targetGrid.GridType == GridType.Obstacle)
            {
                return "障碍格";
            }

            if (IsHazardousSpeGrid(targetGrid))
            {
                return "危险特殊格";
            }

            if (targetGrid != currentGrid && !targetGrid.isEmpty())
            {
                return "非空格";
            }

            if (targetGrid != currentGrid && !IsInDodgeRange(currentGrid, dodgeSkill, targetGrid))
            {
                return "超出轻功范围";
            }

            return null;
        }

        private static bool IsHazardousSpeGrid(GridUnitData targetGrid)
        {
            SpeGridObjData speGridObjData = targetGrid?.speGridObjData;
            if (speGridObjData == null)
            {
                return false;
            }

            if (speGridObjData.GetValueRate() < 0f)
            {
                return true;
            }

            SpeGridObjType speGridObjType = speGridObjData.speGridObjType;
            return speGridObjType == SpeGridObjType.PoisonInsect ||
                   speGridObjType == SpeGridObjType.PoisonLake ||
                   speGridObjType == SpeGridObjType.ArrowTrap ||
                   speGridObjType == SpeGridObjType.Spike ||
                   speGridObjType == SpeGridObjType.StoneTrap ||
                   speGridObjType == SpeGridObjType.HotCarbon;
        }

        private static float GetSpeGridValueRate(GridUnitData targetGrid)
        {
            SpeGridObjData speGridObjData = targetGrid?.speGridObjData;
            return speGridObjData == null ? 0f : speGridObjData.GetValueRate();
        }

        private static int GetNearestEnemyDistance(BattleController controller, GridUnitData targetGrid)
        {
            BattleUnit nowActiveUnit = controller?.nowActiveUnit;
            if (nowActiveUnit == null || targetGrid == null || controller.teams == null)
            {
                return int.MaxValue;
            }

            int nearestDistance = int.MaxValue;
            for (int teamIndex = 0; teamIndex < controller.teams.Count; teamIndex++)
            {
                BattleTeam team = controller.teams[teamIndex];
                if (team == null || team == nowActiveUnit.battleTeam || team.battleUnits == null)
                {
                    continue;
                }

                for (int unitIndex = 0; unitIndex < team.battleUnits.Count; unitIndex++)
                {
                    BattleUnit battleUnit = team.battleUnits[unitIndex];
                    if (battleUnit == null || !battleUnit.IsAlive || battleUnit.mapGrid == null)
                    {
                        continue;
                    }

                    int distance = targetGrid.Distance(battleUnit.mapGrid);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                    }
                }
            }

            return nearestDistance;
        }

        private static string DescribeGrid(GridUnitData grid)
        {
            if (grid == null)
            {
                return "null";
            }

            return $"map={grid.mapID}, row={grid.row}, col={grid.column}";
        }

        private static bool IsInDodgeRange(GridUnitData currentGrid, KungfuSkillLvData dodgeSkill, GridUnitData targetGrid)
        {
            KungfuSkillData skillData = dodgeSkill.DataBase();
            if (currentGrid == null || skillData?.attackRangeData == null || skillData.attackRangeData.Count <= 0 || targetGrid == null)
            {
                return false;
            }

            int distance = currentGrid.Distance(targetGrid);
            for (int i = 0; i < skillData.attackRangeData.Count; i++)
            {
                SkillAttackRangeData rangeData = skillData.attackRangeData[i];
                if (rangeData != null && distance >= rangeData.minRange && distance <= rangeData.maxRange)
                {
                    return true;
                }
            }

            // Main.Log.Msg($"AI 轻功目标超出范围: current={DescribeGrid(currentGrid)}, target={DescribeGrid(targetGrid)}, distance={distance}");
            return false;
        }

        private static bool TryGetNextSupportedSkill(HeroData heroData, KungfuSkillLvData usedSkill, out int activeSkill)
        {
            if (CanUseSupportedSkill(heroData, heroData?.internalSkill, usedSkill))
            {
                activeSkill = 0;
                return true;
            }

            if (CanUseSupportedSkill(heroData, heroData?.dodgeSkill, usedSkill))
            {
                activeSkill = 1;
                return true;
            }

            if (CanUseSupportedSkill(heroData, heroData?.uniqueSkill, usedSkill))
            {
                activeSkill = 2;
                return true;
            }

            activeSkill = -1;
            return false;
        }

        private static bool CanUseSupportedSkill(HeroData heroData, KungfuSkillLvData skill, KungfuSkillLvData usedSkill)
        {
            return heroData != null &&
                   skill != usedSkill &&
                   BattleSkillUnlockHelper.IsSupportedSkill(skill) &&
                   heroData.CanUseSkill(skill);
        }
    }

    internal static class BattleController_PlayBattleUnitMove_MoveNext_Patch
    {
        public static void Postfix(object __instance, bool __result)
        {
            if (!__result || __instance == null)
            {
                return;
            }

            BattleController controller = Main.GetBattleUnitAttackEndController(__instance);
            if (controller == null || controller.nowActiveState != ActiveUnitBattleState.WaitAIPlan)
            {
                return;
            }

            BattleUnit nowActiveUnit = controller.nowActiveUnit;
            if (nowActiveUnit == null || nowActiveUnit.playerControl)
            {
                return;
            }

            HeroData heroData = nowActiveUnit.heroData;
            if (BattleController_BattleUnitAttackEnd_MoveNext_Patch.BuildCurrentGridAttackPlan(controller, nowActiveUnit) != null)
            {
                AIBattlePlan buffPlan = BattleController_BattleUnitAttackEnd_MoveNext_Patch.BuildNextSupportedSkillPlan(controller, nowActiveUnit, heroData, null, true);
                if (buffPlan != null)
                {
                    BattleController_BattleUnitAttackEnd_MoveNext_Patch.ContinueAiTurnWithPlan(controller, heroData, buffPlan, true);
                }

                return;
            }

            if (!CanUseDodgeAfterMove(heroData))
            {
                return;
            }

            AIBattlePlan dodgePlan = BattleController_BattleUnitAttackEnd_MoveNext_Patch.BuildDodgeSkillPlan(controller, nowActiveUnit, heroData.dodgeSkill);
            if (dodgePlan == null)
            {
                return;
            }

            BattleController_BattleUnitAttackEnd_MoveNext_Patch.ContinueAiTurnWithPlan(controller, heroData, dodgePlan, true);
        }

        private static bool CanUseDodgeAfterMove(HeroData heroData)
        {
            KungfuSkillLvData dodgeSkill = heroData?.dodgeSkill;
            return BattleSkillUnlockHelper.IsSupportedSkill(dodgeSkill) && heroData.CanUseSkill(dodgeSkill);
        }
    }
}
