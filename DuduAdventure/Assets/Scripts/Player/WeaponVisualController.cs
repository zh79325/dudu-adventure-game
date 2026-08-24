using UnityEngine;
using DuduAdventure.Equipment;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 武器可视化控制器 —— 在角色身上显示当前装备的武器精灵，并播放攻击动画
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 监听 EquipmentManager.OnEquipmentChanged 切换武器精灵
    /// - 根据 PlayerStateMachine 状态播放武器动画：
    ///   * Idle: 武器斜持于身侧
    ///   * Run: 武器随步伐轻微晃动
    ///   * Attack: 武器做挥砍旋转动画
    ///   * Jump: 武器上扬
    /// </remarks>
    public class WeaponVisualController : MonoBehaviour
    {
        #region 配置

        [Header("武器挂载")]
        [Tooltip("武器精灵的本地偏移（相对角色中心）")]
        [SerializeField] private Vector2 _weaponOffset = new Vector2(0.3f, 0.2f);

        [Tooltip("武器精灵的默认旋转角（度）")]
        [SerializeField] private float _idleAngle = -30f;

        [Tooltip("武器精灵的排序层级（相对角色）")]
        [SerializeField] private int _sortingOrderOffset = 1;

        [Header("攻击动画")]
        [Tooltip("挥砍起始角度")]
        [SerializeField] private float _attackStartAngle = 60f;

        [Tooltip("挥砍结束角度")]
        [SerializeField] private float _attackEndAngle = -120f;

        [Tooltip("挥砍持续时间（秒）")]
        [SerializeField] private float _attackDuration = 0.35f;

        [Header("行走晃动")]
        [Tooltip("行走时武器晃动幅度（度）")]
        [SerializeField] private float _walkSwayAmplitude = 15f;

        [Tooltip("行走时武器晃动速度")]
        [SerializeField] private float _walkSwaySpeed = 8f;

        [Header("武器缩放")]
        [Tooltip("武器整体缩放倍数（让武器更醒目）")]
        [SerializeField] private float _weaponScale = 1.5f;

        [Header("默认武器（无装备时显示）")]
        [SerializeField] private Sprite _defaultWeaponSprite;

        #endregion

        #region 运行时

        private GameObject _weaponGO;
        private SpriteRenderer _weaponSR;
        private Transform _weaponTransform;

        private PlayerStateMachine _stateMachine;
        private EquipmentManager _equipManager;
        private SpriteRenderer _characterSR;

        private float _attackTimer;
        private bool _isAttacking;
        private float _walkTimer;
        private PlayerState _lastState;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            _equipManager = GetComponent<EquipmentManager>();
            _characterSR = GetComponentInChildren<SpriteRenderer>();

            CreateWeaponChild();
        }

        private void OnEnable()
        {
            if (_equipManager != null)
            {
                _equipManager.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        private void OnDisable()
        {
            if (_equipManager != null)
            {
                _equipManager.OnEquipmentChanged -= OnEquipmentChanged;
            }
        }

        private void Start()
        {
            // 初始化武器显示
            RefreshWeaponSprite();
        }

        private void LateUpdate()
        {
            if (_weaponTransform == null || _stateMachine == null) return;

            PlayerState state = _stateMachine.CurrentState;

            // 检测进入攻击状态
            if (state == PlayerState.Attack && _lastState != PlayerState.Attack)
            {
                StartAttackAnimation();
            }

            // 更新武器位置（跟随朝向翻转）
            UpdateWeaponPosition();

            // 更新武器旋转动画
            UpdateWeaponRotation(state);

            _lastState = state;
        }

        #endregion

        #region 创建武器子物体

        private void CreateWeaponChild()
        {
            _weaponGO = new GameObject("WeaponVisual");
            _weaponGO.transform.SetParent(transform);
            _weaponGO.transform.localPosition = new Vector3(_weaponOffset.x, _weaponOffset.y, 0f);
            _weaponGO.transform.localRotation = Quaternion.Euler(0f, 0f, _idleAngle);
            _weaponGO.transform.localScale = new Vector3(_weaponScale, _weaponScale, 1f);

            _weaponSR = _weaponGO.AddComponent<SpriteRenderer>();
            _weaponSR.sortingOrder = (_characterSR != null ? _characterSR.sortingOrder : 10) + _sortingOrderOffset;

            // 必须继承角色的 Sorting Layer 和材质：
            // 新建的 SpriteRenderer 默认落在 "Default" 层、并套上 URP 的 Sprite-Lit-Default，
            // 前者会让武器被地面盖掉，后者在无光照场景里会渲染成纯黑块。
            if (_characterSR != null)
            {
                _weaponSR.sortingLayerID = _characterSR.sortingLayerID;
                _weaponSR.sharedMaterial = _characterSR.sharedMaterial;
            }

            _weaponTransform = _weaponGO.transform;

            // 设置初始精灵
            if (_defaultWeaponSprite != null)
            {
                _weaponSR.sprite = _defaultWeaponSprite;
            }
        }

        #endregion

        #region 装备事件

        private void OnEquipmentChanged(EquipmentSlot slot)
        {
            if (slot == EquipmentSlot.Weapon)
            {
                RefreshWeaponSprite();
            }
        }

        private void RefreshWeaponSprite()
        {
            if (_equipManager == null || _weaponSR == null) return;

            var equipped = _equipManager.GetEquipped(EquipmentSlot.Weapon);
            if (equipped != null && equipped.Template != null && equipped.Template.WeaponSprite != null)
            {
                _weaponSR.sprite = equipped.Template.WeaponSprite;
            }
            else
            {
                // 没有装备武器时用默认精灵
                _weaponSR.sprite = _defaultWeaponSprite;
            }
        }

        #endregion

        #region 武器动画

        private void UpdateWeaponPosition()
        {
            if (_characterSR == null) return;

            // 根据角色朝向翻转武器位置
            bool facingLeft = _characterSR.flipX;
            float xOffset = facingLeft ? -Mathf.Abs(_weaponOffset.x) : Mathf.Abs(_weaponOffset.x);
            _weaponTransform.localPosition = new Vector3(xOffset, _weaponOffset.y, 0f);

            // 武器精灵也翻转
            _weaponSR.flipX = facingLeft;
        }

        private void UpdateWeaponRotation(PlayerState state)
        {
            float targetAngle;

            if (_isAttacking)
            {
                // 攻击动画：从起始角到结束角
                _attackTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_attackTimer / _attackDuration);

                // 使用缓出曲线让挥砍有爆发力
                float easedT = 1f - (1f - t) * (1f - t);
                targetAngle = Mathf.Lerp(_attackStartAngle, _attackEndAngle, easedT);

                if (t >= 1f)
                {
                    _isAttacking = false;
                }
            }
            else
            {
                switch (state)
                {
                    case PlayerState.Run:
                        _walkTimer += Time.deltaTime * _walkSwaySpeed;
                        targetAngle = _idleAngle + Mathf.Sin(_walkTimer) * _walkSwayAmplitude;
                        break;

                    case PlayerState.Jump:
                        targetAngle = _idleAngle + 20f; // 跳跃时武器上扬
                        break;

                    default:
                        targetAngle = _idleAngle;
                        break;
                }
            }

            // 根据朝向镜像角度
            bool facingLeft = _characterSR != null && _characterSR.flipX;
            if (facingLeft)
            {
                targetAngle = -targetAngle;
            }

            _weaponTransform.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        }

        private void StartAttackAnimation()
        {
            _isAttacking = true;
            _attackTimer = 0f;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 强制刷新武器显示（外部装备变更时调用）
        /// </summary>
        public void ForceRefresh()
        {
            RefreshWeaponSprite();
        }

        /// <summary>
        /// 设置默认武器精灵（供初始化使用）
        /// </summary>
        public void SetDefaultWeaponSprite(Sprite sprite)
        {
            _defaultWeaponSprite = sprite;
            if (_weaponSR != null && _weaponSR.sprite == null)
            {
                _weaponSR.sprite = sprite;
            }
        }

        #endregion
    }
}
