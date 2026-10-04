using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Il2Cpp;

namespace LongYinCustomKungfuSkill
{
    internal static class AuctionBookSelfTest
    {
        internal static readonly string RequestPath = Path.Combine(Main.ConfigDir, "RunAuctionSelfTest.once");
        internal static readonly string ReportPath = Path.Combine(Main.ConfigDir, "AuctionSelfTest.txt");
        private static bool _requested;
        private static int _round;
        private static int _bookCount;
        private static int _customBookCount;
        private static int _xianxiaBookCount;
        private static readonly HashSet<int> SeenCustomIds = new();
        private static readonly StringBuilder Details = new();

        internal static void Initialize()
        {
            _round = 0;
            _bookCount = 0;
            _customBookCount = 0;
            _xianxiaBookCount = 0;
            SeenCustomIds.Clear();
            Details.Clear();
            _requested = File.Exists(RequestPath);
            if (_requested)
            {
                Main.LOG.Msg("[拍卖自检] 已预约一次自检，游戏数据就绪后模拟拍卖，不向背包或存档添加物品。");
            }
        }

        internal static void Update()
        {
            if (!_requested)
            {
                return;
            }

            GameDataController gameData = GameDataController.Instance;
            GameController game = GameController.Instance;
            PlotController plot = PlotController.Instance;
            if (gameData?.kungfuSkillDataBase == null || gameData.kungfuSkillDataBase.Count == 0 ||
                game?.worldData == null || plot == null)
            {
                return;
            }

            try
            {
                if (_round == 0)
                {
                    File.Delete(RequestPath);
                    Main.LOG.Msg("[拍卖自检] 开始，每帧模拟一轮，仅生成内存中的临时秘籍。");
                }

                string before = SnapshotPools(gameData);
                var items = new ItemListData();
                var itemTypes = new Il2CppSystem.Collections.Generic.List<int>();
                itemTypes.Add((int)ItemType.Book);
                plot.GenerateAuctionItem(items, (_round % 6) * 2f, itemTypes, 32);
                if (PlotController_GenerateAuctionItem_Patch.CurrentPool != null)
                {
                    throw new InvalidOperationException("拍卖结束后仍残留候选池作用域。");
                }

                if (!string.Equals(before, SnapshotPools(gameData), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("模拟拍卖改变了江湖或门派原生功法池。");
                }

                for (int i = 0; i < items.allItem.Count; i++)
                {
                    ItemData book = items.allItem[i];
                    KungfuSkillData skill = book?.bookData?.DataBase();
                    if (book?.type != ItemType.Book || skill == null || skill.hide)
                    {
                        throw new InvalidOperationException("模拟拍卖返回了非秘籍、无效或隐藏功法。");
                    }

                    _bookCount++;
                    if (Main.IsInjectedCustomSkillId(skill.skillID))
                    {
                        _customBookCount++;
                        if (skill.belongForceID == 25)
                        {
                            _xianxiaBookCount++;
                        }

                        if (SeenCustomIds.Add(skill.skillID))
                        {
                            Details.AppendLine($"抽到：{skill.name}，功法ID={skill.skillID}，所属势力={skill.belongForceID}，等级={skill.rareLv}");
                        }
                    }
                }

                _round++;
                // 先取足够样本；尚未抽到仙霞派时继续，但不把随机未命中判为功能故障。
                if ((_round >= 24 && _xianxiaBookCount > 0) || _round >= 96)
                {
                    Finish(_xianxiaBookCount > 0 ? "通过" : "样本中未抽到仙霞派，结果未确认");
                }
            }
            catch (Exception ex)
            {
                _requested = false;
                Main.LOG.Error($"[拍卖自检] 失败：{ex}");
                WriteReport($"失败：{ex}");
            }
        }

        private static string SnapshotPools(GameDataController gameData)
        {
            var snapshot = new StringBuilder();
            var forceIds = new List<int>();
            foreach (int forceId in gameData.kungfuSkillDataList.Keys)
            {
                forceIds.Add(forceId);
            }

            foreach (int forceId in forceIds.OrderBy(id => id))
            {
                var levels = gameData.kungfuSkillDataList[forceId];
                snapshot.Append(forceId).Append(':');
                for (int level = 0; level < levels.Count; level++)
                {
                    snapshot.Append('[');
                    var skills = levels[level];
                    for (int i = 0; i < skills.Count; i++)
                    {
                        snapshot.Append(skills[i].skillID).Append(',');
                    }

                    snapshot.Append(']');
                }
            }

            return snapshot.ToString();
        }

        private static void Finish(string result)
        {
            _requested = false;
            Main.LOG.Msg($"[拍卖自检] {result}：生成 {_bookCount} 本秘籍，新增功法 {_customBookCount} 本，仙霞派 {_xianxiaBookCount} 本；原生池保持不变。报告：{ReportPath}");
            WriteReport(result);
        }

        private static void WriteReport(string result)
        {
            try
            {
                File.WriteAllText(ReportPath,
                    $"拍卖自检：{result}\r\n时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
                    $"轮数：{_round}；秘籍：{_bookCount}；新增功法：{_customBookCount}；仙霞派：{_xianxiaBookCount}\r\n" +
                    "测试物品仅存在于临时内存列表，不添加到背包或存档。\r\n" + Details,
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[拍卖自检] 写入报告失败：{ex}");
            }
        }
    }
}
