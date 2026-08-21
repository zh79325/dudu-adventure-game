using System;
using UnityEngine;

namespace DuduAdventure.Stats
{
    /// <summary>
    /// 资源类型枚举
    /// </summary>
    public enum ResourceType
    {
        Mana,   // 蓝量 - 用于释放小技能
        Rage    // 怒气 - 用于释放大绝招
    }

    /// <summary>
    /// 通用资源组件 - 管理蓝量或怒气等可消耗/可积攒的资源
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 一个角色可以挂多个 ResourceComponent，分别管理不同资源
    /// - 通过 ResourceType 区分是蓝量还是怒气
    /// - 蓝量：有自然回复，技能消耗
    /// - 怒气：无自然回复（或缓慢衰减），攻击/受击积攒，满了可以放大招
    /// </remarks>
    public class ResourceComponent : MonoBehaviour
    {
        #region Inspector 配置

        [Header("资源类型")]
        [SerializeField] private ResourceType _resourceType = ResourceType.Mana;

        [Header("数值设置")]
        [Tooltip("最大值")]
        [SerializeField] private float _maxValue = 100f;

        [Tooltip("初始值（-1 表示满）")]
        [SerializeField] private float _initialValue = -1f;

        [Header("回复/衰减")]
        [Tooltip("每秒自然变化量（正=回复，负=衰减，0=不变）")]
        [SerializeField] private float _regenPerSecond = 0f;

        [Tooltip("回复延迟（消耗后多少秒才开始回复）")]
        [SerializeField] private float _regenDelay = 2f;

        #endregion

        #region 事件

        /// <summary>
        /// 资源值变化时触发
        /// 参数：当前值, 最大值, 变化量
        /// </summary>
        public event Action<float, float, float> OnValueChanged;

        /// <summary>
        /// 资源满时触发
        /// </summary>
        public event Action OnFull;

        /// <summary>
        /// 资源耗尽时触发
        /// </summary>
        public event Action OnDepleted;

        #endregion

        #region 运行时状态

        private float _currentValue;
        private float _regenDelayTimer;

        #endregion

        #region 公共属性

        public ResourceType Type => _resourceType;
        public float CurrentValue => _currentValue;
        public float MaxValue => _maxValue;
        public float Percent => _maxValue > 0f ? _currentValue / _maxValue : 0f;
        public bool IsFull => _currentValue >= _maxValue;
        public bool IsEmpty => _currentValue <= 0f;

        #endregion

        #region 生命周期

        private void Start()
        {
            _currentValue = _initialValue < 0 ? _maxValue : Mathf.Clamp(_initialValue, 0, _maxValue);
        }

        private void Update()
        {
            // 回复延迟倒计时
            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= Time.deltaTime;
                return;
            }

            // 自然回复/衰减
            if (_regenPerSecond != 0f && _currentValue < _maxValue && _currentValue > 0f)
            {
                Modify(_regenPerSecond * Time.deltaTime);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 消耗资源（技能释放时调用）
        /// </summary>
        /// <param name="amount">消耗量（正数）</param>
        /// <returns>是否有足够资源可消耗</returns>
        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (_currentValue < amount) return false;

            Modify(-amount);
            _regenDelayTimer = _regenDelay;
            return true;
        }

        /// <summary>
        /// 增加资源（攻击/受击积攒怒气，或拾取回蓝道具）
        /// </summary>
        public void Add(float amount)
        {
            if (amount <= 0f) return;
            Modify(amount);
        }

        /// <summary>
        /// 直接设置当前值
        /// </summary>
        public void SetValue(float value)
        {
            float old = _currentValue;
            _currentValue = Mathf.Clamp(value, 0f, _maxValue);
            float delta = _currentValue - old;

            if (Mathf.Abs(delta) > 0.001f)
            {
                OnValueChanged?.Invoke(_currentValue, _maxValue, delta);
            }
        }

        /// <summary>
        /// 修改最大值（升级或装备加成）
        /// </summary>
        public void SetMaxValue(float newMax, bool fillToMax = false)
        {
            _maxValue = Mathf.Max(1f, newMax);
            if (fillToMax)
            {
                _currentValue = _maxValue;
            }
            else
            {
                _currentValue = Mathf.Min(_currentValue, _maxValue);
            }
        }

        /// <summary>
        /// 回满
        /// </summary>
        public void Fill()
        {
            SetValue(_maxValue);
        }

        /// <summary>
        /// 清空
        /// </summary>
        public void Clear()
        {
            SetValue(0f);
        }

        #endregion

        #region 内部方法

        private void Modify(float delta)
        {
            float old = _currentValue;
            _currentValue = Mathf.Clamp(_currentValue + delta, 0f, _maxValue);
            float actualDelta = _currentValue - old;

            if (Mathf.Abs(actualDelta) > 0.001f)
            {
                OnValueChanged?.Invoke(_currentValue, _maxValue, actualDelta);
            }

            // 检查边界事件
            if (_currentValue >= _maxValue && old < _maxValue)
            {
                OnFull?.Invoke();
            }
            else if (_currentValue <= 0f && old > 0f)
            {
                OnDepleted?.Invoke();
            }
        }

        #endregion
    }
}
