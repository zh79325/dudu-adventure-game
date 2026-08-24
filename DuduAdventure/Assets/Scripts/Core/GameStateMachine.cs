using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuduAdventure.Core
{
    /// <summary>
    /// 状态接口 - 所有状态类都必须实现此接口
    /// 这是状态机模式的核心，定义了每个状态必须具备的行为
    /// </summary>
    /// <remarks>
    /// 设计说明：
    /// - 使用接口而非抽象类，让状态类可以自由继承其他基类
    /// - TContext 是泛型参数，代表拥有此状态机的对象类型（如 PlayerController、EnemyBase）
    /// </remarks>
    public interface IState<TContext> where TContext : class
    {
        /// <summary>
        /// 进入状态时调用（仅调用一次）
        /// 用于初始化状态数据、播放进入动画等
        /// </summary>
        /// <param name="context">拥有此状态机的对象</param>
        void Enter(TContext context);

        /// <summary>
        /// 状态持续期间每帧调用
        /// 用于处理状态逻辑、动画更新等
        /// </summary>
        /// <param name="context">拥有此状态机的对象</param>
        void Update(TContext context);

        /// <summary>
        /// 每固定物理帧调用（对应 FixedUpdate）
        /// 用于处理物理相关的逻辑
        /// </summary>
        /// <param name="context">拥有此状态机的对象</param>
        void FixedUpdate(TContext context);

        /// <summary>
        /// 离开状态时调用（仅调用一次）
        /// 用于清理状态数据、停止动画等
        /// </summary>
        /// <param name="context">拥有此状态机的对象</param>
        void Exit(TContext context);
    }

    /// <summary>
    /// 状态转换规则 - 描述从一个状态到另一个状态的转换条件
    /// </summary>
    /// <typeparam name="TContext">拥有状态机的对象类型</typeparam>
    [Serializable]
    public class StateTransition<TContext> where TContext : class
    {
        // 目标状态的名称（用于查找目标状态）
        public string TargetStateName { get; private set; }

        // 转换条件 - 一个返回 bool 的函数
        // 当返回 true 时，允许转换到目标状态
        private readonly Func<TContext, bool> _condition;

        /// <summary>
        /// 创建一条状态转换规则
        /// </summary>
        /// <param name="targetStateName">目标状态名称</param>
        /// <param name="condition">转换条件函数</param>
        public StateTransition(string targetStateName, Func<TContext, bool> condition)
        {
            TargetStateName = targetStateName;
            _condition = condition;
        }

        /// <summary>
        /// 检查转换条件是否满足
        /// </summary>
        public bool ShouldTransition(TContext context)
        {
            return _condition != null && _condition(context);
        }
    }

    /// <summary>
    /// 通用状态机 - 可复用于玩家、敌人、UI 等各种需要状态管理的场景
    /// </summary>
    /// <remarks>
    /// 使用说明：
    /// 1. 创建状态类，实现 IState&lt;TContext&gt; 接口
    /// 2. 用 AddState() 注册所有状态
    /// 3. 用 AddTransition() 添加状态之间的转换规则
    /// 4. 用 Initialize() 设置初始状态并启动状态机
    /// 5. 在 Update() 和 FixedUpdate() 中调用状态机的对应方法
    /// </remarks>
    /// <typeparam name="TContext">拥有状态机的对象类型</typeparam>
    public class StateMachine<TContext> where TContext : class
    {
        // 所有已注册的状态，用字典存储方便按名称查找
        private readonly Dictionary<string, IState<TContext>> _states = new Dictionary<string, IState<TContext>>();

        // 每个状态对应的转换规则列表
        private readonly Dictionary<string, List<StateTransition<TContext>>> _transitions =
            new Dictionary<string, List<StateTransition<TContext>>>();

        // 当前正在运行的状态
        public IState<TContext> CurrentState { get; private set; }

        // 当前状态的名称
        public string CurrentStateName { get; private set; }

        // 状态机的拥有者
        private readonly TContext _context;

        // 状态机是否已初始化
        private bool _isInitialized;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="context">状态机的拥有者（如 PlayerController 实例）</param>
        public StateMachine(TContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 注册一个状态
        /// </summary>
        /// <param name="stateName">状态名称（用于标识和查找）</param>
        /// <param name="state">状态实例</param>
        public void AddState(string stateName, IState<TContext> state)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                Debug.LogError("[StateMachine] 状态名称不能为空！");
                return;
            }

            if (_states.ContainsKey(stateName))
            {
                Debug.LogWarning($"[StateMachine] 状态 '{stateName}' 已存在，将被覆盖");
            }

            _states[stateName] = state;

            // 同时创建空的转换规则列表
            if (!_transitions.ContainsKey(stateName))
            {
                _transitions[stateName] = new List<StateTransition<TContext>>();
            }
        }

        /// <summary>
        /// 添加一条状态转换规则
        /// </summary>
        /// <param name="fromState">起始状态名称</param>
        /// <param name="toState">目标状态名称</param>
        /// <param name="condition">转换条件</param>
        public void AddTransition(string fromState, string toState, Func<TContext, bool> condition)
        {
            // 检查起始状态是否已注册
            if (!_states.ContainsKey(fromState))
            {
                Debug.LogError($"[StateMachine] 起始状态 '{fromState}' 未注册！");
                return;
            }

            // 检查目标状态是否已注册
            if (!_states.ContainsKey(toState))
            {
                Debug.LogError($"[StateMachine] 目标状态 '{toState}' 未注册！");
                return;
            }

            // 创建转换规则并添加到起始状态的转换列表中
            var transition = new StateTransition<TContext>(toState, condition);

            if (!_transitions.ContainsKey(fromState))
            {
                _transitions[fromState] = new List<StateTransition<TContext>>();
            }

            _transitions[fromState].Add(transition);
        }

        /// <summary>
        /// 初始化状态机并进入初始状态
        /// </summary>
        /// <param name="initialStateName">初始状态名称</param>
        public void Initialize(string initialStateName)
        {
            if (!_states.ContainsKey(initialStateName))
            {
                Debug.LogError($"[StateMachine] 初始状态 '{initialStateName}' 未注册！");
                return;
            }

            _isInitialized = true;

            // 进入初始状态（不退出旧状态，因为没有旧状态）
            CurrentState = _states[initialStateName];
            CurrentStateName = initialStateName;
            CurrentState.Enter(_context);

            Debug.Log($"[StateMachine] 初始化完成，进入状态: {initialStateName}");
        }

        /// <summary>
        /// 每帧调用 - 检查转换条件并更新当前状态
        /// 应在拥有者的 Update() 方法中调用
        /// </summary>
        public void Update()
        {
            if (!_isInitialized || CurrentState == null) return;

            // 先检查是否有满足条件的转换
            CheckTransitions();

            // 更新当前状态
            CurrentState?.Update(_context);
        }

        /// <summary>
        /// 每固定物理帧调用
        /// 应在拥有者的 FixedUpdate() 方法中调用
        /// </summary>
        public void FixedUpdate()
        {
            if (!_isInitialized || CurrentState == null) return;

            CurrentState.FixedUpdate(_context);
        }

        /// <summary>
        /// 强制切换到指定状态（跳过转换条件检查）
        /// 用于被攻击、死亡等需要立即切换状态的情况
        /// </summary>
        /// <param name="stateName">目标状态名称</param>
        public void ForceTransition(string stateName)
        {
            if (!_states.ContainsKey(stateName))
            {
                Debug.LogError($"[StateMachine] 目标状态 '{stateName}' 未注册！");
                return;
            }

            ChangeState(stateName);
        }

        /// <summary>
        /// 检查当前状态的所有转换规则
        /// 按添加顺序检查，第一个满足条件的转换会被执行
        /// </summary>
        private void CheckTransitions()
        {
            // 如果当前状态没有转换规则，直接返回
            if (!_transitions.ContainsKey(CurrentStateName)) return;

            var transitions = _transitions[CurrentStateName];

            // 遍历所有转换规则
            foreach (var transition in transitions)
            {
                // 检查转换条件
                if (transition.ShouldTransition(_context))
                {
                    ChangeState(transition.TargetStateName);
                    return; // 每帧最多只执行一次转换
                }
            }
        }

        /// <summary>
        /// 执行状态切换
        /// </summary>
        /// <param name="newStateName">新状态名称</param>
        private void ChangeState(string newStateName)
        {
            // 避免切换到当前状态
            if (CurrentStateName == newStateName) return;

            string oldStateName = CurrentStateName;

            // 退出旧状态
            CurrentState?.Exit(_context);

            // 进入新状态
            CurrentState = _states[newStateName];
            CurrentStateName = newStateName;
            CurrentState.Enter(_context);

            #if UNITY_EDITOR && STATEMACHINE_VERBOSE
            Debug.Log($"[StateMachine] 状态转换: {oldStateName} -> {newStateName}");
            #endif
        }
    }
}
