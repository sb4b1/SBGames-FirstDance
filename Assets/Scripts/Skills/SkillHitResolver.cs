using SB4b1.Logging;
using UnityEngine;

public class SkillHitResolver : MonoBehaviour
{
    public static event System.Action<SkillInstance, GameObject, float> OnSkillHealApplied;

    public void ResolveHit(SkillInstance skill, GameObject target)
    {
        if (target == null) return;

        // Healing skills (AlliesOnly or SelfOnly): restore life to target's CharacterStatsManager
        if (skill != null && skill.Data != null &&
            (skill.Data.targetFilter == SkillData.SkillTargetFilter.AlliesOnly || skill.Data.targetFilter == SkillData.SkillTargetFilter.SelfOnly))
        {
            var stats = target.GetComponent<CharacterStatsManager>() ?? target.GetComponentInParent<CharacterStatsManager>();
            if (stats != null)
            {
                float healAmount = skill.CurrentDamage > 0f ? skill.CurrentDamage : skill.Data.baseDamage;
                OnSkillHealApplied?.Invoke(skill, target, healAmount);
                stats.RestoreLife(healAmount);
            }
            return;
        }

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
