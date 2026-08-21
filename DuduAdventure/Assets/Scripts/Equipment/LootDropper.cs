using UnityEngine;
using DuduAdventure.Combat;

namespace DuduAdventure.Equipment
{
    /// <summary>
    /// 掉落生成器 - 挂在敌人身上，死亡时生成 DropPickup 实体
    /// </summary>
    /// <remarks>
    /// 使用方式：
    /// 1. 在敌人 Prefab 上添加此组件
    /// 2. 配置 LootTable（掉什么）和 DropPickupPrefab（掉落物外观）
    /// 3. 此组件会自动监听同物体上 HealthComponent 的 OnDeath 事件
    /// 
    /// 掉落物会以小弧线弹出，落地后可被玩家拾取。
    /// </remarks>
    public class LootDropper : MonoBehaviour
    {
        #region Inspector 配置

        [Header("掉落配置")]
        [Tooltip("使用的掉落表")]
        [SerializeField] private LootTable _lootTable;

        [Tooltip("掉落物 Prefab（需要有 DropPickup 组件）")]
        [SerializeField] private GameObject _dropPickupPrefab;

        [Header("生成设置")]
        [Tooltip("掉落物生成的 Y 轴偏移")]
        [SerializeField] private float _spawnYOffset = 0.5f;

        [Tooltip("掉落物水平随机散布范围")]
        [SerializeField] private float _spreadRange = 0.5f;

        #endregion

        #region 组件引用

        private HealthComponent _health;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _health = GetComponent<Combat.HealthComponent>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= HandleDeath;
            }
        }

        #endregion

        #region 掉落逻辑

        /// <summary>
        /// 死亡时尝试生成掉落物
        /// </summary>
        private void HandleDeath()
        {
            if (_lootTable == null)
            {
                Debug.LogWarning($"[LootDropper] {gameObject.name} 没有配置 LootTable");
                return;
            }

            // Roll 掉落
            var equipment = _lootTable.Roll();
            if (equipment == null)
            {
                Debug.Log($"[LootDropper] {gameObject.name} 本次没有掉落");
                return;
            }

            // 生成掉落物实体
            SpawnDrop(equipment);
        }

        /// <summary>
        /// 在世界中生成掉落物 GameObject
        /// </summary>
        private void SpawnDrop(EquipmentInstance equipment)
        {
            // 计算生成位置（带随机偏移）
            Vector3 spawnPos = transform.position;
            spawnPos.y += _spawnYOffset;
            spawnPos.x += Random.Range(-_spreadRange, _spreadRange);

            GameObject dropGO;

            if (_dropPickupPrefab != null)
            {
                dropGO = Instantiate(_dropPickupPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // 没有配 Prefab 时创建一个简易的
                dropGO = CreateDefaultDropGO(spawnPos);
            }

            // 注入装备数据
            var pickup = dropGO.GetComponent<DropPickup>();
            if (pickup == null)
            {
                pickup = dropGO.AddComponent<DropPickup>();
            }
            pickup.Init(equipment);

            Debug.Log($"[LootDropper] {gameObject.name} 掉落了 " +
                      $"[{equipment.Template.Rarity}] {equipment.Template.DisplayName}");
        }

        /// <summary>
        /// 创建默认的掉落物（没有配 Prefab 时的兜底方案）
        /// </summary>
        private GameObject CreateDefaultDropGO(Vector3 position)
        {
            var go = new GameObject("DropPickup");
            go.transform.position = position;

            // 添加精灵渲染器（用默认白色方块代替）
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDefaultSprite();
            sr.sortingOrder = 100; // 显示在最上层

            // 添加触发碰撞体
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.8f;
            col.isTrigger = true;

            return go;
        }

        /// <summary>
        /// 创建一个简单的默认精灵（4x4 白色纹理）
        /// </summary>
        private Sprite CreateDefaultSprite()
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        }

        #endregion
    }
}
