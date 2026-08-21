using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 装备模板（ScriptableObject）—— 描述一类装备的蓝图
    /// </summary>
    /// <remarks>
    /// 一把"金箍棒"是一个模板，但掉落 10 次会产生 10 个不同的 EquipmentInstance，
    /// 每个实例有各自随机出来的词条和数值。这就是"暗黑式随机装备"的核心。
    /// 
    /// 策划的工作流：
    /// 1. 创建 AffixDefinition SO（各种词条模板）
    /// 2. 创建 EquipmentTemplate SO，填入名字、槽位、可能出的词条池
    /// 3. 把 EquipmentTemplate 配到 LootTable 里
    /// 4. 运行时敌人死亡 → LootTable 抽模板 → 模板.CreateInstance() 生成实例
    /// </remarks>
    [CreateAssetMenu(fileName = "NewEquipment", menuName = "DuduAdventure/Equipment/Equipment Template")]
    public class EquipmentTemplate : ScriptableObject
    {
        [Header("基本信息")]
        [Tooltip("装备显示名称")]
        public string DisplayName = "新装备";

        [Tooltip("装备描述")]
        [TextArea(2, 4)]
        public string Description = "";

        [Tooltip("装备图标（UI 用）")]
        public Sprite Icon;

        [Tooltip("装备槽位")]
        public EquipmentSlot Slot = EquipmentSlot.Weapon;

        [Header("稀有度配置")]
        [Tooltip("这件装备的固定稀有度。如果想让同一模板掉出不同稀有度，创建多个模板。")]
        public Rarity Rarity = Rarity.Common;

        [Header("词条池")]
        [Tooltip("这件装备可能 roll 出的词条。实际数量由稀有度决定。")]
        public AffixDefinition[] PossibleAffixes;

        [Header("词条数量（按稀有度）")]
        [Tooltip("各稀有度对应的词条数量，索引 = (int)Rarity。长度应为 5。")]
        public int[] AffixCountByRarity = { 1, 2, 2, 3, 4 };

        /// <summary>
        /// 根据模板生成一个运行时装备实例
        /// </summary>
        public EquipmentInstance CreateInstance()
        {
            int affixCount = GetAffixCount();
            var rolledAffixes = RollAffixes(affixCount);
            return new EquipmentInstance(this, rolledAffixes);
        }

        private int GetAffixCount()
        {
            int idx = (int)Rarity;
            if (idx < AffixCountByRarity.Length)
                return AffixCountByRarity[idx];
            return 1;
        }

        private List<RolledAffix> RollAffixes(int count)
        {
            var result = new List<RolledAffix>();

            if (PossibleAffixes == null || PossibleAffixes.Length == 0)
                return result;

            // 从可用词条池中随机不重复地选
            var pool = new List<AffixDefinition>();
            foreach (var affix in PossibleAffixes)
            {
                if (affix != null && affix.MinRarity <= Rarity)
                {
                    pool.Add(affix);
                }
            }

            // Fisher-Yates 洗牌后取前 N 个
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int actualCount = Mathf.Min(count, pool.Count);
            for (int i = 0; i < actualCount; i++)
            {
                var affix = pool[i];
                var modifier = affix.CreateModifier(Rarity);
                result.Add(new RolledAffix(affix, modifier));
            }

            return result;
        }
    }
}
