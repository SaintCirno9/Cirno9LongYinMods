using MelonLoader;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

[assembly: MelonInfo(typeof(LongYinGameplayTweaks.Main), "LongYinGameplayTweaks", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace LongYinGameplayTweaks
{
    public class Main : MelonMod
    {
        // MelonLoader 会自动处理带有 [HarmonyPatch] 的类
        // 因此这里移除了手动创建的 Harmony 实例和相关代码

        public override void OnInitializeMelon()
        {
            LOG.Msg("LongYinGameplayTweaks loaded!");
        }

        public static readonly MelonLogger.Instance LOG = Melon<Main>.Logger;
    }
    // [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeMaxAttri))]
    // public class ChangeMaxAttriPatch
    // {
    //     [HarmonyPrefix]
    //     public static bool Prefix(HeroData __instance, int id, int num, bool showInfo)
    //     {
    //         // 在修改数值前的日志记录逻辑
    //         Main.LOG.Msg($"ChangeMaxAttri called: id={id}, num={num}, showInfo={showInfo}");
    //
    //         var maxAttri = __instance.maxAttri;
    //         maxAttri[id] = maxAttri[id] + num; // 修改最大属性值
    //         
    //         return false; // 阻止原方法执行
    //     }
    // }
    //
    // [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeMaxFightSkill))]
    // public class ChangeMaxFightSkillPatch
    // {
    //     [HarmonyPrefix]
    //     public static bool Prefix(HeroData __instance, int id, int num, bool showInfo)
    //     {
    //         // 在修改数值前的日志记录逻辑
    //         Main.LOG.Msg($"ChangeMaxFightSkill called: id={id}, num={num}, showInfo={showInfo}");
    //
    //         var maxFightSkill = __instance.maxFightSkill;
    //         maxFightSkill[id] = maxFightSkill[id] + num; // 修改最大战斗技能值
    //         
    //         return false; // 阻止原方法执行
    //     }
    // }

    // [HarmonyPatch(typeof(HeroData), nameof(HeroData.GetMaxAttri))]
    // public class GetMaxAttriPatch
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(HeroData __instance, int id, ref float __result)
    //     {
    //         __result *= 2; // 将结果翻倍
    //         Main.LOG.Msg($"GetMaxAttri patched: id={id}, original result={__result / 2}, new result={__result}");
    //     }
    // }

    // [HarmonyPatch(typeof(HeroData), nameof(HeroData.ChangeAttriAndSkill))]
    // public class ChangeAttriAndSkillPatch
    // {
    //     [HarmonyPrefix]
    //     public static bool Prefix(HeroData __instance, HeroSpeAddDataType speAddDataType, float num)
    //     {
    //         if (__instance.heroID == 0)
    //         {
    //             Main.LOG.Msg($"heroName: {__instance.heroName}");
    //         }
    //         return true; // 允许原方法执行
    //     }
    // }

    [HarmonyPatch(typeof(HeroData), nameof(HeroData.RefreshMaxAttriAndSkill))]
    public class RefreshMaxAttriAndSkillPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(HeroData __instance)
        {
            // Main.LOG.Msg($"RefreshMaxAttriAndSkill called for hero: {__instance.heroName}");
            return __instance.heroID != 0;
        }
    }

    [HarmonyPatch(typeof(BreakThroughController), nameof(BreakThroughController.BreakThroughChoiceClicked))]
    public class BreakThroughChoiceClickedPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BreakThroughController __instance, BreakThroughChoiceController targetChoice)
        {
            if (targetChoice?.extraAddData?.heroSpeAddData == null)
            {
                return true;
            }

            var target = targetChoice.extraAddData.heroSpeAddData;
            target.Clear();

            var allAvailableChoices = new Dictionary<int, float>();
            List<HeroSpeAddDataBase> speAddDataBase = GameDataController.Instance.speAddDataBase;
            foreach (int choiceType in __instance.targetSkill.GetBreakThroughAvailableChoice())
            {
                allAvailableChoices[choiceType] = speAddDataBase[choiceType].speValue * 10;
            }

            Merge(allAvailableChoices);

            // var lv = __instance.targetSkill.lv + 1;
            // var db = __instance.targetSkill.DataBase();
            // Merge(db.GetSpeEquipData(lv)?.heroSpeAddData);
            // Merge(db.GetSpeUseData(lv)?.heroSpeAddData);

            Main.LOG.Msg("BreakThrough attributes changed.");
            return true; // 允许原方法执行


            void Merge(Dictionary<int, float> source)
            {
                if (source == null)
                {
                    return;
                }

                foreach (KeyValuePair<int, float> kvp in source)
                {
                    if (target.ContainsKey(kvp.Key))
                    {
                        target[kvp.Key] += kvp.Value;
                    }
                    else
                    {
                        target.Add(kvp.Key, kvp.Value);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(DrinkUIController), nameof(DrinkUIController.NextButtonClicked))]
    public class NextButtonClickedPatch
    {
        // Postfix 会在原方法执行完毕后立刻执行
        [HarmonyPostfix]
        public static void Postfix(DrinkUIController __instance)
        {
            __instance.playerFillTarget = 1.25f;
            // __instance.enemyLose = true;
        }
    }

    [HarmonyPatch(typeof(DrinkUIController), nameof(DrinkUIController.SetFillAmount))]
    public class SetFillAmountPatch
    {
        [HarmonyPrefix]
        public static void Prefix(DrinkUIController __instance, GameObject targetBar)
        {
            if (targetBar == __instance.enemyBar)
            {
                __instance.enemyFillAmount = 0.95f;
            }
        }
    }

    [HarmonyPatch(typeof(GameDataController), nameof(GameDataController.GetExternalStorageMaxValue))]
    public class GetExternalStorageMaxValuePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref int __result)
        {
            __result *= 100;
        }
    }

    

    [HarmonyPatch(typeof(GameController), nameof(GameController.StartLoadGame))]
    public class HeroTagReplaceTagCleanupPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            List<HeroTagDataBase> heroTags = GameDataController.Instance?.heroTagDataBase;
            if (heroTags is not { Count: > 0 })
            {
                Main.LOG.Msg("HeroTag database is empty. Skipping replaceTag cleanup.");
                return;
            }

            foreach (HeroTagDataBase heroTag in heroTags)
            {
                heroTag?.replaceTag?.Clear();
            }

            Main.LOG.Msg("HeroTag replacement rules cleared.");
        }
    }
}
