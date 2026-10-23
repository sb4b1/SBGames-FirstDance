using UnityEngine;

[CreateAssetMenu(fileName = "UtilityRecoverGoal", menuName = "UtilityAI/Goals/RecoverGoal")]
public class UtilityRecoverGoal : UtilityGoal
{
    [Header("Urgency Curves")]
    [Tooltip("Maps health deficit (1 - hpFraction) [0..1] to recovery urgency [0..1].")]
    [SerializeField] private AnimationCurve healthDeficitCurve = AnimationCurve.Linear(0f, 0.55f, 1f, 1f);

    [Tooltip("Maps mana deficit (1 - manaFraction) [0..1] to recovery urgency [0..1].")]
    [SerializeField] private AnimationCurve manaDeficitCurve = AnimationCurve.Linear(0f, 0.45f, 1f, 0.90f);

    [Header("Scoring Weights")]
    [Tooltip("Base urgency when curing a harmful status effect with a Status Cure item.")]
    [SerializeField, Range(0.5f, 1.0f)] private float statusCureUrgency = 0.88f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.StatsManager == null) return 0f;

        var kit = context.ItemKitController;

        // If already casting an item, maintain high commitment until cast finishes or is interrupted
        if (kit != null && kit.IsCastingItem)
        {
            return context.RecoverySafety;
        }

        context.GetRecoveryThresholdsAndQuota(out float hpTriggerFrac, out float manaTriggerFrac, out int perRoomQuota);

        // If no recovery item can be used right now (empty kit, on cooldown, or per-room quota exhausted),
        // return 0 so UtilitySurviveGoal (flee) or combat goals win immediately.
        bool hasItem = kit != null && kit.HasAnyApplicableItemNow(hpTriggerFrac, manaTriggerFrac, perRoomQuota);
        bool hasSelfSkill = context.HasReadySelfOnlyHealSkill();
        if (!hasItem && !hasSelfSkill)
        {
            return 0f;
        }

        var stats = context.StatsManager;
        float bestUrgency = 0f;

        // 1. Health Potion evaluation
        if (stats.MaxLife != null && stats.MaxLife.Value > 0f)
        {
            float hpFrac = Mathf.Clamp01(stats.Life / stats.MaxLife.Value);
            if (hpFrac <= hpTriggerFrac && stats.Life < stats.MaxLife.Value &&
                ((kit != null && kit.CanUse(ConsumableItemCategory.HealthPotion, perRoomQuota)) || hasSelfSkill))
            {
                float deficit = 1f - hpFrac;
                float hpScore = healthDeficitCurve != null ? healthDeficitCurve.Evaluate(deficit) : Mathf.Lerp(0.6f, 1.0f, deficit);

                // Fight Outlook modulation:
                // Losing with no escape (cornered): Recovery items first (+0.10f).
                // Winning decisively mid-melee: suppress drinking potions mid-melee when about to win (-0.30f).
                var outlook = context.CachedFightOutlook;
                if (context.IsCornered)
                {
                    hpScore += 0.10f;
                }
                else if (outlook.IsWinningOrEven && outlook.NormalizedOutlook > 0.40f && hpFrac > 0.25f)
                {
                    hpScore -= 0.30f;
                }

                if (hpScore > bestUrgency)
                {
                    bestUrgency = hpScore;
                }
            }
        }

        // 2. Status Cure evaluation
        var statusMgr = context.StatusEffectManager;
        if (statusMgr != null && statusMgr.HasHarmfulEffect() &&
            kit != null && kit.CanUse(ConsumableItemCategory.StatusCure, perRoomQuota))
        {
            if (statusCureUrgency > bestUrgency)
            {
                bestUrgency = statusCureUrgency;
            }
        }

        // 3. Mana Potion evaluation
        if (stats.MaxMana != null && stats.MaxMana.Value > 0f)
        {
            float manaFrac = Mathf.Clamp01(stats.Mana / stats.MaxMana.Value);
            bool shouldDrinkMana = (manaFrac <= manaTriggerFrac && stats.Mana < stats.MaxMana.Value) || (context.CannotCast && context.BasicAttackMode == BasicAttackMode.None && stats.Mana < stats.MaxMana.Value);
            if (shouldDrinkMana && kit != null && kit.CanUse(ConsumableItemCategory.ManaPotion, perRoomQuota))
            {
                float deficit = 1f - manaFrac;
                float manaScore = manaDeficitCurve != null ? manaDeficitCurve.Evaluate(deficit) : Mathf.Lerp(0.5f, 0.9f, deficit);

                // If caster cannot cast in combat, elevate urgency to recover mana promptly
                if (context.CannotCast && context.BasicAttackMode == BasicAttackMode.None)
                {
                    manaScore = Mathf.Max(manaScore, 0.85f);
                }

                if (manaScore > bestUrgency)
                {
                    bestUrgency = manaScore;
                }
            }
        }

        // 4. Leader SquadOrderType.Recover boost when out of combat
        if (bestUrgency > 0f && context.SquadOrder == SquadOrderType.Recover)
        {
            float compliance = context.AdventurerProfile != null ? context.AdventurerProfile.SquadOrderCompliance : 0.85f;
            bestUrgency = Mathf.Max(bestUrgency, 0.80f + 0.15f * compliance);
        }

        return Mathf.Clamp01(bestUrgency) * context.RecoverySafety;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Recover;
    }
}
