using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class FollowLeaderAction : GoapActionBase<FollowLeaderAction.Data>
{
    private const float DEFAULT_FOLLOW_RADIUS = 0.75f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.Timer = 0.5f;
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        data.Timer -= context.DeltaTime;
        return data.Timer <= 0f ? ActionRunState.Completed : ActionRunState.Continue;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        return distance <= DEFAULT_FOLLOW_RADIUS;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public float Timer { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}
