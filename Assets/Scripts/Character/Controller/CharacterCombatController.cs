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
    public Transform AttackPoint => attackPoint;

    [SerializeField] private FactionMember factionMember;

    private readonly Collider2D[] hitBuffer = new Collider2D[16];
    private readonly System.Collections.Generic.List<IDamageable> hitTargetsBuffer = new(16);
    public static readonly System.Collections.Generic.List<string> DiagnosticLogs = new();

    public static int TotalSwingsAttempted { get; set; } = 0;
    public static int TotalSwingsHit { get; set; } = 0;
    public static int TotalSwingsMissed { get; set; } = 0;

    private int consecutiveMissCount = 0;
    private float cachedAttackOffset = -1f;
    private Vector2 lastAimedDirection = Vector2.right;

    public float AttackRadius => attackRadius > 0f ? attackRadius : DEFAULT_ATTACK_RADIUS;

    public float AttackOffset
    {
        get
        {
            if (cachedAttackOffset < 0f)
            {
                if (attackPoint != null)
                {
                    Vector2 diff = attackPoint.position - transform.position;
                    float mag = diff.magnitude;
                    cachedAttackOffset = mag > 0.05f ? mag : 0.4f;
                }
                else
                {
                    cachedAttackOffset = 0.4f;
                }
            }
            return cachedAttackOffset;
        }
    }

    public float MaxEffectiveReach => AttackOffset + AttackRadius;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<CharacterBrain>();
        if (stats == null) stats = GetComponent<CharacterStatsManager>();
        if (factionMember == null) factionMember = GetComponentInParent<FactionMember>();
    } 

    public bool Attack()
    {
        Transform target = brain != null && brain.BaseAI != null ? brain.BaseAI.CurrentTarget : null;
        if (target != null)
        {
            Vector2 toTarget = (Vector2)(target.position - transform.position);
            float dist = toTarget.magnitude;
            if (dist > 0.01f)
            {
                lastAimedDirection = toTarget / dist;
            }

            var flip = GetComponentInChildren<CharacterFlipRotator>();
            if (flip != null && Mathf.Abs(toTarget.x) > 0.05f)
            {
                flip.ApplyFlip(toTarget.x > 0f);
            }

            // Aim AttackPoint along the direction to target at configured offset
            if (attackPoint != null)
            {
                attackPoint.position = (Vector2)transform.position + lastAimedDirection * AttackOffset;
            }
        }
        else
        {
            // Default aiming in current facing direction
            float facingSign = transform.localScale.x >= 0f ? 1f : -1f;
            lastAimedDirection = new Vector2(facingSign, 0f);
            if (attackPoint != null)
            {
                attackPoint.position = (Vector2)transform.position + lastAimedDirection * AttackOffset;
            }
        }

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
        Transform currentTarget = brain != null && brain.BaseAI != null ? brain.BaseAI.CurrentTarget : null;
        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)(currentTarget.position - transform.position);
            float dist = toTarget.magnitude;
            if (dist > 0.01f)
            {
                lastAimedDirection = toTarget / dist;
            }
        }

        // Re-aim hit circle at animation event point
        Vector2 hitCircleCenter = (Vector2)transform.position + lastAimedDirection * AttackOffset;
        if (attackPoint != null)
        {
            attackPoint.position = hitCircleCenter;
        }

        float radius = AttackRadius;
        int hitCount = Physics2D.OverlapCircle(hitCircleCenter, radius, ContactFilter2D.noFilter, hitBuffer);
        TotalSwingsAttempted++;

        hitTargetsBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null) continue;

            var targetBrain = hit.GetComponentInParent<CharacterBrain>();
            if (targetBrain == null || targetBrain == this.brain) continue; // Skip self or non-characters

            // Prevent friendly fire
            if (factionMember != null)
            {
                if (!factionMember.IsHostileTo(targetBrain.gameObject)) continue;
            }
            else
            {
                if (CompareTag("Adventurer") && targetBrain.CompareTag("Adventurer")) continue;
                if (CompareTag("Monster") && targetBrain.CompareTag("Monster")) continue;
            }

            IDamageable targetStats = targetBrain.StatsManager;
            if (targetStats != null && !hitTargetsBuffer.Contains(targetStats))
            {
                hitTargetsBuffer.Add(targetStats);
                float damageAmount = stats != null && stats.Damage != null && stats.Damage.Value > 0 ? stats.Damage.Value : 10f;
                targetStats.TakeDamage(new DamageData(damageAmount, DamageType.Physical));
                ELogger.Log(this, nameof(SearchForTargets), $"Hit {targetBrain.name} for {damageAmount} damage");
            }
        }

        bool landedHit = hitTargetsBuffer.Count > 0;
        if (landedHit)
        {
            consecutiveMissCount = 0;
            TotalSwingsHit++;
        }
        else
        {
            consecutiveMissCount++;
            TotalSwingsMissed++;

            // Miss safety net: after 2 consecutive swings with no hit, step closer to target
            if (consecutiveMissCount >= 2 && currentTarget != null)
            {
                var agent = GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    Vector3 closePos = Vector3.MoveTowards(transform.position, currentTarget.position, 0.35f);
                    agent.SetDestination(closePos);
                }
            }
        }

        // Diagnostic logging
        if (currentTarget != null)
        {
            float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
            float aiAttackRange = stats != null && stats.AttackRange != null ? stats.AttackRange.Value : 1f;
            float aiStandingDist = brain != null && brain.BaseAI != null ? brain.BaseAI.GetPreferredStandingDistance(aiAttackRange) : aiAttackRange;
            string diag = $"[SWING_DIAG] Attacker={name} pos={transform.position:F2} | Target={currentTarget.name} pos={currentTarget.position:F2} | AimedCenter={hitCircleCenter:F2} | Radius={radius:F2} | LandedHit={landedHit} | Hits={hitCount} | TargetDist={distToTarget:F2} | ConsecutiveMisses={consecutiveMissCount} | AIRangeCheck(atkRange={aiAttackRange:F2}, standingDist={aiStandingDist:F2})";
            DiagnosticLogs.Add(diag);
        }
    }
}