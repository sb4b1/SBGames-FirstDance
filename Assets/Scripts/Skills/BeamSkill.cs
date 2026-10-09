using UnityEngine;

public class BeamSkill : MonoBehaviour
{
    private SkillInstance skill;
    private SkillTargeting targeting;
    private SkillHitResolver hitResolver;

    private float tickTimer;

    private void Awake()
    {
        skill = GetComponent<SkillInstance>();
        targeting = GetComponent<SkillTargeting>();
        hitResolver = GetComponent<SkillHitResolver>();
    }

    private void Update()
    {
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0) return;

        tickTimer = skill.Stats != null ? skill.Stats.BeamTickInterval.Value : (skill.Data != null ? skill.Data.beamTickInterval : 0.1f);

        var targets = targeting.GetBeamTargets();
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target != null)
                {
                    skill.OnHit(target, HitContext.BeamTick);
                }
            }
        }
    }
}
