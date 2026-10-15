using System;

namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Type of room within the generated dungeon layout.
    /// </summary>
    public enum DungeonRoomType
    {
        Entrance = 0,
        Normal = 1,
        SafeRoom = 2,
        Boss = 3
    }

    /// <summary>
    /// Cardinal doorway directions on the macro room grid.
    /// </summary>
    public enum CardinalDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    /// <summary>
    /// Pure C# integer coordinate pair for macro room-grid and tile-grid positioning.
    /// Zero UnityEngine dependencies.
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridCoord Offset(int dx, int dy) => new GridCoord(X + dx, Y + dy);

        public GridCoord Offset(CardinalDirection direction, int step = 1)
        {
            var (dx, dy) = direction.ToOffset();
            return new GridCoord(X + (dx * step), Y + (dy * step));
        }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(GridCoord left, GridCoord right) => left.Equals(right);
        public static bool operator !=(GridCoord left, GridCoord right) => !left.Equals(right);
    }

    /// <summary>
    /// Utility extensions for CardinalDirection.
    /// </summary>
    public static class CardinalDirectionExtensions
    {
        public static readonly CardinalDirection[] All = new[]
        {
            CardinalDirection.North,
            CardinalDirection.East,
            CardinalDirection.South,
            CardinalDirection.West
        };

        public static CardinalDirection GetOpposite(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North: return CardinalDirection.South;
                case CardinalDirection.South: return CardinalDirection.North;
                case CardinalDirection.East:  return CardinalDirection.West;
                case CardinalDirection.West:  return CardinalDirection.East;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static (int dx, int dy) ToOffset(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North: return (0, 1);
                case CardinalDirection.East:  return (1, 0);
                case CardinalDirection.South: return (0, -1);
                case CardinalDirection.West:  return (-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
