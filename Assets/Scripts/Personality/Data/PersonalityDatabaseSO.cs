using System;
using System.Collections.Generic;
using SBGames.Shop.Core;
using UnityEngine;

namespace SBGames.Personality.Data
{
    [Serializable]
    public class ArchetypeRaceMapping
    {
        public string archetypeId;
        public RaceInstinctSO raceInstinct;
    }

    /// <summary>
    /// Central catalog holding all available Character Traits, Race Instincts, and archetype defaults.
    /// Used by the Monster Shop for trait assignment upon summoning, and by the Altar Event for 1-of-3 trait drafts.
    /// </summary>
    [CreateAssetMenu(fileName = "PersonalityDatabase", menuName = "SBGames/Personality/Personality Database")]
    public class PersonalityDatabaseSO : ScriptableObject
    {
        [Header("Available Trait Pool")]
        [SerializeField] private List<CharacterTraitSO> availableTraits = new();

        [Header("Available Race Instincts")]
        [SerializeField] private List<RaceInstinctSO> availableRaces = new();

        [Header("Default Archetype Mappings")]
        [SerializeField] private List<ArchetypeRaceMapping> archetypeRaces = new();

        public IReadOnlyList<CharacterTraitSO> AvailableTraits => availableTraits;
        public IReadOnlyList<RaceInstinctSO> AvailableRaces => availableRaces;

        private Dictionary<string, RaceInstinctSO> _raceLookup;
        private Dictionary<string, CharacterTraitSO> _traitLookup;
        private Dictionary<string, RaceInstinctSO> _archetypeLookup;

        public void Initialize()
        {
            _traitLookup = new Dictionary<string, CharacterTraitSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in availableTraits)
            {
                if (t != null && !string.IsNullOrWhiteSpace(t.TraitId))
                    _traitLookup[t.TraitId] = t;
            }

            _raceLookup = new Dictionary<string, RaceInstinctSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in availableRaces)
            {
                if (r != null && !string.IsNullOrWhiteSpace(r.RaceId))
                    _raceLookup[r.RaceId] = r;
            }

            _archetypeLookup = new Dictionary<string, RaceInstinctSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in archetypeRaces)
            {
                if (m != null && !string.IsNullOrWhiteSpace(m.archetypeId) && m.raceInstinct != null)
                    _archetypeLookup[m.archetypeId] = m.raceInstinct;
            }
        }

        private void EnsureLookup()
        {
            if (_traitLookup == null || _raceLookup == null || _archetypeLookup == null)
            {
                Initialize();
            }
        }

        public CharacterTraitSO GetTraitById(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId)) return null;
            EnsureLookup();
            return _traitLookup.GetValueOrDefault(traitId);
        }

        public RaceInstinctSO GetRaceById(string raceId)
        {
            if (string.IsNullOrWhiteSpace(raceId)) return null;
            EnsureLookup();
            return _raceLookup.GetValueOrDefault(raceId);
        }

        public RaceInstinctSO GetRaceForArchetype(string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId)) return null;
            EnsureLookup();
            if (_archetypeLookup.TryGetValue(archetypeId, out var race))
            {
                return race;
            }

            // Fallback: search availableRaces for name match or first available
            if (availableRaces != null && availableRaces.Count > 0)
            {
                for (int i = 0; i < availableRaces.Count; i++)
                {
                    if (availableRaces[i] != null && archetypeId.IndexOf(availableRaces[i].RaceId, StringComparison.OrdinalIgnoreCase) >= 0)
                        return availableRaces[i];
                }
                return availableRaces[0];
            }

            return null;
        }

        /// <summary>
        /// Rolls a random trait from the available pool, optionally excluding a specific trait.
        /// </summary>
        public CharacterTraitSO RollRandomTrait(CharacterTraitSO exclude = null, IRandomProvider rng = null)
        {
            if (availableTraits == null || availableTraits.Count == 0) return null;

            var candidates = new List<CharacterTraitSO>(availableTraits.Count);
            for (int i = 0; i < availableTraits.Count; i++)
            {
                var t = availableTraits[i];
                if (t != null && t != exclude)
                {
                    candidates.Add(t);
                }
            }

            if (candidates.Count == 0) return exclude;

            int index = rng != null ? rng.Next(0, candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
            return candidates[index];
        }

        /// <summary>
        /// Rolls 'count' unique randomized traits for the Altar 1-of-N draft, excluding the current trait if possible.
        /// </summary>
        public List<CharacterTraitSO> RollDraftTraits(int count, CharacterTraitSO currentTrait, IRandomProvider rng = null)
        {
            var results = new List<CharacterTraitSO>(count);
            if (availableTraits == null || availableTraits.Count == 0) return results;

            var pool = new List<CharacterTraitSO>(availableTraits.Count);
            for (int i = 0; i < availableTraits.Count; i++)
            {
                var t = availableTraits[i];
                if (t != null && t != currentTrait)
                {
                    pool.Add(t);
                }
            }

            // If pool is smaller than count, allow including currentTrait as fallback candidate
            if (pool.Count < count && currentTrait != null && !pool.Contains(currentTrait))
            {
                pool.Add(currentTrait);
            }

            while (results.Count < count && pool.Count > 0)
            {
                int idx = rng != null ? rng.Next(0, pool.Count) : UnityEngine.Random.Range(0, pool.Count);
                results.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return results;
        }
    }
}
