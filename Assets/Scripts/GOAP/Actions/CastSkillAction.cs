using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class CastSkillAction : GoapActionBase<CastSkillAction.Data>
{
    private const float DEFAULT_SAFETY_TIMEOUT = 3.5f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.CastInitiated = false;
        data.SafetyTimeout = DEFAULT_SAFETY_TIMEOUT;
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (!data.CastInitiated)
        {
            // If brain is busy or in recovery, wait in combat idle before starting cast
            if (data.Brain != null && !data.Brain.CanCastSkill)
            {
                return ActionRunState.Continue;
            }

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
                        data.Brain.EndCastSkill(0f);
                    }
                    return ActionRunState.Completed;
                }
            }
            else if (data.Controller != null)
            {
                data.Controller.CastSkill();
            }

            data.CastInitiated = true;
            return ActionRunState.Continue;
        }

        // Safety timeout to prevent soft-locks if animation events are not configured
        data.SafetyTimeout -= context.DeltaTime;
        if (data.SafetyTimeout <= 0f)
        {
            if (data.Brain != null)
            {
                data.Brain.InterruptAllActions();
            }
            return ActionRunState.Completed;
        }

        // Stay in action while skill animation is playing
        if (data.Brain != null && data.Brain.IsCastingSkill)
        {
            return ActionRunState.Continue;
        }

        return ActionRunState.Completed;
    }

    public override void End(IMonoAgent agent, Data data)
    {
        data.CastInitiated = false;
    }

    public override bool IsValid(IActionReceiver agent, Data data)
    {
        // While cast animation is actively playing, protect the action from cancellation
        if (data.CastInitiated && data.Brain != null && data.Brain.IsCastingSkill)
        {
            return true;
        }

        return base.IsValid(agent, data);
    }

    public override void Stop(IMonoAgent agent, Data data)
    {
        if (data.Brain != null && data.Brain.IsCastingSkill)
        {
            data.Brain.EndCastSkill();
        }
        data.CastInitiated = false;
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
        public bool CastInitiated { get; set; }
        public float SafetyTimeout { get; set; }

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