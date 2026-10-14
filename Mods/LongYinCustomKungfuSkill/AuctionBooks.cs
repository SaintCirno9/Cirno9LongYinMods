using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace LongYinCustomKungfuSkill
{
    internal sealed class AuctionBookPool
    {
        internal readonly List<int>[] CustomSkillIds = new List<int>[6];
        internal readonly int[] JianghuSkillCounts = new int[6];

        internal AuctionBookPool(GameDataController gameData)
        {
            for (int level = 0; level < CustomSkillIds.Length; level++)
            {
                CustomSkillIds[level] = new List<int>();
            }

            if (gameData?.kungfuSkillDataList == null || gameData.bigForceIDList == null ||
                !gameData.kungfuSkillDataList.TryGetValue(-1, out var jianghuPools) || jianghuPools == null)
            {
                return;
            }

            for (int level = 0; level < JianghuSkillCounts.Length && level < jianghuPools.Count; level++)
            {
                JianghuSkillCounts[level] = jianghuPools[level]?.Count ?? 0;
            }

            foreach (int skillId in Main.GetInjectedCustomSkillIds())
            {
                KungfuSkillData skill = gameData.GetSkillDataBase(skillId);
                if (skill == null || skill.hide || skill.belongForceID == -1 ||
                    gameData.bigForceIDList.Contains(skill.belongForceID) ||
                    skill.rareLv < 0 || skill.rareLv >= CustomSkillIds.Length)
                {
                    continue;
                }

                // 江湖自定义功法与大门派功法已由原生生成器处理，只补充新增小门派功法。
                CustomSkillIds[skill.rareLv].Add(skill.skillID);
            }
        }

        internal void ApplyToBook(int forceId, Il2CppSystem.Random randomTarget, ref ItemData book)
        {
            if (forceId != -1 || book?.type != ItemType.Book || book.bookData == null)
            {
                return;
            }

            KungfuSkillData originalSkill = book.bookData.DataBase();
            if (originalSkill == null || originalSkill.belongForceID != -1 ||
                originalSkill.rareLv < 0 || originalSkill.rareLv >= CustomSkillIds.Length)
            {
                return;
            }

            int level = originalSkill.rareLv;
            List<int> customSkills = CustomSkillIds[level];
            if (customSkills.Count == 0)
            {
                return;
            }

            // 保留原版大门派抽取分支；江湖分支按“原江湖池 + 自定义小门派池”等权抽取。
            int originalCount = JianghuSkillCounts[level];
            int choice = GlobalData.RandomRange(0, originalCount + customSkills.Count, randomTarget);
            if (choice >= originalCount)
            {
                book = book.SetBookData(customSkills[choice - originalCount], book.rareLv);
            }
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.GenerateAuctionItem))]
    internal static class PlotController_GenerateAuctionItem_Patch
    {
        [ThreadStatic]
        internal static AuctionBookPool CurrentPool;

        [HarmonyPrefix]
        internal static void Prefix(out AuctionBookPool __state)
        {
            __state = CurrentPool;
            CurrentPool = new AuctionBookPool(GameDataController.Instance);
        }

        [HarmonyFinalizer]
        internal static void Finalizer(AuctionBookPool __state)
        {
            // 无论正常返回还是抛出异常都恢复作用域，并支持嵌套调用。
            CurrentPool = __state;
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.GenerateBook))]
    internal static class GameController_GenerateBook_Patch
    {
        [HarmonyPostfix]
        internal static void Postfix(int forceID, Il2CppSystem.Random randomTarget, ref ItemData __result)
        {
            PlotController_GenerateAuctionItem_Patch.CurrentPool?.ApplyToBook(forceID, randomTarget, ref __result);
        }
    }
}
