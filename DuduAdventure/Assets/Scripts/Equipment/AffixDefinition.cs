using UnityEngine;
using DuduAdventure.Stats;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 词条定义（ScriptableObject）—— 描述一种可能出现在装备上的词条
    /// </summary>
    /// <remarks>
    /// 例如：
    ///   "锋锐" → Attack +5~+15 (Flat)
    ///   "狂暴" → CritRate +3%~+8% (Percent)
    ///   "坚韧" → MaxHP +20~+60 (Flat)
    /// 
    /// 策划在 Unity 编辑器里创建这些 SO 资产，丢进一个文件夹。
    /// EquipmentTemplate 引用多个 AffixDefinition，装备掉落时从中随机选并 roll 数值。
    /// 
    /// 设计思路：
    /// - 一个 AffixDefinition 只描述一条属性的范围，不包含多条
    ///   （如果想做"复合词条"如"力量+10, 暴击+2%"，用两个 AffixDefinition 组合）
    /// - MinValue / MaxValue 是开区间随机 [min, max]
    /// - IsPercent 决定 roll 出来的数字填入 StatModifier 的 FlatBonus 还是 PercentBonus
    /// </remarks>
    [CreateAssetMenu(fileName = "NewAffix", menuName = "DuduAdventure/Equipment/Affix Definition")]
    public class AffixDefinition : ScriptableObject
    {
        [Header("词条基本信息")]
        [Tooltip("词条显示名称（给玩家看的）")]
        public string DisplayName = "新词条";

        [Tooltip("词条描述模板，用 {value} 占位。例：'攻击力 +{value}'")]
        public string DescriptionTemplate = "{stat} +{value}";

        [Header("属性效果")]
        [Tooltip("影响的属性类型")]
        public StatType AffectedStat = StatType.Attack;

        [Tooltip("是否为百分比加成（否则为固定值加成）")]
        public bool IsPercent;

        [Tooltip("最小 roll 值")]
        public float MinValue = 1f;

        [Tooltip("最大 roll 值")]
        public float MaxValue = 10f;

        [Header("稀有度限制")]
        [Tooltip("最低需要什么稀有度才能出现这条词条")]
        public Rarity MinRarity = Rarity.Common;

        /// <summary>
        /// 随机 roll 一个数值
        /// </summary>
        public float RollValue()
        {
            return Random.Range(MinValue, MaxValue);
        }

        /// <summary>
        /// 按稀有度缩放后 roll（高稀有度 roll 的下限更高）
        /// </summary>
        /// <remarks>
        /// 公式：value = Lerp(MinValue, MaxValue, Random(rarityFloor, 1))
        /// 稀有度越高，rarityFloor 越高，下限被拉高，但上限不变
        /// </remarks>
        public float RollValueByRarity(Rarity rarity)
        {
            // 稀有度 0~4 映射到下限系数 0~0.6
            // Common: floor=0 (全随机), Legendary: floor=0.6 (至少 60% 位置起步)
            float rarityFloor = (int)rarity * 0.15f;
            float t = Random.Range(rarityFloor, 1f);
            return Mathf.Lerp(MinValue, MaxValue, t);
        }

        /// <summary>
        /// 生成这条词条的 StatModifier
        /// </summary>
        public StatModifier CreateModifier(Rarity rarity)
        {
            float value = RollValueByRarity(rarity);

            if (IsPercent)
            {
                return new StatModifier(AffectedStat, 0f, value);
            }
            else
            {
                return new StatModifier(AffectedStat, value, 0f);
            }
        }
    }
}
