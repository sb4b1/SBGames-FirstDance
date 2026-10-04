using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class CastSkillAction : GoapActionBase<CastSkillAction.Data>
{
    public override void Start(IMonoAgent agent, Data data)
    {
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        GameObject targetObj = null;
        if (data.Target is TransformTarget tt && tt.Transform != null)
        {
            targetObj = tt.Transform.gameObject;
        }

        var skillController = data.SkillController;
        if (skillController != null)
        {
            bool castStarted = skillController.TryStartCastSkillAI(targetObj);
            if (!castStarted)
            {
                if (data.Brain != null)
                {
                    data.Brain.EndCastSkill();
                }
                return ActionRunState.Completed;
            }

            return ActionRunState.WaitThenComplete(1.2f);
        }

        if (data.Controller != null)
        {
            data.Controller.CastSkill();
            return ActionRunState.WaitThenComplete(1.2f);
        }

        return ActionRunState.Completed;
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
        var skillController = data.SkillController;
        if (skillController != null)
        {
            return distance <= skillController.GetBestReadySkillRange();
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
        public BaseAI AI { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterCombatController Controller { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }

        public CharacterSkillController SkillController =>
            (AI != null && AI.SkillController != null)
                ? AI.SkillController
                : (Brain != null ? Brain.GetComponentInChildren<CharacterSkillController>() : null);
    }
}