using System;
using System.Collections.Generic;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Loop.Talents
{
    /// <summary>
    /// ScriptableObject defining a single talent node within the Dungeon Master Talent Tree.
    /// Stores metadata, unlock constraints, prerequisites, rank gating, and active power configurations.
    /// </summary>
    [CreateAssetMenu(fileName = "NewTalentNode", menuName = "SBGames/Talents/Talent Node", order = 20)]
    public class TalentNodeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this talent node (e.g. DM_SWARM_01).")]
        [SerializeField] private string talentId = "DM_TALENT_01";

        [Tooltip("User-facing display name of the talent.")]
        [SerializeField] private string displayName = "New Talent";

        [Tooltip("Detailed description of the talent mechanics and lore.")]
        [SerializeField, TextArea(2, 5)] private string description = "";

        [Tooltip("UI Icon representation.")]
        [SerializeField] private Sprite icon;

        [Header("Classification")]
        [Tooltip("Thematic pillar this talent belongs to.")]
        [SerializeField] private TalentPillar pillar = TalentPillar.Core;

        [Tooltip("Tier level of the talent.")]
        [SerializeField] private TalentTier tier = TalentTier.Tier0;

        [Tooltip("Behavioral execution type (Passive, ActivePower, Aura, Triggered).")]
        [SerializeField] private TalentType talentType = TalentType.Passive;

        [Header("Unlock Constraints (Planning Phase)")]
        [Tooltip("Energy currency cost required to unlock this talent.")]
        [SerializeField, Min(0)] private int energyCost = 15;

        [Tooltip("Minimum dungeon gate rank required before this talent can be unlocked.")]
        [SerializeField] private DungeonRank minimumRank = DungeonRank.E;

        [Tooltip("Other talent nodes that must be unlocked prior to unlocking this node.")]
        [SerializeField] private List<TalentNodeSO> prerequisites = new();

        [Tooltip("Whether all prerequisites or any prerequisite is required.")]
        [SerializeField] private TalentPrerequisiteLogic prerequisiteLogic = TalentPrerequisiteLogic.All;

        [Header("Active Power Configuration (Battle Phase)")]
        [Tooltip("Energy cost to cast this active power in the Battle phase once unlocked.")]
        [SerializeField, Min(0)] private int activeEnergyCost = 0;

        [Tooltip("Cooldown duration in seconds between active power uses.")]
        [SerializeField, Min(0f)] private float cooldownDuration = 0f;

        [Header("System Modifiers & Hooks (Extensible Grounding)")]
        [Tooltip("Additive bonus to PlayerModifier in DifficultyScaler (e.g., 0.15 = +15% total defense multiplier).")]
        [SerializeField] private float playerModifierBonus = 0f;

        [Tooltip("Flat bonus to Dungeon Master synergy power rating in DifficultyScaler.")]
        [SerializeField] private int dmSynergyPowerBonus = 0;

        [Tooltip("Custom tag or category string for future gameplay extension.")]
        [SerializeField] private string customTag = "";

        #region Public Properties

        public string TalentId => talentId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public TalentPillar Pillar => pillar;
        public TalentTier Tier => tier;
        public TalentType Type => talentType;
        public int EnergyCost => Mathf.Max(0, energyCost);
        public DungeonRank MinimumRank => minimumRank;
        public IReadOnlyList<TalentNodeSO> Prerequisites => prerequisites;
        public TalentPrerequisiteLogic PrerequisiteLogic => prerequisiteLogic;
        public int ActiveEnergyCost => Mathf.Max(0, activeEnergyCost);
        public float CooldownDuration => Mathf.Max(0f, cooldownDuration);
        public float PlayerModifierBonus => playerModifierBonus;
        public int DmSynergyPowerBonus => dmSynergyPowerBonus;
        public string CustomTag => customTag;

        #endregion

        /// <summary>
        /// Programmatic initialization helper for unit tests and runtime procedurally generated talents.
        /// </summary>
        public void Initialize(
            string id,
            string name,
            int cost,
            TalentPillar pillar = TalentPillar.Core,
            TalentTier tier = TalentTier.Tier0,
            TalentType type = TalentType.Passive,
            DungeonRank minRank = DungeonRank.E,
            IEnumerable<TalentNodeSO> prereqs = null,
            TalentPrerequisiteLogic prereqLogic = TalentPrerequisiteLogic.All,
            int activeCost = 0,
            float cooldown = 0f,
            float playerModBonus = 0f,
            int synergyBonus = 0,
            string desc = "",
            string tag = "")
        {
            talentId = id;
            displayName = name;
            energyCost = Mathf.Max(0, cost);
            this.pillar = pillar;
            this.tier = tier;
            talentType = type;
            minimumRank = minRank;
            prerequisiteLogic = prereqLogic;
            activeEnergyCost = Mathf.Max(0, activeCost);
            cooldownDuration = Mathf.Max(0f, cooldown);
            playerModifierBonus = playerModBonus;
            dmSynergyPowerBonus = synergyBonus;
            description = desc;
            customTag = tag;

            prerequisites.Clear();
            if (prereqs != null)
            {
                prerequisites.AddRange(prereqs);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(talentId))
            {
                talentId = name.ToUpper().Replace(" ", "_");
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = name;
            }
        }
    }
}
