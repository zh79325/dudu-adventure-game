using System;
using System.Collections.Generic;
using UnityEngine;
using DuduAdventure.Stats;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 装备管理器 —— 挂在玩家身上，管理穿戴/卸载/背包/丢弃/粉碎
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class EquipmentManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("掉落设置")]
        [Tooltip("丢弃时生成的 DropPickup Prefab")]
        [SerializeField] private GameObject _dropPickupPrefab;

        [Tooltip("丢弃物生成的前方偏移距离")]
        [SerializeField] private float _dropForwardOffset = 1.5f;

        #endregion

        #region 事件

        /// <summary>
        /// 装备变化时触发（UI 监听用）
        /// </summary>
        public event Action<EquipmentSlot> OnEquipmentChanged;

        /// <summary>
        /// 拾取新装备时触发
        /// </summary>
        public event Action<EquipmentInstance> OnItemPickedUp;

        /// <summary>
        /// 背包内容变化时触发（拾取/穿戴/卸下/丢弃/粉碎都会触发）
        /// </summary>
        public event Action OnInventoryChanged;

        /// <summary>
        /// 丢弃装备到地面时触发
        /// 参数：被丢弃的装备
        /// </summary>
        public event Action<EquipmentInstance> OnItemDropped;

        /// <summary>
        /// 粉碎装备时触发
        /// 参数：被粉碎的装备
        /// </summary>
        public event Action<EquipmentInstance> OnItemSalvaged;

        #endregion

        #region 运行时状态

        private CharacterStats _stats;

        // 当前穿戴的装备，key = 槽位
        private readonly Dictionary<EquipmentSlot, EquipmentInstance> _equipped = new();

        // 背包（简易列表）
        private readonly List<EquipmentInstance> _inventory = new();

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取某槽位当前穿戴的装备（可能为 null）
        /// </summary>
        public EquipmentInstance GetEquipped(EquipmentSlot slot)
        {
            return _equipped.TryGetValue(slot, out var item) ? item : null;
        }

        /// <summary>
        /// 获取背包列表（只读）
        /// </summary>
        public IReadOnlyList<EquipmentInstance> Inventory => _inventory;

        /// <summary>
        /// 拾取一件装备（加入背包）
        /// </summary>
        public void PickUp(EquipmentInstance item)
        {
            if (item == null) return;

            _inventory.Add(item);
            OnItemPickedUp?.Invoke(item);
            OnInventoryChanged?.Invoke();

            Debug.Log($"[EquipmentManager] 拾取: {item.DisplayName} ({item.Rarity})");
        }

        /// <summary>
        /// 从背包穿上一件装备
        /// </summary>
        public void Equip(EquipmentInstance item)
        {
            if (item == null) return;

            var slot = item.Slot;

            // 如果该槽位已有装备，先卸下
            if (_equipped.TryGetValue(slot, out var oldItem))
            {
                Unequip(slot);
            }

            // 从背包移除（如果在背包里的话）
            _inventory.Remove(item);

            // 穿上
            _equipped[slot] = item;
            _stats.AddModifiers(item.InstanceId, item.GetModifiers());

            OnEquipmentChanged?.Invoke(slot);
            OnInventoryChanged?.Invoke();
            Debug.Log($"[EquipmentManager] 装备: {item.DisplayName} → {slot}");
        }

        /// <summary>
        /// 卸下某槽位的装备（回到背包）
        /// </summary>
        public void Unequip(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item)) return;

            _stats.RemoveModifiers(item.InstanceId);
            _equipped.Remove(slot);
            _inventory.Add(item);

            OnEquipmentChanged?.Invoke(slot);
            OnInventoryChanged?.Invoke();
            Debug.Log($"[EquipmentManager] 卸下: {item.DisplayName}");
        }

        /// <summary>
        /// 拾取并立即穿上（便捷接口）
        /// </summary>
        public void PickUpAndEquip(EquipmentInstance item)
        {
            if (item == null) return;

            _inventory.Add(item);
            Equip(item);

            OnItemPickedUp?.Invoke(item);
        }

        /// <summary>
        /// 获取所有已穿戴装备（只读视图，UI 用）
        /// </summary>
        public IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> GetAllEquipped()
        {
            return _equipped;
        }

        /// <summary>
        /// 丢弃装备到地面 - 从背包移除并生成可拾取的地面掉落物
        /// 其他玩家可以走过去按攻击键捡起来
        /// </summary>
        /// <param name="item">要丢弃的装备（必须在背包中）</param>
        public void DropToWorld(EquipmentInstance item)
        {
            if (item == null) return;

            if (!_inventory.Remove(item))
            {
                Debug.LogWarning($"[EquipmentManager] 无法丢弃：{item.DisplayName} 不在背包中");
                return;
            }

            // 生成地面掉落物
            SpawnDropPickup(item);

            OnItemDropped?.Invoke(item);
            OnInventoryChanged?.Invoke();
            Debug.Log($"[EquipmentManager] 丢弃到地面: {item.DisplayName}");
        }

        /// <summary>
        /// 丢弃已穿戴的装备（先卸下再丢）
        /// </summary>
        public void DropEquippedToWorld(EquipmentSlot slot)
        {
            if (!_equipped.ContainsKey(slot)) return;

            Unequip(slot);
            // 卸下后物品已在背包末尾
            var item = _inventory[_inventory.Count - 1];
            DropToWorld(item);
        }

        /// <summary>
        /// 粉碎装备 - 永久销毁（将来可以返还材料/经验）
        /// </summary>
        /// <param name="item">要粉碎的装备（必须在背包中）</param>
        /// <returns>粉碎是否成功</returns>
        public bool Salvage(EquipmentInstance item)
        {
            if (item == null) return false;

            if (!_inventory.Remove(item))
            {
                Debug.LogWarning($"[EquipmentManager] 无法粉碎：{item.DisplayName} 不在背包中");
                return false;
            }

            // TODO: 返还材料/经验值
            // 例如：按稀有度给经验 —— LevelSystem.AddExp(rarityExpTable[item.Rarity])

            OnItemSalvaged?.Invoke(item);
            OnInventoryChanged?.Invoke();
            Debug.Log($"[EquipmentManager] 粉碎: {item.DisplayName} ({item.Rarity})");
            return true;
        }

        /// <summary>
        /// 粉碎已穿戴的装备（先卸下再粉碎）
        /// </summary>
        public bool SalvageEquipped(EquipmentSlot slot)
        {
            if (!_equipped.ContainsKey(slot)) return false;

            Unequip(slot);
            var item = _inventory[_inventory.Count - 1];
            return Salvage(item);
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 在角色前方生成一个可拾取的掉落物
        /// </summary>
        private void SpawnDropPickup(EquipmentInstance equipment)
        {
            // 计算生成位置（角色前方）
            Vector3 spawnPos = transform.position;
            var playerCtrl = GetComponent<Player.PlayerController>();
            float facing = playerCtrl != null ? playerCtrl.FacingDirection : 1f;
            spawnPos.x += facing * _dropForwardOffset;
            spawnPos.y += 0.5f;

            GameObject dropGO;

            if (_dropPickupPrefab != null)
            {
                dropGO = Instantiate(_dropPickupPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // 兜底：动态创建
                dropGO = new GameObject($"Drop_{equipment.DisplayName}");
                dropGO.transform.position = spawnPos;

                var sr = dropGO.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 100;
                var tex = new Texture2D(8, 8);
                var pixels = new Color[64];
                for (int i = 0; i < 64; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);

                var col = dropGO.AddComponent<CircleCollider2D>();
                col.radius = 0.8f;
                col.isTrigger = true;

                dropGO.AddComponent<DropPickup>();
            }

            // 注入装备数据
            var pickup = dropGO.GetComponent<DropPickup>();
            if (pickup != null)
            {
                pickup.Init(equipment);
            }
        }

        #endregion
    }
}
