using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class ScoutReconAction : GoapActionBase<ScoutReconAction.Data>
{
    private const float MAX_RECON_DURATION = 6.0f;
    private const float ARRIVAL_DISTANCE = 1.5f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.Timer = MAX_RECON_DURATION;
        if (data.SquadMember != null)
        {
            data.SquadMember.IsScouting = true;
        }
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        data.Timer -= context.DeltaTime;

        // Infiltration ends early if an enemy or hazard has been detected locally
        bool hasDetectedEnemy = data.BaseAI != null && data.BaseAI.EnemyTargets != null && data.BaseAI.EnemyTargets.Count > 0;
        bool hasDetectedHazard = data.BaseAI != null && data.BaseAI.AIMemory != null && data.BaseAI.AIMemory.HazardCount > 0;

        if (hasDetectedEnemy || hasDetectedHazard || data.Timer <= 0f)
        {
            return ActionRunState.Completed;
        }

        return ActionRunState.Continue;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        return distance <= ARRIVAL_DISTANCE;
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
