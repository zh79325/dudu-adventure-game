using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 帧精灵动画器 —— 根据状态切换对应精灵帧。
    /// 与 ProceduralSpriteAnimator 配合使用（后者负责 scale 动画）。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameSpriteAnimator : MonoBehaviour
    {
        [Header("精灵帧")]
        [Tooltip("待机帧")]
        [SerializeField] private Sprite _idleSprite;

        [Tooltip("行走帧 1")]
        [SerializeField] private Sprite _walkFrame1;

        [Tooltip("行走帧 2")]
        [SerializeField] private Sprite _walkFrame2;

        [Tooltip("攻击帧")]
        [SerializeField] private Sprite _attackSprite;

        [Header("动画设置")]
        [Tooltip("每秒切换帧数（行走时）")]
        [SerializeField] private float _walkFPS = 6f;

        private SpriteRenderer _sr;
        private PlayerStateMachine _stateMachine;
        private float _walkTimer;
        private int _currentFrame;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _stateMachine = GetComponent<PlayerStateMachine>();
        }

        private void Update()
        {
            if (_stateMachine == null || _sr == null) return;

            var state = _stateMachine.CurrentState;

            switch (state)
            {
                case PlayerState.Run:
                    if (_walkFrame1 != null && _walkFrame2 != null)
                    {
                        _walkTimer += Time.deltaTime * _walkFPS;
                        if (_walkTimer >= 1f)
                        {
                            _walkTimer -= 1f;
                            _currentFrame = 1 - _currentFrame;
                        }
                        _sr.sprite = _currentFrame == 0 ? _walkFrame1 : _walkFrame2;
                    }
                    break;

                case PlayerState.Attack:
                    if (_attackSprite != null)
                    {
                        _sr.sprite = _attackSprite;
                    }
                    _walkTimer = 0f;
                    _currentFrame = 0;
                    break;

                case PlayerState.Jump:
                    // 跳跃时用 Walk1 帧（双腿张开的姿势更像跳跃）
                    if (_walkFrame1 != null)
                    {
                        _sr.sprite = _walkFrame1;
                    }
                    _walkTimer = 0f;
                    _currentFrame = 0;
                    break;

                default:
                    if (_idleSprite != null)
                    {
                        _sr.sprite = _idleSprite;
                    }
                    _walkTimer = 0f;
                    _currentFrame = 0;
                    break;
            }
        }
    }
}
