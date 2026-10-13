using System.Collections.Generic;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using SB4b1.Helpers;
using SBGames.Characters.AI;
using SBGames.Personality;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(GoapActionProvider))]
public class BaseAI : MonoBehaviour, IPoolable
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AgentBehaviour agentBehaviour;
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterStatsManager stats;
    [SerializeField] private CharacterMovementController motionController;
    [SerializeField] private CharacterSkillController skillController;
    [SerializeField] private VisionController vision;
    [SerializeField] private SquadMember squadMember;
    [SerializeField] private List<UtilityGoal> goals;

    // Automatically populated from CharacterSkillController.Skills at runtime.
    // Each Skill's UtilityData (ScriptableObject) is collected here so SelectBestSkill can evaluate them.
    // You do NOT need to assign these manually in the Inspector.
    [SerializeField] private List<UtilitySkill> skills = new();

    [SerializeField] private CharacterPersonality personality;
    [SerializeField] private CharacterAIMemory aiMemory;
    [SerializeField] private AdventurerAIProfile adventurerProfile;
    [SerializeField] private CharacterClassStyleTableSO classStyleTable;
    [SerializeField] private ItemUseQualityTuningSO itemUseQualityTuning;
    [SerializeField] private AdventurerItemKitController itemKitController;
    [SerializeField] private StatusEffectManager statusEffectManager;
    [SerializeField] private AIDecisionThrottler decisionThrottler;
    [SerializeField] private DoorwayStagingController doorwayStaging;

    [Header("Goal Hysteresis (Phase 5)")]
    [Tooltip("Score commitment bonus applied to the currently active goal (if its raw score > 0) so competing goals like Recover and Survive do not flip-flop on boundary ticks.")]
    [SerializeField, Range(0f, 0.25f)] private float goalHysteresisBonus = 0.08f;

    private bool hasProfileSurvivalThreshold;
    private float profileSurvivalHpThreshold;
    private TargetPriorityMode profileTargetPriorityMode = TargetPriorityMode.Nearest;
    private ItemUseQuality profileItemUseQuality = ItemUseQuality.LateWasteful;

    public CharacterBrain Brain => brain ??= GetComponentInParent<CharacterBrain>();
    public CharacterStatsManager StatsManager => stats ??= (GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>());
    public CharacterSkillController SkillController => skillController;
    public VisionController Vision => vision ??= (GetComponent<VisionController>() ?? GetComponentInChildren<VisionController>() ?? GetComponentInParent<VisionController>() ?? (transform.root != null ? transform.root.GetComponentInChildren<VisionController>() : null));
    public SquadMember SquadMember => squadMember ??= (GetComponent<SquadMember>() ?? GetComponentInParent<SquadMember>() ?? GetComponentInChildren<SquadMember>());
    public CharacterPersonality Personality => personality ??= (GetComponent<CharacterPersonality>() ?? GetComponentInParent<CharacterPersonality>() ?? GetComponentInChildren<CharacterPersonality>());
    public CharacterAIMemory AIMemory => aiMemory ??= (GetComponent<CharacterAIMemory>() ?? GetComponentInChildren<CharacterAIMemory>() ?? GetComponentInParent<CharacterAIMemory>());
    public AIDecisionThrottler DecisionThrottler => decisionThrottler ??= (GetComponent<AIDecisionThrottler>() ?? GetComponentInChildren<AIDecisionThrottler>() ?? GetComponentInParent<AIDecisionThrottler>());
    public DoorwayStagingController DoorwayStaging => doorwayStaging ??= (GetComponent<DoorwayStagingController>() ?? GetComponentInChildren<DoorwayStagingController>() ?? GetComponentInParent<DoorwayStagingController>() ?? (transform.root != null ? transform.root.GetComponentInChildren<DoorwayStagingController>() : null));
    public AdventurerItemKitController ItemKitController => itemKitController ??= (GetComponent<AdventurerItemKitController>() ?? GetComponentInParent<AdventurerItemKitController>() ?? GetComponentInChildren<AdventurerItemKitController>());
    public StatusEffectManager StatusEffectManager => statusEffectManager ??= (GetComponent<StatusEffectManager>() ?? GetComponentInParent<StatusEffectManager>() ?? GetComponentInChildren<StatusEffectManager>());
    public AdventurerAIProfile AdventurerProfile => adventurerProfile;
    public CharacterClassStyleTableSO ClassStyleTable => classStyleTable;
    public ItemUseQualityTuningSO ItemUseQualityTuning { get => itemUseQualityTuning; set => itemUseQualityTuning = value; }
    public CharacterClass CharacterClass => StatsManager != null ? StatsManager.CharacterClass : CharacterClass.None;
    public AdventurerFunction CharacterFunction => StatsManager != null ? StatsManager.CharacterFunction : AdventurerFunction.None;
    public AdventurerFunction EffectiveFunction => StatsManager != null ? StatsManager.EffectiveFunction : AdventurerFunction.DPS;
    public AdventurerWealth CharacterWealth => StatsManager != null ? StatsManager.CharacterWealth : AdventurerWealth.Average;
    public TargetPriorityMode TargetPriorityMode => adventurerProfile != null ? adventurerProfile.TargetPriorityMode : profileTargetPriorityMode;
    public ItemUseQuality ItemUseQuality => adventurerProfile != null ? adventurerProfile.ItemUseQuality : profileItemUseQuality;
    public float PreferredRangeBand => classStyleTable != null ? classStyleTable.GetPreferredRangeBand(CharacterClass) : 1.0f;
    public float SkillReliance => classStyleTable != null ? classStyleTable.GetSkillReliance(CharacterClass) : 0.5f;
    public float SkillRelianceMultiplier => classStyleTable != null ? Mathf.Lerp(0.5f, 1.5f, SkillReliance) : 1.0f;
    public float GoalHysteresisBonus { get => goalHysteresisBonus; set => goalHysteresisBonus = Mathf.Clamp(value, 0f, 0.5f); }
    public EnumUtilGoal ActiveGoal => activeGoal;
    public List<UtilityGoal> Goals { get => goals; set => goals = value; }
    public List<UtilitySkill> Skills => skills;

    public void SetClassStyleTable(CharacterClassStyleTableSO table) => classStyleTable = table;
    public void SetTargetPriorityMode(TargetPriorityMode mode) => profileTargetPriorityMode = mode;
    public void SetItemUseQuality(ItemUseQuality quality) => profileItemUseQuality = quality;
    public float GetPreferredStandingDistance(float attackRange) => Mathf.Max(0.1f, attackRange * PreferredRangeBand);
    public Vector3 GetFormationTargetPosition() => SquadMember != null && SquadMember.HasLeader ? SquadMember.GetFormationTargetPosition() : (LeaderTransform != null ? LeaderTransform.position : transform.position);

    // --- Precomputed Zero-Alloc Personality Biases ---
    public float EffectiveThreatMultiplier => Personality != null ? Personality.Snapshot.ThreatMultiplier : 1.0f;
    public float EffectiveFearMultiplier => Personality != null ? Personality.Snapshot.FearMultiplier : 1.0f;
    public bool IsFearLocked => Personality != null && Personality.Snapshot.IsFearLocked;
    public float SurvivalHpThreshold
    {
        get
        {
            if (adventurerProfile != null)
            {
                float personalityMultiplier = Personality != null ? Personality.GetFleeThresholdMultiplier() : 1.0f;
                return adventurerProfile.GetAdjustedFleeThreshold(personalityMultiplier);
            }
            if (hasProfileSurvivalThreshold)
            {
                return profileSurvivalHpThreshold;
            }
            return Personality != null ? Personality.Snapshot.SurvivalHpThreshold : 0.25f;
        }
    }
    public bool IgnoreSurvivalGoal => Personality != null && Personality.Snapshot.IgnoreSurvivalGoal;
    public int ClusterCountOffset => Personality != null ? Personality.Snapshot.ClusterCountOffset : 0;
    public float RangePreferenceMultiplier => Personality != null ? Personality.Snapshot.RangePreferenceMultiplier : 1.0f;
    public float ExecutionHealthThreshold => Personality != null ? Personality.Snapshot.ExecutionHealthThreshold : 0.30f;
    public float ExecutionBonusMultiplier => Personality != null ? Personality.Snapshot.ExecutionBonusMultiplier : 1.0f;
    public float PeelingHealthThreshold => Personality != null ? Personality.Snapshot.PeelingHealthThreshold : 0.35f;
    public float PeelingBonusMultiplier => Personality != null ? Personality.Snapshot.PeelingBonusMultiplier : 1.0f;
    public float LowHealthTargetPriority => Personality != null ? Personality.Snapshot.LowHealthTargetPriority : 1.0f;
    public float BleedingTargetPriority => Personality != null ? Personality.Snapshot.BleedingTargetPriority : 1.0f;

    /// <summary>True when at least one skill in the SkillController is off cooldown.</summary>
    public bool IsSkillReady => skillController != null && skillController.HasReadySkill();

    // This can be used to differentiate between different types of agents (e.g., "NormalAgent", "AggressiveAgent", etc.)
    // NormalAgent is the default type, and if no specific behavior is assigned in the GoapActionProvider, it will use the NormalAgent behavior
    [SerializeField] private string agentType = "NormalAgent";

    private GoapBehaviour goap;
    private GoapActionProvider provider;
    private EnumUtilGoal activeGoal = EnumUtilGoal.None;

    [SerializeField] private Transform currentTarget;
    [SerializeField] private float targetDistance;
    [SerializeField] private List<Transform> enemyTargets;
    [SerializeField] private List<Transform> allyTargets;

    public Transform CurrentTarget { get => currentTarget; private set => currentTarget = value; }
    public float TargetDistance { get => targetDistance; private set => targetDistance = value; }
    public List<Transform> EnemyTargets { get => enemyTargets; set => enemyTargets = value; }
    public List<Transform> AllyTargets { get => allyTargets; set => allyTargets = value; }

    [SerializeField] private List<KnownEnemyRecord> localKnownEnemies = new(8);
    [SerializeField] private bool hasAlertedAllies = true;
    [SerializeField] private float alertShoutRadius = 12f;
    [SerializeField] private float alertShoutCooldown = 4f;
    private float lastAlertShoutTime = -999f;
    private static readonly Collider2D[] proximityAlertBuffer = new Collider2D[16];

    public System.Action<Transform> OnAlertShout;

    public bool HasAlertedAllies { get => hasAlertedAllies; internal set => hasAlertedAllies = value; }
    public float AlertShoutRadius { get => alertShoutRadius; set => alertShoutRadius = value; }
    public float AlertShoutCooldown { get => alertShoutCooldown; set => alertShoutCooldown = value; }
    public float LastAlertShoutTime => lastAlertShoutTime;
    public List<KnownEnemyRecord> LocalKnownEnemies => localKnownEnemies;

    public IReadOnlyList<KnownEnemyRecord> KnownEnemies
    {
        get
        {
            if (IsScoutingActive)
            {
                return localKnownEnemies;
            }
            if (HasSquadLeader && SquadMember.Leader.Blackboard != null)
            {
                return SquadMember.Leader.Blackboard.KnownEnemies;
            }
            return localKnownEnemies;
        }
    }

    public bool IsScoutingActive => SquadMember != null && (SquadMember.IsScouting || SquadOrder == SquadOrderType.ScoutAhead);

    public bool HasKnownEnemies
    {
        get
        {
            if (HasSquadLeader && SquadMember.Leader.Blackboard != null)
            {
                return SquadMember.Leader.Blackboard.HasKnownEnemies;
            }
            return localKnownEnemies.Count > 0;
        }
    }

    public bool HasSquadLeader => SquadMember != null && SquadMember.HasLeader;
    public bool IsStayingLoose => SquadMember != null && SquadMember.IsStayingLoose;
    public SquadOrderType SquadOrder => SquadMember != null ? SquadMember.CurrentOrder : SquadOrderType.None;
    public Transform FocusFireTarget => SquadMember != null ? SquadMember.FocusFireTarget : null;
    public Transform LeaderTransform => SquadMember != null ? SquadMember.LeaderTransform : null;
    public Vector3 SquadRallyPoint => SquadMember != null ? SquadMember.RallyPoint : transform.position;

    [SerializeField] private Vector3 spawnPosition;
    public Vector3 SpawnPosition { get => spawnPosition; set => spawnPosition = value; }

    [SerializeField] private int threatLevel;
    [SerializeField] private int fearLevel;
    [SerializeField] private float agentLife;
    [SerializeField] private float agentMana;

    public int ThreatLevel { get => threatLevel; private set => threatLevel = value; }
    public int FearLevel { get => fearLevel; private set => fearLevel = value; }
    public float AgentLife { get => agentLife; private set => agentLife = value; }
    public float AgentMana { get => agentMana; private set => agentMana = value; }

    public int EnemiesInSkillRange(float range)
    {
        if (enemyTargets == null || enemyTargets.Count == 0) return 0;
        int count = 0;
        for (int i = 0; i < enemyTargets.Count; i++)
        {
            if (enemyTargets[i] != null && Vector3.Distance(transform.position, enemyTargets[i].position) <= range)
                count++;
        }
        return count;
    }

