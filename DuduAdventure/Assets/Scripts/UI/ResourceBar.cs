using UnityEngine;
using UnityEngine.UI;
using DuduAdventure.Stats;
using DuduAdventure.Combat;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 单条资源条 UI 组件 - 控制填充量和颜色
    /// </summary>
    /// <remarks>
    /// 挂在 UI 条的根物体上。
    /// 结构：本体 Image (背景) → Fill Image (填充)
    /// 通过 SetPercent() 控制填充比例
    /// </remarks>
    public class ResourceBar : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("填充部分的 Image（Filled 类型）")]
        [SerializeField] private Image _fillImage;

        [Tooltip("显示数值的 Text（可选）")]
        [SerializeField] private TMPro.TextMeshProUGUI _valueText;

        [Header("颜色")]
        [Tooltip("满时的颜色")]
        [SerializeField] private Color _fullColor = Color.white;

        [Tooltip("空时的颜色（渐变过渡）")]
        [SerializeField] private Color _emptyColor = Color.white;

        [Header("动画")]
        [Tooltip("数值变化的平滑速度（0=瞬间）")]
        [SerializeField] private float _smoothSpeed = 8f;

        private float _targetPercent = 1f;
        private float _displayPercent = 1f;

        public void SetPercent(float percent)
        {
            _targetPercent = Mathf.Clamp01(percent);
        }

        public void SetPercentImmediate(float percent)
        {
            _targetPercent = Mathf.Clamp01(percent);
            _displayPercent = _targetPercent;
            ApplyVisual();
        }

        public void SetText(string text)
        {
            if (_valueText != null)
                _valueText.text = text;
        }

        private void Update()
        {
            if (Mathf.Abs(_displayPercent - _targetPercent) > 0.001f)
            {
                if (_smoothSpeed <= 0f)
                {
                    _displayPercent = _targetPercent;
                }
                else
                {
                    _displayPercent = Mathf.Lerp(_displayPercent, _targetPercent, Time.deltaTime * _smoothSpeed);
                }
                ApplyVisual();
            }
        }

        private void ApplyVisual()
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = _displayPercent;
                _fillImage.color = Color.Lerp(_emptyColor, _fullColor, _displayPercent);
            }
        }
    }
}
