using System;
using System.Collections.Generic;
using UnityEngine;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Markers;

namespace SBGames.Dungeon.Unity.Pieces
{
    /// <summary>
    /// Component placed on a Dungeon Piece prefab. Serves as the source of truth for
    /// room type, openings, buildable placement cells, anchors, and room volumes.
    /// Thin Unity wrapper around pure C# DungeonPieceDescriptor.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SBGames/Dungeon/Dungeon Piece")]
    public class DungeonPiece : MonoBehaviour
    {
        [Header("Identity & Type")]
        [SerializeField] private string pieceId = "piece_01";
        [SerializeField] private DungeonRoomType roomType = DungeonRoomType.Normal;

        [Header("Doorway Openings")]
        [SerializeField] private OpeningFlags openings = OpeningFlags.North;
        [SerializeField] private bool isRotatable = true;
        [SerializeField] private bool hasDirectionalArt = false;

        [Header("Generation Weight & Limits")]
        [SerializeField, Min(0.01f)] private float weight = 1.0f;
        [SerializeField, Min(0)] private int minCountPerDungeon = 0;
        [SerializeField, Min(0)] private int maxCountPerDungeon = 0; // 0 = unlimited

        [Header("Footprint & Grid")]
        [SerializeField] private Vector2Int footprint = Vector2Int.one; // strictly 1x1 in v1

