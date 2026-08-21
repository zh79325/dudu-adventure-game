using System;
using UnityEngine;

namespace DuduAdventure.Combat
{
    /// <summary>
    /// 通用生命值组件 - 可挂载到玩家、敌人等任何需要生命值的对象上
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 组件化设计，不依赖特定角色类型
    /// - 通过事件通知外部系统（UI、音效、特效等）
    /// - 支持无敌帧（I-Frames）机制，避免被连续击中
    /// </remarks>
    [DisallowMultipleComponent]
    public class HealthComponent : MonoBehaviour
    {
        #region Inspector 配置

        [Header("生命值设置")]
        [Tooltip("最大生命值")]
        [SerializeField] private int _maxHP = 100;

        [Tooltip("是否在游戏开始时满血")]
        [SerializeField] private bool _startAtFullHP = true;

        [Tooltip("初始生命值（如果不满血）")]
        [SerializeField] private int _initialHP = 100;

        [Header("无敌帧设置")]
        [Tooltip("受伤后的无敌时间（秒）")]
        [SerializeField] private float _invincibilityDuration = 1.0f;

        [Tooltip("无敌期间是否闪烁")]
        [SerializeField] private bool _flashDuringInvincibility = true;

        [Tooltip("闪烁频率（每秒闪烁次数）")]
        [SerializeField] private float _flashFrequency = 8f;

        [Header("受击效果")]
        [Tooltip("受击时的击退衰减系数（0~1，越大击退越弱）")]
        [SerializeField] private float _knockbackDamping = 0.8f;

        #endregion

        #region 事件

        /// <summary>
        /// 受到伤害时触发
        /// 参数：伤害量, 当前血量, 最大血量, 击退方向
        /// </summary>
        public event Action<int, int, int, Vector2> OnDamaged;

        /// <summary>
        /// 治疗时触发
        /// 参数：治疗量, 当前血量, 最大血量
        /// </summary>
        public event Action<int, int, int> OnHealed;

        /// <summary>
        /// 死亡时触发
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// 无敌状态改变时触发
        /// 参数：是否无敌
        /// </summary>
        public event Action<bool> OnInvincibilityChanged;

        #endregion

        #region 运行时状态

        // 当前生命值
        private int _currentHP;

        // 无敌计时器
        private float _invincibilityTimer;

        // 是否已死亡
        private bool _isDead;

        // Sprite 渲染器引用（用于闪烁效果）
        private SpriteRenderer _spriteRenderer;

        // 闪烁效果相关
        private float _flashTimer;
        private bool _isFlashing;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前生命值
        /// </summary>
        public int CurrentHP => _currentHP;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHP => _maxHP;

        /// <summary>
        /// 生命值百分比（0~1）
        /// </summary>
        public float HPPercent => _maxHP > 0 ? (float)_currentHP / _maxHP : 0f;

        /// <summary>
        /// 是否已死亡
        /// </summary>
        public bool IsDead => _isDead;

        /// <summary>
        /// 是否处于无敌状态
        /// </summary>
        public bool IsInvincible => _invincibilityTimer > 0f;

        /// <summary>
        /// 无敌剩余时间比例（用于 UI 显示）
        /// </summary>
        public float InvincibilityProgress =>
            _invincibilityDuration > 0f ? _invincibilityTimer / _invincibilityDuration : 0f;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            // 初始化生命值
            _currentHP = _startAtFullHP ? _maxHP : Mathf.Clamp(_initialHP, 0, _maxHP);
        }

        private void Update()
        {
            // 更新无敌计时器
            if (_invincibilityTimer > 0f)
            {
                _invincibilityTimer -= Time.deltaTime;

                // 闪烁效果
                if (_flashDuringInvincibility && _spriteRenderer != null)
                {
                    UpdateFlashEffect();
                }

                // 无敌时间结束
                if (_invincibilityTimer <= 0f)
                {
                    EndInvincibility();
                }
            }
        }

        #endregion

