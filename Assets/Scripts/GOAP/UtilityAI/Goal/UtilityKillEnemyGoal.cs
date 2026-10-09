using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Kill Enemy")]
public class UtilityKillEnemyGoal : UtilityGoal
{
    private const float MIN_VALUE = 0f;
    private const float MAX_VALUE = 1f;
    [SerializeField] private AnimationCurve threatCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private AnimationCurve fearCurve = AnimationCurve.Linear(0, 0, 1, 1);


    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.ThreatLevel <= 0f)
        {
            return 0f;
        }

        float effectiveThreat = context.ThreatLevel * context.EffectiveThreatMultiplier;
        float effectiveFear = context.IsFearLocked ? 0f : context.FearLevel * context.EffectiveFearMultiplier;

        float threatValue = threatCurve.Evaluate(Map(effectiveThreat, 0f, 100f));
        float fearValue = fearCurve.Evaluate(Map(effectiveFear, 0f, 100f));

        return Mathf.Clamp01(threatValue - fearValue);
    }

    protected float Map(float value, float inMin, float inMax)
    {
        return (value - inMin) / (inMax - inMin) * (MAX_VALUE - MIN_VALUE) + MIN_VALUE;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.KillEnemy;
    }
}