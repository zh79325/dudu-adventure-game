using System;
using UnityEngine;
using UnityEngine.UI;
using DuduAdventure.Equipment;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 装备槽 UI 元素 - 显示已穿戴的某个槽位
    /// </summary>
    public class EquipSlotUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMPro.TextMeshProUGUI _slotLabel;
        [SerializeField] private GameObject _emptyHint;
        [SerializeField] private Button _button;

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
        /// 设置此槽位显示的装备（null = 空槽）
        /// </summary>
        public void SetItem(EquipmentInstance item, EquipmentSlot slot)
        {
            if (_slotLabel != null)
                _slotLabel.text = slot.ToString();

            if (item == null)
            {
                // 空槽
                if (_iconImage != null)
                {
                    _iconImage.enabled = false;
                }
                if (_emptyHint != null)
                    _emptyHint.SetActive(true);
                if (_backgroundImage != null)
                    _backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
            else
            {
                // 有装备
                if (_iconImage != null)
                {
                    _iconImage.enabled = true;
                    _iconImage.sprite = item.Template.Icon;
                    // 没有图标时用纯色代替
                    if (item.Template.Icon == null)
                    {
                        _iconImage.color = GetRarityColor(item.Rarity);
                    }
                    else
                    {
                        _iconImage.color = Color.white;
                    }
                }
                if (_emptyHint != null)
                    _emptyHint.SetActive(false);
                if (_backgroundImage != null)
                    _backgroundImage.color = GetRarityColor(item.Rarity) * 0.3f + new Color(0, 0, 0, 0.5f);
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
