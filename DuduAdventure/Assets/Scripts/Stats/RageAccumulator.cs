using UnityEngine;
using DuduAdventure.Combat;

namespace DuduAdventure.Stats
{
    /// <summary>
    /// 怒气积攒器 - 监听战斗事件来积攒怒气
    /// </summary>
    /// <remarks>
    /// 积攒机制：
    /// - 攻击命中敌人：+攻击怒气值（连击越多积攒越快）
    /// - 被敌人攻击：+受击怒气值（受伤越重积攒越多）
    /// - 怒气满后可以释放大绝招
    /// - 释放大绝招后清空怒气
    /// 
    /// 使用方式：
    /// 挂在玩家身上，同时需要同对象有 ResourceComponent(Rage) 和 HealthComponent
    /// </remarks>
    [RequireComponent(typeof(HealthComponent))]
    public class RageAccumulator : MonoBehaviour
    {
        #region Inspector 配置

        [Header("攻击积攒")]
        [Tooltip("每次攻击命中获得的基础怒气")]
        [SerializeField] private float _ragePerHit = 5f;

        [Tooltip("连击加成系数（第 N 击额外加 N*此值 的怒气）")]
        [SerializeField] private float _comboRageBonus = 2f;

        [Header("受击积攒")]
        [Tooltip("每受到 1 点伤害获得的怒气")]
        [SerializeField] private float _ragePerDamageTaken = 0.3f;

        [Tooltip("受击时的最低怒气保底值")]
        [SerializeField] private float _minRageOnHit = 3f;

        #endregion

        #region 组件引用

        private ResourceComponent _rageResource;
        private HealthComponent _health;
        private Player.PlayerCombat _playerCombat;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _playerCombat = GetComponent<Player.PlayerCombat>();

            // 找到 Rage 类型的 ResourceComponent
            var resources = GetComponents<ResourceComponent>();
            foreach (var res in resources)
            {
                if (res.Type == ResourceType.Rage)
                {
                    _rageResource = res;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            // 监听受击
            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
            }

            // 监听攻击命中
            if (_playerCombat != null)
            {
                _playerCombat.OnAttackHit += HandleAttackHit;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnAttackHit -= HandleAttackHit;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 攻击命中敌人时积攒怒气
        /// </summary>
        /// <param name="comboCount">当前连击数</param>
        private void HandleAttackHit(int comboCount)
        {
            if (_rageResource == null) return;

            float rage = _ragePerHit + comboCount * _comboRageBonus;
            _rageResource.Add(rage);

            Debug.Log($"[RageAccumulator] 攻击命中积攒怒气 +{rage:F1}，" +
                      $"当前 {_rageResource.CurrentValue:F0}/{_rageResource.MaxValue:F0}");
        }

        /// <summary>
        /// 被攻击时积攒怒气
        /// </summary>
        private void HandleDamaged(int damage, int currentHP, int maxHP, Vector2 knockback)
        {
            if (_rageResource == null) return;

            float rage = Mathf.Max(_minRageOnHit, damage * _ragePerDamageTaken);
            _rageResource.Add(rage);

            Debug.Log($"[RageAccumulator] 受击积攒怒气 +{rage:F1}，" +
                      $"当前 {_rageResource.CurrentValue:F0}/{_rageResource.MaxValue:F0}");
        }

        #endregion
    }
}
