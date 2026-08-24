using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Dungeon
{
    /// <summary>
    /// 副本管理器 — 管理整个副本流程（房间进度、通关判定）
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 追踪所有房间的清场状态
    /// - 当 Boss 房清场时触发通关
    /// - 提供副本进度信息给 UI
    /// </remarks>
    public class DungeonManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("副本配置")]
        [Tooltip("副本名称")]
        [SerializeField] private string _dungeonName = "花果山";

        [Tooltip("按顺序排列的房间控制器")]
        [SerializeField] private RoomController[] _rooms;

        [Header("通关设置")]
        [Tooltip("Boss 击杀后延迟多久显示通关（秒）")]
        [SerializeField] private float _victoryDelay = 2f;

        #endregion

        #region 事件

        /// <summary>副本开始</summary>
        public event Action OnDungeonStarted;

        /// <summary>房间清场 (roomIndex)</summary>
        public event Action<int> OnRoomCleared;

        /// <summary>副本通关</summary>
        public event Action OnDungeonCompleted;

        /// <summary>副本失败（全灭）</summary>
        public event Action OnDungeonFailed;

        #endregion

        #region 运行时状态

        private int _currentRoomIndex;
        private bool _dungeonCompleted;

        #endregion

        #region 公共属性

        public static DungeonManager Instance { get; private set; }
        public string DungeonName => _dungeonName;
        public int CurrentRoomIndex => _currentRoomIndex;
        public int TotalRooms => _rooms != null ? _rooms.Length : 0;
        public bool IsCompleted => _dungeonCompleted;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_rooms == null || _rooms.Length == 0)
            {
                Debug.LogError("[DungeonManager] 没有配置房间！");
                return;
            }

            // 订阅所有房间的事件
            for (int i = 0; i < _rooms.Length; i++)
            {
                int index = i; // 闭包捕获
                _rooms[i].OnRoomCleared += () => HandleRoomCleared(index);
            }

            Debug.Log($"[DungeonManager] 副本 [{_dungeonName}] 开始，共 {_rooms.Length} 个房间");
            OnDungeonStarted?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region 内部逻辑

        private void HandleRoomCleared(int roomIndex)
        {
            Debug.Log($"[DungeonManager] 房间 {roomIndex + 1}/{_rooms.Length} 已清场");
            _currentRoomIndex = roomIndex + 1;
            OnRoomCleared?.Invoke(roomIndex);

            // 如果是 Boss 房，副本通关
            if (_rooms[roomIndex].IsBossRoom)
            {
                StartCoroutine(VictoryRoutine());
            }
        }

        private System.Collections.IEnumerator VictoryRoutine()
        {
            yield return new WaitForSeconds(_victoryDelay);

            _dungeonCompleted = true;
            Debug.Log($"[DungeonManager] 副本 [{_dungeonName}] 通关！");
            OnDungeonCompleted?.Invoke();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 副本失败（外部调用，如全灭时）
        /// </summary>
        public void FailDungeon()
        {
            if (_dungeonCompleted) return;
            Debug.Log($"[DungeonManager] 副本 [{_dungeonName}] 失败");
            OnDungeonFailed?.Invoke();
        }

        #endregion
    }
}
