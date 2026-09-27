using System;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Strategy interface for calculating the effective capacity consumption
    /// of a unit given its base capacity and external combat power factor.
    /// Enables pluggable formulas (linear, stepped, exponential, soft-capped).
    /// </summary>
    public interface ICapacityMultiplierStrategy
    {
        /// <summary>
        /// Calculates final integer capacity consumed.
        /// </summary>
        /// <param name="baseCapacity">Baseline capacity rating of the unit type.</param>
        /// <param name="combatPowerFactor">External combat power multiplier (e.g. 1.0 = normal, 2.0 = double).</param>
        /// <returns>Non-negative integer capacity consumed.</returns>
        int CalculateEffectiveCapacity(int baseCapacity, float combatPowerFactor);
    }

    /// <summary>
    /// Default linear multiplier strategy.
    /// Scaled Capacity = Ceil(BaseCapacity * max(0, CombatPowerFactor)).
    /// Uses Ceiling rounding so that any fractional capacity upgrade claims a full slot,
    /// preventing sub-slot exploitation. Configurable minimum floor.
    /// </summary>
    public class LinearCapacityMultiplierStrategy : ICapacityMultiplierStrategy
    {
        private readonly int minimumCapacity;

        public LinearCapacityMultiplierStrategy(int minimumCapacity = 1)
        {
            this.minimumCapacity = Math.Max(0, minimumCapacity);
        }

        public int CalculateEffectiveCapacity(int baseCapacity, float combatPowerFactor)
        {
            if (baseCapacity <= 0) return 0;

            float validFactor = Mathf.Max(0f, combatPowerFactor);
            float scaled = baseCapacity * validFactor;

            // Ceil to prevent 0.5 capacity bypassing whole integer capacity slot restrictions
            int result = Mathf.CeilToInt(scaled);
            return Mathf.Max(minimumCapacity, result);
        }
    }
}
