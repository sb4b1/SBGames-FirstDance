using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UtilitySkillRuntimeDatabase : MonoBehaviour
{
    [SerializeField] private UtilitySkillDatabaseSO database;

    private Dictionary<string, UtilitySkillEntry> utilSkillDict;

    public static UtilitySkillRuntimeDatabase Instance { get; private set; }

    void Awake()
    {
        utilSkillDict = database.utilSkills
            .ToDictionary(c => c.id, c => c);
    }

    void Start()
    {
        GameServices.Register(this);

        if (utilSkillDict == null || utilSkillDict.Count == 0)
        {
            Debug.LogWarning("[UtilitySkillRuntimeDatabase] Utility skill dictionary is empty. Check if the database is assigned and populated.");
        }
    }

    public UtilitySkillEntry Get(string id)
    {
        return utilSkillDict.TryGetValue(id, out var entry)
            ? entry
            : null;
    }
}