using UnityEngine;

public class ConstantConsideration : UtilityGoal
{
    [SerializeField] protected float score = .4f;

    public override float Evaluate(BaseAI context)
    {
        return score;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.None;
    }
}