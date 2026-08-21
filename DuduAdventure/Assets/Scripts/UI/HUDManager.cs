using UnityEngine;
using DuduAdventure.Player;

namespace DuduAdventure.UI
{
    /// <summary>
    /// HUD 管理器 - 管理玩家 HUD 面板的创建和绑定
    /// </summary>
    /// <remarks>
    /// 使用方式：
    /// 1. 场景中放一个 Canvas，挂 HUDManager
    /// 2. 配置 PlayerHUD Prefab（带 ResourceBar 等子物体）
    /// 3. 玩家角色生成后调用 RegisterPlayer() 绑定
    /// 
    /// 支持多人：最多 4 个玩家，HUD 横向排列在底部
    /// </remarks>
    public class HUDManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("配置")]
        [Tooltip("PlayerHUD Prefab")]
        [SerializeField] private GameObject _playerHUDPrefab;

        [Tooltip("HUD 容器（HorizontalLayoutGroup）")]
        [SerializeField] private RectTransform _hudContainer;

        [Header("布局")]
        [Tooltip("最大显示 HUD 数量")]
        [SerializeField] private int _maxHUDs = 4;

        #endregion

        #region 运行时

        private PlayerHUD[] _huds;
        private int _hudCount;

        #endregion

        #region 单例

        public static HUDManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _huds = new PlayerHUD[_maxHUDs];
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 注册一个玩家，创建对应的 HUD
        /// </summary>
        public PlayerHUD RegisterPlayer(GameObject player)
        {
            if (_hudCount >= _maxHUDs)
            {
                Debug.LogWarning("[HUDManager] HUD 数量已满");
                return null;
            }

            if (_playerHUDPrefab == null || _hudContainer == null)
            {
                Debug.LogWarning("[HUDManager] 未配置 HUD Prefab 或容器");
                return null;
            }

            // 生成 HUD
            var hudGO = Instantiate(_playerHUDPrefab, _hudContainer);
            hudGO.name = $"HUD_{player.name}";

            var hud = hudGO.GetComponent<PlayerHUD>();
            if (hud == null)
            {
                Debug.LogError("[HUDManager] PlayerHUD Prefab 缺少 PlayerHUD 组件");
                return null;
            }

            hud.Init(player);
            _huds[_hudCount] = hud;
            _hudCount++;

            Debug.Log($"[HUDManager] 注册玩家 HUD: {player.name} (#{_hudCount})");
            return hud;
        }

        /// <summary>
        /// 移除指定玩家的 HUD
        /// </summary>
        public void UnregisterPlayer(GameObject player)
        {
            for (int i = 0; i < _hudCount; i++)
            {
                if (_huds[i] != null && _huds[i].gameObject.name == $"HUD_{player.name}")
                {
                    Destroy(_huds[i].gameObject);
                    // 后面的往前挪
                    for (int j = i; j < _hudCount - 1; j++)
                    {
                        _huds[j] = _huds[j + 1];
                    }
                    _huds[_hudCount - 1] = null;
                    _hudCount--;
                    break;
                }
            }
        }

        #endregion
    }
}
