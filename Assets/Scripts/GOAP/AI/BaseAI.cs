using System.Collections.Generic;
using CrashKonijn.Goap.Runtime;
using SB4b1.Helpers;
using UnityEngine;
using UnityEngine.AI;

public class BaseAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterStatsManager stats;
    [SerializeField] private CharacterMovementController controller;
    [SerializeField] private List<UtilityGoal> goals;
    
    // This can be used to differentiate between different types of agents (e.g., "NormalAgent", "AggressiveAgent", etc.)
    // NormalAgent is the default type, and if no specific behavior is assigned in the GoapActionProvider, it will use the NormalAgent behavior
    [SerializeField] private string agentType = "NormalAgent";

    protected GoapBehaviour goap;
    protected GoapActionProvider provider;

    public Transform CurrentTarget { get; protected set; }
    public Transform[] EnemyTargets { get; protected set; }
    public Transform[] AllyTargets { get; protected set; }

    public int ThreatLevel { get; protected set; }
    public int FearLevel { get; protected set; }
    public float AgentLife { get; protected set; }

#if UNITY_EDITOR
    [Header("Debugging")]
    [SerializeField] private string currentGoalKey = EnumUtilGoal.None.ToString();
    [SerializeField] private SerializableDictionary<EnumUtilGoal, float> goalScores = new();
#endif








    protected virtual void Awake()
    {
        this.goap = FindAnyObjectByType<GoapBehaviour>();
        this.provider = this.GetComponent<GoapActionProvider>();

        if (this.provider.AgentTypeBehaviour == null)
            this.provider.AgentType = this.goap.GetAgentType(agentType);
    }
 
    private void Start()
    {
        if (stats == null) stats = GetComponentInParent<CharacterStatsManager>();
        if (controller == null) controller = GetComponentInParent<CharacterMovementController>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        InitializeAgent();
    }

    protected virtual void Update()
    {
        SetGoal();
    }

    private void InitializeAgent()
    {
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.Movespeed.Value;
    }

    protected virtual void SetGoal()
    {
        Planner(SelectBestGoal());
    }

    public void SetTarget(Transform target) => CurrentTarget = target;

    public void SetEnemyTargets(Transform[] targets) => EnemyTargets = targets;

    public void AgentDestination(Vector2 targetPosition) => agent.SetDestination(targetPosition);
    public bool IsAgentStop(bool value) => agent.isStopped = value;

    private void Planner(EnumUtilGoal goalKey)
    {
        switch (goalKey)
        {
            case EnumUtilGoal.Wander:
                provider.RequestGoal<WanderGoal>();
                break;
            default:
                Debug.LogWarning("Unknown utility goal: " + goalKey);
                break;
        }
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

}