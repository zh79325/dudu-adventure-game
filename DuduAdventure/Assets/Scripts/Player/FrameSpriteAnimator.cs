using UnityEngine;
using DuduAdventure.Skill;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 帧精灵动画器 —— 根据状态切换对应精灵帧。
    /// 与 ProceduralSpriteAnimator 配合使用（后者负责 scale 动画）。
    /// </summary>
    /// <remarks>
    /// 帧序列约定：
    /// - Run：_walkFrames 循环播放（帧数任意，按 _walkFPS 匀速推进）
    /// - Attack：_attackFrames 按 PlayerStateMachine.AttackProgress 均分播放（一次性，不循环）
    /// - Cast：优先用 SkillManager.CurrentSkill.CastFrames，按 CastProgress 均分；缺省回退到 _attackFrames
    /// - Jump：_jumpSprite，缺省用走路第一帧
    /// - 其他：_idleSprite
    ///
    /// 所有帧必须是同尺寸画布 + 同脚底基线，否则会出现"忽胖忽瘦/忽高忽矮"。
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameSpriteAnimator : MonoBehaviour
    {
        [Header("精灵帧")]
        [Tooltip("待机帧")]
        [SerializeField] private Sprite _idleSprite;

        [Tooltip("行走循环帧（建议 4 帧：接触 / 下沉 / 交换 / 抬起）")]
        [SerializeField] private Sprite[] _walkFrames;

        [Tooltip("普攻帧序列（建议 3 帧：抬棍 / 挥出 / 收势）")]
        [SerializeField] private Sprite[] _attackFrames;

        [Tooltip("跳跃/腾空帧（留空则用行走第一帧）")]
        [SerializeField] private Sprite _jumpSprite;

        [Header("动画设置")]
        [Tooltip("行走每秒切换帧数")]
        [SerializeField] private float _walkFPS = 10f;

        private SpriteRenderer _sr;
        private PlayerStateMachine _stateMachine;
        private SkillManager _skillManager;

        private float _walkTimer;
        private int _walkIndex;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            _skillManager = GetComponent<SkillManager>();
        }

        private void Update()
        {
            if (_stateMachine == null || _sr == null) return;

            switch (_stateMachine.CurrentState)
            {
                case PlayerState.Run:
                    PlayWalkLoop();
                    break;

                case PlayerState.Attack:
                    SetFrameByProgress(_attackFrames, _stateMachine.AttackProgress);
                    ResetWalkCycle();
                    break;

                case PlayerState.Cast:
                    var castFrames = _skillManager != null && _skillManager.CurrentSkill != null
                        ? _skillManager.CurrentSkill.CastFrames
                        : null;
                    if (castFrames == null || castFrames.Length == 0) castFrames = _attackFrames;
                    SetFrameByProgress(castFrames, _stateMachine.CastProgress);
                    ResetWalkCycle();
                    break;

                case PlayerState.Jump:
                    SetSprite(_jumpSprite != null ? _jumpSprite : FirstWalkFrame());
                    ResetWalkCycle();
                    break;

                default:
                    SetSprite(_idleSprite);
                    ResetWalkCycle();
                    break;
            }
        }

        /// <summary>
        /// 行走循环：按 _walkFPS 匀速推进整个帧数组
        /// </summary>
        private void PlayWalkLoop()
        {
            if (_walkFrames == null || _walkFrames.Length == 0)
            {
                SetSprite(_idleSprite);
                return;
            }

            if (_walkFrames.Length == 1)
            {
                SetSprite(_walkFrames[0]);
                return;
            }

            _walkTimer += Time.deltaTime * _walkFPS;
            while (_walkTimer >= 1f)
            {
                _walkTimer -= 1f;
                _walkIndex = (_walkIndex + 1) % _walkFrames.Length;
            }

            SetSprite(_walkFrames[_walkIndex]);
        }

        /// <summary>
        /// 按进度 (0~1) 在帧序列上均分取帧，不循环（进度到 1 停在最后一帧）
        /// </summary>
        private void SetFrameByProgress(Sprite[] frames, float progress)
        {
            if (frames == null || frames.Length == 0) return;

            int index = Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Clamp01(progress) * frames.Length),
                0,
                frames.Length - 1);

            SetSprite(frames[index]);
        }

        private Sprite FirstWalkFrame() =>
            _walkFrames != null && _walkFrames.Length > 0 ? _walkFrames[0] : _idleSprite;

        private void SetSprite(Sprite s)
        {
            if (s != null && _sr.sprite != s) _sr.sprite = s;
        }

        private void ResetWalkCycle()
        {
            _walkTimer = 0f;
            _walkIndex = 0;
        }
    }
}
