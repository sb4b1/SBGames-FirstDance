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
        if (skill == null)
            return;

        GameObject target = other.transform.root.gameObject;

        if (!skill.CanHitTarget(target, HitContext.Direct))
            return;

        skill.OnHit(target, HitContext.Direct);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (skill == null)
            return;

        GameObject target = other.transform.root.gameObject;
        if (skill.CasterGameObject != null && target == skill.CasterGameObject)
        {
            skill.NotifyExitedCaster();
        }
    }
}
