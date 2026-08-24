using UnityEngine;

namespace DuduAdventure.Dungeon
{
    /// <summary>
    /// 房间进入触发器 — 玩家进入时激活对应 RoomController
    /// </summary>
    /// <remarks>
    /// 放在每个房间入口处，Trigger Collider2D。
    /// 玩家进入时通知 RoomController 开始战斗（如果房间还没被激活过）。
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class RoomTrigger : MonoBehaviour
    {
        [Tooltip("关联的房间控制器")]
        [SerializeField] private RoomController _room;

        [Tooltip("触发后是否自动销毁 Trigger（防止重复触发）")]
        [SerializeField] private bool _destroyAfterTrigger = true;

        private bool _triggered;

        private void Reset()
        {
            // 自动设置为 Trigger
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;

            // 只响应玩家层（Layer 9）
            if (other.gameObject.layer != 9) return;

            if (_room == null || _room.IsActive || _room.IsCleared) return;

            _triggered = true;
            _room.ActivateRoom();

            if (_destroyAfterTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
