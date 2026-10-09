using UnityEngine;

namespace SBGames.Personality.Data
{
    /// <summary>
    /// Represents the inherent, immutable psychological instincts of a monster or character race.
    /// Defines base emotional constraints (e.g., Undead fear immunity, Goblinoid cowardice, Beast bloodlust).
    /// </summary>
    [CreateAssetMenu(fileName = "NewRaceInstinct", menuName = "SBGames/Personality/Race Instinct")]
    public class RaceInstinctSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string raceId = "Undead";
        [SerializeField] private string displayName = "Undead";
        [SerializeField, TextArea(2, 4)] private string description = "Fear is locked to 0. Immune to panic and retreat.";
        [SerializeField] private Sprite icon;

        [Header("Fear & Threat Constraints")]
        [Tooltip("When true, the unit's fear level is permanently locked to lockedFearValue regardless of sensory inputs.")]
        [SerializeField] private bool lockFear = false;
        [SerializeField] private float lockedFearValue = 0f;

        [Tooltip("Multiplier applied to fear generation from sensory perception (e.g. 1.5 for easily panicked Goblinoids).")]
        [SerializeField, Range(0f, 3f)] private float fearIntakeMultiplier = 1.0f;

        [Tooltip("Multiplier applied to threat generation from sensing enemies.")]
        [SerializeField, Range(0f, 3f)] private float threatIntakeMultiplier = 1.0f;

        [Header("Targeting Instincts")]
        [Tooltip("Priority multiplier for targets that are bleeding or below 30% HP (Beast instinct).")]
        [SerializeField, Range(1f, 3f)] private float woundedTargetPriority = 1.0f;

        [Header("Survival Thresholds")]
        [Tooltip("Default HP threshold (0.0 to 1.0) under which survival/flee goals begin scoring.")]
        [SerializeField, Range(0f, 1f)] private float baselineSurvivalHpThreshold = 0.20f;

        [Tooltip("If true, the race completely ignores survival/flee goals regardless of fear or health.")]
        [SerializeField] private bool immuneToPanic = false;

        public string RaceId => raceId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;

        public bool LockFear => lockFear;
        public float LockedFearValue => lockedFearValue;
        public float FearIntakeMultiplier => fearIntakeMultiplier;
        public float ThreatIntakeMultiplier => threatIntakeMultiplier;
        public float WoundedTargetPriority => woundedTargetPriority;
        public float BaselineSurvivalHpThreshold => baselineSurvivalHpThreshold;
        public bool ImmuneToPanic => immuneToPanic;
    }
}
