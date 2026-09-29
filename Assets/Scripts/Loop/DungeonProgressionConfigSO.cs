using System;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Configuration ScriptableObject defining probability tables, XP progression thresholds,
    /// and advancement mechanics for the dungeon progression system.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonProgressionConfig", menuName = "SBGames/Progression/Dungeon Progression Config", order = 25)]
    public class DungeonProgressionConfigSO : ScriptableObject
    {
        [Header("Cross-Rank Attempt Probabilities (Relative Distance)")]
        [Tooltip("Probability weight for adventurers of the matching dungeon rank (Delta = 0). Primary target.")]
        [SerializeField, Range(0.01f, 1.0f)] private float sameRankProbability = 0.70f;

        [Tooltip("Probability weight for adventurers 1 rank HIGHER attempting this lower dungeon (Delta = +1).")]
        [SerializeField, Range(0.0f, 1.0f)] private float oneRankHigherProbability = 0.15f;

        [Tooltip("Probability weight for adventurers 2 ranks HIGHER attempting this lower dungeon (Delta = +2).")]
        [SerializeField, Range(0.0f, 1.0f)] private float twoRanksHigherProbability = 0.04f;

        [Tooltip("Probability weight for adventurers 3+ ranks HIGHER attempting this lower dungeon (Delta >= +3).")]
        [SerializeField, Range(0.0f, 1.0f)] private float threePlusRanksHigherProbability = 0.01f;

        [Tooltip("Probability weight for adventurers 1 rank LOWER attempting this higher dungeon (Delta = -1).")]
        [SerializeField, Range(0.0f, 1.0f)] private float oneRankLowerProbability = 0.07f;

        [Tooltip("Probability weight for adventurers 2 ranks LOWER attempting this higher dungeon (Delta = -2).")]
        [SerializeField, Range(0.0f, 1.0f)] private float twoRanksLowerProbability = 0.02f;

        [Tooltip("Probability weight for adventurers 3+ ranks LOWER attempting this higher dungeon (Delta <= -3).")]
        [SerializeField, Range(0.0f, 1.0f)] private float threePlusRanksLowerProbability = 0.01f;

        [Header("Promotion Progression Points (XP Thresholds)")]
        [Tooltip("Progression points required to advance from Rank E to Rank D.")]
        [SerializeField, Min(10)] private int rankEToDPoints = 100;

        [Tooltip("Progression points required to advance from Rank D to Rank C.")]
        [SerializeField, Min(10)] private int rankDToCPoints = 250;

        [Tooltip("Progression points required to advance from Rank C to Rank B.")]
        [SerializeField, Min(10)] private int rankCToBPoints = 500;

        [Tooltip("Progression points required to advance from Rank B to Rank A.")]
        [SerializeField, Min(10)] private int rankBToAPoints = 1000;

        [Tooltip("Progression points required to advance from Rank A to Rank S.")]
        [SerializeField, Min(10)] private int rankAToSPoints = 2500;

        [Header("XP Rewards Per Defeated Adventurer Rank")]
        [Tooltip("Base progression points awarded for successfully defending against Rank E adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankE = 20;

        [Tooltip("Base progression points awarded for successfully defending against Rank D adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankD = 50;

        [Tooltip("Base progression points awarded for successfully defending against Rank C adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankC = 120;

        [Tooltip("Base progression points awarded for successfully defending against Rank B adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankB = 260;

        [Tooltip("Base progression points awarded for successfully defending against Rank A adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankA = 550;

        [Tooltip("Base progression points awarded for successfully defending against Rank S adventurers.")]
        [SerializeField, Min(1)] private int baseRewardRankS = 1200;

        [Header("Advancement & Streak Mechanics")]
        [Tooltip("Whether the dungeon automatically ranks up upon reaching the required XP threshold.")]
        [SerializeField] private bool autoPromoteOnThresholdMet = true;

        [Tooltip("Bonus XP multiplier applied per active win streak (e.g. 0.05 = +5% XP per consecutive win, capped at max streak).")]
        [SerializeField, Range(0f, 0.5f)] private float winStreakBonusMultiplier = 0.05f;

        [Tooltip("Maximum win streak count factored into the XP bonus multiplier.")]
        [SerializeField, Min(1)] private int maxWinStreakBonusCap = 10;

        #region Public Getters

        public float SameRankProbability => sameRankProbability;
        public float OneRankHigherProbability => oneRankHigherProbability;
        public float TwoRanksHigherProbability => twoRanksHigherProbability;
        public float ThreePlusRanksHigherProbability => threePlusRanksHigherProbability;
        public float OneRankLowerProbability => oneRankLowerProbability;
        public float TwoRanksLowerProbability => twoRanksLowerProbability;
        public float ThreePlusRanksLowerProbability => threePlusRanksLowerProbability;

        public bool AutoPromoteOnThresholdMet => autoPromoteOnThresholdMet;
        public float WinStreakBonusMultiplier => winStreakBonusMultiplier;
        public int MaxWinStreakBonusCap => maxWinStreakBonusCap;

        #endregion

        /// <summary>
        /// Retrieves the raw relative probability weight for a given signed rank delta (AdventurerRank - DungeonRank).
        /// </summary>
        /// <param name="rankDelta">Signed rank difference (positive = adventurer is higher; negative = adventurer is lower).</param>
        /// <returns>Non-negative probability weight for this relative distance.</returns>
        public float GetRawWeightForDelta(int rankDelta)
        {
            if (rankDelta == 0) return sameRankProbability;
            if (rankDelta == 1) return oneRankHigherProbability;
            if (rankDelta == 2) return twoRanksHigherProbability;
            if (rankDelta >= 3) return threePlusRanksHigherProbability;
            if (rankDelta == -1) return oneRankLowerProbability;
            if (rankDelta == -2) return twoRanksLowerProbability;
            return threePlusRanksLowerProbability; // rankDelta <= -3
        }

        /// <summary>
        /// Returns the target progression points required to advance from the given rank to the next tier.
        /// </summary>
        /// <param name="rank">Current dungeon rank.</param>
        /// <returns>Points threshold, or 0 if rank is already max (S).</returns>
        public int GetRequiredPointsForNextRank(DungeonRank rank)
        {
            return rank switch
            {
                DungeonRank.E => rankEToDPoints,
                DungeonRank.D => rankDToCPoints,
                DungeonRank.C => rankCToBPoints,
                DungeonRank.B => rankBToAPoints,
                DungeonRank.A => rankAToSPoints,
                DungeonRank.S => 0, // Max rank reached
                _ => 0
            };
        }

        /// <summary>
        /// Returns the base progression XP granted when successfully defeating an adventurer party of the specified rank.
        /// </summary>
        /// <param name="adventurerRank">Rank of the defeated adventurer party.</param>
        /// <returns>Base progression points awarded.</returns>
        public int GetBaseXpForAdventurerRank(DungeonRank adventurerRank)
        {
            return adventurerRank switch
            {
                DungeonRank.E => baseRewardRankE,
                DungeonRank.D => baseRewardRankD,
                DungeonRank.C => baseRewardRankC,
                DungeonRank.B => baseRewardRankB,
                DungeonRank.A => baseRewardRankA,
                DungeonRank.S => baseRewardRankS,
                _ => baseRewardRankE
            };
        }
    }
}
