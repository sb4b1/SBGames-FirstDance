using System;
using UnityEngine;
using ArtificeToolkit.Attributes;

namespace SBGames.Loop
{
    /// <summary>
    /// Represents the high-level phase within the Core Game Loop.
    /// </summary>
    public enum GamePhase
    {
        NotStarted = 0,
        Planning = 1,
        Battle = 2,
        GameOver = 3
    }

    /// <summary>
    /// The outcome of a battle simulation.
    /// </summary>
    public enum BattleOutcome
    {
        Victory = 0, // All adventurers die or retreat (dungeon defended)
        Defeat = 1   // Dungeon cleared by adventurers
    }

    /// <summary>
    /// Reason explaining why the run concluded.
    /// </summary>
    public enum GameOverReason
    {
        None = 0,
        EssenceDepleted = 1,
        ArtifactsLost = 2,
        SurpassedAllRounds = 3
    }

    /// <summary>
    /// Encapsulates the battle outcome parameters passed into CoreGameLoop from external battle systems.
    /// </summary>
    public readonly struct BattleResult
    {
        public readonly BattleOutcome Outcome;
        public readonly int? EssenceDamage;
        public readonly int? EnergyReward;

        public BattleResult(BattleOutcome outcome, int? essenceDamage = null, int? energyReward = null)
        {
            Outcome = outcome;
            EssenceDamage = essenceDamage;
            EnergyReward = energyReward;
        }

        public static BattleResult Win(int? bonusEnergy = null)
        {
            return new BattleResult(BattleOutcome.Victory, essenceDamage: 0, energyReward: bonusEnergy);
        }

        public static BattleResult Loss(int? essenceDamage = null)
        {
            return new BattleResult(BattleOutcome.Defeat, essenceDamage: essenceDamage, energyReward: 0);
        }
    }

    /// <summary>
    /// Summary of the resolved round battle.
    /// </summary>
    public readonly struct BattleResolutionResult
    {
        public readonly int RoundNumber;
        public readonly BattleOutcome Outcome;
        public readonly int EssenceDeducted;
        public readonly int EnergyAwarded;
        public readonly bool IsGameOver;
        public readonly GameOverReason GameOverReason;

        public BattleResolutionResult(
            int roundNumber,
            BattleOutcome outcome,
            int essenceDeducted,
            int energyAwarded,
            bool isGameOver,
            GameOverReason gameOverReason)
        {
            RoundNumber = roundNumber;
            Outcome = outcome;
            EssenceDeducted = essenceDeducted;
            EnergyAwarded = energyAwarded;
            IsGameOver = isGameOver;
            GameOverReason = gameOverReason;
        }
    }

