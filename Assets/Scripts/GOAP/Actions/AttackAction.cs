using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;

public class AttackAction : GoapActionBase<AttackAction.Data>
{    
    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        data.Controller.Attack();
        return ActionRunState.WaitThenComplete(2f);
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        return distance <= data.Stats.AttackRange.Value;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public float Distance { get; set; }

        [GetComponentInParent]
        public CharacterCombatController Controller { get; set; }
        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}