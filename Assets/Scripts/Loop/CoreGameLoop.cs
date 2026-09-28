using System;
using UnityEngine;

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

        // Current runtime state
        private GamePhase currentPhase = GamePhase.NotStarted;
        private int currentRound = 1;
        private GameOverReason gameOverReason = GameOverReason.None;

        // Public Properties
        public GamePhase CurrentPhase => currentPhase;
        public int CurrentRound => currentRound;
        public DungeonMaster DM => dungeonMaster;
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
                dungeonMaster = GetComponent<DungeonMaster>();
                if (dungeonMaster == null)
                {
                    dungeonMaster = FindAnyObjectByType<DungeonMaster>();
                }
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
            int maxRoundLimit = 0)
        {
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated -= HandleDungeonMasterDefeated;
            }

            dungeonMaster = dm;
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
                energyAwarded = result.EnergyReward ?? victoryEnergyBonus;
                dungeonMaster.GainEnergy(energyAwarded);
            }
            else
            {
                // Loss: Dungeon cleared by adventurers
                int prevEssence = dungeonMaster.Essence;
                dungeonMaster.ApplyRoundLossPenalty(result.EssenceDamage);
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
    }
}
