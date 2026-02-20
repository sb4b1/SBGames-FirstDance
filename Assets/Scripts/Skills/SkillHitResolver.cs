using UnityEngine;

public class SkillHitResolver : MonoBehaviour
{
    public void ResolveHit(SkillInstance skill, GameObject target)
    {
        if (!target.TryGetComponent(out IDamageable damagable))
            return;

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
