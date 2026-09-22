using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/Database/UtilitySkill Database")]
public class UtilitySkillDatabase : ScriptableObject
{
    public List<UtilitySkillEntry> utilSkills;
}

[System.Serializable]
public class UtilitySkillEntry
{
    public string id;
    public UtilitySkill so; //scriptable object reference
}