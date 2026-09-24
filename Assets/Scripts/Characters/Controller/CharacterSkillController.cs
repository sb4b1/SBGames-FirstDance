using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class CharacterSkillController : MonoBehaviour
{
    [SerializeField] private BaseAI baseAI;
    [SerializeField] private SkillTargeting skillTargeting;

    public SkillTargeting SkillTargeting => skillTargeting;


    [SerializeField] private List<Skill> skills = new();
    public IReadOnlyList<Skill> Skills => skills;
    [SerializeField] private GameObject skillSpawnPoint;
    public GameObject SpawnPoint => skillSpawnPoint;

    public CharacterSkillController()
    {
        skills = new List<Skill>();
    }

    private void Awake()
    {
        if (baseAI == null)
        {
            baseAI = GetComponentInParent<BaseAI>();
        }

        InitializeExistingSkills();
    }

    private void OnValidate()
    {
        // Keeps Inspector skills valid in Editor mode when editing button attributes
        InitializeExistingSkills();
    }


    private void Start()
    {
        if (skillSpawnPoint == null)
        {
            skillSpawnPoint = transform.Find("SpawnPoint").gameObject;
        }
    }

    private void InitializeExistingSkills()
    {
        if (skills == null) return;
        foreach (var skill in skills)
        {
            skill?.SetController(this);
        }
    }

    public void AddSkill(Skill newSkill)
    {
        if (newSkill == null) return;
        newSkill.SetController(this);
        skills.Add(newSkill);
        baseAI?.InitializeSkills();
    }

    public void RemoveSkill(Skill skill)
    {
        if (skills.Contains(skill))
        {
            skills.Remove(skill);
            baseAI?.InitializeSkills();
        }
    }

    public bool HasReadySkill()
    {
        if (skills == null || skills.Count == 0) return false;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && skills[i].IsReady) return true;
        }
        return false;
    }

    public bool TryCastSkillAI()
    {
        // Let BaseAI evaluate and select the best skill using its internal utility list
        if (baseAI != null)
        {
            UtilitySkill selectedSkill = baseAI.SelectBestSkill();
            if (selectedSkill != null)
            {
                TriggerSkill(selectedSkill);
                return true;
            }
        }

        // Fallback: trigger first available skill that is off cooldown
        Skill readySkill = skills?.Find(s => s != null && s.IsReady);
        if (readySkill != null)
        {
            readySkill.Trigger();
            return true;
        }

        return false;
    }

    /// <summary>Triggers the Skill whose UtilityData matches the given ScriptableObject.</summary>
    public void TriggerSkill(UtilitySkill utilSkill)
    {
        if (utilSkill == null) return;
        Skill skillToTrigger = skills.Find(s => s != null && s.UtilityData == utilSkill);
        if (skillToTrigger != null)
            skillToTrigger.Trigger();
        else
            Debug.LogWarning($"[CharacterSkillController] No Skill found for UtilitySkill '{utilSkill.GetID()}'.");
    }

    /// <summary>Triggers the Skill matching the given skillID string.</summary>
    public void TriggerSkill(string skillID)
    {
        Skill skillToTrigger = skills.Find(s => s != null && s.ID == skillID);
        if (skillToTrigger != null)
            skillToTrigger.Trigger();
        else
            Debug.LogWarning($"[CharacterSkillController] Skill with ID '{skillID}' not found.");
    }

    /// <summary>
    /// Returns all UtilitySkill SOs collected directly from each Skill's UtilityData.
    /// No database lookup required when SkillData.utilityData is assigned.
    /// </summary>
    public List<UtilitySkill> GetUtilSkillList()
    {
        List<UtilitySkill> utilSkills = new();
        if (skills == null) return utilSkills;

        foreach (var skill in skills)
        {
            if (skill == null) continue;
            UtilitySkill data = skill.UtilityData;
            if (data != null && !utilSkills.Contains(data))
                utilSkills.Add(data);
        }
        return utilSkills;
    }

    [System.Serializable]
    public class Skill
    {
        [System.NonSerialized]
        private CharacterSkillController controller;

        public Skill() { }

        public Skill(GameObject prefab, CharacterSkillController owner = null)
        {
            this.skillPrefab = prefab;
            if (owner != null)
            {
                SetController(owner);
            }
        }

        public void SetController(CharacterSkillController ownerController)
        {
            this.controller = ownerController;
            UpdateSkillCaster();
        }

        [SerializeField] private GameObject skillPrefab;
        private SkillCaster skill;
        public string ID => skill != null && skill.Data != null ? skill.Data.skillID : string.Empty;
        public SkillCaster Caster => skill;
        public bool IsReady => skill != null && skill.IsReady();

        /// <summary>
        /// The UtilitySkill ScriptableObject for this skill.
        /// Priority: SkillData.utilityData → UtilitySkillRuntimeDatabase lookup by skillID.
        /// </summary>
        public UtilitySkill UtilityData
        {
            get
            {
                // Prefer the direct reference on SkillData
                if (skill != null && skill.Data != null && skill.Data.utilityData != null)
                    return skill.Data.utilityData;

                // Fallback: runtime database lookup by skill ID
                if (!string.IsNullOrEmpty(ID) && GameServices.UtilitySkillRuntimeDatabase != null)
                {
                    var entry = GameServices.UtilitySkillRuntimeDatabase.Get(ID);
                    if (entry != null && entry.so != null)
                        return entry.so;
                }

                return null;
            }
        }

        // Public Property: Automatically gets SkillCaster when assigned via code
        public GameObject SkillPrefab
        {
            get => skillPrefab;
            set
            {
                skillPrefab = value;
                UpdateSkillCaster();
            }
        }

        // Called automatically in the Unity Editor whenever a field is changed in the Inspector
        private void OnValidate()
        {
            UpdateSkillCaster();
        }

        public SkillCaster SkillCaster => skill;

        // Core logic to extract SkillCaster from the prefab/GameObject
        private void UpdateSkillCaster()
        {
            if (skillPrefab != null)
            {
                // GetComponent works whether skillPrefab is a scene object or a prefab asset
                skill = skillPrefab.GetComponent<SkillCaster>();

                if (skill == null)
                {
                    Debug.LogWarning($"[Skill] The assigned prefab '{skillPrefab.name}' is missing a SkillCaster component!");
                }

                if (controller != null)
                {
                    skill.SetupSkillCaster(
                        controller.SkillTargeting, 
                        controller.SpawnPoint != null ? controller.SpawnPoint.transform : null, 
                        controller.transform.root
                    );
                }
            }
            else
            {
                skill = null;
            }
        }


        [Button]
        public void Trigger()
        {
            if (skill != null)
            {
                if (skill.Flags == 3)
                {
                    skill.DebugCastSkill();
                    Debug.Log($"Skill {ID} triggered successfully.");
                }
                else
                {
                    Debug.LogWarning($"Skill {ID} cannot be cast due to failing preconditions.");
                }
            }
        }
    }
}