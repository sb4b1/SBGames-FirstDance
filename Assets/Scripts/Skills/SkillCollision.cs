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
        GameObject target = other.transform.root.gameObject;

        if (target == null)
            return;

        if (skill.HitTargets.Contains(target))
            return;

        if (target.TryGetComponent(out IIdentifier objID) && objID.UniqueId == skill.casterID)
            return;

        skill.OnHit(target, HitContext.Direct);
    }
}
