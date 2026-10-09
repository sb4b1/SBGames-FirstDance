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

        DamageType damageType = skill != null && skill.Data != null ? skill.Data.damageType : DamageType.Physical;
        damagable.TakeDamage(new DamageData(
            skill != null ? skill.CurrentDamage : 0f,
            damageType,
            skill
        ));

        StatusEffectManager sem = target.GetComponent<StatusEffectManager>() ?? target.GetComponentInParent<StatusEffectManager>() ?? target.transform.root.GetComponentInChildren<StatusEffectManager>();
        if (sem == null)
            return;

        if (skill?.Data != null && skill.Data.statusEffects != null)
        {
            var effects = skill.Data.statusEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                var application = effects[i];
                if (application != null && Random.value <= application.procChance)
                {
                    sem.ApplyEffect(application.effect);
                }
            }
        }
    }
}
