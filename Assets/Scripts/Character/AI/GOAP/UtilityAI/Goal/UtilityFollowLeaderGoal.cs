using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Follow Leader")]
public class UtilityFollowLeaderGoal : UtilityGoal
{
    [SerializeField] private float explicitOrderWeight = 0.85f;
    [SerializeField] private float leashDistance = 6.0f;
    [SerializeField] private float maxDistance = 15.0f;
    [SerializeField] private AnimationCurve distanceRegroupCurve = AnimationCurve.Linear(0, 0, 1, 1);

    public override float Evaluate(BaseAI context)
    {
        if (context == null || (!context.HasSquadLeader && !context.IsStayingLoose)) return 0f;

        // Explicit command from leader
        if (context.HasSquadLeader && context.SquadOrder == SquadOrderType.FollowLeader)
        {
            return explicitOrderWeight;
        }

        // If in combat and no explicit order, stay in combat unless drifted too far
        var leaderTransform = context.LeaderTransform;
        if (leaderTransform == null) return 0f;

        float sqrDist = (context.transform.position - leaderTransform.position).sqrMagnitude;
        float sqrLeash = leashDistance * leashDistance;

        // If within leash, do not pull away from combat or idle, UNLESS healer/empty-caster with no damage skill has nothing to engage
        if (sqrDist <= sqrLeash)
        {
            // Healer / None mode with no damage skill and no wounded ally -> keep formation with leader
            if (context.BasicAttackMode == BasicAttackMode.None && !context.HasUsableDamageSkill())
            {
                // Score moderate priority to stay near party rear
                return sqrDist > 1.5f * 1.5f ? 0.45f : 0.1f;
            }

            // Empty-caster with no usable skills right now -> keep safe distance near party leader / tank
            if (context.BasicAttackMode == BasicAttackMode.None && context.CannotCast)
            {
                return sqrDist > 2.0f * 2.0f ? 0.50f : 0.15f;
            }

            return 0f;
        }

        // Drifting too far: calculate leash tension score
        float dist = Mathf.Sqrt(sqrDist);
        float normalizedDist = Mathf.Clamp01((dist - leashDistance) / Mathf.Max(1f, maxDistance - leashDistance));
        float score = distanceRegroupCurve.Evaluate(normalizedDist) * 0.9f;

        // If under threat, slightly reduce pull so agent can defend itself unless extremely far
        if (context.ThreatLevel > 0)
        {
            score *= 0.7f;
        }

        return score;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.FollowLeader;
    }
}
