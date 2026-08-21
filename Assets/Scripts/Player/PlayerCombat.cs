using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家战斗系统 - 处理孙悟空的攻击、连招和闪避
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 攻击使用圆形判定区域（OverlapCircle），而非碰撞体触发
    /// - 连招通过攻击窗口机制实现：攻击后短暂时间内可以再次攻击形成连击
    /// - 冲刺/闪避提供无敌帧，增加操作深度
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerCombat : MonoBehaviour
    {
        #region Inspector 配置

        [Header("攻击设置")]
        [Tooltip("基础攻击伤害")]
        [SerializeField] private int _baseDamage = 10;

        [Tooltip("攻击判定半径")]
        [SerializeField] private float _attackRadius = 1.2f;

        [Tooltip("攻击判定的偏移位置（相对于角色中心，x 为前方距离）")]
        [SerializeField] private Vector2 _attackOffset = new Vector2(0.8f, 0f);

        [Tooltip("攻击冷却时间（秒）")]
        [SerializeField] private float _attackCooldown = 0.3f;

        [Tooltip("攻击持续时间（判定框激活的时间）")]
        [SerializeField] private float _attackDuration = 0.2f;

        [Tooltip("可攻击的敌人图层")]
        [SerializeField] private LayerMask _enemyLayer;

        [Header("连招设置")]
        [Tooltip("最大连击数")]
        [SerializeField] private int _maxComboCount = 3;

        [Tooltip("连招窗口时间（上一次攻击后多少秒内可以继续连击）")]
        [SerializeField] private float _comboWindow = 0.6f;

        [Tooltip("每次连击的伤害倍率增量（第 2 击 = 1.3x，第 3 击 = 1.6x）")]
        [SerializeField] private float _comboDamageMultiplier = 0.3f;

        [Tooltip("连击击退力度增量")]
        [SerializeField] private float _comboKnockbackMultiplier = 0.2f;

        [Header("冲刺/闪避设置")]
        [Tooltip("冲刺距离")]
        [SerializeField] private float _dashDistance = 4f;

        [Tooltip("冲刺持续时间（秒）")]
        [SerializeField] private float _dashDuration = 0.15f;

        [Tooltip("冲刺冷却时间（秒）")]
        [SerializeField] private float _dashCooldown = 1.0f;

        [Tooltip("冲刺期间是否无敌")]
        [SerializeField] private bool _invincibleDuringDash = true;

        #endregion

        #region 事件

        /// <summary>
        /// 攻击命中时触发
        /// 参数：连击序号（从 1 开始）
        /// </summary>
        public event Action<int> OnAttackHit;

        /// <summary>
        /// 冲刺开始和结束时触发
        /// 参数：是否开始冲刺
        /// </summary>
        public event Action<bool> OnDash;

        #endregion

        #region 组件引用

        private PlayerController _playerController;
        private PlayerStateMachine _playerStateMachine;

        #endregion

        #region 运行时状态

        // 攻击冷却计时
        private float _attackCooldownTimer;

        // 连招相关
        private int _currentComboCount;        // 当前连击计数
        private float _comboWindowTimer;       // 连招窗口计时器
        private bool _isAttacking;             // 是否正在攻击中

        // 冲刺相关
        private float _dashCooldownTimer;      // 冲刺冷却计时器
        private bool _isDashing;               // 是否正在冲刺
        private bool _isInvincible;            // 是否处于无敌状态

        // 攻击判定结果缓存
        // Unity 6 已废弃 NonAlloc 系列 API，改用可复用的 List 版本重载。
        // List 只创建一次并反复复用，同样不会产生每帧 GC 分配。
        private readonly List<Collider2D> _hitResults = new List<Collider2D>(16);

        // 攻击判定的碰撞过滤器（缓存复用，避免每次攻击重新构造）
        private ContactFilter2D _attackFilter;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在攻击
        /// </summary>
        public bool IsAttacking => _isAttacking;

        /// <summary>
        /// 是否正在冲刺
        /// </summary>
        public bool IsDashing => _isDashing;

        /// <summary>
        /// 是否处于无敌状态
        /// </summary>
        public bool IsInvincible => _isInvincible;

        /// <summary>
        /// 当前连击计数
        /// </summary>
        public int ComboCount => _currentComboCount;

        /// <summary>
        /// 冲刺冷却进度（0~1，1 表示可用）
        /// </summary>
        public float DashCooldownProgress =>
            _dashCooldown > 0f ? 1f - Mathf.Clamp01(_dashCooldownTimer / _dashCooldown) : 1f;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerStateMachine = GetComponent<PlayerStateMachine>();
        }

        private void Update()
        {
            // 更新冷却计时器
            UpdateTimers();

            // 处理输入
            HandleInput();
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理战斗相关输入
        /// </summary>
        private void HandleInput()
        {
            // 攻击输入（鼠标左键 或 J 键）
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
            {
                TryAttack();
            }

            // 冲刺输入（Shift 键 或 K 键）
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.K))
            {
                TryDash();
            }
        }

        #endregion

        #region 攻击系统

        /// <summary>
        /// 尝试攻击 - 检查冷却和连招条件
        /// </summary>
        private void TryAttack()
        {
            // 冷却中不能攻击
            if (_attackCooldownTimer > 0f) return;

            // 冲刺中不能攻击
            if (_isDashing) return;

            // 如果在连招窗口内，增加连击计数
            if (_comboWindowTimer > 0f && _currentComboCount < _maxComboCount)
            {
                _currentComboCount++;
            }
            else
            {
                // 超出窗口或达到最大连击数，重置连击
                _currentComboCount = 1;
            }

            // 开始攻击
            StartCoroutine(AttackRoutine());
        }

        /// <summary>
        /// 攻击协程 - 控制攻击的完整流程
        /// </summary>
        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;

            // 通知状态机进入攻击状态
            _playerStateMachine.TriggerAttack();

            // 计算当前连击的伤害和击退
            float damageMultiplier = 1f + (_currentComboCount - 1) * _comboDamageMultiplier;
            float knockbackMultiplier = 1f + (_currentComboCount - 1) * _comboKnockbackMultiplier;

            Debug.Log($"[战斗] 第 {_currentComboCount} 击！伤害倍率: {damageMultiplier:F1}x");

            // 等待一小段时间再执行判定（配合动画前摇）
            yield return new WaitForSeconds(0.05f);

            // 执行攻击判定
            PerformAttackHit(damageMultiplier, knockbackMultiplier);

            // 保持攻击状态
            yield return new WaitForSeconds(_attackDuration);

            _isAttacking = false;

            // 设置冷却
            _attackCooldownTimer = _attackCooldown;

            // 重置连招窗口计时器
            _comboWindowTimer = _comboWindow;
        }

        /// <summary>
        /// 执行攻击判定 - 使用 OverlapCircle 检测范围内的敌人
        /// </summary>
        /// <param name="damageMultiplier">伤害倍率</param>
        /// <param name="knockbackMultiplier">击退倍率</param>
        private void PerformAttackHit(float damageMultiplier, float knockbackMultiplier)
        {
            // 计算攻击判定的世界坐标位置
            Vector2 attackCenter = CalculateAttackCenter();

            // 配置碰撞过滤器：只检测敌人图层，并且把触发器（Trigger）也算进命中结果
            // （Unity 6 的新 API 用 ContactFilter2D 代替原来直接传 LayerMask 的写法）
            _attackFilter.useTriggers = true;
            _attackFilter.SetLayerMask(_enemyLayer);
            _attackFilter.useLayerMask = true;

            // 使用 OverlapCircle 的 List 重载检测范围内的所有碰撞体
            // 结果会写入 _hitResults，方法内部会先清空列表
            int hitCount = Physics2D.OverlapCircle(
                attackCenter,
                _attackRadius,
                _attackFilter,
                _hitResults
            );

            // 计算最终伤害
            int finalDamage = Mathf.RoundToInt(_baseDamage * damageMultiplier);
            float finalKnockback = 5f * knockbackMultiplier;

            // 对每个命中的敌人造成伤害
            for (int i = 0; i < hitCount; i++)
            {
                // 尝试获取敌人的 HealthComponent（可能在自身或父物体上）
                var health = _hitResults[i].GetComponent<Combat.HealthComponent>();
                if (health == null)
                {
                    health = _hitResults[i].GetComponentInParent<Combat.HealthComponent>();
                }

                if (health != null)
                {
                    // 计算击退方向（从玩家指向敌人）
                    Vector2 knockbackDir = (_hitResults[i].transform.position - transform.position).normalized;

                    // 造成伤害
                    health.TakeDamage(finalDamage, knockbackDir * finalKnockback);

                    Debug.Log($"[战斗] 命中 {_hitResults[i].name}，造成 {finalDamage} 点伤害");
                }

                // TODO: 命中特效（火花、屏幕震动）
                // TODO: 命中音效
            }

            if (hitCount > 0)
            {
                OnAttackHit?.Invoke(_currentComboCount);
            }
        }

        /// <summary>
        /// 计算攻击判定中心位置（根据角色朝向调整）
        /// </summary>
        private Vector2 CalculateAttackCenter()
        {
            Vector2 center = (Vector2)transform.position;
            // 根据朝向翻转 X 偏移
            center.x += _attackOffset.x * _playerController.FacingDirection;
            center.y += _attackOffset.y;
            return center;
        }

        #endregion

        #region 冲刺系统

        /// <summary>
        /// 尝试冲刺/闪避
        /// </summary>
        private void TryDash()
        {
            // 冷却中不能冲刺
            if (_dashCooldownTimer > 0f) return;

            // 已经在冲刺中
            if (_isDashing) return;

            StartCoroutine(DashRoutine());
        }

        /// <summary>
        /// 冲刺协程 - 快速向当前朝向移动一段距离
        /// </summary>
        private IEnumerator DashRoutine()
        {
            _isDashing = true;

            // 开始冲刺
            OnDash?.Invoke(true);

            // 冲刺期间无敌
            if (_invincibleDuringDash)
            {
                _isInvincible = true;
            }

            Debug.Log("[战斗] 冲刺闪避！");

            // 计算冲刺速度
            float dashSpeed = _dashDistance / _dashDuration;
            float dashDirection = _playerController.FacingDirection;

            // TODO: 播放冲刺动画和特效（残影效果）
            // TODO: 播放冲刺音效

            // 冲刺持续期间
            float elapsed = 0f;
            while (elapsed < _dashDuration)
            {
                // 设置水平速度为冲刺速度
                _playerController.SetHorizontalVelocity(dashSpeed * dashDirection);

                elapsed += Time.deltaTime;
                yield return null; // 等待下一帧
            }

            // 冲刺结束
            _isDashing = false;
            _isInvincible = false;

            OnDash?.Invoke(false);

            // 设置冲刺冷却
            _dashCooldownTimer = _dashCooldown;
        }

        #endregion

        #region 计时器更新

        /// <summary>
        /// 更新所有冷却和窗口计时器
        /// </summary>
        private void UpdateTimers()
        {
            // 攻击冷却
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            // 连招窗口
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;

                // 窗口关闭，重置连击计数
                if (_comboWindowTimer <= 0f)
                {
                    _currentComboCount = 0;
                }
            }

            // 冲刺冷却
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置连击计数（受击时调用）
        /// </summary>
        public void ResetCombo()
        {
            _currentComboCount = 0;
            _comboWindowTimer = 0f;
        }

        /// <summary>
        /// 增加基础伤害（装备或 Buff 加成）
        /// </summary>
        public void AddBonusDamage(int bonus)
        {
            _baseDamage += bonus;
        }

        #endregion

        #region 调试可视化

        private void OnDrawGizmosSelected()
        {
            // 绘制攻击判定范围
            if (_playerController != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(CalculateAttackCenter(), _attackRadius);
            }
        }

        #endregion
    }
}
