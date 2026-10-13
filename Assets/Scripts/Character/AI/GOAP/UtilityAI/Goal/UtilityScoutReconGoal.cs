using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/UtilityAI/Goal/ScoutRecon")]
public class UtilityScoutReconGoal : UtilityGoal
{
    [SerializeField] private float basePriority = 0.98f;

    public override float Evaluate(BaseAI context)
    {
        if (context == null || context.SquadMember == null) return 0f;

        // Active only if unit has order ScoutAhead or is currently designated as scouting
        if (context.SquadOrder != SquadOrderType.ScoutAhead && !context.SquadMember.IsScouting)
        {
            return 0f;
        }

        // Hard gate: critical survival health takes precedence
        if (context.StatsManager != null && context.StatsManager.Life / Mathf.Max(1f, context.StatsManager.MaxLife.Value) <= context.SurvivalHpThreshold)
        {
            return 0f;
        }

        // If scout has already found something, prioritize returning to debrief
        bool hasDetectedEnemy = context.EnemyTargets != null && context.EnemyTargets.Count > 0;
        bool hasDetectedHazard = context.AIMemory != null && context.AIMemory.HazardCount > 0;
        if (hasDetectedEnemy || hasDetectedHazard)
        {
            return 0f;
        }

        return basePriority;
    }

    public override EnumUtilGoal GetGOAPGoalKey() => EnumUtilGoal.ScoutRecon;
}
