using UnityEngine;
using DuduAdventure.Core;

namespace DuduAdventure.Player
{
    /// <summary>
    /// 玩家状态枚举 - 定义孙悟空所有可能的行为状态
    /// </summary>
    public enum PlayerState
    {
        Idle,       // 站立待机
        Run,        // 奔跑
        Jump,       // 跳跃上升
        Fall,       // 下落
        Attack,     // 普通攻击（金箍棒）
        Hit,        // 受击
        Transform   // 七十二变（预留功能）
    }

    /// <summary>
    /// 玩家状态机 - 管理孙悟空所有行为状态的切换
    /// 基于通用 StateMachine 构建，添加了玩家专用的状态和转换规则
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerStateMachine : MonoBehaviour
    {
        #region 字段

        // 内部使用的通用状态机实例
        private StateMachine<PlayerController> _stateMachine;

        // 玩家控制器引用
        private PlayerController _playerController;

        // 当前玩家状态（供外部读取）
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        // 是否被击晕/受控（被击时短暂禁用其他转换）
        private bool _isStunned;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            // 初始化状态机
            InitializeStateMachine();
        }

        private void Update()
        {
            // 驱动状态机更新
            _stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine?.FixedUpdate();
        }

        #endregion

        #region 状态机初始化

        /// <summary>
        /// 初始化所有状态和转换规则
        /// </summary>
        private void InitializeStateMachine()
        {
            // 创建状态机实例，传入玩家控制器作为上下文
            _stateMachine = new StateMachine<PlayerController>(_playerController);

            // ====== 注册所有状态 ======
            _stateMachine.AddState("Idle", new IdleState(this));
            _stateMachine.AddState("Run", new RunState(this));
            _stateMachine.AddState("Jump", new JumpState(this));
            _stateMachine.AddState("Fall", new FallState(this));
            _stateMachine.AddState("Attack", new AttackState(this));
            _stateMachine.AddState("Hit", new HitState(this));
            _stateMachine.AddState("Transform", new TransformState(this));

            // ====== 定义状态转换规则 ======
            // 注意：转换规则按优先级排列，先匹配的先执行

            // 从 Idle（待机）出发
            _stateMachine.AddTransition("Idle", "Run", ctx => ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Idle", "Jump", ctx => !ctx.IsGrounded && ctx.VerticalSpeed > 0.1f);
            _stateMachine.AddTransition("Idle", "Fall", ctx => !ctx.IsGrounded && ctx.VerticalSpeed < -0.1f);

            // 从 Run（奔跑）出发
            _stateMachine.AddTransition("Run", "Idle", ctx => !ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Run", "Jump", ctx => !ctx.IsGrounded && ctx.VerticalSpeed > 0.1f);
            _stateMachine.AddTransition("Run", "Fall", ctx => !ctx.IsGrounded && ctx.VerticalSpeed < -0.1f);

            // 从 Jump（跳跃上升）出发
            _stateMachine.AddTransition("Jump", "Fall", ctx => ctx.VerticalSpeed < 0f);
            _stateMachine.AddTransition("Jump", "Idle", ctx => ctx.IsGrounded);

            // 从 Fall（下落）出发
            _stateMachine.AddTransition("Fall", "Idle", ctx => ctx.IsGrounded && !ctx.IsMoving);
            _stateMachine.AddTransition("Fall", "Run", ctx => ctx.IsGrounded && ctx.IsMoving);

            // 从 Attack（攻击）出发 - 攻击结束后根据情况回到其他状态
            _stateMachine.AddTransition("Attack", "Idle", ctx => !ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Attack", "Run", ctx => ctx.IsMoving && ctx.IsGrounded);
            _stateMachine.AddTransition("Attack", "Fall", ctx => !ctx.IsGrounded);

            // 从 Hit（受击）出发 - 受击结束后恢复
            _stateMachine.AddTransition("Hit", "Idle", ctx => ctx.IsGrounded && !_isStunned);
            _stateMachine.AddTransition("Hit", "Fall", ctx => !ctx.IsGrounded && !_isStunned);

            // 初始化到 Idle 状态
            _stateMachine.Initialize("Idle");
            CurrentState = PlayerState.Idle;
        }

        #endregion

        #region 外部触发方法

        /// <summary>
        /// 强制进入攻击状态（由 PlayerCombat 调用）
        /// </summary>
        public void TriggerAttack()
        {
            _stateMachine.ForceTransition("Attack");
            CurrentState = PlayerState.Attack;
        }

        /// <summary>
        /// 强制进入受击状态（由 HealthComponent 的受伤事件调用）
        /// </summary>
        public void TriggerHit()
        {
            _isStunned = true;
            _stateMachine.ForceTransition("Hit");
            CurrentState = PlayerState.Hit;
        }

        /// <summary>
        /// 结束受击状态
        /// </summary>
        public void EndStun()
        {
            _isStunned = false;
        }

        /// <summary>
        /// 触发七十二变（预留功能）
        /// </summary>
        public void TriggerTransform()
        {
            _stateMachine.ForceTransition("Transform");
            CurrentState = PlayerState.Transform;
        }

        #endregion

        #region 状态类定义

        /// <summary>
        /// 待机状态 - 孙悟空原地站立
        /// </summary>
        public class IdleState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            public IdleState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Idle;
                // TODO: 播放待机动画
            }

            public void Update(PlayerController ctx)
            {
                // 待机状态下无额外逻辑，转换由状态机自动处理
            }

            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 停止待机动画
            }
        }

