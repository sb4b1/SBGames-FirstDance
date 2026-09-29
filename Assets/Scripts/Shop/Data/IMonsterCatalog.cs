using System.Collections.Generic;
using SBGames.Loop;

namespace SBGames.Shop.Data
{
    /// <summary>
    /// Contract providing access to monster templates categorized by progression rank tier.
    /// Decouples the shop generation engine from the underlying storage mechanism (mock data, ScriptableObjects, Addressables, etc.).
    /// </summary>
    public interface IMonsterCatalog
    {
        /// <summary>
        /// Retrieves all monster templates registered for a specific rank tier.
        /// </summary>
        IReadOnlyList<MonsterTemplate> GetTemplatesForRank(DungeonRank rank);

        /// <summary>
        /// Retrieves a monster template by its unique archetype identifier.
        /// </summary>
        MonsterTemplate GetTemplateById(string archetypeId);

        /// <summary>
        /// Checks if there are any available templates for a specified rank.
        /// </summary>
        bool HasTemplatesForRank(DungeonRank rank);

        /// <summary>
        /// Total number of archetypes across all ranks.
        /// </summary>
        int TotalArchetypeCount { get; }
    }
}
