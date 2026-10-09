using System;
using System.Collections.Generic;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Shop.Core
{
    /// <summary>
    /// Configuration for shop mechanics, slot counts, reroll pricing, and dynamic probability curves.
    /// Supports both code-based instantiation and Inspector customization.
    /// </summary>
    [Serializable]
    public class ShopConfig
    {
        public const int DefaultSlotCount = 5;
        public const int DefaultRerollCost = 2;
        public const float DefaultLevelCostMultiplier = 0.5f;

        [SerializeField, Range(1, 10)] private int slotCount = DefaultSlotCount;
        [SerializeField, Min(0)] private int manualRerollCost = DefaultRerollCost;
        [SerializeField, Min(0f)] private float levelCostMultiplier = DefaultLevelCostMultiplier;

        public int SlotCount
        {
            get => slotCount;
            set => slotCount = Mathf.Clamp(value, 1, 10);
        }

        public int ManualRerollCost
        {
            get => manualRerollCost;
            set => manualRerollCost = Mathf.Max(0, value);
        }

        public float LevelCostMultiplier
        {
            get => levelCostMultiplier;
            set => levelCostMultiplier = Mathf.Max(0f, value);
        }

        public ShopConfig(
            int slotCount = DefaultSlotCount,
            int manualRerollCost = DefaultRerollCost,
            float levelCostMultiplier = DefaultLevelCostMultiplier)
        {
            SlotCount = slotCount;
            ManualRerollCost = manualRerollCost;
            LevelCostMultiplier = levelCostMultiplier;
        }

        private static readonly Dictionary<DungeonRank, float>[] CachedRankProbabilities;
        private static readonly Dictionary<int, float>[] CachedLevelProbabilities;

        static ShopConfig()
        {
            // Matrix: [Tier 0..5][Rank E..S]
            float[,] oddsMatrix = new float[,]
            {
                // Tier 0 (Early - Round 1-3, Rank E): Basic monsters
                { 75f, 25f,  0f,  0f,  0f,  0f },
                // Tier 1 (Round 4-6, Rank D): Intro to uncommon
                { 45f, 40f, 15f,  0f,  0f,  0f },
                // Tier 2 (Round 7-9, Rank C): Balanced mid-game
                { 25f, 35f, 30f, 10f,  0f,  0f },
                // Tier 3 (Round 10-12, Rank B): Advanced monsters appearing
                { 15f, 25f, 35f, 20f,  5f,  0f },
                // Tier 4 (Round 13-15, Rank A): High tier power
                {  5f, 15f, 30f, 30f, 15f,  5f },
                // Tier 5 (Round 16+, Rank S): Endgame / legendary tier
                {  2f,  8f, 20f, 30f, 25f, 15f }
            };

            CachedRankProbabilities = new Dictionary<DungeonRank, float>[6];
            for (int t = 0; t < 6; t++)
            {
                CachedRankProbabilities[t] = new Dictionary<DungeonRank, float>
                {
                    [DungeonRank.E] = oddsMatrix[t, 0],
                    [DungeonRank.D] = oddsMatrix[t, 1],
                    [DungeonRank.C] = oddsMatrix[t, 2],
                    [DungeonRank.B] = oddsMatrix[t, 3],
                    [DungeonRank.A] = oddsMatrix[t, 4],
                    [DungeonRank.S] = oddsMatrix[t, 5]
                };
            }

            CachedLevelProbabilities = new Dictionary<int, float>[]
            {
                new() { [1] = 95f, [2] = 5f,  [3] = 0f },
                new() { [1] = 85f, [2] = 12f, [3] = 3f },
                new() { [1] = 75f, [2] = 18f, [3] = 7f },
                new() { [1] = 65f, [2] = 23f, [3] = 10f, [4] = 2f },
                new() { [1] = 55f, [2] = 25f, [3] = 14f, [4] = 5f, [5] = 1f }
            };
        }

        /// <summary>
        /// Calculates rank probability distribution weights (E to S) based on the current round number and DungeonRank tier.
        /// Weights represent relative percentages summing to 100%.
        /// </summary>
        /// <param name="roundNumber">Current game round (1-indexed).</param>
        /// <param name="playerRank">Current progression rank tier of the dungeon/player.</param>
        /// <returns>Dictionary mapping each DungeonRank to its roll weight.</returns>
        public virtual IReadOnlyDictionary<DungeonRank, float> GetRankProbabilities(int roundNumber, DungeonRank playerRank)
        {
            roundNumber = Mathf.Max(1, roundNumber);

            int roundTier = Mathf.Clamp((roundNumber - 1) / 3, 0, 5);
            int rankTier = (int)playerRank;
            int effectiveTier = Mathf.Clamp(Mathf.Max(roundTier, rankTier), 0, 5);

            return CachedRankProbabilities[effectiveTier];
        }

        /// <summary>
        /// Calculates spawn level probability weights based on round number and progression rank.
        /// Monsters mostly spawn at Level 1, with a chance to roll Level 2, 3, or up to 5 at later rounds.
        /// </summary>
        /// <param name="roundNumber">Current game round (1-indexed).</param>
        /// <param name="playerRank">Current progression rank.</param>
        /// <returns>Dictionary mapping spawn level to its roll weight.</returns>
        public virtual IReadOnlyDictionary<int, float> GetLevelProbabilities(int roundNumber, DungeonRank playerRank)
        {
            roundNumber = Mathf.Max(1, roundNumber);
            int tier = Mathf.Clamp((roundNumber - 1) / 4 + (int)playerRank / 2, 0, 4);

            return CachedLevelProbabilities[tier];
        }
    }
}
