using SB4b1.Logging;
using UnityEngine;

[RequireComponent(typeof(CharacterBrain))]
[RequireComponent(typeof(CharacterStatsManager))]
public class CharacterCombatController : MonoBehaviour
{
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterStatsManager stats;

    [SerializeField] private Transform attackPoint;
    private const float ATTACK_RADIUS = 0.15f;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<CharacterBrain>();
        if (stats == null) stats = GetComponent<CharacterStatsManager>();
    } 

    public void Attack()
    {
        brain.TryStartAttack();
    }

    public void CastSkill()
    {
        brain.TryCastSkill();
    }

    public void SearchForTargets()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPoint.transform.position, ATTACK_RADIUS);

        foreach (Collider2D hit in hitColliders)
        {
            var target = hit.transform.root;
            if (target == this.transform) continue; // Skip self
            target.TryGetComponent(out IDamageable targetStats);
            ELogger.Log(this, nameof(SearchForTargets), $"Hit: {target.name}");
        }
    }
}