using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace NoGenderLimit
{
    [HarmonyPatch(typeof(PlotController), nameof(PlotController.AskHeroMakeFriend))]
    public static class DialogInteractionPatches
    {
        [HarmonyPostfix]
        public static void AskHeroMakeFriendPostfix(PlotController __instance)
        {
            if (__instance == null)
            {
                return;
            }

            HeroData sourceHero = __instance.sourceInteractHero;
            if (sourceHero == null)
            {
                sourceHero = GameController.Instance?.worldData?.Player();
            }

            HeroData targetHero = __instance.targetInteractHero;
            if (sourceHero == null || targetHero == null)
            {
                return;
            }

            // 异性结缘入口走原版逻辑，这里只补同性分支缺失的入口。
            if (sourceHero.isFemale != targetHero.isFemale)
            {
                return;
            }

            // 原版在更高关系阶段不会再走“结缘”入口补充逻辑。
            if (targetHero.HaveRelationBetterThanFriend(sourceHero.heroID, false, false))
            {
                return;
            }

            // 只有已交友或已有师徒关系时，原版才会进入这段关系选项对话。
            if (!targetHero.HaveFriend(sourceHero.heroID) &&
                !targetHero.HaveTeacherStudentRelation(sourceHero.heroID))
            {
                return;
            }

            // 与原版保持一致：特定版本下不显示该入口。
            if ((int)GlobalData.GameVersion == 2)
            {
                return;
            }

            SinglePlotData currentPlot = __instance.nowSinglePlot;
            List<SinglePlotChoiceData> choices = currentPlot?.choices;
            if (currentPlot == null || choices == null || choices.Count == 0)
            {
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                SinglePlotChoiceData existingChoice = choices[i];
                if (existingChoice == null)
                {
                    continue;
                }

                if (existingChoice.callFuc == "AskHeroToLover" || existingChoice.choiceText == "结缘")
                {
                    return;
                }
            }

            SinglePlotChoiceData loverChoice = new()
            {
                choiceText = "结缘",
                callFuc = "AskHeroToLover",
                requirements = new List<PlotChoiceRequirement>(),
                relations = new List<RelationRequirementType>(),
                costResource = new List<ResourceData>()
            };
            loverChoice.requirements.Add(new PlotChoiceRequirement(ChoiceRequirementType.FavorDegree, 100f));

            int insertIndex = choices.Count;
            if (insertIndex > 0)
            {
                insertIndex--;
            }

            choices.Insert(insertIndex, loverChoice);
            currentPlot.choices = choices;

            Main.Log.Msg($"已为 {targetHero.heroName} 的交友关系对话补充同性结缘入口。");
        }
    }
}
