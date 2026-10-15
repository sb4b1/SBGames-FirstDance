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

        public PieceRole Role { get; set; } = PieceRole.Room;
        public int CellWidth { get; set; } = 0;
        public int CellHeight { get; set; } = 0;
        public List<PieceOpening> OpeningsList { get; set; } = new List<PieceOpening>();
        public HashSet<GridCoord> OccupiedLocalTiles { get; set; } = new HashSet<GridCoord>();

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
            Role = PieceRole.Room;
            Openings = openings;
            IsRotatable = isRotatable;
            Weight = Math.Max(0.01f, weight);
            MinCountPerDungeon = Math.Max(0, minCount);
            MaxCountPerDungeon = Math.Max(0, maxCount);
            Footprint = new GridCoord(1, 1);
            BuildableMask = new HashSet<GridCoord>();
            OpeningsList = new List<PieceOpening>();
            OccupiedLocalTiles = new HashSet<GridCoord>();
        }

        public DungeonPieceDescriptor(
            string pieceId,
            PieceRole role,
            DungeonRoomType roomType,
            int cellWidth,
            int cellHeight,
            IEnumerable<PieceOpening> openings,
            bool isRotatable = false,
            float weight = 1.0f,
            int minCount = 0,
            int maxCount = 0)
        {
            PieceId = pieceId;
            Role = role;
            RoomType = roomType;
            CellWidth = Math.Max(1, cellWidth);
            CellHeight = Math.Max(1, cellHeight);
            OpeningsList = openings != null ? new List<PieceOpening>(openings) : new List<PieceOpening>();
            Openings = OpeningGeometry.ToOpeningFlags(OpeningsList);
            IsRotatable = isRotatable;
            Weight = Math.Max(0.01f, weight);
            MinCountPerDungeon = Math.Max(0, minCount);
            MaxCountPerDungeon = Math.Max(0, maxCount);
            Footprint = new GridCoord(1, 1);
            BuildableMask = new HashSet<GridCoord>();
            OccupiedLocalTiles = new HashSet<GridCoord>();
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
        /// Returns the piece's freeform openings rotated by k quarter turns clockwise.
        /// Swaps dimensions on odd quarter-turns.
        /// </summary>
        public List<PieceOpening> GetRotatedOpeningsList(int quarterTurns, int cellW = 0, int cellH = 0)
        {
            if (OpeningsList == null || OpeningsList.Count == 0) return new List<PieceOpening>();
            int k = ((quarterTurns % 4) + 4) % 4;
            if (!IsRotatable || k == 0) return new List<PieceOpening>(OpeningsList);

            int w = cellW > 0 ? cellW : (CellWidth > 0 ? CellWidth : 16);
            int h = cellH > 0 ? cellH : (CellHeight > 0 ? CellHeight : 16);

            var rotated = new List<PieceOpening>(OpeningsList.Count);
            foreach (var o in OpeningsList)
            {
                rotated.Add(o.RotateClockwise(w, h, k));
            }
            return rotated;
        }

        /// <summary>
        /// Returns the piece's occupied local tiles rotated by k quarter turns clockwise.
        /// Swaps dimensions on odd quarter-turns.
        /// </summary>
        public HashSet<GridCoord> GetRotatedOccupiedTiles(int quarterTurns, int cellW = 0, int cellH = 0)
        {
            if (OccupiedLocalTiles == null || OccupiedLocalTiles.Count == 0) return new HashSet<GridCoord>();
            int k = ((quarterTurns % 4) + 4) % 4;
            if (!IsRotatable || k == 0) return new HashSet<GridCoord>(OccupiedLocalTiles);

            int w = cellW > 0 ? cellW : (CellWidth > 0 ? CellWidth : 16);
            int h = cellH > 0 ? cellH : (CellHeight > 0 ? CellHeight : 16);

            var rotated = new HashSet<GridCoord>(OccupiedLocalTiles.Count);
            foreach (var c in OccupiedLocalTiles)
            {
                rotated.Add(OpeningGeometry.RotateCoord(c, k, w, h));
            }
            return rotated;
        }

        /// <summary>
        /// Rotates a local coordinate within a square cell of size S clockwise by k quarter turns.
        /// </summary>
        public static GridCoord RotateLocalCoord(GridCoord coord, int quarterTurns, int cellSize)
        {
            return OpeningGeometry.RotateCoord(coord, quarterTurns, cellSize, cellSize);
        }

        /// <summary>
        /// Rotates a local coordinate within a rectangle of dimensions (cellW, cellH) clockwise by k quarter turns.
        /// </summary>
        public static GridCoord RotateLocalCoord(GridCoord coord, int quarterTurns, int cellW, int cellH)
        {
            return OpeningGeometry.RotateCoord(coord, quarterTurns, cellW, cellH);
        }

        /// <summary>
        /// Computes the piece-local buildable mask rotated by k quarter turns.
        /// Uses descriptor CellWidth and CellHeight (fallback 16).
        /// </summary>
        public HashSet<GridCoord> GetRotatedBuildableMask(int quarterTurns)
        {
            int w = CellWidth > 0 ? CellWidth : 16;
            int h = CellHeight > 0 ? CellHeight : 16;
            return GetRotatedBuildableMask(quarterTurns, w, h);
        }

        /// <summary>
        /// Computes the piece-local buildable mask rotated by k quarter turns.
        /// </summary>
        public HashSet<GridCoord> GetRotatedBuildableMask(int quarterTurns, int cellW, int cellH)
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

            var rotated = new HashSet<GridCoord>(BuildableMask.Count);
            foreach (var coord in BuildableMask)
            {
                rotated.Add(OpeningGeometry.RotateCoord(coord, k, cellW, cellH));
            }
            return rotated;
        }

        /// <summary>
        /// Computes the piece-local buildable mask rotated by k quarter turns.
        /// Requires square cell (cellSize = roomWidth == roomHeight).
        /// </summary>
        public HashSet<GridCoord> GetRotatedBuildableMask(int quarterTurns, int cellSize)
        {
            return GetRotatedBuildableMask(quarterTurns, cellSize, cellSize);
        }

        /// <summary>
        /// Computes the piece-local ground cells rotated by k quarter turns clockwise.
        /// Uses descriptor CellWidth and CellHeight (fallback 16).
        /// </summary>
        public HashSet<GridCoord> GetRotatedGroundCells(int quarterTurns)
        {
            int w = CellWidth > 0 ? CellWidth : 16;
            int h = CellHeight > 0 ? CellHeight : 16;
            return GetRotatedGroundCells(quarterTurns, w, h);
        }

        /// <summary>
        /// Computes the piece-local ground cells rotated by k quarter turns clockwise.
        /// </summary>
        public HashSet<GridCoord> GetRotatedGroundCells(int quarterTurns, int cellW, int cellH)
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
                rotated.Add(OpeningGeometry.RotateCoord(coord, k, cellW, cellH));
            }
            return rotated;
        }

        /// <summary>
        /// Computes the piece-local ground cells rotated by k quarter turns clockwise.
        /// Requires square cell (cellSize = roomWidth == roomHeight).
        /// </summary>
        public HashSet<GridCoord> GetRotatedGroundCells(int quarterTurns, int cellSize)
        {
            return GetRotatedGroundCells(quarterTurns, cellSize, cellSize);
        }
    }
}
