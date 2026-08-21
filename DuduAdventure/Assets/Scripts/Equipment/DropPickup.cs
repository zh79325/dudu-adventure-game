using UnityEngine;
using DuduAdventure.Stats;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 地面掉落物 - 玩家靠近后自动拾取并装备
    /// </summary>
    /// <remarks>
    /// 生成方式：LootDropper 在敌人死亡时 Instantiate 此 Prefab，
    /// 并通过 Init() 注入 EquipmentInstance 数据。
    /// 
    /// 拾取逻辑：
    /// - 使用 Trigger 碰撞检测玩家进入范围
    /// - 拾取后通知玩家的 EquipmentManager 装备
    /// - 播放简单的浮动动画（向上弹起再落下）
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
        [SerializeField] private float _glowDuration = 10f;

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

        #endregion

        #region 公共属性

        /// <summary>
        /// 持有的装备数据（UI 悬浮提示用）
        /// </summary>
        public EquipmentInstance Equipment => _equipmentInstance;

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
                    // 落地
                    transform.position = _startPos;
                    _hasLanded = true;
                }
                else
                {
                    // 抛物线：y = 4h*t*(1-t)
                    float yOffset = 4f * _bounceHeight * t * (1f - t);
                    transform.position = _startPos + Vector3.up * yOffset;
                }
            }
            else
            {
                // 落地后闪烁提示
                _glowTimer += Time.deltaTime;
                if (_spriteRenderer != null)
                {
                    float alpha = 0.7f + 0.3f * Mathf.Sin(_glowTimer * 4f);
                    Color c = _spriteRenderer.color;
                    c.a = alpha;
                    _spriteRenderer.color = c;
                }

                // 超时消失
                if (_glowTimer > _glowDuration)
                {
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region 拾取

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isPickedUp) return;
            if (_equipmentInstance == null) return;

            // 检查是否是玩家图层
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
                return;

            // 获取玩家的 EquipmentManager
            var equipMgr = other.GetComponent<EquipmentManager>();
            if (equipMgr == null)
                equipMgr = other.GetComponentInParent<EquipmentManager>();

            if (equipMgr == null) return;

            // 拾取并装备
            equipMgr.PickUpAndEquip(_equipmentInstance);
            _isPickedUp = true;

            Debug.Log($"[DropPickup] {other.name} 拾取了 [{_equipmentInstance.Template.Rarity}] " +
                      $"{_equipmentInstance.Template.DisplayName}");

            // TODO: 播放拾取音效
            // TODO: 显示装备获得 UI 提示

            Destroy(gameObject);
        }

        #endregion

        #region 辅助

        /// <summary>
        /// 根据稀有度返回对应颜色
        /// </summary>
        private Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:    return Color.white;
                case Rarity.Uncommon:  return Color.green;
                case Rarity.Rare:      return new Color(0.2f, 0.5f, 1f); // 蓝色
                case Rarity.Epic:      return new Color(0.6f, 0.2f, 0.9f); // 紫色
                case Rarity.Legendary: return new Color(1f, 0.6f, 0f); // 橙色
                default:               return Color.white;
            }
        }

        #endregion
    }
}
