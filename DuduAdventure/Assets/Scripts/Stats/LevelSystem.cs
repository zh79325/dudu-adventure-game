using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Stats
{
    /// <summary>
    /// 等级系统 - 管理经验获取、升级、属性成长和技能解锁
    /// </summary>
    /// <remarks>
    /// 升级收益：
    /// - 每级自动获得基础属性成长（攻击+2, 防御+1, 血量+15 等）
    /// - 特定等级解锁新技能（通过 SkillUnlockLevel 配置）
    /// 
    /// 经验曲线：
    /// - 使用公式: requiredExp = baseExp * level^exponent
    /// - 可调参数，让前期升级快、后期变慢
    /// </remarks>
    [RequireComponent(typeof(CharacterStats))]
    public class LevelSystem : MonoBehaviour
    {
        #region Inspector 配置

        [Header("等级设置")]
        [Tooltip("初始等级")]
        [SerializeField] private int _startLevel = 1;

        [Tooltip("最大等级")]
        [SerializeField] private int _maxLevel = 30;

        [Header("经验曲线")]
        [Tooltip("1 级升 2 级所需经验")]
        [SerializeField] private int _baseExpToLevel = 100;

        [Tooltip("经验增长指数（越大后期升级越慢）")]
        [SerializeField] private float _expExponent = 1.5f;

        [Header("每级属性成长")]
        [Tooltip("每级攻击力增长")]
        [SerializeField] private float _attackPerLevel = 2f;

        [Tooltip("每级防御力增长")]
        [SerializeField] private float _defensePerLevel = 1f;

        [Tooltip("每级最大生命增长")]
        [SerializeField] private float _maxHPPerLevel = 15f;

        [Tooltip("每级最大蓝量增长")]
        [SerializeField] private float _maxManaPerLevel = 8f;

        [Header("技能解锁等级")]
        [Tooltip("各技能的解锁等级配置")]
        [SerializeField] private SkillUnlockEntry[] _skillUnlocks;

        #endregion

        #region 事件

        /// <summary>
        /// 升级时触发
        /// 参数：新等级
        /// </summary>
        public event Action<int> OnLevelUp;

        /// <summary>
        /// 经验变化时触发
        /// 参数：当前经验, 升级所需经验
        /// </summary>
        public event Action<int, int> OnExpChanged;

        /// <summary>
        /// 技能解锁时触发
        /// 参数：技能 ID
        /// </summary>
        public event Action<string> OnSkillUnlocked;

        #endregion

        #region 运行时状态

        private int _currentLevel;
        private int _currentExp;
        private CharacterStats _stats;
        private Combat.HealthComponent _health;

        // 已解锁的技能 ID 列表
        private readonly HashSet<string> _unlockedSkills = new();

        #endregion

        #region 公共属性

        public int CurrentLevel => _currentLevel;
        public int MaxLevel => _maxLevel;
        public int CurrentExp => _currentExp;
        public int ExpToNextLevel => GetRequiredExp(_currentLevel);
        public float ExpPercent => ExpToNextLevel > 0 ? (float)_currentExp / ExpToNextLevel : 1f;
        public bool IsMaxLevel => _currentLevel >= _maxLevel;
        public IReadOnlyCollection<string> UnlockedSkills => _unlockedSkills;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _health = GetComponent<Combat.HealthComponent>();
        }

        private void Start()
        {
            _currentLevel = _startLevel;
            _currentExp = 0;

            // 检查初始等级已解锁的技能
            CheckSkillUnlocks();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获得经验值（击杀敌人、完成任务等）
        /// </summary>
        public void AddExp(int amount)
        {
            if (amount <= 0) return;
            if (IsMaxLevel) return;

            _currentExp += amount;

            Debug.Log($"[LevelSystem] {gameObject.name} 获得 {amount} 经验，" +
                      $"当前 {_currentExp}/{ExpToNextLevel}");

            // 检查是否升级（可能连升多级）
            while (_currentExp >= ExpToNextLevel && !IsMaxLevel)
            {
                _currentExp -= ExpToNextLevel;
                LevelUp();
            }

            // 已满级时多余经验清零
            if (IsMaxLevel)
            {
                _currentExp = 0;
            }

            OnExpChanged?.Invoke(_currentExp, ExpToNextLevel);
        }

        /// <summary>
        /// 查询某技能是否已解锁
        /// </summary>
        public bool IsSkillUnlocked(string skillId)
        {
            return _unlockedSkills.Contains(skillId);
        }

        /// <summary>
        /// 获取指定等级升级所需经验
        /// </summary>
        public int GetRequiredExp(int level)
        {
            return Mathf.RoundToInt(_baseExpToLevel * Mathf.Pow(level, _expExponent));
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 执行升级逻辑
        /// </summary>
        private void LevelUp()
        {
            _currentLevel++;

            Debug.Log($"[LevelSystem] {gameObject.name} 升级！当前 Lv.{_currentLevel}");

            // 应用属性成长
            ApplyLevelUpStats();

            // 检查技能解锁
            CheckSkillUnlocks();

            // 升级回满血蓝
            RefillOnLevelUp();

            OnLevelUp?.Invoke(_currentLevel);
        }

        /// <summary>
        /// 应用每级属性成长到 CharacterStats
        /// </summary>
        private void ApplyLevelUpStats()
        {
            if (_stats == null) return;

            // 使用等级作为修改器来源 ID，每次升级更新总成长量
            string sourceId = "LevelGrowth";
            int growthLevels = _currentLevel - _startLevel;

            var modifiers = new List<StatModifier>();

            if (_attackPerLevel > 0f)
                modifiers.Add(new StatModifier(StatType.Attack, _attackPerLevel * growthLevels, 0f));
            if (_defensePerLevel > 0f)
                modifiers.Add(new StatModifier(StatType.Defense, _defensePerLevel * growthLevels, 0f));
            if (_maxHPPerLevel > 0f)
                modifiers.Add(new StatModifier(StatType.MaxHP, _maxHPPerLevel * growthLevels, 0f));

            // 先移除旧的再加新的（覆盖式更新）
            _stats.RemoveModifiers(sourceId);
            _stats.AddModifiers(sourceId, modifiers);
        }

        /// <summary>
        /// 升级时回满血蓝
        /// </summary>
        private void RefillOnLevelUp()
        {
            // 回满血
            if (_health != null)
            {
                _health.SetMaxHP(Mathf.RoundToInt(_stats.GetStat(StatType.MaxHP)));
                _health.FullHeal();
            }

            // 回满蓝
            var resources = GetComponents<ResourceComponent>();
            foreach (var res in resources)
            {
                if (res.Type == ResourceType.Mana)
                {
                    res.SetMaxValue(res.MaxValue + _maxManaPerLevel);
                    res.Fill();
                }
            }
        }

        /// <summary>
        /// 检查当前等级是否解锁了新技能
        /// </summary>
        private void CheckSkillUnlocks()
        {
            if (_skillUnlocks == null) return;

            foreach (var entry in _skillUnlocks)
            {
                if (entry.UnlockLevel <= _currentLevel && !_unlockedSkills.Contains(entry.SkillId))
                {
                    _unlockedSkills.Add(entry.SkillId);
                    OnSkillUnlocked?.Invoke(entry.SkillId);

                    Debug.Log($"[LevelSystem] 解锁技能: {entry.SkillId} (Lv.{entry.UnlockLevel})");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 技能解锁配置条目
    /// </summary>
    [Serializable]
    public struct SkillUnlockEntry
    {
        [Tooltip("技能标识 ID")]
        public string SkillId;

        [Tooltip("解锁所需等级")]
        public int UnlockLevel;

        [Tooltip("技能显示名称")]
        public string DisplayName;
    }
}
