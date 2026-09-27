using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Contract for spatial tracking and tile collision on the dungeon grid.
    /// </summary>
    public interface IGridOccupancyTracker
    {
        Vector2Int GridSize { get; }
        bool IsWithinBounds(Vector2Int position);
        bool IsOccupied(Vector2Int position);
        bool TryGetUnitAt(Vector2Int position, out PlacedUnitInstance unit);
        bool RegisterUnit(PlacedUnitInstance unit);
        bool UnregisterUnit(Vector2Int position, out PlacedUnitInstance removedUnit);
        IReadOnlyCollection<PlacedUnitInstance> GetAllPlacedUnits();
        void Clear();
    }

    /// <summary>
    /// Standard discrete 2D grid occupancy tracker backed by a hash map.
    /// </summary>
    public class GridOccupancyTracker : IGridOccupancyTracker
    {
        private readonly Vector2Int gridSize;
        private readonly Dictionary<Vector2Int, PlacedUnitInstance> gridCells;

        public Vector2Int GridSize => gridSize;

        public GridOccupancyTracker(int width, int height)
        {
            gridSize = new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
            gridCells = new Dictionary<Vector2Int, PlacedUnitInstance>();
        }

        public GridOccupancyTracker(Vector2Int size) : this(size.x, size.y) { }

        public bool IsWithinBounds(Vector2Int position)
        {
            return position.x >= 0 && position.x < gridSize.x &&
                   position.y >= 0 && position.y < gridSize.y;
        }

        public bool IsOccupied(Vector2Int position)
        {
            return gridCells.ContainsKey(position);
        }

        public bool TryGetUnitAt(Vector2Int position, out PlacedUnitInstance unit)
        {
            return gridCells.TryGetValue(position, out unit);
        }

        public bool RegisterUnit(PlacedUnitInstance unit)
        {
            if (unit == null) return false;
            if (!IsWithinBounds(unit.GridPosition)) return false;
            if (IsOccupied(unit.GridPosition)) return false;

            gridCells[unit.GridPosition] = unit;
            return true;
        }

        public bool UnregisterUnit(Vector2Int position, out PlacedUnitInstance removedUnit)
        {
            if (gridCells.TryGetValue(position, out removedUnit))
            {
                gridCells.Remove(position);
                return true;
            }

            removedUnit = null;
            return false;
        }

        public IReadOnlyCollection<PlacedUnitInstance> GetAllPlacedUnits()
        {
            return gridCells.Values;
        }

        public void Clear()
        {
            gridCells.Clear();
        }
    }
}
