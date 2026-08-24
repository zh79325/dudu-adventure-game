using UnityEngine;

namespace DuduAdventure.Camera
{
    /// <summary>
    /// 视差滚动层 —— 根据相机相对初始位置的位移，驱动本层的水平/竖直偏移。
    /// </summary>
    /// <remarks>
    /// 挂在每个背景层容器上（Sky / FarBackground / BackWall / NearForeground）。
    /// Ground / Gameplay 层不需要挂（视差 = 1，与相机同步等价于不做额外偏移）。
    /// 视差因子小于 1 = 远景（比相机慢），大于 1 = 近景（比相机快）。
    /// 建议因子：Sky 0.05, FarBackground 0.3, BackWall 0.6, NearForeground 1.2。
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("视差因子")]
        [Tooltip("水平视差：0 = 完全静止，1 = 与相机同速，>1 = 比相机快")]
        [Range(0f, 2f)]
        [SerializeField] private float _parallaxX = 0.3f;

        [Tooltip("竖直视差：brawler 一般设 0")]
        [Range(0f, 2f)]
        [SerializeField] private float _parallaxY = 0f;

        [Header("横向无缝平铺")]
        [Tooltip("是否自动横向循环（贴图必须可无缝拼接）")]
        [SerializeField] private bool _infiniteHorizontal = false;

        [Tooltip("单张贴图的世界宽度；为 0 时自动从子 SpriteRenderer.bounds 取值")]
        [SerializeField] private float _tileWidth = 0f;

        [Header("相机")]
        [Tooltip("留空则自动使用 Camera.main")]
        [SerializeField] private Transform _cameraTransform;

        // 本层锚点（初始位置）
        private Vector3 _initialPosition;
        // 相机锚点（初始位置）
        private Vector3 _initialCameraPos;
        private bool _hasAnchor;

        private void OnEnable()
        {
            EnsureCamera();
            SnapAnchor();
        }

        private void EnsureCamera()
        {
            if (_cameraTransform != null) return;
            var cam = UnityEngine.Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
        }

        /// <summary>
        /// 采样当前本层与相机的位置作为锚点。视差从锚点开始累计。
        /// </summary>
        public void SnapAnchor()
        {
            _initialPosition = transform.position;
            EnsureCamera();
            _initialCameraPos = _cameraTransform != null ? _cameraTransform.position : Vector3.zero;

            // 自动检测贴图宽度
            if (_infiniteHorizontal && _tileWidth <= 0f)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null) _tileWidth = sr.bounds.size.x;
            }
            _hasAnchor = true;
        }

        private void LateUpdate()
        {
            if (!_hasAnchor) SnapAnchor();
            if (_cameraTransform == null) return;

            Vector3 camDelta = _cameraTransform.position - _initialCameraPos;
            Vector3 target = new Vector3(
                _initialPosition.x + camDelta.x * _parallaxX,
                _initialPosition.y + camDelta.y * _parallaxY,
                _initialPosition.z
            );

            // 横向无缝循环：把 X 偏移收敛到 [-tileWidth/2, tileWidth/2)
            if (_infiniteHorizontal && _tileWidth > 0f)
            {
                float offset = target.x - _initialPosition.x;
                float wrapped = Mathf.Repeat(offset + _tileWidth * 0.5f, _tileWidth) - _tileWidth * 0.5f;
                target.x = _initialPosition.x + wrapped;
            }

            transform.position = target;
        }
    }
}
