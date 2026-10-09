using System;
using SBGames.Personality.Data;
using UnityEngine;

namespace SBGames.Personality
{
    /// <summary>
    /// Component attached to the character root GameObject.
    /// Bridges high-level psychological state (immutable Race Instinct + dynamic Trait) with runtime AI.
    /// Automatically maintains and caches a precomputed <see cref="PersonalityModifierSnapshot"/> for zero-allocation
    /// evaluation by Utility AI goals and skills.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPersonality : MonoBehaviour
    {
        [Header("Dual-Layer Psychological Model")]
        [Tooltip("The immutable race instinct of this character.")]
        [SerializeField] private RaceInstinctSO raceInstinct;

        [Tooltip("The active individual personality trait.")]
        [SerializeField] private CharacterTraitSO activeTrait;

        [Header("Precomputed Zero-Alloc Modifier Snapshot (Read-Only)")]
        [SerializeField] private PersonalityModifierSnapshot modifierSnapshot = PersonalityModifierSnapshot.Default;

        public RaceInstinctSO RaceInstinct => raceInstinct;
        public CharacterTraitSO ActiveTrait => activeTrait;
        public ref readonly PersonalityModifierSnapshot Snapshot => ref modifierSnapshot;

        /// <summary>
        /// Invoked when the character's active trait is changed (e.g. via Altar transmutation).
        /// Arguments: (oldTrait, newTrait).
        /// </summary>
        public event Action<CharacterTraitSO, CharacterTraitSO> OnTraitChanged;

        private void Awake()
        {
            RecomputeSnapshot();
        }

        private void OnValidate()
        {
            RecomputeSnapshot();
        }

        /// <summary>
        /// Initializes the unit's race and starting trait.
        /// </summary>
        public void Initialize(RaceInstinctSO race, CharacterTraitSO trait)
        {
            raceInstinct = race;
            var oldTrait = activeTrait;
            activeTrait = trait;

            RecomputeSnapshot();
            OnTraitChanged?.Invoke(oldTrait, activeTrait);
        }

        /// <summary>
        /// Updates the unit's personality trait (e.g. from Altar ritual draft).
        /// Recalculates the modifier snapshot immediately for zero-allocation AI updates.
        /// </summary>
        public void SetTrait(CharacterTraitSO newTrait)
        {
            if (activeTrait == newTrait) return;

            var oldTrait = activeTrait;
            activeTrait = newTrait;

            RecomputeSnapshot();
            OnTraitChanged?.Invoke(oldTrait, activeTrait);
        }

        /// <summary>
        /// Rebuilds the cached modifier snapshot from the current race instinct and active trait.
        /// </summary>
        public void RecomputeSnapshot()
        {
            if (activeTrait != null)
            {
                modifierSnapshot = activeTrait.BuildSnapshot(raceInstinct);
            }
            else if (raceInstinct != null)
            {
                // Fallback snapshot with only race rules
                var snap = PersonalityModifierSnapshot.Default;
                snap.ThreatMultiplier = raceInstinct.ThreatIntakeMultiplier;
                snap.FearMultiplier = raceInstinct.LockFear ? 0f : raceInstinct.FearIntakeMultiplier;
                snap.IsFearLocked = raceInstinct.LockFear;
                snap.LockedFearValue = raceInstinct.LockedFearValue;
                snap.IgnoreSurvivalGoal = raceInstinct.ImmuneToPanic;
                snap.SurvivalHpThreshold = raceInstinct.BaselineSurvivalHpThreshold;
                snap.BleedingTargetPriority = raceInstinct.WoundedTargetPriority;
                modifierSnapshot = snap;
            }
            else
            {
                modifierSnapshot = PersonalityModifierSnapshot.Default;
            }
        }
    }
}
