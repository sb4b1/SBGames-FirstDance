using System;
using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Tracks historical progression statistics for a specific dungeon rank.
    /// </summary>
    [Serializable]
    public class RankProgressionStats
    {
        [SerializeField] private DungeonRank rank;
        [SerializeField] private int totalAttempts;
        [SerializeField] private int totalVictories;
        [SerializeField] private int totalDefeats;

        public DungeonRank Rank => rank;
        public int TotalAttempts => totalAttempts;
        public int TotalVictories => totalVictories;
        public int TotalDefeats => totalDefeats;
        public float WinRate => totalAttempts > 0 ? (float)totalVictories / totalAttempts : 0f;

        public RankProgressionStats(DungeonRank rank)
        {
            this.rank = rank;
            this.totalAttempts = 0;
            this.totalVictories = 0;
            this.totalDefeats = 0;
        }

        public void RecordOutcome(bool isVictory)
        {
            totalAttempts++;
            if (isVictory)
            {
                totalVictories++;
            }
            else
            {
                totalDefeats++;
            }
        }

        public void Reset()
        {
            totalAttempts = 0;
            totalVictories = 0;
            totalDefeats = 0;
        }
    }

    /// <summary>
    /// Manages the round-based dungeon progression lifecycle, gate ranking tiers (E through S),
    /// adventurer attempt selection with cross-rank probability distributions, and progression point accumulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonProgression : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Optional ScriptableObject configuration asset. If null, built-in baseline values are used.")]
        [SerializeField] private DungeonProgressionConfigSO customConfig;

        [Header("Runtime State (Read Only)")]
        [Tooltip("Current gate/dungeon rank.")]
        [SerializeField, ReadOnly] private DungeonRank currentRank = DungeonRank.E;

        [Tooltip("Progression points accumulated towards the next gate rank.")]
        [SerializeField, ReadOnly] private int currentProgressionPoints = 0;

        [Tooltip("Adventurer rank currently challenging the dungeon for the active round.")]
        [SerializeField, ReadOnly] private DungeonRank currentAdventurerAttemptRank = DungeonRank.E;

        [Tooltip("Total dungeon defense attempts across all rounds and ranks.")]
        [SerializeField, ReadOnly] private int totalAttempts = 0;

        [Tooltip("Total successful dungeon defenses.")]
        [SerializeField, ReadOnly] private int totalVictories = 0;

        [Tooltip("Total dungeon breaches/losses.")]
        [SerializeField, ReadOnly] private int totalDefeats = 0;

        [Tooltip("Current active consecutive victory streak.")]
        [SerializeField, ReadOnly] private int currentWinStreak = 0;

        [Tooltip("Highest consecutive victory streak achieved in this run.")]
        [SerializeField, ReadOnly] private int highestWinStreak = 0;

        [Header("Optional Loop & Fame Integration")]
        [Tooltip("Optional reference to CoreGameLoop to automatically trigger attempt selection on round start.")]
        [SerializeField] private CoreGameLoop coreGameLoop;

        [Tooltip("Optional reference to DungeonFameSystem to apply fame-driven probability modulation and reward scaling.")]
        [SerializeField] private DungeonFameSystem fameSystem;

        // Per-rank stats lookup table
        private readonly Dictionary<DungeonRank, RankProgressionStats> _rankStats = new();

        #region Public Properties

        /// <summary>The active dungeon rank tier.</summary>
        public DungeonRank CurrentRank => currentRank;

        /// <summary>Optional reference to the active DungeonFameSystem.</summary>
        public DungeonFameSystem FameSystem { get => fameSystem; set => fameSystem = value; }

        /// <summary>Current adventurer rank attempting this dungeon round.</summary>
        public DungeonRank CurrentAdventurerAttemptRank => currentAdventurerAttemptRank;

        /// <summary>Current progression points towards the next rank.</summary>
        public int CurrentProgressionPoints => currentProgressionPoints;

        /// <summary>Target progression points required to advance to the next rank (0 if already at Rank S).</summary>
        public int RequiredPointsForNextRank => GetConfig().GetRequiredPointsForNextRank(currentRank);

        /// <summary>Normalized progress towards next rank [0.0 to 1.0].</summary>
        public float ProgressNormalized
        {
            get
            {
                int req = RequiredPointsForNextRank;
                if (req <= 0) return 1.0f;
                return Mathf.Clamp01((float)currentProgressionPoints / req);
            }
        }

        /// <summary>Total dungeon defense attempts across all ranks.</summary>
        public int TotalAttempts => totalAttempts;

        /// <summary>Total successful dungeon defenses.</summary>
        public int TotalVictories => totalVictories;

        /// <summary>Total defeats/breaches suffered.</summary>
        public int TotalDefeats => totalDefeats;

        /// <summary>Current consecutive victory streak.</summary>
        public int CurrentWinStreak => currentWinStreak;

        /// <summary>Highest consecutive victory streak recorded.</summary>
        public int HighestWinStreak => highestWinStreak;

        /// <summary>Overall defense win rate across all ranks [0.0 to 1.0].</summary>
        public float OverallWinRate => totalAttempts > 0 ? (float)totalVictories / totalAttempts : 0f;

        /// <summary>Whether the dungeon has reached the maximum gate tier (Rank S).</summary>
        public bool IsMaxRank => currentRank == DungeonRankExtensions.MaxRank;

        /// <summary>Active configuration asset or fallback instance.</summary>
        public DungeonProgressionConfigSO Config => customConfig;

        #endregion

        #region Events

        /// <summary>Fired when the dungeon rank changes or advances (oldRank, newRank).</summary>
        public event Action<DungeonRank, DungeonRank> OnRankChanged;

        /// <summary>Fired when an adventurer attempt is rolled (dungeonRank, adventurerRank).</summary>
        public event Action<DungeonRank, DungeonRank> OnAdventurerAttemptDetermined;

        /// <summary>Fired when progression points are awarded (pointsGained, currentPoints, requiredPoints).</summary>
        public event Action<int, int, int> OnProgressionPointsGained;

        /// <summary>Fired when a rank promotion occurs (newRank).</summary>
        public event Action<DungeonRank> OnRankPromoted;

        #endregion

        private void Awake()
        {
            InitializeRankStats();
            if (coreGameLoop == null)
            {
                coreGameLoop = GetComponent<CoreGameLoop>() ?? FindAnyObjectByType<CoreGameLoop>();
            }
            if (fameSystem == null)
            {
                fameSystem = GetComponent<DungeonFameSystem>() ?? FindAnyObjectByType<DungeonFameSystem>();
            }
        }

        private void OnEnable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted += HandleRoundStarted;
                coreGameLoop.OnRoundCompleted += HandleRoundCompleted;
            }
        }

        private void OnDisable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted -= HandleRoundStarted;
                coreGameLoop.OnRoundCompleted -= HandleRoundCompleted;
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime setups and headless unit testing.
        /// </summary>
        /// <param name="startingRank">The initial gate rank.</param>
        /// <param name="config">Optional configuration ScriptableObject.</param>
        /// <param name="loop">Optional CoreGameLoop reference.</param>
        /// <param name="fame">Optional DungeonFameSystem reference.</param>
        public void Initialize(
            DungeonRank startingRank = DungeonRank.E,
            DungeonProgressionConfigSO config = null,
            CoreGameLoop loop = null,
            DungeonFameSystem fame = null)
        {
            InitializeRankStats();

            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted -= HandleRoundStarted;
                coreGameLoop.OnRoundCompleted -= HandleRoundCompleted;
            }

            customConfig = config;
            coreGameLoop = loop;
            fameSystem = fame;

            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted += HandleRoundStarted;
                coreGameLoop.OnRoundCompleted += HandleRoundCompleted;
            }

            currentRank = startingRank;
            currentProgressionPoints = 0;
            currentAdventurerAttemptRank = startingRank;
            totalAttempts = 0;
            totalVictories = 0;
            totalDefeats = 0;
            currentWinStreak = 0;
            highestWinStreak = 0;
        }

        /// <summary>
        /// Sets a custom configuration asset dynamically at runtime.
        /// </summary>
        /// <param name="config">The new configuration ScriptableObject.</param>
        public void SetConfig(DungeonProgressionConfigSO config)
        {
            customConfig = config;
        }

        /// <summary>
        /// Sets or replaces the active DungeonFameSystem reference at runtime.
        /// </summary>
        /// <param name="fame">The new DungeonFameSystem instance.</param>
        public void SetFameSystem(DungeonFameSystem fame)
        {
            fameSystem = fame;
        }

        #region Cross-Rank Probability Engine

        private readonly float[] _cachedProbabilities = new float[DungeonRankExtensions.RankCount];

        /// <summary>
        /// Populates the provided float array with normalized probability distribution for all six ranks.
        /// Zero allocations when passing a pre-allocated array.
        /// </summary>
        public void GetNormalizedProbabilities(DungeonRank dungeonRank, float[] probabilities)
        {
            if (probabilities == null || probabilities.Length < DungeonRankExtensions.RankCount) return;

            var config = GetConfig();
            float totalWeight = 0f;

            // 1. Accumulate raw relative weights for each rank and apply fame boosts
            for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
            {
                var candidateRank = (DungeonRank)r;
                int delta = (int)candidateRank - (int)dungeonRank;
                float rawWeight = config.GetRawWeightForDelta(delta);

                if (fameSystem != null)
                {
                    rawWeight += fameSystem.GetProbabilityBonusForDelta(delta);
                }

                probabilities[r] = Mathf.Max(0f, rawWeight);
                totalWeight += probabilities[r];
            }

            // 2. Normalize so sum equals exactly 1.0 (with fallback to 100% matching rank if sum is 0)
            if (totalWeight <= 0.0001f)
            {
                for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
                {
                    probabilities[r] = (r == (int)dungeonRank) ? 1.0f : 0.0f;
                }
                return;
            }

            for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
            {
                probabilities[r] /= totalWeight;
            }
        }

        /// <summary>
        /// Calculates the normalized probability distribution for all six ranks attempting a dungeon of the given rank.
        /// Normalizes weights across all valid ranks to account for boundary clamping (e.g. at Rank E or Rank S)
        /// and incorporates Fame Tier probability modulation.
        /// </summary>
        /// <param name="dungeonRank">The dungeon rank to evaluate.</param>
        /// <returns>A float array of size 6 where index corresponds to <see cref="DungeonRank"/> enum value.</returns>
        public float[] GetNormalizedProbabilities(DungeonRank dungeonRank)
        {
            float[] probabilities = new float[DungeonRankExtensions.RankCount];
            GetNormalizedProbabilities(dungeonRank, probabilities);
            return probabilities;
        }

        /// <summary>
        /// Evaluates and determines which adventurer rank attempts the dungeon using the configured probability distribution.
        /// </summary>
        /// <param name="rng">Optional System.Random instance for deterministic simulation; uses UnityEngine.Random if null.</param>
        /// <returns>The rolled <see cref="DungeonRank"/> of the adventurer party.</returns>
        public DungeonRank DetermineAttemptRank(System.Random rng = null)
        {
            float roll = rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
            return DetermineAttemptRank(roll);
        }

        /// <summary>
        /// Evaluates and determines the attempting adventurer rank using a predetermined roll value in range [0.0, 1.0).
        /// Ideal for unit testing and deterministic scenario replay.
        /// </summary>
        /// <param name="normalizedRoll">A normalized scalar between 0.0 (inclusive) and 1.0 (exclusive).</param>
        /// <returns>The determined <see cref="DungeonRank"/>.</returns>
        public DungeonRank DetermineAttemptRank(float normalizedRoll)
        {
            float clampedRoll = Mathf.Clamp(normalizedRoll, 0.0f, 0.999999f);
            GetNormalizedProbabilities(currentRank, _cachedProbabilities);

            float cumulative = 0f;
            for (int r = 0; r < _cachedProbabilities.Length; r++)
            {
                cumulative += _cachedProbabilities[r];
                if (clampedRoll < cumulative)
                {
                    var selectedRank = (DungeonRank)r;
                    currentAdventurerAttemptRank = selectedRank;
                    OnAdventurerAttemptDetermined?.Invoke(currentRank, selectedRank);
                    return selectedRank;
                }
            }

            // Edge fallback to current dungeon rank
            currentAdventurerAttemptRank = currentRank;
            OnAdventurerAttemptDetermined?.Invoke(currentRank, currentRank);
            return currentRank;
        }

        #endregion

        #region Progression State & Rank Advancement

        /// <summary>
        /// Records the outcome of a dungeon defense attempt, updates history and win streaks,
        /// awards progression points (XP), and evaluates rank promotion.
        /// </summary>
        /// <param name="attemptedRank">The adventurer rank that attempted the dungeon.</param>
        /// <param name="outcome">The battle outcome (Victory or Defeat).</param>
        /// <param name="difficultyMultiplier">Optional scaling multiplier based on battle challenge rating (default 1.0).</param>
        /// <returns>The amount of progression points awarded for this attempt.</returns>
        public int RecordAttemptResult(
            DungeonRank attemptedRank,
            BattleOutcome outcome,
            float difficultyMultiplier = 1.0f)
        {
            bool isVictory = outcome == BattleOutcome.Victory;
            return RecordAttemptResult(attemptedRank, isVictory, difficultyMultiplier);
        }

        /// <summary>
        /// Records the outcome of a dungeon defense attempt with a boolean victory flag.
        /// </summary>
        /// <param name="attemptedRank">The adventurer rank that attempted the dungeon.</param>
        /// <param name="isVictory">True if dungeon was successfully defended; false if cleared/breached.</param>
        /// <param name="difficultyMultiplier">Optional scaling multiplier based on battle challenge rating (default 1.0).</param>
        /// <returns>The amount of progression points awarded for this attempt.</returns>
        public int RecordAttemptResult(
            DungeonRank attemptedRank,
            bool isVictory,
            float difficultyMultiplier = 1.0f)
        {
            totalAttempts++;
            var rankStats = GetOrCreateRankStats(currentRank);
            rankStats.RecordOutcome(isVictory);

            int pointsGained = 0;
            var config = GetConfig();

            if (isVictory)
            {
                totalVictories++;
                currentWinStreak++;
                if (currentWinStreak > highestWinStreak)
                {
                    highestWinStreak = currentWinStreak;
                }

                // Calculate XP award
                int baseXp = config.GetBaseXpForAdventurerRank(attemptedRank);
                int streakBonusCap = Mathf.Min(currentWinStreak, config.MaxWinStreakBonusCap);
                float streakBonus = 1.0f + (streakBonusCap * config.WinStreakBonusMultiplier);
                float diffMult = Mathf.Max(0.1f, difficultyMultiplier);

                // Rank delta multiplier (bonus for defeating higher-rank adventurers)
                int rankDelta = (int)attemptedRank - (int)currentRank;
                float crossRankBonus = rankDelta > 0 ? 1.0f + (rankDelta * 0.5f) : 1.0f;

                // Fame XP multiplier
                float fameXpMultiplier = fameSystem != null ? fameSystem.GetActiveDefinition().XpRewardMultiplier : 1.0f;

                pointsGained = Mathf.RoundToInt(baseXp * streakBonus * diffMult * crossRankBonus * fameXpMultiplier);
                AddProgressionPoints(pointsGained);
            }
            else
            {
                totalDefeats++;
                currentWinStreak = 0;
            }

            return pointsGained;
        }

        /// <summary>
        /// Directly adds progression points towards the next rank promotion.
        /// Automatically advances rank if threshold is met and auto-promotion is enabled.
        /// </summary>
        /// <param name="points">Amount of progression points to add (non-negative).</param>
        public void AddProgressionPoints(int points)
        {
            if (points <= 0 || IsMaxRank) return;

            currentProgressionPoints += points;
            int required = RequiredPointsForNextRank;

            OnProgressionPointsGained?.Invoke(points, currentProgressionPoints, required);

            if (GetConfig().AutoPromoteOnThresholdMet && required > 0 && currentProgressionPoints >= required)
            {
                AdvanceRank();
            }
        }

        /// <summary>
        /// Checks whether the progression criteria for the next rank promotion is satisfied.
        /// </summary>
        /// <returns>True if accumulated points meet or exceed the required threshold.</returns>
        public bool CanAdvanceRank()
        {
            if (IsMaxRank) return false;
            int required = RequiredPointsForNextRank;
            return required > 0 && currentProgressionPoints >= required;
        }

        /// <summary>
        /// Attempts to promote the dungeon to the next rank tier if requirements are met.
        /// </summary>
        /// <returns>True if rank promotion succeeded, false otherwise.</returns>
        public bool TryAdvanceRank()
        {
            if (!CanAdvanceRank()) return false;
            AdvanceRank();
            return true;
        }

        /// <summary>
        /// Promotes the dungeon to the next rank tier regardless of current XP points.
        /// Carries over any excess progression points to the new tier.
        /// </summary>
        public void AdvanceRank()
        {
            if (IsMaxRank) return;

            var oldRank = currentRank;
            int required = RequiredPointsForNextRank;
            int carryoverPoints = Mathf.Max(0, currentProgressionPoints - required);

            currentRank = currentRank.GetNextRank();
            currentProgressionPoints = IsMaxRank ? 0 : carryoverPoints;

            OnRankChanged?.Invoke(oldRank, currentRank);
            OnRankPromoted?.Invoke(currentRank);

            Debug.Log($"[DungeonProgression] Dungeon promoted from {oldRank.GetDisplayName()} to {currentRank.GetDisplayName()}!");
        }

        /// <summary>
        /// Manually sets the dungeon rank directly to a specific target tier.
        /// </summary>
        /// <param name="newRank">The target rank to assign.</param>
        /// <param name="resetPoints">If true, clears accumulated progression points.</param>
        public void SetRank(DungeonRank newRank, bool resetPoints = true)
        {
            if (currentRank == newRank) return;

            var oldRank = currentRank;
            currentRank = newRank;

            if (resetPoints)
            {
                currentProgressionPoints = 0;
            }

            OnRankChanged?.Invoke(oldRank, currentRank);
        }

        /// <summary>
        /// Resets all progression state, streaks, history, and rank back to initial baseline.
        /// </summary>
        public void ResetProgression(DungeonRank startingRank = DungeonRank.E)
        {
            var oldRank = currentRank;
            currentRank = startingRank;
            currentProgressionPoints = 0;
            currentAdventurerAttemptRank = startingRank;
            totalAttempts = 0;
            totalVictories = 0;
            totalDefeats = 0;
            currentWinStreak = 0;
            highestWinStreak = 0;

            foreach (var kvp in _rankStats)
            {
                kvp.Value.Reset();
            }

            if (oldRank != currentRank)
            {
                OnRankChanged?.Invoke(oldRank, currentRank);
            }
        }

        /// <summary>
        /// Retrieves statistical progression history for a specific rank.
        /// </summary>
        /// <param name="rank">The rank to query.</param>
        /// <returns>Historical progression statistics record.</returns>
        public RankProgressionStats GetRankStats(DungeonRank rank)
        {
            return GetOrCreateRankStats(rank);
        }

        #endregion

        #region CoreGameLoop Handlers

        private void HandleRoundStarted(int roundNumber)
        {
            // Determine incoming adventurer party rank on each round start
            DetermineAttemptRank();
        }

        private void HandleRoundCompleted(int roundNumber, BattleResolutionResult resolution)
        {
            // Automatically record round outcome
            RecordAttemptResult(currentAdventurerAttemptRank, resolution.Outcome);
        }

        #endregion

        #region Private Helpers

        private DungeonProgressionConfigSO _fallbackConfig;

        private DungeonProgressionConfigSO GetConfig()
        {
            if (customConfig != null) return customConfig;

            if (_fallbackConfig == null)
            {
                _fallbackConfig = ScriptableObject.CreateInstance<DungeonProgressionConfigSO>();
            }
            return _fallbackConfig;
        }

        private void InitializeRankStats()
        {
            if (_rankStats.Count > 0) return;

            for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
            {
                var rank = (DungeonRank)r;
                _rankStats[rank] = new RankProgressionStats(rank);
            }
        }

        private RankProgressionStats GetOrCreateRankStats(DungeonRank rank)
        {
            InitializeRankStats();
            if (!_rankStats.TryGetValue(rank, out var stats))
            {
                stats = new RankProgressionStats(rank);
                _rankStats[rank] = stats;
            }
            return stats;
        }

        #endregion

        #region Context Menu & Debug Controls

        [ContextMenu("Debug/Determine Attempt Rank")]
        [Button]
        public void DebugDetermineAttempt()
        {
            var rolled = DetermineAttemptRank();
            Debug.Log($"[DungeonProgression Debug] Dungeon Rank: {currentRank.GetDisplayName()} -> Rolled Adventurer: {rolled.GetDisplayName()}");
        }

        [ContextMenu("Debug/Grant 100 XP")]
        [Button]
        public void DebugGrantPoints()
        {
            AddProgressionPoints(100);
            Debug.Log($"[DungeonProgression Debug] Points: {currentProgressionPoints}/{RequiredPointsForNextRank} ({ProgressNormalized:P0})");
        }

        [ContextMenu("Debug/Promote Next Rank")]
        [Button]
        public void DebugAdvanceRank()
        {
            AdvanceRank();
        }

        #endregion
    }
}
