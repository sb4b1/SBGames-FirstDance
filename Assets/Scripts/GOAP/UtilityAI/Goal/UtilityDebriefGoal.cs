using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/DebriefLeader")]
public class UtilityDebriefGoal : UtilityGoal
{
    [SerializeField] private float basePriority = 0.99f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.SquadMember == null || !context.SquadMember.HasLeader) return 0f;

        // Debriefing is active if the unit is on a scouting mission
        if (context.SquadOrder != SquadOrderType.ScoutAhead && !context.SquadMember.IsScouting)
        {
            return 0f;
        }

        // Hard gate: critical survival health takes precedence
        if (context.StatsManager != null && context.StatsManager.Life / Mathf.Max(1f, context.StatsManager.MaxLife.Value) <= context.SurvivalHpThreshold)
        {
            return 0f;
        }

        // If scout has detected enemies or hazards locally, returning to debrief is high urgency
        bool hasDetectedEnemy = (context.EnemyTargets != null && context.EnemyTargets.Count > 0) || context.LocalKnownEnemies.Count > 0;
        bool hasDetectedHazard = context.AIMemory != null && context.AIMemory.HazardCount > 0;

        if (hasDetectedEnemy || hasDetectedHazard)
        {
            return basePriority;
        }

        return 0f;
    }

    public override EnumUtilGoal GetGOAPGoalKey() => EnumUtilGoal.DebriefLeader;
}
