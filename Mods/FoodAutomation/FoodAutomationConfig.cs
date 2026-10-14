using System;
using MelonLoader;
using MelonLoader.Utils;

namespace FoodAutomation
{
    internal static class FoodAutomationConfig
    {
        private static readonly int[] InnDeliveryReserveOptions = { 10000, 30000, 50000, 100000 };
        private static readonly string ConfigFilePath = $"{MelonEnvironment.UserDataDirectory}\\FoodAutomation.cfg";

        private static bool _initialized;
        private static MelonPreferences_Category _category = null!;

        internal static MelonPreferences_Entry<bool> EnableInnDelivery { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> InnDeliveryReserveMoney { get; private set; } = null!;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _category = MelonPreferences.CreateCategory("FoodAutomation", "FoodAutomation");
            _category.SetFilePath(ConfigFilePath);

            EnableInnDelivery = _category.CreateEntry("EnableInnDelivery", false, description: "是否开启每月自动购买全图加上限食物");
            InnDeliveryReserveMoney = _category.CreateEntry("InnDeliveryReserveMoney", 100000, description: "自动购买后保留的最低银钱");

            int normalizedReserve = NormalizeReserveMoney(InnDeliveryReserveMoney.Value);
            if (normalizedReserve != InnDeliveryReserveMoney.Value)
            {
                // 只允许预设档位，非法值回退到默认档并重写配置。
                InnDeliveryReserveMoney.Value = normalizedReserve;
            }

            Save();
        }

        internal static void ToggleInnDelivery()
        {
            EnableInnDelivery.Value = !EnableInnDelivery.Value;
            Save();
        }

        internal static void CycleInnDeliveryReserve()
        {
            int current = NormalizeReserveMoney(InnDeliveryReserveMoney.Value);
            int nextIndex = Array.IndexOf(InnDeliveryReserveOptions, current);
            if (nextIndex < 0)
            {
                nextIndex = InnDeliveryReserveOptions.Length - 1;
            }

            nextIndex = (nextIndex + 1) % InnDeliveryReserveOptions.Length;
            InnDeliveryReserveMoney.Value = InnDeliveryReserveOptions[nextIndex];
            Save();
        }

        internal static void Save()
        {
            if (!_initialized)
            {
                return;
            }

            InnDeliveryReserveMoney.Value = NormalizeReserveMoney(InnDeliveryReserveMoney.Value);
            _category.SaveToFile();
        }

        private static int NormalizeReserveMoney(int value)
        {
            for (int i = 0; i < InnDeliveryReserveOptions.Length; i++)
            {
                if (InnDeliveryReserveOptions[i] == value)
                {
                    return value;
                }
            }

            return 100000;
        }
    }
}
