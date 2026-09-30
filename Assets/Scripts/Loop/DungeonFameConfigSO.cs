using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Configuration data for a specific Fame Tier.
    /// </summary>
    [Serializable]
    public class FameTierDefinition
    {
        [Tooltip("The Fame Tier category.")]
        public DungeonFameTier Tier = DungeonFameTier.Unknown;

        [Tooltip("Consecutive win-streak required to reach this tier.")]
        [Min(0)] public int RequiredWinStreak = 0;

        [Tooltip("Custom display title for the tier. If empty, default title is used.")]
        public string TitleOverride = "";

        [Header("Attraction Probability Bonuses (Added to raw weight)")]
        [Tooltip("Additional probability weight for +1 Rank adventurers.")]
        [Range(0f, 2.0f)] public float OneRankHigherBonusWeight = 0f;

        [Tooltip("Additional probability weight for +2 Ranks higher adventurers.")]
        [Range(0f, 2.0f)] public float TwoRanksHigherBonusWeight = 0f;

        [Tooltip("Additional probability weight for +3 or S-Rank adventurers.")]
        [Range(0f, 2.0f)] public float ThreePlusRanksHigherBonusWeight = 0f;

        [Header("Victory Rewards & Modifiers")]
        [Tooltip("Multiplier applied to base energy reward upon defending the round.")]
        [Range(1.0f, 5.0f)] public float EnergyRewardMultiplier = 1.0f;

        [Tooltip("Multiplier applied to progression XP awarded upon defending the round.")]
        [Range(1.0f, 5.0f)] public float XpRewardMultiplier = 1.0f;

        [Tooltip("Probability [0.0 to 1.0] of dropping a rare artifact when defeating higher-rank adventurers.")]
        [Range(0f, 1.0f)] public float RareArtifactDropChance = 0f;
    }

    /// <summary>
    /// Template for candidate rare artifact rewards awarded when defending against high-fame challengers.
    /// </summary>
    [Serializable]
    public class RareArtifactDropTemplate
    {
        public string ArtifactName = "Hero's Tainted Relic";
        public int PowerLevel = 2;
        public int EssenceCost = 0;
        public bool StartEmpowered = false;
        public DungeonRank MinimumAdventurerRank = DungeonRank.D;
    }

    /// <summary>
    /// ScriptableObject defining fame progression thresholds, probability modulation,
    /// reward scaling, and bonus loot drop tables for the Fame system.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonFameConfig", menuName = "SBGames/Progression/Dungeon Fame Config", order = 26)]
    public class DungeonFameConfigSO : ScriptableObject
    {
        [Header("Fame Tier Definitions (Ordered Tier 0 to 5)")]
        [SerializeField] private List<FameTierDefinition> tiers = new List<FameTierDefinition>();

        [Header("Breach / Defeat Risk Scaling")]
        [Tooltip("Additional percentage essence damage inflicted per adventurer rank delta above the dungeon rank when breached.")]
        [SerializeField, Range(0f, 1.0f)] private float breachEssencePenaltyPerRankDelta = 0.25f;

        [Header("Candidate Artifact Loot Pool")]
        [Tooltip("Artifacts that can drop upon winning against higher-rank adventurer parties under high fame.")]
        [SerializeField] private List<RareArtifactDropTemplate> artifactLootPool = new List<RareArtifactDropTemplate>();

        public float BreachEssencePenaltyPerRankDelta => breachEssencePenaltyPerRankDelta;
        public IReadOnlyList<RareArtifactDropTemplate> ArtifactLootPool => artifactLootPool;

        private void OnEnable()
        {
            EnsureDefaultTiers();
            EnsureDefaultLootPool();
        }

        public void EnsureDefaultTiers()
        {
            if (tiers != null && tiers.Count == DungeonFameTierExtensions.TierCount) return;

            tiers = new List<FameTierDefinition>
            {
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.Unknown,
                    RequiredWinStreak = 0,
                    OneRankHigherBonusWeight = 0.0f,
                    TwoRanksHigherBonusWeight = 0.0f,
                    ThreePlusRanksHigherBonusWeight = 0.0f,
                    EnergyRewardMultiplier = 1.0f,
                    XpRewardMultiplier = 1.0f,
                    RareArtifactDropChance = 0.0f
                },
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.Rumored,
                    RequiredWinStreak = 2,
                    OneRankHigherBonusWeight = 0.10f,
                    TwoRanksHigherBonusWeight = 0.02f,
                    ThreePlusRanksHigherBonusWeight = 0.0f,
                    EnergyRewardMultiplier = 1.15f,
                    XpRewardMultiplier = 1.20f,
                    RareArtifactDropChance = 0.0f
                },
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.Feared,
                    RequiredWinStreak = 4,
                    OneRankHigherBonusWeight = 0.25f,
                    TwoRanksHigherBonusWeight = 0.08f,
                    ThreePlusRanksHigherBonusWeight = 0.02f,
                    EnergyRewardMultiplier = 1.35f,
                    XpRewardMultiplier = 1.50f,
                    RareArtifactDropChance = 0.15f
                },
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.Infamous,
                    RequiredWinStreak = 7,
                    OneRankHigherBonusWeight = 0.45f,
                    TwoRanksHigherBonusWeight = 0.20f,
                    ThreePlusRanksHigherBonusWeight = 0.06f,
                    EnergyRewardMultiplier = 1.60f,
                    XpRewardMultiplier = 1.80f,
                    RareArtifactDropChance = 0.25f
                },
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.Legendary,
                    RequiredWinStreak = 10,
                    OneRankHigherBonusWeight = 0.70f,
                    TwoRanksHigherBonusWeight = 0.40f,
                    ThreePlusRanksHigherBonusWeight = 0.15f,
                    EnergyRewardMultiplier = 2.00f,
                    XpRewardMultiplier = 2.20f,
                    RareArtifactDropChance = 0.40f
                },
                new FameTierDefinition
                {
                    Tier = DungeonFameTier.MythicCalamity,
                    RequiredWinStreak = 15,
                    OneRankHigherBonusWeight = 1.00f,
                    TwoRanksHigherBonusWeight = 0.75f,
                    ThreePlusRanksHigherBonusWeight = 0.40f,
                    EnergyRewardMultiplier = 2.50f,
                    XpRewardMultiplier = 3.00f,
                    RareArtifactDropChance = 0.60f
                }
            };
        }

        public void EnsureDefaultLootPool()
        {
            if (artifactLootPool != null && artifactLootPool.Count > 0) return;

            artifactLootPool = new List<RareArtifactDropTemplate>
            {
                new RareArtifactDropTemplate
                {
                    ArtifactName = "Challenger's Broken Insignia",
                    PowerLevel = 2,
                    EssenceCost = 0,
                    StartEmpowered = false,
                    MinimumAdventurerRank = DungeonRank.D
                },
                new RareArtifactDropTemplate
                {
                    ArtifactName = "Vanquished Champion's Crest",
                    PowerLevel = 3,
                    EssenceCost = 0,
                    StartEmpowered = false,
                    MinimumAdventurerRank = DungeonRank.C
                },
                new RareArtifactDropTemplate
                {
                    ArtifactName = "Grand Inquisitor's Soul Relic",
                    PowerLevel = 5,
                    EssenceCost = 0,
                    StartEmpowered = true,
                    MinimumAdventurerRank = DungeonRank.B
                },
                new RareArtifactDropTemplate
                {
                    ArtifactName = "Hero King's Shattered Aegis",
                    PowerLevel = 8,
                    EssenceCost = 0,
                    StartEmpowered = true,
                    MinimumAdventurerRank = DungeonRank.A
                }
            };
        }

        /// <summary>
        /// Retrieves the configuration definition for a specific fame tier.
        /// </summary>
        public FameTierDefinition GetDefinition(DungeonFameTier tier)
        {
            EnsureDefaultTiers();
            int index = (int)tier;
            if (index >= 0 && index < tiers.Count)
            {
                return tiers[index];
            }
            return tiers[0];
        }

        /// <summary>
        /// Evaluates which fame tier corresponds to a given undefeated win-streak.
        /// </summary>
        public DungeonFameTier EvaluateTierFromStreak(int winStreak)
        {
            EnsureDefaultTiers();
            DungeonFameTier highestUnlocked = DungeonFameTier.Unknown;

            for (int i = 0; i < tiers.Count; i++)
            {
                if (winStreak >= tiers[i].RequiredWinStreak)
                {
                    highestUnlocked = tiers[i].Tier;
                }
            }

            return highestUnlocked;
        }
    }
}
