using UnityEngine;

namespace DuduAdventure.Dungeon
{
    /// <summary>
    /// 房间门/障碍物 — 清场前阻挡通行，清场后打开
    /// </summary>
    /// <remarks>
    /// 实现方式：
    /// - Lock 时激活 Collider2D（阻挡）+ 显示门的 Sprite
    /// - Unlock 时关闭 Collider2D + 隐藏/播放开门动画
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class RoomGate : MonoBehaviour
    {
        [Header("视觉")]
        [Tooltip("门关闭时显示的对象（可以是子物体带 Sprite）")]
        [SerializeField] private GameObject _closedVisual;

        [Tooltip("门打开时显示的对象（可选）")]
        [SerializeField] private GameObject _openedVisual;

        [Header("设置")]
        [Tooltip("初始是否锁定")]
        [SerializeField] private bool _startLocked = true;

        private Collider2D _collider;
        private bool _isLocked;

        public bool IsLocked => _isLocked;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_startLocked)
                Lock();
            else
                Unlock();
        }

        /// <summary>锁定门（阻挡通行）</summary>
        public void Lock()
        {
            _isLocked = true;
            _collider.enabled = true;

            if (_closedVisual != null) _closedVisual.SetActive(true);
            if (_openedVisual != null) _openedVisual.SetActive(false);
        }

        /// <summary>解锁门（允许通行）</summary>
        public void Unlock()
        {
            _isLocked = false;
            _collider.enabled = false;

            if (_closedVisual != null) _closedVisual.SetActive(false);
            if (_openedVisual != null) _openedVisual.SetActive(true);

            Debug.Log($"[RoomGate] {gameObject.name} 已开启");
        }
    }
}
