using UnityEngine;
using DuduAdventure.Core;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家控制器 - 负责处理孙悟空的移动和跳跃
    /// 使用 Rigidbody2D 进行物理驱动的移动
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 控制器只负责"移动输入 -> 物理运动"的转换
    /// - 状态管理交给 PlayerStateMachine
    /// - 战斗逻辑交给 PlayerCombat
    /// - 各组件各司其职，互不耦合
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public class PlayerController : MonoBehaviour
    {
        #region Inspector 配置

        [Header("移动设置")]
        [Tooltip("水平移动速度")]
        [SerializeField] private float _moveSpeed = 8f;

        [Tooltip("加速时间（从 0 到最大速度需要多久，越小越灵敏）")]
        [SerializeField] private float _accelerationTime = 0.1f;

        [Tooltip("减速时间（松开方向键后多久停下来）")]
        [SerializeField] private float _decelerationTime = 0.2f;

        [Header("跳跃设置")]
        [Tooltip("跳跃力度")]
        [SerializeField] private float _jumpForce = 14f;

        [Tooltip("最大跳跃次数（1 = 普通跳跃, 2 = 二段跳）")]
        [SerializeField] private int _maxJumpCount = 2;

        [Tooltip("松开跳跃键时，垂直速度的衰减比例（0~1，越小跳得越低）")]
        [SerializeField] private float _jumpCutMultiplier = 0.5f;

        [Header("土狼时间（Coyote Time）")]
        [Tooltip("离开平台后仍可跳跃的时间窗口（秒）")]
        [SerializeField] private float _coyoteTime = 0.12f;

        [Header("跳跃缓冲（Jump Buffer）")]
        [Tooltip("落地前按跳跃键的缓冲时间（秒）")]
        [SerializeField] private float _jumpBufferTime = 0.1f;

        [Header("地面检测")]
        [Tooltip("地面检测点（通常放在角色脚底）")]
        [SerializeField] private Transform _groundCheckPoint;

        [Tooltip("地面检测半径")]
        [SerializeField] private float _groundCheckRadius = 0.2f;

        [Tooltip("地面图层（只检测这些图层）")]
        [SerializeField] private LayerMask _groundLayer;

        [Header("墙壁检测（预留功能）")]
        [Tooltip("墙壁检测点（通常放在角色侧面）")]
        [SerializeField] private Transform _wallCheckPoint;

        [Tooltip("墙壁检测半径")]
        [SerializeField] private float _wallCheckRadius = 0.2f;

        #endregion

        #region 组件引用

        // 刚体组件 - 控制物理运动
        private Rigidbody2D _rigidbody;

        // 玩家状态机
        private PlayerStateMachine _stateMachine;

        // Sprite 渲染器（用于翻转朝向）
        private SpriteRenderer _spriteRenderer;

        #endregion

        #region 运行时状态

        // 水平输入值（-1 到 1）
        private float _horizontalInput;

        // 当前水平速度（用于平滑加速/减速）
        private float _currentHorizontalSpeed;

        // SmoothDamp 内部使用的速度缓存
        // 注意：必须是独立字段，不能和 _currentHorizontalSpeed 共用同一个变量，
        // 否则插值计算会出错（角色移动会变得非常僵硬或抽搐）
        private float _speedSmoothVelocity;

        // 剩余跳跃次数
        private int _jumpCount;

        // 土狼时间计时器
        private float _coyoteTimer;

        // 跳跃缓冲计时器
        private float _jumpBufferTimer;

        // 是否正在地面上
        private bool _isGrounded;

        // 上一帧是否在地面上（用于检测刚离开地面的瞬间）
        private bool _wasGrounded;

        // 角色朝向（1 = 右, -1 = 左）
        private int _facingDirection = 1;

        // 是否正在贴墙滑行（预留）
        private bool _isWallSliding;

        #endregion

        #region 公共属性（供其他脚本读取）

        /// <summary>
        /// 当前水平移动速度
        /// </summary>
        public float HorizontalSpeed => _rigidbody != null ? _rigidbody.linearVelocity.x : 0f;

        /// <summary>
        /// 当前垂直速度
        /// </summary>
        public float VerticalSpeed => _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;

        /// <summary>
        /// 是否正在地面上
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// 角色朝向（1 = 右, -1 = 左）
        /// </summary>
        public int FacingDirection => _facingDirection;

        /// <summary>
        /// 是否正在移动（有水平输入）
        /// </summary>
        public bool IsMoving => Mathf.Abs(_horizontalInput) > 0.1f;

        /// <summary>
        /// 移动速度配置值（供战斗系统等外部使用）
        /// </summary>
        public float MoveSpeed => _moveSpeed;

        #endregion

        #region 生命周期

        private void Awake()
        {
            // 获取必需组件
            _rigidbody = GetComponent<Rigidbody2D>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            // 配置刚体
            // 冻结旋转，防止角色被碰撞推倒
            _rigidbody.freezeRotation = true;
        }

        private void Start()
        {
            // 如果场景中没有设置地面检测点，使用自身位置
            if (_groundCheckPoint == null)
            {
                Debug.LogWarning("[PlayerController] 未设置地面检测点，将使用角色底部");
                // 创建一个子物体作为地面检测点
                GameObject groundCheck = new GameObject("GroundCheck");
                groundCheck.transform.SetParent(transform);
                groundCheck.transform.localPosition = new Vector3(0, -0.5f, 0); // 假设角色高度约 1 单位
                _groundCheckPoint = groundCheck.transform;
            }
        }

        /// <summary>
        /// Update 每帧调用 - 处理输入
        /// </summary>
        private void Update()
        {
            // 读取水平输入（A/D 或 左/右方向键）
            _horizontalInput = Input.GetAxisRaw("Horizontal");

            // 处理跳跃输入
            HandleJumpInput();

            // 更新朝向
            UpdateFacingDirection();
        }

        /// <summary>
        /// FixedUpdate 固定物理帧调用 - 处理物理运动
        /// 注意：物理相关的代码都应放在 FixedUpdate 中
        /// </summary>
        private void FixedUpdate()
        {
            // 更新地面检测
            CheckGrounded();

            // 更新墙壁检测
            CheckWall();

            // 应用水平移动
            ApplyHorizontalMovement();

            // 更新土狼时间和跳跃缓冲计时器
            UpdateTimers();
        }

        #endregion

        #region 移动逻辑

        /// <summary>
        /// 应用水平移动 - 平滑加速和减速
        /// </summary>
        private void ApplyHorizontalMovement()
        {
            // 计算目标速度
            float targetSpeed = _horizontalInput * _moveSpeed;

            // 根据是否有输入选择不同的平滑时间
            float smoothTime = Mathf.Abs(targetSpeed) > 0.01f ? _accelerationTime : _decelerationTime;

            // 使用 SmoothDamp 实现平滑的速度过渡
            _currentHorizontalSpeed = Mathf.SmoothDamp(
                _currentHorizontalSpeed,      // 当前速度
                targetSpeed,                  // 目标速度
                ref _speedSmoothVelocity,     // SmoothDamp 的内部速度缓存（必须独立字段）
                smoothTime                    // 平滑时间
            );

            // 应用速度到刚体（只修改 X 轴，Y 轴由跳跃和重力控制）
            _rigidbody.linearVelocity = new Vector2(_currentHorizontalSpeed, _rigidbody.linearVelocity.y);
        }

        /// <summary>
        /// 更新角色朝向 - 根据移动方向翻转精灵
        /// </summary>
        private void UpdateFacingDirection()
        {
            // 只在有输入时更新朝向
            if (_horizontalInput > 0.1f)
            {
                _facingDirection = 1; // 朝右
            }
            else if (_horizontalInput < -0.1f)
            {
                _facingDirection = -1; // 朝左
            }

            // 通过翻转 X 轴缩放来改变朝向
            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _facingDirection == -1;
            }
        }

        #endregion

        #region 跳跃逻辑

        /// <summary>
        /// 处理跳跃输入
        /// </summary>
        private void HandleJumpInput()
        {
            // 检测跳跃键按下
            if (Input.GetButtonDown("Jump"))
            {
                // 记录跳跃缓冲时间
                _jumpBufferTimer = _jumpBufferTime;
            }

            // 检查是否可以跳跃：
            // 1. 有跳跃缓冲（最近按过跳跃键）
            // 2. 在地面上 或 在土狼时间内 或 还有剩余跳跃次数（二段跳）
            bool canJump = _jumpBufferTimer > 0f &&
                          (_isGrounded || _coyoteTimer > 0f || _jumpCount < _maxJumpCount);

            if (canJump)
            {
                Jump();
            }

            // 松开跳跃键时，减少上升速度（实现短按小跳，长按大跳）
            if (Input.GetButtonUp("Jump") && _rigidbody.linearVelocity.y > 0f)
            {
                // 减少向上的速度
                Vector2 velocity = _rigidbody.linearVelocity;
                velocity.y *= _jumpCutMultiplier;
                _rigidbody.linearVelocity = velocity;
            }
        }

        /// <summary>
        /// 执行跳跃
        /// </summary>
        private void Jump()
        {
            // 设置垂直速度为跳跃力度
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, _jumpForce);

            // 消耗一次跳跃次数
            _jumpCount++;

            // 重置土狼时间和跳跃缓冲
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;

            Debug.Log($"[Player] 跳跃！剩余跳跃次数: {_maxJumpCount - _jumpCount}");
        }

        /// <summary>
        /// 更新各种计时器
        /// </summary>
        private void UpdateTimers()
        {
            // 更新土狼时间计时器
            if (_isGrounded)
            {
                _coyoteTimer = _coyoteTime; // 在地面上时重置
            }
            else
            {
                _coyoteTimer -= Time.fixedDeltaTime; // 离开地面后倒计时
            }

            // 更新跳跃缓冲计时器
            _jumpBufferTimer -= Time.fixedDeltaTime;
        }

        #endregion

        #region 检测方法

        /// <summary>
        /// 地面检测 - 使用 OverlapCircle 检测脚下是否有地面
        /// </summary>
        private void CheckGrounded()
        {
            _wasGrounded = _isGrounded;

            // 使用 Physics2D.OverlapCircle 在检测点画一个圆，检查是否与地面图层碰撞
            _isGrounded = Physics2D.OverlapCircle(
                _groundCheckPoint.position,  // 检测位置
                _groundCheckRadius,          // 检测半径
                _groundLayer                 // 只检测地面图层
            );

            // 刚接触地面时重置跳跃次数
            if (_isGrounded && !_wasGrounded)
            {
                _jumpCount = 0;
            }

            // TODO: 可选 - 在 Scene 视图中绘制检测区域，方便调试
            // Debug.DrawRay 或 Gizmos.DrawWireSphere
        }

        /// <summary>
        /// 墙壁检测 - 检测角色是否贴着墙壁（预留功能）
        /// </summary>
        private void CheckWall()
        {
            if (_wallCheckPoint == null) return;

            _isWallSliding = Physics2D.OverlapCircle(
                _wallCheckPoint.position,
                _wallCheckRadius,
                _groundLayer // 墙壁也在地面图层上
            );

            // TODO: 实现墙壁滑行逻辑（减速下落、墙壁跳跃等）
        }

        #endregion

        #region 公共方法（供其他脚本调用）

        /// <summary>
        /// 设置水平速度（供战斗系统的冲刺等功能使用）
        /// </summary>
        public void SetHorizontalVelocity(float velocity)
        {
            _currentHorizontalSpeed = velocity;
            _rigidbody.linearVelocity = new Vector2(velocity, _rigidbody.linearVelocity.y);
        }

        /// <summary>
        /// 设置垂直速度（供战斗系统的击飞等功能使用）
        /// </summary>
        public void SetVerticalVelocity(float velocity)
        {
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, velocity);
        }

        /// <summary>
        /// 禁用移动控制（如被击晕、过场动画时）
        /// </summary>
        public void DisableControl()
        {
            enabled = false;
        }

        /// <summary>
        /// 恢复移动控制
        /// </summary>
        public void EnableControl()
        {
            enabled = true;
        }

        #endregion

        #region 调试可视化

        /// <summary>
        /// 在 Scene 视图中绘制调试辅助线框
        /// 只在编辑器中生效，不会影响游戏运行
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // 绘制地面检测圆
            if (_groundCheckPoint != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
            }

            // 绘制墙壁检测圆
            if (_wallCheckPoint != null)
            {
                Gizmos.color = _isWallSliding ? Color.blue : Color.yellow;
                Gizmos.DrawWireSphere(_wallCheckPoint.position, _wallCheckRadius);
            }
        }

        #endregion
    }
}