        /// <summary>
        /// 奔跑状态 - 孙悟空移动中
        /// </summary>
        public class RunState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            public RunState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Run;
                // TODO: 播放奔跑动画
                // TODO: 播放脚步声效
            }

            public void Update(PlayerController ctx)
            {
                // TODO: 根据速度调整动画播放速率
                // 示例：animator.SetFloat("Speed", Mathf.Abs(ctx.HorizontalSpeed));
            }

            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 停止奔跑动画
            }
        }

        /// <summary>
        /// 跳跃状态 - 角色正在上升
        /// </summary>
        public class JumpState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            public JumpState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Jump;
                // TODO: 播放跳跃动画
                // TODO: 播放跳跃音效
            }

            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 结束跳跃动画
            }
        }

        /// <summary>
        /// 下落状态 - 角色正在下落
        /// </summary>
        public class FallState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            public FallState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Fall;
                // TODO: 播放下落动画
            }

            public void Update(PlayerController ctx) { }
            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 落地特效和音效
            }
        }

        /// <summary>
        /// 攻击状态 - 挥舞金箍棒攻击
        /// </summary>
        public class AttackState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            // 攻击持续时间（秒）
            private float _attackTimer;
            private const float AttackDuration = 0.4f;

            public AttackState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Attack;
                _attackTimer = AttackDuration;

                // TODO: 播放攻击动画
                // TODO: 激活攻击判定框
                // TODO: 播放攻击音效
            }

            public void Update(PlayerController ctx)
            {
                _attackTimer -= Time.deltaTime;

                // 攻击时间结束，退出攻击状态（转换规则会处理后续）
                // 这里不主动退出，让状态机的转换条件处理
            }

            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 关闭攻击判定框
            }
        }

        /// <summary>
        /// 受击状态 - 角色被敌人打中
        /// </summary>
        public class HitState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            // 受击硬直时间
            private float _stunTimer;
            private const float StunDuration = 0.5f;

            public HitState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Hit;
                _stunTimer = StunDuration;

                // TODO: 播放受击动画
                // TODO: 播放受击音效
                // TODO: 闪烁效果（受伤无敌帧视觉反馈）
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
                // TODO: 停止闪烁效果
            }
        }

        /// <summary>
        /// 七十二变状态 - 变身技能（预留功能）
        /// 孙悟空可以变身为各种形态（鸟、鱼、虫子等），每种形态有不同能力
        /// </summary>
        public class TransformState : IState<PlayerController>
        {
            private readonly PlayerStateMachine _owner;

            public TransformState(PlayerStateMachine owner)
            {
                _owner = owner;
            }

            public void Enter(PlayerController ctx)
            {
                _owner.CurrentState = PlayerState.Transform;
                Debug.Log("[PlayerState] 七十二变！");

                // TODO: 显示变身选择 UI
                // TODO: 播放变身动画和特效
                // TODO: 根据选择的形态切换角色能力
            }

            public void Update(PlayerController ctx)
            {
                // TODO: 处理变身形态的特殊逻辑
            }

            public void FixedUpdate(PlayerController ctx) { }

            public void Exit(PlayerController ctx)
            {
                // TODO: 恢复原始形态
                // TODO: 播放解除变身动画
            }
        }

        #endregion
    }
}
