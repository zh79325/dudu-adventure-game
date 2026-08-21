using UnityEngine;

namespace DuduAdventure.Combat
{
    /// <summary>
    /// 伤害发射器 - 挂载在攻击判定框（Hitbox）上
    /// 当攻击动画激活时，此组件所在的碰撞体会对接触到的目标造成伤害
    /// </summary>
    /// <remarks>
    /// 使用方法：
    /// 1. 在武器/攻击部位创建子物体
    /// 2. 给子物体添加 Collider2D（设为 Trigger）和此脚本
    /// 3. 配置伤害数值和阵营
    /// 4. 在动画中控制此物体的激活/禁用来匹配攻击时机
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class DamageDealer : MonoBehaviour
    {
        #region Inspector 配置

        [Header("伤害设置")]
        [Tooltip("基础伤害值")]
        [SerializeField] private int _damage = 10;

        [Tooltip("击退力度")]
        [SerializeField] private float _knockbackForce = 5f;

        [Header("阵营设置")]
        [Tooltip("伤害发射器的阵营")]
        [SerializeField] private Faction _faction = Faction.Player;

        [Tooltip("可伤害的目标图层")]
        [SerializeField] private LayerMask _targetLayer;

        [Header("攻击特性")]
        [Tooltip("每次激活只能伤害同一目标一次")]
        [SerializeField] private bool _hitOncePerActivation = true;

        [Tooltip("可以穿透多个目标")]
        [SerializeField] private bool _canPierce = true;

        #endregion

        #region 阵营枚举

        /// <summary>
        /// 阵营 - 用于区分友方/敌方，防止误伤
        /// </summary>
        public enum Faction
        {
            Player, // 玩家阵营（攻击敌人）
            Enemy,  // 敌人阵营（攻击玩家）
            Neutral // 中立（如陷阱，对所有人都造成伤害）
        }

        #endregion

        #region 运行时状态

        // 已命中的目标列表（防止重复伤害）
        private System.Collections.Generic.HashSet<GameObject> _hitTargets =
            new System.Collections.Generic.HashSet<GameObject>();

        // 组件引用
        private Collider2D _collider;

        // 本次激活是否已触发
        private bool _hasTriggered;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前伤害值（可被 Buff 修改）
        /// </summary>
        public int Damage
        {
            get => _damage;
            set => _damage = Mathf.Max(0, value);
        }

        /// <summary>
        /// 击退力度
        /// </summary>
        public float KnockbackForce => _knockbackForce;

        /// <summary>
        /// 所属阵营
        /// </summary>
        public Faction OwnerFaction => _faction;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            // 每次激活时重置命中列表
            ResetHitTargets();
        }

        /// <summary>
        /// Trigger 碰撞检测 - 当有其他碰撞体进入触发器时调用
        /// 注意：需要在 Collider2D 上勾选 "Is Trigger"
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 检查目标是否在可伤害的图层上
            if (!IsInLayerMask(other.gameObject.layer, _targetLayer))
                return;

            // 检查是否已经命中过此目标
            if (_hitOncePerActivation && _hitTargets.Contains(other.gameObject))
                return;

            // 执行伤害
            ApplyDamage(other);
        }

        /// <summary>
        /// Trigger 持续碰撞检测 - 停留在触发器内时每帧调用
        /// 用于处理目标进入后仍然停留在判定框内的情况
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            // 只在非"单次命中"模式下使用
            if (_hitOncePerActivation) return;

            if (!IsInLayerMask(other.gameObject.layer, _targetLayer))
                return;

            ApplyDamage(other);
        }

        #endregion

        #region 伤害逻辑

        /// <summary>
        /// 对目标施加伤害
        /// </summary>
        private void ApplyDamage(Collider2D target)
        {
            // 尝试获取目标的 HealthComponent
            HealthComponent health = target.GetComponent<HealthComponent>();
            if (health == null)
            {
                health = target.GetComponentInParent<HealthComponent>();
            }

            if (health == null) return;

            // 如果目标已死亡，不处理
            if (health.IsDead) return;

            // 计算击退方向
            Vector2 knockbackDir = CalculateKnockbackDirection(target.transform);

            // 造成伤害
            health.TakeDamage(_damage, knockbackDir * _knockbackForce);

            // 记录已命中的目标
            _hitTargets.Add(target.gameObject);

            Debug.Log($"[DamageDealer] 对 {target.name} 造成 {_damage} 点伤害");

            // 如果设置不穿透，禁用自身
            if (!_canPierce)
            {
                gameObject.SetActive(false);
            }

            // TODO: 触发命中特效（火花粒子）
            // TODO: 播放命中音效
            // TODO: 触发屏幕震动（通过 CameraFollow）
        }

        /// <summary>
        /// 计算击退方向（从自身指向目标）
        /// </summary>
        private Vector2 CalculateKnockbackDirection(Transform target)
        {
            Vector2 direction = (target.position - transform.position).normalized;

            // 确保击退有向上的分量（让打击感更好）
            direction.y = Mathf.Max(direction.y, 0.3f);

            return direction;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置命中列表 - 每次新的攻击动作开始时调用
        /// </summary>
        public void ResetHitTargets()
        {
            _hitTargets.Clear();
            _hasTriggered = false;
        }

        /// <summary>
        /// 添加伤害加成（Buff 系统使用）
        /// </summary>
        public void AddDamageBonus(int bonus)
        {
            _damage += bonus;
        }

        /// <summary>
        /// 设置击退力度
        /// </summary>
        public void SetKnockbackForce(float force)
        {
            _knockbackForce = force;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查某个图层是否在图层掩码中
        /// </summary>
        private bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        #endregion

        #region 调试可视化

        private void OnDrawGizmosSelected()
        {
            // 绘制伤害判定区域的可视化
            Gizmos.color = _faction == Faction.Player ? Color.blue : Color.red;

            if (_collider is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(
                    transform.position + (Vector3)circle.offset,
                    circle.radius
                );
            }
            else if (_collider is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(
                    transform.position + (Vector3)box.offset,
                    box.size
                );
            }
        }

        #endregion
    }
}
