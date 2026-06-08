using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MailFilter.Main), "MailFilter", "1.1.0", "SaintCirno9", null)]
[assembly: MelonGame("TppStudio", "LongYinLiZhiZhuan")]

namespace MailFilter
{
    public enum MailCategory
    {
        Unknown = 0,
        PreloverJealousy = 1,
        HaterReconcile = 2,
        GenericVisitInvitation = 3,
        TreasureInvitation = 4,
        MartialArtsDiscussion = 5,
        BookExchange = 6,
        InsightVisitInvitation = 7,
        WealthVisitInvitation = 8,
        HaterAidRequest = 9,
        BanditAidRequest = 10,
        PrisonVisitInvitation = 11,
        SectBanquetInvitation = 12,
        RetirementFarewell = 13,
        SectDefenseRequest = 14,
        FriendlyForceAidRequest = 15
    }

    public enum HeroRelationLevel
    {
        AlwaysBlock = -1,
        Hater = 0,
        None = 1,
        Friend = 2,
        TeacherStudent = 3,
        Brother = 4,
        Relative = 5,
        Prelover = 6,
        Lover = 7
    }

    public sealed class Main : MelonMod
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        private static MailFilterConfig _config = new();

        internal static MelonLogger.Instance Log => Melon<Main>.Logger;
        internal static bool VerboseLoggingEnabled => _config.VerboseLogging;

        public override void OnInitializeMelon()
        {
            LoadConfig();
            Log.Msg("MailFilter 已加载。");
            Log.Msg("当前版本支持按已识别信件类型 + 角色关系阈值配置拦截。");
            Log.Msg($"配置文件：{GetConfigPath()}");
            Log.Msg($"当前开关：启用过滤={_config.EnableFilter}，仇敌和解={_config.BlockHaterReconcile}，仇家踪迹求援={_config.BlockHaterAidRequest}，贼人袭击求援={_config.BlockBanditAidRequest}，入狱探望={_config.BlockPrisonVisitInvitation}，门派宴会={_config.BlockSectBanquetInvitation}，退隐告别={_config.BlockRetirementFarewell}，门派遇袭={_config.BlockSectDefenseRequest}，友派求援={_config.BlockFriendlyForceAidRequest}，前任争风={_config.BlockPreloverJealousy}，普通邀约={_config.BlockGenericVisitInvitation}，探宝邀约={_config.BlockTreasureInvitation}，武学请教={_config.BlockMartialArtsDiscussion}，秘籍交换={_config.BlockBookExchange}，武学心得邀约={_config.BlockInsightVisitInvitation}，盈余邀约={_config.BlockWealthVisitInvitation}");
            Log.Msg("关系层级：仇敌 < 无特殊关系 < 友人 < 师徒 < 结义 < 亲属 < 前任 < 恋人。");
        }

        public override void OnUpdate()
        {
            MailFilterUi.HandleInput();
        }

        private static void LoadConfig()
        {
            string configPath = GetConfigPath();
            try
            {
                string configDirectory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                if (File.Exists(configPath))
                {
                    MailFilterConfig loadedConfig = JsonSerializer.Deserialize<MailFilterConfig>(File.ReadAllText(configPath), JsonOptions);
                    _config = loadedConfig ?? new MailFilterConfig();
                }
                else
                {
                    _config = new MailFilterConfig();
                }
            }
            catch (Exception ex)
            {
                _config = new MailFilterConfig();
                Log.Warning($"读取配置失败，将使用默认配置：{ex.Message}");
            }

            SaveConfig();
        }

        private static void SaveConfig()
        {
            string configPath = GetConfigPath();
            File.WriteAllText(configPath, JsonSerializer.Serialize(_config, JsonOptions));
        }

