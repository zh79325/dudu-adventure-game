using System;
using UnityEngine;
using DuduAdventure.Equipment;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 武器姿势关键帧 —— 描述武器在某个时间点的位置和角度。
    /// </summary>
    /// <remarks>
    /// Offset：武器 pivot（握把端）相对角色中心的本地偏移，X 会根据角色朝向自动镜像。
    /// Rotation：武器 Z 轴旋转（度）。约定 0° = 棒身垂直向上，正值 = 逆时针（棒尖偏向角色背后），
    /// 负值 = 顺时针（棒尖偏向角色前方）。角色 flipX 时角度会自动取反以保持视觉一致。
    /// </remarks>
    [Serializable]
    public struct WeaponAttackPose
    {
        [Tooltip("握把端相对角色中心的本地偏移（面向右时）")]
        public Vector2 Offset;

        [Tooltip("武器 Z 轴旋转（度）。0° = 竖直向上，正值 = 逆时针")]
        public float Rotation;
    }

    /// <summary>
    /// 武器可视化控制器 —— 在角色身上显示当前装备的武器精灵，并按关键帧姿势播放攻击动画。
    /// </summary>
    /// <remarks>
    /// 职责：
    /// - 监听 EquipmentManager.OnEquipmentChanged 切换武器精灵
    /// - 根据 PlayerStateMachine 状态播放武器动画：
    ///   * Idle：武器停在 _idleOffset + _idleAngle
    ///   * Run：在 idle 姿势基础上加轻微晃动
    ///   * Attack：按 _attackPoses 关键帧姿势数组，用 PlayerStateMachine.AttackProgress 分段线性插值
    ///   * Jump：武器上扬
    ///
    /// 关键设计（修 "武器在手上转圈" 的 bug）：
    /// - 武器 sprite 的 pivot 必须设在握把端（Custom pivot，比如 0.5,0.08），
    ///   而不是中心。这样棒身绕手旋转，而不是绕棒身中点甩。
    /// - 攻击用 3~N 个关键帧姿势（Offset + Rotation），
    ///   而不是单条 startAngle→endAngle 的 lerp。分段插值让动作明显"分段"，
    ///   看起来像挥砍而不是风车式转动。
    /// </remarks>
    public class WeaponVisualController : MonoBehaviour
    {
        #region 配置

        [Header("武器挂载 - 空闲")]
        // v3 sprite (280×343, baseline y=331, PPU=175) 前手（viewer's right）大约在 pixel (200, 240)，
        // 对应角色本地 world (0.34, -0.39)。取 (0.30, -0.30) 让 pivot（棒身 grip 端）落在拳心稍上、略向前，
        // 既贴住手，又让棒身底部略微伸到手下方，避免看起来"漂浮"。
        [Tooltip("空闲时武器握把相对角色中心的偏移（面向右时）")]
        [SerializeField] private Vector2 _weaponOffset = new Vector2(0.30f, -0.30f);

        [Tooltip("空闲时武器的旋转角（度，0=竖直向上，正=逆时针）")]
        // 参考「英雄横挎金箍棒」的 chibi 立绘：棒身斜挎过身、棒尖翘到背后头肩之上。
        // 角色面朝右时背侧在 viewer's 左，正值 CCW 让棒尖偏左上；+45 让棒身接近对角斜挎。
        [SerializeField] private float _idleAngle = 45f;

        [Tooltip("武器精灵的排序层级（相对角色）")]
        [SerializeField] private int _sortingOrderOffset = 1;

        [Header("攻击关键帧")]
        [Tooltip("攻击时按进度分段线性插值的姿势关键帧。至少 2 帧，建议 3~4 帧对应帧序列的 Atk1/2/3。")]
        [SerializeField]
        private WeaponAttackPose[] _attackPoses = new WeaponAttackPose[]
        {
            // Pose 0 (t=0, 起势 - 匹配 Atk1 身体帧)：握把在右肩前，棒尖翘向后上方
            new WeaponAttackPose { Offset = new Vector2(0.15f, 0.55f), Rotation = 30f },
            // Pose 1 (t=0.5, 挥出接触 - 匹配 Atk2 身体帧)：握把前推到胸前，棒身横劈斜向前下
            new WeaponAttackPose { Offset = new Vector2(0.40f, 0.20f), Rotation = -75f },
            // Pose 2 (t=1, 收势 - 匹配 Atk3 身体帧)：握把继续前伸，棒尖砸至前下方
            new WeaponAttackPose { Offset = new Vector2(0.35f, -0.15f), Rotation = -140f },
        };

        [Header("行走晃动")]
        [Tooltip("行走时武器晃动幅度（度）")]
        [SerializeField] private float _walkSwayAmplitude = 10f;

        [Tooltip("行走时武器晃动速度")]
        [SerializeField] private float _walkSwaySpeed = 8f;

        [Header("武器缩放")]
        [Tooltip("武器整体缩放倍数（让武器更醒目）")]
        [SerializeField] private float _weaponScale = 1f;

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

        private float _walkTimer;

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
            RefreshWeaponSprite();
        }

        private void LateUpdate()
        {
            if (_weaponTransform == null || _stateMachine == null) return;

            bool facingLeft = _characterSR != null && _characterSR.flipX;
            PlayerState state = _stateMachine.CurrentState;

            Vector2 offset;
            float angle;

            switch (state)
            {
                case PlayerState.Attack:
                    SampleAttackPose(_stateMachine.AttackProgress, out offset, out angle);
                    break;

                case PlayerState.Run:
                    offset = _weaponOffset;
                    _walkTimer += Time.deltaTime * _walkSwaySpeed;
                    angle = _idleAngle + Mathf.Sin(_walkTimer) * _walkSwayAmplitude;
                    break;

                case PlayerState.Jump:
                    offset = _weaponOffset;
                    angle = _idleAngle + 20f; // 跳跃时武器上扬
                    break;

                default:
                    offset = _weaponOffset;
                    angle = _idleAngle;
                    _walkTimer = 0f;
                    break;
            }

            ApplyPose(offset, angle, facingLeft);

            if (_weaponSR != null) _weaponSR.flipX = facingLeft;
        }

        #endregion

        #region 关键帧采样

        /// <summary>
        /// 按 0~1 的进度在 _attackPoses 上做分段线性插值，输出当前的 offset 和 angle。
        /// </summary>
        private void SampleAttackPose(float progress, out Vector2 offset, out float angle)
        {
            if (_attackPoses == null || _attackPoses.Length == 0)
            {
                offset = _weaponOffset;
                angle = _idleAngle;
                return;
            }

            if (_attackPoses.Length == 1)
            {
                offset = _attackPoses[0].Offset;
                angle = _attackPoses[0].Rotation;
                return;
            }

            float t = Mathf.Clamp01(progress);
            float scaled = t * (_attackPoses.Length - 1);
            int i0 = Mathf.FloorToInt(scaled);
            int i1 = Mathf.Min(i0 + 1, _attackPoses.Length - 1);
            float f = scaled - i0;

            offset = Vector2.Lerp(_attackPoses[i0].Offset, _attackPoses[i1].Offset, f);
            angle = Mathf.Lerp(_attackPoses[i0].Rotation, _attackPoses[i1].Rotation, f);
        }

        /// <summary>
        /// 应用姿势到武器 Transform，按角色朝向自动镜像 X 偏移和旋转角。
        /// </summary>
        private void ApplyPose(Vector2 offset, float angle, bool facingLeft)
        {
            float x = facingLeft ? -offset.x : offset.x;
            _weaponTransform.localPosition = new Vector3(x, offset.y, 0f);

            // 面向左时角度取反：使武器在两个朝向下都朝向角色的"前方"
            float z = facingLeft ? -angle : angle;
            _weaponTransform.localRotation = Quaternion.Euler(0f, 0f, z);
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
                _weaponSR.sprite = _defaultWeaponSprite;
            }
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
