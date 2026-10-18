using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Kill Enemy")]
public class UtilityKillEnemyGoal : UtilityGoal
{
    private const float MIN_VALUE = 0f;
    private const float MAX_VALUE = 1f;
    [SerializeField] private AnimationCurve threatCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private AnimationCurve fearCurve = AnimationCurve.Linear(0, 0, 1, 0.5f);


    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.ThreatLevel <= 0f)
        {
            return 0f;
        }

        // A character with mode None and no damage skill (e.g. Healer) must never select KillEnemy
        if (context.BasicAttackMode == BasicAttackMode.None && !context.HasUsableDamageSkill())
        {
            return 0f;
        }

        // Empty caster with mode None cannot cast right now (out of mana or all skills on cooldown) -> yield to kite/recover
        if (context.BasicAttackMode == BasicAttackMode.None && context.CannotCast)
        {
            return 0f;
        }

        // Suppress engaging enemies autonomously when held in Standby
        if (context.SquadOrder == SquadOrderType.Standby)
        {
            return 0f;
        }

        float effectiveThreat = context.ThreatLevel * context.EffectiveThreatMultiplier;
        float effectiveFear = context.IsFearLocked ? 0f : context.FearLevel * context.EffectiveFearMultiplier;

        float normalizedThreat = Mathf.Clamp01(Map(effectiveThreat, 0f, 100f));
        float normalizedFear = Mathf.Clamp01(Map(effectiveFear, 0f, 100f));

        float threatValue = threatCurve.Evaluate(normalizedThreat);
        float fearValue = fearCurve.Evaluate(normalizedFear);

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