#if UNITY_EDITOR
    [Header("Debugging")]
    [SerializeField] private string currentGoalKey = EnumUtilGoal.None.ToString();
    [SerializeField] private SerializableDictionary<EnumUtilGoal, float> goalScores = new();
    [SerializeField] private string currentSkillKey = "None";
    [SerializeField] private float currentSkillScore = 0f;
    [SerializeField] private SerializableDictionary<string, float> skillScores = new();
#endif

    private void Awake()
    {
        this.goap = FindAnyObjectByType<GoapBehaviour>();
        this.provider = this.GetComponent<GoapActionProvider>();

        if (this.provider.AgentTypeBehaviour == null && this.goap != null)
            this.provider.AgentType = this.goap.GetAgentType(agentType);

        if (GetComponent<AIDecisionThrottler>() == null)
        {
            gameObject.AddComponent<AIDecisionThrottler>();
        }

        if (GetComponent<CharacterAIMemory>() == null && GetComponentInParent<CharacterAIMemory>() == null && GetComponentInChildren<CharacterAIMemory>() == null)
        {
            gameObject.AddComponent<CharacterAIMemory>();
        }

        decisionThrottler = GetComponent<AIDecisionThrottler>();
        if (aiMemory == null)
        {
            aiMemory = GetComponent<CharacterAIMemory>() ?? GetComponentInChildren<CharacterAIMemory>() ?? GetComponentInParent<CharacterAIMemory>();
        }

        if (adventurerProfile != null)
        {
            ApplyAdventurerProfile(adventurerProfile);
        }
    }

    private void OnEnable()
    {
        if (adventurerProfile != null)
        {
            ApplyAdventurerProfile(adventurerProfile);
        }
    }

    private void Start()
    {
        if (brain == null) brain = GetComponentInParent<CharacterBrain>();
        if (agentBehaviour == null) agentBehaviour = GetComponent<AgentBehaviour>();
        if (stats == null) stats = GetComponentInParent<CharacterStatsManager>();
        if (motionController == null) motionController = GetComponentInParent<CharacterMovementController>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        // Auto-find SkillController if not assigned
        if (skillController == null)
        {
            skillController = GetComponent<CharacterSkillController>();
            if (skillController == null) skillController = GetComponentInChildren<CharacterSkillController>();
            if (skillController == null) skillController = GetComponentInParent<CharacterSkillController>();
            if (skillController == null && transform.root != null) skillController = transform.root.GetComponentInChildren<CharacterSkillController>();
        }

        // Auto-find VisionController if not assigned
        if (vision == null)
        {
            vision = GetComponent<VisionController>();
            if (vision == null) vision = GetComponentInChildren<VisionController>();
            if (vision == null) vision = GetComponentInParent<VisionController>();
            if (vision == null && transform.root != null) vision = transform.root.GetComponentInChildren<VisionController>();
        }

        // Auto-find CharacterAIMemory if not assigned
        if (aiMemory == null)
        {
            aiMemory = GetComponent<CharacterAIMemory>();
            if (aiMemory == null) aiMemory = GetComponentInChildren<CharacterAIMemory>();
            if (aiMemory == null) aiMemory = GetComponentInParent<CharacterAIMemory>();
            if (aiMemory == null && transform.root != null) aiMemory = transform.root.GetComponentInChildren<CharacterAIMemory>();
        }

        if (doorwayStaging == null)
        {
            doorwayStaging = GetComponent<DoorwayStagingController>();
            if (doorwayStaging == null) doorwayStaging = GetComponentInChildren<DoorwayStagingController>();
            if (doorwayStaging == null) doorwayStaging = GetComponentInParent<DoorwayStagingController>();
            if (doorwayStaging == null && transform.root != null) doorwayStaging = transform.root.GetComponentInChildren<DoorwayStagingController>();
        }

        if (spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
        }

        if (adventurerProfile != null)
        {
            ApplyAdventurerProfile(adventurerProfile);
        }

        InitializeAgent();
        InitializeSkills();
    }

    public void OnSpawn()
    {
        if (adventurerProfile != null)
        {
            ApplyAdventurerProfile(adventurerProfile);
        }
    }

    public void OnDespawn()
    {
    }

    public void SetSurvivalHpThreshold(float threshold)
    {
        profileSurvivalHpThreshold = Mathf.Clamp01(threshold);
        hasProfileSurvivalThreshold = true;
    }

    /// <summary>
    /// Applies data-driven adventurer rank profile parameters to AI subsystems.
    /// If <paramref name="profile"/> is null, existing defaults remain untouched.
    /// </summary>
    public void ApplyAdventurerProfile(AdventurerAIProfile profile)
    {
        if (profile == null) return;

        adventurerProfile = profile;
        profileTargetPriorityMode = profile.TargetPriorityMode;
        profileItemUseQuality = profile.ItemUseQuality;

        if (DecisionThrottler != null)
        {
            DecisionThrottler.SetDecisionInterval(profile.DecisionInterval);
        }

        if (Vision != null)
        {
            Vision.SetVisionRadius(profile.VisionRadius);
        }

        float personalityMultiplier = Personality != null ? Personality.GetFleeThresholdMultiplier() : 1.0f;
        SetSurvivalHpThreshold(profile.GetAdjustedFleeThreshold(personalityMultiplier));

        if (AIMemory != null)
        {
            AIMemory.SetDefaultHazardDuration(profile.HazardMemoryDuration);
        }

        if (DoorwayStaging != null)
        {
            DoorwayStaging.SetHesitationDuration(profile.DoorwayHesitation);
        }

        if (ItemKitController != null)
        {
            ItemKitController.ResolveRuntimeKit();
        }
    }

    private void Update()
    {
        // Physics, agent movement, and resource stats update smoothly every frame
        UpdateResources();

        if (currentTarget != null)
        {
            targetDistance = Vector3.Distance(transform.position, currentTarget.position);
        }
        else
        {
            targetDistance = 0f;
        }
    }

    /// <summary>
    /// Evaluates Utility AI goals and requests the best goal from GOAP.
    /// Called at throttled intervals by <see cref="AIDecisionThrottler"/>.
    /// </summary>
    public void PerformStrategicDecision()
    {
        SetGoal();
    }

    /// <summary>
    /// Resets navigation, stops the active GOAP action, and clears the active goal on severe events.
    /// </summary>
    public void InterruptCurrentPlan()
    {
        activeGoal = EnumUtilGoal.None;

        if (agentBehaviour != null)
        {
            agentBehaviour.StopAction(false);
        }

        IsAgentStop(true);
    }

    private void InitializeAgent()
    {
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.Movespeed.Value;
    }

    /// <summary>
    /// Collects all UtilitySkill ScriptableObjects from the CharacterSkillController's skill list
    /// and populates the internal <see cref="skills"/> list automatically.
    /// Call this again if skills are added/removed at runtime.
    /// </summary>
    public void InitializeSkills()
    {
        skills.Clear();

        if (skillController == null) return;

        foreach (var skill in skillController.Skills)
        {
            if (skill == null) continue;

            UtilitySkill utilData = skill.UtilityData;
            if (utilData != null && !skills.Contains(utilData))
            {
                skills.Add(utilData);
            }
        }

#if UNITY_EDITOR
        if (skills.Count == 0)
            Debug.LogWarning($"[BaseAI] {name}: No UtilitySkill data found. " +
                             $"Make sure each SkillCaster's SkillData has a 'utilityData' ScriptableObject assigned, " +
                             $"or that the UtilitySkillRuntimeDatabase contains matching entries.");
        else
            Debug.Log($"[BaseAI] {name}: Initialized {skills.Count} utility skill(s): " +
                      string.Join(", ", skills.ConvertAll(s => s.GetID())));
#endif
    }

    // Emotional state Method
    #region Emotional State

    private void CalculateEmotionalState()
    {

    }

    public void SenseEnemy()
    {
        // Increase threat level when sensing an enemy, scaled by trait/race
        threatLevel += Mathf.RoundToInt(10 * EffectiveThreatMultiplier);

        if (IsFearLocked)
        {
            fearLevel = Mathf.RoundToInt(Personality.Snapshot.LockedFearValue);
        }
        else
        {
            fearLevel += Mathf.RoundToInt(5 * EffectiveFearMultiplier);
        }
    }

    public void LostEnemy()
    {
        // Decrease threat level when losing sight of an enemy
        threatLevel = Mathf.Max(0, threatLevel - Mathf.RoundToInt(10 * EffectiveThreatMultiplier));

        if (IsFearLocked)
        {
            fearLevel = Mathf.RoundToInt(Personality.Snapshot.LockedFearValue);
        }
        else
        {
            fearLevel = Mathf.Max(0, fearLevel - Mathf.RoundToInt(5 * EffectiveFearMultiplier));
        }
    }

    #endregion

    public void UpdateTargets()
    {
        if (vision != null)
        {
            enemyTargets = vision.EnemyTargets;
            allyTargets = vision.AllyTargets;
        }

        float currentTime = Time.time;

        // Synchronize directly seen enemies into known enemies
        if (enemyTargets != null && enemyTargets.Count > 0)
        {
            for (int i = 0; i < enemyTargets.Count; i++)
            {
                var enemy = enemyTargets[i];
                if (enemy == null) continue;

                // Update local known enemies
                bool existsLocally = false;
                for (int j = 0; j < localKnownEnemies.Count; j++)
                {
                    if (localKnownEnemies[j].Target == enemy)
                    {
                        var record = localKnownEnemies[j];
                        record.UpdatePosition(enemy.position, currentTime);
                        localKnownEnemies[j] = record;
                        existsLocally = true;
                        break;
                    }
                }
                if (!existsLocally)
                {
                    localKnownEnemies.Add(new KnownEnemyRecord(enemy, enemy.position, currentTime));
                }

                // If in squad, report to squad blackboard
                if (HasSquadLeader && SquadMember.Leader.Blackboard != null)
                {
                    SquadMember.Leader.Blackboard.ReportEnemy(enemy, enemy.position, currentTime);
                }
            }

            // If we have spotted enemies and shout cooldown has elapsed, mark unalerted to allow alert shout
            if (hasAlertedAllies && (currentTime - lastAlertShoutTime) >= alertShoutCooldown)
            {
                hasAlertedAllies = false;
            }
        }

        // Prune expired local known enemies
        for (int i = localKnownEnemies.Count - 1; i >= 0; i--)
        {
            if (!localKnownEnemies[i].IsValid(currentTime))
            {
                localKnownEnemies.RemoveAt(i);
            }
        }

        // Check if current target broke direct visual contact: record last known position
        if (currentTarget != null && (enemyTargets == null || !enemyTargets.Contains(currentTarget)))
        {
            if (AIMemory != null)
            {
                AIMemory.RecordLastKnownPosition(currentTarget, currentTarget.position, 6f);
            }
        }

        // Ghost threat cleanup: if no enemies remain in direct perception, known enemies, or last known target memory, clean up threat/fear
        bool hasMemoryTarget = AIMemory != null && AIMemory.HasLastKnownTarget;
        if ((enemyTargets == null || enemyTargets.Count == 0) && !HasKnownEnemies && !hasMemoryTarget && threatLevel > 0)
        {
            threatLevel = 0;
            if (!IsFearLocked)
            {
                fearLevel = 0;
            }
            hasAlertedAllies = true;
        }
    }

    public void ReceiveAlert(Transform enemy, Vector3 lastKnownPosition, float duration = 6f)
    {
        if (enemy == null) return;

        float currentTime = Time.time;
        bool existsLocally = false;
        for (int i = 0; i < localKnownEnemies.Count; i++)
        {
            if (localKnownEnemies[i].Target == enemy)
            {
                var record = localKnownEnemies[i];
                record.UpdatePosition(lastKnownPosition, currentTime);
                localKnownEnemies[i] = record;
                existsLocally = true;
                break;
            }
        }
        if (!existsLocally)
        {
            localKnownEnemies.Add(new KnownEnemyRecord(enemy, lastKnownPosition, currentTime, duration));
        }

        if (!IsScoutingActive && HasSquadLeader && SquadMember.Leader.Blackboard != null)
        {
            SquadMember.Leader.Blackboard.ReportEnemy(enemy, lastKnownPosition, currentTime, duration);
        }

        // Elevate ThreatLevel to enter combat readiness stance
        threatLevel = Mathf.Max(threatLevel, Mathf.RoundToInt(15 * EffectiveThreatMultiplier));
    }

    public void PerformAlertShout()
    {
        hasAlertedAllies = true;
        lastAlertShoutTime = Time.time;

        Transform primaryTarget = currentTarget;
        if (primaryTarget == null && enemyTargets != null && enemyTargets.Count > 0)
        {
            primaryTarget = enemyTargets[0];
        }
        else if (primaryTarget == null && HasKnownEnemies)
        {
            var known = KnownEnemies;
            if (known.Count > 0) primaryTarget = known[0].Target;
        }

        Vector3 targetPos = primaryTarget != null ? primaryTarget.position : transform.position;

        OnAlertShout?.Invoke(primaryTarget);

        // If scout is on active reconnaissance, do not alert squad or shout to allies
        if (IsScoutingActive)
        {
            return;
        }

        // 1. Squad Communication: Notify squad leader and all fellow squad members
        if (HasSquadLeader && SquadMember.Leader != null)
        {
            var leader = SquadMember.Leader;
            if (primaryTarget != null)
            {
                leader.Blackboard.ReportEnemy(primaryTarget, targetPos, lastAlertShoutTime);
            }

            var leaderAI = leader.GetComponentInChildren<BaseAI>();
            if (leaderAI != null && leaderAI != this)
            {
                leaderAI.ReceiveAlert(primaryTarget, targetPos);
            }

            var members = leader.Members;
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    if (m != null && m.IsAlive && m.BaseAI != null && m.BaseAI != this)
                    {
                        m.BaseAI.ReceiveAlert(primaryTarget, targetPos);
                    }
                }
            }
        }

        // 2. Proximity Alert Broadcast: Alert nearby non-squad allies within shout radius
        var factionMember = GetComponentInParent<FactionMember>();
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, alertShoutRadius, proximityAlertBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = proximityAlertBuffer[i];
            if (col == null) continue;

            bool isAlly = false;
            if (factionMember != null)
            {
                isAlly = factionMember.IsAllyOf(col.gameObject);
            }
            else
            {
                if ((CompareTag("Adventurer") && col.CompareTag("Adventurer")) ||
                    (CompareTag("Monster") && col.CompareTag("Monster")))
                {
                    isAlly = true;
                }
            }

            if (isAlly)
            {
                var allyAI = col.GetComponentInChildren<BaseAI>() ?? col.GetComponentInParent<BaseAI>();
                if (allyAI != null && allyAI != this)
                {
                    allyAI.ReceiveAlert(primaryTarget, targetPos);
                }
            }
        }
    }

    public void UpdateResources()
    {
        var s = StatsManager;
        if (s != null)
        {
            agentLife = s.Life;
            agentMana = s.Mana;
        }
    }

    public void SetTarget(Transform target)
    {
        CurrentTarget = target;
        TargetDistance = target != null ? Vector3.Distance(transform.position, target.position) : 0f;
    }

    public void SetTarget(Transform target, float distance)
    {
        CurrentTarget = target;
        TargetDistance = distance;
    }

    public void SetAgentLife(float life) => AgentLife = life;
    public void SetAgentMana(float mana) => AgentMana = mana;
    public void SetFearLevel(int level) => FearLevel = level;

    public void SetGoal(bool force = false)
    {
        EnumUtilGoal bestGoal = SelectBestGoal();
        if (bestGoal == EnumUtilGoal.None)
        {
            activeGoal = EnumUtilGoal.None;
            return;
        }

        bool goalChanged = bestGoal != activeGoal;
        bool hasNoActiveAction = agentBehaviour != null && agentBehaviour.ActionState.Action == null;

        if (force || goalChanged || hasNoActiveAction)
        {
            activeGoal = bestGoal;
            Planner(bestGoal);
        }
    }

    public void AgentDestination(Vector2 targetPosition)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.SetDestination(targetPosition);
        }
    }

    public bool IsAgentStop(bool value)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            return agent.isStopped = value;
        }
        return false;
    }

    public EnumUtilGoal EvaluateBestGoalForTesting()
    {
        EnumUtilGoal chosen = SelectBestGoal();
        activeGoal = chosen;
        return chosen;
    }

    public void SetActiveGoalForTesting(EnumUtilGoal goal)
    {
        activeGoal = goal;
    }

    private EnumUtilGoal SelectBestGoal()
    {
        if (goals == null || goals.Count == 0)
        {
            return EnumUtilGoal.None;
        }

        UtilityGoal bestGoal = null;
        float highestScore = 0f;

#if UNITY_EDITOR
        goalScores.Clear();
#endif

        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];
            if (goal == null) continue;

            EnumUtilGoal goalKey = goal.GetGOAPGoalKey();
            float score = goal.Evaluate(this);

            // Hysteresis: grant a commitment bonus to the currently active goal if it is still valid (score > 0)
            // so competing goals like Recover and Survive do not flip-flop on boundary ticks.
            if (score > 0f && activeGoal != EnumUtilGoal.None && goalKey == activeGoal)
            {
                score += goalHysteresisBonus;
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestGoal = goal;
            }

