using Il2Cpp;
using System.Globalization;

namespace FoodAutomation
{
    internal static class FoodAutomationItemUtility
    {
        internal static bool IsCapBoostFood(ItemData item)
        {
            ChangeHeroStateData changeHeroState = item?.medFoodData?.changeHeroState;
            return changeHeroState != null && (changeHeroState.maxhp > 0f || changeHeroState.maxMana > 0f);
        }

        internal static string FormatMoneyShort(int value)
        {
            if (value >= 10000)
            {
                float wan = value / 10000f;
                return wan.ToString("0.#", CultureInfo.InvariantCulture) + "万";
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
