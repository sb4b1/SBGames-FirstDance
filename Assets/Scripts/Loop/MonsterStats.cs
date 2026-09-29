using System;
using SBGames.Inventory.Stats;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Pure data model representing the combat attributes and power metrics of a monster unit.
    /// Supports direct instantiation, Inspector serialization, and builder chaining.
    /// </summary>
    [Serializable]
    public class MonsterStats : IMonsterStats
    {
        [SerializeField] private string monsterId = "monster_default";
        [SerializeField] private string monsterName = "Default Monster";
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1.0f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float defense = 5f;
        [SerializeField, Min(0)] private int activeModifierCount = 0;
        [SerializeField, Min(0)] private int equippedArtifactPower = 0;

        /// <inheritdoc />
        public string MonsterId => monsterId;

        /// <inheritdoc />
        public string MonsterName => monsterName;

        /// <inheritdoc />
        public int Level => level;

        /// <inheritdoc />
        public float MaxHealth => maxHealth;

        /// <inheritdoc />
        public float Damage => damage;

        /// <inheritdoc />
        public float AttackInterval => attackInterval;

        /// <inheritdoc />
        public float MoveSpeed => moveSpeed;

        /// <inheritdoc />
        public float Defense => defense;

        /// <inheritdoc />
        public int ActiveModifierCount => activeModifierCount;

        /// <inheritdoc />
        public int EquippedArtifactPower => equippedArtifactPower;

        /// <summary>
        /// Parameterless constructor for serialization and reflection.
        /// </summary>
        public MonsterStats()
        {
        }

        /// <summary>
        /// Comprehensive constructor to configure a complete monster stats instance.
        /// </summary>
        public MonsterStats(
            string monsterId,
            string monsterName,
            int level = 1,
            float maxHealth = 100f,
            float damage = 10f,
            float attackInterval = 1.0f,
            float moveSpeed = 3.5f,
            float defense = 5f,
            int activeModifierCount = 0,
            int equippedArtifactPower = 0)
        {
            this.monsterId = string.IsNullOrEmpty(monsterId) ? Guid.NewGuid().ToString() : monsterId;
            this.monsterName = string.IsNullOrEmpty(monsterName) ? "Unnamed Monster" : monsterName;
            this.level = Mathf.Max(1, level);
            this.maxHealth = Mathf.Max(1f, maxHealth);
            this.damage = Mathf.Max(0f, damage);
            this.attackInterval = Mathf.Max(0.01f, attackInterval);
            this.moveSpeed = Mathf.Max(0f, moveSpeed);
            this.defense = Mathf.Max(0f, defense);
            this.activeModifierCount = Mathf.Max(0, activeModifierCount);
            this.equippedArtifactPower = Mathf.Max(0, equippedArtifactPower);
        }

        /// <summary>
        /// Factory helper to generate standard rank-tiered monster stats presets.
        /// </summary>
        /// <param name="rank">The target dungeon rank.</param>
        /// <param name="monsterName">Optional custom name.</param>
        /// <returns>A balanced <see cref="MonsterStats"/> instance configured for the specified rank tier.</returns>
        public static MonsterStats CreateForRank(DungeonRank rank, string monsterName = null)
        {
            int lvl = (int)rank * 5 + 1;
            float hp = 100f + ((int)rank * 150f);
            float dmg = 10f + ((int)rank * 12f);
            float def = 2f + ((int)rank * 6f);
            float atkInt = Mathf.Max(0.4f, 1.2f - ((int)rank * 0.12f));
            string name = monsterName ?? $"Rank {rank} Guardian";

            return new MonsterStats(
                monsterId: $"monster_{rank}_{Guid.NewGuid().ToString().Substring(0, 6)}",
                monsterName: name,
                level: lvl,
                maxHealth: hp,
                damage: dmg,
                attackInterval: atkInt,
                moveSpeed: 3.5f,
                defense: def,
                activeModifierCount: (int)rank,
                equippedArtifactPower: (int)rank * 10
            );
        }

        /// <summary>
        /// Extracts monster stats from an existing <see cref="CharacterCombatPower"/> component.
        /// </summary>
        /// <param name="combatPower">The CharacterCombatPower component to inspect.</param>
        /// <returns>A populated <see cref="MonsterStats"/> instance reflecting the component's live state.</returns>
        public static MonsterStats FromCharacterCombatPower(CharacterCombatPower combatPower)
        {
            if (combatPower == null)
            {
                throw new ArgumentNullException(nameof(combatPower), "[MonsterStats] CharacterCombatPower reference cannot be null.");
            }

            var statsManager = combatPower.StatsManager;
            int lvl = combatPower.Level;
            int totalMods = combatPower.TotalActiveModifiers;
            int artifactPwr = combatPower.HasEquippedArtifact ? 40 : 0;
            artifactPwr += combatPower.InscribedRuneCount * 15;

            float hp = statsManager != null && statsManager.MaxLife != null ? statsManager.MaxLife.Value : 100f;
            float dmg = statsManager != null && statsManager.Damage != null ? statsManager.Damage.Value : 10f;
            float spd = statsManager != null && statsManager.Movespeed != null ? statsManager.Movespeed.Value : 3.5f;
            float interval = statsManager != null && statsManager.AttackTimeInterval != null && statsManager.AttackTimeInterval.Value > 0.01f
                ? statsManager.AttackTimeInterval.Value
                : 1.0f;
            float def = statsManager != null ? statsManager.GetStatValue(StatType.Defense) : 0f;

            string id = combatPower.gameObject.name;
            string name = combatPower.gameObject.name;

            return new MonsterStats(
                monsterId: id,
                monsterName: name,
                level: lvl,
                maxHealth: hp,
                damage: dmg,
                attackInterval: interval,
                moveSpeed: spd,
                defense: def,
                activeModifierCount: totalMods,
                equippedArtifactPower: artifactPwr
            );
        }

        /// <summary>
        /// Extracts monster stats from a Unity GameObject by searching for <see cref="CharacterCombatPower"/>
        /// or <see cref="CharacterStatsManager"/> components.
        /// </summary>
        /// <param name="target">The GameObject to inspect.</param>
        /// <returns>A populated <see cref="MonsterStats"/> instance, or a baseline instance if no components found.</returns>
        public static MonsterStats FromGameObject(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "[MonsterStats] GameObject target cannot be null.");
            }

            var combatPower = target.GetComponentInChildren<CharacterCombatPower>();
            if (combatPower != null)
            {
                return FromCharacterCombatPower(combatPower);
            }

            var statsManager = target.GetComponentInChildren<CharacterStatsManager>();
            if (statsManager != null)
            {
                float hp = statsManager.MaxLife != null ? statsManager.MaxLife.Value : 100f;
                float dmg = statsManager.Damage != null ? statsManager.Damage.Value : 10f;
                float spd = statsManager.Movespeed != null ? statsManager.Movespeed.Value : 3.5f;
                float interval = statsManager.AttackTimeInterval != null && statsManager.AttackTimeInterval.Value > 0.01f
                    ? statsManager.AttackTimeInterval.Value
                    : 1.0f;
                float def = statsManager.GetStatValue(StatType.Defense);

                return new MonsterStats(
                    monsterId: target.name,
                    monsterName: target.name,
                    level: statsManager.Level,
                    maxHealth: hp,
                    damage: dmg,
                    attackInterval: interval,
                    moveSpeed: spd,
                    defense: def,
                    activeModifierCount: 0,
                    equippedArtifactPower: 0
                );
            }

            // Fallback generic baseline
            return new MonsterStats(
                monsterId: target.name,
                monsterName: target.name,
                level: 1,
                maxHealth: 100f,
                damage: 10f,
                attackInterval: 1.0f,
                moveSpeed: 3.5f,
                defense: 0f,
                activeModifierCount: 0,
                equippedArtifactPower: 0
            );
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{MonsterName} (Lvl {Level})] HP: {MaxHealth:F0}, DMG: {Damage:F1}, ATK_INT: {AttackInterval:F2}s, DEF: {Defense:F1}, MODS: {ActiveModifierCount}, ART_PWR: {EquippedArtifactPower}";
        }
    }
}
