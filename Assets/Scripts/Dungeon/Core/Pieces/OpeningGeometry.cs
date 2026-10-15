using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Pure C# opening geometry and edge coordinate calculations.
    /// Zero UnityEngine references.
    /// </summary>
    public static class OpeningGeometry
    {
        /// <summary>
        /// Checks if an opening of width openingWidth can be symmetrically centered on a cell of dimensions cellW x cellH.
        /// Requires (cellW - openingWidth) and (cellH - openingWidth) to be non-negative and even.
        /// </summary>
        public static bool CanCenterOpening(int cellW, int cellH, int openingWidth)
        {
            if (openingWidth <= 0) return false;
            if (openingWidth > cellW || openingWidth > cellH) return false;
            return ((cellW - openingWidth) % 2 == 0) && ((cellH - openingWidth) % 2 == 0);
        }

        /// <summary>
        /// Returns the piece-local edge tile coordinates for a standard centered opening of width openingWidth.
        /// Coordinate (0,0) is the bottom-left corner of the piece.
        /// </summary>
        public static List<GridCoord> GetOpeningEdgeCoords(int cellW, int cellH, int openingWidth, CardinalDirection direction)
        {
            var coords = new List<GridCoord>(openingWidth);
            if (openingWidth <= 0 || cellW <= 0 || cellH <= 0)
            {
                return coords;
            }

            int startX = (cellW - openingWidth) / 2;
            int startY = (cellH - openingWidth) / 2;

            switch (direction)
            {
                case CardinalDirection.North:
                    for (int i = 0; i < openingWidth; i++)
                    {
                        coords.Add(new GridCoord(startX + i, cellH - 1));
                    }
                    break;

                case CardinalDirection.South:
                    for (int i = 0; i < openingWidth; i++)
                    {
                        coords.Add(new GridCoord(startX + i, 0));
                    }
                    break;

                case CardinalDirection.West:
                    for (int i = 0; i < openingWidth; i++)
                    {
                        coords.Add(new GridCoord(0, startY + i));
                    }
                    break;

                case CardinalDirection.East:
                    for (int i = 0; i < openingWidth; i++)
                    {
                        coords.Add(new GridCoord(cellW - 1, startY + i));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }

            return coords;
        }

        /// <summary>
        /// Rotates a cardinal direction clockwise by k quarter turns.
        /// Consistent with OpeningFlags.Rotate.
        /// </summary>
        public static CardinalDirection RotateDirection(CardinalDirection direction, int quarterTurns)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            return (CardinalDirection)(((int)direction + k) % 4);
        }

        /// <summary>
        /// Rotates a coordinate within a square cell of size cellSize clockwise by k quarter turns.
        /// Consistent with DungeonPieceDescriptor.RotateLocalCoord.
        /// </summary>
        public static GridCoord RotateCoord(GridCoord coord, int quarterTurns, int cellSize)
        {
            return DungeonPieceDescriptor.RotateLocalCoord(coord, quarterTurns, cellSize);
        }

        /// <summary>
        /// Rotates an enumerable of coordinates within a square cell of size cellSize clockwise by k quarter turns.
        /// </summary>
        public static List<GridCoord> RotateCoords(IEnumerable<GridCoord> coords, int quarterTurns, int cellSize)
        {
            var list = new List<GridCoord>();
            if (coords == null) return list;
            foreach (var c in coords)
            {
                list.Add(RotateCoord(c, quarterTurns, cellSize));
            }
            return list;
        }
    }
}
