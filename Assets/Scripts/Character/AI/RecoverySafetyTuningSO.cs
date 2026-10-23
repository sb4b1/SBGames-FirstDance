using UnityEngine;

/// <summary>Immutable recovery safety curves. Reads only the AI blackboard and its damage memory.</summary>
[CreateAssetMenu(fileName = "RecoverySafety_Default", menuName = "SBGames/AI/Recovery Safety Tuning")]
public class RecoverySafetyTuningSO : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float maximumThreat = 80f;
    [SerializeField, Min(0.01f)] private float recentDamageWindow = 2f;
    [SerializeField, Range(0.01f, 1f)] private float dangerousDamageFraction = 0.25f;
    [SerializeField, Min(0f)] private float dangerDistance = 1.5f;
    [SerializeField, Min(0.01f)] private float safeDistance = 5f;
    [SerializeField] private AnimationCurve threatSafety = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve damageSafety = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve proximitySafety = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float Evaluate(BaseAI ai)
    {
        if (ai == null) return 0f;
        float threat = Mathf.Clamp01(ai.ThreatLevel / Mathf.Max(0.01f, maximumThreat));
        var stats = ai.StatsManager;
        float maxHp = stats != null ? stats.MaxLife.Value : 1f;
        float damage = ai.AIMemory != null ? ai.AIMemory.GetRecentDamage(recentDamageWindow) : 0f;
        float damageRatio = Mathf.Clamp01(damage / Mathf.Max(0.01f, maxHp * dangerousDamageFraction));
        float nearestSqr = float.PositiveInfinity;
        var enemies = ai.EnemyTargets;
        Vector3 origin = ai.transform.position;
        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float sqr = (enemy.position - origin).sqrMagnitude;
                if (sqr < nearestSqr) nearestSqr = sqr;
            }
        }
        if (ai.CurrentTarget != null && ai.CurrentTarget.gameObject.activeInHierarchy)
            nearestSqr = Mathf.Min(nearestSqr, (ai.CurrentTarget.position - origin).sqrMagnitude);
        float proximity = float.IsPositiveInfinity(nearestSqr) ? 1f :
            Mathf.InverseLerp(dangerDistance * dangerDistance, safeDistance * safeDistance, nearestSqr);
        return Mathf.Clamp01(threatSafety.Evaluate(threat)) * Mathf.Clamp01(damageSafety.Evaluate(damageRatio)) *
            Mathf.Clamp01(proximitySafety.Evaluate(proximity));
    }
}
