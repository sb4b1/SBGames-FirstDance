using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Focus Fire")]
public class UtilityFocusFireGoal : UtilityGoal
{
    [SerializeField] private float baseScore = 0.95f;
    [SerializeField] private AnimationCurve fearSuppressionCurve = AnimationCurve.Linear(0, 1, 1, 0.3f);

    public override float Evaluate(BaseAI context)
    {
        if (context == null || !context.HasSquadLeader) return 0f;
        if (context.SquadOrder != SquadOrderType.FocusFire) return 0f;
        if (context.FocusFireTarget == null) return 0f;

        float fearModifier = fearSuppressionCurve.Evaluate(Mathf.Clamp01(context.FearLevel / 100f));
        return baseScore * fearModifier;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.FocusFire;
    }
}
