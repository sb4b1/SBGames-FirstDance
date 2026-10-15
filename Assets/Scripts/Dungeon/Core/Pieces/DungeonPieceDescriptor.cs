using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Pure C# descriptor of an authored Dungeon Piece filling one macro cell.
    /// Zero UnityEngine references.
    /// </summary>
    public class DungeonPieceDescriptor
    {
        public string PieceId { get; set; }
        public DungeonRoomType RoomType { get; set; }
        public OpeningFlags Openings { get; set; }
        public bool IsRotatable { get; set; }
        public float Weight { get; set; } = 1.0f;
        public int MinCountPerDungeon { get; set; } = 0;
        public int MaxCountPerDungeon { get; set; } = 0; // 0 = unlimited

        /// <summary>
        /// Macro-cell footprint (strictly 1x1 in v1).
        /// </summary>
        public GridCoord Footprint { get; set; } = new GridCoord(1, 1);

        /// <summary>
        /// Piece-local coordinates of buildable placement cells.
        /// (0,0) is the bottom-left corner of the piece.
        /// </summary>
        public HashSet<GridCoord> BuildableMask { get; set; } = new HashSet<GridCoord>();

        /// <summary>
        /// True if the piece sprite has directional details (e.g. shadows, stairs, lighting)
        /// that would look abnormal when rotated.
        /// </summary>
        public bool HasDirectionalArt { get; set; } = false;

        /// <summary>
        /// Number of authored entry anchors (required >= 1 for Entrance pieces).
        /// </summary>
        public int EntryAnchorCount { get; set; } = 0;

        /// <summary>
        /// Piece-local coordinates containing Ground tiles.
        /// </summary>
        public HashSet<GridCoord> GroundCells { get; set; } = new HashSet<GridCoord>();

        public DungeonPieceDescriptor() { }

        public DungeonPieceDescriptor(
            string pieceId,
            DungeonRoomType roomType,
            OpeningFlags openings,
            bool isRotatable = false,
            float weight = 1.0f,
            int minCount = 0,
            int maxCount = 0)
        {
            PieceId = pieceId;
            RoomType = roomType;
            Openings = openings;
            IsRotatable = isRotatable;
            Weight = Math.Max(0.01f, weight);
            MinCountPerDungeon = Math.Max(0, minCount);
            MaxCountPerDungeon = Math.Max(0, maxCount);
            Footprint = new GridCoord(1, 1);
            BuildableMask = new HashSet<GridCoord>();
        }

        /// <summary>
        /// Returns the piece's openings rotated by k quarter turns clockwise.
        /// If the piece is not rotatable and k != 0, returns the unrotated openings.
        /// </summary>
        public OpeningFlags GetRotatedOpenings(int quarterTurns)
        {
            if (!IsRotatable || quarterTurns == 0) return Openings;
            return Openings.Rotate(quarterTurns);
        }

        /// <summary>
        /// Rotates a local coordinate within a square cell of size S clockwise by k quarter turns.
        /// </summary>
        public static GridCoord RotateLocalCoord(GridCoord coord, int quarterTurns, int cellSize)
        {
            int k = ((quarterTurns % 4) + 4) % 4;
            int x = coord.X;
            int y = coord.Y;
            int sMinus1 = cellSize - 1;

            switch (k)
            {
                case 1:  return new GridCoord(y, sMinus1 - x);
                case 2:  return new GridCoord(sMinus1 - x, sMinus1 - y);
                case 3:  return new GridCoord(sMinus1 - y, x);
                default: return new GridCoord(x, y);
            }
        }

        /// <summary>
        /// Computes the piece-local buildable mask rotated by k quarter turns.
        /// Requires square cell (cellSize = roomWidth == roomHeight).
        /// </summary>
        public HashSet<GridCoord> GetRotatedBuildableMask(int quarterTurns, int cellSize)
        {
            if (BuildableMask == null || BuildableMask.Count == 0)
            {
                return new HashSet<GridCoord>();
            }

            int k = ((quarterTurns % 4) + 4) % 4;
            if (!IsRotatable || k == 0)
            {
                return new HashSet<GridCoord>(BuildableMask);
            }

            var rotated = new HashSet<GridCoord>();
            foreach (var coord in BuildableMask)
            {
                rotated.Add(RotateLocalCoord(coord, k, cellSize));
            }
            return rotated;
        }

        /// <summary>
        /// Computes the piece-local ground cells rotated by k quarter turns clockwise.
        /// Requires square cell (cellSize = roomWidth == roomHeight).
        /// </summary>
        public HashSet<GridCoord> GetRotatedGroundCells(int quarterTurns, int cellSize)
        {
            if (GroundCells == null || GroundCells.Count == 0)
            {
                return new HashSet<GridCoord>();
            }

            int k = ((quarterTurns % 4) + 4) % 4;
            if (!IsRotatable || k == 0)
            {
                return new HashSet<GridCoord>(GroundCells);
            }

            var rotated = new HashSet<GridCoord>(GroundCells.Count);
            foreach (var coord in GroundCells)
            {
                rotated.Add(RotateLocalCoord(coord, k, cellSize));
            }
            return rotated;
        }
    }
}
