using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Represents a 2-tile (or width-tile) doorway opening socket along a piece's wall.
    /// AnchorTile: start tile of the opening along the wall (smallest X for N/S, smallest Y for E/W).
    /// Direction: outward facing cardinal direction (Normal).
    /// Width: number of doorway tiles (default 2).
    /// Zero UnityEngine dependencies.
    /// </summary>
    [Serializable]
    public struct PieceOpening : IEquatable<PieceOpening>
    {
        public GridCoord AnchorTile;
        public CardinalDirection Direction;
        public byte Width;

        public PieceOpening(GridCoord anchorTile, CardinalDirection direction, byte width = 2)
        {
            AnchorTile = anchorTile;
            Direction = direction;
            Width = Math.Max((byte)1, width);
        }

        public PieceOpening(int x, int y, CardinalDirection direction, byte width = 2)
            : this(new GridCoord(x, y), direction, width)
        {
        }

        /// <summary>
        /// Returns the exact edge tile coordinates (GridCoord) occupied by this opening.
        /// North/South: AnchorTile.Offset(i, 0)
        /// East/West: AnchorTile.Offset(0, i)
        /// </summary>
        public List<GridCoord> GetEdgeTiles()
        {
            var tiles = new List<GridCoord>(Width);
            for (int i = 0; i < Width; i++)
            {
                if (Direction == CardinalDirection.North || Direction == CardinalDirection.South)
                {
                    tiles.Add(new GridCoord(AnchorTile.X + i, AnchorTile.Y));
                }
                else
                {
                    tiles.Add(new GridCoord(AnchorTile.X, AnchorTile.Y + i));
                }
            }
            return tiles;
        }

        /// <summary>
        /// Returns the adjacent outside connection tiles (AnchorTile + Normal(Direction)).
        /// </summary>
        public List<GridCoord> GetOutsideConnectionTiles()
        {
            var (dx, dy) = Direction.ToOffset();
            var tiles = new List<GridCoord>(Width);
            for (int i = 0; i < Width; i++)
            {
                if (Direction == CardinalDirection.North || Direction == CardinalDirection.South)
                {
                    tiles.Add(new GridCoord(AnchorTile.X + i + dx, AnchorTile.Y + dy));
                }
                else
                {
                    tiles.Add(new GridCoord(AnchorTile.X + dx, AnchorTile.Y + i + dy));
                }
            }
            return tiles;
        }

        /// <summary>
        /// Rotates the opening clockwise by k quarter turns on a piece of dimensions (cellW, cellH).
        /// Odd quarter-turns swap width and height accurately.
        /// </summary>
        public PieceOpening RotateClockwise(int cellW, int cellH, int quarterTurns)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            if (k == 0) return this;

            var cur = this;
            int curW = cellW;
            int curH = cellH;

            for (int step = 0; step < k; step++)
            {
                var nextDir = cur.Direction.RotateClockwise();
                GridCoord nextAnchor;
                switch (cur.Direction)
                {
                    case CardinalDirection.North:
                        nextAnchor = new GridCoord(curH - 1, curW - cur.Width - cur.AnchorTile.X);
                        break;
                    case CardinalDirection.East:
                        nextAnchor = new GridCoord(cur.AnchorTile.Y, 0);
                        break;
                    case CardinalDirection.South:
                        nextAnchor = new GridCoord(0, curW - cur.Width - cur.AnchorTile.X);
                        break;
                    case CardinalDirection.West:
                        nextAnchor = new GridCoord(cur.AnchorTile.Y, curW - 1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                cur = new PieceOpening(nextAnchor, nextDir, cur.Width);
                int temp = curW;
                curW = curH;
                curH = temp;
            }

            return cur;
        }

        /// <summary>
        /// Checks if two openings on the same piece overlap on any edge tile.
        /// </summary>
        public bool Overlaps(PieceOpening other)
        {
            var myTiles = GetEdgeTiles();
            var otherTiles = other.GetEdgeTiles();
            for (int i = 0; i < myTiles.Count; i++)
            {
                for (int j = 0; j < otherTiles.Count; j++)
                {
                    if (myTiles[i] == otherTiles[j]) return true;
                }
            }
            return false;
        }

        public bool Equals(PieceOpening other) =>
            AnchorTile == other.AnchorTile && Direction == other.Direction && Width == other.Width;

        public override bool Equals(object obj) => obj is PieceOpening other && Equals(other);

        public override int GetHashCode() => unchecked((AnchorTile.GetHashCode() * 397) ^ ((int)Direction * 31) ^ Width);

        public override string ToString() => $"Opening[{Direction} at {AnchorTile}, w={Width}]";

        public static bool operator ==(PieceOpening left, PieceOpening right) => left.Equals(right);
        public static bool operator !=(PieceOpening left, PieceOpening right) => !left.Equals(right);
    }
}
