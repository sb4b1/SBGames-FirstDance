using System.Collections;
using UnityEngine;

public class AoEDetonation : MonoBehaviour, ISkillDetonation
{
    public void Detonate(SkillInstance skill)
    {
        var targeting = skill.GetComponent<SkillTargeting>();

        foreach (var target in targeting.GetAOETargets())
        {
            skill.OnHit(target, HitContext.AoE);
        }

        skill.OnDespawn();
    }
}