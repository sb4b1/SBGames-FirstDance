using UnityEngine;

namespace SBGames.Personality.Data
{
    /// <summary>
    /// Represents an individual psychological personality trait assigned to a unit.
    /// Modifies Utility AI Goal selection (fear, threat, survival triggers) and Skill evaluation
    /// (AoE clustering, distance preferences, low-HP execution, and ally peeling).
    /// </summary>
    [CreateAssetMenu(fileName = "NewTrait", menuName = "SBGames/Personality/Character Trait")]
    public class CharacterTraitSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string traitId = "Reckless";
        [SerializeField] private string displayName = "Reckless";
        [SerializeField, TextArea(2, 4)] private string description = "Fear is eliminated. Fires AoE skills even against single targets. Never retreats.";
        [SerializeField] private Sprite icon;
        [SerializeField] private Color badgeColor = new Color(0.9f, 0.3f, 0.2f, 1f);

        [Header("Goal Scoring Biases (UtilityGoal)")]
        [Tooltip("Multiplier applied to Threat curve scoring (UtilityKillEnemyGoal).")]
        [SerializeField, Range(0.1f, 3f)] private float threatMultiplier = 1.0f;

        [Tooltip("Multiplier applied to Fear curve scoring (UtilityKillEnemyGoal & UtilitySurviveGoal).")]
        [SerializeField, Range(0f, 3f)] private float fearMultiplier = 1.0f;

        [Tooltip("If true, zeroes out fear calculation regardless of sensory input (e.g. Reckless).")]
        [SerializeField] private bool zeroFear = false;

        [Tooltip("HP percentage under which survival/flee goals activate. (e.g. 0.50 for Cowardly, 0 for Reckless).")]
        [SerializeField, Range(0f, 1f)] private float survivalHpThreshold = 0.25f;

        [Tooltip("If true, completely disables survival/flee goals (unit fights to the death).")]
        [SerializeField] private bool neverFlee = false;

        [Header("Skill Considerations (UtilitySkill)")]
        [Tooltip("Offset applied to required enemies for AoE cluster skills. (e.g. +1 for Pragmatic, -2 for Reckless to fire AoE on 1 target).")]
        [SerializeField] private int clusterCountOffset = 0;

        [Tooltip("Multiplier for ranged and kiting skills (e.g. 1.5 for Opportunist).")]
        [SerializeField, Range(0.5f, 2.5f)] private float rangePreferenceMultiplier = 1.0f;

        [Tooltip("Health percentage below which a target qualifies for execution bonus (e.g. 0.30 for Bloodthirsty).")]
        [SerializeField, Range(0f, 0.5f)] private float executionHealthThreshold = 0.30f;

        [Tooltip("Score multiplier applied to offensive skills when the target is below executionHealthThreshold.")]
        [SerializeField, Range(1f, 3f)] private float executionBonusMultiplier = 1.0f;

        [Tooltip("Ally health percentage below which defensive / peeling skills receive a score bonus (Guardian).")]
        [SerializeField, Range(0f, 0.6f)] private float peelingHealthThreshold = 0.35f;

        [Tooltip("Score multiplier applied to protective/peeling skills when any ally is critically wounded.")]
        [SerializeField, Range(1f, 3f)] private float peelingBonusMultiplier = 1.0f;

        [Header("Target Priority Bias")]
        [Tooltip("Priority weight towards low-health targets (Bloodthirsty).")]
        [SerializeField, Range(1f, 3f)] private float lowHealthTargetPriority = 1.0f;

        public string TraitId => traitId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color BadgeColor => badgeColor;

        public float ThreatMultiplier => threatMultiplier;
        public float FearMultiplier => fearMultiplier;
        public bool ZeroFear => zeroFear;
        public float SurvivalHpThreshold => survivalHpThreshold;
        public bool NeverFlee => neverFlee;

        public int ClusterCountOffset => clusterCountOffset;
        public float RangePreferenceMultiplier => rangePreferenceMultiplier;
        public float ExecutionHealthThreshold => executionHealthThreshold;
        public float ExecutionBonusMultiplier => executionBonusMultiplier;
        public float PeelingHealthThreshold => peelingHealthThreshold;
        public float PeelingBonusMultiplier => peelingBonusMultiplier;
        public float LowHealthTargetPriority => lowHealthTargetPriority;

        /// <summary>
        /// Combines this trait with the unit's RaceInstinctSO into an immutable, precomputed snapshot
        /// for zero-allocation evaluation ticks in Utility AI.
        /// </summary>
        public PersonalityModifierSnapshot BuildSnapshot(RaceInstinctSO race)
        {
            var snap = PersonalityModifierSnapshot.Default;

            float raceThreatMul = race != null ? race.ThreatIntakeMultiplier : 1f;
            float raceFearMul = race != null ? race.FearIntakeMultiplier : 1f;
            bool raceFearLock = race != null && race.LockFear;
            float raceLockedFear = race != null ? race.LockedFearValue : 0f;
            bool raceNoPanic = race != null && race.ImmuneToPanic;
            float raceBaseSurvival = race != null ? race.BaselineSurvivalHpThreshold : 0.20f;
            float raceWoundedBias = race != null ? race.WoundedTargetPriority : 1f;

            // Threat
            snap.ThreatMultiplier = Mathf.Max(0.01f, threatMultiplier * raceThreatMul);

            // Fear
            if (raceFearLock)
            {
                snap.IsFearLocked = true;
                snap.LockedFearValue = raceLockedFear;
                snap.FearMultiplier = 0f;
            }
            else if (zeroFear)
            {
                snap.IsFearLocked = true;
                snap.LockedFearValue = 0f;
                snap.FearMultiplier = 0f;
            }
            else
            {
                snap.IsFearLocked = false;
                snap.LockedFearValue = 0f;
                snap.FearMultiplier = Mathf.Max(0f, fearMultiplier * raceFearMul);
            }

            // Survival
            snap.IgnoreSurvivalGoal = neverFlee || raceNoPanic;
            snap.SurvivalHpThreshold = Mathf.Clamp01(Mathf.Max(survivalHpThreshold, raceBaseSurvival));

            // Skill Considerations
            snap.ClusterCountOffset = clusterCountOffset;
            snap.RangePreferenceMultiplier = rangePreferenceMultiplier;
            snap.ExecutionHealthThreshold = executionHealthThreshold;
            snap.ExecutionBonusMultiplier = executionBonusMultiplier;
            snap.PeelingHealthThreshold = peelingHealthThreshold;
            snap.PeelingBonusMultiplier = peelingBonusMultiplier;

            // Target Priority
            snap.LowHealthTargetPriority = lowHealthTargetPriority;
            snap.BleedingTargetPriority = raceWoundedBias;

            return snap;
        }
    }
}
