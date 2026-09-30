using System;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Represents the tier of renown and notoriety the dungeon has accumulated
    /// through consecutive undefeated round victories.
    /// </summary>
    public enum DungeonFameTier
    {
        Unknown = 0,       // Tier 0: 0-1 wins - Obscure hideout
        Rumored = 1,       // Tier 1: 2-3 wins - Whispers among novices
        Feared = 2,        // Tier 2: 4-6 wins - Regional threat
        Infamous = 3,      // Tier 3: 7-9 wins - Renowned death-trap
        Legendary = 4,     // Tier 4: 10-14 wins - Continental legend
        MythicCalamity = 5 // Tier 5: 15+ wins - Realm-shaking menace
    }

    /// <summary>
    /// Extension methods and formatting utilities for <see cref="DungeonFameTier"/>.
    /// </summary>
    public static class DungeonFameTierExtensions
    {
        /// <summary>
        /// Total number of distinct Fame Tiers.
        /// </summary>
        public const int TierCount = 6;

        /// <summary>
        /// Maximum possible Fame Tier.
        /// </summary>
        public const DungeonFameTier MaxTier = DungeonFameTier.MythicCalamity;

        /// <summary>
        /// Returns a thematic title string for the fame tier.
        /// </summary>
        public static string GetTitle(this DungeonFameTier tier)
        {
            return tier switch
            {
                DungeonFameTier.Unknown => "Obscure Hideout",
                DungeonFameTier.Rumored => "Rumored Cavern",
                DungeonFameTier.Feared => "Feared Crypt",
                DungeonFameTier.Infamous => "Infamous Death-Trap",
                DungeonFameTier.Legendary => "Legendary Dread-Citadel",
                DungeonFameTier.MythicCalamity => "Mythic Domain of Calamity",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Returns a formatted tier label (e.g. "Fame Tier 3 - Infamous Death-Trap").
        /// </summary>
        public static string GetFormattedLabel(this DungeonFameTier tier)
        {
            return $"Fame Tier {(int)tier} ({tier.GetTitle()})";
        }

        /// <summary>
        /// Returns a theme color hex code for the fame tier.
        /// </summary>
        public static string GetColorHex(this DungeonFameTier tier)
        {
            return tier switch
            {
                DungeonFameTier.Unknown => "#9E9E9E",        // Neutral Grey
                DungeonFameTier.Rumored => "#8BC34A",        // Light Green
                DungeonFameTier.Feared => "#00BCD4",         // Cyan
                DungeonFameTier.Infamous => "#AB47BC",       // Vivid Purple
                DungeonFameTier.Legendary => "#FF9800",      // Radiant Orange / Amber
                DungeonFameTier.MythicCalamity => "#F44336", // Fiery Crimson / Red
                _ => "#FFFFFF"
            };
        }

        /// <summary>
        /// Returns the minimum win-streak count required to unlock this fame tier.
        /// </summary>
        public static int GetDefaultThreshold(this DungeonFameTier tier)
        {
            return tier switch
            {
                DungeonFameTier.Unknown => 0,
                DungeonFameTier.Rumored => 2,
                DungeonFameTier.Feared => 4,
                DungeonFameTier.Infamous => 7,
                DungeonFameTier.Legendary => 10,
                DungeonFameTier.MythicCalamity => 15,
                _ => 0
            };
        }

        /// <summary>
        /// Evaluates the matching fame tier from an active win-streak count using baseline thresholds.
        /// </summary>
        public static DungeonFameTier FromWinStreak(int streak)
        {
            if (streak >= 15) return DungeonFameTier.MythicCalamity;
            if (streak >= 10) return DungeonFameTier.Legendary;
            if (streak >= 7) return DungeonFameTier.Infamous;
            if (streak >= 4) return DungeonFameTier.Feared;
            if (streak >= 2) return DungeonFameTier.Rumored;
            return DungeonFameTier.Unknown;
        }
    }
}
