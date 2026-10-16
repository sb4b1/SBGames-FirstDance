using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class HealAllyAction : GoapActionBase<HealAllyAction.Data>
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
                // Find first ready AlliesOnly heal skill
                CharacterSkillController.Skill healSkill = null;
                var skillList = skillController.Skills;
                if (skillList != null)
                {
                    for (int i = 0; i < skillList.Count; i++)
                    {
                        var s = skillList[i];
                        if (s != null && s.Data != null && s.Data.targetFilter == SkillData.SkillTargetFilter.AlliesOnly && s.IsReady)
                        {
                            healSkill = s;
                            break;
                        }
                    }
                }

                if (healSkill == null)
                {
                    if (data.Brain != null) data.Brain.EndCastSkill(0f);
                    return ActionRunState.Completed;
                }

                // Dynamic facing towards heal target before cast
                if (targetObj != null)
                {
                    float dx = targetObj.transform.position.x - data.Brain.transform.position.x;
                    var flip = data.Brain.GetComponentInChildren<CharacterFlipRotator>();
                    if (flip != null && Mathf.Abs(dx) > 0.05f) flip.ApplyFlip(dx > 0f);
                }

                bool castStarted = skillController.StartCasting(healSkill, targetObj);
                if (!castStarted)
                {
                    if (data.Brain != null) data.Brain.EndCastSkill(0f);
                    return ActionRunState.Completed;
                }
            }
            else
            {
                return ActionRunState.Completed;
            }

            data.CastInitiated = true;
            return ActionRunState.Continue;
        }

        // Safety timeout to prevent soft-locks
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
            var skillList = skillController.Skills;
            if (skillList != null)
            {
                for (int i = 0; i < skillList.Count; i++)
                {
                    var s = skillList[i];
                    if (s != null && s.Data != null && s.Data.targetFilter == SkillData.SkillTargetFilter.AlliesOnly)
                    {
                        return distance <= s.Data.EffectiveRange;
                    }
                }
            }
        }

        return distance <= 6f;
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
        public CharacterStatsManager Stats { get; set; }

        public CharacterSkillController SkillController =>
            (AI != null && AI.SkillController != null)
                ? AI.SkillController
                : (Brain != null ? Brain.GetComponentInChildren<CharacterSkillController>() : null);
    }
}
