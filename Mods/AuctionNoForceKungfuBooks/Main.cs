using HarmonyLib;
using Il2Cpp;
using MelonLoader;

[assembly: MelonInfo(typeof(AuctionNoForceKungfuBooks.Main), "AuctionNoForceKungfuBooks", "1.0.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace AuctionNoForceKungfuBooks
{
    public sealed class Main : MelonMod
    {
        internal static MelonLogger.Instance Log => Melon<Main>.Logger;

        public override void OnInitializeMelon()
        {
            Log.Msg("AuctionNoForceKungfuBooks 已加载，拍卖不会刷出门派武功秘籍。");
        }

        internal static void RemoveForceBooksFromAuction(ItemListData targetItemList)
        {
            if (targetItemList?.allItem == null)
            {
                return;
            }

            for (int i = targetItemList.allItem.Count - 1; i >= 0; i--)
            {
                ItemData item = targetItemList.allItem[i];
                if (!IsForceBook(item))
                {
                    continue;
                }

                RemoveAuctionItemAt(targetItemList, i, item);
            }
        }

        private static bool IsForceBook(ItemData item)
        {
            if (item == null || item.type != ItemType.Book || item.bookData == null)
            {
                return false;
            }

            KungfuSkillData skill = item.bookData.DataBase();
            return skill != null && skill.belongForceID != -1;
        }

        private static void RemoveAuctionItemAt(ItemListData targetItemList, int index, ItemData item)
        {
            targetItemList.allItem.RemoveAt(index);

            int itemType = (int)item.type;
            if (targetItemList.itemTypeList == null || itemType < 0 || itemType >= targetItemList.itemTypeList.Count)
            {
                return;
            }

            Il2CppSystem.Collections.Generic.List<ItemData> typedItems = targetItemList.itemTypeList[itemType];
            if (typedItems == null)
            {
                return;
            }

            if (typedItems.Remove(item))
            {
                return;
            }

            for (int i = typedItems.Count - 1; i >= 0; i--)
            {
                ItemData typedItem = typedItems[i];
                if (typedItem?.bookData != null && typedItem.bookData.skillID == item.bookData.skillID && typedItem.rareLv == item.rareLv)
                {
                    typedItems.RemoveAt(i);
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlotController), nameof(PlotController.GenerateAuctionItem))]
    [HarmonyAfter("LongYinCustomKungfuSkill")]
    public static class PlotControllerGenerateAuctionItemPatch
    {
        public static void Postfix(ItemListData targetItemList)
        {
            Main.RemoveForceBooksFromAuction(targetItemList);
        }
    }
}
