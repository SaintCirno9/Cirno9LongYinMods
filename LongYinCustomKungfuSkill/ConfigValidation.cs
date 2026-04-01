using System;
using System.Globalization;

namespace LongYinCustomKungfuSkill
{
    internal sealed class ConfigColumnMap
    {
        public ConfigColumnMap(string[] headers)
        {
            Id = Main.FindColumnIndex(headers, "id");
            Type = Main.FindColumnIndex(headers, "类别");
            Level = Main.FindColumnIndex(headers, "等级");
            Name = Main.FindColumnIndex(headers, "名字");
            BaseDamage = Main.FindColumnIndex(headers, "基础威力");
            ExpRatio = Main.FindColumnIndex(headers, "经验系数");
            ManaCost = Main.FindColumnIndex(headers, "内力消耗");
            BelongForce = Main.FindColumnIndex(headers, "所属势力");
            UseTarget = Main.FindColumnIndex(headers, "使用目标");
            AttackRange = Main.FindColumnIndex(headers, "攻击范围");
            DamageRange = Main.FindColumnIndex(headers, "伤害范围");
            SummonId = Main.FindColumnIndex(headers, "召唤ID");
            UseCount = Main.FindColumnIndex(headers, "使用次数");
            Action = Main.FindColumnIndex(headers, "动作");
            DamageOrder = Main.FindColumnIndex(headers, "伤害顺序");
            AutoMove = Main.FindColumnIndex(headers, "角色位移");
            Trail = Main.FindColumnIndex(headers, "Trail");
            Hidden = Main.FindColumnIndex(headers, "隐藏");
        }

        public int Id { get; }
        public int Type { get; }
        public int Level { get; }
        public int Name { get; }
        public int BaseDamage { get; }
        public int ExpRatio { get; }
        public int ManaCost { get; }
        public int BelongForce { get; }
        public int UseTarget { get; }
        public int AttackRange { get; }
        public int DamageRange { get; }
        public int SummonId { get; }
        public int UseCount { get; }
        public int Action { get; }
        public int DamageOrder { get; }
        public int AutoMove { get; }
        public int Trail { get; }
        public int Hidden { get; }
    }

    internal static class ConfigRowValidator
    {
        private static readonly System.Collections.Generic.HashSet<string> ValidSkillTypes = new(StringComparer.Ordinal)
        {
            "内功", "轻功", "绝技", "拳掌", "剑法", "刀法", "长兵", "奇门", "射术"
        };

        private static readonly System.Collections.Generic.HashSet<string> ValidDamageOrders = new(StringComparer.Ordinal)
        {
            "Same", "Distance", "Random"
        };

        private static readonly System.Collections.Generic.HashSet<string> ValidAutoMoveValues = new(StringComparer.Ordinal)
        {
            "TRUE", "FALSE"
        };

        private static readonly System.Collections.Generic.HashSet<string> ValidHiddenValues = new(StringComparer.Ordinal)
        {
            "0", "1"
        };

        private static readonly System.Collections.Generic.HashSet<int> ValidUseTargets = new()
        {
            0, 1, 4
        };

        private static readonly System.Collections.Generic.HashSet<string> StrictDamageSkillTypes = new(StringComparer.Ordinal)
        {
            "拳掌", "剑法", "刀法", "长兵", "射术"
        };

        private static readonly System.Collections.Generic.HashSet<string> RangeActionSkillTypes = new(StringComparer.Ordinal)
        {
            "拳掌", "剑法", "刀法", "长兵", "射术", "奇门"
        };

        internal static bool TryValidateRow(string[] cells, int rowNum, string sourceName, ConfigColumnMap map, out int skillId)
        {
            skillId = -1;
            try
            {
                if (!TryParseRequiredInt(cells, map.Id, out skillId))
                {
                    LogInvalidRow(sourceName, rowNum, "功法 ID 无效");
                    return false;
                }

                if (skillId < 0)
                {
                    LogInvalidRow(sourceName, rowNum, "功法 ID 不能小于 0");
                    return false;
                }

                string type = Main.GetCell(cells, map.Type);
                if (!ValidSkillTypes.Contains(type))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别无效：{type}");
                    return false;
                }

                if (!TryParseRequiredInt(cells, map.Level, out int level) || level < 0 || level > 5)
                {
                    LogInvalidRow(sourceName, rowNum, $"等级无效：{Main.GetCell(cells, map.Level)}");
                    return false;
                }

                string name = Main.GetCell(cells, map.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    LogInvalidRow(sourceName, rowNum, "名称为空");
                    return false;
                }

                if (!TryParseRequiredFloat(cells, map.ExpRatio, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"经验系数无效：{Main.GetCell(cells, map.ExpRatio)}");
                    return false;
                }

                if (!TryParseOptionalFloat(cells, map.BaseDamage, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"基础威力无效：{Main.GetCell(cells, map.BaseDamage)}");
                    return false;
                }

                string summonIdText = Main.GetCell(cells, map.SummonId);
                bool hasSummonId = !string.IsNullOrWhiteSpace(summonIdText);

                if (StrictDamageSkillTypes.Contains(type) &&
                    !hasSummonId &&
                    string.IsNullOrWhiteSpace(Main.GetCell(cells, map.BaseDamage)))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别 {type} 必须填写基础威力");
                    return false;
                }

