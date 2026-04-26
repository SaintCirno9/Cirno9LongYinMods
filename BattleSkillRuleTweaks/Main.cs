using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: MelonInfo(typeof(BattleSkillRuleTweaks.Main), "BattleSkillRuleTweaks", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace BattleSkillRuleTweaks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        private const int BattleUnitAttackEndThisOffset = 0x20;
        private const int BattleControllerNowActiveUnitAttackedOffset = 0x119;

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
            AIBattlePlan nextSupportedSkillPlan = BuildNextSupportedSkillPlan(controller, nowActiveUnit, heroData, usedSkill);
            AIBattlePlan defaultAiPlan = nextSupportedSkillPlan == null ? controller.GetValueableAIBattlePlan(AIBattlePlanType.Rest) : null;
            AIBattlePlan currentGridAttackPlan = nextSupportedSkillPlan == null && defaultAiPlan == null ? BuildCurrentGridAttackPlan(controller, nowActiveUnit) : null;
            AIBattlePlan plan = nextSupportedSkillPlan ?? defaultAiPlan ?? currentGridAttackPlan;
            if (plan == null)
            {
                // Main.Log.Msg($"AI 技能后未生成后续计划，保留原版结束回合: type={(FightSkillType)usedSkill.Type()}");
                return;
            }

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
            controller.GenerateAIPlanUseSkill(plan);

            if (plan.battleAttackPlan == null || plan.battleAttackPlan.Count <= 0)
            {
                return null;
            }

            AIBattleAttackPlan attackPlan = plan.battleAttackPlan[0];
            if (attackPlan == null || attackPlan.activeSkill < 3 || !HasBattleUnitTarget(attackPlan))
            {
                return null;
            }

            return plan;
        }

        private static bool HasBattleUnitTarget(AIBattleAttackPlan attackPlan)
        {
            if (attackPlan?.attackTargetGridUnits == null || attackPlan.attackTargetGridUnits.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < attackPlan.attackTargetGridUnits.Count; i++)
            {
                GridUnitData targetGrid = attackPlan.attackTargetGridUnits[i];
                if (targetGrid?.battleUnit != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static AIBattlePlan BuildNextSupportedSkillPlan(BattleController controller, BattleUnit nowActiveUnit, HeroData heroData, KungfuSkillLvData usedSkill)
        {
            if (nowActiveUnit?.mapGrid == null || !TryGetNextSupportedSkill(heroData, usedSkill, out int activeSkill))
            {
                return null;
            }

            if (activeSkill == 1)
            {
                // Main.Log.Msg($"AI 准备续用轻功: current={DescribeGrid(nowActiveUnit.mapGrid)}");
                AIBattlePlan dodgePlan = BuildDodgeSkillPlan(controller, nowActiveUnit, heroData.dodgeSkill);
                if (dodgePlan != null)
                {
                    // Main.Log.Msg($"AI 轻功计划生成成功: moveTarget={DescribeGrid(dodgePlan.moveTarget)}, current={DescribeGrid(nowActiveUnit.mapGrid)}");
                    return dodgePlan;
                }

                // Main.Log.Warning($"AI 轻功计划生成失败，回退到当前格释放: current={DescribeGrid(nowActiveUnit.mapGrid)}");
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

            AIBattlePlan plan = new AIBattlePlan(AIBattlePlanType.UseSkill)
            {
                bestPlanValue = 999999f,
                moveTarget = nowActiveUnit.mapGrid
            };
            controller.GenerateAIPlanUseSkill(plan);
            // Main.Log.Msg($"AI 原版轻功候选: current={DescribeGrid(nowActiveUnit.mapGrid)}, moveTarget={DescribeGrid(plan.moveTarget)}, planCount={(plan.battleAttackPlan == null ? -1 : plan.battleAttackPlan.Count)}");

            if (plan.battleAttackPlan == null)
            {
                return null;
            }

            for (int i = 0; i < plan.battleAttackPlan.Count; i++)
            {
                AIBattleAttackPlan attackPlan = plan.battleAttackPlan[i];
                GridUnitData targetGrid = GetFirstEmptyTargetGridInDodgeRange(nowActiveUnit.mapGrid, dodgeSkill, attackPlan);
                // Main.Log.Msg($"AI 原版轻功候选明细: index={i}, activeSkill={(attackPlan == null ? -1 : attackPlan.activeSkill)}, targetCount={GetTargetGridCount(attackPlan)}, firstTarget={DescribeFirstTargetGrid(attackPlan)}, emptyTarget={DescribeGrid(targetGrid)}");
                if (targetGrid == null)
                {
                    continue;
                }

                if (attackPlan.activeSkill == 1)
                {
                    plan.battleAttackPlan.Clear();
                    attackPlan.attackTargetGridUnits.Clear();
                    attackPlan.attackTargetGridUnits.Add(targetGrid);
                    plan.battleAttackPlan.Add(attackPlan);
                    plan.bestPlanValue = attackPlan.planValue;
                    return plan;
                }

                // Main.Log.Msg($"AI 轻功复用原版目标格: oldActiveSkill={attackPlan.activeSkill}, target={DescribeGrid(targetGrid)}");
                return BuildDirectSkillPlan(1, nowActiveUnit.mapGrid, targetGrid, attackPlan.planValue);
            }

            return null;
        }

        private static GridUnitData GetFirstEmptyTargetGridInDodgeRange(GridUnitData currentGrid, KungfuSkillLvData dodgeSkill, AIBattleAttackPlan attackPlan)
        {
            if (attackPlan?.attackTargetGridUnits == null || attackPlan.attackTargetGridUnits.Count <= 0)
            {
                return null;
            }

            for (int i = 0; i < attackPlan.attackTargetGridUnits.Count; i++)
            {
                GridUnitData targetGrid = attackPlan.attackTargetGridUnits[i];
                if (targetGrid != null &&
                    targetGrid.battleUnit == null &&
                    targetGrid.obstale == null &&
                    targetGrid.GridType != GridType.Obstacle &&
                    targetGrid.isEmpty() &&
                    IsInDodgeRange(currentGrid, dodgeSkill, targetGrid))
                {
                    return targetGrid;
                }
            }

            return null;
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
            if (!CanUseDodgeAfterMove(heroData))
            {
                return;
            }

            if (BattleController_BattleUnitAttackEnd_MoveNext_Patch.BuildCurrentGridAttackPlan(controller, nowActiveUnit) != null)
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
