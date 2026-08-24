using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Dungeon
{
    /// <summary>
    /// 房间控制器 — 管理单个房间的战斗波次和清场状态
    /// </summary>
    /// <remarks>
    /// DNF 式副本流程：
    /// 1. 玩家进入房间 → 触发 Trigger
    /// 2. 入口关闭（RoomGate Lock）
    /// 3. 按波次刷怪（WaveSpawner）
    /// 4. 全部击杀 → 出口打开（RoomGate Unlock）
    /// 5. 进入下一房间重复
    /// </remarks>
    public class RoomController : MonoBehaviour
    {
        #region Inspector 配置

        [Header("房间设置")]
        [Tooltip("房间编号（从 0 开始）")]
        [SerializeField] private int _roomIndex;

        [Tooltip("是否为 Boss 房")]
        [SerializeField] private bool _isBossRoom;

        [Header("门控")]
        [Tooltip("入口门（玩家进入后关闭）")]
        [SerializeField] private RoomGate _entryGate;

        [Tooltip("出口门（清场后开启）")]
        [SerializeField] private RoomGate _exitGate;

        [Header("刷怪配置")]
        [Tooltip("每波敌人的配置")]
        [SerializeField] private WaveConfig[] _waves;

        [Header("波次间隔")]
        [Tooltip("击杀当前波后等待多久刷下一波（秒）")]
        [SerializeField] private float _waveCooldown = 1.5f;

        #endregion

        #region 事件

        /// <summary>房间战斗开始</summary>
        public event Action OnRoomActivated;

        /// <summary>房间清场完毕</summary>
        public event Action OnRoomCleared;

        /// <summary>当前波次变更 (waveIndex, totalWaves)</summary>
        public event Action<int, int> OnWaveChanged;

        #endregion

        #region 运行时状态

        private enum RoomState { Idle, Active, Cleared }
        private RoomState _state = RoomState.Idle;

        private int _currentWaveIndex;
        private readonly List<GameObject> _aliveEnemies = new();
        private Coroutine _waveRoutine;

        #endregion

        #region 公共属性

        public int RoomIndex => _roomIndex;
        public bool IsBossRoom => _isBossRoom;
        public bool IsCleared => _state == RoomState.Cleared;
        public bool IsActive => _state == RoomState.Active;

        #endregion

        #region 公共方法

        /// <summary>
        /// 激活房间（由 DungeonManager 或 Trigger 调用）
        /// </summary>
        public void ActivateRoom()
        {
            if (_state != RoomState.Idle) return;

            _state = RoomState.Active;

            // 关闭入口
            if (_entryGate != null)
                _entryGate.Lock();

            // 锁定出口
            if (_exitGate != null)
                _exitGate.Lock();

            Debug.Log($"[Room {_roomIndex}] 战斗开始！共 {_waves.Length} 波");
            OnRoomActivated?.Invoke();

            // 开始刷怪
            _currentWaveIndex = 0;
            _waveRoutine = StartCoroutine(SpawnWaveRoutine());
        }

        #endregion

        #region 刷怪逻辑

        private IEnumerator SpawnWaveRoutine()
        {
            while (_currentWaveIndex < _waves.Length)
            {
                var wave = _waves[_currentWaveIndex];
                OnWaveChanged?.Invoke(_currentWaveIndex, _waves.Length);
                Debug.Log($"[Room {_roomIndex}] 第 {_currentWaveIndex + 1}/{_waves.Length} 波");

                // 生成当前波的所有敌人
                SpawnWave(wave);

                // 等待当前波全部击杀
                yield return new WaitUntil(() => _aliveEnemies.Count == 0);

                _currentWaveIndex++;

                // 波次间隔
                if (_currentWaveIndex < _waves.Length)
                {
                    yield return new WaitForSeconds(_waveCooldown);
                }
            }

            // 全部波次清完
            ClearRoom();
        }

        private void SpawnWave(WaveConfig wave)
        {
            if (wave.Entries == null) return;

            foreach (var entry in wave.Entries)
            {
                if (entry.EnemyPrefab == null || entry.SpawnPoints == null) continue;

                for (int i = 0; i < entry.SpawnPoints.Length; i++)
                {
                    var spawnPos = entry.SpawnPoints[i] != null
                        ? entry.SpawnPoints[i].position
                        : transform.position + UnityEngine.Vector3.right * (i * 2f);

                    var enemy = Instantiate(entry.EnemyPrefab, spawnPos, Quaternion.identity, transform);
                    _aliveEnemies.Add(enemy);

                    // 监听敌人死亡
                    var health = enemy.GetComponent<Combat.HealthComponent>();
                    if (health != null)
                    {
                        health.OnDeath += () => OnEnemyDied(enemy);
                    }
                }
            }
        }

        private void OnEnemyDied(GameObject enemy)
        {
            _aliveEnemies.Remove(enemy);
        }

        private void ClearRoom()
        {
            _state = RoomState.Cleared;

            // 打开出口
            if (_exitGate != null)
                _exitGate.Unlock();

            // Boss 房打开入口让玩家可以回头捡东西
            if (_isBossRoom && _entryGate != null)
                _entryGate.Unlock();

            Debug.Log($"[Room {_roomIndex}] 清场完毕！{(_isBossRoom ? "Boss 已击杀！" : "出口已开启")}");
            OnRoomCleared?.Invoke();
        }

        #endregion
    }

    /// <summary>
    /// 单波敌人配置
    /// </summary>
    [Serializable]
    public class WaveConfig
    {
        [Tooltip("本波包含的敌人条目")]
        public WaveEntry[] Entries;
    }

    /// <summary>
    /// 波内单种敌人配置
    /// </summary>
    [Serializable]
    public class WaveEntry
    {
        [Tooltip("敌人预制体")]
        public GameObject EnemyPrefab;

        [Tooltip("生成位置（Transform 数组，每个位置生成一只）")]
        public Transform[] SpawnPoints;
    }
}
