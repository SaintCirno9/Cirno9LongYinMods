using MelonLoader;
using MelonLoader.Utils;

namespace DualCultivation
{
    internal static class DualCultivationConfig
    {
        private const bool DefaultEnableMod = true;
        private const int DefaultRequiredFavor = 100;
        private const int DefaultRelationRequirement = 1;
        private const int DefaultMonthlyLimit = 1;
        private const int DefaultWorkDays = 1;
        private const double DefaultRewardRate = 0.01d;
        private const int DefaultMaxHpGain = 50;
        private const int DefaultMaxManaGain = 100;
        private const int DefaultFavorGain = 10;
        private const string OldDefaultPromptText = "称呼想和我双修吗？";
        private const string OldDefaultResultText = "此番双修我获益匪浅，不知称呼收获如何？";
        private const string OldDefaultStartChoiceText = "开始双修";
        private const string DefaultPromptText = "{称呼}可愿与我共参阴阳、同调内息？|今日气息圆融，{称呼}可愿与我同修一日？|我近日修行略有所悟，想与{称呼}互证一番。|若{称呼}得闲，不妨与我闭关同修。|你我内息相近，今日同修或许会有所进境。";
        private const string DefaultRewardPreviewText = "可获得{HpGain}点生命上限和{ManaGain}点内力上限，对方可获得{TargetHpGain}点生命上限和{TargetManaGain}点内力上限。";
        private const string DefaultStartChoiceText = "双修";
        private const string DefaultConfirmChoiceText = "开始双修";
        private const string DefaultCancelChoiceText = "转念一想";
        private const string DefaultResultText = "此番双修我获益匪浅，不知{称呼}收获如何？|今日与你气机相合，周天运转竟比平日顺畅许多。|此番同修之效远胜独自闭关，看来你我确有默契。|真气往复之间，我隐有所悟，想来{称呼}也必有所获。|这一日修行颇有进境，若日后得闲，不妨再试。";
        private const string DefaultResultChoiceText = "我亦获益良多";
        private const string DefaultBackPic = "PlotImage/萍水相逢";
        private const bool DefaultVerboseLog = false;

        private static MelonPreferences_Category _category = null!;
        private static bool _initialized;

        internal static string ConfigFilePath { get; } = $"{MelonEnvironment.UserDataDirectory}\\DualCultivation.cfg";

