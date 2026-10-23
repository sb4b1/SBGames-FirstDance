using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Fall Back")]
public class UtilityFallBackGoal : UtilityGoal
{
    [SerializeField] private float baseScore = 1.0f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || !context.HasSquadLeader) return 0f;
        if (context.SquadOrder != SquadOrderType.FallBack) return 0f;

                float score = baseScore;

        // Modulate with Fight Outlook:
        // Losing with escape: FallBack rises (reinforces retreat order).
        // Losing with no escape (cornered): cornered units fight back rather than retreating into a wall.
        var outlook = context.CachedFightOutlook;
        if (!context.HasEscapeRoute)
        {
            score *= 0.40f;
        }
        else if (outlook.IsLosing)
        {
            score += 0.20f * Mathf.Abs(outlook.NormalizedOutlook);
        }

        return score;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.FallBack;
    }
}
