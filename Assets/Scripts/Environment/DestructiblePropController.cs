using System;
using SBGames.Inventory.Data;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Environment
{
    /// <summary>
    /// Controller for interactive destructible world objects (Chests, Crates, Barrels, Training Props).
    /// Implements IDamageable to respond to attacks, swap visuals on hit/break, and spawn loot drops.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class DestructiblePropController : MonoBehaviour, IDamageable
    {
        [Header("Prop Identity")]
        [SerializeField] private string propName = "Wooden Crate";

        [Header("Health & Durability")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [SerializeField] private float currentHealth = 30f;
        [SerializeField] private bool isInvulnerable = false;

        [Header("Visuals & States")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite intactSprite;
        [SerializeField] private Sprite brokenOrOpenSprite;
        [SerializeField] private Animator animator;
        [SerializeField] private string breakTriggerName = "Break";

        [Header("Loot & Drops")]
        [Tooltip("Optional prefab spawned at prop position upon destruction.")]
        [SerializeField] private GameObject dropPrefab;
        [Tooltip("Optional ItemDataSO definition to instantiate as a world drop.")]
        [SerializeField] private ItemDataSO dropItemData;
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;

        [Header("Colliders")]
        [SerializeField] private Collider2D propCollider;
        [SerializeField] private bool disableColliderOnBreak = true;

        [Header("Events")]
        public UnityEvent<float, float> onHealthChanged = new();
        public UnityEvent onDamaged = new();
        public UnityEvent onDestroyedOrOpened = new();

        public bool IsDestroyed { get; private set; } = false;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            if (propCollider == null) propCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            currentHealth = maxHealth;
            if (spriteRenderer != null && intactSprite != null)
            {
                spriteRenderer.sprite = intactSprite;
            }
        }

        public bool TakeDamage(in DamageData damage)
        {
            if (IsDestroyed || isInvulnerable) return false;

            currentHealth = Mathf.Max(0f, currentHealth - damage.Amount);
            onHealthChanged?.Invoke(currentHealth, maxHealth);
            onDamaged?.Invoke();

            if (currentHealth <= 0f)
            {
                BreakOrOpen();
                return true;
            }

            return false;
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(new DamageData(damage, DamageType.Physical));
        }

        public void ForceDeath()
        {
            if (IsDestroyed) return;
            currentHealth = 0f;
            BreakOrOpen();
        }

        public void BreakOrOpen()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;

            if (spriteRenderer != null && brokenOrOpenSprite != null)
            {
                spriteRenderer.sprite = brokenOrOpenSprite;
            }

            if (animator != null && !string.IsNullOrEmpty(breakTriggerName))
            {
                animator.SetTrigger(breakTriggerName);
            }

            if (disableColliderOnBreak && propCollider != null)
            {
                propCollider.enabled = false;
            }

            SpawnLoot();
            onDestroyedOrOpened?.Invoke();
        }

        private void SpawnLoot()
        {
            if (UnityEngine.Random.value > dropChance) return;

            if (dropPrefab != null)
            {
                Instantiate(dropPrefab, transform.position, Quaternion.identity);
            }
            else if (dropItemData != null && dropItemData.Icon != null)
            {
                // Create basic world drop instance if prefab is not set
                var lootGo = new GameObject($"Drop_{dropItemData.ItemID}");
                lootGo.transform.position = transform.position;
                var sr = lootGo.AddComponent<SpriteRenderer>();
                sr.sprite = dropItemData.Icon;
                sr.sortingLayerName = "Item";
            }
        }
    }
}
