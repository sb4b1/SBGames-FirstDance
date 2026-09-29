using System;
using SBGames.Loop;
using SBGames.Shop.Data;
using UnityEngine;

namespace SBGames.Shop.Unity
{
    /// <summary>
    /// Component attached to monster GameObjects spawned onto the dungeon grid from the Monster Shop.
    /// Stores the shop item metadata, rank, level, and combat stats.
    /// Synchronizes stats with <see cref="CharacterStatsManager"/> or <see cref="CharacterBrain"/> if present.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterRuntimeInstance : MonoBehaviour
    {
        [Header("Shop Metadata (Read-Only)")]
        [SerializeField] private string archetypeId;
        [SerializeField] private DungeonRank rank;
        [SerializeField] private int level = 1;
        [SerializeField] private int purchaseCost;

        [Header("Runtime Combat Stats")]
        [SerializeField] private float maxHealth;
        [SerializeField] private float currentHealth;
        [SerializeField] private float damage;
        [SerializeField] private float defense;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float attackInterval;

        private MonsterShopItem _shopItem;

        public MonsterShopItem ShopItem => _shopItem;
        public string ArchetypeId => archetypeId;
        public DungeonRank Rank => rank;
        public int Level => level;
        public int PurchaseCost => purchaseCost;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float Damage => damage;
        public float Defense => defense;
        public float MoveSpeed => moveSpeed;
        public float AttackInterval => attackInterval;

        /// <summary>
        /// Initializes the monster instance with data rolled from the shop.
        /// </summary>
        public void Initialize(MonsterShopItem item)
        {
            _shopItem = item ?? throw new ArgumentNullException(nameof(item));
            archetypeId = item.ArchetypeId;
            rank = item.Rank;
            level = item.Level;
            purchaseCost = item.Cost;

            var stats = item.Stats;
            maxHealth = stats.MaxHealth;
            currentHealth = stats.MaxHealth;
            damage = stats.Damage;
            defense = stats.Defense;
            moveSpeed = stats.MoveSpeed;
            attackInterval = stats.AttackInterval;

            gameObject.name = $"{item.DisplayName} (Lvl {item.Level})";

            // If CharacterStatsManager exists on the prefab, sync base values
            var statsManager = GetComponent<CharacterStatsManager>();
            if (statsManager != null)
            {
                statsManager.SetLevel(level);
                if (statsManager.MaxLife != null) statsManager.MaxLife.BaseValue = maxHealth;
                if (statsManager.Damage != null) statsManager.Damage.BaseValue = damage;
                if (statsManager.Movespeed != null) statsManager.Movespeed.BaseValue = moveSpeed;
                if (statsManager.AttackTimeInterval != null) statsManager.AttackTimeInterval.BaseValue = attackInterval;
            }
        }
    }
}