        [Header("Tilemaps")]
        [Tooltip("Ground tilemap defining walkable floor tiles.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap groundTilemap;
        [Tooltip("Walls tilemap defining impassable obstacle tiles.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap wallsTilemap;
        [Tooltip("Decoration tilemap for non-colliding decor.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap decorationTilemap;

        [Header("Buildable Placement Mask")]
        [Tooltip("Piece-local grid coordinates (0,0 to cellWidth-1, cellHeight-1) where DM is permitted to place defenders/traps.")]
        [SerializeField] private List<Vector2Int> buildableCells = new List<Vector2Int>();

        [Header("Anchors & Sockets")]
        [SerializeField] private List<Transform> entryAnchors = new List<Transform>();
        [SerializeField] private List<Transform> exitAnchors = new List<Transform>();

        [Tooltip("Reserved for future loot/hazard phases.")]
        [SerializeField] private List<Transform> hazardAnchors = new List<Transform>();
        [SerializeField] private List<Transform> lootAnchors = new List<Transform>();

        [Header("Room Volumes")]
        [Tooltip("One or more RoomVolume components defining physical detection boundaries (several allowed for irregular shapes).")]
        [SerializeField] private List<RoomVolume> roomVolumes = new List<RoomVolume>();

        public string PieceId
        {
            get => pieceId;
            set => pieceId = value;
        }

        public DungeonRoomType RoomType
        {
            get => roomType;
            set => roomType = value;
        }

        public OpeningFlags Openings
        {
            get => openings;
            set => openings = value;
        }

        public bool IsRotatable
        {
            get => isRotatable;
            set => isRotatable = value;
        }

        public bool HasDirectionalArt
        {
            get => hasDirectionalArt;
            set => hasDirectionalArt = value;
        }

        public float Weight
        {
            get => weight;
            set => weight = Mathf.Max(0.01f, value);
        }

        public int MinCountPerDungeon
        {
            get => minCountPerDungeon;
            set => minCountPerDungeon = Mathf.Max(0, value);
        }

        public int MaxCountPerDungeon
        {
            get => maxCountPerDungeon;
            set => maxCountPerDungeon = Mathf.Max(0, value);
        }

        public Vector2Int Footprint
        {
            get => footprint;
            set => footprint = value;
        }

        public UnityEngine.Tilemaps.Tilemap GroundTilemap
        {
            get => groundTilemap;
            set => groundTilemap = value;
        }

        public UnityEngine.Tilemaps.Tilemap WallsTilemap
        {
            get => wallsTilemap;
            set => wallsTilemap = value;
        }

        public UnityEngine.Tilemaps.Tilemap DecorationTilemap
        {
            get => decorationTilemap;
            set => decorationTilemap = value;
        }

        public IReadOnlyList<Vector2Int> BuildableCells => buildableCells;
        public IReadOnlyList<Transform> EntryAnchors => entryAnchors;
        public IReadOnlyList<Transform> ExitAnchors => exitAnchors;
        public IReadOnlyList<Transform> HazardAnchors => hazardAnchors;
        public IReadOnlyList<Transform> LootAnchors => lootAnchors;
        public IReadOnlyList<RoomVolume> RoomVolumes => roomVolumes;

        private void Reset()
        {
            AutoFindTilemaps();
        }

        public void AutoFindTilemaps()
        {
            var tilemaps = GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                string tmName = tm.gameObject.name;
                if (string.Equals(tmName, "Ground", StringComparison.OrdinalIgnoreCase))
                {
                    groundTilemap = tm;
                }
                else if (string.Equals(tmName, "Walls", StringComparison.OrdinalIgnoreCase))
                {
                    wallsTilemap = tm;
                }
                else if (string.Equals(tmName, "Decoration", StringComparison.OrdinalIgnoreCase))
                {
                    decorationTilemap = tm;
                }
            }
        }

        public void AddBuildableCell(Vector2Int localCoord)
        {
            if (!buildableCells.Contains(localCoord))
            {
                buildableCells.Add(localCoord);
            }
        }

        public void RemoveBuildableCell(Vector2Int localCoord)
        {
            buildableCells.Remove(localCoord);
        }

        public void ClearBuildableCells()
        {
            buildableCells.Clear();
        }

        public bool IsCellBuildable(Vector2Int localCoord)
        {
            return buildableCells.Contains(localCoord);
        }

        public void FillBuildableBox(int cellWidth, int cellHeight, int margin = 1)
        {
            buildableCells.Clear();
            for (int x = margin; x < cellWidth - margin; x++)
            {
                for (int y = margin; y < cellHeight - margin; y++)
                {
                    buildableCells.Add(new Vector2Int(x, y));
                }
            }
        }

        /// <summary>
        /// Returns the set of piece-local grid coordinates containing Ground tiles.
        /// Evaluates tile positions within 0..cellWidth-1 and 0..cellHeight-1.
        /// </summary>
        public HashSet<GridCoord> GetGroundCellCoords(int cellWidth = 16, int cellHeight = 16)
        {
            var set = new HashSet<GridCoord>();
            if (groundTilemap == null)
            {
                AutoFindTilemaps();
            }

            if (groundTilemap == null) return set;

            bool hasDirectTiles = false;
            foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.HasTile(pos) && pos.x >= 0 && pos.x < cellWidth && pos.y >= 0 && pos.y < cellHeight)
                {
                    hasDirectTiles = true;
                    break;
                }
            }

            int offsetX = 0;
            int offsetY = 0;
            if (!hasDirectTiles)
            {
                offsetX = cellWidth / 2;
                offsetY = cellHeight / 2;
            }

            foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.HasTile(pos))
                {
                    int gx = pos.x + offsetX;
                    int gy = pos.y + offsetY;
                    if (gx >= 0 && gx < cellWidth && gy >= 0 && gy < cellHeight)
                    {
                        set.Add(new GridCoord(gx, gy));
                    }
                }
            }

            return set;
        }

        /// <summary>
        /// Generates the buildable placement mask: Ground cells, minus cells covered by Walls,
        /// minus an edge margin, minus cells within openingClearance of each declared Opening.
        /// The resulting mask is stored in buildableCells and remains hand-editable.
        /// </summary>
        public void GenerateBuildableMaskFromGround(
            int margin = 1,
            int openingClearance = 2,
            int cellWidth = 16,
            int cellHeight = 16,
            int openingWidth = 2)
        {
            if (groundTilemap == null)
            {
                AutoFindTilemaps();
            }

            if (groundTilemap == null)
            {
                FillBuildableBox(cellWidth, cellHeight, margin);
                return;
            }

            var groundSet = GetGroundCellCoords(cellWidth, cellHeight);
            if (groundSet.Count == 0)
            {
                FillBuildableBox(cellWidth, cellHeight, margin);
                return;
            }

            // Find wall cells
            var wallSet = new HashSet<GridCoord>();
            if (wallsTilemap != null)
            {
                bool hasDirectWalls = false;
                foreach (var pos in wallsTilemap.cellBounds.allPositionsWithin)
                {
                    if (wallsTilemap.HasTile(pos) && pos.x >= 0 && pos.x < cellWidth && pos.y >= 0 && pos.y < cellHeight)
                    {
                        hasDirectWalls = true;
                        break;
                    }
                }

                int offX = hasDirectWalls ? 0 : (cellWidth / 2);
                int offY = hasDirectWalls ? 0 : (cellHeight / 2);

                foreach (var pos in wallsTilemap.cellBounds.allPositionsWithin)
                {
                    if (wallsTilemap.HasTile(pos))
                    {
                        int wx = pos.x + offX;
                        int wy = pos.y + offY;
                        if (wx >= 0 && wx < cellWidth && wy >= 0 && wy < cellHeight)
                        {
                            wallSet.Add(new GridCoord(wx, wy));
                        }
                    }
                }
            }

            // Calculate opening clearance cells
            var clearanceSet = new HashSet<GridCoord>();
            if (OpeningGeometry.CanCenterOpening(cellWidth, cellHeight, openingWidth))
            {
                for (int d = 0; d < 4; d++)
                {
                    var dir = (CardinalDirection)d;
                    if (openings.ContainsDirection(dir))
                    {
                        var edgeCoords = OpeningGeometry.GetOpeningEdgeCoords(cellWidth, cellHeight, openingWidth, dir);
                        foreach (var edge in edgeCoords)
                        {
                            for (int dx = -openingClearance + 1; dx < openingClearance; dx++)
                            {
                                for (int dy = -openingClearance + 1; dy < openingClearance; dy++)
                                {
                                    int cx = edge.X + dx;
                                    int cy = edge.Y + dy;
                                    if (cx >= 0 && cx < cellWidth && cy >= 0 && cy < cellHeight)
                                    {
                                        clearanceSet.Add(new GridCoord(cx, cy));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            buildableCells.Clear();
            foreach (var cell in groundSet)
            {
                // Minus walls
                if (wallSet.Contains(cell)) continue;

                // Minus edge margin
                if (cell.X < margin || cell.X >= cellWidth - margin || cell.Y < margin || cell.Y >= cellHeight - margin)
                {
                    continue;
                }

                // Minus opening clearance
                if (clearanceSet.Contains(cell)) continue;

                buildableCells.Add(new Vector2Int(cell.X, cell.Y));
            }
        }

        /// <summary>
        /// Converts this MonoBehaviour piece into a pure C# DungeonPieceDescriptor.
        /// </summary>
        public DungeonPieceDescriptor ToDescriptor()
        {
            var mask = new HashSet<GridCoord>();
            for (int i = 0; i < buildableCells.Count; i++)
            {
                mask.Add(new GridCoord(buildableCells[i].x, buildableCells[i].y));
            }

            int validEntryAnchors = 0;
            if (entryAnchors != null)
            {
                for (int i = 0; i < entryAnchors.Count; i++)
                {
                    if (entryAnchors[i] != null) validEntryAnchors++;
                }
            }

            return new DungeonPieceDescriptor
            {
                PieceId = string.IsNullOrEmpty(pieceId) ? gameObject.name : pieceId,
                RoomType = roomType,
                Openings = openings,
                IsRotatable = isRotatable,
                Weight = weight,
                MinCountPerDungeon = minCountPerDungeon,
                MaxCountPerDungeon = maxCountPerDungeon,
                Footprint = new GridCoord(footprint.x, footprint.y),
                BuildableMask = mask,
                HasDirectionalArt = hasDirectionalArt,
                EntryAnchorCount = validEntryAnchors,
                GroundCells = GetGroundCellCoords()
            };
        }

        /// <summary>
        /// Synchronizes this component from a pure C# DungeonPieceDescriptor.
        /// </summary>
        public void LoadFromDescriptor(DungeonPieceDescriptor descriptor)
        {
            if (descriptor == null) return;
            pieceId = descriptor.PieceId;
            roomType = descriptor.RoomType;
            openings = descriptor.Openings;
            isRotatable = descriptor.IsRotatable;
            weight = descriptor.Weight;
            minCountPerDungeon = descriptor.MinCountPerDungeon;
            maxCountPerDungeon = descriptor.MaxCountPerDungeon;
            footprint = new Vector2Int(descriptor.Footprint.X, descriptor.Footprint.Y);
            hasDirectionalArt = descriptor.HasDirectionalArt;

            buildableCells.Clear();
            if (descriptor.BuildableMask != null)
            {
                foreach (var c in descriptor.BuildableMask)
                {
                    buildableCells.Add(new Vector2Int(c.X, c.Y));
                }
            }
        }

        /// <summary>
        /// Ensures at least one RoomVolume exists on this prefab.
        /// </summary>
        public void EnsureRoomVolume(Vector3 center, Vector3 size)
        {
            roomVolumes.RemoveAll(r => r == null);
            if (roomVolumes.Count == 0)
            {
                var rv = GetComponent<RoomVolume>();
                if (rv == null) rv = gameObject.AddComponent<RoomVolume>();
                rv.SetBounds(new Bounds(center, size));
                roomVolumes.Add(rv);
            }
        }
    }
}
