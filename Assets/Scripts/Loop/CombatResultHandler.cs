using System;
using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SBGames.Characters.AI;
using SBGames.Loop.UI;
using SBGames.Squads;
using UnityEngine;

namespace SBGames.Loop
{
    public enum AdventurerCombatState
    {
        Alive = 0,
        Dead = 1,
        Retreated = 2,
        Escaped = 3
    }

    public enum CombatState
    {
        Idle = 0,
        CombatActive = 1,
        CombatEnded = 2,
        AwaitingAcknowledgment = 3
    }

    [Serializable]
    public class AdventurerCombatRecord
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private float initialHealth;
        [SerializeField] private float finalHealth;
        [SerializeField] private float damageTaken;
        [SerializeField] private AdventurerCombatState state;

        public string Id => id;
        public string Name => name;
        public float InitialHealth => initialHealth;
        public float FinalHealth { get => finalHealth; set => finalHealth = Mathf.Max(0f, value); }
        public float DamageTaken { get => damageTaken; set => damageTaken = Mathf.Max(0f, value); }
        public AdventurerCombatState State { get => state; set => state = value; }

        public bool IsAlive => state == AdventurerCombatState.Alive;
        public bool IsDead => state == AdventurerCombatState.Dead;
        public bool HasRetreated => state == AdventurerCombatState.Retreated;
        public bool HasEscaped => state == AdventurerCombatState.Escaped;

