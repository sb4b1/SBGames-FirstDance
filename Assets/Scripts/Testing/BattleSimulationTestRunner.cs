using System;
using System.Collections;
using System.Collections.Generic;
using SBGames.Dungeon.Markers;
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

            [Header("AI & Tactical Profile")]
            [Tooltip("Optional Adventurer AI Profile override (Rank E, D, C). If null, uses prefab default.")]
            public AdventurerAIProfile profileOverride;

            [Tooltip("Optional Adventurer function/role override (Tank, DPS, Healer, Scout).")]
            public AdventurerFunction functionOverride = AdventurerFunction.None;

            [Tooltip("Designate this unit as the party leader for its faction squad.")]
            public bool isPartyLeader = false;

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
            public UnityEngine.AI.NavMeshAgent Agent { get; }
            public BattleFaction Faction { get; }
            public UnitSpawnSlot SourceSlot { get; }
            public float InitialMaxLife { get; }
            public float DamageDealt { get; private set; }
            public float DamageTaken { get; private set; }
            public float HealingReceived { get; private set; }
            public float SkillHealingReceived { get; private set; }
            public float PotionHealingReceived { get; private set; }
            public float RegenHealingReceived { get; private set; }
            public int HealsCast { get; private set; }
            public float FirstHealTime { get; private set; } = -1f;
            public int FleeEvents { get; private set; }
            public int SurviveGoalTransitions { get; private set; }
            public float IdleTime { get; private set; }
            public EnumUtilGoal LastGoalKey { get; set; } = EnumUtilGoal.None;
            public readonly List<SkillCaster> SubscribedCasters = new();

            private readonly BattleSimulationTestRunner ownerRunner;

            public bool IsAlive => GameObject != null && Stats != null && Stats.Life > 0;
            public float CurrentLife => Stats != null ? Stats.Life : 0f;
            public float MaxLife => Stats != null && Stats.MaxLife != null ? Stats.MaxLife.Value : InitialMaxLife;
            public float HealthPercent => MaxLife > 0.001f ? Mathf.Clamp01(CurrentLife / MaxLife) : 0f;

            public void RecordDamageDealt(float dmg) => DamageDealt += dmg;
            public void RecordDamageTaken(float dmg) => DamageTaken += dmg;
            public void RecordHealingReceived(float amount, bool isSkill)
            {
                HealingReceived += amount;
                if (isSkill)
                {
                    SkillHealingReceived += amount;
                }
                else
                {
                    PotionHealingReceived += amount;
                }
            }
            public void RecordRegenHealingReceived(float amount)
            {
                HealingReceived += amount;
                RegenHealingReceived += amount;
            }
            public void AddIdleTime(float dt) => IdleTime += dt;
            public void IncrementFleeEvents()
            {
                FleeEvents++;
                SurviveGoalTransitions++;
            }

            public void OnDamagedHandler(DamageData dmg)
            {
                RecordDamageTaken(dmg.Amount);

                if (ownerRunner != null && dmg.Amount > 0f && dmg.Source != null)
                {
                    var all = ownerRunner.activeCombatants;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all[i];
                        if (c == null) continue;

                        if (dmg.Source is CharacterStatsManager statsSource && c.Stats == statsSource)
                        {
                            c.RecordDamageDealt(dmg.Amount);
                            break;
                        }
                        else if (dmg.Source is SkillInstance skillInst && skillInst.CasterGameObject == c.GameObject)
                        {
                            c.RecordDamageDealt(dmg.Amount);
                            break;
                        }
                        else if (dmg.Source is GameObject goSource && c.GameObject == goSource)
                        {
                            c.RecordDamageDealt(dmg.Amount);
                            break;
                        }
                        else if (dmg.Source is Component compSource && c.GameObject == compSource.gameObject)
                        {
                            c.RecordDamageDealt(dmg.Amount);
                            break;
                        }
                    }
                }
            }

            public void OnLifeRestoredHandler(float amount)
            {
                // If SkillHitResolver resolved a skill heal this frame or just prior, it is skill healing; otherwise potion/consumable
                bool isSkill = (ownerRunner != null && (Time.time - ownerRunner.LastSkillHealTimestamp) < 0.05f);
                RecordHealingReceived(amount, isSkill);
                if (ownerRunner != null && isSkill && ownerRunner.FirstHealElapsedTime < 0f && ownerRunner.State == SimulationState.Running)
                {
                    ownerRunner.FirstHealElapsedTime = ownerRunner.ElapsedTime;
                }
            }

            public void OnRegenLifeRestoredHandler(float amount)
            {
                RecordRegenHealingReceived(amount);
            }

            public void OnSkillCastHandler(SkillData skillData, GameObject target)
            {
                if (skillData != null && (skillData.targetFilter == SkillData.SkillTargetFilter.AlliesOnly || skillData.targetFilter == SkillData.SkillTargetFilter.SelfOnly))
                {
                    HealsCast++;
                    float elapsed = ownerRunner != null ? ownerRunner.ElapsedTime : 0f;
                    if (FirstHealTime < 0f)
                        FirstHealTime = elapsed;
                    if (ownerRunner != null && ownerRunner.FirstHealElapsedTime < 0f)
                        ownerRunner.FirstHealElapsedTime = elapsed;
                }
            }

            public TrackedCombatant(GameObject go, CharacterBrain brain, CharacterStatsManager stats, BaseAI ai, BattleFaction faction, UnitSpawnSlot slot, BattleSimulationTestRunner runner = null)
            {
                GameObject = go;
                Brain = brain;
                Stats = stats;
                AI = ai;
                Agent = go != null ? go.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
                Faction = faction;
                SourceSlot = slot;
                ownerRunner = runner;
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
            public float monsterDps;
            public float adventurerDps;
            public float tankDamageTaken;
            public float dpsDamageTaken;
            public float healerDamageTaken;
            public int healsCast;
            public float totalHealingDone;
            public float skillHealingDone;
            public float potionHealingDone;
            public float regenHealing;
            public float timeToFirstHeal;
            public int tankFleeEvents;
            public int healerSurviveEvents;
            public float healerIdleTime;
            public int totalSwingsAttempted;
            public int totalSwingsHit;
            public int totalSwingsMissed;
            public string timeoutReason;
            public string summary;

            public override string ToString()
            {
                return $"[Battle Report] Outcome: {outcome} | Duration: {duration:F2}s | Adv DPS: {adventurerDps:F1} | Mon DPS: {monsterDps:F1} | Swings(Hit/Miss): {totalSwingsHit}/{totalSwingsMissed} ({(totalSwingsAttempted > 0 ? (float)totalSwingsHit / totalSwingsAttempted * 100f : 0f):F1}%) | DmgTaken(T/D/H): {tankDamageTaken:F0}/{dpsDamageTaken:F0}/{healerDamageTaken:F0} | Heals: {healsCast} (Skill: {skillHealingDone:F0} HP, Pot: {potionHealingDone:F0} HP, Regen: {regenHealing:F0} HP, 1st: {(timeToFirstHeal >= 0f ? $"{timeToFirstHeal:F2}s" : "N/A")}) | TankFlees: {tankFleeEvents} | HealerSurvive: {healerSurviveEvents} | HealerIdle: {healerIdleTime:F2}s | Monsters: {survivingMonsters}/{initialMonsters} | Adventurers: {survivingAdventurers}/{initialAdventurers}";
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
        [SerializeField] private float maxSimulationDuration = 60f;

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
        public float FirstHealElapsedTime { get; set; } = -1f;
        public float LastSkillHealTimestamp { get; set; } = -100f;
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

        private void OnEnable()
        {
            SkillHitResolver.OnSkillHealApplied += HandleSkillHealApplied;
        }

        private void OnDisable()
        {
            SkillHitResolver.OnSkillHealApplied -= HandleSkillHealApplied;
            // Restore normal timeScale on exit to avoid persisting warped timeScale across editor scenes
            Time.timeScale = 1f;
        }

        private void HandleSkillHealApplied(SkillInstance skill, GameObject target, float amount)
        {
            LastSkillHealTimestamp = Time.time;
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
            FirstHealElapsedTime = -1f;

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

            // Wire PartyLeader and SquadMembers for Adventurers
            PartyLeader leader = null;
            TrackedCombatant leaderCombatant = null;
            for (int i = 0; i < adventurerCombatants.Count; i++)
            {
                var combatant = adventurerCombatants[i];
                if (combatant.SourceSlot != null && combatant.SourceSlot.isPartyLeader)
                {
                    leaderCombatant = combatant;
                    leader = combatant.GameObject.GetComponent<PartyLeader>();
                    if (leader == null)
                    {
                        leader = combatant.GameObject.AddComponent<PartyLeader>();
                    }
                    break;
                }
            }

            if (leader == null && adventurerCombatants.Count > 1)
            {
                leaderCombatant = adventurerCombatants[0];
                leader = leaderCombatant.GameObject.GetComponent<PartyLeader>();
                if (leader == null)
                {
                    leader = leaderCombatant.GameObject.AddComponent<PartyLeader>();
                }
            }

            if (leader != null)
            {
                var evaluator = leader.GetComponent<SquadTacticalEvaluator>();
                if (evaluator == null)
                {
                    evaluator = leader.gameObject.AddComponent<SquadTacticalEvaluator>();
                }

                for (int i = 0; i < adventurerCombatants.Count; i++)
                {
                    var combatant = adventurerCombatants[i];
                    if (combatant == leaderCombatant) continue;

                    var member = combatant.GameObject.GetComponent<SquadMember>();
                    if (member == null)
                    {
                        member = combatant.GameObject.AddComponent<SquadMember>();
                    }
                    leader.RegisterMember(member);
                }

                // If sandbox has no dungeon rooms, ensure blackboard starts in FocusFire / Engage mode
                if (DungeonRoomRegistry.Rooms.Count == 0 && monsterCombatants.Count > 0)
                {
                    Transform initialTarget = null;
                    for (int m = 0; m < monsterCombatants.Count; m++)
                    {
                        if (monsterCombatants[m] != null && monsterCombatants[m].GameObject != null)
                        {
                            initialTarget = monsterCombatants[m].GameObject.transform;
                            break;
                        }
                    }
                    leader.Blackboard.SetOrder(SquadOrderType.FocusFire, initialTarget, initialTarget != null ? initialTarget.position : Vector3.zero);
                }
            }

            // 3. Initialize timing and state
            simulationElapsedTime = 0f;
            simulationStartTime = Time.time;
            FirstHealElapsedTime = -1f;
            CharacterCombatController.TotalSwingsAttempted = 0;
            CharacterCombatController.TotalSwingsHit = 0;
            CharacterCombatController.TotalSwingsMissed = 0;
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

                // Clear stats & skill listeners
                if (combatant.Stats != null)
                {
                    combatant.Stats.CancelAttackTimer();
                    combatant.Stats.OnDamaged -= combatant.OnDamagedHandler;
                    combatant.Stats.OnLifeRestored -= combatant.OnLifeRestoredHandler;
                    combatant.Stats.OnRegenLifeRestored -= combatant.OnRegenLifeRestoredHandler;
                }

                for (int c = 0; c < combatant.SubscribedCasters.Count; c++)
                {
                    if (combatant.SubscribedCasters[c] != null)
                    {
                        combatant.SubscribedCasters[c].OnSkillCast -= combatant.OnSkillCastHandler;
                    }
                }
                combatant.SubscribedCasters.Clear();

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
            FirstHealElapsedTime = -1f;
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

            float totalAdvDamageTaken = 0f;
            float tankDmg = 0f;
            float dpsDmg = 0f;
            float healerDmg = 0f;
            int totalHealsCast = 0;
            float totalHealingDone = 0f;
            float totalSkillHealingDone = 0f;
            float totalPotionHealingDone = 0f;
            float totalRegenHealingDone = 0f;
            int tankFlees = 0;
            int healerSurvives = 0;
            float healerIdle = 0f;

            for (int i = 0; i < adventurerCombatants.Count; i++)
            {
                var adv = adventurerCombatants[i];
                totalAdvDamageTaken += adv.DamageTaken;
                totalHealsCast += adv.HealsCast;
                totalHealingDone += adv.HealingReceived;
                totalSkillHealingDone += adv.SkillHealingReceived;
                totalPotionHealingDone += adv.PotionHealingReceived;
                totalRegenHealingDone += adv.RegenHealingReceived;

                AdventurerFunction func = adv.SourceSlot != null ? adv.SourceSlot.functionOverride : AdventurerFunction.None;
                if (func == AdventurerFunction.Tank)
                {
                    tankDmg += adv.DamageTaken;
                    tankFlees += adv.FleeEvents;
                }
                else if (func == AdventurerFunction.Healer)
                {
                    healerDmg += adv.DamageTaken;
                    healerIdle += adv.IdleTime;
                    healerSurvives += adv.SurviveGoalTransitions;
                }
                else
                {
                    dpsDmg += adv.DamageTaken;
                }
            }

            float totalMonDamageTaken = 0f;
            for (int i = 0; i < monsterCombatants.Count; i++)
            {
                totalMonDamageTaken += monsterCombatants[i].DamageTaken;
                totalRegenHealingDone += monsterCombatants[i].RegenHealingReceived;
            }

            float safeDuration = Mathf.Max(0.1f, simulationElapsedTime);
            float advDps = totalMonDamageTaken / safeDuration;
            float monDps = totalAdvDamageTaken / safeDuration;
            string firstHealStr = FirstHealElapsedTime >= 0f ? $"{FirstHealElapsedTime:F2}s" : "N/A";

            int swingsAttempted = CharacterCombatController.TotalSwingsAttempted;
            int swingsHit = CharacterCombatController.TotalSwingsHit;
            int swingsMissed = CharacterCombatController.TotalSwingsMissed;
            string timeoutReason = outcome == BattleOutcome.Timeout 
                ? $"Stalemate at {simulationElapsedTime:F1}s: {survivingMonsters} monsters vs {survivingAdventurers} adventurers remain alive. Swings hit/missed: {swingsHit}/{swingsMissed}."
                : string.Empty;

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
                adventurerDps = advDps,
                monsterDps = monDps,
                tankDamageTaken = tankDmg,
                dpsDamageTaken = dpsDmg,
                healerDamageTaken = healerDmg,
                healsCast = totalHealsCast,
                totalHealingDone = totalHealingDone,
                skillHealingDone = totalSkillHealingDone,
                potionHealingDone = totalPotionHealingDone,
                regenHealing = totalRegenHealingDone,
                timeToFirstHeal = FirstHealElapsedTime,
                tankFleeEvents = tankFlees,
                healerSurviveEvents = healerSurvives,
                healerIdleTime = healerIdle,
                totalSwingsAttempted = swingsAttempted,
                totalSwingsHit = swingsHit,
                totalSwingsMissed = swingsMissed,
                timeoutReason = timeoutReason,
                summary = $"{winnerString} Duration: {simulationElapsedTime:F2}s | DPS - Adv: {advDps:F1}, Mon: {monDps:F1} | Swings(Hit/Miss): {swingsHit}/{swingsMissed} ({(swingsAttempted > 0 ? (float)swingsHit / swingsAttempted * 100f : 0f):F1}%) | DmgTaken(T/D/H): {tankDmg:F0}/{dpsDmg:F0}/{healerDmg:F0} | Heals: {totalHealsCast} (Skill: {totalSkillHealingDone:F0} HP, Pot: {totalPotionHealingDone:F0} HP, Regen: {totalRegenHealingDone:F0} HP, 1st: {firstHealStr}) | TankFlees: {tankFlees} | HealerSurvive: {healerSurvives} | HealerIdle: {healerIdle:F2}s | Casualties - Mon: {monsterCasualties}/{initialMonsterCount}, Adv: {adventurerCasualties}/{initialAdventurerCount}{(outcome == BattleOutcome.Timeout ? $" | Reason: {timeoutReason}" : "")}"
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
            var agent = unitGo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.updateRotation = false;
                agent.updateUpAxis = false;
            }
            unitGo.transform.rotation = spawnRot;
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

            // 5. Stat, Role & Level Configuration
            if (stats != null)
            {
                if (slot.initialLevel > 1)
                {
                    stats.SetLevel(slot.initialLevel);
                }

                if (slot.functionOverride != AdventurerFunction.None)
                {
                    stats.SetFunctionOverride(slot.functionOverride);
                }

                if (slot.overrideStats)
                {
                    ApplyStatOverrides(stats, slot);
                }
            }

            // Healer function skill setup
            if (slot.functionOverride == AdventurerFunction.Healer || (slot.slotLabel != null && slot.slotLabel.Contains("Healer")))
            {
                if (stats != null)
                {
                    stats.SetBasicAttackModeOverride(BasicAttackMode.None);
                }

                var skillCtrl = unitGo.GetComponentInChildren<CharacterSkillController>();
                if (skillCtrl != null)
                {
#if UNITY_EDITOR
                    var healSkillPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Skill/Default/SKILL_HEAL_001.prefab");
                    if (healSkillPrefab != null)
                    {
                        var skillParent = unitGo.transform.Find("Skills/SkillList") ?? skillCtrl.transform;
                        var healSkillGo = Instantiate(healSkillPrefab, skillParent);
                        healSkillGo.name = healSkillPrefab.name;
                        var healSkill = new CharacterSkillController.Skill(healSkillGo, skillCtrl);
                        skillCtrl.AddSkill(healSkill);
                    }
#endif
                }
            }

            // AI Profile override
            if (ai != null && slot.profileOverride != null)
            {
                ai.ApplyAdventurerProfile(slot.profileOverride);
            }

            // 6. Character Brain & ControlMode Setup
            if (brain != null)
            {
                brain.SetControlMode(ControlMode.AI);
                brain.AIActivation(true);
            }

            // 7. Auto-orient facing towards arena center (x = 0)
            bool faceRight = spawnPos.x < 0f;
            var flipRotator = unitGo.GetComponentInChildren<CharacterFlipRotator>();
            if (flipRotator != null)
            {
                flipRotator.ApplyFlip(faceRight);
            }
            else
            {
                Vector3 scale = unitGo.transform.localScale;
                scale.x = (faceRight ? -1f : 1f) * Mathf.Abs(scale.x);
                unitGo.transform.localScale = scale;
            }

            // 8. Register Tracked Combatant
            var combatant = new TrackedCombatant(unitGo, brain, stats, ai, slot.faction, slot, this);
            if (stats != null)
            {
                stats.OnDamaged += combatant.OnDamagedHandler;
                stats.OnLifeRestored += combatant.OnLifeRestoredHandler;
                stats.OnRegenLifeRestored += combatant.OnRegenLifeRestoredHandler;
            }

            var casters = unitGo.GetComponentsInChildren<SkillCaster>(true);
            for (int c = 0; c < casters.Length; c++)
            {
                if (casters[c] != null)
                {
                    casters[c].OnSkillCast += combatant.OnSkillCastHandler;
                    combatant.SubscribedCasters.Add(casters[c]);
                }
            }

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
                float dt = Time.deltaTime;
                simulationElapsedTime += dt;

                // Track per-frame combat metrics (Tank flee events, Healer survival transitions & idle time)
                for (int i = 0; i < adventurerCombatants.Count; i++)
                {
                    var adv = adventurerCombatants[i];
                    if (!adv.IsAlive) continue;

                    if (adv.AI != null)
                    {
                        EnumUtilGoal currentGoal = adv.AI.CurrentGoalKey;
                        if (currentGoal == EnumUtilGoal.Survive && adv.LastGoalKey != EnumUtilGoal.Survive)
                        {
                            adv.IncrementFleeEvents();
                        }
                        adv.LastGoalKey = currentGoal;
                    }

                    if (adv.SourceSlot != null && adv.SourceSlot.functionOverride == AdventurerFunction.Healer)
                    {
                        bool isMoving = adv.Agent != null && adv.Agent.enabled && adv.Agent.velocity.sqrMagnitude >= 0.01f;
                        bool isAttacking = adv.Brain != null && adv.Brain.IsAttacking;
                        bool isCasting = adv.Brain != null && adv.Brain.IsCastingSkill;
                        if (!isMoving && !isAttacking && !isCasting)
                        {
                            adv.AddIdleTime(dt);
                        }
                    }
                }

                int aliveMonsters = AliveMonstersCount;
                int aliveAdventurers = AliveAdventurersCount;

                // Victory / Wipeout Checks (suppressed during controlled testing without monsters)
                if (!isControlledTestRunning)
                {
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

        #region Scenario Automation API

        [Header("Scenario Test Harness Prefabs & Profiles")]
        [SerializeField] private GameObject defaultAdventurerPrefab;
        [SerializeField] private GameObject defaultMonsterPrefab1;
        [SerializeField] private GameObject defaultMonsterPrefab2;
        [SerializeField] private GameObject defaultMonsterPrefab3;
        [SerializeField] private GameObject defaultBossPrefab;
        [SerializeField] private AdventurerAIProfile defaultProfileRankE;
        [SerializeField] private AdventurerAIProfile defaultProfileRankD;
        [SerializeField] private AdventurerAIProfile defaultProfileRankC;

        public void LoadDefaultScenarioAssets()
        {
#if UNITY_EDITOR
            if (defaultAdventurerPrefab == null)
                defaultAdventurerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N001.prefab");
            if (defaultMonsterPrefab1 == null)
                defaultMonsterPrefab1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");
            if (defaultMonsterPrefab2 == null)
                defaultMonsterPrefab2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N002.prefab");
            if (defaultMonsterPrefab3 == null)
                defaultMonsterPrefab3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");
            if (defaultBossPrefab == null)
                defaultBossPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Boss/MONSTER_BOSS_001.prefab");
            if (defaultProfileRankE == null)
                defaultProfileRankE = UnityEditor.AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            if (defaultProfileRankD == null)
                defaultProfileRankD = UnityEditor.AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            if (defaultProfileRankC == null)
                defaultProfileRankC = UnityEditor.AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
#endif
        }

        public void ConfigureScenario(string rank, bool isLargeGroup, float timeScale, int seed, bool isBoss = false, bool isPressure = false, bool isHealerDanger = false)
        {
            LoadDefaultScenarioAssets();
            UnityEngine.Random.InitState(seed);

            AdventurerAIProfile profile = rank.ToUpperInvariant() switch
            {
                "E" => defaultProfileRankE,
                "D" => defaultProfileRankD,
                "C" => defaultProfileRankC,
                _ => defaultProfileRankE
            };

            roster.Clear();

            // Monsters on left side
            if (isBoss)
            {
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Monster Boss (BOSS_001)",
                    unitPrefab = defaultBossPrefab != null ? defaultBossPrefab : defaultMonsterPrefab1,
                    customSpawnPosition = new Vector3(-2.2f, 0.0f, 0f),
                    faction = BattleFaction.Monster
                });
            }
            else if (isHealerDanger)
            {
                // Pressure scenario where a flanker monster spawns near the Healer at the rear to test Healer survival priority
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Front Monster 1 (N001 Pressure)",
                    unitPrefab = defaultMonsterPrefab1,
                    customSpawnPosition = new Vector3(-1.5f, 1.2f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 260f,
                    customDamage = 18f,
                    customMovespeed = 3.8f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 1.0f
                });
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Front Monster 2 (N003 Pressure)",
                    unitPrefab = defaultMonsterPrefab3,
                    customSpawnPosition = new Vector3(-1.5f, 0.0f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 260f,
                    customDamage = 18f,
                    customMovespeed = 3.8f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 1.0f
                });
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Rear Flanker (N003 Healer Pressure)",
                    unitPrefab = defaultMonsterPrefab3,
                    customSpawnPosition = new Vector3(1.2f, -1.2f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 220f,
                    customDamage = 22f,
                    customMovespeed = 4.0f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 0.9f
                });
            }
            else if (isPressure)
            {
                // Temporarily buffed monster stats applied via UnitSpawnSlot override mechanism (without modifying ScriptableObject assets)
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Monster 1 (N001 Pressure)",
                    unitPrefab = defaultMonsterPrefab1,
                    customSpawnPosition = new Vector3(-1.8f, 1.2f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 280f,
                    customDamage = 18f,
                    customMovespeed = 3.8f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 1.0f
                });
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Monster 2 (N002 Pressure)",
                    unitPrefab = defaultMonsterPrefab2,
                    customSpawnPosition = new Vector3(-1.8f, 0.0f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 260f,
                    customDamage = 16f,
                    customMovespeed = 3.8f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 1.0f
                });
                roster.Add(new UnitSpawnSlot
                {
                    slotLabel = "Monster 3 (N003 Pressure)",
                    unitPrefab = defaultMonsterPrefab3,
                    customSpawnPosition = new Vector3(-1.8f, -1.2f, 0f),
                    faction = BattleFaction.Monster,
                    overrideStats = true,
                    customMaxLife = 260f,
                    customDamage = 18f,
                    customMovespeed = 3.8f,
                    customAttackRange = 0.8f,
                    customAttackInterval = 1.0f
                });
            }
            else if (isLargeGroup)
            {
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 1 (N001)", unitPrefab = defaultMonsterPrefab1, customSpawnPosition = new Vector3(-2.2f, 2.0f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 2 (N001)", unitPrefab = defaultMonsterPrefab1, customSpawnPosition = new Vector3(-2.2f, 0.8f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 3 (N002)", unitPrefab = defaultMonsterPrefab2, customSpawnPosition = new Vector3(-2.2f, -0.4f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 4 (N002)", unitPrefab = defaultMonsterPrefab2, customSpawnPosition = new Vector3(-2.2f, -1.6f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 5 (N003)", unitPrefab = defaultMonsterPrefab3, customSpawnPosition = new Vector3(-3.4f, 1.0f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 6 (N003)", unitPrefab = defaultMonsterPrefab3, customSpawnPosition = new Vector3(-3.4f, -1.0f, 0f), faction = BattleFaction.Monster });
            }
            else
            {
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 1 (N001)", unitPrefab = defaultMonsterPrefab1, customSpawnPosition = new Vector3(-2.2f, 1.2f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 2 (N002)", unitPrefab = defaultMonsterPrefab2, customSpawnPosition = new Vector3(-2.2f, 0.0f, 0f), faction = BattleFaction.Monster });
                roster.Add(new UnitSpawnSlot { slotLabel = "Monster 3 (N003)", unitPrefab = defaultMonsterPrefab3, customSpawnPosition = new Vector3(-2.2f, -1.2f, 0f), faction = BattleFaction.Monster });
            }

            // Adventurers on right side (Tank front/leader, DPS flank, Healer rear)
            roster.Add(new UnitSpawnSlot
            {
                slotLabel = "Adventurer Tank",
                unitPrefab = defaultAdventurerPrefab,
                customSpawnPosition = new Vector3(2.2f, 1.2f, 0f),
                faction = BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.Tank,
                profileOverride = profile,
                isPartyLeader = true
            });
            roster.Add(new UnitSpawnSlot
            {
                slotLabel = "Adventurer DPS",
                unitPrefab = defaultAdventurerPrefab,
                customSpawnPosition = new Vector3(2.2f, 0.0f, 0f),
                faction = BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.DPS,
                profileOverride = profile,
                isPartyLeader = false
            });
            roster.Add(new UnitSpawnSlot
            {
                slotLabel = "Adventurer Healer",
                unitPrefab = defaultAdventurerPrefab,
                customSpawnPosition = new Vector3(2.2f, -1.2f, 0f),
                faction = BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.Healer,
                profileOverride = profile,
                isPartyLeader = false
            });

            SetTimeScale(timeScale);
        }

        [Serializable]
        public class BatchScenarioResult
        {
            public string scenarioName;
            public float timeScale;
            public int seed;
            public BattleOutcome outcome;
            public float duration;
            public float adventurerDps;
            public float monsterDps;
            public int survivingAdventurers;
            public int survivingMonsters;
            public float tankDamageTaken;
            public float dpsDamageTaken;
            public float healerDamageTaken;
            public int healsCast;
            public float totalHealingDone;
            public float skillHealingDone;
            public float potionHealingDone;
            public float regenHealing;
            public float timeToFirstHeal;
            public int tankFleeEvents;
            public int healerSurviveEvents;
            public float healerIdleTime;
            public int totalSwingsAttempted;
            public int totalSwingsHit;
            public int totalSwingsMissed;
            public string timeoutReason;
        }

        public readonly List<BatchScenarioResult> batchResults = new();
        public bool isBatchRunning = false;

        public void StartBatchScenarios(int[] seeds = null)
        {
            if (seeds == null || seeds.Length == 0)
            {
                seeds = new int[] { 101, 102, 103 };
            }
            StartCoroutine(RunAllScenariosRoutine(seeds));
        }

        public IEnumerator RunAllScenariosRoutine(int[] seeds, Action onAllComplete = null)
        {
            isBatchRunning = true;
            batchResults.Clear();

            var scenarios = new (string rank, bool isLargeGroup, bool isBoss, bool isPressure, bool isHealerDanger, string name)[]
            {
                ("E", false, false, false, false, "E-rank vs Small Group"),
                ("D", false, false, false, false, "D-rank vs Small Group"),
                ("C", false, false, false, false, "C-rank vs Small Group"),
                ("C", true,  false, false, false, "C-rank vs Large Group"),
                ("C", false, true,  false, false, "Party vs Boss"),
                ("E", false, false, true,  false, "E-rank vs Pressure Group"),
                ("D", false, false, true,  false, "D-rank vs Pressure Group"),
                ("C", false, false, true,  false, "C-rank vs Pressure Group"),
                ("C", false, false, false, true,  "C-rank vs Healer-Danger Pressure")
            };

            float[] speeds = { 1f };

            foreach (var sc in scenarios)
            {
                foreach (float spd in speeds)
                {
                    foreach (int s in seeds)
                    {
                        ConfigureScenario(sc.rank, sc.isLargeGroup, spd, s, sc.isBoss, sc.isPressure, sc.isHealerDanger);
                        StartSimulation();

                        while (currentState == SimulationState.Running)
                        {
                            yield return null;
                        }

                        var rep = lastReport;
                        batchResults.Add(new BatchScenarioResult
                        {
                            scenarioName = sc.name,
                            timeScale = spd,
                            seed = s,
                            outcome = rep.outcome,
                            duration = rep.duration,
                            adventurerDps = rep.adventurerDps,
                            monsterDps = rep.monsterDps,
                            survivingAdventurers = rep.survivingAdventurers,
                            survivingMonsters = rep.survivingMonsters,
                            tankDamageTaken = rep.tankDamageTaken,
                            dpsDamageTaken = rep.dpsDamageTaken,
                            healerDamageTaken = rep.healerDamageTaken,
                            healsCast = rep.healsCast,
                            totalHealingDone = rep.totalHealingDone,
                            skillHealingDone = rep.skillHealingDone,
                            potionHealingDone = rep.potionHealingDone,
                            regenHealing = rep.regenHealing,
                            timeToFirstHeal = rep.timeToFirstHeal,
                            tankFleeEvents = rep.tankFleeEvents,
                            healerSurviveEvents = rep.healerSurviveEvents,
                            healerIdleTime = rep.healerIdleTime,
                            totalSwingsAttempted = rep.totalSwingsAttempted,
                            totalSwingsHit = rep.totalSwingsHit,
                            totalSwingsMissed = rep.totalSwingsMissed,
                            timeoutReason = rep.timeoutReason
                        });

                        string firstHealStr = rep.timeToFirstHeal >= 0f ? $"{rep.timeToFirstHeal:F2}s" : "N/A";
                        int totalMon = sc.isBoss ? 1 : (sc.isLargeGroup ? 6 : 3);
                        Debug.Log($"[BATCH] {sc.name} | Speed: {spd}x | Seed: {s} | Outcome: {rep.outcome} | Dur: {rep.duration:F2}s | AdvDPS: {rep.adventurerDps:F1} | MonDPS: {rep.monsterDps:F1} | Swings(Hit/Miss): {rep.totalSwingsHit}/{rep.totalSwingsMissed} | DmgTaken(T/D/H): {rep.tankDamageTaken:F0}/{rep.dpsDamageTaken:F0}/{rep.healerDamageTaken:F0} | Heals: {rep.healsCast} (Skill: {rep.skillHealingDone:F0} HP, Pot: {rep.potionHealingDone:F0} HP, Regen: {rep.regenHealing:F0} HP, 1st: {firstHealStr}) | TankFlees: {rep.tankFleeEvents} | HealerSurvive: {rep.healerSurviveEvents} | HealerIdle: {rep.healerIdleTime:F2}s | AdvSurv: {rep.survivingAdventurers}/3 | MonSurv: {rep.survivingMonsters}/{totalMon}{(rep.outcome == BattleOutcome.Timeout ? $" | Reason: {rep.timeoutReason}" : "")}");

                        ResetSimulation(resetTimeScale: false);
                        yield return null;
                    }
                }
            }

            isBatchRunning = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            Debug.Log($"<color=green>[BATCH] All {batchResults.Count} runs completed successfully!</color>");
            onAllComplete?.Invoke();
        }

        [Serializable]
        public class ControlledHealTestResult
        {
            public string rank;
            public int seed;
            public float damageAppliedTime;
            public float timeToFirstHeal;
            public float hpHealed;
            public float reactionDuration; // timeToFirstHeal - damageAppliedTime
            public string details;
        }

        public readonly List<ControlledHealTestResult> controlledTestResults = new();
        public bool isControlledTestRunning = false;

        public void StartControlledHealerComparison(int[] seeds = null, Action onComplete = null)
        {
            if (seeds == null || seeds.Length == 0)
            {
                seeds = new int[] { 101, 102 };
            }
            StartCoroutine(ControlledHealerComparisonRoutine(seeds, onComplete));
        }

        public IEnumerator ControlledHealerComparisonRoutine(int[] seeds, Action onComplete = null)
        {
            isControlledTestRunning = true;
            controlledTestResults.Clear();
            string[] ranks = { "E", "D", "C" };
            const float damageTime = 2.0f;
            const float fixedDamage = 50f;
            const float testTimeout = 15f;

            Debug.Log("<color=cyan>[CONTROLLED HEAL TEST] Starting Deterministic E/D/C Healer Comparison...</color>");

            foreach (var rank in ranks)
            {
                foreach (int s in seeds)
                {
                    // Setup quiet scenario: Adventurers only (no monsters)
                    roster.Clear();
                    AdventurerAIProfile profile = rank switch
                    {
                        "E" => defaultProfileRankE,
                        "D" => defaultProfileRankD,
                        "C" => defaultProfileRankC,
                        _ => defaultProfileRankD
                    };

                    // Tank
                    roster.Add(new UnitSpawnSlot
                    {
                        slotLabel = "Adventurer Tank",
                        unitPrefab = defaultAdventurerPrefab,
                        customSpawnPosition = new Vector3(0.5f, 0f, 0f),
                        faction = BattleFaction.Adventurer,
                        functionOverride = AdventurerFunction.Tank,
                        profileOverride = profile,
                        isPartyLeader = true,
                        overrideStats = true,
                        customMaxLife = 100f
                    });

                    // Healer
                    roster.Add(new UnitSpawnSlot
                    {
                        slotLabel = "Adventurer Healer",
                        unitPrefab = defaultAdventurerPrefab,
                        customSpawnPosition = new Vector3(2.5f, 0f, 0f),
                        faction = BattleFaction.Adventurer,
                        functionOverride = AdventurerFunction.Healer,
                        profileOverride = profile,
                        isPartyLeader = false,
                        overrideStats = true,
                        customMaxLife = 100f
                    });

                    SetTimeScale(1f);
                    UnityEngine.Random.InitState(s);
                    StartSimulation();

                    TrackedCombatant tankCombatant = null;
                    TrackedCombatant healerCombatant = null;
                    for (int i = 0; i < adventurerCombatants.Count; i++)
                    {
                        if (adventurerCombatants[i].SourceSlot.functionOverride == AdventurerFunction.Tank)
                            tankCombatant = adventurerCombatants[i];
                        else if (adventurerCombatants[i].SourceSlot.functionOverride == AdventurerFunction.Healer)
                            healerCombatant = adventurerCombatants[i];

                        // Clear potion kits in controlled healer comparison to ensure Tank does not self-heal
                        var kitCtrl = adventurerCombatants[i].GameObject.GetComponent<AdventurerItemKitController>();
                        if (kitCtrl != null)
                        {
                            kitCtrl.ClearKit();
                        }
                    }

                    bool damageDealt = false;
                    float damageAppliedTimestamp = -1f;
                    float healRecordedTime = -1f;
                    float healAmountRestored = 0f;

                    float testStart = Time.time;
                    while (Time.time - testStart < testTimeout)
                    {
                        float elapsed = Time.time - testStart;

                        // Apply fixed damage to tank at exactly damageTime
                        if (!damageDealt && elapsed >= damageTime && tankCombatant != null && tankCombatant.Stats != null)
                        {
                            damageDealt = true;
                            damageAppliedTimestamp = elapsed;
                            // Apply 50 damage -> brings 100 HP tank to 50 HP (50% HP)
                            // For Rank E (threshold 0.50), D (0.50), C (0.50)
                            // If threshold is 0.50, we deal 55 damage so HP is 45% (< 50% threshold)
                            tankCombatant.Stats.TakeDamage(new DamageData(fixedDamage + 5f, DamageType.True));
                            Debug.Log($"[CONTROLLED TEST] Tank took damage at t={elapsed:F2}s. Current HP: {tankCombatant.Stats.Life}/{tankCombatant.Stats.MaxLife.Value}");
                        }

                        // Check if tank received skill healing
                        if (damageDealt && tankCombatant != null && tankCombatant.SkillHealingReceived > 0f)
                        {
                            healRecordedTime = elapsed;
                            healAmountRestored = tankCombatant.SkillHealingReceived;
                            break;
                        }

                        yield return null;
                    }

                    float reactionDuration = (healRecordedTime >= 0f && damageAppliedTimestamp >= 0f) 
                        ? (healRecordedTime - damageAppliedTimestamp) 
                        : -1f;

                    var res = new ControlledHealTestResult
                    {
                        rank = rank,
                        seed = s,
                        damageAppliedTime = damageAppliedTimestamp,
                        timeToFirstHeal = healRecordedTime,
                        hpHealed = healAmountRestored,
                        reactionDuration = reactionDuration,
                        details = healRecordedTime >= 0f 
                            ? $"Healed {healAmountRestored:F0} HP. Reaction duration: {reactionDuration:F2}s" 
                            : "Did not heal within timeout"
                    };
                    controlledTestResults.Add(res);

                    Debug.Log($"<b>[CONTROLLED TEST RESULT]</b> Rank {res.rank} | Seed: {res.seed} | DamageAt: {res.damageAppliedTime:F2}s | HealAt: {res.timeToFirstHeal:F2}s | ReactionTime: {res.reactionDuration:F2}s | Healed: {res.hpHealed:F0} HP");

                    ResetSimulation(resetTimeScale: false);
                    yield return null;
                }
            }

            isControlledTestRunning = false;
            Debug.Log("<color=green>[CONTROLLED HEAL TEST] Completed all E/D/C runs!</color>");
            onComplete?.Invoke();
        }

        #endregion
    }
}