#if UNITY_EDITOR
            goalScores.GetPairs().Add(new SerializableDictionary<EnumUtilGoal, float>.Pair
            {
                Key = goalKey,
                Value = score
            });
#endif
        }

#if UNITY_EDITOR
        currentGoalKey = bestGoal != null ? bestGoal.GetGOAPGoalKey().ToString() : EnumUtilGoal.None.ToString();
#endif

        return bestGoal != null ? bestGoal.GetGOAPGoalKey() : EnumUtilGoal.None;
    }

    /// <summary>
    /// Selects the best UtilitySkill from the auto-populated skills list (sourced from CharacterSkillController).
    /// Called by CharacterSkillController.TryCastSkillAI().
    /// </summary>
    public UtilitySkill SelectBestSkill()
    {
        return SelectBestSkill(skills);
    }

    /// <summary>
    /// Selects the best UtilitySkill from an explicit list. Useful if you want to pass a custom subset.
    /// </summary>
    public UtilitySkill SelectBestSkill(List<UtilitySkill> utilSkills)
    {
        if (utilSkills == null || utilSkills.Count == 0)
        {
#if UNITY_EDITOR
            currentSkillKey = "None";
            currentSkillScore = 0f;
            skillScores.Clear();
#endif
            return null;
        }

        UtilitySkill bestUtilSkill = null;
        float highestScore = 0f;

#if UNITY_EDITOR
        skillScores.Clear();
#endif

        foreach (var utilSkill in utilSkills)
        {
            if (utilSkill == null) continue;
            float score = utilSkill.Evaluate(this);

#if UNITY_EDITOR
            skillScores.GetPairs().Add(new SerializableDictionary<string, float>.Pair
            {
                Key = utilSkill.GetID(),
                Value = score
            });
#endif

            if (score > highestScore)
            {
                highestScore = score;
                bestUtilSkill = utilSkill;
            }
        }

#if UNITY_EDITOR
        currentSkillKey = bestUtilSkill != null ? bestUtilSkill.GetID() : "None";
        currentSkillScore = bestUtilSkill != null ? highestScore : 0f;
#endif

        return bestUtilSkill;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Records a fallback skill execution in the inspector when no UtilitySkill scored > 0.
    /// </summary>
    public void RecordFallbackSkill(string skillId)
    {
        currentSkillKey = string.IsNullOrEmpty(skillId) ? "[Fallback]" : $"[Fallback] {skillId}";
        currentSkillScore = 0f;
    }
#endif

    #region Planner

    private void Planner(EnumUtilGoal goalKey)
    {
        if (provider == null || provider.AgentType == null) return;

        switch (goalKey)
        {
            case EnumUtilGoal.Wander:
                provider.RequestGoal<IdleGoal>();
                break;
            case EnumUtilGoal.KillEnemy:
                provider.RequestGoal<KillEnemyGoal>();
                break;
            case EnumUtilGoal.FollowLeader:
                provider.RequestGoal<FollowLeaderGoal>();
                break;
            case EnumUtilGoal.FocusFire:
                provider.RequestGoal<FocusFireGoal>();
                break;
            case EnumUtilGoal.FallBack:
                provider.RequestGoal<FallBackGoal>();
                break;
            case EnumUtilGoal.Survive:
                provider.RequestGoal<SurviveGoal>();
                break;
            case EnumUtilGoal.KeepDistance:
                provider.RequestGoal<KeepDistanceGoal>();
                break;
            case EnumUtilGoal.Standby:
                provider.RequestGoal<StandbyGoal>();
                break;
            case EnumUtilGoal.AlertAllies:
                provider.RequestGoal<AlertAlliesGoal>();
                break;
            case EnumUtilGoal.ScoutRecon:
                provider.RequestGoal<ScoutReconGoal>();
                break;
            case EnumUtilGoal.DebriefLeader:
                provider.RequestGoal<DebriefLeaderGoal>();
                break;
            case EnumUtilGoal.Recover:
                provider.RequestGoal<RecoverGoal>();
                break;
            case EnumUtilGoal.Warp:
                provider.RequestGoal<WarpGoal>();
                break;
            default:
                Debug.LogWarning("Unknown utility goal: " + goalKey);
                break;
        }
    }
    #endregion

    #region Item Recovery & Quality Heuristics (Phase 5)

    /// <summary>
    /// Returns the <see cref="ItemUseQualityTuningEntry"/> for this agent's <see cref="ItemUseQuality"/>
    /// using an O(1) enum-indexed lookup with no switch statements.
    /// </summary>
    public ItemUseQualityTuningEntry GetItemUseQualityTuningEntry()
    {
        var tuning = itemUseQualityTuning != null
            ? itemUseQualityTuning
            : (ItemKitController != null ? ItemKitController.QualityTuning : null);
        return ItemUseQualityTuningSO.GetEntryOrDefault(tuning, ItemUseQuality);
    }

    /// <summary>
    /// Computes the active HP/Mana recovery trigger fractions and per-room item quota for this agent.
    /// When <see cref="SquadOrder"/> is <see cref="SquadOrderType.Recover"/>, uses the higher squad recover target fractions.
    /// Zero allocations.
    /// </summary>
    public void GetRecoveryThresholdsAndQuota(out float hpTriggerFrac, out float manaTriggerFrac, out int perRoomQuota)
    {
        var tuning = itemUseQualityTuning != null
            ? itemUseQualityTuning
            : (ItemKitController != null ? ItemKitController.QualityTuning : null);

        ItemUseQualityTuningEntry entry = ItemUseQualityTuningSO.GetEntryOrDefault(tuning, ItemUseQuality);
        perRoomQuota = entry.PerRoomItemQuota;

        if (SquadOrder == SquadOrderType.Recover)
        {
            float squadHpTarget = tuning != null ? tuning.SquadRecoverTargetHpFraction : 0.85f;
            float squadManaTarget = tuning != null ? tuning.SquadRecoverTargetManaFraction : 0.75f;
            hpTriggerFrac = Mathf.Max(entry.HpTriggerFraction, squadHpTarget);
            manaTriggerFrac = Mathf.Max(entry.ManaTriggerFraction, squadManaTarget);
        }
        else
        {
            hpTriggerFrac = entry.HpTriggerFraction;
            manaTriggerFrac = entry.ManaTriggerFraction;
        }
    }

    /// <summary>
    /// Returns true if the agent currently has a usable recovery item (or is mid-cast on one)
    /// that satisfies its current HP, Mana, or harmful status effect triggers and per-room quota.
    /// Zero allocations.
    /// </summary>
    public bool CanRecoverWithItemNow()
    {
        var kit = ItemKitController;
        if (kit == null) return false;

        GetRecoveryThresholdsAndQuota(out float hpTrigger, out float manaTrigger, out int quota);
        return kit.HasAnyApplicableItemNow(hpTrigger, manaTrigger, quota);
    }

    /// <summary>
    /// Computes the desired world position where this agent should stand to drink a recovery item,
    /// driven by <see cref="ItemUseQualityTuningEntry"/> (panic drink in place vs. step back out of melee vs. behind Tank/party rear).
    /// Zero allocations.
    /// </summary>
    public Vector3 GetDesiredRecoveryPosition()
    {
        Vector3 myPos = transform.position;
        ItemUseQualityTuningEntry entry = GetItemUseQualityTuningEntry();

        // LateWasteful (Rank E): drinks immediately where it stands even while under attack
        if (entry.PanicDrinkUnderAttack)
        {
            return myPos;
        }

        Transform nearestEnemy = GetNearestThreatTransform(out float nearestDist);

        // WellTimed (Rank C): seek position behind Tank or at party rear
        if (entry.SeekPartyRearOrBehindTank && HasSquadLeader && SquadMember.Leader != null)
        {
            var leader = SquadMember.Leader;
            Transform anchorTank = FindLivingPartyTankTransform(leader);

            if (anchorTank != null && nearestEnemy != null)
            {
                Vector3 enemyToTank = anchorTank.position - nearestEnemy.position;
                enemyToTank.z = 0f;
                Vector3 behindDir = enemyToTank.sqrMagnitude > 0.001f ? enemyToTank.normalized : -leader.MarchingForward;
                float safeOffset = Mathf.Max(2.0f, entry.SafeEnemyDistance * 0.5f);
                Vector3 candidate = anchorTank.position + behindDir * safeOffset;

                if ((candidate - nearestEnemy.position).sqrMagnitude < entry.SafeEnemyDistance * entry.SafeEnemyDistance)
                {
                    candidate = nearestEnemy.position + behindDir * entry.SafeEnemyDistance;
                }
                return SampleNavMeshOrFallback(candidate, myPos);
            }

            // If no enemy or no separate Tank, position at the party rear behind the leader
            Vector3 rearPos = leader.transform.position - (leader.MarchingForward * 2.5f);
            if (nearestEnemy != null && (rearPos - nearestEnemy.position).sqrMagnitude < entry.SafeEnemyDistance * entry.SafeEnemyDistance)
            {
                Vector3 awayFromEnemy = myPos - nearestEnemy.position;
                awayFromEnemy.z = 0f;
                Vector3 awayDir = awayFromEnemy.sqrMagnitude > 0.001f ? awayFromEnemy.normalized : -leader.MarchingForward;
                rearPos = nearestEnemy.position + awayDir * entry.SafeEnemyDistance;
            }
            return SampleNavMeshOrFallback(rearPos, myPos);
        }

        // Adequate (Rank D) or WellTimed solo: step back out of melee range before drinking
        if (entry.BackOutOfMeleeFirst && nearestEnemy != null && nearestDist < entry.SafeEnemyDistance)
        {
            Vector3 away = myPos - nearestEnemy.position;
            away.z = 0f;
            Vector3 awayDir = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.down;
            Vector3 stepBackPos = nearestEnemy.position + awayDir * entry.SafeEnemyDistance;
            return SampleNavMeshOrFallback(stepBackPos, myPos);
        }

        return myPos;
    }

    /// <summary>
    /// Returns true when the agent is in a valid position to begin or continue its recovery item cast
    /// according to its <see cref="ItemUseQualityTuningEntry"/>.
    /// Zero allocations.
    /// </summary>
    public bool IsReadyToDrinkAtCurrentPosition(float positionTolerance = 0.65f)
    {
        ItemUseQualityTuningEntry entry = GetItemUseQualityTuningEntry();
        if (entry.PanicDrinkUnderAttack)
        {
            return true;
        }

        Transform nearestEnemy = GetNearestThreatTransform(out float nearestDist);
        if (nearestEnemy == null || nearestDist >= entry.SafeEnemyDistance)
        {
            if (!entry.SeekPartyRearOrBehindTank || !HasSquadLeader)
            {
                return true;
            }
        }

        Vector3 desiredPos = GetDesiredRecoveryPosition();
        Vector3 diff = desiredPos - transform.position;
        diff.z = 0f;
        return diff.sqrMagnitude <= (positionTolerance * positionTolerance);
    }

    private Transform GetNearestThreatTransform(out float nearestDistance)
    {
        Transform best = null;
        float bestSqr = float.MaxValue;
        Vector3 myPos = transform.position;

        if (currentTarget != null)
        {
            Vector3 d = currentTarget.position - myPos;
            d.z = 0f;
            bestSqr = d.sqrMagnitude;
            best = currentTarget;
        }

        if (enemyTargets != null)
        {
            for (int i = 0; i < enemyTargets.Count; i++)
            {
                var t = enemyTargets[i];
                if (t == null) continue;
                Vector3 d = t.position - myPos;
                d.z = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = t;
                }
            }
        }

        nearestDistance = best != null ? Mathf.Sqrt(bestSqr) : float.MaxValue;
        return best;
    }

    private Transform FindLivingPartyTankTransform(PartyLeader leader)
    {
        if (leader == null) return null;

        if (leader.IsAlive && leader.gameObject != gameObject && leader.EffectiveLeaderFunction == AdventurerFunction.Tank)
        {
            return leader.transform;
        }

        var members = leader.Members;
        if (members != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m != null && m.IsAlive && m.gameObject != gameObject && m.EffectiveFunction == AdventurerFunction.Tank)
                {
                    return m.transform;
                }
            }
        }

        return leader.IsAlive && leader.gameObject != gameObject ? leader.transform : null;
    }

    private static Vector3 SampleNavMeshOrFallback(Vector3 candidate, Vector3 fallback)
    {
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return candidate;
    }

    #endregion

    /// <summary>
    /// Computes a trait-weighted and profile-priority-weighted target utility score for a given target transform
    /// without managed allocations. Respects <see cref="TargetPriorityMode"/> (Nearest, LowestHp, SupportFirstThenLowestHp),
    /// low-health execution traits, and memory grudge.
    /// </summary>
    public float GetTraitWeightedTargetScore(Transform target, float distance)
    {
        if (target == null) return 0f;

        if (!target.TryGetComponent<CharacterStatsManager>(out var targetStats))
        {
            targetStats = target.GetComponentInParent<CharacterStatsManager>();
        }

        TargetPriorityMode priorityMode = TargetPriorityMode;
        float score;

        if (priorityMode == TargetPriorityMode.LowestHp)
        {
            if (targetStats != null)
            {
                float currentHp = Mathf.Max(0f, targetStats.Life);
                score = (10000f / (1f + currentHp)) + (1f / (1f + distance));
            }
            else
            {
                score = 100f / (1f + distance);
            }
        }
        else if (priorityMode == TargetPriorityMode.SupportFirstThenLowestHp)
        {
            AdventurerFunction targetFunc = AdventurerFunction.None;
            if (targetStats != null)
            {
                targetFunc = targetStats.CharacterFunction;
            }
            else if (target.TryGetComponent<CharacterBrain>(out var targetBrain))
            {
                targetFunc = targetBrain.CharacterFunction;
            }
            else if (target.TryGetComponent<CharacterIdentifier>(out var targetId))
            {
                targetFunc = targetId.CharacterFunction;
            }

            float hpScore = targetStats != null
                ? (10000f / (1f + Mathf.Max(0f, targetStats.Life)))
                : (100f / (1f + distance));
            float distTieBreaker = 1f / (1f + distance);
            float supportBonus = targetFunc == AdventurerFunction.Healer ? 100000f : 0f;
            score = supportBonus + hpScore + distTieBreaker;
        }
        else
        {
            // TargetPriorityMode.Nearest (default)
            score = 100f / (1f + distance);
        }

        // Low health execution bias
        if (LowHealthTargetPriority > 1f && targetStats != null)
        {
            if (targetStats.MaxLife != null && targetStats.MaxLife.Value > 0f)
            {
                float targetHpPercent = targetStats.Life / targetStats.MaxLife.Value;
                if (targetHpPercent <= ExecutionHealthThreshold)
                {
                    score *= LowHealthTargetPriority;
                }
            }
        }

        // Dual-Layer Memory: Grudge & Retaliation weighting
        if (AIMemory != null)
        {
            int targetId = target.gameObject.GetEntityId().GetHashCode();
            float grudge = AIMemory.GetGrudgeScore(targetId);
            if (grudge > 0f)
            {
                score += grudge;
            }

            // Rapid retaliation bonus against immediate attacker within window
            if (AIMemory.HasRecentAttacker && AIMemory.LastAttackerTransform == target)
            {
                float grudgeMultiplier = Personality != null ? Personality.Snapshot.GrudgeWeightMultiplier : 1.0f;
                score += 50f * grudgeMultiplier;
            }
        }

        return score;
    }

    #region Warp Item Heuristics (Phase 8)

    /// <summary>
    /// Returns true if this agent can and should use a Warp Escape item now:
    /// (1) Has usable Warp item in kit (or is already casting Warp)
    /// (2) Under threat (ThreatLevel > 0, enemy in perception or memory)
    /// (3) HP fraction &lt;= warpTriggerHpFraction
    /// (4) If requireNoPotions is true, has no usable health potions remaining
    /// (5) Recover cannot help (no recovery item usable or below emergency trigger)
    /// </summary>
    public bool CanWarpNow()
    {
        var kit = ItemKitController;
        if (kit == null) return false;

        if (kit.IsCastingItem && kit.CastingCategory == ConsumableItemCategory.Warp)
        {
            return true;
        }

        if (!kit.CanUse(ConsumableItemCategory.Warp))
        {
            return false;
        }

        // Threat check: must be in combat / under threat
        bool underThreat = ThreatLevel > 0 || HasKnownEnemies || (enemyTargets != null && enemyTargets.Count > 0);
        if (!underThreat) return false;

        var entry = GetItemUseQualityTuningEntry();

        // HP trigger check
        float maxHp = 100f;
        if (StatsManager != null && StatsManager.MaxLife != null && StatsManager.MaxLife.Value > 0f)
        {
            maxHp = StatsManager.MaxLife.Value;
        }
        float currentHp = StatsManager != null ? StatsManager.Life : AgentLife;
        float hpFrac = Mathf.Clamp01(currentHp / maxHp);

        if (hpFrac > entry.WarpTriggerHpFraction)
        {
            return false;
        }

        // requireNoPotions check
        if (entry.RequireNoPotions && kit.GetRemainingCount(ConsumableItemCategory.HealthPotion) > 0)
        {
            return false;
        }

        // Recover cannot help: no applicable recovery item right now, or HP is critically below flee threshold
        GetRecoveryThresholdsAndQuota(out float hpTrigger, out float manaTrigger, out int quota);
        bool hasPotionsUsable = kit.HasAnyApplicableItemNow(hpTrigger, manaTrigger, quota);
        if (hasPotionsUsable && !entry.RequireNoPotions && hpFrac > SurvivalHpThreshold * 0.75f)
        {
            // If potions are usable and we are not in immediate lethal range, try Recover first
            return false;
        }

        return true;
    }

    /// <summary>
    /// Computes the desired world position for performing Warp escape.
    /// If requireOutOfMelee is true and in melee of an enemy, steps back to safe distance; otherwise stands in place.
    /// </summary>
    public Vector3 GetDesiredWarpPosition()
    {
        Vector3 myPos = transform.position;
        var entry = GetItemUseQualityTuningEntry();

        if (!entry.RequireOutOfMelee)
        {
            return myPos;
        }

        Transform nearestEnemy = GetNearestThreatTransform(out float nearestDist);
        float safeDist = entry.SafeEnemyDistance > 0f ? entry.SafeEnemyDistance : 4.0f;
        if (nearestEnemy != null && nearestDist < safeDist)
        {
            Vector3 fleeDir = (myPos - nearestEnemy.position).normalized;
            if (fleeDir.sqrMagnitude < 0.01f) fleeDir = transform.right;
            return myPos + fleeDir * (safeDist - nearestDist + 1.0f);
        }

        return myPos;
    }

    /// <summary>
    /// Returns true if agent is positioned safely to start casting Warp.
    /// </summary>
    public bool IsReadyToWarpAtCurrentPosition()
    {
        var entry = GetItemUseQualityTuningEntry();
        if (!entry.RequireOutOfMelee) return true;

        Transform nearestEnemy = GetNearestThreatTransform(out float nearestDist);
        float safeDist = entry.SafeEnemyDistance > 0f ? entry.SafeEnemyDistance : 4.0f;
        return nearestEnemy == null || nearestDist >= safeDist;
    }

    #endregion
}