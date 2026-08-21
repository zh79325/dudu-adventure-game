using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 掉落表（ScriptableObject）—— 配置一类敌人死亡时能掉什么装备
    /// </summary>
    /// <remarks>
    /// 用法：
    ///   1. 策划创建 LootTable SO，往 Entries 里添加条目（装备模板 + 权重）
    ///   2. 敌人 Prefab 上引用这个 LootTable
    ///   3. 敌人死亡时调用 LootTable.Roll() 获取一件随机装备
    /// 
    /// 权重机制：
    ///   Entry.Weight 是相对权重，不需要加起来等于 100。
    ///   例如 [金箍棒:10, 九齿钉耙:5, 禅杖:3] 总权重 18，
    ///   金箍棒掉率 = 10/18 ≈ 55.6%
    /// 
    /// 空掉落（Nothing）：
    ///   DropChance 控制"这次击杀是否产生掉落"，0.3 = 30% 概率掉东西。
    ///   先过 DropChance 门槛，再从 Entries 里按权重选。
    /// </remarks>
    [CreateAssetMenu(fileName = "NewLootTable", menuName = "DuduAdventure/Equipment/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Header("掉落概率")]
        [Tooltip("每次击杀掉落东西的概率 (0~1)。0.3 = 30% 会掉。")]
        [Range(0f, 1f)]
        public float DropChance = 0.3f;

        [Header("掉落条目")]
        public LootEntry[] Entries;

        /// <summary>
        /// 尝试 roll 一次掉落
        /// </summary>
        /// <returns>roll 中的装备实例，或 null 表示没掉</returns>
        public EquipmentInstance Roll()
        {
            // 先判断是否产生掉落
            if (UnityEngine.Random.value > DropChance)
                return null;

            // 按权重从 Entries 中选一个模板
            var template = SelectByWeight();
            if (template == null)
                return null;

            // 用模板生成实例
            return template.CreateInstance();
        }

        /// <summary>
        /// 强制掉落（无视 DropChance，用于 BOSS 保底）
        /// </summary>
        public EquipmentInstance RollGuaranteed()
        {
            var template = SelectByWeight();
            if (template == null)
                return null;
            return template.CreateInstance();
        }

        private EquipmentTemplate SelectByWeight()
        {
            if (Entries == null || Entries.Length == 0)
                return null;

            float totalWeight = 0f;
            foreach (var entry in Entries)
            {
                if (entry.Template != null)
                    totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
                return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;

            foreach (var entry in Entries)
            {
                if (entry.Template == null) continue;
                accumulated += entry.Weight;
                if (roll <= accumulated)
                    return entry.Template;
            }

            // 兜底（浮点精度）
            return Entries[Entries.Length - 1].Template;
        }
    }

    /// <summary>
    /// 掉落表条目
    /// </summary>
    [Serializable]
    public struct LootEntry
    {
        [Tooltip("装备模板")]
        public EquipmentTemplate Template;

        [Tooltip("相对权重（越大越容易掉）")]
        public float Weight;
    }
}
