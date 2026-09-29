using System;
using System.Collections.Generic;
using System.Linq;
using ArtificeToolkit.Attributes;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Loop.Talents
{
    /// <summary>
    /// Core runtime manager and state container for the Dungeon Master Talent Tree.
    /// Coordinates talent unlocking with DungeonMaster energy, DungeonProgression rank constraints,
    /// CoreGameLoop phase gating (Planning-only purchases), and active power battle triggers.
    /// </summary>
    [DisallowMultipleComponent]
    public class DMTalentTreeManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Master configuration asset containing all available talent nodes.")]
        [SerializeField] private TalentTreeConfigSO customConfig;

        [Header("System Dependencies")]
        [Tooltip("Reference to DungeonMaster for energy transactions and reset triggers.")]
        [SerializeField] private DungeonMaster dungeonMaster;

        [Tooltip("Reference to DungeonProgression for active gate rank requirement validation.")]
        [SerializeField] private DungeonProgression dungeonProgression;

        [Tooltip("Reference to CoreGameLoop for phase checks and round lifecycle integration.")]
        [SerializeField] private CoreGameLoop coreGameLoop;

        [Header("Runtime State (Read Only)")]
        [SerializeField, ReadOnly] private int unlockedTalentCount = 0;
        [SerializeField, ReadOnly] private int totalEnergySpentOnTalents = 0;

        // Internal State
        private readonly HashSet<string> _unlockedTalentIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _cooldownExpiryTimers = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TalentNodeSO> _unlockedNodes = new();

        #region Events

        /// <summary>Fired whenever a talent node is successfully unlocked.</summary>
        public event Action<TalentNodeSO> OnTalentUnlocked;

        /// <summary>Fired when an active talent power is triggered during battle.</summary>
        public event Action<TalentNodeSO> OnActivePowerTriggered;

        /// <summary>Fired when a talent node's availability state changes.</summary>
        public event Action<TalentNodeSO, TalentUnlockStatus> OnTalentStateChanged;

        /// <summary>Fired when the entire talent tree state is reset (e.g. new run).</summary>
        public event Action OnTalentTreeReset;

        #endregion

        #region Public Properties

        public TalentTreeConfigSO Config => customConfig;
        public IReadOnlyCollection<string> UnlockedTalentIds => _unlockedTalentIds;
        public IReadOnlyList<TalentNodeSO> UnlockedTalents => _unlockedNodes;
        public int UnlockedCount => _unlockedTalentIds.Count;
        public int TotalEnergySpent => totalEnergySpentOnTalents;

        #endregion

        private void Awake()
        {
            if (dungeonMaster == null)
            {
                dungeonMaster = GetComponent<DungeonMaster>() ?? FindAnyObjectByType<DungeonMaster>();
            }
            if (dungeonProgression == null)
            {
                dungeonProgression = GetComponent<DungeonProgression>() ?? FindAnyObjectByType<DungeonProgression>();
            }
            if (coreGameLoop == null)
            {
                coreGameLoop = GetComponent<CoreGameLoop>() ?? FindAnyObjectByType<CoreGameLoop>();
            }
            if (customConfig == null)
            {
                customConfig = Resources.Load<TalentTreeConfigSO>("Talents/DefaultTalentTreeConfig");
            }
        }

        private void OnEnable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandlePhaseChanged;
                coreGameLoop.OnGameOver += HandleGameOver;
            }
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated += HandleDefeated;
            }
        }

        private void OnDisable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandlePhaseChanged;
                coreGameLoop.OnGameOver -= HandleGameOver;
            }
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated -= HandleDefeated;
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime setups and headless unit testing without scene dependency.
        /// </summary>
        public void Initialize(
            DungeonMaster dm = null,
            DungeonProgression progression = null,
            CoreGameLoop loop = null,
            TalentTreeConfigSO config = null)
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandlePhaseChanged;
                coreGameLoop.OnGameOver -= HandleGameOver;
            }
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated -= HandleDefeated;
            }

            dungeonMaster = dm;
            dungeonProgression = progression;
            coreGameLoop = loop;
            customConfig = config != null ? config : Resources.Load<TalentTreeConfigSO>("Talents/DefaultTalentTreeConfig");

            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandlePhaseChanged;
                coreGameLoop.OnGameOver += HandleGameOver;
            }
            if (dungeonMaster != null)
            {
                dungeonMaster.OnDefeated += HandleDefeated;
            }

            ResetTalentTree();
        }

        #region Talent Query & Validation Engine

        /// <summary>
        /// Checks whether a talent is currently unlocked.
        /// </summary>
        public bool IsUnlocked(TalentNodeSO node)
        {
            return node != null && IsUnlocked(node.TalentId);
        }

        /// <summary>
        /// Checks whether a talent identifier is currently unlocked.
        /// </summary>
        public bool IsUnlocked(string talentId)
        {
            return !string.IsNullOrEmpty(talentId) && _unlockedTalentIds.Contains(talentId);
        }

        /// <summary>
        /// Evaluates the complete unlock status for a specific talent node.
        /// </summary>
        public TalentUnlockStatus GetTalentStatus(TalentNodeSO node)
        {
            if (node == null) return TalentUnlockStatus.Locked;
            if (IsUnlocked(node.TalentId)) return TalentUnlockStatus.Unlocked;

            // Check prerequisites
            if (!ArePrerequisitesMet(node)) return TalentUnlockStatus.Locked;

            // Check rank requirement
            var currentRank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            if (currentRank < node.MinimumRank) return TalentUnlockStatus.Locked;

            // Check energy affordability
            int availableEnergy = dungeonMaster != null ? dungeonMaster.Energy : 0;
            if (availableEnergy < node.EnergyCost)
            {
                return TalentUnlockStatus.AffordableOnlyWhenGained;
            }

            return TalentUnlockStatus.Available;
        }

        /// <summary>
        /// Evaluates whether a talent can be purchased and unlocked right now.
        /// </summary>
        /// <param name="node">The talent node to evaluate.</param>
        /// <param name="failureReason">Output string explaining why the unlock is not allowed.</param>
        /// <returns>True if all constraints pass, false otherwise.</returns>
        public bool CanUnlock(TalentNodeSO node, out string failureReason)
        {
            if (node == null)
            {
                failureReason = "Talent node is null.";
                return false;
            }

            if (IsUnlocked(node.TalentId))
            {
                failureReason = $"Talent '{node.DisplayName}' is already unlocked.";
                return false;
            }

            // Phase constraint check (Only allow purchases in Planning Phase if CoreGameLoop is active)
            if (coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.Planning && coreGameLoop.CurrentPhase != GamePhase.NotStarted)
            {
                failureReason = $"Talents can only be purchased during the Planning Phase (Current: {coreGameLoop.CurrentPhase}).";
                return false;
            }

            // Dungeon Rank Gate check
            var currentRank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            if (currentRank < node.MinimumRank)
            {
                failureReason = $"Requires Dungeon {node.MinimumRank.GetDisplayName()} (Current: {currentRank.GetDisplayName()}).";
                return false;
            }

            // Prerequisite check
            if (!ArePrerequisitesMet(node))
            {
                failureReason = "Prerequisite talents have not been unlocked.";
                return false;
            }

            // Energy check via DungeonMaster
            int availableEnergy = dungeonMaster != null ? dungeonMaster.Energy : 0;
            if (availableEnergy < node.EnergyCost)
            {
                failureReason = $"Insufficient Energy. Required: {node.EnergyCost}, Available: {availableEnergy}.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Evaluates whether a talent node's prerequisites are satisfied.
        /// </summary>
        public bool ArePrerequisitesMet(TalentNodeSO node)
        {
            if (node == null) return false;
            if (node.Prerequisites == null || node.Prerequisites.Count == 0) return true;

            if (node.PrerequisiteLogic == TalentPrerequisiteLogic.All)
            {
                for (int i = 0; i < node.Prerequisites.Count; i++)
                {
                    var prereq = node.Prerequisites[i];
                    if (prereq == null || !IsUnlocked(prereq.TalentId))
                    {
                        return false;
                    }
                }
                return true;
            }
            else // TalentPrerequisiteLogic.Any
            {
                for (int i = 0; i < node.Prerequisites.Count; i++)
                {
                    var prereq = node.Prerequisites[i];
                    if (prereq != null && IsUnlocked(prereq.TalentId))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion

        #region Unlocking & Power Execution

        /// <summary>
        /// Attempts to purchase and unlock a talent node.
        /// Atomically deducts energy from DungeonMaster and registers the unlock if valid.
        /// </summary>
        public bool TryUnlockTalent(TalentNodeSO node, out string failureReason)
        {
            if (!CanUnlock(node, out failureReason))
            {
                return false;
            }

            // Deduct Energy via DungeonMaster
            if (dungeonMaster != null && node.EnergyCost > 0)
            {
                bool deducted = dungeonMaster.SpendEnergy(node.EnergyCost);
                if (!deducted)
                {
                    failureReason = "Failed to deduct energy from DungeonMaster.";
                    return false;
                }
            }

            // Register unlock
            _unlockedTalentIds.Add(node.TalentId);
            _unlockedNodes.Add(node);
            totalEnergySpentOnTalents += node.EnergyCost;
            unlockedTalentCount = _unlockedTalentIds.Count;

            OnTalentUnlocked?.Invoke(node);
            OnTalentStateChanged?.Invoke(node, TalentUnlockStatus.Unlocked);

            // Notify status changes for dependent nodes
            NotifyDependentTalentStatusChanges();

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Parameterless overload for TryUnlockTalent.
        /// </summary>
        public bool TryUnlockTalent(TalentNodeSO node)
        {
            return TryUnlockTalent(node, out _);
        }

        /// <summary>
        /// Evaluates whether an active power talent can be triggered during the Battle phase.
        /// </summary>
        public bool CanActivatePower(TalentNodeSO node, out string failureReason)
        {
            if (node == null)
            {
                failureReason = "Talent node is null.";
                return false;
            }

            if (node.Type != TalentType.ActivePower)
            {
                failureReason = $"Talent '{node.DisplayName}' is not an active power.";
                return false;
            }

            if (!IsUnlocked(node.TalentId))
            {
                failureReason = $"Talent '{node.DisplayName}' must be unlocked before activation.";
                return false;
            }

            // Phase constraint check (Only allowed in Battle phase if CoreGameLoop is active)
            if (coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.Battle)
            {
                failureReason = $"Active powers can only be triggered during the Battle phase (Current: {coreGameLoop.CurrentPhase}).";
                return false;
            }

            // Cooldown check
            float remaining = GetRemainingCooldown(node);
            if (remaining > 0f)
            {
                failureReason = $"Active power is on cooldown ({remaining:F1}s remaining).";
                return false;
            }

            // Energy check
            int availableEnergy = dungeonMaster != null ? dungeonMaster.Energy : 0;
            if (availableEnergy < node.ActiveEnergyCost)
            {
                failureReason = $"Insufficient Energy to cast. Required: {node.ActiveEnergyCost}, Available: {availableEnergy}.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Triggers an active power during the Battle phase, consuming active energy and applying cooldown.
        /// </summary>
        public bool TryActivatePower(TalentNodeSO node, out string failureReason)
        {
            if (!CanActivatePower(node, out failureReason))
            {
                return false;
            }

            if (dungeonMaster != null && node.ActiveEnergyCost > 0)
            {
                bool deducted = dungeonMaster.SpendEnergy(node.ActiveEnergyCost);
                if (!deducted)
                {
                    failureReason = "Failed to deduct active energy cost.";
                    return false;
                }
            }

            if (node.CooldownDuration > 0f)
            {
                _cooldownExpiryTimers[node.TalentId] = Time.time + node.CooldownDuration;
            }

            OnActivePowerTriggered?.Invoke(node);
            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns remaining cooldown duration for an active talent in seconds (0 if ready).
        /// </summary>
        public float GetRemainingCooldown(TalentNodeSO node)
        {
            if (node == null) return 0f;
            if (_cooldownExpiryTimers.TryGetValue(node.TalentId, out float expiryTime))
            {
                float remaining = expiryTime - Time.time;
                return Mathf.Max(0f, remaining);
            }
            return 0f;
        }

        /// <summary>
        /// Debug/Cheat tool: forcefully unlocks a talent without energy or rank checks.
        /// </summary>
        public void ForceUnlock(TalentNodeSO node)
        {
            if (node == null || IsUnlocked(node.TalentId)) return;

            _unlockedTalentIds.Add(node.TalentId);
            _unlockedNodes.Add(node);
            unlockedTalentCount = _unlockedTalentIds.Count;

            OnTalentUnlocked?.Invoke(node);
            OnTalentStateChanged?.Invoke(node, TalentUnlockStatus.Unlocked);
            NotifyDependentTalentStatusChanges();
        }

        /// <summary>
        /// Resets the talent tree state for a new run.
        /// </summary>
        public void ResetTalentTree()
        {
            _unlockedTalentIds.Clear();
            _unlockedNodes.Clear();
            _cooldownExpiryTimers.Clear();
            totalEnergySpentOnTalents = 0;
            unlockedTalentCount = 0;

            OnTalentTreeReset?.Invoke();
            NotifyDependentTalentStatusChanges();
        }

        #endregion

        #region Stat & Synergy Aggregators (Extensibility Hooks)

        /// <summary>
        /// Calculates the total additive player modifier bonus granted by all unlocked talents.
        /// Intended for injection into <see cref="DifficultyScaler"/>.
        /// </summary>
        public float GetAggregatePlayerModifierBonus()
        {
            float bonus = 0f;
            for (int i = 0; i < _unlockedNodes.Count; i++)
            {
                var node = _unlockedNodes[i];
                if (node != null)
                {
                    bonus += node.PlayerModifierBonus;
                }
            }
            return bonus;
        }

        /// <summary>
        /// Calculates the total flat DM synergy defense power bonus granted by all unlocked talents.
        /// </summary>
        public int GetAggregateSynergyPowerBonus()
        {
            int bonus = 0;
            for (int i = 0; i < _unlockedNodes.Count; i++)
            {
                var node = _unlockedNodes[i];
                if (node != null)
                {
                    bonus += node.DmSynergyPowerBonus;
                }
            }
            return bonus;
        }

        /// <summary>
        /// Returns all talent nodes currently eligible to be unlocked.
        /// </summary>
        public List<TalentNodeSO> GetAvailableTalents()
        {
            var result = new List<TalentNodeSO>();
            var all = GetAllTalents();
            for (int i = 0; i < all.Count; i++)
            {
                var node = all[i];
                if (node != null && GetTalentStatus(node) == TalentUnlockStatus.Available)
                {
                    result.Add(node);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns all talent nodes defined in configuration.
        /// </summary>
        public IReadOnlyList<TalentNodeSO> GetAllTalents()
        {
            return customConfig != null ? customConfig.Talents : Array.Empty<TalentNodeSO>();
        }

        #endregion

        #region Private Helpers & Event Listeners

        private void NotifyDependentTalentStatusChanges()
        {
            if (customConfig == null || customConfig.Talents == null) return;

            foreach (var talent in customConfig.Talents)
            {
                if (talent != null)
                {
                    var status = GetTalentStatus(talent);
                    OnTalentStateChanged?.Invoke(talent, status);
                }
            }
        }

        private void HandlePhaseChanged(GamePhase newPhase)
        {
            // Reset active power cooldowns when transitioning out of battle
            if (newPhase == GamePhase.Planning)
            {
                _cooldownExpiryTimers.Clear();
            }
            NotifyDependentTalentStatusChanges();
        }

        private void HandleGameOver(GameOverReason reason, int roundNumber)
        {
            ResetTalentTree();
        }

        private void HandleDefeated(DefeatReason reason)
        {
            ResetTalentTree();
        }

        #endregion
    }
}
