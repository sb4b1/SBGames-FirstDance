using SB4b1.Logging;
using UnityEngine;

public class SkillHitResolver : MonoBehaviour
{
    public void ResolveHit(SkillInstance skill, GameObject target)
    {

        if (!target.TryGetComponent(out IDamageable damagable))
        {
            ELogger.Warning(this, nameof(ResolveHit), $"Target {target.name} does not implement IDamageable. Cannot apply damage.");
            return;
        }

        #if UNITY_EDITOR
        ELogger.Log(this, nameof(ResolveHit), $"Skill {skill.Data.name} hit target {target.name} for {skill.CurrentDamage} damage.");
        #endif

    

        damagable.TakeDamage(new DamageData(
            skill.CurrentDamage,
            skill.Data.damageType,
            skill
        ));

        if (!target.TryGetComponent(out StatusEffectManager sem))
            return;

        foreach (var application in skill.Data.statusEffects)
        {
            if (Random.value > application.procChance)
                continue;

            sem.ApplyEffect(application.effect);
        }
    }
}
