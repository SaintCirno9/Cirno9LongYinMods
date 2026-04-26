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

        private static Type FindBattleUnitAttackEndIteratorType()
        {
            Type[] nestedTypes = typeof(BattleController).GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (Type nestedType in nestedTypes)
            {
                if (nestedType.Name.Contains("BattleUnitAttackEnd"))
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

        private static void TryContinueAiTurn(BattleController controller, BattleUnit nowActiveUnit, HeroData heroData, KungfuSkillLvData usedSkill)
        {
            AIBattlePlan nextSupportedSkillPlan = BuildNextSupportedSkillPlan(nowActiveUnit, heroData, usedSkill);
            AIBattlePlan plan = nextSupportedSkillPlan ?? controller.GetValueableAIBattlePlan(AIBattlePlanType.Rest);
            if (plan == null)
            {
                // Main.Log.Msg($"AI 技能后未生成后续计划，保留原版结束回合: type={(FightSkillType)usedSkill.Type()}");
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
            }
            else
            {
                heroData.SetNowActiveSkill(null);
            }

            if (nextSupportedSkillPlan != null)
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
            Main.Log.Msg($"AI 技能后续行动: type={(FightSkillType)usedSkill.Type()}, plan={plan.battlePlanType}, move={(controller.nowActiveState == ActiveUnitBattleState.ShowMovingRage)}, state={controller.nowActiveState}, best={plan.bestPlanValue}");
        }

        private static AIBattlePlan BuildNextSupportedSkillPlan(BattleUnit nowActiveUnit, HeroData heroData, KungfuSkillLvData usedSkill)
        {
            if (nowActiveUnit?.mapGrid == null || !TryGetNextSupportedSkill(heroData, usedSkill, out int activeSkill))
            {
                return null;
            }

            AIBattleAttackPlan attackPlan = new AIBattleAttackPlan
            {
                planValue = 999999f,
                activeSkill = activeSkill,
                attackTargetGridUnits = new Il2CppSystem.Collections.Generic.List<GridUnitData>()
            };
            attackPlan.attackTargetGridUnits.Add(nowActiveUnit.mapGrid);

            AIBattlePlan plan = new AIBattlePlan(AIBattlePlanType.UseSkill)
            {
                bestPlanValue = attackPlan.planValue,
                moveTarget = nowActiveUnit.mapGrid
            };
            plan.battleAttackPlan.Add(attackPlan);
            return plan;
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
}
