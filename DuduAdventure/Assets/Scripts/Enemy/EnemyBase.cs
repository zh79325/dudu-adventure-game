using System;
using UnityEngine;

namespace DuduAdventure.Enemy
{
    /// <summary>
    /// 敌人基类 - 所有敌人的基础模板
    /// 提供巡逻、检测玩家、受伤、死亡等通用功能
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 使用虚方法（virtual）让子类可以重写特定行为
    /// - 不同类型的敌人（小妖怪、BOSS 等）继承此类，添加自己的特殊能力
    /// - 例如：黑熊精可能有冲撞攻击，蜘蛛精可能有远程毒液
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Combat.HealthComponent))]
    public class EnemyBase : MonoBehaviour
    {
        #region Inspector 配置

        [Header("基础属性")]
        [Tooltip("敌人名称（用于调试和 UI 显示）")]
        [SerializeField] protected string _enemyName = "小妖怪";

        [Tooltip("移动速度")]
        [SerializeField] protected float _moveSpeed = 3f;

        [Tooltip("攻击力")]
        [SerializeField] protected int _attackDamage = 10;

        [Tooltip("攻击范围")]
        [SerializeField] protected float _attackRange = 1.5f;

        [Tooltip("攻击冷却时间（秒）")]
        [SerializeField] protected float _attackCooldown = 1.5f;

        [Header("巡逻设置")]
        [Tooltip("巡逻移动范围（左右各多少距离）")]
        [SerializeField] protected float _patrolRange = 4f;

        [Tooltip("到达巡逻端点后等待时间（秒）")]
        [SerializeField] protected float _patrolWaitTime = 1f;

        [Header("玩家检测")]
        [Tooltip("检测玩家的范围半径")]
        [SerializeField] protected float _detectionRange = 6f;

        [Tooltip("玩家图层")]
        [SerializeField] protected LayerMask _playerLayer;

        [Header("朝向")]
        [Tooltip("初始朝向（1 = 右, -1 = 左）")]
        [SerializeField] protected int _initialFacingDirection = 1;

        #endregion

        #region 事件

        /// <summary>
        /// 敌人死亡时触发
        /// </summary>
        public event Action<EnemyBase> OnEnemyDied;

        /// <summary>
        /// 敌人发现玩家时触发
        /// </summary>
        public event Action<EnemyBase> OnPlayerDetected;

        #endregion

        #region 组件引用

        protected Rigidbody2D _rigidbody;
        protected Combat.HealthComponent _healthComponent;
        protected SpriteRenderer _spriteRenderer;

        #endregion

        #region 运行时状态

        /// <summary>
        /// 敌人行状态枚举
        /// </summary>
        public enum EnemyBehavior
        {
            Patrol,     // 巡逻
            Chase,      // 追击玩家
            Attack,     // 攻击
            Hit,        // 受击
            Dead        // 死亡
        }

        // 当前行为状态
        protected EnemyBehavior _currentBehavior = EnemyBehavior.Patrol;

        // 巡逻相关
        private Vector3 _patrolStartPoint;      // 巡逻起始位置
        private float _patrolWaitTimer;          // 等待计时器
        protected int _facingDirection;          // 当前朝向

        // 玩家检测
        protected Transform _playerTransform;    // 当前锁定的玩家
        private bool _playerInRange;             // 玩家是否在检测范围内

        // 重新选目标的计时器。
        // 多人合作时目标必须能改：原目标倒地、或另一个玩家凑得更近，都该换人。
        // 每帧遍历全部玩家没必要，隔一小段时间重选一次就够了，
        // 而且这点延迟反而让敌人"反应"显得自然，不会像瞄准机器一样瞬间切换。
        private float _retargetTimer;
        private const float RetargetInterval = 0.4f;

        // 攻击相关
        private float _attackCooldownTimer;

        // 死亡标记
        private bool _isDead;

        #endregion

        #region 公共属性

        public string EnemyName => _enemyName;
        public bool IsDead => _isDead;
        public EnemyBehavior CurrentBehavior => _currentBehavior;
        public int AttackDamage => _attackDamage;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _healthComponent = GetComponent<Combat.HealthComponent>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _rigidbody.freezeRotation = true;
            _rigidbody.gravityScale = 0f; // DNF 式：敌人也不用重力
        }

        protected virtual void Start()
        {
            // 记录巡逻起始点
            _patrolStartPoint = transform.position;

            // 设置初始朝向
            _facingDirection = _initialFacingDirection;

            // 订阅死亡事件
            if (_healthComponent != null)
            {
                _healthComponent.OnDeath += HandleDeath;
            }

            // 选定追击目标（最近的活着的玩家）
            AcquireTarget();
        }

        protected virtual void Update()
        {
            // 死亡的敌人不执行逻辑
            if (_isDead) return;

            // 更新冷却计时器
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            // 检测玩家
            DetectPlayer();

            // 根据当前行为执行逻辑
            switch (_currentBehavior)
            {
                case EnemyBehavior.Patrol:
                    PatrolBehavior();
                    break;
                case EnemyBehavior.Chase:
                    ChaseBehavior();
                    break;
                case EnemyBehavior.Attack:
                    AttackBehavior();
                    break;
                case EnemyBehavior.Hit:
                    HitBehavior();
                    break;
            }

            // 更新朝向
            UpdateFacing();
        }

        protected virtual void FixedUpdate()
        {
            if (_isDead) return;
        }

        #endregion

        #region 巡逻行为

        /// <summary>
        /// 巡逻逻辑 - 在起始点左右一定范围内来回走动
        /// </summary>
        protected virtual void PatrolBehavior()
        {
            // 如果发现了玩家，切换到追击状态
            if (_playerInRange)
            {
                ChangeBehavior(EnemyBehavior.Chase);
                return;
            }

            // 等待中
            if (_patrolWaitTimer > 0f)
            {
                _patrolWaitTimer -= Time.deltaTime;
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            // 计算巡逻边界
            float leftBound = _patrolStartPoint.x - _patrolRange;
            float rightBound = _patrolStartPoint.x + _patrolRange;

            // 到达边界，转向并等待
            if (transform.position.x <= leftBound)
            {
                _facingDirection = 1; // 转向右
                _patrolWaitTimer = _patrolWaitTime;
            }
            else if (transform.position.x >= rightBound)
            {
                _facingDirection = -1; // 转向左
                _patrolWaitTimer = _patrolWaitTime;
            }

            // 移动（巡逻只走水平方向）
            _rigidbody.linearVelocity = new Vector2(_moveSpeed * _facingDirection, 0f);
        }

        #endregion

        #region 追击行为

        /// <summary>
        /// 追击逻辑 - 向玩家移动（水平 + 纵深）
        /// </summary>
        protected virtual void ChaseBehavior()
        {
            // 丢失玩家视野，回到巡逻
            if (!_playerInRange || _playerTransform == null)
            {
                ChangeBehavior(EnemyBehavior.Patrol);
                return;
            }

            // 计算与玩家的距离
            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);

            // 进入攻击范围
            if (distanceToPlayer <= _attackRange)
            {
                ChangeBehavior(EnemyBehavior.Attack);
                return;
            }

            // 向玩家移动（DNF 式：X 和 Y 都追）
            Vector2 toPlayer = (Vector2)_playerTransform.position - (Vector2)transform.position;
            Vector2 moveDir = toPlayer.normalized;
            float chaseSpeed = _moveSpeed * 1.5f;

            _facingDirection = toPlayer.x >= 0 ? 1 : -1;

            _rigidbody.linearVelocity = moveDir * chaseSpeed;
        }

        #endregion

        #region 攻击行为

        /// <summary>
        /// 攻击逻辑
        /// </summary>
        protected virtual void AttackBehavior()
        {
            // 如果玩家超出攻击范围，回到追击
            if (_playerTransform == null)
            {
                ChangeBehavior(EnemyBehavior.Patrol);
                return;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);

            if (distanceToPlayer > _attackRange * 1.2f)
            {
                ChangeBehavior(EnemyBehavior.Chase);
                return;
            }

            // 停止移动
            _rigidbody.linearVelocity = Vector2.zero;

            // 检查攻击冷却
            if (_attackCooldownTimer <= 0f)
            {
                PerformAttack();
                _attackCooldownTimer = _attackCooldown;
            }
        }

        /// <summary>
        /// 执行攻击 - 子类可以重写以实现不同的攻击方式
        /// </summary>
        protected virtual void PerformAttack()
        {
            Debug.Log($"[{_enemyName}] 发动攻击！伤害: {_attackDamage}");

            // TODO: 播放攻击动画
            // TODO: 激活攻击判定框（可以用 DamageDealer 组件）

            // 简单实现：直接对范围内玩家造成伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                (Vector2)transform.position + new Vector2(_facingDirection * _attackRange * 0.5f, 0f),
                _attackRange * 0.5f,
                _playerLayer
            );

            foreach (var hit in hits)
            {
                var playerHealth = hit.GetComponent<Combat.HealthComponent>();
                if (playerHealth != null)
                {
                    Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
                    playerHealth.TakeDamage(_attackDamage, knockbackDir * 5f);
                }
            }
        }

        #endregion

        #region 受击行为

        /// <summary>
        /// 受击行为 - 短暂停顿
        /// </summary>
        protected virtual void HitBehavior()
        {
            // 受击状态短暂保持后回到追击/巡逻
            _rigidbody.linearVelocity = Vector2.zero;

            // 受击持续时间由状态机或计时器控制
            // 这里简化处理，直接切回追击
        }

        /// <summary>
        /// 被调用时进入受击状态（由 HealthComponent 的 OnDamaged 事件触发）
        /// </summary>
        public virtual void OnHit(int damage, Vector2 knockback)
        {
            if (_isDead) return;

            // 应用击退
            _rigidbody.linearVelocity = knockback;

            // 进入受击状态
            ChangeBehavior(EnemyBehavior.Hit);

            Debug.Log($"[{_enemyName}] 受到 {damage} 点伤害！");

            // TODO: 播放受击动画
            // TODO: 受击闪烁效果
        }

        #endregion

        #region 玩家检测

        /// <summary>
        /// 选定追击目标 - 最近的那个活着的玩家
        /// </summary>
        /// <remarks>
        /// 单人时代这里只需要 FindGameObjectWithTag("Player") 缓存一次。
        /// 多人合作下这个写法有两个致命问题：
        /// 1. 永远只盯着第一个被找到的玩家，另外三个人可以贴着敌人脸打而它无反应；
        /// 2. 那个玩家一死，引用变空，敌人从此变成木头。
        ///
        /// 设为 virtual 是为了给特殊怪留口子——比如 Boss 可以改写成"打伤害最高的人"
        /// 或"锁定唐僧"，而不必复制整套检测逻辑。
        /// </remarks>
        protected virtual void AcquireTarget()
        {
            var nearest = DuduAdventure.Player.PlayerRegistry.GetNearestPlayer(
                transform.position, aliveOnly: true);

            if (nearest != null)
            {
                _playerTransform = nearest.transform;
                return;
            }

            // 兜底：注册表里没人（灰盒调试角色没挂 PlayerIdentity）时按标签找
            var tagged = GameObject.FindGameObjectWithTag("Player");
            _playerTransform = tagged != null ? tagged.transform : null;
        }

        /// <summary>
        /// 检测玩家是否在范围内
        /// </summary>
        protected virtual void DetectPlayer()
        {
            // 周期性重选目标：目标丢失、目标倒地、或别人凑得更近时都要换人
            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer <= 0f || _playerTransform == null)
            {
                AcquireTarget();
                _retargetTimer = RetargetInterval;
            }

            if (_playerTransform == null) return;

            float distance = Vector2.Distance(transform.position, _playerTransform.position);
            bool wasInRange = _playerInRange;
            _playerInRange = distance <= _detectionRange;

            // 刚发现玩家时触发事件
            if (_playerInRange && !wasInRange)
            {
                OnPlayerDetected?.Invoke(this);
                Debug.Log($"[{_enemyName}] 发现了入侵者！");
            }
        }

        #endregion

        #region 死亡处理

        /// <summary>
        /// 处理死亡
        /// </summary>
        protected virtual void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log($"[{_enemyName}] 被消灭了！");

            // 停止移动
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.simulated = false; // 停止物理模拟

            ChangeBehavior(EnemyBehavior.Dead);

            // 触发死亡事件
            OnEnemyDied?.Invoke(this);

            // TODO: 播放死亡动画
            // TODO: 播放死亡音效
            // TODO: 掉落物品/经验值
            // TODO: 加分

            // 通知 GameManager 加分
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.AddScore(100); // 默认击杀分数
            }

            // 延迟销毁（等死亡动画播完）
            Destroy(gameObject, 2f);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 切换行为状态
        /// </summary>
        protected void ChangeBehavior(EnemyBehavior newBehavior)
        {
            if (_currentBehavior == newBehavior) return;

            Debug.Log($"[{_enemyName}] 行为变更: {_currentBehavior} -> {newBehavior}");
            _currentBehavior = newBehavior;

            // TODO: 根据新行为播放对应动画
        }

        /// <summary>
        /// 更新精灵朝向
        /// </summary>
        protected virtual void UpdateFacing()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _facingDirection == -1;
            }
        }

        #endregion

        #region 调试可视化

        protected virtual void OnDrawGizmosSelected()
        {
            // 绘制检测范围
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _detectionRange);

            // 绘制攻击范围
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            // 绘制巡逻范围
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                float leftBound = _patrolStartPoint.x - _patrolRange;
                float rightBound = _patrolStartPoint.x + _patrolRange;
                Gizmos.DrawLine(
                    new Vector3(leftBound, transform.position.y, 0),
                    new Vector3(rightBound, transform.position.y, 0)
                );
            }
        }

        #endregion
    }
}