    /// <summary>
    /// Orchestrates the two-phase cycle (Planning -> Battle) and manages round state transitions.
    /// Owns the round state machine, triggers win/loss conditions based on dungeon defense outcome,
    /// and maintains strict independence from combat simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoreGameLoop : MonoBehaviour
    {
        [Header("Player & Container Reference")]
        [Tooltip("Reference to the Dungeon Master data container.")]
        [SerializeField] private DungeonMaster dungeonMaster;

        [Tooltip("Optional reference to DungeonFameSystem for reward scaling.")]
        [SerializeField] private DungeonFameSystem fameSystem;

        [Header("Round & Phase Configuration")]
        [Tooltip("Starting round index.")]
        [SerializeField] private int startingRound = 1;

        [Tooltip("Energy granted to DM at the start of each Planning phase.")]
        [SerializeField] private int planningEnergyAllowance = 15;

        [Tooltip("Energy awarded to DM upon successfully defending the dungeon (Victory).")]
        [SerializeField] private int victoryEnergyBonus = 25;

        [Tooltip("Optional wave limit for victory. Set to 0 or negative for Endless Survival mode.")]
        [SerializeField] private int maxRounds = 0;

        [Tooltip("Automatically starts Round 1 (Planning phase) when the scene starts if not already started.")]
        [SerializeField] private bool autoStartOnSceneLoad = true;

        [Header("Runtime State (Read Only)")]
        [Tooltip("Current phase of the game loop.")]
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private GamePhase currentPhase = GamePhase.NotStarted;

        [Tooltip("Current stage / round number.")]
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int currentRound = 1;

        [Tooltip("Reason why game over was triggered (if applicable).")]
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private GameOverReason gameOverReason = GameOverReason.None;

        [Header("Debug / Stage Controls")]
        [Tooltip("Target round number to jump to when clicking Jump To Target Round.")]
        [SerializeField] private int debugTargetRound = 1;

        [Tooltip("Target phase to switch to when clicking Switch To Target Phase.")]
        [SerializeField] private GamePhase debugTargetPhase = GamePhase.Planning;

        [Tooltip("Target game over reason when clicking Trigger Game Over.")]
        [SerializeField] private GameOverReason debugGameOverReason = GameOverReason.EssenceDepleted;

        // Public Properties
        public GamePhase CurrentPhase => currentPhase;
        public int CurrentRound => currentRound;
        public DungeonMaster DM => dungeonMaster;
        public DungeonFameSystem FameSystem { get => fameSystem; set => fameSystem = value; }
        public bool IsGameOver => currentPhase == GamePhase.GameOver;
        public GameOverReason ReasonForGameOver => gameOverReason;
        public bool CanPerformPlanningActions => currentPhase == GamePhase.Planning;

        // Events
        public event Action<GamePhase> OnPhaseChanged;
        public event Action<int> OnRoundStarted;
        public event Action<int, BattleResolutionResult> OnRoundCompleted;
        public event Action<GameOverReason, int> OnGameOver;

        private void Awake()
        {
            if (dungeonMaster == null)
            {
                dungeonMaster = GetComponent<DungeonMaster>() ?? FindAnyObjectByType<DungeonMaster>();
            }
            if (fameSystem == null)
            {
                fameSystem = GetComponent<DungeonFameSystem>() ?? FindAnyObjectByType<DungeonFameSystem>();
            }
        }

        private void Start()
        {
            if (autoStartOnSceneLoad && currentPhase == GamePhase.NotStarted)
            {
                StartGame();
            }
        }

        private void OnEnable()
        {
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated += HandleDungeonMasterDefeated;
            }
        }

        private void OnDisable()
        {
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated -= HandleDungeonMasterDefeated;
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime setups and unit testing without scene setup.
        /// </summary>
        public void Initialize(
            DungeonMaster dm,
            int initialRound = 1,
            int planningAllowance = 15,
            int victoryBonus = 25,
            int maxRoundLimit = 0,
            DungeonFameSystem fame = null)
        {
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated -= HandleDungeonMasterDefeated;
            }

            dungeonMaster = dm;
            fameSystem = fame;

            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated += HandleDungeonMasterDefeated;
            }

