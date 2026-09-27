using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Contract for computing or retrieving the external Combat Power factor for a unit.
    /// Fully decouples combat calculations, relic buffs, and stat evaluations from placement logic.
    /// </summary>
    public interface ICombatPowerProvider
    {
        /// <summary>
        /// Evaluates the dynamic Combat Power factor for a given unit type.
        /// </summary>
        /// <param name="unitDefinition">The unit specification being placed.</param>
        /// <returns>A non-negative scaling factor (e.g. 1.0 = standard, 2.0 = double combat power).</returns>
        float GetCombatPowerFactor(IUnitPlacementDefinition unitDefinition);
    }

    /// <summary>
    /// Flexible implementation supporting baseline factors, per-unit overrides,
    /// and global run multipliers (e.g. dungeon floor buffs, relics).
    /// </summary>
    public class DefaultCombatPowerProvider : ICombatPowerProvider
    {
        private float globalMultiplier = 1.0f;
        private readonly Dictionary<string, float> unitPowerOverrides = new();

        public float GlobalMultiplier
        {
            get => globalMultiplier;
            set => globalMultiplier = Mathf.Max(0.1f, value);
        }

        public DefaultCombatPowerProvider(float defaultGlobalMultiplier = 1.0f)
        {
            GlobalMultiplier = defaultGlobalMultiplier;
        }

        public void SetUnitPowerFactor(string unitId, float factor)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            unitPowerOverrides[unitId] = Mathf.Max(0.1f, factor);
        }

        public void RemoveUnitPowerFactor(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            unitPowerOverrides.Remove(unitId);
        }

        public float GetCombatPowerFactor(IUnitPlacementDefinition unitDefinition)
        {
            if (unitDefinition == null) return 1.0f;

            float baseFactor = 1.0f;
            if (unitPowerOverrides.TryGetValue(unitDefinition.UnitId, out float overrideFactor))
            {
                baseFactor = overrideFactor;
            }

            return baseFactor * globalMultiplier;
        }
    }
}
