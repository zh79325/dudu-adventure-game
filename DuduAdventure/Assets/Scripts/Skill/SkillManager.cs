using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuduAdventure.Stats;
using DuduAdventure.Player;
using DuduAdventure.Combat;

namespace DuduAdventure.Skill
{
    /// <summary>
    /// 技能输入槽位（对应键位）
    /// </summary>
    public enum SkillSlot
    {
        Skill1 = 0,     // 小技能 1（默认 U）
        Skill2 = 1,     // 小技能 2（默认 I）
        Skill3 = 2,     // 小技能 3（默认 O）
        Ultimate = 3    // 大绝招（默认 P）
    }

    /// <summary>
    /// 技能管理器 - 管理角色技能槽位、冷却和释放
    /// </summary>
    /// <remarks>
    /// DNF 风格操作：
    /// - 4 个技能槽位（3 小技能 + 1 绝招）
    /// - 按键直接释放，无需瞄准
    /// - 释放时有前摇/后摇，期间不可移动
    /// - 小技能消耗蓝量，绝招消耗满怒气
    /// </remarks>
    [RequireComponent(typeof(CharacterStats))]
    public class SkillManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("技能配置")]
        [Tooltip("装配的技能（按槽位顺序：Skill1, Skill2, Skill3, Ultimate）")]
        [SerializeField] private SkillDefinition[] _equippedSkills = new SkillDefinition[4];

        [Header("按键设置")]
        [SerializeField] private KeyCode _skill1Key = KeyCode.U;
        [SerializeField] private KeyCode _skill2Key = KeyCode.I;
        [SerializeField] private KeyCode _skill3Key = KeyCode.O;
        [SerializeField] private KeyCode _ultimateKey = KeyCode.P;

        [Header("伤害检测")]
        [Tooltip("技能伤害检测的敌人图层")]
        [SerializeField] private LayerMask _enemyLayer;

        #endregion

        #region 事件

        /// <summary>
        /// 技能释放时触发
        /// 参数：槽位, 技能定义
        /// </summary>
        public event Action<SkillSlot, SkillDefinition> OnSkillCast;

        /// <summary>
        /// 技能冷却完成时触发
        /// 参数：槽位
        /// </summary>
        public event Action<SkillSlot> OnSkillReady;

        #endregion

        #region 运行时状态

        private CharacterStats _characterStats;
        private ResourceComponent _mana;
        private ResourceComponent _rage;
        private PlayerController _playerController;
        private LevelSystem _levelSystem;

        // 每个槽位的冷却剩余时间
        private readonly float[] _cooldownTimers = new float[4];

        // 是否正在释放技能
        private bool _isCasting;
        private Coroutine _castCoroutine;

        // 碰撞检测缓存
        private readonly List<Collider2D> _hitResults = new(16);
        private ContactFilter2D _skillFilter;

        #endregion

        #region 公共属性

        /// <summary>是否正在施法</summary>
        public bool IsCasting => _isCasting;

        /// <summary>获取指定槽位的技能定义（可能为 null）</summary>
        public SkillDefinition GetSkill(SkillSlot slot) =>
            (int)slot < _equippedSkills.Length ? _equippedSkills[(int)slot] : null;

        /// <summary>获取指定槽位冷却剩余时间</summary>
        public float GetCooldownRemaining(SkillSlot slot) => _cooldownTimers[(int)slot];

        /// <summary>获取指定槽位冷却进度 (0=冷却中, 1=就绪)</summary>
        public float GetCooldownProgress(SkillSlot slot)
        {
            var skill = GetSkill(slot);
            if (skill == null || skill.Cooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01(_cooldownTimers[(int)slot] / skill.Cooldown);
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            _characterStats = GetComponent<CharacterStats>();
            _playerController = GetComponent<PlayerController>();
            _levelSystem = GetComponent<LevelSystem>();

            // 找蓝量和怒气
            var resources = GetComponents<ResourceComponent>();
            foreach (var res in resources)
            {
                if (res.Type == ResourceType.Mana) _mana = res;
                else if (res.Type == ResourceType.Rage) _rage = res;
            }

            // 配置碰撞过滤器
            _skillFilter.useTriggers = true;
            _skillFilter.useLayerMask = true;
            _skillFilter.SetLayerMask(_enemyLayer);
        }

        private void Update()
        {
            // 更新冷却
            UpdateCooldowns();

            // 施法中不处理新输入
            if (_isCasting) return;

            // 检测技能输入
            if (UnityEngine.Input.GetKeyDown(_skill1Key)) TryCast(SkillSlot.Skill1);
            else if (UnityEngine.Input.GetKeyDown(_skill2Key)) TryCast(SkillSlot.Skill2);
            else if (UnityEngine.Input.GetKeyDown(_skill3Key)) TryCast(SkillSlot.Skill3);
            else if (UnityEngine.Input.GetKeyDown(_ultimateKey)) TryCast(SkillSlot.Ultimate);
        }

        #endregion

        #region 技能释放

        /// <summary>
        /// 尝试释放指定槽位的技能
        /// </summary>
        public bool TryCast(SkillSlot slot)
        {
            var skill = GetSkill(slot);
            if (skill == null) return false;

            // 检查是否解锁
            if (_levelSystem != null && skill.UnlockLevel > 0)
            {
                if (!_levelSystem.IsSkillUnlocked(skill.SkillId))
                {
                    Debug.Log($"[SkillManager] {skill.DisplayName} 尚未解锁（需要 Lv.{skill.UnlockLevel}）");
                    return false;
                }
            }

            // 检查冷却
            if (_cooldownTimers[(int)slot] > 0f)
            {
                Debug.Log($"[SkillManager] {skill.DisplayName} 冷却中（剩余 {_cooldownTimers[(int)slot]:F1}s）");
                return false;
            }

            // 检查消耗
            if (!CheckCost(skill)) return false;

            // 扣除消耗
            ConsumeCost(skill);

            // 开始施法
            _castCoroutine = StartCoroutine(CastRoutine(slot, skill));
            return true;
        }

        /// <summary>
        /// 技能施法协程
        /// </summary>
        private IEnumerator CastRoutine(SkillSlot slot, SkillDefinition skill)
        {
            _isCasting = true;

            // 锁定朝向
            if (skill.LockFacing && _playerController != null)
            {
                _playerController.SetMovementLocked(true);
            }

            Debug.Log($"[SkillManager] 释放技能: {skill.DisplayName}（{skill.CostType}）");
            OnSkillCast?.Invoke(slot, skill);

            // 前摇
            if (skill.CastTime > 0f)
            {
                yield return new WaitForSeconds(skill.CastTime);
            }

            // 执行技能效果
            ExecuteSkillEffect(skill);

            // 多段攻击
            if (skill.HitCount > 1)
            {
                for (int i = 1; i < skill.HitCount; i++)
                {
                    yield return new WaitForSeconds(skill.HitInterval);
                    ExecuteSkillEffect(skill);
                }
            }

            // 后摇
            if (skill.RecoveryTime > 0f)
            {
                yield return new WaitForSeconds(skill.RecoveryTime);
            }

            // 施法结束
            _isCasting = false;

            if (skill.LockFacing && _playerController != null)
            {
                _playerController.SetMovementLocked(false);
            }

            // 进入冷却
            _cooldownTimers[(int)slot] = skill.Cooldown;
        }

        #endregion

        #region 技能效果执行

        /// <summary>
        /// 根据技能类型执行具体效果
        /// </summary>
        private void ExecuteSkillEffect(SkillDefinition skill)
        {
            switch (skill.EffectType)
            {
                case SkillEffectType.MeleeArea:
                    ExecuteMeleeArea(skill);
                    break;
                case SkillEffectType.Projectile:
                    ExecuteProjectile(skill);
                    break;
                case SkillEffectType.GroundSlam:
                    ExecuteGroundSlam(skill);
                    break;
                case SkillEffectType.Dash:
                    // Dash 由协程内特殊处理
                    ExecuteMeleeArea(skill);
                    break;
                case SkillEffectType.Buff:
                    // TODO: Buff 系统
                    Debug.Log($"[SkillManager] Buff 效果（待实现）");
                    break;
            }
        }

        /// <summary>
        /// 近战范围伤害 - 前方扇形/圆形区域
        /// </summary>
        private void ExecuteMeleeArea(SkillDefinition skill)
        {
            Vector2 center = GetSkillCenter(skill.EffectRadius * 0.5f);

            int hitCount = Physics2D.OverlapCircle(center, skill.EffectRadius, _skillFilter, _hitResults);

            int damage = CalculateSkillDamage(skill);

            for (int i = 0; i < hitCount; i++)
            {
                ApplyDamageToTarget(_hitResults[i], damage, skill.KnockbackForce);
            }

            if (hitCount > 0)
            {
                Debug.Log($"[SkillManager] {skill.DisplayName} 命中 {hitCount} 个目标，" +
                          $"每个 {damage} 伤害");
            }
        }

        /// <summary>
        /// 投射物 - 向前方发射（简化版：直接判定一条线上的敌人）
        /// </summary>
        private void ExecuteProjectile(SkillDefinition skill)
        {
            float facing = _playerController != null ? _playerController.FacingDirection : 1f;
            Vector2 origin = (Vector2)transform.position;
            Vector2 direction = new Vector2(facing, 0f);

            // 用 BoxCast 模拟投射物路径
            var hits = Physics2D.BoxCastAll(
                origin,
                new Vector2(0.5f, skill.EffectRadius),
                0f,
                direction,
                skill.EffectDistance,
                _enemyLayer
            );

            int damage = CalculateSkillDamage(skill);

            foreach (var hit in hits)
            {
                ApplyDamageToTarget(hit.collider, damage, skill.KnockbackForce);
            }

            if (hits.Length > 0)
            {
                Debug.Log($"[SkillManager] {skill.DisplayName} 投射命中 {hits.Length} 个目标");
            }
        }

        /// <summary>
        /// 地面冲击波 - 以自身为中心的大范围伤害
        /// </summary>
        private void ExecuteGroundSlam(SkillDefinition skill)
        {
            Vector2 center = (Vector2)transform.position;

            int hitCount = Physics2D.OverlapCircle(center, skill.EffectRadius, _skillFilter, _hitResults);

            int damage = CalculateSkillDamage(skill);

            for (int i = 0; i < hitCount; i++)
            {
                ApplyDamageToTarget(_hitResults[i], damage, skill.KnockbackForce * 1.5f);
            }

            if (hitCount > 0)
            {
                Debug.Log($"[SkillManager] {skill.DisplayName} 冲击波命中 {hitCount} 个目标，" +
                          $"每个 {damage} 伤害");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 计算技能伤害中心位置
        /// </summary>
        private Vector2 GetSkillCenter(float forwardOffset)
        {
            Vector2 pos = (Vector2)transform.position;
            float facing = _playerController != null ? _playerController.FacingDirection : 1f;
            pos.x += forwardOffset * facing;
            return pos;
        }

        /// <summary>
        /// 计算技能伤害值
        /// </summary>
        private int CalculateSkillDamage(SkillDefinition skill)
        {
            if (_characterStats != null)
            {
                var (baseDmg, _) = _characterStats.CalculateAttackDamage(skill.DamageMultiplier);
                return baseDmg;
            }
            return Mathf.RoundToInt(10 * skill.DamageMultiplier);
        }

        /// <summary>
        /// 对目标施加伤害
        /// </summary>
        private void ApplyDamageToTarget(Collider2D target, int damage, float knockback)
        {
            var health = target.GetComponent<HealthComponent>();
            if (health == null) health = target.GetComponentInParent<HealthComponent>();
            if (health == null) return;

            Vector2 knockDir = (target.transform.position - transform.position).normalized;
            health.TakeDamage(damage, knockDir * knockback);
        }

        /// <summary>
        /// 检查是否能支付技能消耗
        /// </summary>
        private bool CheckCost(SkillDefinition skill)
        {
            switch (skill.CostType)
            {
                case SkillCostType.Mana:
                    if (_mana == null || _mana.CurrentValue < skill.ManaCost)
                    {
                        Debug.Log($"[SkillManager] 蓝量不足（需要 {skill.ManaCost}，当前 {_mana?.CurrentValue ?? 0}）");
                        return false;
                    }
                    return true;

                case SkillCostType.FullRage:
                    if (_rage == null || !_rage.IsFull)
                    {
                        Debug.Log($"[SkillManager] 怒气未满（需要满怒，当前 {_rage?.CurrentValue ?? 0}/{_rage?.MaxValue ?? 100}）");
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// 扣除技能消耗
        /// </summary>
        private void ConsumeCost(SkillDefinition skill)
        {
            switch (skill.CostType)
            {
                case SkillCostType.Mana:
                    _mana?.TryConsume(skill.ManaCost);
                    break;
                case SkillCostType.FullRage:
                    _rage?.Clear(); // 消耗全部怒气
                    break;
            }
        }

        /// <summary>
        /// 更新冷却计时器
        /// </summary>
        private void UpdateCooldowns()
        {
            for (int i = 0; i < _cooldownTimers.Length; i++)
            {
                if (_cooldownTimers[i] > 0f)
                {
                    _cooldownTimers[i] -= Time.deltaTime;
                    if (_cooldownTimers[i] <= 0f)
                    {
                        _cooldownTimers[i] = 0f;
                        OnSkillReady?.Invoke((SkillSlot)i);
                    }
                }
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 装备一个技能到指定槽位
        /// </summary>
        public void EquipSkill(SkillSlot slot, SkillDefinition skill)
        {
            _equippedSkills[(int)slot] = skill;
        }

        /// <summary>
        /// 卸下指定槽位的技能
        /// </summary>
        public void UnequipSkill(SkillSlot slot)
        {
            _equippedSkills[(int)slot] = null;
        }

        /// <summary>
        /// 中断当前施法
        /// </summary>
        public void InterruptCast()
        {
            if (_isCasting && _castCoroutine != null)
            {
                StopCoroutine(_castCoroutine);
                _isCasting = false;
                if (_playerController != null)
                    _playerController.SetMovementLocked(false);
            }
        }

        #endregion
    }
}
