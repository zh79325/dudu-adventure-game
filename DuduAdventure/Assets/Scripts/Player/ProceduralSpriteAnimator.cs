using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 程序化精灵动画 - 通过缩放/旋转/位移让单帧精灵也有动作感
    /// </summary>
    /// <remarks>
    /// 挂载到玩家 Prefab 上，自动读取 PlayerStateMachine 状态并施加视觉变形：
    /// - Idle: 呼吸式脉动
    /// - Run: 前倾 + 节奏弹跳
    /// - Jump: 起跳拉伸 / 落地压扁
    /// - Attack: 缩放冲击
    /// - Hit: 抖动
    /// </remarks>
    public class ProceduralSpriteAnimator : MonoBehaviour
    {
        #region 配置

        [Header("呼吸动画 (Idle)")]
        [SerializeField] private float _breathSpeed = 2f;
        [SerializeField] private float _breathAmplitude = 0.03f;

        [Header("跑步动画 (Run)")]
        [SerializeField] private float _runBobSpeed = 12f;
        [SerializeField] private float _runBobAmplitude = 0.06f;
        [SerializeField] private float _runLeanAngle = 8f;
        [SerializeField] private float _runSquashX = 0.95f;
        [SerializeField] private float _runStretchY = 1.05f;

        [Header("跳跃动画 (Jump)")]
        [SerializeField] private float _jumpStretchX = 0.85f;
        [SerializeField] private float _jumpStretchY = 1.2f;
        [SerializeField] private float _landSquashX = 1.25f;
        [SerializeField] private float _landSquashY = 0.75f;
        [SerializeField] private float _landSquashDuration = 0.15f;

        [Header("攻击动画 (Attack)")]
        [Tooltip("攻击瞬间的整体缩放冲击。形变主要由攻击美术帧承担，这里只保留轻微力量感")]
        [SerializeField] private float _attackPunchScale = 1.05f;
        [SerializeField] private float _attackPunchDuration = 0.12f;

        [Header("受击动画 (Hit)")]
        [SerializeField] private float _hitShakeIntensity = 0.1f;
        [SerializeField] private float _hitShakeDuration = 0.3f;
        [SerializeField] private float _hitShakeSpeed = 40f;

        #endregion

        #region 运行时状态

        private PlayerStateMachine _stateMachine;
        private PlayerController _controller;
        private Transform _visualTransform;
        private Vector3 _baseScale;

        private float _breathTimer;
        private float _runTimer;
        private float _landSquashTimer;
        private float _attackPunchTimer;
        private float _hitShakeTimer;

        private PlayerState _lastState;
        private bool _wasGrounded = true;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            _controller = GetComponent<PlayerController>();

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                _visualTransform = sr.transform;
            }
            else
            {
                _visualTransform = transform;
            }

            _baseScale = _visualTransform.localScale;
        }

        private void LateUpdate()
        {
            if (_stateMachine == null || _visualTransform == null) return;

            PlayerState currentState = _stateMachine.CurrentState;

            // 检测状态切换
            DetectStateTransitions(currentState);

            // 计算目标变形（只修改缩放，不动位置/旋转，避免与物理移动冲突）
            Vector3 scale = _baseScale;

            // 根据状态施加动画
            switch (currentState)
            {
                case PlayerState.Idle:
                    ApplyBreathing(ref scale);
                    break;

                case PlayerState.Run:
                    ApplyRunScale(ref scale);
                    break;

                case PlayerState.Jump:
                    ApplyJumpStretch(ref scale);
                    break;

                case PlayerState.Attack:
                    ApplyAttackPunch(ref scale);
                    break;

                case PlayerState.Hit:
                    ApplyHitScale(ref scale);
                    break;
            }

            // 叠加落地压扁（任何状态都可能触发）
            ApplyLandSquash(ref scale);

            // 只应用缩放
            _visualTransform.localScale = scale;

            _lastState = currentState;
        }

        #endregion

        #region 状态切换检测

        private void DetectStateTransitions(PlayerState currentState)
        {
            // 进入攻击状态：触发攻击冲击
            if (currentState == PlayerState.Attack && _lastState != PlayerState.Attack)
            {
                _attackPunchTimer = _attackPunchDuration;
            }

            // 进入受击状态：触发抖动
            if (currentState == PlayerState.Hit && _lastState != PlayerState.Hit)
            {
                _hitShakeTimer = _hitShakeDuration;
            }

            // 落地检测：从跳跃到地面
            if (_controller != null)
            {
                bool isGrounded = _controller.IsGrounded;
                if (isGrounded && !_wasGrounded)
                {
                    _landSquashTimer = _landSquashDuration;
                }
                _wasGrounded = isGrounded;
            }
        }

        #endregion

        #region 动画效果

        /// <summary>
        /// 呼吸脉动：Y轴微小缩放振荡
        /// </summary>
        private void ApplyBreathing(ref Vector3 scale)
        {
            _breathTimer += Time.deltaTime * _breathSpeed;
            float breath = Mathf.Sin(_breathTimer) * _breathAmplitude;
            scale.y = _baseScale.y * (1f + breath);
            scale.x = _baseScale.x * (1f - breath * 0.5f); // 反向微压缩保持体积
        }

        /// <summary>
        /// 跑步变形：交替压缩拉伸模拟步伐节奏
        /// </summary>
        private void ApplyRunScale(ref Vector3 scale)
        {
            _runTimer += Time.deltaTime * _runBobSpeed;

            // 交替压缩拉伸（模拟步伐弹跳）
            float cycle = Mathf.Sin(_runTimer);
            scale.x = _baseScale.x * (1f + cycle * 0.04f);
            scale.y = _baseScale.y * (1f - cycle * 0.04f);
        }

        /// <summary>
        /// 跳跃拉伸：纵向拉长、横向压窄
        /// </summary>
        private void ApplyJumpStretch(ref Vector3 scale)
        {
            if (_controller != null && !_controller.IsGrounded)
            {
                scale.x = _baseScale.x * _jumpStretchX;
                scale.y = _baseScale.y * _jumpStretchY;
            }
        }

        /// <summary>
        /// 落地压扁：短暂横向扩张、纵向压缩
        /// </summary>
        private void ApplyLandSquash(ref Vector3 scale)
        {
            if (_landSquashTimer > 0f)
            {
                _landSquashTimer -= Time.deltaTime;
                float t = _landSquashTimer / _landSquashDuration;
                float easedT = t * t; // 快速恢复

                scale.x = Mathf.Lerp(_baseScale.x, _baseScale.x * _landSquashX, easedT);
                scale.y = Mathf.Lerp(_baseScale.y, _baseScale.y * _landSquashY, easedT);
            }
        }

        /// <summary>
        /// 攻击冲击：快速放大后回弹
        /// </summary>
        private void ApplyAttackPunch(ref Vector3 scale)
        {
            if (_attackPunchTimer > 0f)
            {
                _attackPunchTimer -= Time.deltaTime;
                float t = _attackPunchTimer / _attackPunchDuration;
                // 先放大后回弹
                float punch = Mathf.Sin(t * Mathf.PI) * (_attackPunchScale - 1f);
                scale.x = _baseScale.x * (1f + punch);
                scale.y = _baseScale.y * (1f + punch);
            }
        }

        /// <summary>
        /// 受击变形：快速X轴缩放振荡
        /// </summary>
        private void ApplyHitScale(ref Vector3 scale)
        {
            if (_hitShakeTimer > 0f)
            {
                _hitShakeTimer -= Time.deltaTime;
                float t = _hitShakeTimer / _hitShakeDuration;
                float shake = Mathf.Sin(Time.time * _hitShakeSpeed) * 0.1f * t;
                scale.x = _baseScale.x * (1f + shake);
            }
        }

        #endregion
    }
}
