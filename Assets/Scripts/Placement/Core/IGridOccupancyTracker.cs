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
        private readonly List<PlacedUnitInstance> distinctUnits;

        public Vector2Int GridSize => gridSize;

        public GridOccupancyTracker(int width, int height)
        {
            gridSize = new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
            gridCells = new Dictionary<Vector2Int, PlacedUnitInstance>();
            distinctUnits = new List<PlacedUnitInstance>();
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

            int fpX = unit.Definition != null ? Mathf.Max(1, unit.Definition.Footprint.x) : 1;
            int fpY = unit.Definition != null ? Mathf.Max(1, unit.Definition.Footprint.y) : 1;

            // 1. Verify all cells in footprint are within bounds and unoccupied
            for (int x = 0; x < fpX; x++)
            {
                for (int y = 0; y < fpY; y++)
                {
                    Vector2Int pos = new Vector2Int(unit.GridPosition.x + x, unit.GridPosition.y + y);
                    if (!IsWithinBounds(pos) || IsOccupied(pos))
                    {
                        return false;
                    }
                }
            }

            // 2. Register all cells to this unit
            for (int x = 0; x < fpX; x++)
            {
                for (int y = 0; y < fpY; y++)
                {
                    Vector2Int pos = new Vector2Int(unit.GridPosition.x + x, unit.GridPosition.y + y);
                    gridCells[pos] = unit;
                }
            }

            if (!distinctUnits.Contains(unit))
            {
                distinctUnits.Add(unit);
            }
            return true;
        }

        public bool UnregisterUnit(Vector2Int position, out PlacedUnitInstance removedUnit)
        {
            if (gridCells.TryGetValue(position, out removedUnit) && removedUnit != null)
            {
                int fpX = removedUnit.Definition != null ? Mathf.Max(1, removedUnit.Definition.Footprint.x) : 1;
                int fpY = removedUnit.Definition != null ? Mathf.Max(1, removedUnit.Definition.Footprint.y) : 1;

                for (int x = 0; x < fpX; x++)
                {
                    for (int y = 0; y < fpY; y++)
                    {
                        Vector2Int pos = new Vector2Int(removedUnit.GridPosition.x + x, removedUnit.GridPosition.y + y);
                        gridCells.Remove(pos);
                    }
                }

                distinctUnits.Remove(removedUnit);
                return true;
            }

            removedUnit = null;
            return false;
        }

        public IReadOnlyCollection<PlacedUnitInstance> GetAllPlacedUnits()
        {
            return distinctUnits;
        }

        public void Clear()
        {
            gridCells.Clear();
            distinctUnits.Clear();
        }
    }
}
