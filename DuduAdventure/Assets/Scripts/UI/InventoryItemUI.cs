using System;
using UnityEngine;
using UnityEngine.UI;
using DuduAdventure.Equipment;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 背包物品条目 UI - 显示背包中的一件装备
    /// </summary>
    public class InventoryItemUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private TMPro.TextMeshProUGUI _nameText;
        [SerializeField] private TMPro.TextMeshProUGUI _slotText;
        [SerializeField] private Button _button;

        private EquipmentInstance _item;

        /// <summary>
        /// 点击回调
        /// </summary>
        public Action OnClicked { get; set; }

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => OnClicked?.Invoke());
        }

        /// <summary>
        /// 设置此条目显示的装备
        /// </summary>
        public void SetItem(EquipmentInstance item)
        {
            _item = item;
            if (item == null) return;

            if (_nameText != null)
            {
                _nameText.text = item.DisplayName;
                _nameText.color = GetRarityColor(item.Rarity);
            }

            if (_slotText != null)
            {
                _slotText.text = item.Slot.ToString();
            }

            if (_iconImage != null)
            {
                if (item.Template.Icon != null)
                {
                    _iconImage.sprite = item.Template.Icon;
                    _iconImage.color = Color.white;
                }
                else
                {
                    _iconImage.color = GetRarityColor(item.Rarity);
                }
                _iconImage.enabled = true;
            }

            if (_rarityBorder != null)
            {
                _rarityBorder.color = GetRarityColor(item.Rarity);
            }
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
    }
}
