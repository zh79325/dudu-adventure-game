using System;
using UnityEngine;

namespace DuduAdventure.Core
{
    /// <summary>
    /// 游戏状态枚举 - 定义游戏中所有可能的全局状态
    /// </summary>
    public enum GameState
    {
        Loading,    // 加载中（场景切换、资源加载）
        Playing,    // 游戏进行中
        Paused,     // 暂停
        GameOver,   // 游戏结束（玩家死亡）
        Victory     // 通关胜利
    }

    /// <summary>
    /// 游戏管理器 - 整个游戏的核心控制器
    /// 使用单例模式，保证全局只有一个实例
    /// 负责管理游戏状态、分数、关卡进度等全局数据
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 单例模式

        // 单例实例引用，全局唯一
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// Awake 在对象创建时调用，比 Start 更早
        /// 在这里初始化单例，确保其他脚本在 Start 中可以使用 Instance
        /// </summary>
        private void Awake()
        {
            // 如果已经有实例存在，说明是重复创建，销毁自己
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 检测到重复实例，正在销毁...");
                Destroy(gameObject);
                return;
            }

            // 设置自己为全局实例
            Instance = this;

            // 切换场景时不要销毁这个游戏管理器
            DontDestroyOnLoad(gameObject);

            // 初始化游戏数据
            InitializeGame();
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// 游戏状态改变时触发的事件
        /// 参数：旧状态, 新状态
        /// 用法示例：GameManager.Instance.OnGameStateChanged += (oldState, newState) => { ... };
        /// </summary>
        public event Action<GameState, GameState> OnGameStateChanged;

        /// <summary>
        /// 分数改变时触发的事件
        /// 参数：当前总分
        /// 用法示例：GameManager.Instance.OnScoreChanged += (score) => { ui.UpdateScore(score); };
        /// </summary>
        public event Action<int> OnScoreChanged;

        /// <summary>
        /// 关卡改变时触发的事件
        /// 参数：当前关卡编号
        /// </summary>
        public event Action<int> OnLevelChanged;

        #endregion

        #region 游戏数据（可在 Inspector 面板中配置）

        [Header("初始设置")]
        [Tooltip("初始生命数量")]
        [SerializeField] private int _initialLives = 3;

        [Tooltip("最大关卡数量")]
        [SerializeField] private int _maxLevels = 10;

        // ---- 运行时数据 ----

        // 当前游戏状态（只读属性，只能通过 ChangeState 方法修改）
        public GameState CurrentState { get; private set; } = GameState.Loading;

        // 当前分数
        public int Score { get; private set; }

        // 当前关卡编号（从 1 开始）
        public int CurrentLevel { get; private set; } = 1;

        // 剩余生命数
        public int Lives { get; private set; }

        // 游戏是否暂停中
        public bool IsPaused => CurrentState == GameState.Paused;

        // 游戏是否正在进行中（非暂停、非结束）
        public bool IsPlaying => CurrentState == GameState.Playing;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化游戏数据为默认值
        /// 在游戏启动时和重新开始游戏时调用
        /// </summary>
        private void InitializeGame()
        {
            Score = 0;
            CurrentLevel = 1;
            Lives = _initialLives;
            ChangeState(GameState.Loading);
        }

        #endregion

        #region 游戏流程控制

        /// <summary>
        /// 开始游戏 - 从第一关开始
        /// 通常由主菜单的"开始游戏"按钮调用
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[GameManager] 游戏开始！踏上取经之路...");

            // 重置所有数据
            Score = 0;
            CurrentLevel = 1;
            Lives = _initialLives;

            // 通知 UI 更新分数
            OnScoreChanged?.Invoke(Score);

            // 切换到游戏进行中状态
            ChangeState(GameState.Playing);

