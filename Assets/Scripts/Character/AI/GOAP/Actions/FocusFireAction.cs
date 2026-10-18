using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

public class FocusFireAction : GoapActionBase<FocusFireAction.Data>
{
    private const float ATTACK_TIMEOUT = 1.5f;

    public override void Start(IMonoAgent agent, Data data)
    {
        data.AttackInitiated = false;
        data.Timer = ATTACK_TIMEOUT;
    }

    public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
    {
        if (!data.AttackInitiated)
        {
            if (data.Brain != null && !data.Brain.CanAttack)
            {
                return ActionRunState.Continue;
            }

            // Check if ready skill can be used on the focus target first
            if (data.SkillController != null && data.SkillController.HasReadySkill())
            {
                GameObject targetObj = null;
                if (data.Target is TransformTarget tt && tt.Transform != null)
                {
                    targetObj = tt.Transform.gameObject;
                }

                if (data.SkillController.TryStartCastSkillAI(targetObj))
                {
                    data.AttackInitiated = true;
                    return ActionRunState.Continue;
                }
            }

            // Fallback to standard attack
            if (data.Controller != null)
            {
                bool started = data.Controller.Attack();
                if (started)
                {
                    data.AttackInitiated = true;
                    return ActionRunState.Continue;
                }
            }

            return ActionRunState.Continue;
        }

        data.Timer -= context.DeltaTime;
        if (data.Timer <= 0f)
        {
            if (data.Brain != null && (data.Brain.IsAttacking || data.Brain.IsCastingSkill))
            {
                data.Brain.InterruptAllActions();
            }
            return ActionRunState.Completed;
        }

        if (data.Brain != null && (data.Brain.IsAttacking || data.Brain.IsCastingSkill))
        {
            return ActionRunState.Continue;
        }

        return ActionRunState.Completed;
    }

    public override void End(IMonoAgent agent, Data data)
    {
        data.AttackInitiated = false;
    }

    public override void Stop(IMonoAgent agent, Data data)
    {
        if (data.Brain != null && (data.Brain.IsAttacking || data.Brain.IsCastingSkill))
        {
            data.Brain.InterruptAllActions();
        }
        data.AttackInitiated = false;
    }

    public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references)
    {
        float targetExtent = 0.25f;
        if (data.Target is TransformTarget tt && tt.Transform != null)
        {
            var col = tt.Transform.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                targetExtent = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
            }
        }

        float maxReachWithTarget = 0.85f;
        if (data.Controller != null)
        {
            maxReachWithTarget = data.Controller.MaxEffectiveReach + targetExtent;
        }

        float statAttackRange = data.Stats != null && data.Stats.AttackRange != null ? data.Stats.AttackRange.Value : 1f;
        float standingDistance = data.AI != null ? data.AI.GetPreferredStandingDistance(statAttackRange) : statAttackRange;

        float effectiveAttackRange = Mathf.Min(standingDistance, maxReachWithTarget);
        return distance <= effectiveAttackRange;
    }

    public class Data : IActionData
    {
        public ITarget Target { get; set; }
        public bool AttackInitiated { get; set; }
        public float Timer { get; set; }

        [GetComponentInParent]
        public BaseAI AI { get; set; }

        [GetComponentInParent]
        public CharacterBrain Brain { get; set; }

        [GetComponentInParent]
        public CharacterCombatController Controller { get; set; }

        [GetComponentInParent]
        public CharacterSkillController SkillController { get; set; }

        [GetComponentInParent]
        public CharacterStatsManager Stats { get; set; }
    }
}
