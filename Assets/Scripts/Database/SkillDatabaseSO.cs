using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SBGames.Database
{
    /// <summary>
    /// ScriptableObject database containing all SkillData assets.
    /// Provides fast O(1) runtime lookups and skill queries.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "SBGames/Database/Skill Database")]
    public class SkillDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<SkillData> skills = new();

        private Dictionary<string, SkillData> _lookup;

        public IReadOnlyList<SkillData> Skills => skills;

        /// <summary>
        /// Initializes runtime dictionary for O(1) lookups.
        /// </summary>
        public void Initialize()
        {
            _lookup = new Dictionary<string, SkillData>(StringComparer.OrdinalIgnoreCase);

            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                string id = skill.skillID;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"[SkillDatabaseSO] Skill at index {i} has an empty ID.", this);
                    continue;
                }

                if (_lookup.ContainsKey(id))
                {
                    Debug.LogWarning($"[SkillDatabaseSO] Duplicate skill ID '{id}' found in database. Overwriting previous entry.", this);
                }

                _lookup[id] = skill;
            }
        }

        private void EnsureInitialized()
        {
            if (_lookup == null)
            {
                Initialize();
            }
        }

        public SkillData Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureInitialized();
            return _lookup.GetValueOrDefault(id);
        }

        public bool TryGet(string id, out SkillData skill)
        {
            skill = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureInitialized();
            return _lookup.TryGetValue(id, out skill);
        }

        public IEnumerable<SkillData> GetByType(SkillType type)
        {
            return skills.Where(s => s != null && s.type == type);
        }

        /// <summary>
        /// Editor-only assignment method for auto-indexing.
        /// </summary>
        public void SetSkillsEditor(List<SkillData> newSkills)
        {
            skills = newSkills ?? new List<SkillData>();
        }
    }
}
