using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Stats
{
    /// <summary>
    /// 属性类型枚举
    /// </summary>
    /// <remarks>
    /// 这是整个属性系统的"通用语言"。装备词条、CharacterStats、伤害公式都用它来标识一条属性。
    /// 枚举值不用 Flags，纯索引用途。扩展属性直接往后面加就行，不会打破序列化。
    /// </remarks>
    public enum StatType
    {
        MaxHP,          // 生命上限
        Attack,         // 攻击力（物理）
        Defense,        // 防御力
        AttackSpeed,    // 攻击速度（倍率，1.0 = 基础）
        MoveSpeed,      // 移动速度（倍率，1.0 = 基础）
        CritRate,       // 暴击率（0~1）
        CritDamage,     // 暴击伤害倍率（1.5 = 150% 伤害）
    }

    /// <summary>
    /// 属性修改器 —— 一条"加多少"的记录
    /// </summary>
    /// <remarks>
    /// Flat = 加法叠加（+50 攻击）
    /// Percent = 乘法叠加（+20% 攻击 → 0.2）
    /// 
    /// 最终公式：finalValue = (baseValue + ΣFlat) * (1 + ΣPercent)
    /// 这是暗黑/DNF 类游戏最常见的两段聚合方式：
    ///   先把所有加法项堆上去，再一次性乘以百分比总和。
    ///   简单、可预测、玩家容易口算。
    /// </remarks>
    [Serializable]
    public struct StatModifier
    {
        public StatType StatType;
        public float FlatBonus;
        public float PercentBonus;

        public StatModifier(StatType type, float flat, float percent = 0f)
        {
            StatType = type;
            FlatBonus = flat;
            PercentBonus = percent;
        }
    }

    /// <summary>
    /// 角色属性组件 —— 挂在每个有属性的实体上（玩家、敌人）
    /// </summary>
    /// <remarks>
    /// 职责：
    /// 1. 持有基础属性（Inspector 可调）
    /// 2. 接受外部修改器（装备、Buff）
    /// 3. 计算最终属性并通知订阅者
    /// 
    /// 设计选择：
    /// - 用 Dictionary 存修改器列表，key 是来源标识（装备 ID / Buff ID）
    /// - 脏标记 + 懒计算：改修改器时标脏，读属性时才重算
    /// - 不继承 ScriptableObject：每个实体的属性是独立运行时实例
    /// </remarks>
    public class CharacterStats : MonoBehaviour
    {
        #region Inspector 基础属性

        [Header("基础属性（角色白板值）")]
        [SerializeField] private float _baseMaxHP = 100f;
        [SerializeField] private float _baseAttack = 10f;
        [SerializeField] private float _baseDefense = 2f;
        [SerializeField] private float _baseAttackSpeed = 1f;
        [SerializeField] private float _baseMoveSpeed = 1f;
        [SerializeField] private float _baseCritRate = 0.05f;
        [SerializeField] private float _baseCritDamage = 1.5f;

        #endregion

        #region 事件

        /// <summary>
        /// 属性发生变化时触发（UI 绑定用）
        /// </summary>
        public event Action OnStatsChanged;

        #endregion

        #region 运行时状态

        // 所有外部修改器，key = 来源 ID（装备 instanceId / buff ID）
        private readonly Dictionary<string, List<StatModifier>> _modifiers = new();

        // 缓存的最终属性
        private readonly Dictionary<StatType, float> _finalStats = new();

        // 脏标记
        private bool _isDirty = true;

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取某项属性的最终值
        /// </summary>
        public float GetStat(StatType type)
        {
            if (_isDirty) Recalculate();
            return _finalStats.TryGetValue(type, out float val) ? val : 0f;
        }

        /// <summary>
        /// 快捷属性
        /// </summary>
        public float MaxHP => GetStat(StatType.MaxHP);
        public float Attack => GetStat(StatType.Attack);
        public float Defense => GetStat(StatType.Defense);
        public float AttackSpeed => GetStat(StatType.AttackSpeed);
        public float MoveSpeed => GetStat(StatType.MoveSpeed);
        public float CritRate => GetStat(StatType.CritRate);
        public float CritDamage => GetStat(StatType.CritDamage);

        /// <summary>
        /// 添加一组修改器（装备穿上 / Buff 生效时调用）
        /// </summary>
        public void AddModifiers(string sourceId, List<StatModifier> mods)
        {
            _modifiers[sourceId] = mods;
            MarkDirty();
        }

        /// <summary>
        /// 移除一组修改器（脱装备 / Buff 结束时调用）
        /// </summary>
        public void RemoveModifiers(string sourceId)
        {
            if (_modifiers.Remove(sourceId))
            {
                MarkDirty();
            }
        }

        /// <summary>
        /// 清除所有外部修改器（重置到白板）
        /// </summary>
        public void ClearAllModifiers()
        {
            _modifiers.Clear();
            MarkDirty();
        }

        /// <summary>
        /// 判断一次攻击是否暴击（外部调用，传入随机数或直接用内部随机）
        /// </summary>
        public bool RollCrit()
        {
            return UnityEngine.Random.value < CritRate;
        }

        /// <summary>
        /// 计算一次攻击的最终伤害（含暴击判定）
        /// </summary>
        public (int damage, bool isCrit) CalculateAttackDamage(float comboMultiplier = 1f)
        {
            bool isCrit = RollCrit();
            float raw = Attack * comboMultiplier;
            if (isCrit) raw *= CritDamage;
            return (Mathf.RoundToInt(raw), isCrit);
        }

        #endregion

        #region 内部计算

        private void MarkDirty()
        {
            _isDirty = true;
        }

        private void Recalculate()
        {
            _isDirty = false;

            // 初始化为基础值
            Dictionary<StatType, float> flatSums = new();
            Dictionary<StatType, float> percentSums = new();

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                flatSums[type] = 0f;
                percentSums[type] = 0f;
            }

            // 累加所有修改器
            foreach (var modList in _modifiers.Values)
            {
                foreach (var mod in modList)
                {
                    flatSums[mod.StatType] += mod.FlatBonus;
                    percentSums[mod.StatType] += mod.PercentBonus;
                }
            }

            // 聚合公式: final = (base + ΣFlat) * (1 + ΣPercent)
            _finalStats[StatType.MaxHP] = (_baseMaxHP + flatSums[StatType.MaxHP]) * (1f + percentSums[StatType.MaxHP]);
            _finalStats[StatType.Attack] = (_baseAttack + flatSums[StatType.Attack]) * (1f + percentSums[StatType.Attack]);
            _finalStats[StatType.Defense] = (_baseDefense + flatSums[StatType.Defense]) * (1f + percentSums[StatType.Defense]);
            _finalStats[StatType.AttackSpeed] = (_baseAttackSpeed + flatSums[StatType.AttackSpeed]) * (1f + percentSums[StatType.AttackSpeed]);
            _finalStats[StatType.MoveSpeed] = (_baseMoveSpeed + flatSums[StatType.MoveSpeed]) * (1f + percentSums[StatType.MoveSpeed]);
            _finalStats[StatType.CritRate] = Mathf.Clamp01((_baseCritRate + flatSums[StatType.CritRate]) * (1f + percentSums[StatType.CritRate]));
            _finalStats[StatType.CritDamage] = (_baseCritDamage + flatSums[StatType.CritDamage]) * (1f + percentSums[StatType.CritDamage]);

            OnStatsChanged?.Invoke();
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            // 首次计算
            Recalculate();
        }

        #endregion
    }
}
