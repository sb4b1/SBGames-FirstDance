using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    /// <summary>
    /// Base ScriptableObject defining a test scenario for a sandbox.
    /// Derived types add sandbox-specific configuration (e.g. CombatScenarioSO, DungeonScenarioSO).
    /// </summary>
    public abstract class SandboxScenarioSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique programmatic identifier for this scenario (e.g. A1, S1, ROOM_3X3).")]
        [SerializeField] private string scenarioId;

        [Tooltip("Human-readable scenario display name.")]
        [SerializeField] private string displayName;

        [Tooltip("Target sandbox identifier (e.g. Combat, DungeonGen, MetaGUI, AdventurerRun).")]
        [SerializeField] private string sandboxId;

        [TextArea(2, 5)]
        [Tooltip("Detailed description of what this scenario exercises and verifies.")]
        [SerializeField] private string description;

        [Header("Sign-off Checklist Coverage")]
        [Tooltip("Checklist item IDs from the sandbox sign-off sheet covered by this scenario.")]
        [SerializeField] private List<string> coveredChecklistItemIds = new();

        public string ScenarioId => scenarioId;
        public string DisplayName => displayName;
        public string SandboxId => sandboxId;
        public string Description => description;
        public IReadOnlyList<string> CoveredChecklistItemIds => coveredChecklistItemIds;

        public void SetBaseProperties(string id, string name, string sandbox, string desc, IEnumerable<string> checklistIds = null)
        {
            scenarioId = id;
            displayName = name;
            sandboxId = sandbox;
            description = desc;
            coveredChecklistItemIds.Clear();
            if (checklistIds != null)
            {
                coveredChecklistItemIds.AddRange(checklistIds);
            }
        }
    }
}
