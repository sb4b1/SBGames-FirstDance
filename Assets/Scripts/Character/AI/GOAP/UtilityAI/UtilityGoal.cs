using UnityEngine;

public abstract class UtilityGoal : ScriptableObject
{
    // This method evaluates the utility of this goal based on the current context of the AI
    public abstract float Evaluate(BaseAI context);

    // This should return the GOAP goal key
    public abstract EnumUtilGoal GetGOAPGoalKey();
}
