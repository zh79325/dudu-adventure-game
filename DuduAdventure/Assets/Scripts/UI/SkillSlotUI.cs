using UnityEngine;
using UnityEngine.UI;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 技能槽 UI - 显示技能图标、冷却遮罩和按键提示
    /// </summary>
    public class SkillSlotUI : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMPro.TextMeshProUGUI _keyText;
        [SerializeField] private GameObject _lockedOverlay;

        #region 公共方法

        /// <summary>
        /// 设置技能图标
        /// </summary>
        public void SetIcon(Sprite icon)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
        }

        /// <summary>
        /// 设置冷却进度（0=可用，1=刚释放等冷却）
        /// </summary>
        public void SetCooldown(float percent)
        {
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = Mathf.Clamp01(percent);
                _cooldownOverlay.enabled = percent > 0.01f;
            }
        }

        /// <summary>
        /// 设置按键提示文字
        /// </summary>
        public void SetKeyHint(string key)
        {
            if (_keyText != null)
                _keyText.text = key;
        }

        /// <summary>
        /// 设置是否锁定（未解锁状态）
        /// </summary>
        public void SetLocked(bool locked)
        {
            if (_lockedOverlay != null)
                _lockedOverlay.SetActive(locked);
        }

        #endregion
    }
}
