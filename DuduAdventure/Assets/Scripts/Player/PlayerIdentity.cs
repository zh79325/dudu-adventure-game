using System;
using System.Collections.Generic;
using UnityEngine;
using DuduAdventure.Combat;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家身份 - 挂在每个角色身上，标记"这是第几号玩家、用的哪个角色"
    /// </summary>
    /// <remarks>
    /// 采用自注册模式：组件自己在 OnEnable 时登记到 PlayerRegistry，OnDisable 时注销。
    /// 这样不管角色是 PlayerJoinManager 动态生成的、还是手工摆在场景里的，
    /// 注册表都能拿到完整名单，不需要额外记一遍。
    /// </remarks>
    [DisallowMultipleComponent]
    public class PlayerIdentity : MonoBehaviour
    {
        #region Inspector 配置

        [Header("玩家标识")]
        [Tooltip("玩家编号（1 = 1P, 2 = 2P ...），由 PlayerJoinManager 在加入时分配")]
        [SerializeField] private int _playerIndex = 1;

        [Tooltip("使用的角色 ID（wukong / bajie / shaseng / tangseng）")]
        [SerializeField] private string _characterId = "wukong";

        #endregion

        #region 公共属性

        /// <summary>
        /// 玩家编号（1P ~ 4P）
        /// </summary>
        public int PlayerIndex => _playerIndex;

        /// <summary>
        /// 使用的角色 ID。同一局里不允许重复，由 PlayerJoinManager 保证。
        /// </summary>
        public string CharacterId => _characterId;

        /// <summary>
        /// 本角色的生命值组件（可能为 null）
        /// </summary>
        public HealthComponent Health { get; private set; }

        /// <summary>
        /// 本角色绑定的设备输入源（键盘兜底的调试角色为 null）
        /// </summary>
        public DeviceInputSource DeviceInput { get; private set; }

        /// <summary>
        /// 是否还活着。没有血量组件时视为活着（灰盒调试角色）。
        /// </summary>
        public bool IsAlive => Health == null || !Health.IsDead;

        /// <summary>
        /// 是否是队长（镜头跟随的那个人）
        /// </summary>
        public bool IsCaptain => PlayerRegistry.Captain == this;

        #endregion

        #region 生命周期

        private void Awake()
        {
            Health = GetComponent<HealthComponent>();
            DeviceInput = GetComponent<DeviceInputSource>();
        }

        private void OnEnable()
        {
            PlayerRegistry.Register(this);
        }

        private void OnDisable()
        {
            // 退出游戏或切场景时也会走到这里，注册表内部做了空值容错
            PlayerRegistry.Unregister(this);
        }

        #endregion

        #region 配置

        /// <summary>
        /// 由 PlayerJoinManager 在生成角色后调用，写入编号与角色
        /// </summary>
        public void Configure(int playerIndex, string characterId)
        {
            _playerIndex = playerIndex;
            _characterId = characterId;
            gameObject.name = $"Player{playerIndex}_{characterId}";
        }

        #endregion
    }

    /// <summary>
    /// 玩家注册表 - 全局唯一的在场玩家名单，并管理"队长"
    /// </summary>
    /// <remarks>
    /// 为什么用静态类而不是 MonoBehaviour 单例：
    /// 单例要处理"场景里忘了放这个物体"、"Awake 顺序不确定导致 Instance 为 null"、
    /// "切场景后旧实例还在"这一堆麻烦。静态类没有生命周期，任何脚本任何时机都能安全访问。
    /// 名单的清理靠 PlayerIdentity 的 OnEnable/OnDisable 自注册来保证，
    /// 再加上每次读取时剔除已销毁的条目双重兜底。
    ///
    /// 队长的作用：镜头只跟队长，其他人的视野与队长一致；离屏的人会被拉回队长身边。
    /// 这是本地同屏合作最省事的方案——不用分屏，也不用动态缩放镜头。
    /// </remarks>
    public static class PlayerRegistry
    {
        #region 常量

        /// <summary>
        /// 最大同时游玩人数。定为 4 的原因：
        /// 同屏 4 人时特效与怪物已经很拥挤，再多会看不清自己的角色；
        /// 而且角色不允许重复，角色池就是 4 个（悟空/八戒/沙僧/唐僧）。
        /// </summary>
        public const int MaxPlayers = 4;

        #endregion

        #region 状态

        private static readonly List<PlayerIdentity> _players = new List<PlayerIdentity>();

        private static PlayerIdentity _captain;

        #endregion

        #region 事件

        /// <summary>有玩家加入</summary>
        public static event Action<PlayerIdentity> OnPlayerJoined;

        /// <summary>有玩家离开</summary>
        public static event Action<PlayerIdentity> OnPlayerLeft;

        /// <summary>队长变更（参数可能为 null，表示已无玩家）</summary>
        public static event Action<PlayerIdentity> OnCaptainChanged;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前在场玩家（只读）
        /// </summary>
        public static IReadOnlyList<PlayerIdentity> Players
        {
            get
            {
                Prune();
                return _players;
            }
        }

        /// <summary>
        /// 当前队长。没有玩家时为 null。
        /// </summary>
        public static PlayerIdentity Captain
        {
            get
            {
                // 队长可能已被销毁（角色死亡移除、切场景），此时立刻改选
                if (_captain == null && _players.Count > 0)
                {
                    PromoteNextCaptain();
                }

                return _captain;
            }
        }

        /// <summary>
        /// 当前人数
        /// </summary>
        public static int Count
        {
            get
            {
                Prune();
                return _players.Count;
            }
        }

        /// <summary>
        /// 是否还能再加人
        /// </summary>
        public static bool CanAcceptMorePlayers => Count < MaxPlayers;

        #endregion

        #region 注册管理

        /// <summary>
        /// 登记一个玩家
        /// </summary>
        public static void Register(PlayerIdentity player)
        {
            if (player == null) return;

            Prune();

            if (_players.Contains(player)) return;

            if (_players.Count >= MaxPlayers)
            {
                Debug.LogWarning(
                    $"[PlayerRegistry] 已达上限 {MaxPlayers} 人，拒绝登记 {player.name}。");
                return;
            }

            _players.Add(player);
            OnPlayerJoined?.Invoke(player);

            // 第一个加入的人自动当队长
            if (_captain == null)
            {
                SetCaptain(player);
            }

            Debug.Log($"[PlayerRegistry] {player.name} 加入，当前 {_players.Count} 人。");
        }

        /// <summary>
        /// 注销一个玩家
        /// </summary>
        public static void Unregister(PlayerIdentity player)
        {
            if (player == null) return;

            if (!_players.Remove(player)) return;

            OnPlayerLeft?.Invoke(player);

            // 队长走了就换人，避免镜头失去目标
            if (_captain == player)
            {
                _captain = null;
                PromoteNextCaptain();
            }
        }

        /// <summary>
        /// 清空名单（切场景或重开一局时调用）
        /// </summary>
        public static void Clear()
        {
            _players.Clear();
            _captain = null;
            OnCaptainChanged?.Invoke(null);
        }

        #endregion

        #region 队长管理

        /// <summary>
        /// 指定队长
        /// </summary>
        public static void SetCaptain(PlayerIdentity player)
        {
            if (player == null) return;

            if (!_players.Contains(player))
            {
                Debug.LogWarning($"[PlayerRegistry] {player.name} 不在名单里，不能当队长。");
                return;
            }

            if (_captain == player) return;

            _captain = player;
            OnCaptainChanged?.Invoke(_captain);

            Debug.Log($"[PlayerRegistry] 队长变更为 {player.name}（镜头将跟随他）。");
        }

        /// <summary>
        /// 从名单里挑下一个人当队长，优先挑活着的
        /// </summary>
        public static void PromoteNextCaptain()
        {
            Prune();

            PlayerIdentity next = null;

            // 优先活着的玩家，否则镜头会停在一具尸体上
            foreach (var p in _players)
            {
                if (p != null && p.IsAlive)
                {
                    next = p;
                    break;
                }
            }

            // 全员倒地时退而求其次，至少让镜头有个目标
            if (next == null && _players.Count > 0)
            {
                next = _players[0];
            }

            _captain = next;
            OnCaptainChanged?.Invoke(_captain);

            if (next != null)
            {
                Debug.Log($"[PlayerRegistry] 自动改选队长为 {next.name}。");
            }
        }

        #endregion

        #region 查询

        /// <summary>
        /// 找离指定位置最近的玩家 - 敌人锁定目标用
        /// </summary>
        /// <param name="from">查询原点（通常是敌人位置）</param>
        /// <param name="aliveOnly">是否只找活着的玩家</param>
        public static PlayerIdentity GetNearestPlayer(Vector2 from, bool aliveOnly = true)
        {
            Prune();

            PlayerIdentity nearest = null;
            float nearestSqr = float.MaxValue;

            foreach (var p in _players)
            {
                if (p == null) continue;
                if (aliveOnly && !p.IsAlive) continue;

                // 用平方距离比较，省掉每个玩家一次开方
                float sqr = ((Vector2)p.transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = p;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 分配下一个可用的玩家编号（1 ~ MaxPlayers）
        /// </summary>
        public static int GetNextFreePlayerIndex()
        {
            Prune();

            for (int index = 1; index <= MaxPlayers; index++)
            {
                bool taken = false;
                foreach (var p in _players)
                {
                    if (p != null && p.PlayerIndex == index)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken) return index;
            }

            return -1;
        }

        /// <summary>
        /// 某个角色是否已被人选走（角色不允许重复）
        /// </summary>
        public static bool IsCharacterTaken(string characterId)
        {
            Prune();

            foreach (var p in _players)
            {
                if (p != null && p.CharacterId == characterId) return true;
            }

            return false;
        }

        #endregion

        #region 内部维护

        /// <summary>
        /// 剔除已销毁的条目
        /// </summary>
        /// <remarks>
        /// 正常流程下 OnDisable 会注销，但角色被 Destroy 时若来不及走完流程，
        /// 名单里会留下"假活着"的空引用（Unity 的 == null 对已销毁对象返回 true）。
        /// 每次读取时清一遍，成本远低于一个查不出来的空引用异常。
        /// </remarks>
        private static void Prune()
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                if (_players[i] == null)
                {
                    _players.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 进入 Play 模式时重置静态状态
        /// </summary>
        /// <remarks>
        /// 如果项目开启了 Enter Play Mode Options 关闭域重载，静态字段不会自动清空，
        /// 上一局的玩家名单会带到下一局。这里显式清一次，两种设置下都安全。
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _players.Clear();
            _captain = null;

            // 事件订阅同样会残留，且残留的回调指向上一局已销毁的对象，调用即报错。
            // SubsystemRegistration 在场景加载之前执行，所以这里清空不会误删本局的订阅。
            OnPlayerJoined = null;
            OnPlayerLeft = null;
            OnCaptainChanged = null;
        }

        #endregion
    }
}
