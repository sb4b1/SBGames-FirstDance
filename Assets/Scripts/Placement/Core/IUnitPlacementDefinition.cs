using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Represents the immutable configuration for a placeable unit type.
    /// Defines base resource cost and baseline capacity footprint.
    /// </summary>
    public interface IUnitPlacementDefinition
    {
        /// <summary>
        /// Unique identifier for the unit type (e.g. "skeleton_warrior", "fire_imp").
        /// </summary>
        string UnitId { get; }

        /// <summary>
        /// Human-readable name for UI and logs.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Base resource or currency cost required to place this unit.
        /// </summary>
        int BaseCost { get; }

        /// <summary>
        /// Base dungeon capacity consumed before combat power scaling is applied.
        /// </summary>
        int BaseCapacity { get; }

        /// <summary>
        /// Spatial footprint on the grid in tiles (defaults to 1x1).
        /// </summary>
        Vector2Int Footprint { get; }

        /// <summary>
        /// Optional prefab to spawn when placed in a Unity scene.
        /// </summary>
        GameObject Prefab { get; }
    }
}
