using UnityEngine;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 帧精灵动画器 —— 在 Run 状态时交替播放行走帧，其他状态显示 Idle 帧。
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

            if (state == PlayerState.Run && _walkFrame1 != null && _walkFrame2 != null)
            {
                // 行走动画：按 FPS 交替帧
                _walkTimer += Time.deltaTime * _walkFPS;
                if (_walkTimer >= 1f)
                {
                    _walkTimer -= 1f;
                    _currentFrame = 1 - _currentFrame; // 0 和 1 交替
                }
                _sr.sprite = _currentFrame == 0 ? _walkFrame1 : _walkFrame2;
            }
            else
            {
                // 非行走状态：显示 Idle 帧
                if (_idleSprite != null)
                {
                    _sr.sprite = _idleSprite;
                }
                // 重置行走计时器
                _walkTimer = 0f;
                _currentFrame = 0;
            }
        }
    }
}
