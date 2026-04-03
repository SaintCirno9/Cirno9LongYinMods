using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace NoGenderLimit
{
    [HarmonyPatch(typeof(PlotController), nameof(PlotController.ChooseQiXiFestivalFriend))]
    public static class QiXiFestivalFriendPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlotController __instance)
        {
            GameController controller = GameController._instance;
            WorldData worldData = controller?.worldData;
            List<HeroData> heroes = worldData?.Heros;
            if (heroes == null)
            {
                Main.Log.Warning("七夕对象筛选失败：角色列表为空。");
                return true;
            }

            List<HeroData> candidates = new();
            for (int i = 1; i < heroes.Count; i++)
            {
                HeroData hero = heroes[i];
                if (hero == null)
                {
                    continue;
                }

                // 这里故意不再判断 hero.isFemale != player.isFemale，
                // 从而允许主角在七夕事件中选择任意性别的对象。
                if (hero.Favor(false) < 50f)
                {
                    continue;
                }

                if (hero.inPrison || hero.hide)
                {
                    continue;
                }

                candidates.Add(hero);
            }

            ChooseController chooseController = ChooseController._instance;
            if (chooseController == null)
            {
                Main.Log.Warning("七夕对象筛选失败：ChooseController 实例为空。");
                return true;
            }

            GameObject gameObject = __instance.gameObject;

            // 这里先保留原流程的目标函数名；后续如果需要，
            // 我们再核对 sendResultParam、filterType 与 cancelFuc 的默认值。
            chooseController.ShowChoosePanel(
                (ChooseType)2,
                candidates,
                gameObject,
                "QiXiFestivalFriendChoosen",
                null,
                (ChooseFilterType)0,
                null);

            Main.Log.Msg($"七夕可选对象数量：{candidates.Count}");
            return false;
        }
    }
}
