using System.Collections.Generic;
using UnityEngine;
using DuduAdventure.Player;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 地面掉落物 - 玩家站上去按攻击键手动拾取（放入背包）
    /// </summary>
    /// <remarks>
    /// 拾取流程：
    /// 1. 玩家进入 Trigger 范围 → 显示拾取提示（将来加 UI）
    /// 2. 玩家按攻击键 → 调用 EquipmentManager.PickUp() 放入背包
    /// 3. 玩家之后通过背包 UI 选择穿哪件
    /// 
    /// 多人支持：同时只有一个玩家能拾取同一个掉落物。
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class DropPickup : MonoBehaviour
    {
        #region Inspector 配置

        [Header("拾取设置")]
        [Tooltip("可拾取的玩家图层")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("掉落动画")]
        [Tooltip("弹起的高度")]
        [SerializeField] private float _bounceHeight = 1.5f;

        [Tooltip("弹起持续时间")]
        [SerializeField] private float _bounceDuration = 0.4f;

        [Tooltip("落地后闪烁提示持续时间")]
        [SerializeField] private float _glowDuration = 30f;

        #endregion

        #region 运行时状态

        // 持有的装备实例数据
        private EquipmentInstance _equipmentInstance;

        // 动画相关
        private Vector3 _startPos;
        private float _bounceTimer;
        private bool _hasLanded;
        private bool _isPickedUp;

        // 渲染
        private SpriteRenderer _spriteRenderer;
        private float _glowTimer;

        // 范围内的玩家列表
        private readonly List<GameObject> _playersInRange = new();

        #endregion

        #region 公共属性

        /// <summary>
        /// 持有的装备数据（UI 悬浮提示用）
        /// </summary>
        public EquipmentInstance Equipment => _equipmentInstance;

        /// <summary>
        /// 是否已落地（落地前不可拾取）
        /// </summary>
        public bool HasLanded => _hasLanded;

        #endregion

        #region 初始化

        /// <summary>
        /// 由 LootDropper 调用，注入装备数据
        /// </summary>
        public void Init(EquipmentInstance equipment)
        {
            _equipmentInstance = equipment;

            // 根据稀有度设置颜色
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = GetRarityColor(equipment.Template.Rarity);
            }
        }

        #endregion

        #region 生命周期

        private void Start()
        {
            _startPos = transform.position;
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            if (_isPickedUp) return;

            // 弹起动画
            if (!_hasLanded)
            {
                _bounceTimer += Time.deltaTime;
                float t = _bounceTimer / _bounceDuration;

                if (t >= 1f)
                {
                    transform.position = _startPos;
                    _hasLanded = true;
                }
                else
                {
                    float yOffset = 4f * _bounceHeight * t * (1f - t);
                    transform.position = _startPos + Vector3.up * yOffset;
                }
            }
            else
            {
                // 落地后闪烁
                _glowTimer += Time.deltaTime;
                if (_spriteRenderer != null)
                {
                    float alpha = 0.7f + 0.3f * Mathf.Sin(_glowTimer * 4f);
                    Color c = _spriteRenderer.color;
                    c.a = alpha;
                    _spriteRenderer.color = c;
                }

                // 检测范围内玩家的输入
                CheckPickupInput();

                // 超时消失
                if (_glowTimer > _glowDuration)
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 碰撞检测（进出范围）

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isPickedUp) return;
            if (!IsPlayerLayer(other.gameObject)) return;

            if (!_playersInRange.Contains(other.gameObject))
            {
                _playersInRange.Add(other.gameObject);
            }

            // TODO: 显示拾取提示 UI（"按 J 拾取"）
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            _playersInRange.Remove(other.gameObject);

            // TODO: 隐藏拾取提示 UI
        }

        #endregion

        #region 拾取逻辑

        /// <summary>
        /// 检测范围内玩家是否按了攻击键
        /// </summary>
        private void CheckPickupInput()
        {
            if (_equipmentInstance == null) return;

            // 遍历范围内的玩家，检查谁按了攻击键
            for (int i = _playersInRange.Count - 1; i >= 0; i--)
            {
                var playerGO = _playersInRange[i];
                if (playerGO == null)
                {
                    _playersInRange.RemoveAt(i);
                    continue;
                }

                // 获取输入源
                var inputSource = playerGO.GetComponent<IPlayerInputSource>();
                if (inputSource == null) continue;

                // 按攻击键拾取
                if (inputSource.AttackPressed)
                {
                    TryPickUp(playerGO);
                    return;
                }
            }
        }

        /// <summary>
        /// 尝试让指定玩家拾取
        /// </summary>
        private void TryPickUp(GameObject playerGO)
        {
            var equipMgr = playerGO.GetComponent<EquipmentManager>();
            if (equipMgr == null)
                equipMgr = playerGO.GetComponentInParent<EquipmentManager>();

            if (equipMgr == null) return;

            // 放入背包（不自动穿戴）
            equipMgr.PickUp(_equipmentInstance);
            _isPickedUp = true;

            Debug.Log($"[DropPickup] {playerGO.name} 拾取了 " +
                      $"[{_equipmentInstance.Template.Rarity}] {_equipmentInstance.Template.DisplayName}" +
                      $" → 放入背包");

            // TODO: 播放拾取音效
            // TODO: 显示"获得装备"飘字

            Destroy(gameObject);
        }

        #endregion

        #region 辅助

        private bool IsPlayerLayer(GameObject go)
        {
            return (_playerLayer.value & (1 << go.layer)) != 0;
        }

        private Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:    return Color.white;
                case Rarity.Uncommon:  return Color.green;
                case Rarity.Rare:      return new Color(0.2f, 0.5f, 1f);
                case Rarity.Epic:      return new Color(0.6f, 0.2f, 0.9f);
                case Rarity.Legendary: return new Color(1f, 0.6f, 0f);
                default:               return Color.white;
            }
        }

        #endregion
    }
}
