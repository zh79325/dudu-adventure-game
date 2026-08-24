using System;
using UnityEngine;

namespace DuduAdventure.Skill
{
    /// <summary>
    /// 技能消耗类型
    /// </summary>
    public enum SkillCostType
    {
        Mana,       // 消耗蓝量（小技能）
        FullRage    // 消耗全部怒气（大绝招，需满怒）
    }

    /// <summary>
    /// 技能效果类型
    /// </summary>
    public enum SkillEffectType
    {
        MeleeArea,      // 近战范围伤害（以自身为中心/前方扇形）
        Projectile,     // 投射物（向前发射）
        Dash,           // 冲刺攻击（位移 + 沿途伤害）
        Buff,           // 增益效果（给自己加 buff）
        GroundSlam      // 地面冲击波（全屏/大范围）
    }

    /// <summary>
    /// 技能定义 ScriptableObject - 配置一个技能的所有静态数据
    /// </summary>
    /// <remarks>
    /// DNF 风格技能设计：
    /// - 每个角色有 3~4 个小技能 + 1 个大绝招
    /// - 小技能消耗蓝量，有冷却
    /// - 大绝招消耗满怒气，冷却较长，伤害爆炸
    /// - 技能通过等级解锁
    /// </remarks>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "DuduAdventure/Skill/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        #region 基本信息

        [Header("基本信息")]
        [Tooltip("技能唯一 ID（与 LevelSystem 的 SkillUnlockEntry.SkillId 对应）")]
        public string SkillId;

        [Tooltip("显示名称")]
        public string DisplayName;

        [Tooltip("技能描述")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("技能图标")]
        public Sprite Icon;

        #endregion

        #region 消耗与冷却

        [Header("消耗与冷却")]
        [Tooltip("消耗类型")]
        public SkillCostType CostType = SkillCostType.Mana;

        [Tooltip("蓝量消耗（CostType=Mana 时生效）")]
        public float ManaCost = 20f;

        [Tooltip("冷却时间（秒）")]
        public float Cooldown = 5f;

        #endregion

        #region 伤害与效果

        [Header("伤害")]
        [Tooltip("基础伤害倍率（基于角色攻击力）")]
        public float DamageMultiplier = 2.0f;

        [Tooltip("命中次数（多段攻击）")]
        public int HitCount = 1;

        [Tooltip("命中间隔（多段时）")]
        public float HitInterval = 0.1f;

        [Header("效果类型")]
        [Tooltip("技能效果类型")]
        public SkillEffectType EffectType = SkillEffectType.MeleeArea;

        [Tooltip("效果范围半径")]
        public float EffectRadius = 2f;

        [Tooltip("效果距离（投射物飞行距离或冲刺距离）")]
        public float EffectDistance = 5f;

        [Tooltip("效果持续时间（秒）")]
        public float EffectDuration = 0.5f;

        [Tooltip("击退力度")]
        public float KnockbackForce = 8f;

        #endregion

        #region 动画与表现

        [Header("表现")]
        [Tooltip("施法前摇时间（秒）")]
        public float CastTime = 0.1f;

        [Tooltip("施法后摇时间（秒，施法后不可行动）")]
        public float RecoveryTime = 0.3f;

        [Tooltip("施法时是否锁定朝向")]
        public bool LockFacing = true;

        [Tooltip("施法时是否无敌")]
        public bool InvincibleDuringCast;

        #endregion

        #region 解锁条件

        [Header("解锁")]
        [Tooltip("解锁等级（0=初始就有）")]
        public int UnlockLevel;

        #endregion
    }
}
