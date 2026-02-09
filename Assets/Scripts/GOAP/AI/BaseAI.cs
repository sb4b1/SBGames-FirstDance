using CrashKonijn.Goap.Runtime;
using UnityEngine;
using UnityEngine.AI;

public class BaseAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterStatsManager stats;
    [SerializeField] private CharacterMovementController controller;

    public Transform CurrentTarget { get; protected set; }
    public Transform[] EnemyTargets { get; protected set; }
    public Transform[] AllyTargets { get; protected set; }

    public int ThreatLevel { get; protected set; }
    public int FearLevel { get; protected set; }
    public float AgentLife { get; protected set; }

    protected GoapActionProvider provider;
    protected GoapBehaviour goap;

    protected virtual void Awake()
    {
        this.goap = FindAnyObjectByType<GoapBehaviour>();
        this.provider = this.GetComponent<GoapActionProvider>();
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

    protected virtual void SetGoal() { }

    public void SetTarget(Transform target) => CurrentTarget = target;

    public void SetEnemyTargets(Transform[] targets) => EnemyTargets = targets;

    public void AgentDestination(Vector2 targetPosition) => agent.SetDestination(targetPosition);
    public bool IsAgentStop(bool value) => agent.isStopped = value;

}