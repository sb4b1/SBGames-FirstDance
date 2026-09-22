using UnityEngine;

public abstract class UtilitySkill : ScriptableObject
{
    // This method evaluates the utility of this skill based on the current context of the AI
    public abstract float Evaluate(BaseAI context);

    // This should return the GOAP goal key
    public abstract string GetID();
}
