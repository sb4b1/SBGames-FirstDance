using System;
using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Central manager for the Dungeon Fame & Notoriety system.
    /// Tracks consecutive undefeated win-streaks, promotes Fame Tiers, modulates adventurer
    /// attempt probabilities to attract higher-rank heroes, scales victory rewards (Energy & XP),
    /// and grants rare artifact loot drops for upset victories against higher-ranked invaders.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonFameSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Configuration ScriptableObject defining fame tiers, probability weight shifts, and rewards.")]
        [SerializeField] private DungeonFameConfigSO customConfig;

        [Header("System Dependencies (Optional Auto-Bound)")]
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonProgression dungeonProgression;
        [SerializeField] private DungeonMaster dungeonMaster;

        [Header("Runtime Fame State (Read Only)")]
        [SerializeField, ReadOnly] private int currentWinStreak = 0;
        [SerializeField, ReadOnly] private int highestWinStreak = 0;
        [SerializeField, ReadOnly] private DungeonFameTier currentTier = DungeonFameTier.Unknown;
        [SerializeField, ReadOnly] private DungeonFameTier highestTierReached = DungeonFameTier.Unknown;
        [SerializeField, ReadOnly] private int totalFamePoints = 0;

        #region Public Properties

        /// <summary>Active consecutive win streak without a single round loss.</summary>
        public int CurrentWinStreak => currentWinStreak;

        /// <summary>Highest win streak recorded across the entire session/run.</summary>
        public int HighestWinStreak => highestWinStreak;

        /// <summary>Active Fame Tier category.</summary>
        public DungeonFameTier CurrentTier => currentTier;

        /// <summary>Highest Fame Tier achieved in this run.</summary>
        public DungeonFameTier HighestTierReached => highestTierReached;

        /// <summary>Thematic title of the current dungeon fame level.</summary>
        public string CurrentTitle => GetActiveDefinition().TitleOverride != "" 
            ? GetActiveDefinition().TitleOverride 
            : currentTier.GetTitle();

        /// <summary>Total cumulative Fame Points earned.</summary>
        public int TotalFamePoints => totalFamePoints;

        /// <summary>Configuration asset in use.</summary>
        public DungeonFameConfigSO Config => customConfig;

        #endregion

        #region Events

        /// <summary>Fired when the undefeated win streak changes (currentStreak, highestStreak).</summary>
        public event Action<int, int> OnFameStreakChanged;

        /// <summary>Fired when the dungeon promotes or demotes in Fame Tier (oldTier, newTier).</summary>
        public event Action<DungeonFameTier, DungeonFameTier> OnFameTierChanged;

        /// <summary>Fired when a higher-rank adventurer party is rolled due to high fame.</summary>
        public event Action<DungeonRank, DungeonRank, DungeonFameTier> OnHighRankAdventurerAttracted;

        /// <summary>Fired when a rare artifact is looted from defeating a high-rank challenger.</summary>
        public event Action<Artifact> OnRareArtifactLooted;

        #endregion

        private void Awake()
        {
            BindDependencies();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// Programmatic initialization for runtime setups and headless testing.
        /// </summary>
        public void Initialize(
            DungeonFameConfigSO config = null,
            CoreGameLoop loop = null,
            DungeonProgression progression = null,
            DungeonMaster dm = null,
            int startingStreak = 0)
        {
            UnsubscribeEvents();

            customConfig = config;
            coreGameLoop = loop;
            dungeonProgression = progression;
            dungeonMaster = dm;

            currentWinStreak = Mathf.Max(0, startingStreak);
            highestWinStreak = currentWinStreak;
            currentTier = EvaluateCurrentTier();
            highestTierReached = currentTier;
            totalFamePoints = 0;

            SubscribeEvents();
        }

        #region Dependency Binding & Event Hooks

        private void BindDependencies()
        {
            if (coreGameLoop == null)
            {
                coreGameLoop = GetComponent<CoreGameLoop>() ?? FindAnyObjectByType<CoreGameLoop>();
            }

            if (dungeonProgression == null)
            {
                dungeonProgression = GetComponent<DungeonProgression>() ?? FindAnyObjectByType<DungeonProgression>();
            }

            if (dungeonMaster == null)
            {
                dungeonMaster = GetComponent<DungeonMaster>() ?? FindAnyObjectByType<DungeonMaster>();
            }
        }

        private void SubscribeEvents()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundCompleted += HandleRoundCompleted;
            }

            if (dungeonProgression != null)
            {
                dungeonProgression.OnAdventurerAttemptDetermined += HandleAdventurerAttemptDetermined;
            }
        }

        private void UnsubscribeEvents()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundCompleted -= HandleRoundCompleted;
            }

            if (dungeonProgression != null)
            {
                dungeonProgression.OnAdventurerAttemptDetermined -= HandleAdventurerAttemptDetermined;
            }
        }

        private void HandleRoundCompleted(int roundNumber, BattleResolutionResult resolution)
        {
            DungeonRank dungeonRank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            DungeonRank adventurerRank = dungeonProgression != null ? dungeonProgression.CurrentAdventurerAttemptRank : dungeonRank;

            RecordBattleOutcome(resolution.Outcome, dungeonRank, adventurerRank);
        }

        private void HandleAdventurerAttemptDetermined(DungeonRank dungeonRank, DungeonRank adventurerRank)
        {
            if (adventurerRank > dungeonRank && currentTier > DungeonFameTier.Unknown)
            {
                OnHighRankAdventurerAttracted?.Invoke(adventurerRank, dungeonRank, currentTier);
                Debug.Log($"[DungeonFameSystem] High Fame ({currentTier.GetTitle()}) attracted a higher-rank challenger: {adventurerRank.GetDisplayName()} vs Dungeon {dungeonRank.GetDisplayName()}!");
            }
        }

        #endregion

        #region Core Outcome Processing

        /// <summary>
        /// Records battle outcome, updates win-streak, evaluates Fame Tier transitions,
        /// and rolls for potential rare artifact loot drops.
        /// </summary>
        public void RecordBattleOutcome(BattleOutcome outcome, DungeonRank dungeonRank, DungeonRank adventurerRank)
        {
            var oldTier = currentTier;

            if (outcome == BattleOutcome.Victory)
            {
                currentWinStreak++;
                if (currentWinStreak > highestWinStreak)
                {
                    highestWinStreak = currentWinStreak;
                }

                // Award Fame points based on adventurer rank and tier
                int rankDelta = (int)adventurerRank - (int)dungeonRank;
                int basePoints = ((int)adventurerRank + 1) * 50;
                int upsetBonus = rankDelta > 0 ? (rankDelta * 100) : 0;
                int gainedFame = basePoints + upsetBonus;
                totalFamePoints += gainedFame;

                currentTier = EvaluateCurrentTier();
                if ((int)currentTier > (int)highestTierReached)
                {
                    highestTierReached = currentTier;
                }

                OnFameStreakChanged?.Invoke(currentWinStreak, highestWinStreak);

                if (currentTier != oldTier)
                {
                    OnFameTierChanged?.Invoke(oldTier, currentTier);
                    Debug.Log($"[DungeonFameSystem] Fame Tier elevated! New Tier: {currentTier.GetFormattedLabel()} (Streak: {currentWinStreak})");
                }

                // Check rare artifact loot drop if defeating higher rank adventurer at Tier >= 2
                RollRareArtifactDrop(dungeonRank, adventurerRank);
            }
            else
            {
                // Hard reset win-streak and fame tier on defeat
                currentWinStreak = 0;
                currentTier = DungeonFameTier.Unknown;

                OnFameStreakChanged?.Invoke(currentWinStreak, highestWinStreak);

                if (oldTier != DungeonFameTier.Unknown)
                {
                    OnFameTierChanged?.Invoke(oldTier, currentTier);
                    Debug.Log($"[DungeonFameSystem] Dungeon defense was breached! Fame streak reset to 0. Fame Tier downgraded from {oldTier.GetTitle()} to {currentTier.GetTitle()}.");
                }
            }
        }

        /// <summary>
        /// Evaluates current fame tier based on the active win streak.
        /// </summary>
        public DungeonFameTier EvaluateCurrentTier()
        {
            if (customConfig != null)
            {
                return customConfig.EvaluateTierFromStreak(currentWinStreak);
            }
            return DungeonFameTierExtensions.FromWinStreak(currentWinStreak);
        }

        /// <summary>
        /// Retrieves active fame tier definition.
        /// </summary>
        public FameTierDefinition GetActiveDefinition()
        {
            if (customConfig != null)
            {
                return customConfig.GetDefinition(currentTier);
            }

            // Fallback default definition
            return new FameTierDefinition
            {
                Tier = currentTier,
                RequiredWinStreak = currentTier.GetDefaultThreshold(),
                EnergyRewardMultiplier = 1.0f + ((int)currentTier * 0.25f),
                XpRewardMultiplier = 1.0f + ((int)currentTier * 0.35f),
                OneRankHigherBonusWeight = (int)currentTier * 0.15f,
                TwoRanksHigherBonusWeight = Mathf.Max(0, ((int)currentTier - 1)) * 0.10f,
                ThreePlusRanksHigherBonusWeight = Mathf.Max(0, ((int)currentTier - 3)) * 0.15f,
                RareArtifactDropChance = Mathf.Clamp01((int)currentTier * 0.12f)
            };
        }

        #endregion

        #region Cross-Rank Probability Modulation

        /// <summary>
        /// Calculates the additive probability weight bonus for a given signed rank delta (Adventurer - Dungeon)
        /// based on the active Fame Tier.
        /// </summary>
        /// <param name="delta">Signed rank distance (adventurer rank - dungeon rank).</param>
        /// <returns>Additive probability weight bonus (>= 0.0).</returns>
        public float GetProbabilityBonusForDelta(int delta)
        {
            var def = GetActiveDefinition();
            if (delta == 1) return def.OneRankHigherBonusWeight;
            if (delta == 2) return def.TwoRanksHigherBonusWeight;
            if (delta >= 3) return def.ThreePlusRanksHigherBonusWeight;
            return 0f;
        }

        #endregion

        #region Reward & Penalty Multipliers

        /// <summary>
        /// Calculates scaled energy reward upon victory factoring in Fame Tier multipliers and adventurer rank upset bonus.
        /// </summary>
        public int CalculateScaledEnergyReward(int baseEnergy, DungeonRank dungeonRank, DungeonRank adventurerRank)
        {
            var def = GetActiveDefinition();
            float tierMult = def.EnergyRewardMultiplier;

            int rankDelta = (int)adventurerRank - (int)dungeonRank;
            float upsetBonus = rankDelta > 0 ? (1.0f + (rankDelta * 0.20f)) : 1.0f;

            return Mathf.RoundToInt(baseEnergy * tierMult * upsetBonus);
        }

        /// <summary>
        /// Calculates scaled progression XP reward upon victory factoring in Fame Tier multipliers.
        /// </summary>
        public int CalculateScaledXpReward(int baseXp, DungeonRank dungeonRank, DungeonRank adventurerRank)
        {
            var def = GetActiveDefinition();
            float tierMult = def.XpRewardMultiplier;

            int rankDelta = (int)adventurerRank - (int)dungeonRank;
            float upsetBonus = rankDelta > 0 ? (1.0f + (rankDelta * 0.30f)) : 1.0f;

            return Mathf.RoundToInt(baseXp * tierMult * upsetBonus);
        }

        /// <summary>
        /// Calculates scaled essence breach penalty when a higher-rank adventurer party breaches the dungeon defense.
        /// </summary>
        public int CalculateScaledBreachPenalty(int basePenalty, DungeonRank dungeonRank, DungeonRank adventurerRank)
        {
            int rankDelta = (int)adventurerRank - (int)dungeonRank;
            if (rankDelta <= 0) return basePenalty;

            float penaltyRate = customConfig != null ? customConfig.BreachEssencePenaltyPerRankDelta : 0.25f;
            float penaltyMultiplier = 1.0f + (rankDelta * penaltyRate);

            return Mathf.RoundToInt(basePenalty * penaltyMultiplier);
        }

        #endregion

        #region Rare Artifact Loot Drop

        /// <summary>
        /// Rolls for rare artifact loot drop when player defeats an adventurer of higher rank than the dungeon.
        /// </summary>
        public Artifact RollRareArtifactDrop(
            DungeonRank dungeonRank,
            DungeonRank adventurerRank,
            System.Random rng = null,
            float? rollOverride = null)
        {
            int rankDelta = (int)adventurerRank - (int)dungeonRank;
            if (rankDelta <= 0 || currentTier < DungeonFameTier.Feared)
            {
                return null;
            }

            var def = GetActiveDefinition();
            float dropChance = def.RareArtifactDropChance;
            if (dropChance <= 0.001f) return null;

            float roll = rollOverride.HasValue ? rollOverride.Value : (rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value);
            if (roll > dropChance) return null;

            // Pick candidate artifact from loot pool
            var artifact = GenerateArtifactDrop(adventurerRank);
            if (artifact != null)
            {
                if (dungeonMaster != null)
                {
                    dungeonMaster.AddArtifact(artifact);
                }

                OnRareArtifactLooted?.Invoke(artifact);
                Debug.Log($"[DungeonFameSystem] Rare Loot Drop! Obtained '{artifact.ArtifactName}' (Power {artifact.PowerLevel}) from defeating {adventurerRank.GetDisplayName()} challengers!");
            }

            return artifact;
        }

        private Artifact GenerateArtifactDrop(DungeonRank adventurerRank)
        {
            var pool = customConfig != null ? customConfig.ArtifactLootPool : null;
            if (pool != null && pool.Count > 0)
            {
                var eligible = new List<RareArtifactDropTemplate>();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (adventurerRank >= pool[i].MinimumAdventurerRank)
                    {
                        eligible.Add(pool[i]);
                    }
                }

                if (eligible.Count > 0)
                {
                    int pickedIndex = UnityEngine.Random.Range(0, eligible.Count);
                    var template = eligible[pickedIndex];
                    return new Artifact(template.ArtifactName, template.PowerLevel, template.EssenceCost, null, template.StartEmpowered);
                }
            }

            // Fallback generic dropped relic
            string fallbackName = $"Hero's Trophy ({adventurerRank} Rank)";
            int fallbackPower = Mathf.Max(2, (int)adventurerRank + 1);
            return new Artifact(fallbackName, fallbackPower, 0, null, false);
        }

        #endregion

        #region Debug Controls

        [ContextMenu("Debug/Grant Win Streak +1")]
        [Button]
        public void DebugGrantWin()
        {
            DungeonRank dRank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            DungeonRank aRank = dungeonProgression != null ? dungeonProgression.CurrentAdventurerAttemptRank : dRank;
            RecordBattleOutcome(BattleOutcome.Victory, dRank, aRank);
        }

        [ContextMenu("Debug/Simulate Defeat (Reset Fame)")]
        [Button]
        public void DebugSimulateDefeat()
        {
            DungeonRank dRank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            DungeonRank aRank = dungeonProgression != null ? dungeonProgression.CurrentAdventurerAttemptRank : dRank;
            RecordBattleOutcome(BattleOutcome.Defeat, dRank, aRank);
        }

        [ContextMenu("Debug/Set Streak To 10 (Legendary)")]
        [Button]
        public void DebugSetLegendary()
        {
            currentWinStreak = 10;
            currentTier = EvaluateCurrentTier();
            OnFameStreakChanged?.Invoke(currentWinStreak, highestWinStreak);
            OnFameTierChanged?.Invoke(DungeonFameTier.Unknown, currentTier);
            Debug.Log($"[DungeonFameSystem Debug] Fame set to {currentTier.GetFormattedLabel()}");
        }

        #endregion
    }
}
