using System;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Represents the six adventurer and dungeon gate ranks in progression order.
    /// E is the lowest entry rank, and S is the pinnacle legendary rank.
    /// </summary>
    public enum DungeonRank
    {
        E = 0,
        D = 1,
        C = 2,
        B = 3,
        A = 4,
        S = 5
    }

    /// <summary>
    /// Extension methods and utility helpers for the <see cref="DungeonRank"/> enumeration.
    /// </summary>
    public static class DungeonRankExtensions
    {
        /// <summary>
        /// Total number of defined ranks in the progression hierarchy.
        /// </summary>
        public const int RankCount = 6;

        /// <summary>
        /// Minimum rank in the progression hierarchy (E).
        /// </summary>
        public const DungeonRank MinRank = DungeonRank.E;

        /// <summary>
        /// Maximum rank in the progression hierarchy (S).
        /// </summary>
        public const DungeonRank MaxRank = DungeonRank.S;

        /// <summary>
        /// Returns the next higher rank, clamped to <see cref="MaxRank"/> (S).
        /// </summary>
        /// <param name="rank">The current rank.</param>
        /// <returns>The subsequent rank in progression order, or S if already at maximum.</returns>
        public static DungeonRank GetNextRank(this DungeonRank rank)
        {
            int next = (int)rank + 1;
            return (DungeonRank)Mathf.Clamp(next, (int)MinRank, (int)MaxRank);
        }

        /// <summary>
        /// Returns the previous lower rank, clamped to <see cref="MinRank"/> (E).
        /// </summary>
        /// <param name="rank">The current rank.</param>
        /// <returns>The preceding rank in progression order, or E if already at minimum.</returns>
        public static DungeonRank GetPreviousRank(this DungeonRank rank)
        {
            int prev = (int)rank - 1;
            return (DungeonRank)Mathf.Clamp(prev, (int)MinRank, (int)MaxRank);
        }

        /// <summary>
        /// Calculates the signed rank distance between this rank and a target rank.
        /// Positive value indicates other is higher rank; negative indicates other is lower.
        /// </summary>
        /// <param name="current">The reference rank.</param>
        /// <param name="other">The target rank to compare against.</param>
        /// <returns>The signed distance (other - current).</returns>
        public static int RankDifference(this DungeonRank current, DungeonRank other)
        {
            return (int)other - (int)current;
        }

        /// <summary>
        /// Returns a formatted display name for the rank (e.g. "Rank E", "Rank S").
        /// </summary>
        /// <param name="rank">The rank to format.</param>
        /// <returns>User-friendly display string.</returns>
        public static string GetDisplayName(this DungeonRank rank)
        {
            return $"Rank {rank}";
        }

        /// <summary>
        /// Returns a UI color code string corresponding to the rank's thematic rarity.
        /// </summary>
        /// <param name="rank">The rank to evaluate.</param>
        /// <returns>Hexadecimal color code string with leading '#' symbol.</returns>
        public static string GetColorHex(this DungeonRank rank)
        {
            return rank switch
            {
                DungeonRank.E => "#9E9E9E", // Grey
                DungeonRank.D => "#4CAF50", // Green
                DungeonRank.C => "#2196F3", // Blue
                DungeonRank.B => "#9C27B0", // Purple
                DungeonRank.A => "#FF9800", // Orange
                DungeonRank.S => "#E91E63", // Radiant Magenta / Red
                _ => "#FFFFFF"
            };
        }

        /// <summary>
        /// Returns the base combat power baseline associated with this rank tier.
        /// </summary>
        /// <param name="rank">The rank to evaluate.</param>
        /// <returns>Estimated baseline power rating for an adventurer of this rank.</returns>
        public static int GetBasePowerBaseline(this DungeonRank rank)
        {
            return rank switch
            {
                DungeonRank.E => 100,
                DungeonRank.D => 250,
                DungeonRank.C => 550,
                DungeonRank.B => 1100,
                DungeonRank.A => 2000,
                DungeonRank.S => 3500,
                _ => 100
            };
        }
    }
}
