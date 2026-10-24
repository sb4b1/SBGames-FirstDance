using UnityEngine;

[CreateAssetMenu(fileName = "UtilityHealAllyGoal", menuName = "SBGames/UtilityAI/Goal/HealAlly")]
public class UtilityHealAllyGoal : UtilityGoal
{
    [Header("Curves & Weight")]
    [Tooltip("Urgency curve evaluated from 0 (at threshold) to 1 (at critical HP).")]
    [SerializeField] private AnimationCurve urgencyCurve = AnimationCurve.Linear(0f, 0.55f, 1f, 0.90f);

    [SerializeField, Range(0f, 1f)] private float baseWeight = 1.0f;

    [Tooltip("Existing tuning profile used only when the agent has no profile override.")]
    [SerializeField] private AdventurerAIProfile fallbackProfile;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;
        var profile = context.AdventurerProfile != null ? context.AdventurerProfile : fallbackProfile;
        if (profile == null)
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        float lowestHpPercent = 1f;
        bool hasCandidate = false;
        var skills = context.SkillController != null ? context.SkillController.Skills : null;
        if (skills != null)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || skill.Data == null || !skill.IsReady ||
                    skill.Data.baseHeal <= 0f ||
                    (skill.Caster.Stats != null && context.AgentMana < skill.Caster.Stats.ManaCost.Value) ||
                    skill.Data.targetFilter != SkillData.SkillTargetFilter.AlliesOnly ||
                    skill.Data.selfTarget == SkillData.SelfTarget.Only) continue;

                // Perception/squad caches only; target preference stays with the profile.
                if (context.GetBestWoundedAllyTarget(out _, out float hp, skill.Data.selfTarget, false, profile))
                {
                    hasCandidate = true;
                    lowestHpPercent = Mathf.Min(lowestHpPercent, hp);
                }
            }
        }
        if (!hasCandidate)
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        float maxHp = context.StatsManager != null && context.StatsManager.MaxLife != null
            ? context.StatsManager.MaxLife.Value : 0f;
        float ownHpPercent = maxHp > 0f ? Mathf.Clamp01(context.AgentLife / maxHp) : 1f;
        if (context.SurvivalHpThreshold > 0f && ownHpPercent <= context.SurvivalHpThreshold)
        {
            context.WoundedAllyDetectedTime = -1f;
            return 0f;
        }

        // 4. Reaction delay from AdventurerAIProfile (Rank E: 0.8s, Rank D: 0.25s, Rank C: 0.05s)
        float reactionDelay = profile.HealReactionDelay;
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
        float threshold = profile.HealHpThreshold;
        float deficit = threshold > 0f ? Mathf.Clamp01((threshold - lowestHpPercent) / threshold) : 0f;
        float score = urgencyCurve.Evaluate(deficit) * baseWeight * context.HealSafety;

        return Mathf.Clamp01(score);
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.HealAlly;
    }
}
