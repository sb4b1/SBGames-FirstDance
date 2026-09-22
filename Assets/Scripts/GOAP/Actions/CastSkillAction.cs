using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;

public class CastSkillAction : GoapActionBase<CastSkillAction.Data>
{    
    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        data.Controller.CastSkill();
        return ActionRunState.WaitThenComplete(2f);
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        return distance <= data.Stats.AttackRange.Value;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }

        [GetComponentInParent]
        public CharacterCombatController Controller { get; set; }
        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}