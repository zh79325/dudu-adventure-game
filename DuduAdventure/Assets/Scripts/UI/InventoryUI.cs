using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DuduAdventure.Equipment;
using DuduAdventure.Player;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 背包/装备 UI - 按键呼出，展示已穿戴装备和背包物品
    /// </summary>
    /// <remarks>
    /// 操作方式：
    /// - 按 Tab/I 键打开/关闭背包
    /// - 打开后显示：左侧=已穿戴的 6 个槽位，右侧=背包列表
    /// - 点击背包物品 → 穿戴（替换同槽位旧装备）
    /// - 点击已穿戴装备 → 卸下到背包
    /// 
    /// 此脚本管理 UI 逻辑，实际 UI 布局在 Canvas 中搭建。
    /// </remarks>
    public class InventoryUI : MonoBehaviour
    {
        #region Inspector 配置

        [Header("面板引用")]
        [Tooltip("背包面板根物体（开关用）")]
        [SerializeField] private GameObject _panelRoot;

        [Header("装备槽 UI")]
        [Tooltip("6 个装备槽位的 UI 组件（按 EquipmentSlot 枚举顺序）")]
        [SerializeField] private EquipSlotUI[] _equipSlots;

        [Header("背包列表")]
        [Tooltip("背包物品的容器（ScrollView 的 Content）")]
        [SerializeField] private RectTransform _inventoryContent;

        [Tooltip("背包物品条目 Prefab")]
        [SerializeField] private GameObject _inventoryItemPrefab;

        [Header("信息面板")]
        [Tooltip("装备详情文字")]
        [SerializeField] private TMPro.TextMeshProUGUI _detailText;

        [Header("按键设置")]
        [Tooltip("打开背包的按键")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.I;

        #endregion

        #region 运行时状态

        private EquipmentManager _equipmentManager;
        private bool _isOpen;
        private readonly List<GameObject> _spawnedItems = new();

        #endregion

        #region 公共属性

        public bool IsOpen => _isOpen;

        #endregion

        #region 生命周期

        private void Start()
        {
            // 默认关闭
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        private void Update()
        {
            // 按键切换
            if (UnityEngine.Input.GetKeyDown(_toggleKey))
            {
                Toggle();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 绑定目标玩家的 EquipmentManager
        /// </summary>
        public void Bind(EquipmentManager manager)
        {
            // 解除旧绑定
            if (_equipmentManager != null)
            {
                _equipmentManager.OnInventoryChanged -= Refresh;
                _equipmentManager.OnEquipmentChanged -= _ => Refresh();
            }

            _equipmentManager = manager;

            if (_equipmentManager != null)
            {
                _equipmentManager.OnInventoryChanged += Refresh;
                _equipmentManager.OnEquipmentChanged += _ => Refresh();
            }
        }

        /// <summary>
        /// 打开/关闭背包
        /// </summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _isOpen = true;
            _panelRoot.SetActive(true);
            Refresh();

            // TODO: 暂停游戏或降低游戏速度
        }

        public void Close()
        {
            if (_panelRoot == null) return;
            _isOpen = false;
            _panelRoot.SetActive(false);

            // TODO: 恢复游戏速度
        }

        #endregion

        #region 刷新 UI

        /// <summary>
        /// 刷新整个背包 UI
        /// </summary>
        private void Refresh()
        {
            if (!_isOpen) return;

            RefreshEquipSlots();
            RefreshInventoryList();
        }

        /// <summary>
        /// 刷新已穿戴的装备槽
        /// </summary>
        private void RefreshEquipSlots()
        {
            if (_equipSlots == null || _equipmentManager == null) return;

            var equipped = _equipmentManager.GetAllEquipped();

            for (int i = 0; i < _equipSlots.Length; i++)
            {
                if (_equipSlots[i] == null) continue;

                var slot = (EquipmentSlot)i;
                equipped.TryGetValue(slot, out var item);
                _equipSlots[i].SetItem(item, slot);
                _equipSlots[i].OnClicked = () => OnEquipSlotClicked(slot);
            }
        }

        /// <summary>
        /// 刷新背包物品列表
        /// </summary>
        private void RefreshInventoryList()
        {
            // 清除旧的
            foreach (var go in _spawnedItems)
            {
                if (go != null) Destroy(go);
            }
            _spawnedItems.Clear();

            if (_inventoryContent == null || _inventoryItemPrefab == null) return;
            if (_equipmentManager == null) return;

            var inventory = _equipmentManager.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                var item = inventory[i];
                var go = Instantiate(_inventoryItemPrefab, _inventoryContent);
                _spawnedItems.Add(go);

                var itemUI = go.GetComponent<InventoryItemUI>();
                if (itemUI != null)
                {
                    itemUI.SetItem(item);
                    var capturedItem = item;
                    itemUI.OnClicked = () => OnInventoryItemClicked(capturedItem);
                }
            }
        }

        #endregion

        #region 交互

        /// <summary>
        /// 点击背包中的物品 → 穿上
        /// </summary>
        private void OnInventoryItemClicked(EquipmentInstance item)
        {
            if (_equipmentManager == null) return;
            _equipmentManager.Equip(item);
            ShowDetail(item, "已装备");
        }

        /// <summary>
        /// 点击已穿戴的装备槽 → 卸下
        /// </summary>
        private void OnEquipSlotClicked(EquipmentSlot slot)
        {
            if (_equipmentManager == null) return;

            var item = _equipmentManager.GetEquipped(slot);
            if (item != null)
            {
                _equipmentManager.Unequip(slot);
                ShowDetail(item, "已卸下");
            }
        }

        /// <summary>
        /// 显示物品详情
        /// </summary>
        private void ShowDetail(EquipmentInstance item, string action = "")
        {
            if (_detailText == null || item == null) return;

            string rarityColor = GetRarityHexColor(item.Rarity);
            string text = $"<color={rarityColor}>[{item.Rarity}] {item.DisplayName}</color>\n";
            text += $"槽位: {item.Slot}\n\n";

            foreach (var affix in item.Affixes)
            {
                string sign = affix.Modifier.FlatBonus > 0 ? "+" : "";
                if (affix.Definition.IsPercent)
                {
                    text += $"  {affix.Definition.DisplayName}: {sign}{affix.Modifier.PercentBonus * 100f:F1}%\n";
                }
                else
                {
                    text += $"  {affix.Definition.DisplayName}: {sign}{affix.Modifier.FlatBonus:F0}\n";
                }
            }

            if (!string.IsNullOrEmpty(action))
            {
                text += $"\n<color=#888888>{action}</color>";
            }

            _detailText.text = text;
        }

        private string GetRarityHexColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:    return "#FFFFFF";
                case Rarity.Uncommon:  return "#00FF00";
                case Rarity.Rare:      return "#3388FF";
                case Rarity.Epic:      return "#9933FF";
                case Rarity.Legendary: return "#FF9900";
                default:               return "#FFFFFF";
            }
        }

        #endregion
    }
}
