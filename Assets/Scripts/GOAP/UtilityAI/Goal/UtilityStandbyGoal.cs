using UnityEngine;

[CreateAssetMenu(fileName = "UtilityStandbyGoal", menuName = "SBGames/UtilityAI/Goal/Standby")]
public class UtilityStandbyGoal : UtilityGoal
{
    [SerializeField, Range(0f, 1f)] private float priorityScore = 0.95f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // Active only when the unit belongs to a squad and the leader issued a Standby / Hold order
        if (context.HasSquadLeader && context.SquadOrder == SquadOrderType.Standby)
        {
            return priorityScore;
        }

        return 0f;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Standby;
    }
}
