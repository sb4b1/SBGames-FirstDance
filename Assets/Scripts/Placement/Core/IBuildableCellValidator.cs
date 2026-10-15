using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Contract for verifying whether a grid cell is valid for building/placement.
    /// Used to enforce room-type restrictions (e.g. Entrance room is strictly non-buildable,
    /// while Normal, Safe, Boss rooms and corridors are buildable).
    /// </summary>
    public interface IBuildableCellValidator
    {
        bool IsCellBuildable(Vector2Int position);
    }

    /// <summary>
    /// Fast hash-set based buildable cell validator.
    /// Provides O(1) lookup with zero managed allocations during validation queries.
    /// </summary>
    public class SetBasedBuildableCellValidator : IBuildableCellValidator
    {
        private readonly HashSet<Vector2Int> _buildableCells;

        public int Count => _buildableCells.Count;

        public SetBasedBuildableCellValidator(IEnumerable<Vector2Int> buildableCells = null)
        {
            _buildableCells = buildableCells != null
                ? new HashSet<Vector2Int>(buildableCells)
                : new HashSet<Vector2Int>();
        }

        public bool IsCellBuildable(Vector2Int position) => _buildableCells.Contains(position);

        public void AddBuildableCell(Vector2Int position) => _buildableCells.Add(position);

        public void RemoveBuildableCell(Vector2Int position) => _buildableCells.Remove(position);

        public void Clear() => _buildableCells.Clear();
    }
}
