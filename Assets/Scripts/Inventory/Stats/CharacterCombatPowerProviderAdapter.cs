using System;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Inventory.Stats
{
    /// <summary>
    /// Implements ICombatPowerProvider by querying a CharacterCombatPower component or prefab
    /// to provide dynamic combat power scaling factors for the placement system.
    /// </summary>
    public class CharacterCombatPowerProviderAdapter : ICombatPowerProvider
    {
        private readonly float _baselineScore;
        private readonly float _globalMultiplier;

        public CharacterCombatPowerProviderAdapter(float baselineScore = 200f, float globalMultiplier = 1.0f)
        {
            _baselineScore = Mathf.Max(1f, baselineScore);
            _globalMultiplier = Mathf.Max(0.1f, globalMultiplier);
        }

        /// <summary>
        /// Evaluates the dynamic Combat Power factor for a given unit definition.
        /// </summary>
        public float GetCombatPowerFactor(IUnitPlacementDefinition unitDefinition)
        {
            if (unitDefinition == null) return 1.0f;

            if (unitDefinition.Prefab != null)
            {
                var cp = unitDefinition.Prefab.GetComponentInChildren<CharacterCombatPower>();
                if (cp != null)
                {
                    float factor = cp.RecalculateCombatPower() / _baselineScore;
                    return Mathf.Max(0.1f, factor * _globalMultiplier);
                }
            }

            return 1.0f * _globalMultiplier;
        }
    }
}
