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
        if (skill.HitTargets.Contains(other.gameObject))
            return;

        skill.OnHit(other.gameObject, HitContext.Direct);
    }
}
