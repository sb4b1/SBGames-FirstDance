using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Encapsulates the intent to place a unit onto the dungeon grid.
    /// Carries the external combat power factor used to scale capacity consumption.
    /// </summary>
    public readonly struct PlacementRequest
    {
        /// <summary>
        /// Specification of the unit to place.
        /// </summary>
        public readonly IUnitPlacementDefinition UnitDefinition;

        /// <summary>
        /// Target 2D tile coordinates in the dungeon grid.
        /// </summary>
        public readonly Vector2Int GridPosition;

        /// <summary>
        /// External combat power factor evaluated outside this system.
        /// Multiplies the base capacity cost of the unit (e.g. 1.0 = standard, 2.0 = double capacity).
        /// </summary>
        public readonly float CombatPowerFactor;

        public PlacementRequest(
            IUnitPlacementDefinition unitDefinition,
            Vector2Int gridPosition,
            float combatPowerFactor = 1.0f)
        {
            UnitDefinition = unitDefinition;
            GridPosition = gridPosition;
            CombatPowerFactor = Mathf.Max(0f, combatPowerFactor);
        }
    }
}
