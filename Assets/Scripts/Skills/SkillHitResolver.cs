using SB4b1.Logging;
using UnityEngine;

public class SkillHitResolver : MonoBehaviour
{
    public void ResolveHit(SkillInstance skill, GameObject target)
    {

        IDamageable damagable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>() ?? target.transform.root.GetComponentInChildren<IDamageable>();
        if (damagable == null)
        {
            ELogger.Warning(this, nameof(ResolveHit), $"Target {target.name} does not implement IDamageable. Cannot apply damage.");
            return;
        }

        damagable.TakeDamage(new DamageData(
            skill.CurrentDamage,
            skill.Data.damageType,
            skill
        ));

        StatusEffectManager sem = target.GetComponent<StatusEffectManager>() ?? target.GetComponentInParent<StatusEffectManager>() ?? target.transform.root.GetComponentInChildren<StatusEffectManager>();
        if (sem == null)
            return;

        foreach (var application in skill.Data.statusEffects)
        {
            if (Random.value > application.procChance)
                continue;

            sem.ApplyEffect(application.effect);
        }
    }
}
