using UnityEngine;

namespace DuduAdventure.Camera
{
    /// <summary>
    /// 摄像机跟随控制器 - 平滑跟随玩家，提供前瞻和震屏效果
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 平滑跟随让镜头移动自然流畅
    /// - 前瞻（Look-Ahead）让玩家看到更多前进方向的内容
    /// - 边界限制防止镜头超出关卡范围
    /// - 屏幕震动增强打击感
    /// </remarks>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        #region Inspector 配置

        [Header("跟随目标")]
        [Tooltip("自动跟随队长（本地多人时应保持开启）")]
        [SerializeField] private bool _followCaptain = true;

        [Tooltip("跟随的目标（通常是玩家）。开启跟随队长时会被自动覆盖。")]
        [SerializeField] private Transform _target;

        [Header("跟随设置")]
        [Tooltip("摄像机与目标的偏移距离")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1f, -10f);

        [Tooltip("跟随平滑度（越小越平滑，越大越灵敏）")]
        [SerializeField] private float _smoothSpeed = 8f;

        [Header("前瞻设置（Look-Ahead）")]
        [Tooltip("前瞻距离（根据目标速度偏移摄像机）")]
        [SerializeField] private float _lookAheadDistance = 2f;

        [Tooltip("前瞻平滑度")]
        [SerializeField] private float _lookAheadSmoothSpeed = 4f;

        [Header("边界限制")]
        [Tooltip("是否启用边界限制")]
        [SerializeField] private bool _useBounds = true;

        [Tooltip("自动计算边界（根据场景中 Environment 物体的渲染范围）")]
        [SerializeField] private bool _autoBounds = true;

        [Tooltip("自动边界额外留白（单位：世界坐标）")]
        [SerializeField] private float _boundsPadding = 2f;

        [Tooltip("关卡最小边界（左下角）— autoBounds 开启时会被自动覆盖")]
        [SerializeField] private Vector2 _boundsMin = new Vector2(-50f, -10f);

        [Tooltip("关卡最大边界（右上角）— autoBounds 开启时会被自动覆盖")]
        [SerializeField] private Vector2 _boundsMax = new Vector2(50f, 30f);

        [Header("屏幕震动")]
        [Tooltip("最大震动幅度")]
        [SerializeField] private float _maxShakeAmount = 0.5f;

        [Tooltip("震动衰减速度")]
        [SerializeField] private float _shakeDecay = 5f;

        #endregion

        #region 运行时状态

        // 当前前瞻偏移
        private Vector3 _currentLookAhead;

        // 目标前瞻偏移
        private Vector3 _targetLookAhead;

        // 屏幕震动相关
        private float _shakeIntensity;       // 当前震动强度
        private float _shakeDuration;        // 震动持续时间

        // 摄像机 Z 轴固定值（2D 游戏中 Z 轴不变）
        private float _fixedZ;

        // 摄像机视口大小的一半（用于边界计算）
        private float _halfCameraHeight;
        private float _halfCameraWidth;

        #endregion

        #region 公共属性

