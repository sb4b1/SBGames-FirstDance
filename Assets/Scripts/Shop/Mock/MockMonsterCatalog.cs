using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Database;
using SBGames.Loop;
using SBGames.Shop.Data;
using UnityEngine;

namespace SBGames.Shop.Mock
{
    /// <summary>
    /// Production-ready mock monster catalog populated with balanced archetypes across all ranks (E through S).
    /// Provides immediate standalone testing for the shop system without external database or asset dependencies.
    /// </summary>
    public class MockMonsterCatalog : IMonsterCatalog
    {
        private readonly Dictionary<DungeonRank, List<MonsterTemplate>> _rankBuckets;
        private readonly Dictionary<string, MonsterTemplate> _archetypeLookup;

        public int TotalArchetypeCount => _archetypeLookup.Count;

        public MockMonsterCatalog()
        {
            _rankBuckets = new Dictionary<DungeonRank, List<MonsterTemplate>>();
            _archetypeLookup = new Dictionary<string, MonsterTemplate>(StringComparer.OrdinalIgnoreCase);

            foreach (DungeonRank rank in Enum.GetValues(typeof(DungeonRank)))
            {
                _rankBuckets[rank] = new List<MonsterTemplate>();
            }

            PopulateDefaultCatalog();
        }

        /// <inheritdoc />
        public IReadOnlyList<MonsterTemplate> GetTemplatesForRank(DungeonRank rank)
        {
            if (_rankBuckets.TryGetValue(rank, out var list))
            {
                return list;
            }
            return Array.Empty<MonsterTemplate>();
        }

        /// <inheritdoc />
        public MonsterTemplate GetTemplateById(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;
            _archetypeLookup.TryGetValue(archetypeId, out var template);
            return template;
        }

        /// <inheritdoc />
        public bool HasTemplatesForRank(DungeonRank rank)
        {
            return _rankBuckets.TryGetValue(rank, out var list) && list.Count > 0;
        }

        /// <summary>
        /// Registers a new template into the catalog dynamically.
        /// </summary>
        public void RegisterTemplate(MonsterTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            _archetypeLookup[template.ArchetypeId] = template;

            var bucket = _rankBuckets[template.BaseRank];
            int existingIndex = bucket.FindIndex(t => t.ArchetypeId.Equals(template.ArchetypeId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                bucket[existingIndex] = template;
            }
            else
            {
                bucket.Add(template);
            }
        }

        /// <summary>
        /// Imports monster entries from a CharacterDatabaseSO into this catalog.
        /// </summary>
        public void ImportFromCharacterDatabase(CharacterDatabaseSO characterDb, DungeonRank defaultRank = DungeonRank.D, int defaultCost = 3)
        {
            if (characterDb == null || characterDb.Characters == null) return;

            foreach (var entry in characterDb.Characters)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;

                float maxHealth = entry.baseStats != null ? entry.baseStats.MaxLife : 100f;
                float damage = entry.baseStats != null ? entry.baseStats.Damage : 10f;
                float attackInterval = entry.baseStats != null ? entry.baseStats.AttackTimeInterval : 1.0f;
                float moveSpeed = entry.baseStats != null ? entry.baseStats.Movespeed : 5.0f;
                float defense = 2f;
                GameObject prefab = entry.GetPrefabDirect();
                string name = string.IsNullOrWhiteSpace(entry.characterName) ? entry.id : entry.characterName;

                var template = new MonsterTemplate(
                    archetypeId: entry.id,
                    displayName: name,
                    baseRank: defaultRank,
                    baseCost: defaultCost,
                    baseMaxHealth: maxHealth,
                    baseDamage: damage,
                    attackInterval: attackInterval,
                    moveSpeed: moveSpeed,
                    baseDefense: defense,
                    prefab: prefab
                );

                RegisterTemplate(template);
            }
        }

        private void PopulateDefaultCatalog()
        {
            // Rank E (Cost: 2)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "goblin_scout",
                displayName: "Goblin Scout",
                baseRank: DungeonRank.E,
                baseCost: 2,
                baseMaxHealth: 80f,
                baseDamage: 8f,
                attackInterval: 0.9f,
                moveSpeed: 3.6f,
                baseDefense: 1f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "slime_green",
                displayName: "Green Slime",
                baseRank: DungeonRank.E,
                baseCost: 2,
                baseMaxHealth: 120f,
                baseDamage: 5f,
                attackInterval: 1.2f,
                moveSpeed: 2.4f,
                baseDefense: 3f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "bat_cave",
                displayName: "Cave Bat",
                baseRank: DungeonRank.E,
                baseCost: 2,
                baseMaxHealth: 60f,
                baseDamage: 7f,
                attackInterval: 0.7f,
                moveSpeed: 4.5f,
                baseDefense: 0f
            ));

