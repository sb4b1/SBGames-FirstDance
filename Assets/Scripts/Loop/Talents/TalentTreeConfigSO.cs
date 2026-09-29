using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SBGames.Loop.Talents
{
    /// <summary>
    /// ScriptableObject database and configuration for the Dungeon Master Talent Tree.
    /// Organizes all available talent nodes and validates tree graph integrity.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTalentTreeConfig", menuName = "SBGames/Talents/Talent Tree Config", order = 21)]
    public class TalentTreeConfigSO : ScriptableObject
    {
        [Header("Tree Identity")]
        [SerializeField] private string treeName = "Dungeon Master Talent Tree";

        [Tooltip("Master collection of all talent nodes included in this tree configuration.")]
        [SerializeField] private List<TalentNodeSO> talents = new();

        public string TreeName => treeName;
        public IReadOnlyList<TalentNodeSO> Talents => talents;

        /// <summary>
        /// Programmatic initialization for tests and runtime setups.
        /// </summary>
        public void Initialize(string name, IEnumerable<TalentNodeSO> talentList)
        {
            treeName = name;
            talents.Clear();
            if (talentList != null)
            {
                talents.AddRange(talentList);
            }
        }

        /// <summary>
        /// Retrieves all talent nodes belonging to a specific pillar.
        /// </summary>
        public IEnumerable<TalentNodeSO> GetTalentsByPillar(TalentPillar pillar)
        {
            return talents.Where(t => t != null && t.Pillar == pillar);
        }

        /// <summary>
        /// Retrieves all talent nodes belonging to a specific tier.
        /// </summary>
        public IEnumerable<TalentNodeSO> GetTalentsByTier(TalentTier tier)
        {
            return talents.Where(t => t != null && t.Tier == tier);
        }

        /// <summary>
        /// Retrieves all root nodes (talents with zero prerequisites).
        /// </summary>
        public IEnumerable<TalentNodeSO> GetRootTalents()
        {
            return talents.Where(t => t != null && (t.Prerequisites == null || t.Prerequisites.Count == 0));
        }

        /// <summary>
        /// Finds a talent by unique identifier.
        /// </summary>
        public TalentNodeSO FindTalentById(string talentId)
        {
            if (string.IsNullOrEmpty(talentId)) return null;
            return talents.FirstOrDefault(t => t != null && t.TalentId.Equals(talentId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates graph structure for integrity (duplicate IDs, null entries, cycles).
        /// </summary>
        /// <param name="validationErrors">List populated with error descriptions if validation fails.</param>
        /// <returns>True if tree is structurally valid, false if errors exist.</returns>
        public bool ValidateTree(out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < talents.Count; i++)
            {
                var node = talents[i];
                if (node == null)
                {
                    validationErrors.Add($"Talent at index {i} is null.");
                    continue;
                }

                if (string.IsNullOrEmpty(node.TalentId))
                {
                    validationErrors.Add($"Talent '{node.name}' has an empty TalentId.");
                }
                else if (!seenIds.Add(node.TalentId))
                {
                    validationErrors.Add($"Duplicate TalentId '{node.TalentId}' found on node '{node.name}'.");
                }
            }

            // Cycle detection using DFS
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in talents)
            {
                if (node == null) continue;
                if (HasCycleDFS(node, visited, recursionStack, validationErrors))
                {
                    // Error is recorded in recursion
                }
            }

            return validationErrors.Count == 0;
        }

        private bool HasCycleDFS(
            TalentNodeSO current,
            HashSet<string> visited,
            HashSet<string> stack,
            List<string> errors)
        {
            if (current == null) return false;

            if (stack.Contains(current.TalentId))
            {
                errors.Add($"Circular dependency detected involving talent '{current.TalentId}'.");
                return true;
            }

            if (visited.Contains(current.TalentId)) return false;

            visited.Add(current.TalentId);
            stack.Add(current.TalentId);

            if (current.Prerequisites != null)
            {
                foreach (var prereq in current.Prerequisites)
                {
                    if (prereq != null && HasCycleDFS(prereq, visited, stack, errors))
                    {
                        return true;
                    }
                }
            }

            stack.Remove(current.TalentId);
            return false;
        }
    }
}