        public AdventurerCombatRecord(string id, string name, float initialHealth)
        {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            this.name = string.IsNullOrEmpty(name) ? "Adventurer" : name;
            this.initialHealth = Mathf.Max(1f, initialHealth);
            this.finalHealth = this.initialHealth;
            this.damageTaken = 0f;
            this.state = AdventurerCombatState.Alive;
        }
    }

    [Serializable]
    public class BossCombatRecord
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private float maxHealth;
        [SerializeField] private float currentHealth;
        [SerializeField] private float damageTaken;

        public string Id => id;
        public string Name => name;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get => currentHealth; set => currentHealth = Mathf.Clamp(value, 0f, maxHealth); }
        public float DamageTaken { get => damageTaken; set => damageTaken = Mathf.Max(0f, value); }
        public bool IsAlive => currentHealth > 0f;

        public BossCombatRecord(string id, string name, float maxHealth, float currentHealth = -1f)
        {
            this.id = string.IsNullOrEmpty(id) ? "FinalBoss" : id;
            this.name = string.IsNullOrEmpty(name) ? "Final Boss" : name;
            this.maxHealth = Mathf.Max(1f, maxHealth);
            this.currentHealth = currentHealth < 0f ? this.maxHealth : Mathf.Clamp(currentHealth, 0f, this.maxHealth);
            this.damageTaken = 0f;
        }
    }

    public readonly struct CombatResultData
    {
        public readonly BattleOutcome Outcome;
        public readonly int RoundNumber;
        public readonly float DurationSeconds;
        public readonly int TotalAdventurers;
        public readonly int DefeatedAdventurers;
        public readonly int RetreatedAdventurers;
        public readonly int EscapedAdventurers;
        public readonly int SurvivingAdventurers;
        public readonly IReadOnlyList<AdventurerCombatRecord> AdventurerRecords;
        public readonly BossCombatRecord BossRecord;
        public readonly int? EnergyReward;
        public readonly int? EssenceDamage;
        public readonly string SummaryMessage;
        public readonly PostBattleReport Report;

        public CombatResultData(
            BattleOutcome outcome,
            int roundNumber,
            float durationSeconds,
            int totalAdventurers,
            int defeatedAdventurers,
            int retreatedAdventurers,
            int survivingAdventurers,
            IReadOnlyList<AdventurerCombatRecord> adventurerRecords,
            BossCombatRecord bossRecord,
            int? energyReward,
            int? essenceDamage,
            string summaryMessage)
            : this(outcome, roundNumber, durationSeconds, totalAdventurers, defeatedAdventurers, retreatedAdventurers, 0, survivingAdventurers, adventurerRecords, bossRecord, energyReward, essenceDamage, summaryMessage, null)
        {
        }

        public CombatResultData(
            BattleOutcome outcome,
            int roundNumber,
            float durationSeconds,
            int totalAdventurers,
            int defeatedAdventurers,
            int retreatedAdventurers,
            int escapedAdventurers,
            int survivingAdventurers,
            IReadOnlyList<AdventurerCombatRecord> adventurerRecords,
            BossCombatRecord bossRecord,
            int? energyReward,
            int? essenceDamage,
            string summaryMessage,
            PostBattleReport report = null)
        {
            Outcome = outcome;
            RoundNumber = roundNumber;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            TotalAdventurers = totalAdventurers;
            DefeatedAdventurers = defeatedAdventurers;
            RetreatedAdventurers = retreatedAdventurers;
            EscapedAdventurers = escapedAdventurers;
            SurvivingAdventurers = survivingAdventurers;
            AdventurerRecords = adventurerRecords ?? Array.Empty<AdventurerCombatRecord>();
            BossRecord = bossRecord;
            EnergyReward = energyReward;
            EssenceDamage = essenceDamage;
            SummaryMessage = summaryMessage ?? string.Empty;
            Report = report;
        }
    }

    [DisallowMultipleComponent]
    public class CombatResultHandler : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonMaster dungeonMaster;

        [Header("Configuration")]
        [SerializeField] private bool autoTrackOnBattlePhase = true;
        [SerializeField] private bool autoDiscoverUnitsOnStart = true;
        [SerializeField] private float defaultBossHealth = 1000f;

        [Header("Runtime State (Read Only)")]
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private CombatState currentCombatState = CombatState.Idle;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int currentRound = 1;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private float combatStartTime;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private float combatEndTime;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private float combatDuration;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int totalAdventurersCount;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int defeatedAdventurersCount;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int retreatedAdventurersCount;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int escapedAdventurersCount;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private int activeAdventurersCount;
        [SerializeField, ArtificeToolkit.Attributes.ReadOnly] private bool isBossAlive = true;

        [Header("Post-Battle Telemetry (Phase 7)")]
        [SerializeField] private PostBattleReport currentReport = new();

        public bool IsRetreatBlocked
        {
            get
            {
                var leader = FindAnyObjectByType<PartyLeader>();
                return leader != null && leader.RetreatBlocked;
            }
        }

        [Header("Debug Controls")]
        [SerializeField] private int debugKills = 3;
        [SerializeField] private int debugRetreats = 1;

        private readonly Dictionary<string, AdventurerCombatRecord> trackedAdventurers = new();
        private readonly Dictionary<string, CharacterStatsManager> adventurerStatsBindings = new();
        private BossCombatRecord trackedBoss;
        private CharacterStatsManager bossStatsBinding;
        private BattleOutcome? evaluatedOutcome;
        private CombatResultData? lastResultData;

        public CombatState CurrentCombatState => currentCombatState;
        public int CurrentRound => currentRound;
        public float CombatDuration => currentCombatState == CombatState.CombatActive ? (Time.time - combatStartTime) : combatDuration;
        public int TotalAdventurersCount => totalAdventurersCount;
        public int DefeatedAdventurersCount => defeatedAdventurersCount;
        public int RetreatedAdventurersCount => retreatedAdventurersCount;
        public int EscapedAdventurersCount => escapedAdventurersCount;
        public int ActiveAdventurersCount => activeAdventurersCount;
        public PostBattleReport CurrentReport => currentReport;
        public bool IsBossAlive => trackedBoss == null || trackedBoss.IsAlive;
        public BossCombatRecord BossRecord => trackedBoss;
        public IReadOnlyDictionary<string, AdventurerCombatRecord> TrackedAdventurers => trackedAdventurers;
        public BattleOutcome? EvaluatedOutcome => evaluatedOutcome;
        public CombatResultData? LastResultData => lastResultData;

        public event Action<CombatResultData> OnShowResultsScreen;
        public event Action<CombatState> OnCombatStateChanged;
        public event Action<string, AdventurerCombatState> OnAdventurerStateChanged;
        public event Action<float, float> OnBossHealthChanged;
        public event Action OnCombatReset;

        private void Awake()
        {
            if (coreGameLoop == null)
            {
                coreGameLoop = GetComponent<CoreGameLoop>() ?? FindAnyObjectByType<CoreGameLoop>();
            }

            if (dungeonMaster == null)
            {
                dungeonMaster = GetComponent<DungeonMaster>() ?? FindAnyObjectByType<DungeonMaster>();
            }
        }

        private void OnEnable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandlePhaseChanged;
            }

            SubscribeEventHub();
        }

        private void OnDisable()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandlePhaseChanged;
            }

            UnsubscribeEventHub();
            UnbindAllEventHooks();
        }

        public void Initialize(
            CoreGameLoop loop = null,
            DungeonMaster dm = null,
            bool autoTrack = true,
            bool autoDiscover = false,
            float defaultBossHp = 1000f)
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandlePhaseChanged;
            }

            coreGameLoop = loop;
            dungeonMaster = dm;
            autoTrackOnBattlePhase = autoTrack;
            autoDiscoverUnitsOnStart = autoDiscover;
            defaultBossHealth = Mathf.Max(1f, defaultBossHp);

            if (coreGameLoop != null && enabled)
            {
                coreGameLoop.OnPhaseChanged += HandlePhaseChanged;
            }

            ResetCombatState();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (!autoTrackOnBattlePhase) return;

            if (phase == GamePhase.Battle)
            {
                int round = coreGameLoop != null ? coreGameLoop.CurrentRound : 1;
                BeginCombat(round);
            }
            else if (phase == GamePhase.Planning && currentCombatState != CombatState.Idle)
            {
                ResetCombatState();
            }
        }

        #region Combat Lifecycle & Registration

        public void BeginCombat(int roundNumber = 1)
        {
            ResetCombatState();

            currentRound = Mathf.Max(1, roundNumber);
            combatStartTime = Time.time;
            combatEndTime = 0f;
            combatDuration = 0f;
            SetState(CombatState.CombatActive);

            if (trackedBoss == null)
            {
                RegisterBoss("Boss_Primary", "Dungeon Master Guardian", defaultBossHealth);
            }

            if (autoDiscoverUnitsOnStart)
            {
                DiscoverActiveUnitsFromCharacterManager();
            }

            Debug.Log($"[CombatResultHandler] Combat started for Round {currentRound}.");
        }

        public bool RegisterAdventurer(string id, string name, float maxHealth, CharacterStatsManager stats = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[CombatResultHandler] Cannot register adventurer with null or empty ID.");
                return false;
            }

            if (trackedAdventurers.ContainsKey(id))
            {
                Debug.LogWarning($"[CombatResultHandler] Adventurer with ID '{id}' is already registered.");
                return false;
            }

            var record = new AdventurerCombatRecord(id, name, maxHealth);
            trackedAdventurers.Add(id, record);

            if (stats != null)
            {
                adventurerStatsBindings[id] = stats;
                stats.OnLifeChanged += (curr, max) => HandleAdventurerHealthChanged(id, curr, max);
            }

            RecalculateAdventurerCounts();
            Debug.Log($"[CombatResultHandler] Registered Adventurer '{name}' (ID: {id}, Max HP: {maxHealth}). Total active: {activeAdventurersCount}");
            return true;
        }

        public bool RegisterAdventurer(CharacterBrain brain)
        {
            if (brain == null) return false;

            string id = brain.gameObject.name;
            if (brain.TryGetComponent<CharacterIdentifier>(out var identifier) && !string.IsNullOrEmpty(identifier.UniqueId))
            {
                id = identifier.UniqueId;
            }

            var stats = brain.GetComponent<CharacterStatsManager>();
            float maxHp = stats != null && stats.MaxLife != null ? stats.MaxLife.Value : 100f;
            return RegisterAdventurer(id, brain.name, maxHp, stats);
        }

        public bool RegisterBoss(string id, string name, float maxHealth, CharacterStatsManager stats = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[CombatResultHandler] Cannot register boss with null or empty ID.");
                return false;
            }

            if (bossStatsBinding != null)
            {
                bossStatsBinding.OnLifeChanged -= HandleBossHealthChanged;
                bossStatsBinding = null;
            }

            trackedBoss = new BossCombatRecord(id, name, maxHealth);
            isBossAlive = trackedBoss.IsAlive;

            if (stats != null)
            {
                bossStatsBinding = stats;
                stats.OnLifeChanged += HandleBossHealthChanged;
            }

            OnBossHealthChanged?.Invoke(trackedBoss.CurrentHealth, trackedBoss.MaxHealth);
            Debug.Log($"[CombatResultHandler] Registered Final Boss '{name}' (ID: {id}, Max HP: {maxHealth}).");
            return true;
        }

        public bool RegisterBoss(CharacterBrain brain)
        {
            if (brain == null) return false;

            string id = brain.gameObject.name;
            if (brain.TryGetComponent<CharacterIdentifier>(out var identifier) && !string.IsNullOrEmpty(identifier.UniqueId))
            {
                id = identifier.UniqueId;
            }

            var stats = brain.GetComponent<CharacterStatsManager>();
            float maxHp = stats != null && stats.MaxLife != null ? stats.MaxLife.Value : defaultBossHealth;
            return RegisterBoss(id, brain.name, maxHp, stats);
        }

        public bool UnregisterAdventurer(string id)
        {
            if (string.IsNullOrEmpty(id) || !trackedAdventurers.TryGetValue(id, out var record))
            {
                return false;
            }

            if (adventurerStatsBindings.TryGetValue(id, out var stats) && stats != null)
            {
                adventurerStatsBindings.Remove(id);
            }

            trackedAdventurers.Remove(id);
            RecalculateAdventurerCounts();

            if (currentCombatState == CombatState.CombatActive)
            {
                EvaluateCombatOutcome(out _);
            }

            return true;
        }

        #endregion

        #region Event Ingestion & State Updates

        public bool ReportAdventurerDamage(string id, float damageTaken, float remainingHealth)
        {
            if (string.IsNullOrEmpty(id) || !trackedAdventurers.TryGetValue(id, out var record))
            {
                return false;
            }

            record.DamageTaken += Mathf.Max(0f, damageTaken);
            record.FinalHealth = Mathf.Max(0f, remainingHealth);

            if (record.FinalHealth <= 0f && record.State == AdventurerCombatState.Alive)
            {
                return ReportAdventurerDeath(id);
            }

            return true;
        }

        public bool ReportAdventurerDeath(string id)
        {
            if (string.IsNullOrEmpty(id) || !trackedAdventurers.TryGetValue(id, out var record))
            {
                Debug.LogWarning($"[CombatResultHandler] ReportAdventurerDeath ignored: Adventurer '{id}' is not registered.");
                return false;
            }

            if (record.State == AdventurerCombatState.Dead)
            {
                return true;
            }

            record.State = AdventurerCombatState.Dead;
            record.FinalHealth = 0f;
            RecalculateAdventurerCounts();

            Debug.Log($"[CombatResultHandler] Adventurer '{record.Name}' ({id}) died. Remaining active: {activeAdventurersCount}/{totalAdventurersCount}");
            OnAdventurerStateChanged?.Invoke(id, AdventurerCombatState.Dead);

            if (currentCombatState == CombatState.CombatActive)
            {
                EvaluateCombatOutcome(out _);
            }

            return true;
        }

        public bool ReportAdventurerRetreat(string id)
        {
            if (string.IsNullOrEmpty(id) || !trackedAdventurers.TryGetValue(id, out var record))
            {
                Debug.LogWarning($"[CombatResultHandler] ReportAdventurerRetreat ignored: Adventurer '{id}' is not registered.");
                return false;
            }

            if (record.State == AdventurerCombatState.Retreated || record.State == AdventurerCombatState.Dead)
            {
                return true;
            }

            record.State = AdventurerCombatState.Retreated;
            RecalculateAdventurerCounts();

            Debug.Log($"[CombatResultHandler] Adventurer '{record.Name}' ({id}) retreated. Remaining active: {activeAdventurersCount}/{totalAdventurersCount}");
            OnAdventurerStateChanged?.Invoke(id, AdventurerCombatState.Retreated);

            if (currentCombatState == CombatState.CombatActive)
            {
                EvaluateCombatOutcome(out _);
            }

            return true;
        }

        public bool ReportAdventurerEscape(string id, Vector3 exitPosition = default)
        {
            if (string.IsNullOrEmpty(id) || !trackedAdventurers.TryGetValue(id, out var record))
            {
                Debug.LogWarning($"[CombatResultHandler] ReportAdventurerEscape ignored: Adventurer '{id}' is not registered.");
                return false;
            }

            if (record.State == AdventurerCombatState.Escaped || record.State == AdventurerCombatState.Dead)
            {
                return true;
            }

            record.State = AdventurerCombatState.Escaped;
            RecalculateAdventurerCounts();

            currentReport?.RecordEscape(id, record.Name, Time.time - combatStartTime);

            Debug.Log($"[CombatResultHandler] Adventurer '{record.Name}' ({id}) escaped through entrance. Remaining active: {activeAdventurersCount}/{totalAdventurersCount}");
            OnAdventurerStateChanged?.Invoke(id, AdventurerCombatState.Escaped);

            if (currentCombatState == CombatState.CombatActive)
            {
                EvaluateCombatOutcome(out _);
            }

            return true;
        }

        public bool ReportBossDamage(float damageTaken, float remainingHealth)
        {
            if (trackedBoss == null)
            {
                RegisterBoss("Boss_Primary", "Dungeon Master Guardian", defaultBossHealth);
            }

            trackedBoss.DamageTaken += Mathf.Max(0f, damageTaken);
            trackedBoss.CurrentHealth = Mathf.Clamp(remainingHealth, 0f, trackedBoss.MaxHealth);
            isBossAlive = trackedBoss.IsAlive;

            OnBossHealthChanged?.Invoke(trackedBoss.CurrentHealth, trackedBoss.MaxHealth);

            if (!isBossAlive)
            {
                return ReportBossDeath();
            }

            return true;
        }

        public bool ReportBossDeath()
        {
            if (trackedBoss == null)
            {
                RegisterBoss("Boss_Primary", "Dungeon Master Guardian", defaultBossHealth);
            }

            trackedBoss.CurrentHealth = 0f;
            isBossAlive = false;

            OnBossHealthChanged?.Invoke(0f, trackedBoss.MaxHealth);
            Debug.LogWarning("[CombatResultHandler] Final Boss was killed!");

            if (currentCombatState == CombatState.CombatActive)
            {
                EvaluateCombatOutcome(out _);
            }

            return true;
        }

        #endregion

        #region Victory & Defeat Evaluation

        public bool EvaluateCombatOutcome(out BattleOutcome? outcome)
        {
            outcome = null;

            if (currentCombatState != CombatState.CombatActive)
            {
                return false;
            }

            if (trackedBoss != null && !trackedBoss.IsAlive)
            {
                outcome = BattleOutcome.Defeat;
                ConcludeCombat(BattleOutcome.Defeat, "Final Boss was destroyed. Dungeon defense breached!");
                return true;
            }

            if (totalAdventurersCount > 0 && activeAdventurersCount == 0)
            {
                outcome = BattleOutcome.Victory;
                string victoryDetails = (defeatedAdventurersCount == totalAdventurersCount)
                    ? "All adventurers were slain!"
                    : $"All adventurers neutralized ({defeatedAdventurersCount} slain, {retreatedAdventurersCount} retreated, {escapedAdventurersCount} escaped).";

                ConcludeCombat(BattleOutcome.Victory, $"Dungeon successfully defended! {victoryDetails}");
                return true;
            }

            return false;
        }

        private void ConcludeCombat(BattleOutcome outcome, string summaryMessage)
        {
            if (currentCombatState != CombatState.CombatActive)
            {
                return;
            }

            combatEndTime = Time.time;
            combatDuration = Mathf.Max(0f, combatEndTime - combatStartTime);
            evaluatedOutcome = outcome;

            SetState(CombatState.CombatEnded);

            int? energyReward = outcome == BattleOutcome.Victory ? null : 0;
            int? essencePenalty = outcome == BattleOutcome.Defeat ? (dungeonMaster != null ? dungeonMaster.RoundLossEssencePenalty : 25) : 0;

            var adventurerList = new List<AdventurerCombatRecord>(trackedAdventurers.Values);
            var result = new CombatResultData(
                outcome,
                currentRound,
                combatDuration,
                totalAdventurersCount,
                defeatedAdventurersCount,
                retreatedAdventurersCount,
                escapedAdventurersCount,
                activeAdventurersCount,
                adventurerList,
                trackedBoss,
                energyReward,
                essencePenalty,
                summaryMessage,
                currentReport);

            lastResultData = result;
            SetState(CombatState.AwaitingAcknowledgment);

            Debug.Log($"[CombatResultHandler] Combat Concluded! Outcome: {outcome} | Round {currentRound} | Duration: {combatDuration:F1}s | Message: {summaryMessage}");

            // Ensure UI is active and listening
            var ui = CombatResultsUIController.Instance ?? FindAnyObjectByType<CombatResultsUIController>(FindObjectsInactive.Include);
            if (ui == null)
            {
                var go = new GameObject("[CombatResultsUI]");
                ui = go.AddComponent<CombatResultsUIController>();
            }

            if (ui != null)
            {
                ui.Initialize(this, coreGameLoop);
            }

            OnShowResultsScreen?.Invoke(result);
        }

        public BattleResolutionResult AcknowledgeResultsAndProceed()
        {
            if (!lastResultData.HasValue)
            {
                Debug.LogWarning("[CombatResultHandler] Cannot acknowledge results: No active combat result found.");
                return default;
            }

            var data = lastResultData.Value;
            BattleResolutionResult resolution = default;

            if (coreGameLoop != null)
            {
                var battleResult = data.Outcome == BattleOutcome.Victory
                    ? BattleResult.Win(data.EnergyReward)
                    : BattleResult.Loss(data.EssenceDamage);

                resolution = coreGameLoop.ResolveBattle(battleResult);
            }
            else
            {
                Debug.LogWarning("[CombatResultHandler] CoreGameLoop reference is null during acknowledgment. Simulating fallback resolution.");
                resolution = new BattleResolutionResult(data.RoundNumber, data.Outcome, data.EssenceDamage ?? 0, data.EnergyReward ?? 0, false, GameOverReason.None);
            }

            ResetCombatState();
            return resolution;
        }

        public void ResetCombatState()
        {
            UnbindAllEventHooks();
            trackedAdventurers.Clear();
            adventurerStatsBindings.Clear();

            totalAdventurersCount = 0;
            defeatedAdventurersCount = 0;
            retreatedAdventurersCount = 0;
            escapedAdventurersCount = 0;
            activeAdventurersCount = 0;

            currentReport?.Clear();

            if (trackedBoss != null)
            {
                trackedBoss = new BossCombatRecord(trackedBoss.Id, trackedBoss.Name, trackedBoss.MaxHealth, trackedBoss.MaxHealth);
                isBossAlive = true;
            }

            evaluatedOutcome = null;
            lastResultData = null;
            SetState(CombatState.Idle);

            OnCombatReset?.Invoke();
            Debug.Log("[CombatResultHandler] Combat state reset to Idle.");
        }

        #endregion

        #region Helpers & Auto-Discovery

        private void SetState(CombatState newState)
        {
            if (currentCombatState == newState) return;
            currentCombatState = newState;
            OnCombatStateChanged?.Invoke(currentCombatState);
        }

        private void RecalculateAdventurerCounts()
        {
            totalAdventurersCount = trackedAdventurers.Count;
            int dead = 0;
            int retreated = 0;
            int escaped = 0;
            int alive = 0;

            foreach (var kvp in trackedAdventurers)
            {
                switch (kvp.Value.State)
                {
                    case AdventurerCombatState.Dead:
                        dead++;
                        break;
                    case AdventurerCombatState.Retreated:
                        retreated++;
                        break;
                    case AdventurerCombatState.Escaped:
                        escaped++;
                        break;
                    case AdventurerCombatState.Alive:
                    default:
                        alive++;
                        break;
                }
            }

            defeatedAdventurersCount = dead;
            retreatedAdventurersCount = retreated;
            escapedAdventurersCount = escaped;
            activeAdventurersCount = alive;
        }

        private void HandleAdventurerHealthChanged(string id, float currentHealth, float maxHealth)
        {
            if (!trackedAdventurers.TryGetValue(id, out var record)) return;

            record.FinalHealth = currentHealth;
            if (currentHealth <= 0f && record.State == AdventurerCombatState.Alive)
            {
                ReportAdventurerDeath(id);
            }
        }

        private void HandleBossHealthChanged(float currentHealth, float maxHealth)
        {
            if (trackedBoss == null) return;

            trackedBoss.CurrentHealth = currentHealth;
            isBossAlive = trackedBoss.IsAlive;
            OnBossHealthChanged?.Invoke(currentHealth, maxHealth);

            if (!isBossAlive && currentCombatState == CombatState.CombatActive)
            {
                ReportBossDeath();
            }
        }

        private void UnbindAllEventHooks()
        {
            foreach (var kvp in adventurerStatsBindings)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.OnLifeChanged -= (curr, max) => HandleAdventurerHealthChanged(kvp.Key, curr, max);
                }
            }

            if (bossStatsBinding != null)
            {
                bossStatsBinding.OnLifeChanged -= HandleBossHealthChanged;
                bossStatsBinding = null;
            }
        }

        #region Event Hub Subscriptions & Telemetry

        private void SubscribeEventHub()
        {
            AdventurerEventHub.OnLeaderRetreatBlocked += HandleLeaderRetreatBlocked;
            AdventurerEventHub.OnPotionConsumed += HandlePotionConsumed;
            AdventurerEventHub.OnRoomEntered += HandleRoomEntered;
            AdventurerEventHub.OnRoomCleared += HandleRoomCleared;
            AdventurerEventHub.OnCheckpointDecision += HandleCheckpointDecision;
            AdventurerEventHub.OnLeaderDied += HandleLeaderDied;
            AdventurerEventHub.OnLeaderChanged += HandleLeaderChanged;
            AdventurerEventHub.OnUnitEscaped += HandleUnitEscaped;
            AdventurerEventHub.OnWarpStarted += HandleWarpStarted;
            AdventurerEventHub.OnWarpCompleted += HandleWarpCompleted;
            AdventurerEventHub.OnWarpInterrupted += HandleWarpInterrupted;
            AdventurerEventHub.OnWarpItemForfeited += HandleWarpItemForfeited;
        }

        private void UnsubscribeEventHub()
        {
            AdventurerEventHub.OnLeaderRetreatBlocked -= HandleLeaderRetreatBlocked;
            AdventurerEventHub.OnPotionConsumed -= HandlePotionConsumed;
            AdventurerEventHub.OnRoomEntered -= HandleRoomEntered;
            AdventurerEventHub.OnRoomCleared -= HandleRoomCleared;
            AdventurerEventHub.OnCheckpointDecision -= HandleCheckpointDecision;
            AdventurerEventHub.OnLeaderDied -= HandleLeaderDied;
            AdventurerEventHub.OnLeaderChanged -= HandleLeaderChanged;
            AdventurerEventHub.OnUnitEscaped -= HandleUnitEscaped;
            AdventurerEventHub.OnWarpStarted -= HandleWarpStarted;
            AdventurerEventHub.OnWarpCompleted -= HandleWarpCompleted;
            AdventurerEventHub.OnWarpInterrupted -= HandleWarpInterrupted;
            AdventurerEventHub.OnWarpItemForfeited -= HandleWarpItemForfeited;
        }

        private void HandleLeaderRetreatBlocked(LeaderRetreatBlockedEvent e)
        {
            string leaderName = e.Leader != null ? e.Leader.name : "Leader";
            currentReport?.RecordBlockedRetreat(leaderName, e.RefusalReason, e.Timestamp - combatStartTime);
        }

        private void HandlePotionConsumed(PotionConsumedEvent e)
        {
            currentReport?.RecordPotionUsed(e.UserName, e.Category, e.ItemId, e.Timestamp - combatStartTime);
        }

        private void HandleRoomEntered(RoomEnteredEvent e)
        {
            currentReport?.RecordRoomEntered(e.RoomId, e.Timestamp - combatStartTime);
        }

        private void HandleRoomCleared(RoomClearedEvent e)
        {
            currentReport?.RecordRoomCleared(e.RoomId, e.Timestamp - combatStartTime);
        }

        private void HandleCheckpointDecision(CheckpointDecisionEvent e)
        {
            string leaderName = e.Leader != null ? e.Leader.name : "Leader";
            currentReport?.RecordCheckpoint(leaderName, e.Decision, e.Reason, e.Timestamp - combatStartTime);
        }

        private void HandleLeaderDied(LeaderDiedEvent e)
        {
            string leaderName = e.Leader != null ? e.Leader.name : "Leader";
            currentReport?.RecordDeath(leaderName, e.Position, e.Timestamp - combatStartTime);
        }

        private void HandleLeaderChanged(LeaderChangedEvent e)
        {
            string oldLeader = e.OldLeader != null ? e.OldLeader.name : "None";
            string newLeader = e.NewLeader != null ? e.NewLeader.name : "None";
            currentReport?.RecordLeaderChange(oldLeader, newLeader, e.Timestamp - combatStartTime);
        }

        private void HandleUnitEscaped(UnitEscapedEvent e)
        {
            currentReport?.RecordEscape(e.UnitId, e.UnitName, e.Timestamp - combatStartTime);
            ReportAdventurerEscape(e.UnitId, e.EscapePosition);
        }

        private void HandleWarpStarted(AdventurerWarpEventData e)
        {
            currentReport?.RecordWarpUsage(e.UserName, e.ItemId, "Started", e.Timestamp - combatStartTime);
        }

        private void HandleWarpCompleted(AdventurerWarpEventData e)
        {
            currentReport?.RecordWarpUsage(e.UserName, e.ItemId, "Completed", e.Timestamp - combatStartTime);
        }

        private void HandleWarpInterrupted(AdventurerWarpEventData e)
        {
            currentReport?.RecordWarpUsage(e.UserName, e.ItemId, "Interrupted", e.Timestamp - combatStartTime);
        }

        private void HandleWarpItemForfeited(AdventurerWarpForfeitEventData e)
        {
            currentReport?.RecordWarpForfeit(e.UserName, e.ItemId, e.EnergyAwarded, e.Timestamp - combatStartTime);
        }

        #endregion

        private void DiscoverActiveUnitsFromCharacterManager()
        {
            if (CharacterManager.Instance == null) return;

            var allCharacters = CharacterManager.Instance.GetAllCharacters();
            if (allCharacters == null) return;

            foreach (var character in allCharacters)
            {
                if (character == null) continue;

                // Ignore friendly monsters
                if (character.GetComponent<SBGames.Shop.Unity.MonsterRuntimeInstance>() != null || character.CompareTag("Monster") || character.name.Contains("Monster"))
                {
                    continue;
                }

                if (character.CompareTag("Boss") || character.name.Contains("Boss"))
                {
                    RegisterBoss(character);
                }
                else if (character.CompareTag("Adventurer") || character.name.Contains("Adventurer") || character.name.Contains("Hero") || character.name.Contains("Enemy"))
                {
                    RegisterAdventurer(character);
                }
            }
        }

        #endregion

        #region Debug Simulation Controls

        [ContextMenu("Debug/Simulate Victory")]
        [Button]
        public void DebugSimulateVictory()
        {
            DebugSimulateVictory(debugKills, debugRetreats);
        }

        public void DebugSimulateVictory(int kills, int retreats)
        {
            int effectiveKills = Mathf.Max(0, kills);
            int effectiveRetreats = Mathf.Max(0, retreats);
            if (effectiveKills == 0 && effectiveRetreats == 0)
            {
                effectiveKills = 3;
                effectiveRetreats = 1;
            }

            // Safety check: Ensure CoreGameLoop is in Battle phase
            if (coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.Battle)
            {
                Debug.Log("[CombatResultHandler Debug] Not in Battle phase; forcing CoreGameLoop to Battle phase.");
                coreGameLoop.DebugStartBattlePhase();
            }

            Debug.Log($"[CombatResultHandler Debug] Simulating Victory with {effectiveKills} kills and {effectiveRetreats} retreats...");

            if (currentCombatState != CombatState.CombatActive)
            {
                BeginCombat(currentRound);
            }

            // 1. Defeat any already tracked adventurers from the scene
            var existingKeys = new List<string>(trackedAdventurers.Keys);
            foreach (var key in existingKeys)
            {
                if (trackedAdventurers.TryGetValue(key, out var record) && record.State == AdventurerCombatState.Alive)
                {
                    record.State = AdventurerCombatState.Dead;
                    record.FinalHealth = 0f;
                }
            }

            // 2. Register mock adventurers
            int total = effectiveKills + effectiveRetreats;
            for (int i = 1; i <= total; i++)
            {
                string id = $"mock_adv_{i}";
                if (!trackedAdventurers.ContainsKey(id))
                {
                    RegisterAdventurer(id, $"Mock Adventurer #{i}", 100f);
                }
            }

            // 3. Mark kills
            for (int i = 1; i <= effectiveKills; i++)
            {
                string id = $"mock_adv_{i}";
                if (trackedAdventurers.TryGetValue(id, out var rec))
                {
                    rec.State = AdventurerCombatState.Dead;
                    rec.FinalHealth = 0f;
                }
            }

            // 4. Mark retreats
            for (int i = effectiveKills + 1; i <= total; i++)
            {
                string id = $"mock_adv_{i}";
                if (trackedAdventurers.TryGetValue(id, out var rec))
                {
                    rec.State = AdventurerCombatState.Retreated;
                }
            }

            // 5. Ensure final counts are updated and force evaluation
            RecalculateAdventurerCounts();
            EvaluateCombatOutcome(out var outcome);
            Debug.Log($"[CombatResultHandler Debug] Victory simulation evaluated: {outcome}");
        }

        [ContextMenu("Debug/Simulate Defeat")]
        [Button]
        public void DebugSimulateDefeat()
        {
            // Safety check: Ensure CoreGameLoop is in Battle phase
            if (coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.Battle)
            {
                Debug.Log("[CombatResultHandler Debug] Not in Battle phase; forcing CoreGameLoop to Battle phase.");
                coreGameLoop.DebugStartBattlePhase();
            }

            Debug.Log("[CombatResultHandler Debug] Simulating Player Defeat (Boss Death)...");
            BeginCombat(currentRound);

            RegisterAdventurer("mock_adv_survivor", "Mock Hero", 200f);
            ReportBossDeath();
        }

        [ContextMenu("Debug/Simulate Simultaneous Tie-Break")]
        [Button]
        public void DebugSimulateSimultaneous()
        {
            // Safety check: Ensure CoreGameLoop is in Battle phase
            if (coreGameLoop != null && coreGameLoop.CurrentPhase != GamePhase.Battle)
            {
                Debug.Log("[CombatResultHandler Debug] Not in Battle phase; forcing CoreGameLoop to Battle phase.");
                coreGameLoop.DebugStartBattlePhase();
            }

            Debug.Log("[CombatResultHandler Debug] Simulating simultaneous Boss Death + Adventurer Wipe...");
            BeginCombat(currentRound);

            RegisterAdventurer("mock_adv_last", "Last Standing Adventurer", 50f);

            if (trackedAdventurers.TryGetValue("mock_adv_last", out var record))
            {
                record.State = AdventurerCombatState.Dead;
                RecalculateAdventurerCounts();
            }

            if (trackedBoss != null)
            {
                trackedBoss.CurrentHealth = 0f;
                isBossAlive = false;
            }

            EvaluateCombatOutcome(out var outcome);
            Debug.Log($"[CombatResultHandler Debug] Simultaneous evaluation resulted in: {outcome}");
        }

        [ContextMenu("Debug/Acknowledge Results & Proceed")]
        [Button]
        public void DebugAcknowledgeAndProceed()
        {
            Debug.Log("[CombatResultHandler Debug] Acknowledging Results Screen and proceeding...");
            AcknowledgeResultsAndProceed();
        }

        [ContextMenu("Debug/Reset Combat")]
        [Button]
        public void DebugResetCombat()
        {
            Debug.Log("[CombatResultHandler Debug] Manually resetting combat state.");
            ResetCombatState();
        }

        #endregion
    }
}
