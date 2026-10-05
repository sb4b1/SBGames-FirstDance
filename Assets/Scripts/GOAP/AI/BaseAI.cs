using System.Collections.Generic;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using SB4b1.Helpers;
using UnityEngine;
using UnityEngine.AI;

public class BaseAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AgentBehaviour agentBehaviour;
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterStatsManager stats;
    [SerializeField] private CharacterMovementController motionController;
    [SerializeField] private CharacterSkillController skillController;
    [SerializeField] private VisionController vision;
    [SerializeField] private List<UtilityGoal> goals;

    // Automatically populated from CharacterSkillController.Skills at runtime.
    // Each Skill's UtilityData (ScriptableObject) is collected here so SelectBestSkill can evaluate them.
    // You do NOT need to assign these manually in the Inspector.
    [SerializeField] private List<UtilitySkill> skills = new();

    public CharacterBrain Brain => brain != null ? brain : (brain = GetComponentInParent<CharacterBrain>());
    public CharacterSkillController SkillController => skillController;
    public List<UtilitySkill> Skills => skills;

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
    public List<Transform> EnemyTargets { get => enemyTargets; private set => enemyTargets = value; }
    public List<Transform> AllyTargets { get => allyTargets; private set => allyTargets = value; }

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

        if (this.provider.AgentTypeBehaviour == null)
            this.provider.AgentType = this.goap.GetAgentType(agentType);

        if (GetComponent<AIDecisionThrottler>() == null)
        {
            gameObject.AddComponent<AIDecisionThrottler>();
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

        InitializeAgent();
        InitializeSkills();
    }

    private void Update()
    {
        // Physics, agent movement, and resource stats update smoothly every frame
        UpdateResources();
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
        // Increase threat level when sensing an enemy
        threatLevel += 10;
        fearLevel += 5; // Optionally increase fear level as well
    }

    public void LostEnemy()
    {
        // Decrease threat level when losing sight of an enemy
        threatLevel -= 10;
        fearLevel -= 5; // Optionally decrease fear level as well
    }

#endregion

    public void UpdateTargets()
    {
        enemyTargets = vision.EnemyTargets;
        allyTargets = vision.AllyTargets;
    }

    public void UpdateResources()
    {
        agentLife = stats.Life;
        agentMana = stats.Mana;
    }

    public void SetTarget(Transform target) => CurrentTarget = target;

    public void SetAgentLife(float life) => AgentLife = life;
    public void SetAgentMana(float mana) => AgentMana = mana;

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

    private EnumUtilGoal SelectBestGoal()
    {
        UtilityGoal bestGoal = null;
        float highestScore = 0f;

        foreach (var goal in goals)
        {
            float score = goal.Evaluate(this);

            if (score > highestScore)
            {
                highestScore = score;
                bestGoal = goal;
            }
        }

#if UNITY_EDITOR
        // Update the current goal key and scores for debugging purposes
        currentGoalKey = bestGoal != null ? bestGoal.GetGOAPGoalKey().ToString() : EnumUtilGoal.None.ToString();
        goalScores.Clear();

        foreach (var goal in goals)
        {
            float score = goal.Evaluate(this);
            goalScores.GetPairs().Add(new SerializableDictionary<EnumUtilGoal, float>.Pair
            {
                Key = goal.GetGOAPGoalKey(),
                Value = score
            });
        }
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
        switch (goalKey)
        {
            case EnumUtilGoal.Wander:
                provider.RequestGoal<WanderGoal>();
                break;
            case EnumUtilGoal.KillEnemy:
                provider.RequestGoal<KillEnemyGoal>();
                break;
            default:
                Debug.LogWarning("Unknown utility goal: " + goalKey);
                break;
        }
    }
#endregion
}