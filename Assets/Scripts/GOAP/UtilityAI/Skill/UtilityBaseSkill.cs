using UnityEngine;

public class UtilityBaseSkill : UtilitySkill
{

    public override float Evaluate(BaseAI context)
    {
        if (context != null && context.ClassStyleTable != null)
        {
            return Mathf.Lerp(1.0f, 0.25f, context.SkillReliance);
        }

        return 1f;
    }

    public override string GetID()
    {
        return "BaseSkill";
    }
}