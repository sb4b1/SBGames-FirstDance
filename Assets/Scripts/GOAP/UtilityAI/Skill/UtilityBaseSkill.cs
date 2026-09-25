using UnityEngine;

public class UtilityBaseSkill : UtilitySkill
{

    public override float Evaluate(BaseAI context)
    {
        return 1;
    }

    public override string GetID()
    {
        return "BaseSkill";
    }
}