using System;
using UnityEngine;

namespace SBGames.Personality.Data
{
    /// <summary>
    /// Immutable value snapshot containing precomputed psychological and utility scoring modifiers.
    /// Cached on <see cref="CharacterPersonality"/> upon trait/race assignment to guarantee zero runtime allocations
    /// in high-frequency Utility AI evaluation hot paths.
    /// </summary>
    [Serializable]
    public struct PersonalityModifierSnapshot
    {
        // --- Threat & Fear Modifiers (Goal Evaluation) ---
        public float ThreatMultiplier;
        public float FearMultiplier;
        public bool IsFearLocked;
        public float LockedFearValue;

        // --- Survival & Courage Gates (UtilitySurviveGoal / KeepDistanceGoal) ---
        public float SurvivalHpThreshold; // Normalized HP percentage (e.g. 0.25 = 25% HP)
        public bool IgnoreSurvivalGoal;    // E.g. Reckless never flees

        // --- Skill Considerations (UtilitySkill Evaluation) ---
        public int ClusterCountOffset;          // E.g. Pragmatic requires +1 enemy, Reckless requires -2 (fires at 1)
        public float RangePreferenceMultiplier; // E.g. Opportunist boosts ranged/kiting skills
        public float ExecutionHealthThreshold;  // E.g. Target HP <= 30%
        public float ExecutionBonusMultiplier;  // E.g. +40% score when target is low
        public float PeelingHealthThreshold;    // E.g. Ally HP <= 35%
        public float PeelingBonusMultiplier;    // E.g. +50% score for peel/buff/defensive skills

        // --- Target Scoring ---
        public float LowHealthTargetPriority;   // Weight bias towards low-HP targets (Bloodthirsty)
        public float BleedingTargetPriority;    // Weight bias towards bleeding targets (Beast)

        // --- Memory & Retention Modifiers ---
        public float HazardMemoryDurationMultiplier; // Cowardly / Cautious > 1, Reckless < 1
        public float GrudgeWeightMultiplier;          // Reckless / Bloodthirsty > 1
        public float BurstDamagePanicMultiplier;      // Cowardly > 1, Reckless < 1
        public bool HasTunnelVision;                  // Reckless / Bloodthirsty has tunnel vision in combat
        public float TargetPruneDelayMultiplier;      // Pragmatic quickly prunes unreachable/immune targets

        public static PersonalityModifierSnapshot Default => new PersonalityModifierSnapshot
        {
            ThreatMultiplier = 1.0f,
            FearMultiplier = 1.0f,
            IsFearLocked = false,
            LockedFearValue = 0f,
            SurvivalHpThreshold = 0.25f,
            IgnoreSurvivalGoal = false,
            ClusterCountOffset = 0,
            RangePreferenceMultiplier = 1.0f,
            ExecutionHealthThreshold = 0.30f,
            ExecutionBonusMultiplier = 1.0f,
            PeelingHealthThreshold = 0.35f,
            PeelingBonusMultiplier = 1.0f,
            LowHealthTargetPriority = 1.0f,
            BleedingTargetPriority = 1.0f,
            HazardMemoryDurationMultiplier = 1.0f,
            GrudgeWeightMultiplier = 1.0f,
            BurstDamagePanicMultiplier = 1.0f,
            HasTunnelVision = false,
            TargetPruneDelayMultiplier = 1.0f
        };
    }
}
