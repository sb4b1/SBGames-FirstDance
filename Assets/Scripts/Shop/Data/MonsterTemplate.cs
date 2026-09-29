using System;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Shop.Data
{
    /// <summary>
    /// Static definition template for a monster archetype within the shop catalog.
    /// Acts as the blueprint from which runtime <see cref="MonsterShopItem"/> instances are spawned.
    /// </summary>
    [Serializable]
    public class MonsterTemplate
    {
        [SerializeField] private string archetypeId;
        [SerializeField] private string displayName;
        [SerializeField] private DungeonRank baseRank;
        [SerializeField] private int baseCost;
        [SerializeField] private int baseCapacity;
        [SerializeField] private Vector2Int footprint;
        [SerializeField] private float baseMaxHealth;
        [SerializeField] private float baseDamage;
        [SerializeField] private float attackInterval;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float baseDefense;
        [SerializeField] private GameObject prefab;

        public string ArchetypeId => archetypeId;
        public string DisplayName => displayName;
        public DungeonRank BaseRank => baseRank;
        public int BaseCost => baseCost;
        public int BaseCapacity => baseCapacity;
        public Vector2Int Footprint => footprint.x < 1 || footprint.y < 1 ? Vector2Int.one : footprint;
        public float BaseMaxHealth => baseMaxHealth;
        public float BaseDamage => baseDamage;
        public float AttackInterval => attackInterval;
        public float MoveSpeed => moveSpeed;
        public float BaseDefense => baseDefense;
        public GameObject Prefab => prefab;

        public MonsterTemplate(
            string archetypeId,
            string displayName,
            DungeonRank baseRank,
            int baseCost,
            float baseMaxHealth,
            float baseDamage,
            float attackInterval = 1.0f,
            float moveSpeed = 3.5f,
            float baseDefense = 0f,
            int baseCapacity = 1,
            Vector2Int? footprint = null,
            GameObject prefab = null)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
                throw new ArgumentException("Archetype ID cannot be null or empty.", nameof(archetypeId));

            this.archetypeId = archetypeId;
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? archetypeId : displayName;
            this.baseRank = baseRank;
            this.baseCost = Mathf.Max(1, baseCost);
            this.baseCapacity = Mathf.Max(1, baseCapacity);
            this.footprint = footprint ?? Vector2Int.one;
            this.baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            this.baseDamage = Mathf.Max(0f, baseDamage);
            this.attackInterval = Mathf.Max(0.05f, attackInterval);
            this.moveSpeed = Mathf.Max(0.1f, moveSpeed);
            this.baseDefense = Mathf.Max(0f, baseDefense);
            this.prefab = prefab;
        }
    }
}
