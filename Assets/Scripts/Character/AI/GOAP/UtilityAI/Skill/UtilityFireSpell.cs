using UnityEngine;

public class UtilityFireSpell : UtilityBaseSkill
{

    public override float Evaluate(BaseAI context)
    {
        return 1;
    }

    public override string GetID()
    {
        return "FireSpell";
    }
}