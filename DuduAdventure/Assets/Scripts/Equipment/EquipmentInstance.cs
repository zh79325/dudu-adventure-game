using System;
using System.Collections.Generic;
using DuduAdventure.Stats;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 已 roll 出的词条 —— 记录"哪条词条"+"roll 了多少"
    /// </summary>
    [Serializable]
    public struct RolledAffix
    {
        /// <summary>
        /// 词条定义引用（用于显示名称、描述模板）
        /// </summary>
        public AffixDefinition Definition;

        /// <summary>
        /// 实际 roll 出来的修改器（包含具体数值）
        /// </summary>
        public StatModifier Modifier;

        public RolledAffix(AffixDefinition def, StatModifier mod)
        {
            Definition = def;
            Modifier = mod;
        }
    }

    /// <summary>
    /// 装备实例 —— 一件已经"掉落"在世界中的具体装备
    /// </summary>
    /// <remarks>
    /// 和 EquipmentTemplate 的关系：
    ///   Template 是"金箍棒的蓝图"，Instance 是"你背包里那把攻击力 +37 暴击 +5% 的金箍棒"。
    /// 
    /// 生命周期：
    ///   1. 敌人死亡 → LootTable 选中一个 Template → Template.CreateInstance() → 生成本对象
    ///   2. 掉落物 GameObject 持有本对象，等玩家拾取
    ///   3. 拾取后存入背包（或直接穿上）
    ///   4. 穿上时，把 Modifiers 注入 CharacterStats
    ///   5. 脱下时，从 CharacterStats 移除
    /// 
    /// 序列化：
    ///   当前版本存内存就行（通关清除）。如果将来要存档，
    ///   需要把 RolledAffix 序列化成 JSON/Binary。
    /// </remarks>
    public class EquipmentInstance
    {
        /// <summary>
        /// 源模板引用
        /// </summary>
        public EquipmentTemplate Template { get; }

        /// <summary>
        /// 实际 roll 出来的词条列表
        /// </summary>
        public IReadOnlyList<RolledAffix> Affixes { get; }

        /// <summary>
        /// 唯一标识（用作 CharacterStats 的修改器来源 ID）
        /// </summary>
        public string InstanceId { get; }

        /// <summary>
        /// 快捷属性
        /// </summary>
        public string DisplayName => Template.DisplayName;
        public Rarity Rarity => Template.Rarity;
        public EquipmentSlot Slot => Template.Slot;

        public EquipmentInstance(EquipmentTemplate template, List<RolledAffix> affixes)
        {
            Template = template;
            Affixes = affixes;
            InstanceId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 把这件装备的词条转换为 StatModifier 列表（喂给 CharacterStats）
        /// </summary>
        public List<StatModifier> GetModifiers()
        {
            var result = new List<StatModifier>(Affixes.Count);
            foreach (var affix in Affixes)
            {
                result.Add(affix.Modifier);
            }
            return result;
        }
    }
}
