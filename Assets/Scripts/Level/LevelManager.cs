using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuduAdventure.Level
{
    /// <summary>
    /// 关卡管理器 - 负责关卡加载、检查点和关卡切换
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// - 使用异步加载场景避免卡顿
    /// - 检查点系统让玩家死亡后可以从最近的安全点重新开始
    /// - 淡入淡出过渡让场景切换更自然
    /// </remarks>
    public class LevelManager : MonoBehaviour
    {
        #region Inspector 配置

        [Header("关卡列表")]
        [Tooltip("所有关卡的场景名称（按顺序排列）")]
        [SerializeField] private string[] _levelNames = { "Level1", "Level2", "Level3" };

        [Header("过渡设置")]
        [Tooltip("过渡画面（全屏黑色 Image 的 Canvas）")]
        [SerializeField] private CanvasGroup _fadeCanvasGroup;

        [Tooltip("淡入持续时间（秒）")]
        [SerializeField] private float _fadeInDuration = 0.5f;

        [Tooltip("淡出持续时间（秒）")]
        [SerializeField] private float _fadeOutDuration = 0.5f;

        [Tooltip("场景切换间的等待时间（让玩家看清过渡画面）")]
        [SerializeField] private float _transitionDelay = 0.3f;

        [Header("检查点")]
        [Tooltip("玩家出生位置的标签（用于 FindGameObjectWithTag）")]
        [SerializeField] private string _spawnPointTag = "SpawnPoint";

        #endregion

        #region 事件

        /// <summary>
        /// 关卡开始加载时触发
        /// 参数：关卡索引
        /// </summary>
        public event Action<int> OnLevelLoadStart;

        /// <summary>
        /// 关卡加载完成时触发
        /// 参数：关卡索引
        /// </summary>
        public event Action<int> OnLevelLoadComplete;

        /// <summary>
        /// 激活检查点时触发
        /// 参数：检查点位置
        /// </summary>
        public event Action<Vector3> OnCheckpointActivated;

        /// <summary>
        /// 关卡完成时触发
        /// 参数：完成的关卡索引
        /// </summary>
        public event Action<int> OnLevelCompleted;

        #endregion

        #region 运行时状态

        // 当前关卡索引
        private int _currentLevelIndex = -1;

        // 最近的检查点位置
        private Vector3 _lastCheckpointPosition;

        // 已激活的检查点列表
        private readonly HashSet<int> _activatedCheckpoints = new HashSet<int>();

        // 是否正在加载场景
        private bool _isLoading;

        // 玩家引用
        private Transform _playerTransform;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前关卡索引
        /// </summary>
        public int CurrentLevelIndex => _currentLevelIndex;

        /// <summary>
        /// 最近的检查点位置
        /// </summary>
        public Vector3 LastCheckpointPosition => _lastCheckpointPosition;

        /// <summary>
        /// 是否正在加载场景
        /// </summary>
        public bool IsLoading => _isLoading;

        /// <summary>
        /// 总关卡数
        /// </summary>
        public int TotalLevels => _levelNames.Length;

        #endregion

        #region 生命周期

        private void Start()
        {
            // 尝试找到玩家
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        #endregion

        #region 关卡加载

        /// <summary>
        /// 加载指定索引的关卡（带过渡效果）
        /// </summary>
        /// <param name="levelIndex">关卡索引</param>
        public void LoadLevel(int levelIndex)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[LevelManager] 正在加载中，请等待...");
                return;
            }

            if (levelIndex < 0 || levelIndex >= _levelNames.Length)
            {
                Debug.LogError($"[LevelManager] 关卡索引 {levelIndex} 超出范围！");
                return;
            }

            StartCoroutine(LoadLevelRoutine(levelIndex));
        }

        /// <summary>
        /// 加载关卡的协程 - 实现淡入 -> 加载 -> 淡出的完整流程
        /// </summary>
        private IEnumerator LoadLevelRoutine(int levelIndex)
        {
            _isLoading = true;

            // 触发加载开始事件
            OnLevelLoadStart?.Invoke(levelIndex);

            // ===== 第一步：淡入（画面变黑） =====
            yield return StartCoroutine(FadeIn());

            // ===== 第二步：异步加载场景 =====
            string sceneName = _levelNames[levelIndex];
            Debug.Log($"[LevelManager] 正在加载关卡: {sceneName}");

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

            // 等待场景加载完成
            while (!loadOperation.isDone)
            {
                // 可以在这里更新加载进度条
                // TODO: 显示加载进度 UI
                // 示例：loadingBar.fillAmount = loadOperation.progress;
                yield return null;
            }

            // 更新当前关卡索引
            _currentLevelIndex = levelIndex;

            // 等待一小段时间确保场景完全初始化
            yield return new WaitForSeconds(_transitionDelay);

            // 重新查找玩家引用（新场景中）
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }

            // 设置摄像机边界（如果新场景有定义的话）
            // TODO: 从关卡数据中读取边界设置

            // 触发加载完成事件
            OnLevelLoadComplete?.Invoke(levelIndex);

            // ===== 第三步：淡出（画面恢复正常） =====
            yield return StartCoroutine(FadeOut());

            _isLoading = false;

            Debug.Log($"[LevelManager] 关卡 {sceneName} 加载完成！");
        }

        /// <summary>
        /// 重新加载当前关卡（玩家死亡后使用）
        /// </summary>
        public void ReloadCurrentLevel()
        {
            if (_currentLevelIndex >= 0)
            {
                LoadLevel(_currentLevelIndex);
            }
        }

        /// <summary>
        /// 加载下一关
        /// </summary>
        public void LoadNextLevel()
        {
            int nextIndex = _currentLevelIndex + 1;

            if (nextIndex >= _levelNames.Length)
            {
                // 已通关所有关卡
                Debug.Log("[LevelManager] 所有关卡已完成！");
                OnLevelCompleted?.Invoke(_currentLevelIndex);

                // 通知 GameManager 胜利
                if (Core.GameManager.Instance != null)
                {
                    Core.GameManager.Instance.NextLevel();
                }
                return;
            }

            // 通知 GameManager 进入下一关
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.NextLevel();
            }

            LoadLevel(nextIndex);
        }

        #endregion

        #region 检查点系统

        /// <summary>
        /// 激活检查点 - 当玩家到达检查点位置时调用
        /// </summary>
        /// <param name="checkpointPosition">检查点位置</param>
        /// <param name="checkpointId">检查点唯一 ID</param>
        public void ActivateCheckpoint(Vector3 checkpointPosition, int checkpointId = -1)
        {
            _lastCheckpointPosition = checkpointPosition;

            if (checkpointId >= 0)
            {
                _activatedCheckpoints.Add(checkpointId);
            }

            Debug.Log($"[LevelManager] 检查点激活！位置: {checkpointPosition}");

            OnCheckpointActivated?.Invoke(checkpointPosition);

            // TODO: 播放检查点激活动画和音效
            // TODO: 显示"检查点已保存"UI 提示
        }

        /// <summary>
        /// 将玩家传送到最近的检查点（死亡后调用）
        /// </summary>
        public void RespawnPlayer()
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                }
            }

            if (_playerTransform == null)
            {
                Debug.LogError("[LevelManager] 找不到玩家，无法重生！");
                return;
            }

            // 检查是否有生命剩余
            if (Core.GameManager.Instance != null && !Core.GameManager.Instance.LoseLife())
            {
                // 没有生命了，游戏结束
                return;
            }

            // 传送到检查点
            StartCoroutine(RespawnRoutine());
        }

        /// <summary>
        /// 重生的协程（带过渡效果）
        /// </summary>
        private IEnumerator RespawnRoutine()
        {
            // 淡入
            yield return StartCoroutine(FadeIn());

            // 传送玩家到检查点
            if (_playerTransform != null)
            {
                // 禁用玩家控制（传送期间）
                var controller = _playerTransform.GetComponent<Player.PlayerController>();
                if (controller != null) controller.DisableControl();

                // 移动玩家
                _playerTransform.position = _lastCheckpointPosition;

                // 恢复玩家血量
                var health = _playerTransform.GetComponent<Combat.HealthComponent>();
                if (health != null) health.ResetHP();

                // 短暂等待
                yield return new WaitForSeconds(0.2f);

                // 恢复玩家控制
                if (controller != null) controller.EnableControl();
            }

            // 淡出
            yield return StartCoroutine(FadeOut());
        }

        /// <summary>
        /// 重置所有检查点（开始新关卡时调用）
        /// </summary>
        public void ResetCheckpoints()
        {
            _activatedCheckpoints.Clear();
            _lastCheckpointPosition = Vector3.zero;
        }

        #endregion

        #region 过渡效果

        /// <summary>
        /// 淡入 - 画面逐渐变黑
        /// </summary>
        private IEnumerator FadeIn()
        {
            if (_fadeCanvasGroup == null) yield break;

            _fadeCanvasGroup.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 不受 TimeScale 影响
                _fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _fadeInDuration);
                yield return null;
            }

            _fadeCanvasGroup.alpha = 1f;
        }

        /// <summary>
        /// 淡出 - 画面逐渐恢复
        /// </summary>
        private IEnumerator FadeOut()
        {
            if (_fadeCanvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                yield return null;
            }

            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.gameObject.SetActive(false);
        }

        #endregion

        #region 关卡完成检测

        /// <summary>
        /// 标记当前关卡完成
        /// 通常由关卡终点的触发器调用
        /// </summary>
        public void CompleteCurrentLevel()
        {
            Debug.Log($"[LevelManager] 关卡 {_currentLevelIndex} 完成！");
            OnLevelCompleted?.Invoke(_currentLevelIndex);

            // 加分奖励
            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.AddScore(500); // 通关奖励
            }

            // 加载下一关
            LoadNextLevel();
        }

        #endregion

        #region 触发器检测

        /// <summary>
        /// 当玩家进入检查点触发器时调用
        /// 将此方法关联到检查点物体的 OnTriggerEnter2D 事件
        /// </summary>
        /// <param name="checkpoint">检查点触发器</param>
        public void OnTriggerEnterCheckpoint(Collider2D checkpoint)
        {
            ActivateCheckpoint(checkpoint.transform.position, checkpoint.GetInstanceID());
        }

        /// <summary>
        /// 当玩家进入关卡终点触发器时调用
        /// </summary>
        public void OnTriggerEnterLevelEnd(Collider2D levelEnd)
        {
            CompleteCurrentLevel();
        }

        #endregion
    }

    /// <summary>
    /// 检查点触发器 - 挂载在关卡中的检查点物体上
    /// 当玩家触碰时激活检查点
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("检查点 ID（同一关卡中应唯一）")]
        [SerializeField] private int _checkpointId;

        [Tooltip("是否已激活")]
        [SerializeField] private bool _isActivated;

        private SpriteRenderer _spriteRenderer;

        // 未激活和激活时的颜色
        [Header("视觉")]
        [SerializeField] private Color _inactiveColor = Color.gray;
        [SerializeField] private Color _activeColor = Color.yellow;

        public bool IsActivated => _isActivated;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 确保碰撞体是触发器
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Start()
        {
            UpdateVisual();
        }

        /// <summary>
        /// 玩家进入检查点触发区域
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 只响应玩家
            if (!other.CompareTag("Player")) return;

            if (_isActivated) return; // 已经激活过了

            _isActivated = true;
            UpdateVisual();

            // 通知 LevelManager
            var levelManager = FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.ActivateCheckpoint(transform.position, _checkpointId);
            }

            Debug.Log($"[Checkpoint] 检查点 {_checkpointId} 已激活！");

            // TODO: 播放激活动画（如旗子升起）
            // TODO: 播放激活音效
        }

        /// <summary>
        /// 更新视觉状态
        /// </summary>
        private void UpdateVisual()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _isActivated ? _activeColor : _inactiveColor;
            }
        }
    }

    /// <summary>
    /// 关卡终点触发器 - 挂载在关卡终点区域
    /// 玩家进入后触发关卡完成
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelEndTrigger : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            var levelManager = FindAnyObjectByType<LevelManager>();
            if (levelManager != null)
            {
                levelManager.CompleteCurrentLevel();
            }
        }
    }
}
