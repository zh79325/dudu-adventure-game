using UnityEngine;

namespace DuduAdventure.Rendering
{
    /// <summary>
    /// 在角色脚下自动生成椭圆阴影，作为视觉锚点。
    /// 阴影位置跟随角色 XY 世界坐标，配合 Y-sort 保证阴影总是压在地面之上、角色之下。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterShadow : MonoBehaviour
    {
        [Header("阴影贴图")]
        [SerializeField] private Sprite _shadowSprite;

        [Header("阴影外观")]
        [Range(0f, 1f)] [SerializeField] private float _alpha = 0.55f;

        [Tooltip("阴影的世界尺寸（宽, 高），单位是世界单位。不受父物体缩放影响。")]
        [SerializeField] private Vector2 _worldSize = new Vector2(1.0f, 0.3f);

        [Tooltip("相对角色轴心的脚底偏移（世界单位）。")]
        [SerializeField] private Vector2 _footOffset = new Vector2(0f, -0.5f);

        [Tooltip("开启后按 SpriteRenderer 的实际包围盒自动推算脚底位置和阴影宽度。")]
        [SerializeField] private bool _autoFit = true;

        [Tooltip("自动推算时阴影宽度相对角色宽度的比例。")]
        [Range(0.3f, 1.5f)] [SerializeField] private float _autoWidthRatio = 0.85f;

        [Tooltip("自动推算时阴影高度相对宽度的比例（椭圆扁平度）。")]
        [Range(0.1f, 0.8f)] [SerializeField] private float _autoFlatten = 0.3f;

        [Header("排序")]
        [SerializeField] private string _sortingLayerName = "Gameplay";
        [SerializeField] private int _orderOffset = -1;

        [Header("材质")]
        [Tooltip("阴影使用的材质。留空时 URP 2D 会给新建的 SpriteRenderer 套 Sprite-Lit-Default，" +
                 "在没有光照的场景里会渲染成纯黑块，所以这里必须显式指定 Unlit 材质。")]
        [SerializeField] private Material _shadowMaterial;

        [Header("跟随目标")]
        [Tooltip("如果留空则跟随本 Transform（一般是角色根节点）。")]
        [SerializeField] private Transform _followTarget;

        private GameObject _shadowGO;
        private SpriteRenderer _shadowRenderer;

        private void OnEnable()
        {
            EnsureShadow();
            UpdateShadow();
        }

        private void LateUpdate()
        {
            if (_shadowGO == null) EnsureShadow();
            UpdateShadow();
        }

        private void EnsureShadow()
        {
            if (_followTarget == null) _followTarget = transform;

            if (_shadowGO == null)
            {
                // 先尝试查找已有子物体（避免重复生成）
                var existing = transform.Find("_Shadow");
                if (existing != null)
                {
                    _shadowGO = existing.gameObject;
                }
                else
                {
                    _shadowGO = new GameObject("_Shadow");
                    _shadowGO.transform.SetParent(transform, false);
                }
                _shadowGO.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
            }

            if (_shadowRenderer == null)
            {
                _shadowRenderer = _shadowGO.GetComponent<SpriteRenderer>();
                if (_shadowRenderer == null)
                {
                    _shadowRenderer = _shadowGO.AddComponent<SpriteRenderer>();
                }
            }

            if (_shadowSprite != null && _shadowRenderer.sprite != _shadowSprite)
            {
                _shadowRenderer.sprite = _shadowSprite;
            }

            if (_shadowMaterial != null && _shadowRenderer.sharedMaterial != _shadowMaterial)
            {
                _shadowRenderer.sharedMaterial = _shadowMaterial;
            }
        }

        private void UpdateShadow()
        {
            if (_shadowRenderer == null || _shadowRenderer.sprite == null) return;

            var bodyRenderer = GetComponent<SpriteRenderer>();
            Vector3 basePos = _followTarget != null ? _followTarget.position : transform.position;

            float footY = basePos.y + _footOffset.y;
            Vector2 size = _worldSize;

            // 自动拟合：用角色 SpriteRenderer 的世界包围盒推算脚底与宽度
            if (_autoFit && bodyRenderer != null && bodyRenderer.sprite != null)
            {
                Bounds b = bodyRenderer.bounds;
                footY = b.min.y + _footOffset.y;
                float w = b.size.x * _autoWidthRatio;
                size = new Vector2(w, w * _autoFlatten);
            }

            _shadowGO.transform.position = new Vector3(basePos.x + _footOffset.x, footY, basePos.z + 0.001f);
            _shadowGO.transform.rotation = Quaternion.identity;

            // 把世界尺寸换算成 localScale，抵消父物体缩放
            var sp = _shadowRenderer.sprite;
            float natW = sp.rect.width / sp.pixelsPerUnit;
            float natH = sp.rect.height / sp.pixelsPerUnit;
            Vector3 parentScale = transform.localToWorldMatrix.lossyScale;
            float psx = Mathf.Approximately(parentScale.x, 0f) ? 1f : Mathf.Abs(parentScale.x);
            float psy = Mathf.Approximately(parentScale.y, 0f) ? 1f : Mathf.Abs(parentScale.y);
            _shadowGO.transform.localScale = new Vector3(
                size.x / (natW * psx),
                size.y / (natH * psy),
                1f);

            _shadowRenderer.color = new Color(0f, 0f, 0f, _alpha);

            if (!string.IsNullOrEmpty(_sortingLayerName))
            {
                _shadowRenderer.sortingLayerName = _sortingLayerName;
            }

            int parentOrder = bodyRenderer != null ? bodyRenderer.sortingOrder : 0;
            _shadowRenderer.sortingOrder = parentOrder + _orderOffset;
        }

        private void OnDisable()
        {
            if (_shadowRenderer != null)
            {
                _shadowRenderer.enabled = false;
            }
        }
    }
}