            // Rank D (Cost: 3)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "MONSTER_N001",
                displayName: "MONSTER_N001",
                baseRank: DungeonRank.D,
                baseCost: 3,
                baseMaxHealth: 100f,
                baseDamage: 10f,
                attackInterval: 1.0f,
                moveSpeed: 5.0f,
                baseDefense: 2f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "MONSTER_N002",
                displayName: "MONSTER_N002",
                baseRank: DungeonRank.D,
                baseCost: 3,
                baseMaxHealth: 100f,
                baseDamage: 10f,
                attackInterval: 1.0f,
                moveSpeed: 5.0f,
                baseDefense: 2f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "skeleton_warrior",
                displayName: "Skeleton Warrior",
                baseRank: DungeonRank.D,
                baseCost: 3,
                baseMaxHealth: 180f,
                baseDamage: 14f,
                attackInterval: 1.0f,
                moveSpeed: 3.0f,
                baseDefense: 4f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "dire_wolf",
                displayName: "Dire Wolf",
                baseRank: DungeonRank.D,
                baseCost: 3,
                baseMaxHealth: 150f,
                baseDamage: 18f,
                attackInterval: 0.8f,
                moveSpeed: 4.2f,
                baseDefense: 2f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "undead_archer",
                displayName: "Undead Archer",
                baseRank: DungeonRank.D,
                baseCost: 3,
                baseMaxHealth: 130f,
                baseDamage: 16f,
                attackInterval: 1.1f,
                moveSpeed: 3.2f,
                baseDefense: 2f
            ));

            // Rank C (Cost: 5)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "hobgoblin_brute",
                displayName: "Hobgoblin Brute",
                baseRank: DungeonRank.C,
                baseCost: 5,
                baseMaxHealth: 320f,
                baseDamage: 25f,
                attackInterval: 1.2f,
                moveSpeed: 2.8f,
                baseDefense: 8f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "orc_berserker",
                displayName: "Orc Berserker",
                baseRank: DungeonRank.C,
                baseCost: 5,
                baseMaxHealth: 290f,
                baseDamage: 32f,
                attackInterval: 0.9f,
                moveSpeed: 3.4f,
                baseDefense: 5f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "dark_sorcerer",
                displayName: "Dark Sorcerer",
                baseRank: DungeonRank.C,
                baseCost: 5,
                baseMaxHealth: 220f,
                baseDamage: 38f,
                attackInterval: 1.4f,
                moveSpeed: 3.0f,
                baseDefense: 3f
            ));

            // Rank B (Cost: 8)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "stone_golem",
                displayName: "Stone Golem",
                baseRank: DungeonRank.B,
                baseCost: 8,
                baseMaxHealth: 650f,
                baseDamage: 45f,
                attackInterval: 1.5f,
                moveSpeed: 2.2f,
                baseDefense: 18f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "wyvern_drake",
                displayName: "Wyvern Drake",
                baseRank: DungeonRank.B,
                baseCost: 8,
                baseMaxHealth: 500f,
                baseDamage: 55f,
                attackInterval: 1.0f,
                moveSpeed: 4.0f,
                baseDefense: 12f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "shadow_assassin",
                displayName: "Shadow Assassin",
                baseRank: DungeonRank.B,
                baseCost: 8,
                baseMaxHealth: 420f,
                baseDamage: 68f,
                attackInterval: 0.7f,
                moveSpeed: 4.8f,
                baseDefense: 8f
            ));

            // Rank A (Cost: 12)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "death_knight",
                displayName: "Death Knight",
                baseRank: DungeonRank.A,
                baseCost: 12,
                baseMaxHealth: 1100f,
                baseDamage: 90f,
                attackInterval: 1.1f,
                moveSpeed: 3.2f,
                baseDefense: 25f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "abyssal_demon",
                displayName: "Abyssal Demon",
                baseRank: DungeonRank.A,
                baseCost: 12,
                baseMaxHealth: 950f,
                baseDamage: 115f,
                attackInterval: 0.95f,
                moveSpeed: 3.6f,
                baseDefense: 20f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "venom_hydra",
                displayName: "Venom Hydra",
                baseRank: DungeonRank.A,
                baseCost: 12,
                baseMaxHealth: 1300f,
                baseDamage: 85f,
                attackInterval: 1.2f,
                moveSpeed: 2.6f,
                baseDefense: 22f
            ));

            // Rank S (Cost: 18)
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "ancient_red_dragon",
                displayName: "Ancient Red Dragon",
                baseRank: DungeonRank.S,
                baseCost: 18,
                baseMaxHealth: 2500f,
                baseDamage: 220f,
                attackInterval: 1.3f,
                moveSpeed: 3.8f,
                baseDefense: 45f
            ));
            RegisterTemplate(new MonsterTemplate(
                archetypeId: "void_monarch",
                displayName: "Void Monarch",
                baseRank: DungeonRank.S,
                baseCost: 18,
                baseMaxHealth: 2100f,
                baseDamage: 260f,
                attackInterval: 0.9f,
                moveSpeed: 4.0f,
                baseDefense: 40f
            ));
        }
    }
}
