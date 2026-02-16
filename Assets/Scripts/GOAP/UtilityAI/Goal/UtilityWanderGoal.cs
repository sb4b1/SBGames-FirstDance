using UnityEngine;

[CreateAssetMenu(menuName = "AI/UtilityGoals/Wander")]
public class UtilityWanderGoal : UtilityGoal
{
    // This score can be adjusted based on how desirable wandering is in the current context
    // Default is set to 0.3, meaning it's a moderately desirable action when not under threat or fear
    [SerializeField] private float wanderScore = .3f;

    public override float Evaluate(BaseAI context)
    {
        // If the agent is under threat or fear, wandering is not a good option
        if (context.ThreatLevel > 0 || context.FearLevel > 0)
            return 0;

        return wanderScore; // Utility score for wandering when not under threat or fear
    }

    public override EnumUtilGoal GetGOAPGoalKey()
    {
        return EnumUtilGoal.Wander;
    }
}
