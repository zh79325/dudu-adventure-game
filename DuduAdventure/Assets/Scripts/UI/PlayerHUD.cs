using UnityEngine;
using DuduAdventure.Stats;
using DuduAdventure.Combat;

namespace DuduAdventure.UI
{
    /// <summary>
    /// 玩家 HUD 面板 - 底部固定显示血量/蓝量/怒气/经验/等级
    /// </summary>
    /// <remarks>
    /// 设计：DNF 风格的底部面板。
    /// 每个玩家一个 PlayerHUD 实例，通过 Init() 绑定目标角色。
    /// 
    /// 层级结构（由 HUDManager 动态生成或手动摆放）：
    /// PlayerHUD_Root
    ///   ├─ HealthBar (ResourceBar)
    ///   ├─ ManaBar (ResourceBar)
    ///   ├─ RageBar (ResourceBar)
    ///   ├─ ExpBar (ResourceBar)
    ///   ├─ LevelText (TMP)
    ///   └─ SkillSlots (GridLayout)
    ///       ├─ SkillSlot_1
    ///       ├─ SkillSlot_2
    ///       └─ SkillSlot_Ultimate
    /// </remarks>
    public class PlayerHUD : MonoBehaviour
    {
        #region Inspector 配置

        [Header("资源条")]
        [SerializeField] private ResourceBar _healthBar;
        [SerializeField] private ResourceBar _manaBar;
        [SerializeField] private ResourceBar _rageBar;
        [SerializeField] private ResourceBar _expBar;

        [Header("文本")]
        [SerializeField] private TMPro.TextMeshProUGUI _levelText;
        [SerializeField] private TMPro.TextMeshProUGUI _nameText;

        [Header("技能槽")]
        [SerializeField] private SkillSlotUI[] _skillSlots;

        #endregion

        #region 运行时状态

        private HealthComponent _health;
        private ResourceComponent _mana;
        private ResourceComponent _rage;
        private LevelSystem _levelSystem;
        private bool _isInitialized;

        #endregion

        #region 初始化

        /// <summary>
        /// 绑定目标角色
        /// </summary>
        public void Init(GameObject target)
        {
            if (target == null) return;

            _health = target.GetComponent<HealthComponent>();
            _levelSystem = target.GetComponent<LevelSystem>();

            // 找到蓝量和怒气组件
            var resources = target.GetComponents<ResourceComponent>();
            foreach (var res in resources)
            {
                if (res.Type == ResourceType.Mana) _mana = res;
                else if (res.Type == ResourceType.Rage) _rage = res;
            }

            // 设置角色名
            if (_nameText != null)
            {
                _nameText.text = target.name;
            }

            // 订阅事件
            if (_health != null)
            {
                _health.OnDamaged += (_, _, _, _) => UpdateHealthBar();
                _health.OnHealed += (_, _, _) => UpdateHealthBar();
            }

            if (_mana != null)
            {
                _mana.OnValueChanged += (_, _, _) => UpdateManaBar();
            }

            if (_rage != null)
            {
                _rage.OnValueChanged += (_, _, _) => UpdateRageBar();
            }

            if (_levelSystem != null)
            {
                _levelSystem.OnExpChanged += (_, _) => UpdateExpBar();
                _levelSystem.OnLevelUp += _ => UpdateLevelText();
            }

            _isInitialized = true;

            // 初始刷新
            UpdateAll();
        }

        #endregion

        #region 更新

        private void Update()
        {
            // 轮询更新（兜底，确保 UI 不会卡在旧值）
            if (_isInitialized && Time.frameCount % 10 == 0)
            {
                UpdateAll();
            }
        }

        private void UpdateAll()
        {
            UpdateHealthBar();
            UpdateManaBar();
            UpdateRageBar();
            UpdateExpBar();
            UpdateLevelText();
        }

        private void UpdateHealthBar()
        {
            if (_healthBar == null || _health == null) return;
            _healthBar.SetPercent(_health.HPPercent);
            _healthBar.SetText($"{_health.CurrentHP}/{_health.MaxHP}");
        }

        private void UpdateManaBar()
        {
            if (_manaBar == null || _mana == null) return;
            _manaBar.SetPercent(_mana.Percent);
            _manaBar.SetText($"{Mathf.RoundToInt(_mana.CurrentValue)}/{Mathf.RoundToInt(_mana.MaxValue)}");
        }

        private void UpdateRageBar()
        {
            if (_rageBar == null || _rage == null) return;
            _rageBar.SetPercent(_rage.Percent);
            _rageBar.SetText($"{Mathf.RoundToInt(_rage.CurrentValue)}/{Mathf.RoundToInt(_rage.MaxValue)}");
        }

        private void UpdateExpBar()
        {
            if (_expBar == null || _levelSystem == null) return;
            _expBar.SetPercent(_levelSystem.ExpPercent);
            _expBar.SetText($"{_levelSystem.CurrentExp}/{_levelSystem.ExpToNextLevel}");
        }

        private void UpdateLevelText()
        {
            if (_levelText == null || _levelSystem == null) return;
            _levelText.text = $"Lv.{_levelSystem.CurrentLevel}";
        }

        #endregion
    }
}
