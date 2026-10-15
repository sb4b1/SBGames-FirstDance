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
        /// Rotates a cardinal direction clockwise by 1 quarter turn (90 degrees).
        /// </summary>
        public static CardinalDirection RotateClockwise(this CardinalDirection direction)
        {
            return (CardinalDirection)(((int)direction + 1) % 4);
        }

        /// <summary>
        /// Gets the effective (width, height) after rotating a piece of dimensions (cellW, cellH) by k quarter turns.
        /// </summary>
        public static void GetEffectiveDimensions(int cellW, int cellH, int quarterTurns, out int effW, out int effH)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            if (k % 2 == 0)
            {
                effW = cellW;
                effH = cellH;
            }
            else
            {
                effW = cellH;
                effH = cellW;
            }
        }

        /// <summary>
        /// Rotates a coordinate within a rectangle of dimensions (cellW, cellH) clockwise by k quarter turns.
        /// Odd quarter-turns swap width and height accurately.
        /// </summary>
        public static GridCoord RotateCoord(GridCoord coord, int quarterTurns, int cellW, int cellH)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            if (k == 0) return coord;

            int x = coord.X;
            int y = coord.Y;
            int curW = cellW;
            int curH = cellH;

            for (int step = 0; step < k; step++)
            {
                int newX = y;
                int newY = curW - 1 - x;
                x = newX;
                y = newY;
                int temp = curW;
                curW = curH;
                curH = temp;
            }

            return new GridCoord(x, y);
        }

        /// <summary>
        /// Rotates a coordinate within a square cell of size cellSize clockwise by k quarter turns.
        /// Consistent with DungeonPieceDescriptor.RotateLocalCoord.
        /// </summary>
        public static GridCoord RotateCoord(GridCoord coord, int quarterTurns, int cellSize)
        {
            return RotateCoord(coord, quarterTurns, cellSize, cellSize);
        }

        /// <summary>
        /// Rotates an enumerable of coordinates within a rectangle (cellW, cellH) clockwise by k quarter turns.
        /// </summary>
        public static List<GridCoord> RotateCoords(IEnumerable<GridCoord> coords, int quarterTurns, int cellW, int cellH)
        {
            var list = new List<GridCoord>();
            if (coords == null) return list;
            foreach (var c in coords)
            {
                list.Add(RotateCoord(c, quarterTurns, cellW, cellH));
            }
            return list;
        }

        /// <summary>
        /// Rotates an enumerable of coordinates within a square cell of size cellSize clockwise by k quarter turns.
        /// </summary>
        public static List<GridCoord> RotateCoords(IEnumerable<GridCoord> coords, int quarterTurns, int cellSize)
        {
            return RotateCoords(coords, quarterTurns, cellSize, cellSize);
        }

        /// <summary>
        /// Computes the exact seam-snapped world tile origin for Piece B attaching via openingB to Piece A at originA via openingA.
        /// Requires openingB.Direction == Opposite(openingA.Direction).
        /// Formula: OriginB = OriginA + OpeningA.AnchorTile + Normal(OpeningA.Direction) - OpeningB.AnchorTile.
        /// </summary>
        public static GridCoord CalculateSnappedWorldOrigin(GridCoord originA, PieceOpening openingA, PieceOpening openingB)
        {
            var (dx, dy) = openingA.Direction.ToOffset();
            return new GridCoord(
                originA.X + openingA.AnchorTile.X + dx - openingB.AnchorTile.X,
                originA.Y + openingA.AnchorTile.Y + dy - openingB.AnchorTile.Y
            );
        }

        /// <summary>
        /// Derives a legacy OpeningFlags bitmask from a collection of PieceOpening sockets for backward compatibility.
        /// </summary>
        public static OpeningFlags ToOpeningFlags(IEnumerable<PieceOpening> openings)
        {
            if (openings == null) return OpeningFlags.None;
            OpeningFlags flags = OpeningFlags.None;
            foreach (var o in openings)
            {
                flags |= o.Direction.ToOpeningFlag();
            }
            return flags;
        }
    }
}
