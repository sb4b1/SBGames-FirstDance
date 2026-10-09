using UnityEngine;

[CreateAssetMenu(fileName = "UtilitySurviveGoal", menuName = "SBGames/UtilityAI/Goal/Survive")]
public class UtilitySurviveGoal : UtilityGoal
{
    [Header("Curves & Weight")]
    [Tooltip("Urgency curve evaluated from 0 (at threshold) to 1 (at 0 HP).")]
    [SerializeField] private AnimationCurve healthUrgencyCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);

    [Tooltip("Fear contribution curve from 0 to 100 fear.")]
    [SerializeField] private AnimationCurve fearCurve = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);

    [SerializeField, Range(0.1f, 3f)] private float baseWeight = 1.0f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // 1. Trait Hard Gate: Reckless / Immune units ignore survival
        if (context.IgnoreSurvivalGoal) return 0f;

        // 2. Health Threshold Gate
        float threshold = context.SurvivalHpThreshold;
        if (threshold <= 0f) return 0f;

        float maxHp = 100f;
        if (context.StatsManager != null && context.StatsManager.MaxLife != null && context.StatsManager.MaxLife.Value > 0f)
        {
            maxHp = context.StatsManager.MaxLife.Value;
        }

        float hpPercent = Mathf.Clamp01(context.AgentLife / maxHp);
        if (hpPercent > threshold)
        {
            // If HP is above fear threshold, only panic if fear is exceedingly high
            if (context.FearLevel < 80 || context.IsFearLocked)
                return 0f;
        }

        // Calculate health deficit (0 when at threshold, 1 when at 0 HP)
        float hpDeficit = threshold > 0f ? Mathf.Clamp01((threshold - hpPercent) / threshold) : 0f;
        float urgency = healthUrgencyCurve.Evaluate(hpDeficit);

        // Fear contribution
        float effectiveFear = context.IsFearLocked ? 0f : context.FearLevel * context.EffectiveFearMultiplier;
        float fearFactor = fearCurve.Evaluate(Mathf.Clamp01(effectiveFear / 100f));

        float totalScore = (urgency * 0.7f + fearFactor * 0.3f) * baseWeight;
        return Mathf.Clamp01(totalScore);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Survive;
    }
}
