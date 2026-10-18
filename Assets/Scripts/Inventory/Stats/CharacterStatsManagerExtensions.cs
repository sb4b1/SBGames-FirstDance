using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SBGames.Inventory.Stats
{
    /// <summary>
    /// Provides extension methods for CharacterStatsManager to resolve Stat instances from StatType enums.
    /// Maps primary core stats directly to CharacterStatsManager properties (MaxLife, Damage, Movespeed, AttackTimeInterval),
    /// while providing an automatic dynamic fallback registry for supplementary stats (Defense, CritChance, etc.).
    /// Uses ConditionalWeakTable so supplementary stats are garbage collected alongside their CharacterStatsManager.
    /// </summary>
    public static class CharacterStatsManagerExtensions
    {
        private static readonly ConditionalWeakTable<CharacterStatsManager, Dictionary<StatType, Stat>> ExtraStatsRegistry = new();

        /// <summary>
        /// Retrieves the Stat instance for the specified StatType on this CharacterStatsManager.
        /// </summary>
        public static Stat GetStat(this CharacterStatsManager manager, StatType type)
        {
            if (manager == null) return null;

            // Direct mapping to explicit properties on CharacterStatsManager
            switch (type)
            {
                case StatType.MaxHealth:
                case StatType.Health:
                    return manager.MaxLife;

                case StatType.Attack:
                    return manager.Damage;

                case StatType.MoveSpeed:
                    return manager.Movespeed;

                case StatType.AttackSpeed:
                    return manager.AttackTimeInterval;

                case StatType.LifeRegen:
                    return manager.LifeRegen;

                case StatType.ManaRegen:
                    return manager.ManaRegen;
            }

            // Dynamic fallback for any additional StatType (Defense, CritChance, ArmorPenetration, etc.)
            var extraTable = ExtraStatsRegistry.GetOrCreateValue(manager);
            if (!extraTable.TryGetValue(type, out var stat))
            {
                stat = new Stat(0f);
                extraTable[type] = stat;
            }

            return stat;
        }

        /// <summary>
        /// Retrieves the calculated float value of a stat for the specified StatType.
        /// </summary>
        public static float GetStatValue(this CharacterStatsManager manager, StatType type)
        {
            var stat = manager.GetStat(type);
            return stat != null ? stat.Value : 0f;
        }

        /// <summary>
        /// Retrieves all active stats (both mapped explicit stats and registered dynamic stats) with their current values.
        /// </summary>
        public static Dictionary<StatType, float> GetAllStatValues(this CharacterStatsManager manager)
        {
            var dict = new Dictionary<StatType, float>();
            if (manager == null) return dict;

            dict[StatType.MaxHealth] = manager.MaxLife.Value;
            dict[StatType.Attack] = manager.Damage.Value;
            dict[StatType.MoveSpeed] = manager.Movespeed.Value;
            dict[StatType.AttackSpeed] = manager.AttackTimeInterval.Value;
            dict[StatType.LifeRegen] = manager.LifeRegen.Value;
            dict[StatType.ManaRegen] = manager.ManaRegen.Value;

            if (ExtraStatsRegistry.TryGetValue(manager, out var extraTable))
            {
                foreach (var kvp in extraTable)
                {
                    dict[kvp.Key] = kvp.Value.Value;
                }
            }

            return dict;
        }
    }
}
