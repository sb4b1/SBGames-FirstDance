using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/Alert Allies")]
public class UtilityAlertAlliesGoal : UtilityGoal
{
    [SerializeField] private float priorityScore = 0.95f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null) return 0f;

        // If already alerted for current targets, skip
        if (context.HasAlertedAllies) return 0f;

        // Must have spotted enemies to shout about
        if (context.EnemyTargets == null || context.EnemyTargets.Count == 0) return 0f;

        // Must respect shout cooldown
        if (Time.time - context.LastAlertShoutTime < context.AlertShoutCooldown) return 0f;

        // Solo check: do not shout if alone without any allies
        bool hasAllies = false;

        if (context.HasSquadLeader && context.SquadMember != null && context.SquadMember.Leader != null)
        {
            if (context.SquadMember.Leader.IsAlive || context.SquadMember.Leader.Members.Count > 1)
            {
                hasAllies = true;
            }
        }
        else
        {
            var leaderComp = context.GetComponent<PartyLeader>() ?? context.GetComponentInParent<PartyLeader>();
            if (leaderComp != null && leaderComp.Members != null && leaderComp.Members.Count > 0)
            {
                hasAllies = true;
            }
        }

        if (!hasAllies && context.AllyTargets != null && context.AllyTargets.Count > 0)
        {
            hasAllies = true;
        }

        if (!hasAllies) return 0f;

        return priorityScore;
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.AlertAllies;
    }
}
