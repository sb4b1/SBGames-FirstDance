using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, null, "UtillityNoAvaiableSkill")]
[CreateAssetMenu(menuName = "SBGames/UtilityAI/Skill/No Available Skill")]
public class UtilityNoAvailableSkill : UtilitySkill
{

    public override float Evaluate(BaseAI context)
    {
        return 1;
    }

    public override string GetID()
    {
        return "NoAvailableSkill";
    }
}