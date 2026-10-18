using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Wander")]
public class UtilityWanderGoal : UtilityGoal
{
    [SerializeField] protected float score = .4f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.ThreatLevel > 0 || context.FearLevel > 0 || context.SquadOrder == SquadOrderType.Standby)
        {
            return 0;
        }

        // Pure healer / None mode with no damage skill stays with party instead of wandering off
        if (context.BasicAttackMode == BasicAttackMode.None && !context.HasUsableDamageSkill())
        {
            return 0;
        }

        return score;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Wander;
    }
}