        private static string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserData", "MailFilter.json");
        }

        internal static bool ShouldFilterCategory(MailCategory category, HeroRelationLevel relationLevel)
        {
            if (!_config.EnableFilter)
            {
                return false;
            }

            return category switch
            {
                MailCategory.PreloverJealousy => ShouldFilterByRelation(_config.BlockPreloverJealousy, _config.PreloverJealousyKeepAtOrAboveRelation, relationLevel),
                MailCategory.HaterReconcile => ShouldFilterByRelation(_config.BlockHaterReconcile, _config.HaterReconcileKeepAtOrAboveRelation, relationLevel),
                MailCategory.HaterAidRequest => ShouldFilterByRelation(_config.BlockHaterAidRequest, _config.HaterAidRequestKeepAtOrAboveRelation, relationLevel),
                MailCategory.BanditAidRequest => ShouldFilterByRelation(_config.BlockBanditAidRequest, _config.BanditAidRequestKeepAtOrAboveRelation, relationLevel),
                MailCategory.PrisonVisitInvitation => ShouldFilterByRelation(_config.BlockPrisonVisitInvitation, _config.PrisonVisitInvitationKeepAtOrAboveRelation, relationLevel),
                MailCategory.SectBanquetInvitation => ShouldFilterByRelation(_config.BlockSectBanquetInvitation, _config.SectBanquetInvitationKeepAtOrAboveRelation, relationLevel),
                MailCategory.RetirementFarewell => ShouldFilterByRelation(_config.BlockRetirementFarewell, _config.RetirementFarewellKeepAtOrAboveRelation, relationLevel),
                MailCategory.SectDefenseRequest => ShouldFilterByRelation(_config.BlockSectDefenseRequest, _config.SectDefenseRequestKeepAtOrAboveRelation, relationLevel),
                MailCategory.FriendlyForceAidRequest => ShouldFilterByRelation(_config.BlockFriendlyForceAidRequest, _config.FriendlyForceAidRequestKeepAtOrAboveRelation, relationLevel),
                MailCategory.GenericVisitInvitation => ShouldFilterByRelation(_config.BlockGenericVisitInvitation, _config.GenericVisitInvitationKeepAtOrAboveRelation, relationLevel),
                MailCategory.TreasureInvitation => ShouldFilterByRelation(_config.BlockTreasureInvitation, _config.TreasureInvitationKeepAtOrAboveRelation, relationLevel),
                MailCategory.MartialArtsDiscussion => ShouldFilterByRelation(_config.BlockMartialArtsDiscussion, _config.MartialArtsDiscussionKeepAtOrAboveRelation, relationLevel),
                MailCategory.BookExchange => ShouldFilterByRelation(_config.BlockBookExchange, _config.BookExchangeKeepAtOrAboveRelation, relationLevel),
                MailCategory.InsightVisitInvitation => ShouldFilterByRelation(_config.BlockInsightVisitInvitation, _config.InsightVisitInvitationKeepAtOrAboveRelation, relationLevel),
                MailCategory.WealthVisitInvitation => ShouldFilterByRelation(_config.BlockWealthVisitInvitation, _config.WealthVisitInvitationKeepAtOrAboveRelation, relationLevel),
                _ => false
            };
        }

        internal static string GetMailCategoryName(MailCategory category)
        {
            return category switch
            {
                MailCategory.PreloverJealousy => "前任问罪信",
                MailCategory.HaterReconcile => "仇敌和解邀约信",
                MailCategory.HaterAidRequest => "仇家踪迹求援信",
                MailCategory.BanditAidRequest => "贼人袭击求援信",
                MailCategory.PrisonVisitInvitation => "入狱求探望信",
                MailCategory.SectBanquetInvitation => "门派宴会邀请信",
                MailCategory.RetirementFarewell => "退隐告别信",
                MailCategory.SectDefenseRequest => "门派领地告急信",
                MailCategory.FriendlyForceAidRequest => "友好门派求援信",
                MailCategory.GenericVisitInvitation => "小聚邀约信",
                MailCategory.TreasureInvitation => "探宝邀约信",
                MailCategory.MartialArtsDiscussion => "偷秘籍邀约信",
                MailCategory.BookExchange => "秘籍交换信",
                MailCategory.InsightVisitInvitation => "武学心得小聚信",
                MailCategory.WealthVisitInvitation => "手头宽裕小聚信",
                _ => "未分类"
            };
        }

        internal static string GetRelationLevelName(HeroRelationLevel relationLevel)
        {
            return relationLevel switch
            {
                HeroRelationLevel.AlwaysBlock => "始终拦截",
                HeroRelationLevel.Hater => "仇敌",
                HeroRelationLevel.None => "无特殊关系",
                HeroRelationLevel.Friend => "友人",
                HeroRelationLevel.TeacherStudent => "师徒",
                HeroRelationLevel.Brother => "结义",
                HeroRelationLevel.Relative => "亲属",
                HeroRelationLevel.Prelover => "前任",
                HeroRelationLevel.Lover => "恋人",
                _ => "未分类关系"
            };
        }

        internal static bool GetEnableFilter()
        {
            return _config.EnableFilter;
        }

        internal static void SetEnableFilter(bool enabled)
        {
            if (_config.EnableFilter == enabled)
            {
                return;
            }

            _config.EnableFilter = enabled;
            SaveConfig();
            Log.Msg($"已通过界面更新总开关：EnableFilter={enabled}");
        }

        internal static bool GetVerboseLogging()
        {
            return _config.VerboseLogging;
        }

        internal static void SetVerboseLogging(bool enabled)
        {
            if (_config.VerboseLogging == enabled)
            {
                return;
            }

            _config.VerboseLogging = enabled;
            SaveConfig();
            Log.Msg($"已通过界面更新日志开关：VerboseLogging={enabled}");
        }

        internal static bool GetCategoryEnabled(MailCategory category)
        {
            return category switch
            {
                MailCategory.PreloverJealousy => _config.BlockPreloverJealousy,
                MailCategory.HaterReconcile => _config.BlockHaterReconcile,
                MailCategory.HaterAidRequest => _config.BlockHaterAidRequest,
                MailCategory.BanditAidRequest => _config.BlockBanditAidRequest,
                MailCategory.PrisonVisitInvitation => _config.BlockPrisonVisitInvitation,
                MailCategory.SectBanquetInvitation => _config.BlockSectBanquetInvitation,
                MailCategory.RetirementFarewell => _config.BlockRetirementFarewell,
                MailCategory.SectDefenseRequest => _config.BlockSectDefenseRequest,
                MailCategory.FriendlyForceAidRequest => _config.BlockFriendlyForceAidRequest,
                MailCategory.GenericVisitInvitation => _config.BlockGenericVisitInvitation,
                MailCategory.TreasureInvitation => _config.BlockTreasureInvitation,
                MailCategory.MartialArtsDiscussion => _config.BlockMartialArtsDiscussion,
                MailCategory.BookExchange => _config.BlockBookExchange,
                MailCategory.InsightVisitInvitation => _config.BlockInsightVisitInvitation,
                MailCategory.WealthVisitInvitation => _config.BlockWealthVisitInvitation,
                _ => false
            };
        }

        internal static void SetCategoryEnabled(MailCategory category, bool enabled)
        {
            bool changed = false;
            switch (category)
            {
                case MailCategory.PreloverJealousy:
                    changed = _config.BlockPreloverJealousy != enabled;
                    _config.BlockPreloverJealousy = enabled;
                    break;
                case MailCategory.HaterReconcile:
                    changed = _config.BlockHaterReconcile != enabled;
                    _config.BlockHaterReconcile = enabled;
                    break;
                case MailCategory.HaterAidRequest:
                    changed = _config.BlockHaterAidRequest != enabled;
                    _config.BlockHaterAidRequest = enabled;
                    break;
                case MailCategory.BanditAidRequest:
                    changed = _config.BlockBanditAidRequest != enabled;
                    _config.BlockBanditAidRequest = enabled;
                    break;
                case MailCategory.PrisonVisitInvitation:
                    changed = _config.BlockPrisonVisitInvitation != enabled;
                    _config.BlockPrisonVisitInvitation = enabled;
                    break;
                case MailCategory.SectBanquetInvitation:
                    changed = _config.BlockSectBanquetInvitation != enabled;
                    _config.BlockSectBanquetInvitation = enabled;
                    break;
                case MailCategory.RetirementFarewell:
                    changed = _config.BlockRetirementFarewell != enabled;
                    _config.BlockRetirementFarewell = enabled;
                    break;
                case MailCategory.SectDefenseRequest:
                    changed = _config.BlockSectDefenseRequest != enabled;
                    _config.BlockSectDefenseRequest = enabled;
                    break;
                case MailCategory.FriendlyForceAidRequest:
                    changed = _config.BlockFriendlyForceAidRequest != enabled;
                    _config.BlockFriendlyForceAidRequest = enabled;
                    break;
                case MailCategory.GenericVisitInvitation:
                    changed = _config.BlockGenericVisitInvitation != enabled;
                    _config.BlockGenericVisitInvitation = enabled;
                    break;
                case MailCategory.TreasureInvitation:
                    changed = _config.BlockTreasureInvitation != enabled;
                    _config.BlockTreasureInvitation = enabled;
                    break;
                case MailCategory.MartialArtsDiscussion:
                    changed = _config.BlockMartialArtsDiscussion != enabled;
                    _config.BlockMartialArtsDiscussion = enabled;
                    break;
                case MailCategory.BookExchange:
                    changed = _config.BlockBookExchange != enabled;
                    _config.BlockBookExchange = enabled;
                    break;
                case MailCategory.InsightVisitInvitation:
                    changed = _config.BlockInsightVisitInvitation != enabled;
                    _config.BlockInsightVisitInvitation = enabled;
                    break;
                case MailCategory.WealthVisitInvitation:
                    changed = _config.BlockWealthVisitInvitation != enabled;
                    _config.BlockWealthVisitInvitation = enabled;
                    break;
            }

            if (!changed)
            {
                return;
            }

            SaveConfig();
            Log.Msg($"已通过界面更新邮件开关：类型={GetMailCategoryName(category)}，启用过滤={enabled}");
        }

        internal static HeroRelationLevel GetKeepAtOrAboveRelation(MailCategory category)
        {
            return category switch
            {
                MailCategory.PreloverJealousy => _config.PreloverJealousyKeepAtOrAboveRelation,
                MailCategory.HaterReconcile => _config.HaterReconcileKeepAtOrAboveRelation,
                MailCategory.HaterAidRequest => _config.HaterAidRequestKeepAtOrAboveRelation,
                MailCategory.BanditAidRequest => _config.BanditAidRequestKeepAtOrAboveRelation,
                MailCategory.PrisonVisitInvitation => _config.PrisonVisitInvitationKeepAtOrAboveRelation,
                MailCategory.SectBanquetInvitation => _config.SectBanquetInvitationKeepAtOrAboveRelation,
                MailCategory.RetirementFarewell => _config.RetirementFarewellKeepAtOrAboveRelation,
                MailCategory.SectDefenseRequest => _config.SectDefenseRequestKeepAtOrAboveRelation,
                MailCategory.FriendlyForceAidRequest => _config.FriendlyForceAidRequestKeepAtOrAboveRelation,
                MailCategory.GenericVisitInvitation => _config.GenericVisitInvitationKeepAtOrAboveRelation,
                MailCategory.TreasureInvitation => _config.TreasureInvitationKeepAtOrAboveRelation,
                MailCategory.MartialArtsDiscussion => _config.MartialArtsDiscussionKeepAtOrAboveRelation,
                MailCategory.BookExchange => _config.BookExchangeKeepAtOrAboveRelation,
                MailCategory.InsightVisitInvitation => _config.InsightVisitInvitationKeepAtOrAboveRelation,
                MailCategory.WealthVisitInvitation => _config.WealthVisitInvitationKeepAtOrAboveRelation,
                _ => HeroRelationLevel.AlwaysBlock
            };
        }

        internal static void SetKeepAtOrAboveRelation(MailCategory category, HeroRelationLevel relationLevel)
        {
            bool changed = false;
            switch (category)
            {
                case MailCategory.PreloverJealousy:
                    changed = _config.PreloverJealousyKeepAtOrAboveRelation != relationLevel;
                    _config.PreloverJealousyKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.HaterReconcile:
                    changed = _config.HaterReconcileKeepAtOrAboveRelation != relationLevel;
                    _config.HaterReconcileKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.HaterAidRequest:
                    changed = _config.HaterAidRequestKeepAtOrAboveRelation != relationLevel;
                    _config.HaterAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.BanditAidRequest:
                    changed = _config.BanditAidRequestKeepAtOrAboveRelation != relationLevel;
                    _config.BanditAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.PrisonVisitInvitation:
                    changed = _config.PrisonVisitInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.PrisonVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.SectBanquetInvitation:
                    changed = _config.SectBanquetInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.SectBanquetInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.RetirementFarewell:
                    changed = _config.RetirementFarewellKeepAtOrAboveRelation != relationLevel;
                    _config.RetirementFarewellKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.SectDefenseRequest:
                    changed = _config.SectDefenseRequestKeepAtOrAboveRelation != relationLevel;
                    _config.SectDefenseRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.FriendlyForceAidRequest:
                    changed = _config.FriendlyForceAidRequestKeepAtOrAboveRelation != relationLevel;
                    _config.FriendlyForceAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.GenericVisitInvitation:
                    changed = _config.GenericVisitInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.GenericVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.TreasureInvitation:
                    changed = _config.TreasureInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.TreasureInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.MartialArtsDiscussion:
                    changed = _config.MartialArtsDiscussionKeepAtOrAboveRelation != relationLevel;
                    _config.MartialArtsDiscussionKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.BookExchange:
                    changed = _config.BookExchangeKeepAtOrAboveRelation != relationLevel;
                    _config.BookExchangeKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.InsightVisitInvitation:
                    changed = _config.InsightVisitInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.InsightVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.WealthVisitInvitation:
                    changed = _config.WealthVisitInvitationKeepAtOrAboveRelation != relationLevel;
                    _config.WealthVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
            }

            if (!changed)
            {
                return;
            }

            SaveConfig();
            Log.Msg($"已通过界面更新关系阈值：类型={GetMailCategoryName(category)}，保留关系>={GetRelationLevelName(relationLevel)}");
        }

        internal static void SetCategoryFilterState(MailCategory category, bool enabled, HeroRelationLevel relationLevel)
        {
            bool enabledChanged = GetCategoryEnabled(category) != enabled;
            bool relationChanged = GetKeepAtOrAboveRelation(category) != relationLevel;
            if (!enabledChanged && !relationChanged)
            {
                return;
            }

            switch (category)
            {
                case MailCategory.PreloverJealousy:
                    _config.BlockPreloverJealousy = enabled;
                    _config.PreloverJealousyKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.HaterReconcile:
                    _config.BlockHaterReconcile = enabled;
                    _config.HaterReconcileKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.HaterAidRequest:
                    _config.BlockHaterAidRequest = enabled;
                    _config.HaterAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.BanditAidRequest:
                    _config.BlockBanditAidRequest = enabled;
                    _config.BanditAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.PrisonVisitInvitation:
                    _config.BlockPrisonVisitInvitation = enabled;
                    _config.PrisonVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.SectBanquetInvitation:
                    _config.BlockSectBanquetInvitation = enabled;
                    _config.SectBanquetInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.RetirementFarewell:
                    _config.BlockRetirementFarewell = enabled;
                    _config.RetirementFarewellKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.SectDefenseRequest:
                    _config.BlockSectDefenseRequest = enabled;
                    _config.SectDefenseRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.FriendlyForceAidRequest:
                    _config.BlockFriendlyForceAidRequest = enabled;
                    _config.FriendlyForceAidRequestKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.GenericVisitInvitation:
                    _config.BlockGenericVisitInvitation = enabled;
                    _config.GenericVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.TreasureInvitation:
                    _config.BlockTreasureInvitation = enabled;
                    _config.TreasureInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.MartialArtsDiscussion:
                    _config.BlockMartialArtsDiscussion = enabled;
                    _config.MartialArtsDiscussionKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.BookExchange:
                    _config.BlockBookExchange = enabled;
                    _config.BookExchangeKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.InsightVisitInvitation:
                    _config.BlockInsightVisitInvitation = enabled;
                    _config.InsightVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                case MailCategory.WealthVisitInvitation:
                    _config.BlockWealthVisitInvitation = enabled;
                    _config.WealthVisitInvitationKeepAtOrAboveRelation = relationLevel;
                    break;
                default:
                    return;
            }

            SaveConfig();
            string stateText = enabled
                ? (relationLevel == HeroRelationLevel.AlwaysBlock
                    ? "直接拦截"
                    : $"保留>={GetRelationLevelName(relationLevel)}")
                : "不拦截";
            Log.Msg($"已通过界面更新邮件规则：类型={GetMailCategoryName(category)}，状态={stateText}");
        }

        private static bool ShouldFilterByRelation(bool enabled, HeroRelationLevel keepAtOrAboveRelation, HeroRelationLevel actualRelation)
        {
            if (!enabled)
            {
                return false;
            }

            if (keepAtOrAboveRelation == HeroRelationLevel.AlwaysBlock)
            {
                return true;
            }

            return (int)actualRelation < (int)keepAtOrAboveRelation;
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.GetNewMail))]
    public static class MailFilterPatch
    {
        public sealed class MailFilterState
        {
            public bool ShouldRemove;
            public int MailCountBefore;
            public string SenderName = "系统";
            public string MailTitle = "未命名信件";
            public string MailText = string.Empty;
            public float SenderFavor;
            public HeroRelationLevel SenderRelation;
            public MailCategory MailCategory;
            public bool SenderInterestingStar;
        }

        [HarmonyPrefix]
        public static void Prefix(GameController __instance, MailData targetMail, HeroData sourceHero, ref MailFilterState __state)
        {
            HeroData resolvedSourceHero = ResolveSourceHero(__instance?.worldData, targetMail, sourceHero);
            List<MailData> mailDatas = __instance?.worldData?.MailDatas;
            MailCategory mailCategory = RecognizeMailCategory(__instance?.worldData, targetMail, resolvedSourceHero);
            HeroRelationLevel senderRelation = GetRelationLevel(__instance?.worldData, resolvedSourceHero);
            bool senderInterestingStar = resolvedSourceHero != null && resolvedSourceHero.interestingStar;
            __state = new MailFilterState
            {
                MailCountBefore = mailDatas?.Count ?? -1,
                SenderName = string.IsNullOrEmpty(resolvedSourceHero?.heroName) ? "系统" : resolvedSourceHero.heroName,
                MailTitle = string.IsNullOrEmpty(targetMail?.mailTitle) ? "未命名信件" : targetMail.mailTitle,
                MailText = targetMail?.mailText ?? string.Empty,
                SenderFavor = resolvedSourceHero?.Favor(false) ?? 0f,
                SenderRelation = senderRelation,
                MailCategory = mailCategory,
                SenderInterestingStar = senderInterestingStar,
                ShouldRemove = !senderInterestingStar && Main.ShouldFilterCategory(mailCategory, senderRelation)
            };
        }

        [HarmonyPostfix]
        public static void Postfix(GameController __instance, MailFilterState __state)
        {
            if (__state == null)
            {
                return;
            }

            if (!__state.ShouldRemove)
            {
                if (__state.MailCategory != MailCategory.Unknown && Main.VerboseLoggingEnabled)
                {
                    string starTag = __state.SenderInterestingStar ? "，星标角色=是" : string.Empty;
                    Main.Log.Msg($"已识别来信：类型={Main.GetMailCategoryName(__state.MailCategory)}，发送者={__state.SenderName}，关系={Main.GetRelationLevelName(__state.SenderRelation)}，标题={__state.MailTitle}，好感度={__state.SenderFavor}{starTag}");
                }

                return;
            }

            List<MailData> mailDatas = __instance?.worldData?.MailDatas;
            if (mailDatas == null || __state.MailCountBefore < 0 || mailDatas.Count <= __state.MailCountBefore)
            {
                Main.Log.Warning($"检测到需要过滤的来信，但未找到新增邮件记录。类型={Main.GetMailCategoryName(__state.MailCategory)}，发送者={__state.SenderName}，关系={Main.GetRelationLevelName(__state.SenderRelation)}，标题={__state.MailTitle}，好感度={__state.SenderFavor}");
                return;
            }

            mailDatas.RemoveAt(mailDatas.Count - 1);

            MissionUIController missionUi = MissionUIController.Instance;
            missionUi?.RefreshMailTable();
            missionUi?.RefreshMailNewIcon();

            Main.Log.Msg($"已过滤来信：类型={Main.GetMailCategoryName(__state.MailCategory)}，发送者={__state.SenderName}，关系={Main.GetRelationLevelName(__state.SenderRelation)}，标题={__state.MailTitle}，好感度={__state.SenderFavor}");
        }

        internal static HeroData ResolveSourceHero(WorldData worldData, MailData targetMail, HeroData sourceHero)
        {
            if (sourceHero != null)
            {
                return sourceHero;
            }

            string mailTitle = targetMail?.mailTitle;
            if (worldData == null || string.IsNullOrEmpty(mailTitle))
            {
                return null;
            }

            return worldData.GetHero(mailTitle);
        }

        internal static MailCategory RecognizeMailCategory(WorldData worldData, MailData targetMail, HeroData sourceHero)
        {
            if (worldData == null || targetMail == null)
            {
                return MailCategory.Unknown;
            }

            HeroData player = worldData.Player();
            if (player == null)
            {
                return MailCategory.Unknown;
            }

            if (sourceHero != null && sourceHero.heroID == player.heroID)
            {
                return MailCategory.Unknown;
            }

            string mailText = targetMail.mailText ?? string.Empty;
            if (string.IsNullOrEmpty(mailText))
            {
                return MailCategory.Unknown;
            }

            if (ContainsAll(mailText, "听说", "新欢", "翻脸不认人") && sourceHero?.HavePrelover(player.heroID) == true)
            {
                return MailCategory.PreloverJealousy;
            }

            if (Contains(mailText, "冤家宜解不宜结") && sourceHero?.HaveHater(player.heroID) == true)
            {
                return MailCategory.HaterReconcile;
            }

            if (IsAidRequestMail(mailText, "寻得仇家踪迹"))
            {
                return MailCategory.HaterAidRequest;
            }

            if (IsAidRequestMail(mailText, "遭到贼人袭击"))
            {
                return MailCategory.BanditAidRequest;
            }

            if (IsPrisonVisitInvitationMail(mailText))
            {
                return MailCategory.PrisonVisitInvitation;
            }

            if (IsSectBanquetInvitationMail(mailText))
            {
                return MailCategory.SectBanquetInvitation;
            }

            if (IsRetirementFarewellMail(mailText))
            {
                return MailCategory.RetirementFarewell;
            }

            if (IsSectDefenseRequestMail(mailText))
            {
                return MailCategory.SectDefenseRequest;
            }

            if (IsFriendlyForceAidMail(mailText))
            {
                return MailCategory.FriendlyForceAidRequest;
            }

            if (Contains(mailText, "我近日探得一处宝藏消息"))
            {
                return MailCategory.TreasureInvitation;
            }

            if (ContainsAll(mailText, "久闻", "武学高深莫测", "共商大计"))
            {
                return MailCategory.MartialArtsDiscussion;
            }

            if (ContainsAll(mailText, "得到一", "秘籍", "交换一二"))
            {
                return MailCategory.BookExchange;
            }

            if (Contains(mailText, "近日忽有武学心得"))
            {
                return MailCategory.InsightVisitInvitation;
            }

            if (Contains(mailText, "近日手头颇有盈余"))
            {
                return MailCategory.WealthVisitInvitation;
            }

            if (Contains(mailText, "与吾小聚一番") && Contains(mailText, "我近日"))
            {
                return MailCategory.GenericVisitInvitation;
            }

            return MailCategory.Unknown;
        }

        private static bool Contains(string source, string value)
        {
            return source?.Contains(value, StringComparison.Ordinal) == true;
        }

        private static bool ContainsAll(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null || values.Length <= 0)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!Contains(source, values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null || values.Length <= 0)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Contains(source, values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAidRequestMail(string mailText, string encounterText)
        {
            return ContainsAll(mailText, "我在", "若#PlayerName#能在", "日内赶来助阵", "必当感激不尽")
                && Contains(mailText, encounterText);
        }

        private static bool IsPrisonVisitInvitationMail(string mailText)
        {
            return ContainsAll(mailText, "说来惭愧。近日我一时失手", "抓入狱中", "你若得空不妨来探望一番");
        }

        private static bool IsSectBanquetInvitationMail(string mailText)
        {
            return ContainsAll(mailText, "本月#PlayerForceName#将在门派祠堂中举办一场宴会", "不妨前来与师兄弟们一聚");
        }

        private static bool IsRetirementFarewellMail(string mailText)
        {
            return ContainsAll(mailText, "我近来心生退隐江湖之意", "能来与吾告别", "往日一番情谊");
        }

        private static bool IsSectDefenseRequestMail(string mailText)
        {
            return ContainsAll(mailText, "本门<b>", "遭敌对门派", "危在旦夕")
                && ContainsAny(mailText, "望掌门亲往协助防御", "望掌门亲往指挥防御", "还望各路门客尽快前去支援");
        }

        private static bool IsFriendlyForceAidMail(string mailText)
        {
            return ContainsAll(mailText, "友好门派", "遭敌对门派", "危在旦夕", "向本门求助")
                && ContainsAny(mailText, "望掌门亲往协助防御", "现令各部弟子速速前去支援", "还望各路门客尽快前去支援");
        }

        internal static HeroRelationLevel GetRelationLevel(WorldData worldData, HeroData sourceHero)
        {
            HeroData player = worldData?.Player();
            if (player == null || sourceHero == null)
            {
                return HeroRelationLevel.None;
            }

            int playerHeroId = player.heroID;
            if (sourceHero.Lover == playerHeroId)
            {
                return HeroRelationLevel.Lover;
            }

            if (sourceHero.HavePrelover(playerHeroId))
            {
                return HeroRelationLevel.Prelover;
            }

            if (ContainsHeroId(sourceHero.Relatives, playerHeroId))
            {
                return HeroRelationLevel.Relative;
            }

            if (sourceHero.HaveBrother(playerHeroId))
            {
                return HeroRelationLevel.Brother;
            }

            if (sourceHero.HaveTeacherStudentRelation(playerHeroId))
            {
                return HeroRelationLevel.TeacherStudent;
            }

            if (sourceHero.HaveFriend(playerHeroId))
            {
                return HeroRelationLevel.Friend;
            }

            if (sourceHero.HaveHater(playerHeroId))
            {
                return HeroRelationLevel.Hater;
            }

            return HeroRelationLevel.None;
        }

        private static bool ContainsHeroId(List<int> heroIds, int heroId)
        {
            if (heroIds == null)
            {
                return false;
            }

            for (int i = 0; i < heroIds.Count; i++)
            {
                if (heroIds[i] == heroId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(InfoController), nameof(InfoController.AddMail))]
    public static class MailFilterAddMailPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MailData newMail)
        {
            GameController gameController = GameController.Instance;
            WorldData worldData = gameController?.worldData;
            HeroData sourceHero = MailFilterPatch.ResolveSourceHero(worldData, newMail, null);
            MailCategory mailCategory = MailFilterPatch.RecognizeMailCategory(worldData, newMail, sourceHero);
            HeroRelationLevel senderRelation = MailFilterPatch.GetRelationLevel(worldData, sourceHero);
            bool senderInterestingStar = sourceHero != null && sourceHero.interestingStar;

            if (senderInterestingStar || !Main.ShouldFilterCategory(mailCategory, senderRelation))
            {
                return true;
            }

            string senderName = string.IsNullOrEmpty(sourceHero?.heroName) ? "系统" : sourceHero.heroName;
            string mailTitle = string.IsNullOrEmpty(newMail?.mailTitle) ? "未命名信件" : newMail.mailTitle;
            float senderFavor = sourceHero?.Favor(false) ?? 0f;
            Main.Log.Msg($"已在入队前过滤来信：类型={Main.GetMailCategoryName(mailCategory)}，发送者={senderName}，关系={Main.GetRelationLevelName(senderRelation)}，标题={mailTitle}，好感度={senderFavor}");
            return false;
        }
    }

    public sealed class MailFilterConfig
    {
        public bool EnableFilter { get; set; } = true;
        public bool BlockPreloverJealousy { get; set; }
        public bool BlockHaterReconcile { get; set; } = true;
        public bool BlockHaterAidRequest { get; set; }
        public bool BlockBanditAidRequest { get; set; }
        public bool BlockPrisonVisitInvitation { get; set; }
        public bool BlockSectBanquetInvitation { get; set; }
        public bool BlockRetirementFarewell { get; set; }
        public bool BlockSectDefenseRequest { get; set; }
        public bool BlockFriendlyForceAidRequest { get; set; }
        public bool BlockGenericVisitInvitation { get; set; }
        public bool BlockTreasureInvitation { get; set; }
        public bool BlockMartialArtsDiscussion { get; set; }
        public bool BlockBookExchange { get; set; }
        public bool BlockInsightVisitInvitation { get; set; }
        public bool BlockWealthVisitInvitation { get; set; }
        public HeroRelationLevel PreloverJealousyKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel HaterReconcileKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel HaterAidRequestKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel BanditAidRequestKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel PrisonVisitInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel SectBanquetInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel RetirementFarewellKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel SectDefenseRequestKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel FriendlyForceAidRequestKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel GenericVisitInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel TreasureInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel MartialArtsDiscussionKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel BookExchangeKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel InsightVisitInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public HeroRelationLevel WealthVisitInvitationKeepAtOrAboveRelation { get; set; } = HeroRelationLevel.AlwaysBlock;
        public bool VerboseLogging { get; set; }
    }

    internal static class MailFilterUi
    {
        private const string ConfigButtonName = "MailFilter_ConfigButton";
        private const string ConfigPanelName = "MailFilter_ConfigPanel";
        private const float ConfigButtonOffsetX = -12f;
        private const int CategoryStateCount = 1 + 8;
        private static readonly MailCategory[] CategoryOrder =
        {
            MailCategory.HaterReconcile,
            MailCategory.HaterAidRequest,
            MailCategory.BanditAidRequest,
            MailCategory.PrisonVisitInvitation,
            MailCategory.SectBanquetInvitation,
            MailCategory.RetirementFarewell,
            MailCategory.SectDefenseRequest,
            MailCategory.FriendlyForceAidRequest,
            MailCategory.PreloverJealousy,
            MailCategory.GenericVisitInvitation,
            MailCategory.TreasureInvitation,
            MailCategory.MartialArtsDiscussion,
            MailCategory.BookExchange,
            MailCategory.InsightVisitInvitation,
            MailCategory.WealthVisitInvitation
        };

        private static readonly HeroRelationLevel[] RelationCycleOrder =
        {
            HeroRelationLevel.AlwaysBlock,
            HeroRelationLevel.None,
            HeroRelationLevel.Friend,
            HeroRelationLevel.TeacherStudent,
            HeroRelationLevel.Brother,
            HeroRelationLevel.Relative,
            HeroRelationLevel.Prelover,
            HeroRelationLevel.Lover
        };

        private static readonly System.Collections.Generic.List<CategoryRowWidgets> CategoryRows = new();
        private static GameObject _buttonObject;
        private static GameObject _panelObject;
        private static RectTransform _buttonRect;
        private static RectTransform _panelRect;
        private static Text _buttonLabel;
        private static Text _summaryLabel;
        private static Text _globalFilterButtonLabel;
        private static Text _verboseLoggingButtonLabel;
        private static bool _panelVisible;

        private sealed class CategoryRowWidgets
        {
            public MailCategory Category;
            public Text PreviousLabel;
            public Text StateLabel;
            public Text NextLabel;
        }

        internal static void UpdateMissionUi(MissionUIController controller)
        {
            if (!ShouldShowUi(controller))
            {
                SetObjectActive(_buttonObject, false);
                SetObjectActive(_panelObject, false);
                return;
            }

            RectTransform mailTableRect = controller.mailTable != null ? controller.mailTable.GetComponent<RectTransform>() : null;
            RectTransform parentRect = GetUiRootRect(controller);
            RectTransform missionUiRect = controller.missionUI != null
                ? controller.missionUI.GetComponent<RectTransform>()
                : null;
            if (mailTableRect == null || parentRect == null)
            {
                return;
            }

            Text templateText = FindTemplateText(controller);
            if (templateText == null)
            {
                return;
            }

            RectTransform tabReferenceRect = GetMailTabReferenceRect(controller, mailTableRect);
            RectTransform buttonParentRect = GetButtonParentRect(tabReferenceRect, missionUiRect, parentRect);
            EnsureConfigButton(buttonParentRect, templateText);
            EnsureConfigPanel(parentRect, templateText);
            PositionButton(tabReferenceRect, mailTableRect, buttonParentRect);
            PositionPanel(parentRect);
            RefreshButtonVisual();
            RefreshPanelVisual();

            SetObjectActive(_buttonObject, true);
            SetObjectActive(_panelObject, _panelVisible);
        }

        internal static void HandleInput()
        {
            if (!_panelVisible || _panelObject == null || !_panelObject.activeInHierarchy)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _panelVisible = false;
                SetObjectActive(_panelObject, false);
                RefreshButtonVisual();
            }
        }

        private static bool ShouldShowUi(MissionUIController controller)
        {
            return controller != null
                && controller.showUI
                && controller.missionUI != null
                && controller.missionUI.activeInHierarchy
                && controller.mailTable != null
                && controller.mailTable.activeInHierarchy;
        }

        private static Text FindTemplateText(MissionUIController controller)
        {
            if (controller?.missionUI == null)
            {
                return null;
            }

            Text clearText = FindTextByContent(controller.missionUI.transform, "清空已读信件");
            if (clearText != null)
            {
                return clearText;
            }

            return FindFirstText(controller.missionUI.transform);
        }

        private static void EnsureConfigButton(RectTransform parentRect, Text templateText)
        {
            if (parentRect == null)
            {
                return;
            }

            if (_buttonObject != null && _buttonObject.transform.parent != parentRect.transform)
            {
                UnityEngine.Object.Destroy(_buttonObject);
                _buttonObject = null;
                _buttonRect = null;
                _buttonLabel = null;
            }

            if (_buttonObject != null)
            {
                return;
            }

                _buttonObject = new GameObject(ConfigButtonName);
            _buttonRect = _buttonObject.AddComponent<RectTransform>();
            _buttonRect.SetParent(parentRect, false);
            _buttonRect.sizeDelta = new Vector2(62f, 26f);

            Image background = _buttonObject.AddComponent<Image>();
            background.color = new Color(0.42f, 0.17f, 0.17f, 0.93f);

            Outline outline = _buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = _buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener((UnityAction)TogglePanelVisible);

            _buttonLabel = CreateText("Label", _buttonRect, templateText, 16, TextAnchor.MiddleCenter);
            RectTransform labelRect = _buttonLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static void EnsureConfigPanel(RectTransform parentRect, Text templateText)
        {
            if (_panelObject != null && _panelObject.transform.parent != parentRect.transform)
            {
                UnityEngine.Object.Destroy(_panelObject);
                _panelObject = null;
                _panelRect = null;
                _summaryLabel = null;
                _globalFilterButtonLabel = null;
                _verboseLoggingButtonLabel = null;
                CategoryRows.Clear();
            }

            if (_panelObject != null)
            {
                return;
            }

            _panelObject = new GameObject(ConfigPanelName);
            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.SetParent(parentRect, false);
            _panelRect.sizeDelta = new Vector2(470f, 750f);

            Image panelBackground = _panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            Outline panelOutline = _panelObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.60f, 0.44f, 0.9f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            Text titleText = CreateText("Title", _panelRect, templateText, 24, TextAnchor.MiddleLeft);
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(14f, -36f);
            titleRect.offsetMax = new Vector2(-80f, -8f);
            titleText.text = "MailFilter 过滤设置";
            titleText.color = new Color(0.96f, 0.92f, 0.84f, 1f);

            GameObject closeButtonObject = CreateSmallActionButton(
                "CloseButton",
                _panelRect,
                templateText,
                "×",
                new Vector2(30f, 30f),
                () => _panelVisible = false,
                new Color(0.68f, 0.14f, 0.14f, 0.96f),
                new Color(1f, 0.92f, 0.92f, 1f));
            RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -8f);

            _summaryLabel = CreateText("Summary", _panelRect, templateText, 14, TextAnchor.UpperLeft);
            RectTransform summaryRect = _summaryLabel.GetComponent<RectTransform>();
            summaryRect.anchorMin = new Vector2(0f, 1f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.pivot = new Vector2(0f, 1f);
            summaryRect.offsetMin = new Vector2(14f, -126f);
            summaryRect.offsetMax = new Vector2(-16f, -34f);
            _summaryLabel.color = new Color(0.86f, 0.86f, 0.86f, 1f);
            _summaryLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _summaryLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _summaryLabel.resizeTextForBestFit = false;

            CreateSectionHeader(_panelRect, templateText, "全局设置", -148f);
            CreateLabel(
                "GlobalFilterLabel",
                _panelRect,
                templateText,
                18,
                TextAnchor.MiddleLeft,
                "启用过滤",
                new Vector2(14f, -182f),
                new Vector2(100f, 28f));
            _globalFilterButtonLabel = CreateRowWithActionButton(
                _panelRect,
                templateText,
                string.Empty,
                "GlobalFilterButton",
                -182f,
                118f,
                108f,
                () => Main.SetEnableFilter(!Main.GetEnableFilter()));

            CreateLabel(
                "VerboseLoggingLabel",
                _panelRect,
                templateText,
                18,
                TextAnchor.MiddleLeft,
                "详细日志",
                new Vector2(240f, -182f),
                new Vector2(100f, 28f));
            _verboseLoggingButtonLabel = CreateRowWithActionButton(
                _panelRect,
                templateText,
                string.Empty,
                "VerboseButton",
                -182f,
                344f,
                108f,
                () => Main.SetVerboseLogging(!Main.GetVerboseLogging()));

            CreateSectionHeader(_panelRect, templateText, "按信件类型设置", -248f);

            float startY = -278f;
            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                MailCategory category = CategoryOrder[i];
                float rowY = startY - i * 31f;

                CreateLabel(
                    $"CategoryLabel_{i}",
                    _panelRect,
                    templateText,
                    18,
                    TextAnchor.MiddleLeft,
                    Main.GetMailCategoryName(category),
                    new Vector2(14f, rowY),
                    new Vector2(168f, 28f));

                Text previousLabel = CreateRowWithActionButton(
                    _panelRect,
                    templateText,
                    "◀",
                    $"CategoryPrev_{i}",
                    rowY,
                    190f,
                    28f,
                    () => CycleCategoryFilterState(category, -1));

                Text stateLabel = CreateRowWithActionButton(
                    _panelRect,
                    templateText,
                    string.Empty,
                    $"CategoryState_{i}",
                    rowY,
                    224f,
                    168f,
                    () => CycleCategoryFilterState(category, 1));

                Text nextLabel = CreateRowWithActionButton(
                    _panelRect,
                    templateText,
                    "▶",
                    $"CategoryNext_{i}",
                    rowY,
                    400f,
                    28f,
                    () => CycleCategoryFilterState(category, 1));

                CategoryRows.Add(new CategoryRowWidgets
                {
                    Category = category,
                    PreviousLabel = previousLabel,
                    StateLabel = stateLabel,
                    NextLabel = nextLabel
                });
            }

            SetObjectActive(_panelObject, false);
        }

        private static void PositionButton(RectTransform tabReferenceRect, RectTransform mailTableRect, RectTransform parentRect)
        {
            if (_buttonRect == null || parentRect == null)
            {
                return;
            }

            RectTransform referenceRect = tabReferenceRect != null ? tabReferenceRect : mailTableRect;
            if (referenceRect == null)
            {
                return;
            }

            if (referenceRect.parent == parentRect.transform)
            {
                _buttonRect.anchorMin = referenceRect.anchorMin;
                _buttonRect.anchorMax = referenceRect.anchorMax;
                _buttonRect.pivot = new Vector2(0.5f, 0.5f);

                Vector2 anchoredPosition = referenceRect.anchoredPosition;
                float offsetY = (referenceRect.rect.height * 0.5f) + (_buttonRect.rect.height * 0.5f) + 8f;
                _buttonRect.anchoredPosition = new Vector2(anchoredPosition.x + ConfigButtonOffsetX, anchoredPosition.y - offsetY);
                return;
            }

            _buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            _buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            _buttonRect.pivot = new Vector2(0.5f, 0.5f);

            Vector3[] corners = new Vector3[4];
            referenceRect.GetWorldCorners(corners);
            Vector3 localBottomCenter = parentRect.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);
            _buttonRect.anchoredPosition = new Vector2(localBottomCenter.x + ConfigButtonOffsetX, localBottomCenter.y - 22f);
        }

        private static void PositionPanel(RectTransform parentRect)
        {
            if (_panelRect == null)
            {
                return;
            }

            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchoredPosition = Vector2.zero;
        }

        private static void RefreshButtonVisual()
        {
            if (_buttonLabel != null)
            {
                _buttonLabel.text = "过滤";
                _buttonLabel.color = _panelVisible
                    ? new Color(1f, 0.94f, 0.78f, 1f)
                    : new Color(0.95f, 0.95f, 0.95f, 1f);
            }
        }

        private static void RefreshPanelVisual()
        {
            if (_panelObject == null)
            {
                return;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = "规则：每类邮件使用左右按钮切换状态。\n顺序为“不拦截 -> 直接拦截 -> 保留>=无特殊关系 -> ... -> 保留>=恋人”。\n星标/收藏角色的来信始终不拦截。按 Esc 可直接关闭面板。";
            }

            if (_globalFilterButtonLabel != null)
            {
                _globalFilterButtonLabel.text = Main.GetEnableFilter() ? "已开启" : "已关闭";
                _globalFilterButtonLabel.color = Main.GetEnableFilter() ? new Color(0.90f, 1f, 0.90f, 1f) : new Color(1f, 0.86f, 0.86f, 1f);
            }

            if (_verboseLoggingButtonLabel != null)
            {
                _verboseLoggingButtonLabel.text = Main.GetVerboseLogging() ? "已开启" : "已关闭";
                _verboseLoggingButtonLabel.color = Main.GetVerboseLogging() ? new Color(0.90f, 1f, 0.90f, 1f) : new Color(1f, 0.86f, 0.86f, 1f);
            }

            foreach (CategoryRowWidgets row in CategoryRows)
            {
                bool enabled = Main.GetCategoryEnabled(row.Category);
                HeroRelationLevel relationLevel = Main.GetKeepAtOrAboveRelation(row.Category);

                if (row.StateLabel != null)
                {
                    row.StateLabel.text = GetCategoryStateButtonText(enabled, relationLevel);
                    row.StateLabel.color = enabled ? new Color(0.94f, 0.94f, 0.94f, 1f) : new Color(1f, 0.86f, 0.86f, 1f);
                }

                if (row.PreviousLabel != null)
                {
                    row.PreviousLabel.text = "◀";
                    row.PreviousLabel.color = new Color(0.94f, 0.94f, 0.94f, 1f);
                }

                if (row.NextLabel != null)
                {
                    row.NextLabel.text = "▶";
                    row.NextLabel.color = new Color(0.94f, 0.94f, 0.94f, 1f);
                }
            }
        }

        private static string GetCategoryStateButtonText(bool enabled, HeroRelationLevel relationLevel)
        {
            if (!enabled)
            {
                return "不拦截";
            }

            return relationLevel == HeroRelationLevel.AlwaysBlock
                ? "直接拦截"
                : $"保留>={Main.GetRelationLevelName(relationLevel)}";
        }

        private static void CycleCategoryFilterState(MailCategory category, int direction)
        {
            int currentIndex = GetCategoryStateIndex(category);
            int nextIndex = currentIndex + direction;
            if (nextIndex < 0)
            {
                nextIndex = CategoryStateCount - 1;
            }
            else if (nextIndex >= CategoryStateCount)
            {
                nextIndex = 0;
            }

            ApplyCategoryStateByIndex(category, nextIndex);
        }

        private static int GetCategoryStateIndex(MailCategory category)
        {
            if (!Main.GetCategoryEnabled(category))
            {
                return 0;
            }

            HeroRelationLevel relationLevel = NormalizeRelationForCycle(Main.GetKeepAtOrAboveRelation(category));
            int relationIndex = Array.IndexOf(RelationCycleOrder, relationLevel);
            return relationIndex < 0 ? 1 : relationIndex + 1;
        }

        private static void ApplyCategoryStateByIndex(MailCategory category, int stateIndex)
        {
            if (stateIndex <= 0)
            {
                Main.SetCategoryFilterState(category, false, NormalizeRelationForCycle(Main.GetKeepAtOrAboveRelation(category)));
                return;
            }

            int relationIndex = Math.Min(stateIndex - 1, RelationCycleOrder.Length - 1);
            Main.SetCategoryFilterState(category, true, RelationCycleOrder[relationIndex]);
        }

        private static HeroRelationLevel NormalizeRelationForCycle(HeroRelationLevel relationLevel)
        {
            return relationLevel == HeroRelationLevel.Hater ? HeroRelationLevel.AlwaysBlock : relationLevel;
        }

        private static void TogglePanelVisible()
        {
            _panelVisible = !_panelVisible;
            RefreshButtonVisual();
            SetObjectActive(_panelObject, _panelVisible);
        }

        private static void CreateSectionHeader(RectTransform parent, Text templateText, string content, float y)
        {
            Text header = CreateLabel("SectionHeader_" + content, parent, templateText, 18, TextAnchor.MiddleLeft, content, new Vector2(14f, y), new Vector2(220f, 24f));
            header.color = new Color(0.98f, 0.88f, 0.64f, 1f);
        }

        private static Text CreateRowWithActionButton(
            RectTransform parent,
            Text templateText,
            string leftLabel,
            string buttonName,
            float y,
            float x,
            float width,
            Action onClick)
        {
            GameObject buttonObject = CreateSmallActionButton(buttonName, parent, templateText, leftLabel, new Vector2(width, 28f), onClick, new Color(0.22f, 0.22f, 0.22f, 0.92f), new Color(0.94f, 0.94f, 0.94f, 1f));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(x, y);

            Transform labelTransform = buttonObject.transform.Find("Label");
            return labelTransform != null ? labelTransform.GetComponent<Text>() : null;
        }

        private static GameObject CreateSmallActionButton(
            string name,
            RectTransform parent,
            Text templateText,
            string content,
            Vector2 size,
            Action onClick,
            Color backgroundColor,
            Color textColor)
        {
            GameObject buttonObject = new GameObject(name);
            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.sizeDelta = size;

            Image background = buttonObject.AddComponent<Image>();
            background.color = backgroundColor;

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener((UnityAction)(() =>
            {
                onClick?.Invoke();
                RefreshPanelVisual();
            }));

            Text label = CreateText("Label", rectTransform, templateText, 17, TextAnchor.MiddleCenter);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.text = content;
            label.color = textColor;

            return buttonObject;
        }

        private static Text CreateLabel(
            string name,
            RectTransform parent,
            Text templateText,
            int fontSize,
            TextAnchor alignment,
            string content,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Text label = CreateText(name, parent, templateText, fontSize, alignment);
            RectTransform rectTransform = label.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            label.text = content;
            return label;
        }

        private static Text CreateText(string name, RectTransform parent, Text templateText, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            RectTransform rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            Text label = textObject.AddComponent<Text>();
            label.font = templateText.font;
            label.fontSize = Math.Max(fontSize, templateText.fontSize);
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.color = new Color(0.94f, 0.94f, 0.94f, 1f);

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            return label;
        }

        private static Text FindTextByContent(Transform root, string content)
        {
            if (root == null)
            {
                return null;
            }

            Text selfText = root.GetComponent<Text>();
            if (selfText != null && string.Equals(selfText.text, content, StringComparison.Ordinal))
            {
                return selfText;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Text found = FindTextByContent(child, content);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Text FindFirstText(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Text selfText = root.GetComponent<Text>();
            if (selfText != null)
            {
                return selfText;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Text found = FindFirstText(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static RectTransform GetMailTabReferenceRect(MissionUIController controller, RectTransform mailTableRect)
        {
            if (controller?.missionUI != null)
            {
                var candidates = new System.Collections.Generic.List<Text>();
                FindActiveTextsByContent(controller.missionUI.transform, "信笺", candidates);
                if (candidates.Count > 0)
                {
                    Text bestText = null;
                    float bestDistance = float.MaxValue;
                    Vector3 targetPoint = mailTableRect != null ? mailTableRect.position : controller.missionUI.transform.position;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        Text candidate = candidates[i];
                        float distance = Vector3.Distance(candidate.transform.position, targetPoint);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestText = candidate;
                        }
                    }

                    if (bestText != null)
                    {
                        Transform current = bestText.transform;
                        for (int i = 0; i < 5 && current != null; i++)
                        {
                            RectTransform currentRect = current.GetComponent<RectTransform>();
                            if (currentRect != null && current.gameObject.activeInHierarchy)
                            {
                                Button button = current.GetComponent<Button>();
                                Toggle toggle = current.GetComponent<Toggle>();
                                if (button != null || toggle != null)
                                {
                                    return currentRect;
                                }
                            }

                            current = current.parent;
                        }
                        RectTransform textRect = bestText.GetComponent<RectTransform>();
                        if (textRect != null)
                        {
                            return textRect;
                        }
                    }
                }
            }

            if (controller?.toggleButton == null)
            {
                return null;
            }

            int activeIndex = controller.nowShowType;
            if (activeIndex >= 0 && activeIndex < controller.toggleButton.Count)
            {
                GameObject activeButton = controller.toggleButton[activeIndex];
                if (activeButton != null)
                {
                    RectTransform activeRect = activeButton.GetComponent<RectTransform>();
                    if (activeRect != null)
                    {
                        return activeRect;
                    }
                }
            }

            for (int i = 0; i < controller.toggleButton.Count; i++)
            {
                GameObject button = controller.toggleButton[i];
                if (button != null && button.activeInHierarchy)
                {
                    Toggle toggle = button.GetComponent<Toggle>();
                    if (toggle != null && toggle.isOn)
                    {
                        RectTransform buttonRect = button.GetComponent<RectTransform>();
                        if (buttonRect != null)
                        {
                            return buttonRect;
                        }
                    }
                }
            }

            return null;
        }

        private static void FindActiveTextsByContent(Transform root, string content, System.Collections.Generic.List<Text> result)
        {
            if (root == null || result == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            Text selfText = root.GetComponent<Text>();
            if (selfText != null
                && selfText.gameObject.activeInHierarchy
                && string.Equals(selfText.text, content, StringComparison.Ordinal))
            {
                result.Add(selfText);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                FindActiveTextsByContent(child, content, result);
            }
        }

        private static RectTransform GetUiRootRect(MissionUIController controller)
        {
            if (controller?.missionUI == null)
            {
                return null;
            }

            Transform rootTransform = controller.missionUI.transform.root;
            if (rootTransform != null)
            {
                RectTransform rootRect = rootTransform.GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    return rootRect;
                }
            }

            return controller.missionUI.transform.parent != null
                ? controller.missionUI.transform.parent.GetComponent<RectTransform>()
                : controller.missionUI.GetComponent<RectTransform>();
        }

        private static RectTransform GetButtonParentRect(RectTransform tabReferenceRect, RectTransform missionUiRect, RectTransform fallbackRect)
        {
            if (tabReferenceRect?.parent != null)
            {
                RectTransform parentRect = tabReferenceRect.parent.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    return parentRect;
                }
            }

            return missionUiRect ?? fallbackRect;
        }
    }

    [HarmonyPatch(typeof(MissionUIController), nameof(MissionUIController.Update))]
    public static class MailFilterMissionUiPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MissionUIController __instance)
        {
            MailFilterUi.UpdateMissionUi(__instance);
        }
    }
}
