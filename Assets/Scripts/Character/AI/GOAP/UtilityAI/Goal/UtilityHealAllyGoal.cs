using UnityEngine;

[CreateAssetMenu(fileName = "UtilityHealAllyGoal", menuName = "SBGames/UtilityAI/Goal/HealAlly")]
public class UtilityHealAllyGoal : UtilityGoal
{
    [Header("Curves & Weight")]
    [Tooltip("Urgency curve evaluated from 0 (at threshold) to 1 (at critical HP).")]
    [SerializeField] private AnimationCurve urgencyCurve = AnimationCurve.Linear(0f, 0.55f, 1f, 0.90f);

    [SerializeField, Range(0.1f, 3f)] private float baseWeight = 1.0f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // 1. Hard Gate: Agent must possess a ready heal skill (AlliesOnly) and minimum mana
        if (!context.HasReadyHealSkill() || context.AgentMana < 5f)
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        // 2. Compete with healer's own survival:
        // When the healer is in real danger (HP <= survival threshold or panic),
        // survival goal MUST win over healing allies.
        float maxHp = 100f;
        if (context.StatsManager != null && context.StatsManager.MaxLife != null && context.StatsManager.MaxLife.Value > 0f)
        {
            maxHp = context.StatsManager.MaxLife.Value;
        }

        float ownHpPercent = Mathf.Clamp01(context.AgentLife / maxHp);
        float survivalThreshold = context.SurvivalHpThreshold;

        // If healer is in danger, yield immediately to SurviveGoal
        if (survivalThreshold > 0f && ownHpPercent <= survivalThreshold)
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        // 3. Scan for wounded ally below threshold
        if (!context.GetBestWoundedAllyTarget(out Transform woundedTarget, out float lowestHpPercent))
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        // 4. Reaction delay from AdventurerAIProfile (Rank E: 0.8s, Rank D: 0.25s, Rank C: 0.05s)
        float reactionDelay = context.AdventurerProfile != null ? context.AdventurerProfile.HealReactionDelay : 0.25f;
        if (reactionDelay > 0f)
        {
            if (context.WoundedAllyDetectedTime < 0f)
            {
                context.WoundedAllyDetectedTime = Time.time;
            }

            if (Time.time - context.WoundedAllyDetectedTime < reactionDelay)
            {
                // Delayed reaction: hesitant before realizing ally is wounded
                return 0f;
            }
        }

        // 5. Score calculation: ally HP deficit scales score from 0.55 to 0.90
        float threshold = context.AdventurerProfile != null ? context.AdventurerProfile.HealHpThreshold : 0.50f;
        float deficit = threshold > 0f ? Mathf.Clamp01((threshold - lowestHpPercent) / threshold) : 0f;
        float score = urgencyCurve.Evaluate(deficit) * baseWeight;

        // If healer is somewhat injured (between survivalThreshold and 50%), dampen heal score
        if (ownHpPercent < 0.50f)
        {
            score *= Mathf.InverseLerp(survivalThreshold, 0.50f, ownHpPercent);
        }

        return Mathf.Clamp01(score);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.HealAlly;
    }
}
