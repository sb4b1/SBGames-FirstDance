using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Wander")]
public class UtilityWanderGoal : ConstantConsideration
{
    public override float Evaluate(BaseAI context)
    {
        if (context.ThreatLevel > 0 || context.FearLevel > 0)
        {
            return 0;
        }

        return score;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Wander;
    }
}