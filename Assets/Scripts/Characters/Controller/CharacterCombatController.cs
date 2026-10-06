using SB4b1.Logging;
using UnityEngine;

[RequireComponent(typeof(CharacterBrain))]
[RequireComponent(typeof(CharacterStatsManager))]
public class CharacterCombatController : MonoBehaviour
{
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterStatsManager stats;

    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.4f;
    private const float DEFAULT_ATTACK_RADIUS = 0.4f;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<CharacterBrain>();
        if (stats == null) stats = GetComponent<CharacterStatsManager>();
    } 

    public bool Attack()
    {
        return brain != null && brain.TryStartAttack();
    }

    public void CastSkill()
    {
        if (brain != null && brain.SkillController != null)
        {
            brain.SkillController.TryStartCastSkillAI();
        }
        else if (brain != null)
        {
            brain.TryCastSkill();
        }
    }

    public void SearchForTargets()
    {
        Transform point = attackPoint != null ? attackPoint : transform;
        float radius = attackRadius > 0f ? attackRadius : DEFAULT_ATTACK_RADIUS;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(point.position, radius);

        var hitTargets = new System.Collections.Generic.HashSet<IDamageable>();

        foreach (Collider2D hit in hitColliders)
        {
            var targetRoot = hit.transform.root;
            if (targetRoot == this.transform.root) continue; // Skip self

            IDamageable targetStats = hit.GetComponentInParent<IDamageable>() ?? targetRoot.GetComponentInChildren<IDamageable>();
            if (targetStats != null && hitTargets.Add(targetStats))
            {
                float damageAmount = stats != null && stats.Damage != null && stats.Damage.Value > 0 ? stats.Damage.Value : 10f;
                targetStats.TakeDamage(new DamageData(damageAmount, DamageType.Physical));
                ELogger.Log(this, nameof(SearchForTargets), $"Hit {targetRoot.name} for {damageAmount} damage");
            }
        }
    }
}