using UnityEngine;

[CreateAssetMenu(fileName = "UtilityWarpGoal", menuName = "UtilityAI/Goals/WarpGoal")]
public class UtilityWarpGoal : UtilityGoal
{
    [Header("Urgency Curves")]
    [Tooltip("Maps health deficit (1 - hpFraction) [0..1] to warp escape urgency [0..1].")]
    [SerializeField] private AnimationCurve healthDeficitCurve = AnimationCurve.Linear(0f, 0.70f, 1f, 1.15f);

    [Tooltip("Threat weight multiplier on warp urgency.")]
    [SerializeField, Range(0.5f, 2.0f)] private float threatWeight = 1.0f;

    [Tooltip("Urgency score while actively casting the warp item to guarantee goal commitment.")]
    [SerializeField, Range(1.0f, 1.25f)] private float activeCastCommitmentScore = 1.10f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        var kit = context.ItemKitController;
        if (kit == null) return 0f;

        // If currently casting Warp item, return commitment score so hysteresis/lock keeps action uninterrupted
        if (kit.IsCastingItem && kit.CastingCategory == ConsumableItemCategory.Warp)
        {
            return activeCastCommitmentScore;
        }

        // Must pass all gating heuristics in BaseAI (has item, under threat, low HP, Recover cannot help, etc.)
        if (!context.CanWarpNow())
        {
            return 0f;
        }

        float maxHp = 100f;
        if (context.StatsManager != null && context.StatsManager.MaxLife != null && context.StatsManager.MaxLife.Value > 0f)
        {
            maxHp = context.StatsManager.MaxLife.Value;
        }
        float currentHp = context.StatsManager != null ? context.StatsManager.Life : context.AgentLife;
        float hpFrac = Mathf.Clamp01(currentHp / maxHp);
        float deficit = 1f - hpFrac;

        float score = (healthDeficitCurve != null) ? healthDeficitCurve.Evaluate(deficit) : Mathf.Lerp(0.7f, 1.15f, deficit);
        score *= threatWeight;

        // When health is below flee threshold and Recover cannot help, ensure Warp beats Survive (clamped at 1.0)
        if (hpFrac <= context.SurvivalHpThreshold)
        {
            score = Mathf.Max(score, 1.04f);
        }

        return Mathf.Clamp(score, 0f, 1.15f);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Warp;
    }
}
