using UnityEngine;

namespace DuduAdventure.Rendering
{
    /// <summary>
    /// Y-Sort 组件 —— 按对象的世界 Y 坐标动态设置 sortingOrder，实现 2.5D brawler 的深度排序。
    /// </summary>
    /// <remarks>
    /// 使用前提：Player Settings / Graphics 或相机的 Transparency Sort Mode = CustomAxis，Axis = (0,1,0)。
    /// 已在 SetTransparencySort 脚本中配置为全局默认。
    ///
    /// 组件行为：
    /// - 每帧把 sortingOrder 设置为 -round(y × precision)；
    /// - Y 越小（越靠近相机 / 下方）order 越大 → 绘制在前；
    /// - 挂在需要与其它角色/道具互相遮挡的物体上：玩家、敌人、地面道具、可拾取物；
    /// - 通常与 sortingLayerName = "Gameplay" 配合。
    ///
    /// 与 Transparency Sort Axis 的关系：
    /// - 单纯依赖 Custom Axis 也可以让"同一 sorting layer + 同 order"的 sprite 按 Y 排序；
    /// - 但精灵有子物体（如武器/阴影）或使用 SpriteRenderer 之外的渲染器时，主动写 order 更可控；
    /// - 因此我们两条腿走路：全局 Custom Axis + 关键动态物体挂 YSorter。
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class YSorter : MonoBehaviour
    {
        [Header("排序设置")]
        [Tooltip("目标 SpriteRenderer；留空自动使用自身或第一个子 SpriteRenderer")]
        [SerializeField] private SpriteRenderer _targetRenderer;

        [Tooltip("Y 排序精度：每 1 个世界单位对应多少个 sortingOrder 步长")]
        [Range(1, 1000)]
        [SerializeField] private int _precision = 100;

        [Tooltip("排序参考点在本物体的偏移；通常设成脚底（y = -0.5 或 y = -高度/2）")]
        [SerializeField] private Vector2 _pivotOffset = Vector2.zero;

        [Tooltip("是否同步给所有子 SpriteRenderer（如武器/披风等叠加层）")]
        [SerializeField] private bool _applyToChildren = true;

        [Tooltip("子 SpriteRenderer 相对主 order 的偏移量集合（按发现顺序循环使用）；留空全部继承主 order")]
        [SerializeField] private int[] _childOrderOffsets = new int[] { 1 };

        private SpriteRenderer[] _childRenderers;

        private void OnEnable()
        {
            ResolveTarget();
            CacheChildren();
            Apply();
        }

        private void ResolveTarget()
        {
            if (_targetRenderer != null) return;
            _targetRenderer = GetComponent<SpriteRenderer>();
            if (_targetRenderer == null) _targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void CacheChildren()
        {
            if (!_applyToChildren) { _childRenderers = null; return; }
            var all = GetComponentsInChildren<SpriteRenderer>(true);
            // 排除主 renderer 本身
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != _targetRenderer) n++;
            _childRenderers = new SpriteRenderer[n];
            int k = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] != _targetRenderer) _childRenderers[k++] = all[i];
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_targetRenderer == null) return;
            float refY = transform.position.y + _pivotOffset.y;
            int order = -Mathf.RoundToInt(refY * _precision);
            _targetRenderer.sortingOrder = order;

            if (_applyToChildren && _childRenderers != null && _childRenderers.Length > 0)
            {
                bool hasOffsets = _childOrderOffsets != null && _childOrderOffsets.Length > 0;
                for (int i = 0; i < _childRenderers.Length; i++)
                {
                    if (_childRenderers[i] == null) continue;
                    int add = hasOffsets ? _childOrderOffsets[i % _childOrderOffsets.Length] : 0;
                    _childRenderers[i].sortingOrder = order + add;
                    // 保证子 renderer 与主 renderer 在同一 sorting layer
                    _childRenderers[i].sortingLayerID = _targetRenderer.sortingLayerID;
                }
            }
        }

        /// <summary>
        /// 在编辑器中刷新缓存（Prefab 修改子物体后调用）。
        /// </summary>
        [ContextMenu("Refresh Child Renderers")]
        public void RefreshChildren()
        {
            CacheChildren();
            Apply();
        }
    }
}
