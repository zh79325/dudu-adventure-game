using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家加入管理器 - 本地同屏的"按键加入"入口
    /// </summary>
    /// <remarks>
    /// 为什么不用官方的 PlayerInputManager：
    /// 那套组件要配合 .inputactions 资产和 PlayerInput 组件，绑定过程发生在框架内部，
    /// 出问题时很难在自己的代码里看清"这个手柄到底绑给谁了"。
    /// 本地同屏的规则其实只有三条——一个设备对应一个角色、角色不能重复、最多 4 人——
    /// 直接轮询设备自己实现，全部逻辑都在这一个文件里，一个人维护更省心。
    ///
    /// 使用方式：场景里放一个空物体挂上本组件，配好角色名单和出生点即可。
    /// </remarks>
    [DisallowMultipleComponent]
    public class PlayerJoinManager : MonoBehaviour
    {
        #region 角色配置

        /// <summary>
        /// 一个可选角色
        /// </summary>
        [Serializable]
        public class CharacterEntry
        {
            [Tooltip("角色 ID，需与 PlayerIdentity 的 CharacterId 对应")]
            public string CharacterId = "wukong";

            [Tooltip("显示名，用于加入提示与 UI")]
            public string DisplayName = "孙悟空";

            [Tooltip("角色 Prefab。必须预先挂好 PlayerIdentity 与 DeviceInputSource。")]
            public GameObject Prefab;
        }

        #endregion

        #region Inspector 配置

        [Header("可选角色（顺序即分配顺序，不允许重复）")]
        [SerializeField]
        private List<CharacterEntry> _characters = new List<CharacterEntry>();

        [Header("出生点")]
        [Tooltip("首位玩家的出生点。留空则使用本物体位置。")]
        [SerializeField] private Transform _spawnPoint;

        [Tooltip("中途加入时，直接生成在队长身边而不是回到出生点")]
        [SerializeField] private bool _spawnBesideCaptainWhenLate = true;

        [Tooltip("生成在队长身边时的水平偏移")]
        [SerializeField] private float _besideCaptainOffset = 1.2f;

        [Header("按键")]
        [Tooltip("开局自动用键盘加入 1P（单人调试方便，正式发布可关掉）")]
        [SerializeField] private bool _autoJoinKeyboard = true;

        [Tooltip("是否允许中途按键退出")]
        [SerializeField] private bool _allowLeave = true;

        #endregion

        #region 运行时状态

        // 设备 -> 玩家，用来判断"这个手柄已经在玩了"
        private readonly Dictionary<InputDevice, PlayerIdentity> _deviceToPlayer =
            new Dictionary<InputDevice, PlayerIdentity>();

        #endregion

        #region 事件

        /// <summary>玩家成功加入（参数：玩家、使用的设备）</summary>
        public event Action<PlayerIdentity, InputDevice> OnPlayerJoined;

        /// <summary>加入被拒绝（参数：原因文本），用于在屏幕上给个提示</summary>
        public event Action<string> OnJoinRejected;

        #endregion

        #region 生命周期

        private void Start()
        {
            if (_autoJoinKeyboard && Keyboard.current != null)
            {
                TryJoin(Keyboard.current);
            }
        }

        private void Update()
        {
            PollJoinRequests();

            if (_allowLeave)
            {
                PollLeaveRequests();
            }

            CleanupDisconnectedDevices();
        }

        #endregion

        #region 按键轮询

        /// <summary>
        /// 检查有没有新设备按了"加入"
        /// </summary>
        private void PollJoinRequests()
        {
            // 手柄：Start / Options 键
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.startButton.wasPressedThisFrame)
                {
                    TryJoin(gamepad);
                }
            }

            // 键盘：回车
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                TryJoin(keyboard);
            }
        }

        /// <summary>
        /// 检查有没有玩家按了"退出"
        /// </summary>
        private void PollLeaveRequests()
        {
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.selectButton.wasPressedThisFrame)
                {
                    TryLeave(gamepad);
                }
            }
        }

        #endregion

        #region 加入 / 退出

        /// <summary>
        /// 尝试让某个设备加入游戏
        /// </summary>
        public bool TryJoin(InputDevice device)
        {
            if (device == null) return false;

            // 这个设备已经在玩了，不能一个人占两个角色
            if (_deviceToPlayer.TryGetValue(device, out var existing) && existing != null)
            {
                return false;
            }

            if (!PlayerRegistry.CanAcceptMorePlayers)
            {
                Reject($"已满 {PlayerRegistry.MaxPlayers} 人，无法再加入。");
                return false;
            }

            var entry = FindAvailableCharacter();
            if (entry == null)
            {
                Reject("没有可用角色了（角色不允许重复）。");
                return false;
            }

            if (entry.Prefab == null)
            {
                Reject($"角色 {entry.DisplayName} 没有配置 Prefab。");
                return false;
            }

            int playerIndex = PlayerRegistry.GetNextFreePlayerIndex();
            if (playerIndex < 0)
            {
                Reject("玩家编号已用尽。");
                return false;
            }

            Vector3 spawnPosition = GetSpawnPosition();

            // 生成角色。
            // 注意 Instantiate 的瞬间就会执行角色身上所有组件的 Awake，
            // 所以 DeviceInputSource 必须是 Prefab 上预先挂好的；
            // 如果指望这里 AddComponent，PlayerController 在 Awake 里已经兜底
            // 补了一个键盘输入源，结果就是所有人一起响应键盘。
            var instance = Instantiate(entry.Prefab, spawnPosition, Quaternion.identity);

            var identity = instance.GetComponent<PlayerIdentity>();
            if (identity == null)
            {
                Debug.LogError(
                    $"[PlayerJoinManager] {entry.Prefab.name} 上没有 PlayerIdentity，无法加入。");
                Destroy(instance);
                return false;
            }

            var inputSource = instance.GetComponent<DeviceInputSource>();
            if (inputSource == null)
            {
                Debug.LogError(
                    $"[PlayerJoinManager] {entry.Prefab.name} 上没有 DeviceInputSource，" +
                    "该角色将无法接收手柄输入。请把组件挂到 Prefab 上。");
                Destroy(instance);
                return false;
            }

            identity.Configure(playerIndex, entry.CharacterId);
            inputSource.Bind(device);

            _deviceToPlayer[device] = identity;

            Debug.Log(
                $"[PlayerJoinManager] {playerIndex}P 加入，角色 {entry.DisplayName}，" +
                $"设备 {device.displayName}。");

            OnPlayerJoined?.Invoke(identity, device);
            return true;
        }

        /// <summary>
        /// 让某个设备对应的玩家退出
        /// </summary>
        public bool TryLeave(InputDevice device)
        {
            if (device == null) return false;

            if (!_deviceToPlayer.TryGetValue(device, out var identity)) return false;

            _deviceToPlayer.Remove(device);

            if (identity != null)
            {
                Debug.Log($"[PlayerJoinManager] {identity.PlayerIndex}P 退出。");

                // PlayerIdentity 的 OnDisable 会自动从注册表注销，
                // 如果他是队长，注册表会自动改选下一个人，镜头不会失去目标
                Destroy(identity.gameObject);
            }

            return true;
        }

        #endregion

        #region 内部工具

        /// <summary>
        /// 找一个还没被选走的角色
        /// </summary>
        private CharacterEntry FindAvailableCharacter()
        {
            foreach (var entry in _characters)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CharacterId)) continue;
                if (PlayerRegistry.IsCharacterTaken(entry.CharacterId)) continue;
                return entry;
            }

            return null;
        }

        /// <summary>
        /// 计算出生位置
        /// </summary>
        private Vector3 GetSpawnPosition()
        {
            var captain = PlayerRegistry.Captain;

            // 中途加入时生成在队长身边，否则新玩家会出现在屏幕外，
            // 然后被 OffscreenRecovery 拉回来，多绕一圈还闪一下
            if (_spawnBesideCaptainWhenLate && captain != null)
            {
                return captain.transform.position + new Vector3(_besideCaptainOffset, 0.5f, 0f);
            }

            return _spawnPoint != null ? _spawnPoint.position : transform.position;
        }

        /// <summary>
        /// 清理已拔掉的设备
        /// </summary>
        /// <remarks>
        /// 手柄拔掉后如果不清理，这个设备会永远占着一个角色名额，
        /// 重新插上时又是一个新的 device 对象，玩家会发现"人满了但只有 2 个人在玩"。
        /// </remarks>
        private void CleanupDisconnectedDevices()
        {
            List<InputDevice> stale = null;

            foreach (var pair in _deviceToPlayer)
            {
                bool deviceGone = pair.Key == null || !pair.Key.added;
                bool playerGone = pair.Value == null;

                if (deviceGone || playerGone)
                {
                    stale ??= new List<InputDevice>();
                    stale.Add(pair.Key);
                }
            }

            if (stale == null) return;

            foreach (var device in stale)
            {
                if (_deviceToPlayer.TryGetValue(device, out var identity) && identity != null)
                {
                    // 设备断开就解绑，角色留在场上会变成完全不受控的木头人，
                    // 直接移除更干净；后续可以改成"等待重连"
                    identity.DeviceInput?.Unbind();
                    Destroy(identity.gameObject);
                }

                _deviceToPlayer.Remove(device);
            }
        }

        private void Reject(string reason)
        {
            Debug.LogWarning($"[PlayerJoinManager] 加入被拒绝：{reason}");
            OnJoinRejected?.Invoke(reason);
        }

        #endregion
    }
}
