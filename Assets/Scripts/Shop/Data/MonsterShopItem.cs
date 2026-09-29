using System;
using SBGames.Loop;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Shop.Data
{
    /// <summary>
    /// Runtime instance of a monster currently offered for purchase in a shop slot.
    /// Encapsulates rank, rolled level, calculated purchase cost, and concrete combat attributes.
    /// Implements <see cref="IUnitPlacementDefinition"/> for direct, zero-overhead integration with <see cref="DungeonPlacementSystem"/>.
    /// </summary>
    [Serializable]
    public class MonsterShopItem : IUnitPlacementDefinition
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string archetypeId;
        [SerializeField] private string displayName;
        [SerializeField] private DungeonRank rank;
        [SerializeField] private int level;
        [SerializeField] private int cost;
        [SerializeField] private int baseCapacity;
        [SerializeField] private Vector2Int footprint;
        [SerializeField] private GameObject prefab;
        [SerializeField] private MonsterStats stats;

        /// <summary>
        /// Unique runtime identifier for this generated shop offer instance.
        /// </summary>
        public string InstanceId => instanceId;

        /// <summary>
        /// ID of the archetype blueprint.
        /// </summary>
        public string ArchetypeId => archetypeId;

        /// <summary>
        /// User-facing display name.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Progression rank tier (E through S).
        /// </summary>
        public DungeonRank Rank => rank;

        /// <summary>
        /// Spawning level of the monster (minimum 1).
        /// </summary>
        public int Level => level;

        /// <summary>
        /// Cost in currency/energy required to purchase and summon this monster.
        /// </summary>
        public int Cost => cost;

        /// <summary>
        /// Base dungeon capacity consumed by this unit.
        /// </summary>
        public int BaseCapacity => baseCapacity;

        /// <summary>
        /// Spatial tile footprint on the grid.
        /// </summary>
        public Vector2Int Footprint => footprint.x < 1 || footprint.y < 1 ? Vector2Int.one : footprint;

        /// <summary>
        /// Prefab to instantiate when placed on the grid.
        /// </summary>
        public GameObject Prefab => prefab;

        /// <summary>
        /// Combat and attribute statistics scaled to the monster's rank and level.
        /// </summary>
        public IMonsterStats Stats => stats;

        #region IUnitPlacementDefinition Implementation

        string IUnitPlacementDefinition.UnitId => instanceId;
        string IUnitPlacementDefinition.DisplayName => displayName;
        int IUnitPlacementDefinition.BaseCost => cost;
        int IUnitPlacementDefinition.BaseCapacity => baseCapacity;
        Vector2Int IUnitPlacementDefinition.Footprint => Footprint;
        GameObject IUnitPlacementDefinition.Prefab => prefab;

        #endregion

        public MonsterShopItem(
            string instanceId,
            string archetypeId,
            string displayName,
            DungeonRank rank,
            int level,
            int cost,
            int baseCapacity,
            Vector2Int footprint,
            GameObject prefab,
            MonsterStats stats)
        {
            this.instanceId = string.IsNullOrEmpty(instanceId) ? Guid.NewGuid().ToString() : instanceId;
            this.archetypeId = archetypeId ?? throw new ArgumentNullException(nameof(archetypeId));
            this.displayName = string.IsNullOrEmpty(displayName) ? archetypeId : displayName;
            this.rank = rank;
            this.level = Mathf.Max(1, level);
            this.cost = Mathf.Max(1, cost);
            this.baseCapacity = Mathf.Max(1, baseCapacity);
            this.footprint = footprint.x < 1 || footprint.y < 1 ? Vector2Int.one : footprint;
            this.prefab = prefab;
            this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        /// <summary>
        /// Factory helper to construct a scaled shop item from a template and target level.
        /// </summary>
        public static MonsterShopItem CreateFromTemplate(
            MonsterTemplate template,
            int level,
            float levelCostMultiplier = 0.5f)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            level = Mathf.Max(1, level);

            // Stat growth curve per level (e.g. +20% HP and DMG per level above 1)
            float levelStatMultiplier = 1f + ((level - 1) * 0.20f);
            float scaledHp = template.BaseMaxHealth * levelStatMultiplier;
            float scaledDmg = template.BaseDamage * levelStatMultiplier;
            float scaledDef = template.BaseDefense + ((level - 1) * 2f);

            // Cost formula: baseCost + floor(baseCost * (level - 1) * levelCostMultiplier)
            int extraCost = Mathf.FloorToInt(template.BaseCost * (level - 1) * levelCostMultiplier);
            int finalCost = template.BaseCost + extraCost;

            var generatedStats = new MonsterStats(
                monsterId: $"{template.ArchetypeId}_L{level}",
                monsterName: level > 1 ? $"{template.DisplayName} +{level - 1}" : template.DisplayName,
                level: level,
                maxHealth: scaledHp,
                damage: scaledDmg,
                attackInterval: template.AttackInterval,
                moveSpeed: template.MoveSpeed,
                defense: scaledDef,
                activeModifierCount: (int)template.BaseRank,
                equippedArtifactPower: (int)template.BaseRank * 10
            );

            return new MonsterShopItem(
                instanceId: Guid.NewGuid().ToString(),
                archetypeId: template.ArchetypeId,
                displayName: template.DisplayName,
                rank: template.BaseRank,
                level: level,
                cost: finalCost,
                baseCapacity: template.BaseCapacity,
                footprint: template.Footprint,
                prefab: template.Prefab,
                stats: generatedStats
            );
        }

        public override string ToString()
        {
            return $"[{Rank}] {DisplayName} (Lvl {Level}) - Cost: {Cost} | HP: {Stats.MaxHealth:F0}, ATK: {Stats.Damage:F1}, DEF: {Stats.Defense:F1}";
        }
    }
}