            startingRound = Mathf.Max(1, initialRound);
            currentRound = startingRound;
            planningEnergyAllowance = Mathf.Max(0, planningAllowance);
            victoryEnergyBonus = Mathf.Max(0, victoryBonus);
            maxRounds = maxRoundLimit;
            currentPhase = GamePhase.NotStarted;
            gameOverReason = GameOverReason.None;
        }

        /// <summary>
        /// Starts the game run, initiating Round 1 in the Planning phase.
        /// </summary>
        public void StartGame()
        {
            if (dungeonMaster == null)
            {
                Debug.LogError("[CoreGameLoop] Cannot start game without a DungeonMaster reference!");
                return;
            }

            currentRound = startingRound;
            gameOverReason = GameOverReason.None;

            // Check if DM is already defeated on startup
            if (CheckAndTriggerGameOver()) return;

            EnterPhase(GamePhase.Planning);
            OnRoundStarted?.Invoke(currentRound);
        }

        /// <summary>
        /// Allows monster purchases during the Planning phase without triggering Battle.
        /// </summary>
        public bool TryPurchaseMonster(int energyCost)
        {
            if (currentPhase != GamePhase.Planning)
            {
                Debug.LogWarning("[CoreGameLoop] Cannot purchase monsters outside the Planning phase.");
                return false;
            }

            if (dungeonMaster == null) return false;
            return dungeonMaster.PurchaseMonster(energyCost);
        }

        /// <summary>
        /// Allows artifact forging during the Planning phase without triggering Battle.
        /// </summary>
        public Artifact TryForgeArtifact(string artifactName, int essenceCost, int startingPower = 1)
        {
            if (currentPhase != GamePhase.Planning)
            {
                Debug.LogWarning("[CoreGameLoop] Cannot forge artifacts outside the Planning phase.");
                return null;
            }

            if (dungeonMaster == null) return null;
            var artifact = dungeonMaster.ForgeArtifact(artifactName, essenceCost, startingPower);

            CheckAndTriggerGameOver();
            return artifact;
        }

        /// <summary>
        /// Allows artifact empowering during the Planning phase without triggering Battle.
        /// </summary>
        public bool TryEmpowerArtifact(Artifact artifact, int energyCost, int powerIncrease = 1)
        {
            if (currentPhase != GamePhase.Planning)
            {
                Debug.LogWarning("[CoreGameLoop] Cannot empower artifacts outside the Planning phase.");
                return false;
            }

            if (dungeonMaster == null) return false;
            return dungeonMaster.EmpowerArtifact(artifact, energyCost, powerIncrease);
        }

        /// <summary>
        /// Transitions from Planning to Battle phase.
        /// Returns true if transition succeeded, false if not currently in Planning.
        /// </summary>
        public bool StartBattle()
        {
            if (currentPhase != GamePhase.Planning)
            {
                Debug.LogWarning($"[CoreGameLoop] Cannot start Battle from phase: {currentPhase}");
                return false;
            }

            if (CheckAndTriggerGameOver()) return false;

            EnterPhase(GamePhase.Battle);
            return true;
        }

        /// <summary>
        /// Resolves the current Battle phase using a BattleResult parameter.
        /// Win: All adventurers die/retreat -> dungeon defended.
        /// Loss: Dungeon cleared by adventurers -> deducts essence from DM.
        /// </summary>
        public BattleResolutionResult ResolveBattle(BattleResult result)
        {
            if (currentPhase != GamePhase.Battle)
            {
                Debug.LogError($"[CoreGameLoop] Cannot resolve battle while in phase: {currentPhase}. Battle must be active.");
                return new BattleResolutionResult(currentRound, result.Outcome, 0, 0, IsGameOver, gameOverReason);
            }

            int essenceDeducted = 0;
            int energyAwarded = 0;

            if (result.Outcome == BattleOutcome.Victory)
            {
                // Win: All adventurers died or retreated
                int baseEnergy = result.EnergyReward ?? victoryEnergyBonus;
                if (fameSystem != null && !result.EnergyReward.HasValue)
                {
                    var prog = GetComponent<DungeonProgression>() ?? FindAnyObjectByType<DungeonProgression>();
                    DungeonRank dRank = prog != null ? prog.CurrentRank : DungeonRank.E;
                    DungeonRank aRank = prog != null ? prog.CurrentAdventurerAttemptRank : dRank;
                    energyAwarded = fameSystem.CalculateScaledEnergyReward(baseEnergy, dRank, aRank);
                }
                else
                {
                    energyAwarded = baseEnergy;
                }

                dungeonMaster.GainEnergy(energyAwarded);
            }
            else
            {
                // Loss: Dungeon cleared by adventurers
                int prevEssence = dungeonMaster.Essence;
                int? lossPenalty = result.EssenceDamage;

                if (!lossPenalty.HasValue && fameSystem != null)
                {
                    var prog = GetComponent<DungeonProgression>() ?? FindAnyObjectByType<DungeonProgression>();
                    DungeonRank dRank = prog != null ? prog.CurrentRank : DungeonRank.E;
                    DungeonRank aRank = prog != null ? prog.CurrentAdventurerAttemptRank : dRank;
                    lossPenalty = fameSystem.CalculateScaledBreachPenalty(dungeonMaster.RoundLossEssencePenalty, dRank, aRank);
                }

                dungeonMaster.ApplyRoundLossPenalty(lossPenalty);
                essenceDeducted = Mathf.Max(0, prevEssence - dungeonMaster.Essence);
            }

            // Check if DM was defeated by essence loss or artifact removal
            bool gameOver = CheckAndTriggerGameOver();

            // Check optional MaxRounds win condition for fixed-wave campaigns
            if (!gameOver && maxRounds > 0 && currentRound >= maxRounds && result.Outcome == BattleOutcome.Victory)
            {
                TriggerGameOver(GameOverReason.SurpassedAllRounds);
                gameOver = true;
            }

            var resolution = new BattleResolutionResult(
                currentRound,
                result.Outcome,
                essenceDeducted,
                energyAwarded,
                gameOver,
                gameOverReason);

            OnRoundCompleted?.Invoke(currentRound, resolution);

            // If not game over, progress to next round and transition back to Planning
            if (!gameOver)
            {
                currentRound++;
                EnterPhase(GamePhase.Planning);

                // Grant round planning energy allowance
                dungeonMaster.GainEnergy(planningEnergyAllowance);
                OnRoundStarted?.Invoke(currentRound);
            }

            return resolution;
        }

        /// <summary>
        /// Convenience overload accepting BattleOutcome directly with optional penalty and reward.
        /// </summary>
        public BattleResolutionResult ResolveBattle(
            BattleOutcome outcome,
            int? customEssencePenalty = null,
            int? customEnergyReward = null)
        {
            return ResolveBattle(new BattleResult(outcome, customEssencePenalty, customEnergyReward));
        }

        private void EnterPhase(GamePhase newPhase)
        {
            if (currentPhase == newPhase) return;

            currentPhase = newPhase;
            OnPhaseChanged?.Invoke(currentPhase);
        }

        private bool CheckAndTriggerGameOver()
        {
            if (dungeonMaster == null) return false;

            if (dungeonMaster.Essence <= 0)
            {
                TriggerGameOver(GameOverReason.EssenceDepleted);
                return true;
            }

            if (dungeonMaster.ArtifactCount == 0)
            {
                TriggerGameOver(GameOverReason.ArtifactsLost);
                return true;
            }

            return false;
        }

        private void HandleDungeonMasterDefeated(DefeatReason reason)
        {
            if (currentPhase == GamePhase.GameOver) return;

            GameOverReason mappedReason = reason switch
            {
                DefeatReason.EssenceDepleted => GameOverReason.EssenceDepleted,
                DefeatReason.ArtifactsLost => GameOverReason.ArtifactsLost,
                _ => GameOverReason.None
            };

            TriggerGameOver(mappedReason);
        }

        private void TriggerGameOver(GameOverReason reason)
        {
            if (currentPhase == GamePhase.GameOver) return;

            gameOverReason = reason;
            EnterPhase(GamePhase.GameOver);
            OnGameOver?.Invoke(gameOverReason, currentRound);
        }

        #region Inspector Debug & Stage Controls

        /// <summary>
        /// Starts the game loop from Round 1 or startingRound in Planning phase.
        /// </summary>
        [ContextMenu("Debug/Start Game")]
        [Button]
        public void DebugStartGame()
        {
            Debug.Log("[CoreGameLoop Debug] Starting Game...");
            StartGame();
        }

        /// <summary>
        /// Switches phase directly to Planning without altering the round number.
        /// </summary>
        [ContextMenu("Debug/Start Planning Phase")]
        [Button]
        public void DebugStartPlanningPhase()
        {
            Debug.Log($"[CoreGameLoop Debug] Transitioning to Planning phase (Round {currentRound}).");
            gameOverReason = GameOverReason.None;
            EnterPhase(GamePhase.Planning);
        }

        /// <summary>
        /// Switches phase directly to Battle.
        /// </summary>
        [ContextMenu("Debug/Start Battle Phase")]
        [Button]
        public void DebugStartBattlePhase()
        {
            if (currentPhase == GamePhase.NotStarted)
            {
                StartGame();
            }

            Debug.Log($"[CoreGameLoop Debug] Transitioning to Battle phase (Round {currentRound}).");
            gameOverReason = GameOverReason.None;
            EnterPhase(GamePhase.Battle);
        }

        /// <summary>
        /// Steps forward to the next round in Planning phase and grants planning energy allowance.
        /// </summary>
        [ContextMenu("Debug/Next Round")]
        [Button]
        public void DebugNextRound()
        {
            if (currentPhase == GamePhase.NotStarted)
            {
                StartGame();
                return;
            }

            currentRound++;
            gameOverReason = GameOverReason.None;
            EnterPhase(GamePhase.Planning);

            dungeonMaster?.GainEnergy(planningEnergyAllowance);
            OnRoundStarted?.Invoke(currentRound);
            Debug.Log($"[CoreGameLoop Debug] Advanced to Round {currentRound} (Planning phase).");
        }

        /// <summary>
        /// Steps back to the previous round (clamped to minimum 1) in Planning phase.
        /// </summary>
        [ContextMenu("Debug/Previous Round")]
        [Button]
        public void DebugPreviousRound()
        {
            currentRound = Mathf.Max(1, currentRound - 1);
            gameOverReason = GameOverReason.None;
            EnterPhase(GamePhase.Planning);

            OnRoundStarted?.Invoke(currentRound);
            Debug.Log($"[CoreGameLoop Debug] Reverted to Round {currentRound} (Planning phase).");
        }

        /// <summary>
        /// Sets the round to debugTargetRound configured in the Inspector.
        /// </summary>
        [ContextMenu("Debug/Jump To Target Round")]
        [Button]
        public void DebugJumpToTargetRound()
        {
            DebugSetRound(debugTargetRound, true);
        }

        /// <summary>
        /// Sets the current round/stage directly to a specific value.
        /// </summary>
        public void DebugSetRound(int targetRound, bool notifyListeners = true)
        {
            currentRound = Mathf.Max(1, targetRound);
            debugTargetRound = currentRound;

            if (currentPhase == GamePhase.NotStarted)
            {
                EnterPhase(GamePhase.Planning);
            }

            if (notifyListeners)
            {
                OnRoundStarted?.Invoke(currentRound);
            }

            Debug.Log($"[CoreGameLoop Debug] Jumped to Round {currentRound} (Phase: {currentPhase}).");
        }

        /// <summary>
        /// Switches phase to debugTargetPhase configured in the Inspector.
        /// </summary>
        [ContextMenu("Debug/Switch To Target Phase")]
        [Button]
        public void DebugSwitchToTargetPhase()
        {
            DebugSetPhase(debugTargetPhase);
        }

        /// <summary>
        /// Forces any specified GamePhase transition.
        /// </summary>
        public void DebugSetPhase(GamePhase phase)
        {
            if (phase == GamePhase.GameOver)
            {
                DebugTriggerGameOver(debugGameOverReason != GameOverReason.None ? debugGameOverReason : GameOverReason.EssenceDepleted);
                return;
            }

            gameOverReason = GameOverReason.None;
            EnterPhase(phase);
            Debug.Log($"[CoreGameLoop Debug] Force set Phase to {phase} (Round {currentRound}).");
        }

        /// <summary>
        /// Simulates battle Victory: awards victory bonus energy and advances to the next round Planning phase.
        /// </summary>
        [ContextMenu("Debug/Win Battle")]
        [Button]
        public void DebugWinBattle()
        {
            if (currentPhase != GamePhase.Battle)
            {
                EnterPhase(GamePhase.Battle);
            }

            Debug.Log($"[CoreGameLoop Debug] Resolving Round {currentRound} as Victory.");
            ResolveBattle(BattleResult.Win());
        }

        /// <summary>
        /// Simulates battle Defeat: applies essence penalty to DM and triggers GameOver if essence drops to 0.
        /// </summary>
        [ContextMenu("Debug/Lose Battle")]
        [Button]
        public void DebugLoseBattle()
        {
            if (currentPhase != GamePhase.Battle)
            {
                EnterPhase(GamePhase.Battle);
            }

            Debug.Log($"[CoreGameLoop Debug] Resolving Round {currentRound} as Defeat.");
            ResolveBattle(BattleResult.Loss());
        }

        /// <summary>
        /// Triggers game over using the debugGameOverReason field.
        /// </summary>
        [ContextMenu("Debug/Trigger Game Over")]
        [Button]
        public void DebugTriggerGameOver()
        {
            DebugTriggerGameOver(debugGameOverReason != GameOverReason.None ? debugGameOverReason : GameOverReason.EssenceDepleted);
        }

        /// <summary>
        /// Forces game over state with the specified reason.
        /// </summary>
        public void DebugTriggerGameOver(GameOverReason reason)
        {
            Debug.LogWarning($"[CoreGameLoop Debug] Triggering Game Over (Reason: {reason}) on Round {currentRound}.");
            TriggerGameOver(reason);
        }

        /// <summary>
        /// Resets the game loop to initial starting configuration.
        /// </summary>
        [ContextMenu("Debug/Reset Game Loop")]
        [Button]
        public void DebugResetGame()
        {
            currentRound = startingRound;
            debugTargetRound = startingRound;
            gameOverReason = GameOverReason.None;
            currentPhase = GamePhase.NotStarted;

            Debug.Log("[CoreGameLoop Debug] Game loop reset to initial state.");
            OnPhaseChanged?.Invoke(currentPhase);

            if (autoStartOnSceneLoad)
            {
                StartGame();
            }
        }

        #endregion
    }
}
