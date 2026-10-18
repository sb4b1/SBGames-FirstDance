using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class CharacterSkillController : MonoBehaviour
{
    [SerializeField] private BaseAI baseAI;
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private SkillTargeting skillTargeting;

    public SkillTargeting SkillTargeting => skillTargeting;
    public CharacterBrain Brain => brain;


    [SerializeField] private List<Skill> skills = new();
    public IReadOnlyList<Skill> Skills => skills;
    [SerializeField] private GameObject skillSpawnPoint;
    public GameObject SpawnPoint => skillSpawnPoint;

    private Skill pendingSkill;
    private GameObject pendingTarget;

    public SkillData LastExecutedSkillData { get; private set; }

    public bool HasPendingCast => pendingSkill != null;
    public Skill PendingSkill => pendingSkill;
    public GameObject PendingTarget => pendingTarget;

    public CharacterSkillController()
    {
        skills = new List<Skill>();
    }

    private void Awake()
    {
        if (baseAI == null)
        {
            baseAI = GetComponentInParent<BaseAI>();
            if (baseAI == null && transform.root != null)
            {
                baseAI = transform.root.GetComponentInChildren<BaseAI>();
            }
        }

        if (brain == null)
        {
            brain = GetComponentInParent<CharacterBrain>();
            if (brain == null && transform.root != null)
            {
                brain = transform.root.GetComponentInChildren<CharacterBrain>();
            }
        }

        if (skills == null || skills.Count == 0)
        {
            var casters = GetComponentsInChildren<SkillCaster>(true);
            foreach (var caster in casters)
            {
                skills.Add(new Skill(caster.gameObject, this));
            }
        }

        InitializeExistingSkills();
        baseAI?.InitializeSkills();
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
            var spawnChild = transform.Find("SpawnPoint");
            skillSpawnPoint = spawnChild != null ? spawnChild.gameObject : gameObject;
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

    /// <summary>
    /// Returns the effective casting range of the best utility skill (or the first ready skill).
    /// </summary>
    public float GetBestReadySkillRange()
    {
        if (skills == null || skills.Count == 0) return 5f;

        if (baseAI != null)
        {
            UtilitySkill bestUtil = baseAI.SelectBestSkill();
            if (bestUtil != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null && skills[i].UtilityData == bestUtil && skills[i].Data != null)
                    {
                        return skills[i].Data.EffectiveRange;
                    }
                }
            }
        }

        // Fallback: first ready skill's range
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && skills[i].IsReady && skills[i].Data != null)
            {
                return skills[i].Data.EffectiveRange;
            }
        }

        // Default fallback if no skill is currently ready
        if (skills.Count > 0 && skills[0] != null && skills[0].Data != null)
            return skills[0].Data.EffectiveRange;

        return 5f;
    }

    /// <summary>
    /// Returns the maximum effective range among all currently ready skills.
    /// </summary>
    public float GetMaxReadySkillRange()
    {
        if (skills == null || skills.Count == 0) return 5f;
        float maxRange = 0f;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && skills[i].IsReady && skills[i].Data != null)
            {
                float range = skills[i].Data.EffectiveRange;
                if (range > maxRange) maxRange = range;
            }
        }
        return maxRange > 0f ? maxRange : 5f;
    }

    /// <summary>
    /// Evaluates the best ready skill and target, locks them as a pending cast, and starts the cast animation.
    /// The skill is physically executed when AnimationEvent_CastSkill calls ExecutePendingCast().
    /// </summary>
    public bool TryStartCastSkillAI(GameObject targetOverride = null)
    {
        GameObject target = targetOverride;
        if (target == null && baseAI != null && baseAI.CurrentTarget != null)
        {
            target = baseAI.CurrentTarget.gameObject;
        }

        Skill selectedSkill = null;

        // 1. Let BaseAI evaluate and select best skill
        if (baseAI != null)
        {
            UtilitySkill bestUtil = baseAI.SelectBestSkill();
            if (bestUtil != null && skills != null)
            {
                selectedSkill = skills.Find(s => s != null && s.UtilityData == bestUtil && s.IsReady);
            }
        }

        // 2. Fallback: first ready skill
        if (selectedSkill == null && skills != null)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].IsReady)
                {
                    selectedSkill = skills[i];
#if UNITY_EDITOR
                    baseAI?.RecordFallbackSkill(skills[i].ID);
#endif
                    break;
                }
            }
        }

        if (selectedSkill == null)
        {
            return false;
        }

        return StartCasting(selectedSkill, target);
    }

    /// <summary>
    /// Starts the casting process for a specific skill and target.
    /// </summary>
    public bool StartCasting(Skill skill, GameObject target = null)
    {
        if (skill == null || !skill.IsReady) return false;

        LastExecutedSkillData = skill.Data;

        if (brain != null)
        {
            if (!brain.TryCastSkill()) return false;
        }

        pendingSkill = skill;
        pendingTarget = target;
        return true;
    }

    public bool TryStartCastSkill(UtilitySkill utilSkill, GameObject targetOverride = null)
    {
        if (utilSkill == null || skills == null) return false;
        Skill skillToTrigger = skills.Find(s => s != null && s.UtilityData == utilSkill);
        if (skillToTrigger != null)
            return StartCasting(skillToTrigger, targetOverride);

        Debug.LogWarning($"[CharacterSkillController] No Skill found for UtilitySkill '{utilSkill.GetID()}'.");
        return false;
    }

    public bool TryStartCastSkill(string skillID, GameObject targetOverride = null)
    {
        if (skills == null) return false;
        Skill skillToTrigger = skills.Find(s => s != null && s.ID == skillID);
        if (skillToTrigger != null)
            return StartCasting(skillToTrigger, targetOverride);

        Debug.LogWarning($"[CharacterSkillController] Skill with ID '{skillID}' not found.");
        return false;
    }

    /// <summary>
    /// Called by AnimationEventController when AnimationEvent_CastSkill fires.
    /// Executes the pending skill that was locked when casting started.
    /// </summary>
    public bool ExecutePendingCast()
    {
        if (pendingSkill != null)
        {
            Skill skillToCast = pendingSkill;
            GameObject targetToCast = pendingTarget;
            pendingSkill = null;
            pendingTarget = null;
            return skillToCast.Trigger(targetToCast);
        }

        // Fallback if no pending cast was queued (e.g. direct animation trigger)
        return TryCastSkillAI();
    }

    /// <summary>
    /// Cancels any pending cast without executing it (e.g. on animation end or cast interrupt).
    /// </summary>
    public void CancelPendingCast()
    {
        pendingSkill = null;
        pendingTarget = null;
    }

    public bool TryCastSkillAI(GameObject targetOverride = null)
    {
        GameObject target = targetOverride;
        if (target == null && baseAI != null && baseAI.CurrentTarget != null)
        {
            target = baseAI.CurrentTarget.gameObject;
        }

        // Let BaseAI evaluate and select the best skill using its internal utility list
        if (baseAI != null)
        {
            UtilitySkill selectedSkill = baseAI.SelectBestSkill();
            if (selectedSkill != null)
            {
                return TriggerSkill(selectedSkill, target);
            }
        }

        // Fallback: trigger first available skill that is off cooldown
        if (skills != null)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].IsReady)
                {
#if UNITY_EDITOR
                    baseAI?.RecordFallbackSkill(skills[i].ID);
#endif
                    return skills[i].Trigger(target);
                }
            }
        }

        return false;
    }

    /// <summary>Triggers the Skill whose UtilityData matches the given ScriptableObject.</summary>
    public bool TriggerSkill(UtilitySkill utilSkill, GameObject targetOverride = null)
    {
        if (utilSkill == null || skills == null) return false;
        Skill skillToTrigger = skills.Find(s => s != null && s.UtilityData == utilSkill);
        if (skillToTrigger != null)
            return skillToTrigger.Trigger(targetOverride);
        
        Debug.LogWarning($"[CharacterSkillController] No Skill found for UtilitySkill '{utilSkill.GetID()}'.");
        return false;
    }

    /// <summary>Triggers the Skill matching the given skillID string.</summary>
    public bool TriggerSkill(string skillID, GameObject targetOverride = null)
    {
        if (skills == null) return false;
        Skill skillToTrigger = skills.Find(s => s != null && s.ID == skillID);
        if (skillToTrigger != null)
            return skillToTrigger.Trigger(targetOverride);
        
        Debug.LogWarning($"[CharacterSkillController] Skill with ID '{skillID}' not found.");
        return false;
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
        public SkillData Data => skill != null ? skill.Data : null;
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
                    Transform casterRoot = controller.Brain != null 
                        ? controller.Brain.transform 
                        : (controller.transform.root != null ? controller.transform.root : controller.transform);

                    skill.SetupSkillCaster(
                        controller.SkillTargeting, 
                        controller.SpawnPoint != null ? controller.SpawnPoint.transform : null, 
                        casterRoot
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
            Trigger(null);
        }

        public bool Trigger(GameObject targetOverride)
        {
            if (skill != null)
            {
                if (skill.TryCastSkill(targetOverride))
                {
                    return true;
                }

                if (skill.Flags == 3)
                {
                    skill.DebugCastSkill();
                    return true;
                }

                Debug.LogWarning($"Skill {ID} cannot be cast due to failing preconditions.");
            }
            return false;
        }
    }
}