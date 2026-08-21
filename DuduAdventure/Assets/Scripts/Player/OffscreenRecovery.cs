using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 掉队回收 - 非队长玩家离屏后自动传送回队长身边
    /// </summary>
    /// <remarks>
    /// DNF 式版本：不需要向下打射线找地面了（没有平台/重力），
    /// 直接传送到队长身边同一个纵深位置即可。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerIdentity))]
    public class OffscreenRecovery : MonoBehaviour
    {
        #region Inspector 配置

        [Header("离屏判定")]
        [Tooltip("视口外扩边距（0.12 = 允许超出屏幕边缘 12% 才算离屏）")]
        [SerializeField] private float _viewportMargin = 0.12f;

        [Tooltip("持续离屏多久才传送（秒）")]
        [SerializeField] private float _offscreenGraceTime = 0.6f;

        [Tooltip("两次传送之间的最短间隔（秒）")]
        [SerializeField] private float _teleportCooldown = 1f;

        [Header("落点设置")]
        [Tooltip("传送到队长身边的水平散开距离")]
        [SerializeField] private float _spreadPerPlayer = 0.8f;

        #endregion

        #region 组件引用

        private PlayerIdentity _identity;
        private PlayerController _controller;
        private Rigidbody2D _rigidbody;

        // 注意：这里必须写 UnityEngine.Camera 而不是 Camera。
        // 本项目存在 DuduAdventure.Camera 命名空间。
        private UnityEngine.Camera _camera;

        #endregion

        #region 运行时状态

        private float _offscreenTimer;
        private float _cooldownTimer;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _identity = GetComponent<PlayerIdentity>();
            _controller = GetComponent<PlayerController>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            // 队长是镜头中心，永远不会离屏
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

        private bool EnsureCamera()
        {
            if (_camera != null) return true;
            _camera = UnityEngine.Camera.main;
            return _camera != null;
        }

        private bool IsOffscreen()
        {
            Vector3 viewport = _camera.WorldToViewportPoint(transform.position);
            return viewport.x < -_viewportMargin
                || viewport.x > 1f + _viewportMargin
                || viewport.y < -_viewportMargin
                || viewport.y > 1f + _viewportMargin;
        }

        #endregion

        #region 传送

        private void TeleportToCaptain(PlayerIdentity captain)
        {
            Vector2 captainPos = captain.transform.position;

            // 按玩家编号左右交替散开
            int slot = Mathf.Max(1, _identity.PlayerIndex - 1);
            float direction = (slot % 2 == 0) ? -1f : 1f;
            float offsetX = direction * _spreadPerPlayer * Mathf.Ceil(slot / 2f);

            Vector2 destination = captainPos + new Vector2(offsetX, 0f);

            // DNF 式：直接传送到队长身边，使用队长的 GroundY 作为纵深
            transform.position = destination;

            // 同步 PlayerController 的地面 Y
            var captainController = captain.GetComponent<PlayerController>();
            if (_controller != null && captainController != null)
            {
                _controller.SetGroundY(captainController.GroundY);
            }

            // 清掉速度
            if (_rigidbody != null)
                _rigidbody.linearVelocity = Vector2.zero;

            _offscreenTimer = 0f;
            _cooldownTimer = _teleportCooldown;

            Debug.Log($"[OffscreenRecovery] {gameObject.name} 掉队，已传送回队长身边。");
        }

        #endregion
    }
}
