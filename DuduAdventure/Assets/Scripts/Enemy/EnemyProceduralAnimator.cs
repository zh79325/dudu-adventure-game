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
        private Vector3 _baseLocalPos;
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
            _baseLocalPos = _visualTransform.localPosition;
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
            Vector3 offset = Vector3.zero;
            float rotation = 0f;

            if (_isMoving)
            {
                ApplyMoveBob(ref scale, ref offset, ref rotation);
            }
            else
            {
                ApplyBreathing(ref scale);
            }

            ApplyHitShake(ref offset);

            _visualTransform.localScale = scale;
            _visualTransform.localPosition = _baseLocalPos + offset;
            _visualTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
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

        private void ApplyMoveBob(ref Vector3 scale, ref Vector3 offset, ref float rotation)
        {
            _moveTimer += Time.deltaTime * _moveBobSpeed;

            float bob = Mathf.Abs(Mathf.Sin(_moveTimer)) * _moveBobAmplitude;
            offset.y = bob;

            float squash = Mathf.Sin(_moveTimer * 2f) * 0.02f;
            scale.x = _baseScale.x * (0.97f + squash);
            scale.y = _baseScale.y * (1.03f - squash);

            // 朝向倾斜
            float dir = _spriteRenderer != null && _spriteRenderer.flipX ? -1f : 1f;
            rotation = -dir * _moveLeanAngle * Mathf.Abs(Mathf.Sin(_moveTimer * 0.5f));
        }

        private void ApplyHitShake(ref Vector3 offset)
        {
            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                float t = _hitTimer / _hitShakeDuration;
                float shake = Mathf.Sin(Time.time * 40f) * _hitShakeIntensity * t;
                offset.x += shake;
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
