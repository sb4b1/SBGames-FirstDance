using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Fall Back")]
public class UtilityFallBackGoal : UtilityGoal
{
    [SerializeField] private float baseScore = 1.0f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || !context.HasSquadLeader) return 0f;
        if (context.SquadOrder != SquadOrderType.FallBack) return 0f;

        return baseScore;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.FallBack;
    }
}
