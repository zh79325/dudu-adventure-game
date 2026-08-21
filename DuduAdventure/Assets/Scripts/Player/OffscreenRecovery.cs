using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 掉队回收 - 非队长玩家离屏后自动传送回队长身边
    /// </summary>
    /// <remarks>
    /// 为什么需要这个：
    /// 本地同屏合作只有一个镜头，镜头跟队长。队长一往前跑，落后的人立刻看不见自己的角色，
    /// 只能盲操。传统做法是"镜头动态缩放框住所有人"，但本项目用了 Pixel Perfect Camera，
    /// 它每帧会按参考分辨率强行覆写 orthographicSize，动态缩放根本生效不了。
    /// 所以改成"掉队就拉回队长身边"——像《DNF》《我的世界地下城》那样，代价小且不会晕。
    ///
    /// 注意本组件挂在**每个角色**身上，队长自己会跳过检测（镜头就是跟他的，他不可能离屏）。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerIdentity))]
    public class OffscreenRecovery : MonoBehaviour
    {
        #region Inspector 配置

        [Header("离屏判定")]
        [Tooltip("视口外扩边距（0.1 = 允许超出屏幕边缘 10% 才算离屏，避免贴边时反复传送）")]
        [SerializeField] private float _viewportMargin = 0.12f;

        [Tooltip("持续离屏多久才传送（秒）。给一点宽容时间，避免跳跃出屏一瞬间就被拽回来。")]
        [SerializeField] private float _offscreenGraceTime = 0.6f;

        [Tooltip("两次传送之间的最短间隔（秒），防止落点不佳时反复触发")]
        [SerializeField] private float _teleportCooldown = 1f;

        [Header("落点设置")]
        [Tooltip("传送到队长身边的水平散开距离，按玩家编号错开，避免几个人叠在一起")]
        [SerializeField] private float _spreadPerPlayer = 0.8f;

        [Tooltip("从队长头顶多高处向下找地面")]
        [SerializeField] private float _groundSearchStartHeight = 3f;

        [Tooltip("向下找地面的最大距离")]
        [SerializeField] private float _groundSearchDistance = 12f;

        [Tooltip("落地后离地面的高度（防止卡进地形）")]
        [SerializeField] private float _landingClearance = 0.6f;

        [Tooltip("地面图层，必须与 PlayerController 的 Ground Layer 保持一致")]
        [SerializeField] private LayerMask _groundLayer;

        #endregion

        #region 组件引用

        private PlayerIdentity _identity;
        private Rigidbody2D _rigidbody;

        // 注意：这里必须写 UnityEngine.Camera 而不是 Camera。
        // 本项目存在 DuduAdventure.Camera 命名空间，在 DuduAdventure.Player 里裸写 Camera
        // 会被解析成那个命名空间，编译直接报错。
        private UnityEngine.Camera _camera;

        #endregion

        #region 运行时状态

        // 已经连续离屏多久
        private float _offscreenTimer;

        // 距离上次传送过了多久
        private float _cooldownTimer;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _identity = GetComponent<PlayerIdentity>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            // 队长是镜头中心，永远不会离屏，直接跳过
            if (_identity.IsCaptain)
            {
                _offscreenTimer = 0f;
                return;
            }

            var captain = PlayerRegistry.Captain;
            if (captain == null || captain == _identity) return;

            if (!EnsureCamera()) return;

            if (IsOffscreen())
            {
                _offscreenTimer += Time.deltaTime;

                if (_offscreenTimer >= _offscreenGraceTime && _cooldownTimer <= 0f)
                {
                    TeleportToCaptain(captain);
                }
            }
            else
            {
                _offscreenTimer = 0f;
            }
        }

        #endregion

        #region 离屏检测

        /// <summary>
        /// 确保拿到了主摄像机
        /// </summary>
        private bool EnsureCamera()
        {
            if (_camera != null) return true;

            _camera = UnityEngine.Camera.main;
            return _camera != null;
        }

        /// <summary>
        /// 当前是否在屏幕外
        /// </summary>
        private bool IsOffscreen()
        {
            // 视口坐标：(0,0) 是左下角，(1,1) 是右上角
            Vector3 viewport = _camera.WorldToViewportPoint(transform.position);

            return viewport.x < -_viewportMargin
                || viewport.x > 1f + _viewportMargin
                || viewport.y < -_viewportMargin
                || viewport.y > 1f + _viewportMargin;
        }

        #endregion

        #region 传送

        /// <summary>
        /// 传送到队长身边的安全落点
        /// </summary>
        private void TeleportToCaptain(PlayerIdentity captain)
        {
            Vector2 captainPos = captain.transform.position;

            // 按玩家编号左右错开，避免 3 个人传送后完全重叠、互相挤开
            // 编号 2 -> 右 0.8，编号 3 -> 左 1.6，编号 4 -> 右 2.4，如此交替
            int slot = Mathf.Max(1, _identity.PlayerIndex - 1);
            float direction = (slot % 2 == 0) ? -1f : 1f;
            float offsetX = direction * _spreadPerPlayer * Mathf.Ceil(slot / 2f);

            Vector2 destination = captainPos + new Vector2(offsetX, 0f);

            // 从队长头顶往下打一条射线找地面，避免把人传进墙里或悬空后立刻掉下去
            Vector2 rayOrigin = destination + Vector2.up * _groundSearchStartHeight;
            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin, Vector2.down, _groundSearchDistance, _groundLayer);

            if (hit.collider != null)
            {
                destination = hit.point + Vector2.up * _landingClearance;
            }
            else
            {
                // 找不到地面就直接落在队长头顶一点的位置，
                // 让重力把人带下去，总比留在屏幕外强
                destination = captainPos + new Vector2(offsetX, 1f);
            }

            transform.position = destination;

            // 清掉速度，否则会带着离屏时的下落速度继续往下冲，一落地就又掉出去
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }

            _offscreenTimer = 0f;
            _cooldownTimer = _teleportCooldown;

            Debug.Log($"[OffscreenRecovery] {gameObject.name} 掉队，已传送回队长身边 {destination}。");
        }

        #endregion
    }
}