        #region 受伤

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害量（必须大于 0）</param>
        /// <param name="knockback">击退方向和力度</param>
        public void TakeDamage(int damage, Vector2 knockback = default)
        {
            // 已死亡不再受伤
            if (_isDead) return;

            // 无敌状态忽略伤害
            if (IsInvincible) return;

            // 伤害必须大于 0
            if (damage <= 0)
            {
                Debug.LogWarning("[HealthComponent] 伤害值必须大于 0");
                return;
            }

            // 扣除生命值
            _currentHP = Mathf.Max(0, _currentHP - damage);

            Debug.Log($"[HealthComponent] {gameObject.name} 受到 {damage} 点伤害，" +
                      $"剩余 HP: {_currentHP}/{_maxHP}");

            // 应用击退（衰减后）
            if (knockback != default)
            {
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = knockback * _knockbackDamping;
                }
            }

            // 触发受伤事件
            OnDamaged?.Invoke(damage, _currentHP, _maxHP, knockback);

            // TODO: 播放受击音效
            // TODO: 触发受击特效（粒子、屏幕震动等）

            // 开始无敌帧
            StartInvincibility();

            // 检查是否死亡
            if (_currentHP <= 0)
            {
                Die();
            }
        }

        #endregion

        #region 治疗

        /// <summary>
        /// 恢复生命值
        /// </summary>
        /// <param name="amount">恢复量</param>
        public void Heal(int amount)
        {
            // 已死亡不能治疗
            if (_isDead) return;

            if (amount <= 0)
            {
                Debug.LogWarning("[HealthComponent] 治疗量必须大于 0");
                return;
            }

            int actualHeal = Mathf.Min(amount, _maxHP - _currentHP);
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);

            Debug.Log($"[HealthComponent] {gameObject.name} 恢复了 {actualHeal} 点生命，" +
                      $"当前 HP: {_currentHP}/{_maxHP}");

            // 触发治疗事件
            OnHealed?.Invoke(actualHeal, _currentHP, _maxHP);

            // TODO: 播放治疗特效和音效
        }

        /// <summary>
        /// 回满生命值
        /// </summary>
        public void FullHeal()
        {
            Heal(_maxHP - _currentHP);
        }

        #endregion

        #region 无敌帧

        /// <summary>
        /// 开始无敌状态
        /// </summary>
        private void StartInvincibility()
        {
            if (_invincibilityDuration <= 0f) return;

            _invincibilityTimer = _invincibilityDuration;
            OnInvincibilityChanged?.Invoke(true);
        }

        /// <summary>
        /// 结束无敌状态
        /// </summary>
        private void EndInvincibility()
        {
            _invincibilityTimer = 0f;

            // 恢复精灵显示
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
                Color color = _spriteRenderer.color;
                color.a = 1f;
                _spriteRenderer.color = color;
            }

            _isFlashing = false;
            OnInvincibilityChanged?.Invoke(false);
        }

        /// <summary>
        /// 更新闪烁效果
        /// </summary>
        private void UpdateFlashEffect()
        {
            _flashTimer += Time.deltaTime * _flashFrequency;

            // 通过正弦函数实现平滑的闪烁
            float alpha = (Mathf.Sin(_flashTimer * Mathf.PI * 2f) + 1f) * 0.5f;

            if (_spriteRenderer != null)
            {
                Color color = _spriteRenderer.color;
                color.a = alpha;
                _spriteRenderer.color = color;
            }
        }

        #endregion

        #region 死亡

        /// <summary>
        /// 执行死亡逻辑
        /// </summary>
        private void Die()
        {
            _isDead = true;

            Debug.Log($"[HealthComponent] {gameObject.name} 已死亡");

            // 触发死亡事件
            OnDeath?.Invoke();

            // TODO: 播放死亡动画
            // TODO: 禁用碰撞体
            // TODO: 禁用其他组件
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 修改最大生命值（装备或升级时使用）
        /// </summary>
        /// <param name="newMaxHP">新的最大生命值</param>
        /// <param name="fillToMax">是否同时回满血</param>
        public void SetMaxHP(int newMaxHP, bool fillToMax = false)
        {
            _maxHP = Mathf.Max(1, newMaxHP);

            if (fillToMax)
            {
                _currentHP = _maxHP;
            }
            else
            {
                _currentHP = Mathf.Min(_currentHP, _maxHP);
            }
        }

        /// <summary>
        /// 重置生命值（从检查点重新开始时使用）
        /// </summary>
        public void ResetHP()
        {
            _currentHP = _maxHP;
            _isDead = false;
            _invincibilityTimer = 0f;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
                Color color = _spriteRenderer.color;
                color.a = 1f;
                _spriteRenderer.color = color;
            }
        }

        #endregion
    }
}
