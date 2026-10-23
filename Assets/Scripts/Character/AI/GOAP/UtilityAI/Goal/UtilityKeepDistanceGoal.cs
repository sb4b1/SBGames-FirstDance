using SBGames.Character;
using UnityEngine;

[CreateAssetMenu(fileName = "UtilityKeepDistanceGoal", menuName = "SBGames/UtilityAI/Goal/Keep Distance")]
public class UtilityKeepDistanceGoal : UtilityGoal
{
    [SerializeField, Range(0.1f, 3f)] private float baseWeight = 1.0f;
    [SerializeField] private AnimationCurve distanceUrgencyCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // Never kite if held in Standby
        if (context.SquadOrder == SquadOrderType.Standby)
        {
            return 0f;
        }

        var stats = context.StatsManager;
        if (stats == null) return 0f;

        // Resolve reference range and check if Keep Distance is enabled
        float attackRange = stats.AttackRange != null ? stats.AttackRange.Value : 1f;
        float longestSkillRange = context.GetMainSkillRange();
        float refRange = KeepDistanceResolver.ResolveReferenceRange(attackRange, context.BasicAttackMode, longestSkillRange);

        var tuning = stats.KeepDistanceTuning;
        float autoThreshold = tuning != null ? tuning.AutoEnableRangeThreshold : 3.0f;
        bool isEnabled = KeepDistanceResolver.ResolveIsEnabled(stats.KeepDistanceMode, refRange, autoThreshold);

        if (!isEnabled)
        {
            return 0f;
        }

        // Mage/Healer with BasicAttackMode None should kite when unable to cast (out of mana or all skills on cooldown)
        bool cannotCastYield = context.BasicAttackMode == BasicAttackMode.None && context.CannotCast;

        // Nearest enemy check (from cached data, no physics)
        Transform threat = context.CurrentTarget != null ? context.CurrentTarget : context.GetNearestThreatTransform(out _);
        if (threat == null)
        {
            return 0f;
        }

        Vector3 diff = threat.position - context.transform.position;
        diff.z = 0f;
        float distSqr = diff.sqrMagnitude;
        if (distSqr < 0.0001f) distSqr = 0.0001f;
        float currentDist = Mathf.Sqrt(distSqr);

        KeepDistanceResolver.CalculateHysteresisDistances(
            refRange,
            stats.ComfortDistanceOverride,
            tuning,
            out float startDist,
            out float stopDist);

        // If unit was already kiting (current goal is KeepDistance), maintain until stopDist (hysteresis outer boundary)
        bool wasKiting = context.CurrentActiveGoalKey == EnumUtilGoal.KeepDistance;
        float activeThreshold = wasKiting ? stopDist : startDist;

        if (currentDist > activeThreshold && !cannotCastYield)
        {
            // Enemy is outside comfort boundary and caster is not out of mana/CD -> resume attacking/casting
            return 0f;
        }

        // Skip kiting when cornered or enemy is clearly faster and unit cannot gain distance
        if (context.IsCornered)
        {
            return 0f;
        }

        // Priority calculation:
        // Above KillEnemy only while enemy is inside comfort distance; below Survive and FallBack.
        // KillEnemy evaluates up to ~0.8-1.0 depending on threat/fear.
        // We set score to a solid baseline (~0.75 - 0.85) mapped by distance urgency curve,
        // so it reliably beats standard KillEnemy (~0.5 - 0.7) but yields to Survive/FallBack (~0.9 - 1.2+).
        float urgency = Mathf.Clamp01(1f - (currentDist / Mathf.Max(0.1f, stopDist)));
        float score = distanceUrgencyCurve.Evaluate(urgency) * baseWeight * 0.8f;

        if (cannotCastYield)
        {
            // Elevate priority slightly to ensure kite-and-recover succeeds while on cooldown
            score = Mathf.Max(score, 0.75f);
        }

        return Mathf.Clamp01(score);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.KeepDistance;
    }
}
