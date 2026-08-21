using UnityEngine;

namespace DuduAdventure.Core
{
    /// <summary>
    /// 深度排序 - 按 Y 坐标自动设置 SpriteRenderer 的 sortingOrder
    /// </summary>
    /// <remarks>
    /// DNF 式横版格斗的核心视觉要素：Y 坐标越小（越靠近镜头/屏幕下方）的角色
    /// 应渲染在 Y 坐标更大的角色前面。这个组件每帧根据地面 Y 坐标更新排序值。
    ///
    /// 用法：挂到任何需要参与深度排序的物体上（玩家、敌人、可交互物体）。
    /// 如果物体上有 PlayerController，会读取 GroundY（排除跳跃高度的干扰）；
    /// 否则直接用 transform.position.y。
    /// </remarks>
    public class DepthSortByY : MonoBehaviour
    {
        [Tooltip("排序精度：Y 值乘以这个系数取整后作为 sortingOrder。值越大精度越高。")]
        [SerializeField] private int _sortPrecision = 100;

        [Tooltip("基础偏移量（用于在不同 Sorting Layer 里微调前后关系）")]
        [SerializeField] private int _sortOffset = 0;

        private SpriteRenderer _spriteRenderer;
        private Player.PlayerController _playerController;

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _playerController = GetComponent<Player.PlayerController>();
        }

        private void LateUpdate()
        {
            if (_spriteRenderer == null) return;

            // 有 PlayerController 时用 GroundY（跳跃不影响排序），否则用 position.y
            float depthY = _playerController != null
                ? _playerController.GroundY
                : transform.position.y;

            // Y 越小（越靠前/靠下）→ sortingOrder 越大 → 渲染在前面
            _spriteRenderer.sortingOrder = _sortOffset - Mathf.RoundToInt(depthY * _sortPrecision);
        }
    }
}
