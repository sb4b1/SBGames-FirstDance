using SB4b1.Logging;
using UnityEngine;

public class SkillCollision : MonoBehaviour
{
    private SkillInstance skill;

    private void Awake()
    {
        skill = GetComponentInParent<SkillInstance>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DamageReceiver receiver = other.GetComponentInParent<DamageReceiver>();
        ELogger.Log(this, nameof(OnTriggerEnter2D), $"Skill {skill.Data.name} collided with {other.gameObject.name}");

        //if (receiver == null)
        //    return;

        //GameObject target = receiver.Damageable as MonoBehaviour != null ? (receiver.Damageable as MonoBehaviour).gameObject : null;

        if (skill.HitTargets.Contains(other.gameObject))
            return;

        if (other.gameObject.TryGetComponent(out CharacterIdentifier otherCharID) && otherCharID.ID == skill.casterID)
            return;

        skill.OnHit(other.gameObject, HitContext.Direct);
    }
}