        /// <summary>
        /// 跟随目标
        /// </summary>
        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            _fixedZ = transform.position.z;
        }

        private void Start()
        {
            // 解析跟随目标
            ResolveTarget();

            if (_target == null)
            {
                Debug.LogWarning("[CameraFollow] 未找到跟随目标，等待玩家加入或在 Inspector 中设置 Target");
            }

            // 自动计算边界
            if (_autoBounds && _useBounds)
            {
                RecalculateBounds();
            }

            // 计算视口半尺寸（用于边界限制）
            UpdateViewportSize();
        }

        /// <summary>
        /// 解析当前应该跟随谁
        /// </summary>
        /// <remarks>
        /// 每帧都调用，而不是只在 Start 里解析一次。原因有三个：
        /// 1. 玩家是中途按键加入的，开局时名单可能是空的；
        /// 2. 队长可能中途变更（原队长掉线或倒地）；
        /// 3. 队长角色被销毁后 _target 会变成空引用，必须及时换人。
        /// 这里只是读一个静态属性并做引用比较，开销可以忽略。
        /// </remarks>
        private void ResolveTarget()
        {
            if (_followCaptain)
            {
                var captain = DuduAdventure.Player.PlayerRegistry.Captain;
                if (captain != null)
                {
                    _target = captain.transform;
                    return;
                }
            }

            // 兜底：注册表里没人时按标签找。
            // 灰盒调试场景里的角色没挂 PlayerIdentity，靠这条分支才能跟上。
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _target = player.transform;
                }
            }
        }

        /// <summary>
        /// LateUpdate 在所有 Update 之后调用
        /// 摄像机跟随必须放在 LateUpdate 中，否则会出现抖动
        /// （因为角色的 Update 移动先于摄像机的 LateUpdate 跟随）
        /// </summary>
        private void LateUpdate()
        {
            // 每帧重新解析：玩家中途加入、队长变更、队长被销毁都靠这一步兜住
            ResolveTarget();

            if (_target == null) return;

            // 更新视口大小（窗口可能缩放）
            UpdateViewportSize();

            // 计算目标位置
            Vector3 targetPosition = CalculateTargetPosition();

            // 应用前瞻
            UpdateLookAhead();
            targetPosition += _currentLookAhead;

            // 应用边界限制
            if (_useBounds)
            {
                targetPosition = ClampToBounds(targetPosition);
            }

            // 应用屏幕震动
            Vector3 shakeOffset = CalculateShakeOffset();
            targetPosition += shakeOffset;

            // 平滑移动到目标位置
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                _smoothSpeed * Time.deltaTime
            );
        }

        #endregion

        #region 跟随逻辑

        /// <summary>
        /// 计算摄像机的目标位置
        /// </summary>
        private Vector3 CalculateTargetPosition()
        {
            return new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                _fixedZ // Z 轴保持不变
            );
        }

        /// <summary>
        /// 更新前瞻偏移 - 根据目标移动方向偏移摄像机
        /// </summary>
        private void UpdateLookAhead()
        {
            if (_target == null) return;

            // 获取目标的 Rigidbody2D 来计算速度方向
            Rigidbody2D targetRb = _target.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                Vector2 velocity = targetRb.linearVelocity;

                // 只在水平方向做前瞻
                _targetLookAhead = new Vector3(
                    Mathf.Clamp(velocity.x / 10f, -1f, 1f) * _lookAheadDistance,
                    0f,
                    0f
                );
            }

            // 平滑过渡前瞻偏移
            _currentLookAhead = Vector3.Lerp(
                _currentLookAhead,
                _targetLookAhead,
                _lookAheadSmoothSpeed * Time.deltaTime
            );
        }

        #endregion

        #region 边界限制

        /// <summary>
        /// 将摄像机位置限制在关卡边界内
        /// </summary>
        private Vector3 ClampToBounds(Vector3 position)
        {
            // 计算有效的边界（考虑摄像机视口大小）
            float effectiveMinX = _boundsMin.x + _halfCameraWidth;
            float effectiveMaxX = _boundsMax.x - _halfCameraWidth;
            float effectiveMinY = _boundsMin.y + _halfCameraHeight;
            float effectiveMaxY = _boundsMax.y - _halfCameraHeight;

            // 如果边界范围小于视口大小，居中显示
            if (effectiveMinX > effectiveMaxX)
            {
                position.x = (_boundsMin.x + _boundsMax.x) * 0.5f;
            }
            else
            {
                position.x = Mathf.Clamp(position.x, effectiveMinX, effectiveMaxX);
            }

            if (effectiveMinY > effectiveMaxY)
            {
                position.y = (_boundsMin.y + _boundsMax.y) * 0.5f;
            }
            else
            {
                position.y = Mathf.Clamp(position.y, effectiveMinY, effectiveMaxY);
            }

            return position;
        }

        /// <summary>
        /// 更新视口半尺寸
        /// </summary>
        private void UpdateViewportSize()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            _halfCameraHeight = cam.orthographicSize;
            _halfCameraWidth = _halfCameraHeight * cam.aspect;
        }

        #endregion

        #region 屏幕震动

        /// <summary>
        /// 触发屏幕震动
        /// </summary>
        /// <param name="intensity">震动强度（0~1）</param>
        /// <param name="duration">持续时间（秒）</param>
        public void Shake(float intensity = 0.5f, float duration = 0.3f)
        {
            _shakeIntensity = Mathf.Clamp01(intensity) * _maxShakeAmount;
            _shakeDuration = duration;
        }

        /// <summary>
        /// 触发大震动（Boss 攻击等）
        /// </summary>
        public void BigShake()
        {
            Shake(1.0f, 0.5f);
        }

        /// <summary>
        /// 触发小震动（普通攻击命中等）
        /// </summary>
        public void SmallShake()
        {
            Shake(0.3f, 0.15f);
        }

        /// <summary>
        /// 计算当前帧的震动偏移量
        /// </summary>
        private Vector3 CalculateShakeOffset()
        {
            if (_shakeDuration <= 0f || _shakeIntensity <= 0f)
            {
                return Vector3.zero;
            }

            // 递减震动持续时间
            _shakeDuration -= Time.deltaTime;

            // 使用 PerlinNoise 生成平滑的震动（比 Random 更自然）
            float seed = Time.unscaledTime * 25f;
            float shakeX = (Mathf.PerlinNoise(seed, 0f) - 0.5f) * 2f * _shakeIntensity;
            float shakeY = (Mathf.PerlinNoise(0f, seed) - 0.5f) * 2f * _shakeIntensity;

            // 震动强度逐渐衰减
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, _shakeDecay * Time.deltaTime);

            return new Vector3(shakeX, shakeY, 0f);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置关卡边界（通常由 LevelManager 在关卡加载时调用）
        /// </summary>
        /// <param name="min">左下角</param>
        /// <param name="max">右上角</param>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            _boundsMin = min;
            _boundsMax = max;
            _useBounds = true;
        }

        /// <summary>
        /// 根据场景内容重新计算摄像机边界
        /// </summary>
        /// <remarks>
        /// 优先使用名为 "Environment" 的 GameObject 下所有 Renderer 的包围盒。
        /// 如果找不到 Environment，则回退到场景中所有 SpriteRenderer。
        /// 可在副本加载完成或房间动态生成后调用，确保摄像机能覆盖全部区域。
        /// </remarks>
        public void RecalculateBounds()
        {
            Bounds sceneBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool found = false;

            // 优先从 Environment 物体获取范围
            var envGO = GameObject.Find("Environment");
            Renderer[] renderers = null;

            if (envGO != null)
            {
                renderers = envGO.GetComponentsInChildren<Renderer>();
            }

            // 回退：全场景 SpriteRenderer
            if (renderers == null || renderers.Length == 0)
            {
                renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            }

            foreach (var r in renderers)
            {
                if (!found)
                {
                    sceneBounds = r.bounds;
                    found = true;
                }
                else
                {
                    sceneBounds.Encapsulate(r.bounds);
                }
            }

            if (!found)
            {
                Debug.LogWarning("[CameraFollow] RecalculateBounds: 未找到任何 Renderer，保持当前边界");
                return;
            }

            _boundsMin = new Vector2(sceneBounds.min.x - _boundsPadding, sceneBounds.min.y - _boundsPadding);
            _boundsMax = new Vector2(sceneBounds.max.x + _boundsPadding, sceneBounds.max.y + _boundsPadding);

            Debug.Log($"[CameraFollow] 自动边界: ({_boundsMin.x:F1}, {_boundsMin.y:F1}) ~ ({_boundsMax.x:F1}, {_boundsMax.y:F1})");
        }

        /// <summary>
        /// 立即传送到目标位置（场景加载完成时使用，避免平滑移动）
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null) return;

            transform.position = new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                _fixedZ
            );

            // 重置前瞻
            _currentLookAhead = Vector3.zero;
            _targetLookAhead = Vector3.zero;
        }

        /// <summary>
        /// 设置跟随偏移
        /// </summary>
        public void SetOffset(Vector3 offset)
        {
            _offset = offset;
        }

        #endregion

        #region 调试可视化

        private void OnDrawGizmosSelected()
        {
            if (!_useBounds) return;

            // 绘制关卡边界框
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(
                (_boundsMin.x + _boundsMax.x) * 0.5f,
                (_boundsMin.y + _boundsMax.y) * 0.5f,
                0f
            );
            Vector3 size = new Vector3(
                _boundsMax.x - _boundsMin.x,
                _boundsMax.y - _boundsMin.y,
                0.1f
            );
            Gizmos.DrawWireCube(center, size);
        }

        #endregion
    }
}
