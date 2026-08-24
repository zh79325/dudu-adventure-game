using UnityEngine;
using DuduAdventure.Core;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家控制器 - DNF 式 2.5D 横版格斗移动
    /// </summary>
    /// <remarks>
    /// 物理模型：
    /// - Rigidbody2D.gravityScale = 0，地面上自由走
    /// - X 轴 = 水平移动（通过 velocity，与墙壁/房间边界碰撞）
    /// - Y 轴 = 纵深移动 + 跳跃高度的叠加
    ///   - _groundY 记录角色在地面上的纵深坐标（按上下键改变）
    ///   - _jumpHeight 记录跳跃腾空高度（按跳跃键起跳，假重力拉回来）
    ///   - transform.position.y = _groundY + _jumpHeight
    /// - 精灵排序按 _groundY：越小（越靠镜头前方）排序值越大，渲染在前面
    ///
    /// 与平台跳跃的关键区别：
    /// - 没有真实重力、没有地面碰撞检测、没有平台
    /// - 跳跃是"原地腾空"，落回来还是同一个纵深位置
    /// - 上下走 ≠ 跳跃；上下是走位，跳跃是按 Jump 键
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStateMachine))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        #region Inspector 配置

        [Header("水平移动")]
        [Tooltip("水平移动速度")]
        [SerializeField] private float _moveSpeed = 8f;

        [Tooltip("加速时间（从 0 到最大速度）")]
        [SerializeField] private float _accelerationTime = 0.1f;

        [Tooltip("减速时间（松开方向键后多久停下来）")]
        [SerializeField] private float _decelerationTime = 0.15f;

        [Header("纵深移动（上下走位）")]
        [Tooltip("纵深方向移动速度（通常比水平慢一些）")]
        [SerializeField] private float _depthMoveSpeed = 5f;

        [Tooltip("纵深可走的下界（Y 坐标，越小越靠近镜头）")]
        [SerializeField] private float _minDepthY = -3f;

        [Tooltip("纵深可走的上界（Y 坐标，越大越远离镜头）")]
        [SerializeField] private float _maxDepthY = 1f;

        [Header("跳跃")]
        [Tooltip("跳跃初始速度")]
        [SerializeField] private float _jumpSpeed = 12f;

        [Tooltip("跳跃假重力（每秒减少的上升速度）")]
        [SerializeField] private float _jumpGravity = 40f;

        [Tooltip("最大跳跃次数（1 = 单跳, 2 = 二段跳）")]
        [SerializeField] private int _maxJumpCount = 1;

        #endregion

        #region 组件引用

        private Rigidbody2D _rigidbody;
        private PlayerStateMachine _stateMachine;
        private SpriteRenderer _spriteRenderer;

        // 本角色专属输入源
        private IPlayerInputSource _input;

        #endregion

        #region 运行时状态

        // 输入值
        private float _horizontalInput;
        private float _verticalInput;

        // 水平速度平滑
        private float _currentHorizontalSpeed;
        private float _speedSmoothVelocity;

        // 纵深地面 Y 坐标（不含跳跃高度）
        private float _groundY;

        // 跳跃
        private float _jumpHeight;      // 当前腾空高度（0 = 在地上）
        private float _jumpVelocity;    // 跳跃垂直速度
        private int _jumpCount;         // 已消耗的跳跃次数
        private bool _isGrounded;       // 是否在地面上

        // 朝向
        private int _facingDirection = 1;

        // 移动锁定（技能施法时）
        private bool _movementLocked;

        #endregion

        #region 公共属性

        /// <summary>当前水平速度</summary>
        public float HorizontalSpeed => _currentHorizontalSpeed;

        /// <summary>跳跃垂直速度（正 = 上升, 负 = 下落, 0 = 地面）</summary>
        public float VerticalSpeed => _jumpVelocity;

        /// <summary>是否在地面上（跳跃高度为 0）</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>角色朝向（1 = 右, -1 = 左）</summary>
        public int FacingDirection => _facingDirection;

        /// <summary>是否正在移动（有水平或纵深输入）</summary>
        public bool IsMoving => Mathf.Abs(_horizontalInput) > 0.1f || Mathf.Abs(_verticalInput) > 0.1f;

        /// <summary>移动速度配置值</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>当前地面 Y 坐标（用于深度排序）</summary>
        public float GroundY => _groundY;

        /// <summary>当前跳跃高度</summary>
        public float JumpHeight => _jumpHeight;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            _input = PlayerInputSourceResolver.Resolve(gameObject);

            // DNF 式不用重力，全部手动管理
            _rigidbody.gravityScale = 0f;
            _rigidbody.freezeRotation = true;
        }

        private void Start()
        {
            // 以角色初始 Y 坐标作为地面深度起点
            _groundY = transform.position.y;
            _isGrounded = true;
        }

        private void Update()
        {
            if (_movementLocked)
            {
                _horizontalInput = 0f;
                _verticalInput = 0f;
            }
            else
            {
                _horizontalInput = _input.Horizontal;
                _verticalInput = _input.Vertical;
            }

            HandleJumpInput();
            UpdateFacingDirection();
        }

        private void FixedUpdate()
        {
            ApplyDepthMovement();
            ApplyJumpPhysics();
            ApplyHorizontalMovement();
            SyncPosition();
        }

        #endregion

        #region 水平移动

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = _horizontalInput * _moveSpeed;
            float smoothTime = Mathf.Abs(targetSpeed) > 0.01f ? _accelerationTime : _decelerationTime;

            _currentHorizontalSpeed = Mathf.SmoothDamp(
                _currentHorizontalSpeed,
                targetSpeed,
                ref _speedSmoothVelocity,
                smoothTime
            );
        }

        private void UpdateFacingDirection()
        {
            if (_horizontalInput > 0.1f)
                _facingDirection = 1;
            else if (_horizontalInput < -0.1f)
                _facingDirection = -1;

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection == -1;
        }

        #endregion

        #region 纵深移动

        private void ApplyDepthMovement()
        {
            // 纵深输入改变 _groundY（地面上的前后位置）
            _groundY += _verticalInput * _depthMoveSpeed * Time.fixedDeltaTime;
            _groundY = Mathf.Clamp(_groundY, _minDepthY, _maxDepthY);
        }

        #endregion

        #region 跳跃

        private void HandleJumpInput()
        {
            if (_input.JumpPressed)
            {
                if (_jumpCount < _maxJumpCount)
                {
                    Jump();
                }
            }
        }

        private void Jump()
        {
            _jumpVelocity = _jumpSpeed;
            _jumpCount++;
            _isGrounded = false;
        }

        private void ApplyJumpPhysics()
        {
            if (_isGrounded) return;

            // 假重力
            _jumpVelocity -= _jumpGravity * Time.fixedDeltaTime;
            _jumpHeight += _jumpVelocity * Time.fixedDeltaTime;

            // 落地判定
            if (_jumpHeight <= 0f)
            {
                _jumpHeight = 0f;
                _jumpVelocity = 0f;
                _jumpCount = 0;
                _isGrounded = true;
            }
        }

        #endregion

        #region 位置同步

        /// <summary>
        /// 将水平速度交给 Rigidbody（享受 X 轴墙壁碰撞），
        /// Y 轴直接设置（纵深 + 跳跃高度），因为 Y 轴只需代码软边界。
        /// </summary>
        private void SyncPosition()
        {
            // X 轴通过 velocity 走物理碰撞
            float targetY = _groundY + _jumpHeight;
            float neededVelY = (targetY - transform.position.y) / Time.fixedDeltaTime;

            _rigidbody.linearVelocity = new Vector2(_currentHorizontalSpeed, neededVelY);
        }

        #endregion

        #region 公共方法

        /// <summary>设置水平速度（冲刺等外部调用）</summary>
        public void SetHorizontalVelocity(float velocity)
        {
            _currentHorizontalSpeed = velocity;
        }

        /// <summary>设置垂直速度（击飞等外部调用）</summary>
        public void SetVerticalVelocity(float velocity)
        {
            _jumpVelocity = velocity;
            if (velocity > 0f) _isGrounded = false;
        }

        /// <summary>禁用移动控制</summary>
        public void DisableControl()
        {
            enabled = false;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        /// <summary>恢复移动控制</summary>
        public void EnableControl()
        {
            enabled = true;
        }

        /// <summary>
        /// 锁定/解锁移动输入（技能施法时使用，物理仍然运行）
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
            if (locked)
            {
                _currentHorizontalSpeed = 0f;
            }
        }

        /// <summary>是否被锁定移动</summary>
        public bool IsMovementLocked => _movementLocked;

        /// <summary>
        /// 直接设置地面 Y（传送时使用，避免跳跃残留）
        /// </summary>
        public void SetGroundY(float y)
        {
            _groundY = Mathf.Clamp(y, _minDepthY, _maxDepthY);
            _jumpHeight = 0f;
            _jumpVelocity = 0f;
            _jumpCount = 0;
            _isGrounded = true;
        }

        #endregion

        #region 调试

        private void OnDrawGizmosSelected()
        {
            // 绘制纵深移动范围
            Gizmos.color = Color.cyan;
            Vector3 pos = transform.position;
            Gizmos.DrawLine(
                new Vector3(pos.x - 1f, _minDepthY, 0f),
                new Vector3(pos.x + 1f, _minDepthY, 0f));
            Gizmos.DrawLine(
                new Vector3(pos.x - 1f, _maxDepthY, 0f),
                new Vector3(pos.x + 1f, _maxDepthY, 0f));

            // 地面位置指示
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(new Vector3(pos.x, _groundY, 0f), 0.15f);
            }
        }

        #endregion
    }
}