                if (!TryParseRequiredFloat(cells, map.ManaCost, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"内力消耗无效：{Main.GetCell(cells, map.ManaCost)}");
                    return false;
                }

                if (!TryParseRequiredInt(cells, map.BelongForce, out int belongForce) || belongForce < -1 || belongForce > 29)
                {
                    LogInvalidRow(sourceName, rowNum, $"所属势力无效：{Main.GetCell(cells, map.BelongForce)}");
                    return false;
                }

                if (!TryParseRequiredInt(cells, map.UseTarget, out int useTarget) || !ValidUseTargets.Contains(useTarget))
                {
                    LogInvalidRow(sourceName, rowNum, $"使用目标无效：{Main.GetCell(cells, map.UseTarget)}");
                    return false;
                }

                if (!ValidateTripleIntField(cells, map.AttackRange))
                {
                    LogInvalidRow(sourceName, rowNum, $"攻击范围格式无效：{Main.GetCell(cells, map.AttackRange)}");
                    return false;
                }

                if (RangeActionSkillTypes.Contains(type) && string.IsNullOrWhiteSpace(Main.GetCell(cells, map.AttackRange)))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别 {type} 必须填写攻击范围");
                    return false;
                }

                if (!ValidateTripleIntField(cells, map.DamageRange))
                {
                    LogInvalidRow(sourceName, rowNum, $"伤害范围格式无效：{Main.GetCell(cells, map.DamageRange)}");
                    return false;
                }

                if (RangeActionSkillTypes.Contains(type) && string.IsNullOrWhiteSpace(Main.GetCell(cells, map.DamageRange)))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别 {type} 必须填写伤害范围");
                    return false;
                }

                if (!TryParseOptionalInt(cells, map.SummonId, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"召唤ID无效：{Main.GetCell(cells, map.SummonId)}");
                    return false;
                }

                if (!TryParseOptionalInt(cells, map.UseCount, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"使用次数无效：{Main.GetCell(cells, map.UseCount)}");
                    return false;
                }

                string action = Main.GetCell(cells, map.Action);
                if (RangeActionSkillTypes.Contains(type) && string.IsNullOrWhiteSpace(action))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别 {type} 必须填写动作");
                    return false;
                }

                string damageOrder = Main.GetCell(cells, map.DamageOrder);
                if (!string.IsNullOrWhiteSpace(damageOrder) && !ValidDamageOrders.Contains(damageOrder))
                {
                    LogInvalidRow(sourceName, rowNum, $"伤害顺序无效：{damageOrder}");
                    return false;
                }

                if (RangeActionSkillTypes.Contains(type) && string.IsNullOrWhiteSpace(damageOrder))
                {
                    LogInvalidRow(sourceName, rowNum, $"类别 {type} 必须填写伤害顺序");
                    return false;
                }

                string autoMove = Main.GetCell(cells, map.AutoMove);
                if (!ValidAutoMoveValues.Contains(autoMove))
                {
                    LogInvalidRow(sourceName, rowNum, $"角色位移无效：{autoMove}");
                    return false;
                }

                if (!TryParseRequiredInt(cells, map.Trail, out _))
                {
                    LogInvalidRow(sourceName, rowNum, $"Trail 无效：{Main.GetCell(cells, map.Trail)}");
                    return false;
                }

                string hidden = Main.GetCell(cells, map.Hidden);
                if (!ValidHiddenValues.Contains(hidden))
                {
                    LogInvalidRow(sourceName, rowNum, $"隐藏无效：{hidden}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Main.LOG.Error($"[{sourceName}] 第 {rowNum} 行校验异常：{ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        private static void LogInvalidRow(string sourceName, int rowNum, string reason)
        {
            Main.LOG.Warning($"[{sourceName}] 第 {rowNum} 行数据不合规，已跳过：{reason}");
        }

        private static bool TryParseRequiredInt(string[] cells, int index, out int value)
        {
            string text = Main.GetCell(cells, index);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseOptionalInt(string[] cells, int index, out int value)
        {
            string text = Main.GetCell(cells, index);
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0;
                return true;
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseRequiredFloat(string[] cells, int index, out float value)
        {
            string text = Main.GetCell(cells, index);
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseOptionalFloat(string[] cells, int index, out float value)
        {
            string text = Main.GetCell(cells, index);
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0f;
                return true;
            }

            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool ValidateTripleIntField(string[] cells, int index)
        {
            string text = Main.GetCell(cells, index);
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string[] parts = text.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
