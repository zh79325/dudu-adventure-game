using UnityEngine;
using DuduAdventure.Core;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家状态枚举 - DNF 式横版格斗的行为状态
    /// </summary>
    public enum PlayerState
    {
        Idle,       // 站立待机
        Run,        // 地面移动（水平或纵深）
        Jump,       // 腾空（上升 + 下落统一为一个状态）
        Attack,     // 攻击
        Hit,        // 受击
        Transform,  // 大招（预留）
        Cast        // 释放技能（由 SkillManager 驱动）
    }

    /// <summary>
    /// 玩家状态机 - 管理 DNF 式角色的状态切换
    /// </summary>
    /// <remarks>
    /// 与平台跳跃版的区别：
    /// - 没有独立的 Fall 状态（DNF 跳跃上升+下落是一个完整动作）
    /// - Run 触发条件包含纵深移动（上下走位也算在动）
    /// - IsGrounded 的含义变了：jumpHeight == 0，而非地面碰撞检测
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerStateMachine : MonoBehaviour
    {
        #region 字段

        private StateMachine<PlayerController> _stateMachine;
        private PlayerController _playerController;

        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        private bool _isStunned;

        /// <summary>
        /// 攻击锁定计时器 - 防止攻击状态在动画播完前退出
        /// </summary>
        private float _attackLockTimer;
        private const float AttackLockDuration = 0.4f;

        /// <summary>
        /// 攻击进度 0~1（供帧动画选帧用）
        /// </summary>
        public float AttackProgress =>
            AttackLockDuration <= 0f ? 1f : Mathf.Clamp01(1f - _attackLockTimer / AttackLockDuration);

        /// <summary>
        /// 施法锁定计时器 - 由 SkillManager 通过 TriggerCast 设置
        /// </summary>
        private float _castLockTimer;
        private float _castDuration;

        /// <summary>
        /// 施法进度 0~1（供帧动画选帧用）
        /// </summary>
        public float CastProgress =>
            _castDuration <= 0f ? 1f : Mathf.Clamp01(1f - _castLockTimer / _castDuration);

        #endregion

        #region 生命周期

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            InitializeStateMachine();
        }

        private void Update()
        {
            if (_attackLockTimer > 0f)
                _attackLockTimer -= Time.deltaTime;

            if (_castLockTimer > 0f)
                _castLockTimer -= Time.deltaTime;

            _stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine?.FixedUpdate();
        }

        #endregion

        #region 状态机初始化

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<PlayerController>(_playerController);

            // 注册状态
            _stateMachine.AddState("Idle", new IdleState(this));
            _stateMachine.AddState("Run", new RunState(this));
            _stateMachine.AddState("Jump", new JumpState(this));
            _stateMachine.AddState("Attack", new AttackState(this));
            _stateMachine.AddState("Hit", new HitState(this));
            _stateMachine.AddState("Transform", new TransformState(this));
            _stateMachine.AddState("Cast", new CastState(this));

            // 状态转换规则（DNF 式）
            // Idle -> Run：地面上有移动输入
            _stateMachine.AddTransition("Idle", "Run", ctx => ctx.IsMoving && ctx.IsGrounded);
            // Idle -> Jump：离开地面
            _stateMachine.AddTransition("Idle", "Jump", ctx => !ctx.IsGrounded);

            // Run -> Idle：地面上无输入
            _stateMachine.AddTransition("Run", "Idle", ctx => !ctx.IsMoving && ctx.IsGrounded);
            // Run -> Jump：离开地面（跑步中起跳）
            _stateMachine.AddTransition("Run", "Jump", ctx => !ctx.IsGrounded);

            // Jump -> Idle：落地且无输入
            _stateMachine.AddTransition("Jump", "Idle", ctx => ctx.IsGrounded && !ctx.IsMoving);
            // Jump -> Run：落地且有输入
            _stateMachine.AddTransition("Jump", "Run", ctx => ctx.IsGrounded && ctx.IsMoving);

            // Attack -> 根据落地情况回到对应状态（需等攻击锁定结束）
            _stateMachine.AddTransition("Attack", "Idle", ctx => _attackLockTimer <= 0f && !ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Attack", "Run", ctx => _attackLockTimer <= 0f && ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Attack", "Jump", ctx => _attackLockTimer <= 0f && !ctx.IsGrounded);

            // Cast -> 施法结束后回到对应状态
            _stateMachine.AddTransition("Cast", "Idle", ctx => _castLockTimer <= 0f && !ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Cast", "Run", ctx => _castLockTimer <= 0f && ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Cast", "Jump", ctx => _castLockTimer <= 0f && !ctx.IsGrounded);

            // Hit -> 恢复
            _stateMachine.AddTransition("Hit", "Idle", ctx => ctx.IsGrounded && !_isStunned);
            _stateMachine.AddTransition("Hit", "Jump", ctx => !ctx.IsGrounded && !_isStunned);

            _stateMachine.Initialize("Idle");
            CurrentState = PlayerState.Idle;
        }

        #endregion

        #region 外部触发

        public void TriggerAttack()
        {
            _attackLockTimer = AttackLockDuration;
            _stateMachine.ForceTransition("Attack");
            CurrentState = PlayerState.Attack;
        }

        /// <summary>
        /// 触发施法状态（由 SkillManager 调用）
        /// </summary>
        /// <param name="duration">整段施法总时长 = CastTime + (HitCount-1)*HitInterval + RecoveryTime</param>
        public void TriggerCast(float duration)
        {
            _castDuration = Mathf.Max(0.01f, duration);
            _castLockTimer = _castDuration;
            _stateMachine.ForceTransition("Cast");
            CurrentState = PlayerState.Cast;
        }

        public void TriggerHit()
        {
            _isStunned = true;
            _stateMachine.ForceTransition("Hit");
            CurrentState = PlayerState.Hit;
        }

        public void EndStun()
        {
            _isStunned = false;
        }

        public void TriggerTransform()
        {
            _stateMachine.ForceTransition("Transform");
            CurrentState = PlayerState.Transform;
        }

        #endregion

        #region 状态类

        public class IdleState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            public IdleState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx) { _owner.CurrentState = PlayerState.Idle; }
            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        public class RunState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            public RunState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx) { _owner.CurrentState = PlayerState.Run; }
            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        /// <summary>
        /// 跳跃状态（DNF 式：上升+下落合为一体，落地自动退出）
        /// </summary>
        public class JumpState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            public JumpState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx) { _owner.CurrentState = PlayerState.Jump; }
            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        public class AttackState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            private float _attackTimer;
            private const float AttackDuration = 0.4f;

            public AttackState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Attack;
                _attackTimer = AttackDuration;
            }

            public void Update(PlayerController ctx)
            {
                _attackTimer -= Time.deltaTime;
            }

            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        public class HitState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            private float _stunTimer;
            private const float StunDuration = 0.5f;

            public HitState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Hit;
                _stunTimer = StunDuration;
            }

            public void Update(PlayerController ctx)
            {
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f)
                {
                    _owner.EndStun();
                }
            }

            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                _owner.EndStun();
            }
        }

        /// <summary>
        /// 施法状态（由 SkillManager 触发，持续时间由 TriggerCast 传入）
        /// </summary>
        public class CastState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            public CastState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx) { _owner.CurrentState = PlayerState.Cast; }
            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        public class TransformState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;
            public TransformState(PlayerStateMachine owner) { _owner = owner; }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Transform;
            }

            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }
            public void Exit(PlayerController ctx) { }
        }

        #endregion
    }
}