            // TODO: 加载第一关场景
            // 示例：UnityEngine.SceneManagement.SceneManager.LoadScene("Level1");
        }

        /// <summary>
        /// 暂停游戏 - 冻结游戏逻辑
        /// 通常由暂停菜单按钮或 ESC 键触发
        /// </summary>
        public void PauseGame()
        {
            // 只有在游戏进行中时才能暂停
            if (CurrentState != GameState.Playing)
            {
                Debug.LogWarning("[GameManager] 当前状态不允许暂停: " + CurrentState);
                return;
            }

            Debug.Log("[GameManager] 游戏暂停");

            // 设置时间缩放为 0，冻结所有物理和动画
            Time.timeScale = 0f;

            ChangeState(GameState.Paused);

            // TODO: 显示暂停菜单 UI
        }

        /// <summary>
        /// 恢复游戏 - 从暂停状态继续
        /// 通常由暂停菜单的"继续"按钮调用
        /// </summary>
        public void ResumeGame()
        {
            // 只有在暂停状态时才能恢复
            if (CurrentState != GameState.Paused)
            {
                Debug.LogWarning("[GameManager] 当前状态不允许恢复: " + CurrentState);
                return;
            }

            Debug.Log("[GameManager] 游戏继续");

            // 恢复时间缩放
            Time.timeScale = 1f;

            ChangeState(GameState.Playing);

            // TODO: 隐藏暂停菜单 UI
        }

        /// <summary>
        /// 游戏结束 - 玩家生命耗尽
        /// 通常由玩家死亡逻辑调用
        /// </summary>
        public void GameOver()
        {
            Debug.Log("[GameManager] 游戏结束！取经之路任重道远...");

            ChangeState(GameState.GameOver);

            // TODO: 显示游戏结束画面
            // TODO: 保存最高分
            // TODO: 播放游戏结束音效
        }

        /// <summary>
        /// 进入下一关
        /// 当玩家到达关卡终点时调用
        /// </summary>
        public void NextLevel()
        {
            CurrentLevel++;

            // 检查是否已通关所有关卡
            if (CurrentLevel > _maxLevels)
            {
                Victory();
                return;
            }

            Debug.Log($"[GameManager] 进入第 {CurrentLevel} 关");

            // 通知关卡变更
            OnLevelChanged?.Invoke(CurrentLevel);

            // TODO: 加载下一关场景
            // 示例：UnityEngine.SceneManagement.SceneManager.LoadScene($"Level{CurrentLevel}");
        }

        /// <summary>
        /// 通关胜利 - 所有关卡完成
        /// </summary>
        private void Victory()
        {
            Debug.Log("[GameManager] 恭喜！取经成功，功德圆满！");

            ChangeState(GameState.Victory);

            // TODO: 显示通关画面和最终分数
            // TODO: 播放胜利动画和音乐
        }

        /// <summary>
        /// 重新开始游戏 - 回到第一关
        /// 通常由游戏结束画面的"重新开始"按钮调用
        /// </summary>
        public void RestartGame()
        {
            // 确保时间缩放正常
            Time.timeScale = 1f;
            StartGame();
        }

        /// <summary>
        /// 扣除一条生命
        /// 当玩家死亡且还有剩余生命时调用
        /// </summary>
        /// <returns>是否还有剩余生命</returns>
        public bool LoseLife()
        {
            Lives--;
            Debug.Log($"[GameManager] 剩余生命: {Lives}");

            if (Lives <= 0)
            {
                GameOver();
                return false; // 没有剩余生命了
            }

            return true; // 还有生命，可以从检查点重新开始
        }

        #endregion

        #region 分数系统

        /// <summary>
        /// 增加分数
        /// </summary>
        /// <param name="amount">增加的分数（必须为正数）</param>
        public void AddScore(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[GameManager] 增加的分数必须大于 0");
                return;
            }

            Score += amount;

            // 触发分数变更事件，通知所有监听者（如 UI）
            OnScoreChanged?.Invoke(Score);

            Debug.Log($"[GameManager] 分数 +{amount}，总分: {Score}");
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 切换游戏状态（内部方法）
        /// 所有状态变更都应通过此方法进行，确保事件被正确触发
        /// </summary>
        /// <param name="newState">新的游戏状态</param>
        private void ChangeState(GameState newState)
        {
            // 如果状态没有变化，不需要做任何事
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameManager] 状态变更: {oldState} -> {newState}");

            // 触发状态变更事件
            OnGameStateChanged?.Invoke(oldState, newState);
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// Update 每帧调用一次
        /// 在这里处理全局快捷键输入
        /// </summary>
        private void Update()
        {
            // 按 ESC 键切换暂停/继续
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (CurrentState == GameState.Playing)
                {
                    PauseGame();
                }
                else if (CurrentState == GameState.Paused)
                {
                    ResumeGame();
                }
            }
        }

        /// <summary>
        /// 对象被销毁时清理单例引用
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion
    }
}
