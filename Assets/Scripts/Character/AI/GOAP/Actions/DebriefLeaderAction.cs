using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class DebriefLeaderAction : GoapActionBase<DebriefLeaderAction.Data>
{
    private const float DEBRIEF_PROXIMITY = 2.5f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.Timer = 0.5f;
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (data.SquadMember == null || !data.SquadMember.HasLeader)
        {
            return ActionRunState.Completed;
        }

        float distSqr = (agent.Transform.position - data.SquadMember.Leader.transform.position).sqrMagnitude;
        if (distSqr <= DEBRIEF_PROXIMITY * DEBRIEF_PROXIMITY)
        {
            // Debrief / transfer local records into leader's SquadBlackboard
            data.SquadMember.Leader.SynchronizeScoutIntel(data.SquadMember);
            data.SquadMember.IsScouting = false;
            return ActionRunState.Completed;
        }

        data.Timer -= context.DeltaTime;
        return ActionRunState.Continue;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        return distance <= DEBRIEF_PROXIMITY;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public float Timer { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }

        [GetComponentInParent]
        public SquadMember SquadMember { get; set; }

        [GetComponentInParent]
        public BaseAI BaseAI { get; set; }
    }
}
