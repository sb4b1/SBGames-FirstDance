using System;
using SBGames.Loop;
using SBGames.Personality;
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

        [Header("Personality & Psychological Traits")]
        [SerializeField] private SBGames.Personality.Data.RaceInstinctSO raceInstinct;
        [SerializeField] private SBGames.Personality.Data.CharacterTraitSO activeTrait;

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
        public SBGames.Personality.Data.RaceInstinctSO RaceInstinct => raceInstinct;
        public SBGames.Personality.Data.CharacterTraitSO ActiveTrait => activeTrait;

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

            // Sync personality if item has race or trait assigned
            if (item.RaceInstinct != null || item.Trait != null)
            {
                SetPersonality(item.RaceInstinct, item.Trait);
            }
            else
            {
                // Check if CharacterPersonality is already present on prefab
                var pers = GetComponent<CharacterPersonality>();
                if (pers != null)
                {
                    raceInstinct = pers.RaceInstinct;
                    activeTrait = pers.ActiveTrait;
                }
            }
        }

        public void SetPersonality(SBGames.Personality.Data.RaceInstinctSO race, SBGames.Personality.Data.CharacterTraitSO trait)
        {
            raceInstinct = race;
            activeTrait = trait;

            var pers = GetComponent<CharacterPersonality>();
            if (pers == null) pers = gameObject.AddComponent<CharacterPersonality>();
            pers.Initialize(race, trait);
        }

        public void UpdateTrait(SBGames.Personality.Data.CharacterTraitSO newTrait)
        {
            activeTrait = newTrait;
            var pers = GetComponent<CharacterPersonality>();
            if (pers != null)
            {
                pers.SetTrait(newTrait);
            }
        }
    }
}
