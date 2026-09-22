using System;
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
    [SerializeField] private VisionController vision;
    [SerializeField] private List<UtilityGoal> goals;
    
    // This can be used to differentiate between different types of agents (e.g., "NormalAgent", "AggressiveAgent", etc.)
    // NormalAgent is the default type, and if no specific behavior is assigned in the GoapActionProvider, it will use the NormalAgent behavior
    [SerializeField] private string agentType = "NormalAgent";

    private GoapBehaviour goap;
    private GoapActionProvider provider;

    [SerializeField] private Transform currentTarget;
    [SerializeField] private List<Transform> enemyTargets;
    [SerializeField] private List<Transform> allyTargets;

    public Transform CurrentTarget { get => currentTarget; private set => currentTarget = value; }
    public List<Transform> EnemyTargets { get => enemyTargets; private set => enemyTargets = value; }
    public List<Transform> AllyTargets { get => allyTargets; private set => allyTargets = value; }

    [SerializeField] private int threatLevel;
    [SerializeField] private int fearLevel;
    [SerializeField] private float agentLife;

    public int ThreatLevel { get => threatLevel; private set => threatLevel = value; }
    public int FearLevel { get => fearLevel; private set => fearLevel = value; }
    public float AgentLife { get => agentLife; private set => agentLife = value; }

#if UNITY_EDITOR
    [Header("Debugging")]
    [SerializeField] private string currentGoalKey = EnumUtilGoal.None.ToString();
    [SerializeField] private SerializableDictionary<EnumUtilGoal, float> goalScores = new();
#endif








    private void Awake()
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

    private void Update()
    {
        SetGoal();
    }

    private void InitializeAgent()
    {
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = stats.Movespeed.Value;
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

    public void SetTarget(Transform target) => CurrentTarget = target;

    private void SetGoal()
    {
        Planner(SelectBestGoal());
    }

    public void AgentDestination(Vector2 targetPosition) => agent.SetDestination(targetPosition);
    public bool IsAgentStop(bool value) => agent.isStopped = value;

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

    public UtilitySkill SelectBestSkill(List<UtilitySkill> utilSkills)
    {
        UtilitySkill bestUtilSkill = null;
        float highestScore = 0f;

        foreach (var utilSkill in utilSkills)
        {
            float score = utilSkill.Evaluate(this);

            if (score > highestScore)
            {
                highestScore = score;
                bestUtilSkill = utilSkill;
            }
        }
        return bestUtilSkill;
    }
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