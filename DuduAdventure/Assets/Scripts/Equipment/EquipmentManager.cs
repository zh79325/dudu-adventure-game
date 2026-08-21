using System;
using System.Collections.Generic;
using UnityEngine;
using DuduAdventure.Stats;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 装备管理器 —— 挂在玩家身上，管理穿戴/卸载/背包
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 管理 6 个装备槽位的当前装备
    /// - 穿装备时把词条注入 CharacterStats，脱时移除
    /// - 提供背包（简易列表）
    /// - 提供拾取接口（掉落物调用）
    /// 
    /// MVP 阶段简化：
    /// - 背包无上限（不做格子系统）
    /// - 穿装备自动替换旧的（旧的回背包）
    /// - 不做 UI 绑定（UI 阶段再加）
    /// </remarks>
    [RequireComponent(typeof(CharacterStats))]
    public class EquipmentManager : MonoBehaviour
    {
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
        /// 背包内容变化时触发（拾取/穿戴/卸下都会触发）
        /// </summary>
        public event Action OnInventoryChanged;

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
        /// 拾取并立即穿上（掉落物的便捷接口）
        /// </summary>
        public void PickUpAndEquip(EquipmentInstance item)
        {
            if (item == null) return;

            // 先加入背包再穿（Equip 内部会从背包移除）
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
        /// 丢弃背包中的一件装备
        /// </summary>
        public void DiscardFromInventory(EquipmentInstance item)
        {
            if (item == null) return;
            if (_inventory.Remove(item))
            {
                OnInventoryChanged?.Invoke();
                Debug.Log($"[EquipmentManager] 丢弃: {item.DisplayName}");
            }
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        #endregion
    }
}
