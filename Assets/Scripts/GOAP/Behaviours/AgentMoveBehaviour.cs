using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using SB4b1.Logging;
using UnityEngine;

[RequireComponent(typeof(AgentBehaviour))]
public class AgentMoveBehaviour : MonoBehaviour
{
    [SerializeField] private BaseAI agent;
    [SerializeField] private AgentBehaviour agentBehaviour;

    private ITarget currentTarget;
    private bool shouldMove;
    private Vector3 lastPosition;

    public bool ShouldMove { get { return shouldMove; } }

    private void Awake()
    {
        agentBehaviour = GetComponent<AgentBehaviour>();
        if(agent == null) agent = GetComponent<BaseAI>();
    }

    private void OnEnable()
    {
        this.agentBehaviour.Events.OnTargetInRange += this.TargetInRange;
        this.agentBehaviour.Events.OnTargetChanged += this.TargetChanged;
        this.agentBehaviour.Events.OnTargetNotInRange += this.TargetNotInRange;
        this.agentBehaviour.Events.OnTargetLost += this.TargetLost;
    }

    private void OnDisable()
    {
        this.agentBehaviour.Events.OnTargetInRange -= this.TargetInRange;
        this.agentBehaviour.Events.OnTargetChanged -= this.TargetChanged;
        this.agentBehaviour.Events.OnTargetNotInRange -= this.TargetNotInRange;
        this.agentBehaviour.Events.OnTargetLost -= this.TargetLost;
    }

    private void TargetLost()
    {
        this.currentTarget = null;
        this.shouldMove = false;
        agent.IsAgentStop(true);
    }

    private void TargetInRange(ITarget target)
    {
        this.shouldMove = false;
        agent.IsAgentStop(true);
    }

    private void TargetChanged(ITarget target, bool inRange)
    {
        this.currentTarget = target;
        this.shouldMove = !inRange;
        agent.IsAgentStop(inRange);
    }

    private void TargetNotInRange(ITarget target)
    {
        this.shouldMove = true;
        if (agent != null && agent.Brain != null && !agent.Brain.CanMove)
        {
            agent.IsAgentStop(true);
            return;
        }
        agent.IsAgentStop(false);
    }

    public void Update()
    {
        if (!this.shouldMove) return;
        if (this.currentTarget == null) return;

        if (agent != null && agent.Brain != null && !agent.Brain.CanMove)
        {
            agent.IsAgentStop(true);
            return;
        }

        if (this.lastPosition == currentTarget.Position) return;

        lastPosition = currentTarget.Position;
        agent.IsAgentStop(false);
        agent.AgentDestination(new Vector2(this.currentTarget.Position.x, this.currentTarget.Position.y));
    }

    private void OnDrawGizmos()
    {
        if (this.currentTarget == null)
            return;

        Gizmos.DrawLine(this.transform.position, this.currentTarget.Position);
    }
}