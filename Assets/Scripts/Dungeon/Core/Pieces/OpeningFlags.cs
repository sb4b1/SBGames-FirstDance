using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Bitmask representing doorway openings along cardinal directions on a Dungeon Piece.
    /// Pure C#, zero UnityEngine references.
    /// </summary>
    [Flags]
    public enum OpeningFlags
    {
        None  = 0,
        North = 1 << 0, // 1
        East  = 1 << 1, // 2
        South = 1 << 2, // 4
        West  = 1 << 3, // 8
        All   = North | East | South | West // 15
    }

    public static class OpeningFlagsExtensions
    {
        public static OpeningFlags ToOpeningFlag(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North: return OpeningFlags.North;
                case CardinalDirection.East:  return OpeningFlags.East;
                case CardinalDirection.South: return OpeningFlags.South;
                case CardinalDirection.West:  return OpeningFlags.West;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static bool ContainsDirection(this OpeningFlags flags, CardinalDirection direction)
        {
            return (flags & direction.ToOpeningFlag()) != 0;
        }

        /// <summary>
        /// Rotates the opening bitmask clockwise by k quarter turns (90-degree steps).
        /// Mapping: d in (0..3) maps to (d + k) mod 4.
        /// </summary>
        public static OpeningFlags Rotate(this OpeningFlags flags, int quarterTurns)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            if (k == 0 || flags == OpeningFlags.None || flags == OpeningFlags.All)
            {
                return flags;
            }

            OpeningFlags rotated = OpeningFlags.None;
            for (int d = 0; d < 4; d++)
            {
                var dir = (CardinalDirection)d;
                if (flags.ContainsDirection(dir))
                {
                    var rotatedDir = (CardinalDirection)((d + k) % 4);
                    rotated |= rotatedDir.ToOpeningFlag();
                }
            }

            return rotated;
        }

        public static int CountOpenings(this OpeningFlags flags)
        {
            int count = 0;
            if ((flags & OpeningFlags.North) != 0) count++;
            if ((flags & OpeningFlags.East)  != 0) count++;
            if ((flags & OpeningFlags.South) != 0) count++;
            if ((flags & OpeningFlags.West)  != 0) count++;
            return count;
        }

        public static List<CardinalDirection> ToDirections(this OpeningFlags flags)
        {
            var list = new List<CardinalDirection>(4);
            if ((flags & OpeningFlags.North) != 0) list.Add(CardinalDirection.North);
            if ((flags & OpeningFlags.East)  != 0) list.Add(CardinalDirection.East);
            if ((flags & OpeningFlags.South) != 0) list.Add(CardinalDirection.South);
            if ((flags & OpeningFlags.West)  != 0) list.Add(CardinalDirection.West);
            return list;
        }

        public static OpeningFlags FromDirections(IEnumerable<CardinalDirection> directions)
        {
            if (directions == null) return OpeningFlags.None;
            OpeningFlags flags = OpeningFlags.None;
            foreach (var dir in directions)
            {
                flags |= dir.ToOpeningFlag();
            }
            return flags;
        }
    }
}
