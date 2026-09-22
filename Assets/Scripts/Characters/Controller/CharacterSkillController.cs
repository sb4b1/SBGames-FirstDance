using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class CharacterSkillController : MonoBehaviour
{
    [SerializeField] private BaseAI baseAI;


    public List<Skill> skills;
    [SerializeField] private GameObject skillSpawnPoint;
    public GameObject SpawnPoint => skillSpawnPoint;

    private void Awake()
    {
        if (baseAI == null)
        {
            baseAI = GetComponentInParent<BaseAI>();
        }
    }

    private void Start()
    {
        if (skillSpawnPoint == null)
        {
            skillSpawnPoint = transform.Find("SpawnPoint").gameObject;
        }
    }

    public bool TryCastSkillAI()
    {
        // This method can be called by the AI to attempt to cast a skill based on certain conditions (e.g., cooldowns, target availability, etc.)
        // For simplicity, we'll just trigger the first skill in the list that is available.
        UtilitySkill selectedskill = baseAI.SelectBestSkill(GetUtilSkillList());
        if (selectedskill == null) return false;
        TriggerSkill(selectedskill.GetID());
        return true; // Return true if a skill was successfully triggered
    }

    public void TriggerSkill(string skillID)
    {
        Skill skillToTrigger = skills.Find(skill => skill.ID == skillID);
        if (skillToTrigger != null)
        {
            skillToTrigger.Trigger();
        }
        else
        {
            Debug.LogWarning($"Skill with ID {skillID} not found.");
        }
    }

    private List<UtilitySkill> GetUtilSkillList()
    {
        List<string> utilSkillIDs = new();
        foreach (var skill in skills)
        {
            utilSkillIDs.Add(skill.ID);
        }

        List<UtilitySkill> utilSkills = new();
        foreach (var skillID in utilSkillIDs)
        {
            var utilSkillEntry = GameServices.UtilitySkillRuntimeDatabase.Get(skillID);
            if (utilSkillEntry != null && utilSkillEntry.so != null)
            {
                utilSkills.Add(utilSkillEntry.so);
            }
        }
        return utilSkills;
    }

    [System.Serializable]
    public class Skill
    {
        [SerializeField] private SkillCaster skill;
        public string ID => skill.Data.skillID;

        [Button("Trigger Skill")]
        public void Trigger()
        {
            skill.TryCastSkill();
        }
    }

}