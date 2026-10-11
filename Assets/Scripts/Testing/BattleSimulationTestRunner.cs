using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing
{
    /// <summary>
    /// Master harness for executing standalone combat balance simulations between factions (Monsters vs. Adventurers).
    /// Bypasses all out-of-combat meta loops (Shop, Inventory, Bench, Grid Placement, Round progression).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public class BattleSimulationTestRunner : MonoBehaviour
    {
        #region Enums & Data Structures

        public enum SimulationState
        {
            Idle,
            Running,
            Paused,
            Completed
        }

        public enum BattleFaction
        {
            Monster = 1,
            Adventurer = 2
        }

        public enum BattleOutcome
        {
            None,
            MonstersWin,
            AdventurersWin,
            Draw,
            Timeout
        }

        [Serializable]
        public class UnitSpawnSlot
        {
            [Tooltip("Descriptive label for this slot in the inspector.")]
            public string slotLabel = "Unit Slot";

            [Tooltip("Character prefab to instantiate (must contain CharacterBrain and CharacterStatsManager).")]
            public GameObject unitPrefab;

            [Tooltip("Optional transform marker for spawn position/rotation. If null, customSpawnPosition is used.")]
            public Transform spawnAnchor;

            [Tooltip("Spawn position vector used if spawnAnchor is not assigned.")]
            public Vector3 customSpawnPosition;

            [Tooltip("Combat faction assignment for this unit.")]
            public BattleFaction faction = BattleFaction.Monster;

            [Tooltip("Initial level override applied upon spawning (>= 1).")]
            [Range(1, 100)]
            public int initialLevel = 1;

            [Header("Stat Overrides")]
            [Tooltip("When enabled, overrides base stat values with custom parameters below.")]
            public bool overrideStats = false;

            [Min(1f)] public float customMaxLife = 100f;
            [Min(0f)] public float customDamage = 15f;
            [Min(0.1f)] public float customMovespeed = 3.5f;
            [Min(0.1f)] public float customAttackRange = 1.2f;
            [Min(0.05f)] public float customAttackInterval = 1.0f;

            public Vector3 GetSpawnPosition()
            {
                return spawnAnchor != null ? spawnAnchor.position : customSpawnPosition;
            }

            public Quaternion GetSpawnRotation()
            {
                return spawnAnchor != null ? spawnAnchor.rotation : Quaternion.identity;
            }
        }

        public class TrackedCombatant
        {
            public GameObject GameObject { get; }
            public CharacterBrain Brain { get; }
            public CharacterStatsManager Stats { get; }
            public BaseAI AI { get; }
            public BattleFaction Faction { get; }
            public UnitSpawnSlot SourceSlot { get; }
            public float InitialMaxLife { get; }

            public bool IsAlive => GameObject != null && Stats != null && Stats.Life > 0;
            public float CurrentLife => Stats != null ? Stats.Life : 0f;
            public float MaxLife => Stats != null && Stats.MaxLife != null ? Stats.MaxLife.Value : InitialMaxLife;
            public float HealthPercent => MaxLife > 0.001f ? Mathf.Clamp01(CurrentLife / MaxLife) : 0f;

            public TrackedCombatant(GameObject go, CharacterBrain brain, CharacterStatsManager stats, BaseAI ai, BattleFaction faction, UnitSpawnSlot slot)
            {
                GameObject = go;
                Brain = brain;
                Stats = stats;
                AI = ai;
                Faction = faction;
                SourceSlot = slot;
                InitialMaxLife = stats != null && stats.MaxLife != null ? stats.MaxLife.Value : 100f;
            }
        }

        [Serializable]
        public struct SimulationReport
        {
            public BattleOutcome outcome;
            public float duration;
            public int initialMonsters;
            public int initialAdventurers;
            public int survivingMonsters;
            public int survivingAdventurers;
            public int monsterCasualties;
            public int adventurerCasualties;
            public string summary;

            public override string ToString()
            {
                return $"[Battle Report] Outcome: {outcome} | Duration: {duration:F2}s | Monsters Remaining: {survivingMonsters}/{initialMonsters} | Adventurers Remaining: {survivingAdventurers}/{initialAdventurers}";
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Roster Configuration")]
        [Tooltip("List of unit slots to spawn when starting the simulation.")]
        [SerializeField] private List<UnitSpawnSlot> roster = new();

        [Header("Simulation Timing & Rules")]
        [Tooltip("Time scales available for speed cycling.")]
        [SerializeField] private float[] availableTimeScales = { 1f, 2f, 5f, 10f };

        [Tooltip("Active index in availableTimeScales.")]
        [SerializeField] private int currentTimeScaleIndex = 0;

        [Tooltip("Maximum allowed combat simulation time before declaring a timeout (<= 0 for unlimited).")]
        [SerializeField] private float maxSimulationDuration = 180f;

        [Tooltip("Automatically pause Time.timeScale when a simulation concludes.")]
        [SerializeField] private bool pauseTimeScaleOnEnd = false;

        [Tooltip("Automatically start simulation upon entering Play Mode.")]
        [SerializeField] private bool autoStartOnPlay = false;

        [Header("Hierarchy Organization")]
        [Tooltip("Parent transform to contain all spawned units for clean hierarchy inspection.")]
        [SerializeField] private Transform unitsContainer;

        #endregion

        #region Runtime State

        private SimulationState currentState = SimulationState.Idle;
        private float simulationElapsedTime = 0f;
        private float simulationStartTime = 0f;
        private BattleOutcome currentOutcome = BattleOutcome.None;
        private SimulationReport lastReport;

        private readonly List<TrackedCombatant> activeCombatants = new();
        private readonly List<TrackedCombatant> monsterCombatants = new();
        private readonly List<TrackedCombatant> adventurerCombatants = new();

        private int initialMonsterCount = 0;
        private int initialAdventurerCount = 0;

        private Coroutine simulationLoopCoroutine;

        #endregion

        #region Events

        public event Action OnSimulationStarted;
        public event Action<SimulationReport> OnSimulationEnded;
        public event Action OnSimulationReset;
        public event Action<float> OnTimeScaleChanged;

        #endregion

        #region Properties

        public SimulationState State => currentState;
        public float ElapsedTime => simulationElapsedTime;
        public BattleOutcome CurrentOutcome => currentOutcome;
        public SimulationReport LastReport => lastReport;
        public IReadOnlyList<TrackedCombatant> AllCombatants => activeCombatants;
        public IReadOnlyList<TrackedCombatant> Monsters => monsterCombatants;
        public IReadOnlyList<TrackedCombatant> Adventurers => adventurerCombatants;
        public int InitialMonsters => initialMonsterCount;
        public int InitialAdventurers => initialAdventurerCount;
        public float CurrentTimeScale => availableTimeScales.Length > 0 ? availableTimeScales[currentTimeScaleIndex] : Time.timeScale;
        public List<UnitSpawnSlot> Roster => roster;

        public int AliveMonstersCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < monsterCombatants.Count; i++)
                {
                    if (monsterCombatants[i].IsAlive) count++;
                }
                return count;
            }
        }

        public int AliveAdventurersCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < adventurerCombatants.Count; i++)
                {
                    if (adventurerCombatants[i].IsAlive) count++;
                }
                return count;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (unitsContainer == null)
            {
                var containerGo = new GameObject("SpawnedCombatUnits");
                containerGo.transform.SetParent(transform);
                unitsContainer = containerGo.transform;
            }
        }

        private void Start()
        {
            if (autoStartOnPlay && Application.isPlaying)
            {
                StartSimulation();
            }
        }

        private void OnDisable()
        {
            // Restore normal timeScale on exit to avoid persisting warped timeScale across editor scenes
            Time.timeScale = 1f;
        }

        #endregion

        #region Simulation Lifecycle API

        /// <summary>
        /// Cleans active units, instantiates roster slots, sets AI control, and begins combat simulation.
        /// </summary>
        [ContextMenu("Start Simulation")]
        public void StartSimulation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BattleSimulationTestRunner] StartSimulation can only execute in Play Mode.", this);
                return;
            }

            // 1. Reset any ongoing simulation cleanly
            ResetSimulation(resetTimeScale: false);

            if (roster == null || roster.Count == 0)
            {
                Debug.LogWarning("[BattleSimulationTestRunner] Roster is empty! Assign unit slots before starting simulation.", this);
                return;
            }

            // 2. Instantiate and wire roster units
            initialMonsterCount = 0;
            initialAdventurerCount = 0;

            for (int i = 0; i < roster.Count; i++)
            {
                var slot = roster[i];
                if (slot == null || slot.unitPrefab == null)
                {
                    Debug.LogWarning($"[BattleSimulationTestRunner] Slot #{i} has no prefab assigned. Skipping.", this);
                    continue;
                }

                SpawnUnit(slot, i);
            }

            if (monsterCombatants.Count == 0 && adventurerCombatants.Count == 0)
            {
                Debug.LogError("[BattleSimulationTestRunner] No valid combatants were spawned from roster.", this);
                currentState = SimulationState.Idle;
                return;
            }

            initialMonsterCount = monsterCombatants.Count;
            initialAdventurerCount = adventurerCombatants.Count;

            // 3. Initialize timing and state
            simulationElapsedTime = 0f;
            simulationStartTime = Time.time;
            currentOutcome = BattleOutcome.None;
            currentState = SimulationState.Running;

            ApplyCurrentTimeScale();

            Debug.Log($"<color=cyan>[BattleSimulationTestRunner] Simulation STARTED</color> with {initialMonsterCount} Monsters vs {initialAdventurerCount} Adventurers.");
            OnSimulationStarted?.Invoke();

            // 4. Start monitoring coroutine
            if (simulationLoopCoroutine != null)
            {
                StopCoroutine(simulationLoopCoroutine);
            }
            simulationLoopCoroutine = StartCoroutine(SimulationMonitorRoutine());
        }

        /// <summary>
        /// Instantly restarts the simulation from scratch with current roster settings.
        /// </summary>
        [ContextMenu("Restart / Rerun")]
        public void RestartSimulation()
        {
            StartSimulation();
        }

        /// <summary>
        /// Destroys all active combat units, interrupts brains/AI plans, clears blackboards, and resets state.
        /// </summary>
        [ContextMenu("Clear Board / Reset")]
        public void ResetSimulation()
        {
            ResetSimulation(resetTimeScale: true);
        }

        public void ResetSimulation(bool resetTimeScale)
        {
            if (simulationLoopCoroutine != null)
            {
                StopCoroutine(simulationLoopCoroutine);
                simulationLoopCoroutine = null;
            }

            // 1. Interrupt active actions and destroy units
            for (int i = 0; i < activeCombatants.Count; i++)
            {
                var combatant = activeCombatants[i];
                if (combatant == null || combatant.GameObject == null) continue;

                // Stop AI decision locks & GOAP actions
                if (combatant.AI != null)
                {
                    combatant.AI.InterruptCurrentPlan();
                }

                // Stop attack timers, casting coroutines, and recovery buffers
                if (combatant.Brain != null)
                {
                    combatant.Brain.InterruptAllActions();
                    combatant.Brain.OnDespawn();
                }

                // Clear stats listeners
                if (combatant.Stats != null)
                {
                    combatant.Stats.CancelAttackTimer();
                }

                if (Application.isPlaying)
                {
                    Destroy(combatant.GameObject);
                }
                else
                {
                    DestroyImmediate(combatant.GameObject);
                }
            }

            // 2. Clear tracking buffers
            activeCombatants.Clear();
            monsterCombatants.Clear();
            adventurerCombatants.Clear();

            // Also clean any orphaned child objects under container
            if (unitsContainer != null)
            {
                int childCount = unitsContainer.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    var child = unitsContainer.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }

            // 3. Reset timing and outcome
            simulationElapsedTime = 0f;
            currentOutcome = BattleOutcome.None;
            currentState = SimulationState.Idle;

            if (resetTimeScale)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }

            Debug.Log("[BattleSimulationTestRunner] Board cleared and simulation reset.");
            OnSimulationReset?.Invoke();
        }

        /// <summary>
        /// Concludes the active combat simulation, gathers casualties, and broadcasts the final report.
        /// </summary>
        public void EndSimulation(BattleOutcome outcome)
        {
            if (currentState == SimulationState.Completed) return;

            currentState = SimulationState.Completed;
            currentOutcome = outcome;

            if (simulationLoopCoroutine != null)
            {
                StopCoroutine(simulationLoopCoroutine);
                simulationLoopCoroutine = null;
            }

            int survivingMonsters = AliveMonstersCount;
            int survivingAdventurers = AliveAdventurersCount;
            int monsterCasualties = Mathf.Max(0, initialMonsterCount - survivingMonsters);
            int adventurerCasualties = Mathf.Max(0, initialAdventurerCount - survivingAdventurers);

            string winnerString;
            switch (outcome)
            {
                case BattleOutcome.MonstersWin:
                    winnerString = "<color=green>MONSTERS WIN!</color>";
                    break;
                case BattleOutcome.AdventurersWin:
                    winnerString = "<color=red>ADVENTURERS WIN!</color>";
                    break;
                case BattleOutcome.Draw:
                    winnerString = "<color=yellow>DRAW (MUTUAL ANNIHILATION)!</color>";
                    break;
                case BattleOutcome.Timeout:
                    winnerString = "<color=orange>TIMEOUT (STALEMATE)!</color>";
                    break;
                default:
                    winnerString = outcome.ToString();
                    break;
            }

            lastReport = new SimulationReport
            {
                outcome = outcome,
                duration = simulationElapsedTime,
                initialMonsters = initialMonsterCount,
                initialAdventurers = initialAdventurerCount,
                survivingMonsters = survivingMonsters,
                survivingAdventurers = survivingAdventurers,
                monsterCasualties = monsterCasualties,
                adventurerCasualties = adventurerCasualties,
                summary = $"{winnerString} Duration: {simulationElapsedTime:F2}s | Casualties - Monsters: {monsterCasualties}/{initialMonsterCount}, Adventurers: {adventurerCasualties}/{initialAdventurerCount}"
            };

            Debug.Log($"<b>[BattleSimulationTestRunner]</b> {lastReport.summary}");

            if (pauseTimeScaleOnEnd)
            {
                Time.timeScale = 0f;
            }

            OnSimulationEnded?.Invoke(lastReport);
        }

        #endregion

        #region Internal Spawning & Unit Setup

        private void SpawnUnit(UnitSpawnSlot slot, int slotIndex)
        {
            Vector3 spawnPos = slot.GetSpawnPosition();
            Quaternion spawnRot = slot.GetSpawnRotation();

            GameObject unitGo = Instantiate(slot.unitPrefab, spawnPos, spawnRot, unitsContainer);
            string factionPrefix = slot.faction == BattleFaction.Monster ? "Monster" : "Adventurer";
            unitGo.name = $"{factionPrefix}_{slotIndex + 1}_{slot.unitPrefab.name}";

            // 1. Tag Assignment
            unitGo.tag = slot.faction == BattleFaction.Monster ? "Monster" : "Adventurer";

            // 2. FactionMember Setup
            var factionMember = unitGo.GetComponent<FactionMember>();
            if (factionMember == null)
            {
                factionMember = unitGo.AddComponent<FactionMember>();
            }
            factionMember.Faction = slot.faction == BattleFaction.Monster ? FactionId.Monster : FactionId.Adventurer;

            // 3. Unique CharacterIdentifier Setup
            var identifier = unitGo.GetComponent<CharacterIdentifier>();
            if (identifier == null)
            {
                identifier = unitGo.AddComponent<CharacterIdentifier>();
            }
            if (string.IsNullOrEmpty(identifier.UniqueId))
            {
                identifier.GenerateID();
            }

            // 4. Resolve Core Components
            var brain = unitGo.GetComponent<CharacterBrain>();
            var stats = unitGo.GetComponent<CharacterStatsManager>();
            var ai = unitGo.GetComponentInChildren<BaseAI>();

            // 5. Stat & Level Configuration
            if (stats != null)
            {
                if (slot.initialLevel > 1)
                {
                    stats.SetLevel(slot.initialLevel);
                }

                if (slot.overrideStats)
                {
                    ApplyStatOverrides(stats, slot);
                }
            }

            // 6. Character Brain & ControlMode Setup
            if (brain != null)
            {
                brain.SetControlMode(ControlMode.AI);
                brain.AIActivation(true);
            }

            // 7. Register Tracked Combatant
            var combatant = new TrackedCombatant(unitGo, brain, stats, ai, slot.faction, slot);
            activeCombatants.Add(combatant);

            if (slot.faction == BattleFaction.Monster)
            {
                monsterCombatants.Add(combatant);
            }
            else
            {
                adventurerCombatants.Add(combatant);
            }
        }

        private void ApplyStatOverrides(CharacterStatsManager stats, UnitSpawnSlot slot)
        {
            if (slot.customMaxLife > 0f && stats.MaxLife != null)
            {
                stats.MaxLife.BaseValue = slot.customMaxLife;
                stats.RestoreLife(slot.customMaxLife);
            }

            if (slot.customDamage > 0f && stats.Damage != null)
            {
                stats.Damage.BaseValue = slot.customDamage;
            }

            if (slot.customMovespeed > 0f && stats.Movespeed != null)
            {
                stats.Movespeed.BaseValue = slot.customMovespeed;
            }

            if (slot.customAttackRange > 0f && stats.AttackRange != null)
            {
                stats.AttackRange.BaseValue = slot.customAttackRange;
            }

            if (slot.customAttackInterval > 0f && stats.AttackTimeInterval != null)
            {
                stats.AttackTimeInterval.BaseValue = slot.customAttackInterval;
            }
        }

        #endregion

        #region Simulation Loop & Outcome Detection

        private IEnumerator SimulationMonitorRoutine()
        {
            // Give 1 frame for Awake/Start and Sensor pulses to settle
            yield return null;

            while (currentState == SimulationState.Running)
            {
                simulationElapsedTime += Time.deltaTime;

                int aliveMonsters = AliveMonstersCount;
                int aliveAdventurers = AliveAdventurersCount;

                // Victory / Wipeout Checks
                if (aliveMonsters == 0 && aliveAdventurers == 0)
                {
                    EndSimulation(BattleOutcome.Draw);
                    yield break;
                }
                else if (aliveMonsters == 0)
                {
                    EndSimulation(BattleOutcome.AdventurersWin);
                    yield break;
                }
                else if (aliveAdventurers == 0)
                {
                    EndSimulation(BattleOutcome.MonstersWin);
                    yield break;
                }

                // Timeout Check
                if (maxSimulationDuration > 0f && simulationElapsedTime >= maxSimulationDuration)
                {
                    EndSimulation(BattleOutcome.Timeout);
                    yield break;
                }

                yield return null;
            }
        }

        #endregion

        #region Time Scaling & Fast Forward

        /// <summary>
        /// Cycles to the next available time scale preset (e.g. 1x -> 2x -> 5x -> 10x -> 1x).
        /// </summary>
        [ContextMenu("Toggle / Cycle Speed")]
        public void ToggleSpeed()
        {
            if (availableTimeScales == null || availableTimeScales.Length == 0) return;

            currentTimeScaleIndex = (currentTimeScaleIndex + 1) % availableTimeScales.Length;
            ApplyCurrentTimeScale();
        }

        public void SetTimeScale(float scale)
        {
            float targetScale = Mathf.Max(0f, scale);
            Time.timeScale = targetScale;
            Time.fixedDeltaTime = 0.02f * (targetScale > 0.001f ? targetScale : 1f);
            OnTimeScaleChanged?.Invoke(targetScale);
            Debug.Log($"[BattleSimulationTestRunner] TimeScale set to {targetScale}x");
        }

        public void TogglePause()
        {
            if (currentState == SimulationState.Running)
            {
                currentState = SimulationState.Paused;
                Time.timeScale = 0f;
            }
            else if (currentState == SimulationState.Paused)
            {
                currentState = SimulationState.Running;
                ApplyCurrentTimeScale();
            }
        }

        private void ApplyCurrentTimeScale()
        {
            float targetScale = CurrentTimeScale;
            SetTimeScale(targetScale);
        }

        #endregion

        #region Roster Helpers & Quick Presets

        public void ClearRoster()
        {
            roster.Clear();
        }

        public void AddSlot(GameObject prefab, Vector3 position, BattleFaction faction, int level = 1)
        {
            roster.Add(new UnitSpawnSlot
            {
                slotLabel = $"{faction} Lvl {level}",
                unitPrefab = prefab,
                customSpawnPosition = position,
                faction = faction,
                initialLevel = level
            });
        }

        #endregion
    }
}
