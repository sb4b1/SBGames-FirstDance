using System.Collections;
using UnityEngine;

public class AoEDetonation : MonoBehaviour, ISkillDetonation
{
    public void Detonate(SkillInstance skill)
    {
        var targeting = skill.GetComponent<SkillTargeting>();
        if (targeting != null)
        {
            var targets = targeting.GetAOETargets();
            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null) continue;
                    GameObject targetRoot = target.transform.root.gameObject;
                    if (skill.CanHitTarget(targetRoot, HitContext.AoE))
                    {
                        skill.OnHit(targetRoot, HitContext.AoE);
                    }
                }
            }
        }

        skill.OnDespawn();
    }
}