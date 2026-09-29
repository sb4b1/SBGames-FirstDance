using System;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Contract representing the combat and attribute metrics of a monster within a dungeon.
    /// Provides a decoupled interface consumed by difficulty scaling and combat power calculations.
    /// </summary>
    public interface IMonsterStats
    {
        /// <summary>
        /// Unique identifier or definition archetype ID of the monster.
        /// </summary>
        string MonsterId { get; }

        /// <summary>
        /// User-facing display name of the monster.
        /// </summary>
        string MonsterName { get; }

        /// <summary>
        /// Monster level or growth tier (minimum 1).
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Maximum health / vital points of the monster.
        /// </summary>
        float MaxHealth { get; }

        /// <summary>
        /// Base damage dealt per attack.
        /// </summary>
        float Damage { get; }

        /// <summary>
        /// Time in seconds between consecutive attacks (e.g. 1.0 = 1 attack/sec, 0.5 = 2 attacks/sec).
        /// </summary>
        float AttackInterval { get; }

        /// <summary>
        /// Movement speed in world units per second.
        /// </summary>
        float MoveSpeed { get; }

        /// <summary>
        /// Defense or physical armor rating mitigating incoming damage.
        /// </summary>
        float Defense { get; }

        /// <summary>
        /// Count of active flat or percentage modifiers currently affecting this monster.
        /// </summary>
        int ActiveModifierCount { get; }

        /// <summary>
        /// Sum of equipped artifact or rune power bonuses attached to this monster.
        /// </summary>
        int EquippedArtifactPower { get; }
    }
}