        internal static MelonPreferences_Entry<bool> EnableMod { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> RequiredFavor { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> RelationRequirement { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> MonthlyLimit { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> WorkDays { get; private set; } = null!;
        internal static MelonPreferences_Entry<double> RewardRate { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> MaxHpGain { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> MaxManaGain { get; private set; } = null!;
        internal static MelonPreferences_Entry<int> FavorGain { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> PromptText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> RewardPreviewText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> StartChoiceText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> ConfirmChoiceText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> CancelChoiceText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> ResultText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> ResultChoiceText { get; private set; } = null!;
        internal static MelonPreferences_Entry<string> BackPic { get; private set; } = null!;
        internal static MelonPreferences_Entry<bool> VerboseLog { get; private set; } = null!;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _category = MelonPreferences.CreateCategory("DualCultivation", "DualCultivation");
            _category.SetFilePath(ConfigFilePath);

            EnableMod = _category.CreateEntry("EnableMod", DefaultEnableMod, description: "是否启用双修入口");
            RequiredFavor = _category.CreateEntry("RequiredFavor", DefaultRequiredFavor, description: "开始双修需要的最低好感");
            RelationRequirement = _category.CreateEntry("RelationRequirement", DefaultRelationRequirement, description: "0 不检查伴侣，1 仅当前伴侣，2 当前伴侣或前任伴侣");
            MonthlyLimit = _category.CreateEntry("MonthlyLimit", DefaultMonthlyLimit, description: "每月可与同一 NPC 双修的次数");
            WorkDays = _category.CreateEntry("WorkDays", DefaultWorkDays, description: "双修耗时天数");
            RewardRate = _category.CreateEntry("RewardRate", DefaultRewardRate, description: "收益倍率：自身获得的上限提升 = 对方对应上限 × RewardRate，再受单次上限限制");
            MaxHpGain = _category.CreateEntry("MaxHpGain", DefaultMaxHpGain, description: "单次最多获得的生命上限");
            MaxManaGain = _category.CreateEntry("MaxManaGain", DefaultMaxManaGain, description: "单次最多获得的内力上限");
            FavorGain = _category.CreateEntry("FavorGain", DefaultFavorGain, description: "完成后增加的对方好感");
            PromptText = _category.CreateEntry("PromptText", DefaultPromptText, description: "确认对话文本");
            RewardPreviewText = _category.CreateEntry("RewardPreviewText", DefaultRewardPreviewText, description: "收益预览文本");
            StartChoiceText = _category.CreateEntry("StartChoiceText", DefaultStartChoiceText, description: "修行菜单入口选项文本");
            ConfirmChoiceText = _category.CreateEntry("ConfirmChoiceText", DefaultConfirmChoiceText, description: "确认对话开始选项文本");
            CancelChoiceText = _category.CreateEntry("CancelChoiceText", DefaultCancelChoiceText, description: "取消选项文本");
            ResultText = _category.CreateEntry("ResultText", DefaultResultText, description: "完成后对话文本");
            ResultChoiceText = _category.CreateEntry("ResultChoiceText", DefaultResultChoiceText, description: "完成后选项文本");
            BackPic = _category.CreateEntry("BackPic", DefaultBackPic, description: "确认和完成对话使用的背景图，留空则不指定背景");
            VerboseLog = _category.CreateEntry("VerboseLog", DefaultVerboseLog, description: "是否输出详细日志");

            if (Normalize())
            {
                Save();
            }
        }

        private static bool Normalize()
        {
            bool changed = false;

            if (RequiredFavor.Value < 0)
            {
                RequiredFavor.Value = DefaultRequiredFavor;
                changed = true;
            }

            if (RelationRequirement.Value < 0 || RelationRequirement.Value > 2)
            {
                RelationRequirement.Value = DefaultRelationRequirement;
                changed = true;
            }

            if (MonthlyLimit.Value < 1)
            {
                MonthlyLimit.Value = DefaultMonthlyLimit;
                changed = true;
            }

            if (WorkDays.Value < 1)
            {
                WorkDays.Value = DefaultWorkDays;
                changed = true;
            }

            if (RewardRate.Value <= 0f)
            {
                RewardRate.Value = DefaultRewardRate;
                changed = true;
            }

            if (MaxHpGain.Value < 1)
            {
                MaxHpGain.Value = DefaultMaxHpGain;
                changed = true;
            }

            if (MaxManaGain.Value < 1)
            {
                MaxManaGain.Value = DefaultMaxManaGain;
                changed = true;
            }

            if (FavorGain.Value < 0)
            {
                FavorGain.Value = DefaultFavorGain;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(PromptText.Value))
            {
                PromptText.Value = DefaultPromptText;
                changed = true;
            }
            else if (PromptText.Value == OldDefaultPromptText)
            {
                PromptText.Value = DefaultPromptText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(RewardPreviewText.Value))
            {
                RewardPreviewText.Value = DefaultRewardPreviewText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(StartChoiceText.Value))
            {
                StartChoiceText.Value = DefaultStartChoiceText;
                changed = true;
            }
            else if (StartChoiceText.Value == OldDefaultStartChoiceText)
            {
                StartChoiceText.Value = DefaultStartChoiceText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(ConfirmChoiceText.Value))
            {
                ConfirmChoiceText.Value = DefaultConfirmChoiceText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(CancelChoiceText.Value))
            {
                CancelChoiceText.Value = DefaultCancelChoiceText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(ResultText.Value))
            {
                ResultText.Value = DefaultResultText;
                changed = true;
            }
            else if (ResultText.Value == OldDefaultResultText)
            {
                ResultText.Value = DefaultResultText;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(ResultChoiceText.Value))
            {
                ResultChoiceText.Value = DefaultResultChoiceText;
                changed = true;
            }

            if (BackPic.Value == null)
            {
                BackPic.Value = DefaultBackPic;
                changed = true;
            }

            return changed;
        }

        internal static void Save()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                _category.SaveToFile();
            }
            catch (System.Exception ex)
            {
                Main.Log.Warning($"保存配置文件失败：{ex.Message}");
            }
        }
    }
}
