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

    [Header("Archetype Configuration / Overrides")]
    [Tooltip("If true, minion/archetype ignores survival by default unless overridden by high cowardice trait/instinct.")]
    [SerializeField] private bool ignoreSurvivalGoal = false;

    [Tooltip("Base HP threshold for this goal asset. If 0, survival is suppressed by default. Set to -1 to use character's default threshold.")]
    [SerializeField] private float survivalHpThreshold = -1f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // 1. Trait Hard Gate: Reckless / Immune units ignore survival
        if (context.IgnoreSurvivalGoal) return 0f;

        // Check if unit has an explicit cowardice personality trait or instinct
        bool hasCowardlyPersonality = context.Personality != null &&
            (context.Personality.ActiveTrait != null && context.Personality.ActiveTrait.SurvivalHpThreshold > 0.35f ||
             context.Personality.Snapshot.SurvivalHpThreshold > 0.35f);

        // Archetype default suppression (e.g. minion archetype fighting to the death unless cowardly)
        if (ignoreSurvivalGoal && !hasCowardlyPersonality) return 0f;

        // 2. Health Threshold Gate
        float threshold = (survivalHpThreshold >= 0f && !hasCowardlyPersonality)
            ? survivalHpThreshold
            : context.SurvivalHpThreshold;

        if (threshold <= 0f) return 0f;

        float maxHp = 100f;
        if (context.StatsManager != null && context.StatsManager.MaxLife != null && context.StatsManager.MaxLife.Value > 0f)
        {
            maxHp = context.StatsManager.MaxLife.Value;
        }

        float hpPercent = Mathf.Clamp01(context.AgentLife / maxHp);

        // Burst damage velocity check from Dual-Layer Tactical Combat Memory
        float burstDamage = context.AIMemory != null ? context.AIMemory.GetRecentDamage(2.0f) : 0f;
        float burstPanicMultiplier = context.Personality != null ? context.Personality.Snapshot.BurstDamagePanicMultiplier : 1.0f;
        bool isPanicFromBurst = burstDamage > 0f && (burstDamage * burstPanicMultiplier >= maxHp * 0.25f);

        if (hpPercent > threshold)
        {
            // If HP is above fear threshold, only panic if sudden burst damage velocity or exceedingly high fear
            if (!isPanicFromBurst && (context.FearLevel < 80 || context.IsFearLocked))
                return 0f;
        }

        // Calculate health deficit (0 when at threshold, 1 when at 0 HP)
        float hpDeficit = threshold > 0f ? Mathf.Clamp01((threshold - hpPercent) / threshold) : 0f;
        float urgency = healthUrgencyCurve.Evaluate(hpDeficit);

        // Burst damage urgency boost
        if (isPanicFromBurst)
        {
            float burstRatio = Mathf.Clamp01(burstDamage / (maxHp * 0.5f)) * burstPanicMultiplier;
            urgency = Mathf.Max(urgency, burstRatio);
        }

        // Fear contribution
        float effectiveFear = context.IsFearLocked ? 0f : context.FearLevel * context.EffectiveFearMultiplier;
        float fearFactor = fearCurve.Evaluate(Mathf.Clamp01(effectiveFear / 100f));

        // When health deficit is critical, health urgency alone scales up to 1.0f to override orders
        float totalScore = Mathf.Max(urgency, urgency * 0.7f + fearFactor * 0.3f) * baseWeight;
        return Mathf.Clamp01(totalScore);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Survive;
    }
}
