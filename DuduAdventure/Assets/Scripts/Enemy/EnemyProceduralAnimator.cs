using UnityEngine;

namespace DuduAdventure.Enemy
{
    /// <summary>
    /// 敌人程序化动画 - 简化版，让敌人精灵也有动态感
    /// </summary>
    /// <remarks>
    /// 自动检测移动状态并施加：
    /// - 待机：呼吸脉动
    /// - 移动：弹跳 + 轻微前倾
    /// - 受击：抖动 + 闪白（配合 HealthComponent）
    /// </remarks>
    public class EnemyProceduralAnimator : MonoBehaviour
    {
        #region 配置

        [Header("呼吸")]
        [SerializeField] private float _breathSpeed = 1.5f;
        [SerializeField] private float _breathAmplitude = 0.04f;

        [Header("移动弹跳")]
        [SerializeField] private float _moveBobSpeed = 10f;
        [SerializeField] private float _moveBobAmplitude = 0.08f;
        [SerializeField] private float _moveLeanAngle = 5f;

        [Header("受击")]
        [SerializeField] private float _hitShakeIntensity = 0.15f;
        [SerializeField] private float _hitShakeDuration = 0.2f;

        #endregion

        #region 运行时

        private Transform _visualTransform;
        private SpriteRenderer _spriteRenderer;
        private Vector3 _baseScale;
        private Vector3 _lastPosition;

        private float _breathTimer;
        private float _moveTimer;
        private float _hitTimer;
        private bool _isMoving;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _visualTransform = _spriteRenderer.transform;
            }
            else
            {
                _visualTransform = transform;
            }

            _baseScale = _visualTransform.localScale;
            _lastPosition = transform.position;

            // 订阅受击事件
            var health = GetComponent<Combat.HealthComponent>();
            if (health != null)
            {
                health.OnDamaged += OnDamaged;
            }
        }

        private void LateUpdate()
        {
            if (_visualTransform == null) return;

            // 检测是否在移动
            float moveDelta = (transform.position - _lastPosition).sqrMagnitude;
            _isMoving = moveDelta > 0.0001f;
            _lastPosition = transform.position;

            Vector3 scale = _baseScale;

            if (_isMoving)
            {
                ApplyMoveScale(ref scale);
            }
            else
            {
                ApplyBreathing(ref scale);
            }

            ApplyHitScale(ref scale);

            // 只修改缩放，不动位置/旋转
            _visualTransform.localScale = scale;
        }

        private void OnDestroy()
        {
            var health = GetComponent<Combat.HealthComponent>();
            if (health != null)
            {
                health.OnDamaged -= OnDamaged;
            }
        }

        #endregion

        #region 动画

        private void ApplyBreathing(ref Vector3 scale)
        {
            _breathTimer += Time.deltaTime * _breathSpeed;
            float breath = Mathf.Sin(_breathTimer) * _breathAmplitude;
            scale.y = _baseScale.y * (1f + breath);
            scale.x = _baseScale.x * (1f - breath * 0.5f);
        }

        private void ApplyMoveScale(ref Vector3 scale)
        {
            _moveTimer += Time.deltaTime * _moveBobSpeed;

            float cycle = Mathf.Sin(_moveTimer);
            scale.x = _baseScale.x * (1f + cycle * 0.03f);
            scale.y = _baseScale.y * (1f - cycle * 0.03f);
        }

        private void ApplyHitScale(ref Vector3 scale)
        {
            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                float t = _hitTimer / _hitShakeDuration;
                float shake = Mathf.Sin(Time.time * 40f) * 0.12f * t;
                scale.x = _baseScale.x * (1f + shake);
            }
        }

        #endregion

        #region 事件

        private void OnDamaged(int damage, int currentHP, int maxHP, Vector2 knockback)
        {
            _hitTimer = _hitShakeDuration;
        }

        #endregion
    }
}
