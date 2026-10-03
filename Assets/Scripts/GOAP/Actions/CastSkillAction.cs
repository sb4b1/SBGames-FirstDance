using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;

public class CastSkillAction : GoapActionBase<CastSkillAction.Data>
{
    public override void Start(IMonoAgent agent, Data data)
    {
        if (data.Brain != null)
        {
            data.Brain.TryCastSkill();
        }
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (data.SkillController != null)
        {
            data.SkillController.TryCastSkillAI();
        }
        else if (data.Controller != null)
        {
            data.Controller.CastSkill();
        }

        if (data.Brain != null)
        {
            data.Brain.TryCastSkill();
        }

        return ActionRunState.WaitThenComplete(1.2f);
    }

    public override void End(IMonoAgent agent, Data data)
    {
        if (data.Brain != null)
        {
            data.Brain.EndCastSkill();
        }
    }

    public override void Stop(IMonoAgent agent, Data data)
    {
        if (data.Brain != null)
        {
            data.Brain.EndCastSkill();
        }
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        if (data.SkillController != null)
        {
            return distance <= data.SkillController.GetBestReadySkillRange();
        }

        if (data.Stats != null && data.Stats.AttackRange != null)
        {
            return distance <= data.Stats.AttackRange.Value;
        }

        return distance <= 5f;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }

        [GetComponentInParent]
        public CharacterSkillController SkillController { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterCombatController Controller { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}