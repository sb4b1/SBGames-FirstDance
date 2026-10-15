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
        [SerializeField] private PieceRole role = PieceRole.Room;
        [SerializeField] private DungeonRoomType roomType = DungeonRoomType.Normal;

        [Header("Doorway Openings")]
        [SerializeField] private OpeningFlags openings = OpeningFlags.North;
        [SerializeField] private List<PieceOpening> openingsList = new List<PieceOpening>();
        [SerializeField] private bool isRotatable = true;
        [SerializeField] private bool hasDirectionalArt = false;

        [Header("Generation Weight & Limits")]
        [SerializeField, Min(0.01f)] private float weight = 1.0f;
        [SerializeField, Min(0)] private int minCountPerDungeon = 0;
        [SerializeField, Min(0)] private int maxCountPerDungeon = 0; // 0 = unlimited

        [Header("Footprint & Grid")]
        [SerializeField] private Vector2Int cellSize = new Vector2Int(16, 16);
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

        public PieceRole Role
        {
            get => role;
            set => role = value;
        }

        public DungeonRoomType RoomType
        {
            get => roomType;
            set => roomType = value;
        }

        public Vector2Int CellSize
        {
            get => cellSize;
            set => cellSize = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
        }

        public OpeningFlags Openings
        {
            get => openingsList != null && openingsList.Count > 0 ? OpeningGeometry.ToOpeningFlags(openingsList) : openings;
            set => openings = value;
        }

        public List<PieceOpening> OpeningsList
        {
            get => openingsList;
            set => openingsList = value;
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
        public HashSet<GridCoord> GetGroundCellCoords(int cellWidth = 0, int cellHeight = 0)
        {
            int w = cellWidth > 0 ? cellWidth : cellSize.x;
            int h = cellHeight > 0 ? cellHeight : cellSize.y;
            var set = new HashSet<GridCoord>();
            if (groundTilemap == null)
            {
                AutoFindTilemaps();
            }

            if (groundTilemap == null) return set;

            bool hasDirectTiles = false;
            foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.HasTile(pos) && pos.x >= 0 && pos.x < w && pos.y >= 0 && pos.y < h)
                {
                    hasDirectTiles = true;
                    break;
                }
            }

            int offsetX = 0;
            int offsetY = 0;
            if (!hasDirectTiles)
            {
                offsetX = w / 2;
                offsetY = h / 2;
            }

            foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.HasTile(pos))
                {
                    int gx = pos.x + offsetX;
                    int gy = pos.y + offsetY;
                    if (gx >= 0 && gx < w && gy >= 0 && gy < h)
                    {
                        set.Add(new GridCoord(gx, gy));
                    }
                }
            }

            return set;
        }

        /// <summary>
        /// Returns the set of piece-local grid coordinates containing Wall obstacle tiles.
        /// </summary>
        public HashSet<GridCoord> GetWallCellCoords(int cellWidth = 0, int cellHeight = 0)
        {
            int w = cellWidth > 0 ? cellWidth : cellSize.x;
            int h = cellHeight > 0 ? cellHeight : cellSize.y;
            var set = new HashSet<GridCoord>();
            if (wallsTilemap == null)
            {
                AutoFindTilemaps();
            }

            if (wallsTilemap == null) return set;

            bool hasDirectWalls = false;
            foreach (var pos in wallsTilemap.cellBounds.allPositionsWithin)
            {
                if (wallsTilemap.HasTile(pos) && pos.x >= 0 && pos.x < w && pos.y >= 0 && pos.y < h)
                {
                    hasDirectWalls = true;
                    break;
                }
            }

            int offX = hasDirectWalls ? 0 : (w / 2);
            int offY = hasDirectWalls ? 0 : (h / 2);

            foreach (var pos in wallsTilemap.cellBounds.allPositionsWithin)
            {
                if (wallsTilemap.HasTile(pos))
                {
                    int wx = pos.x + offX;
                    int wy = pos.y + offY;
                    if (wx >= 0 && wx < w && wy >= 0 && wy < h)
                    {
                        set.Add(new GridCoord(wx, wy));
                    }
                }
            }

            return set;
        }

        /// <summary>
        /// Returns the set of all piece-local tiles occupied by either Ground or Walls.
        /// Used for exact zero-overlap collision checks.
        /// </summary>
        public HashSet<GridCoord> OccupiedLocalTiles
        {
            get
            {
                var occupied = GetGroundCellCoords(cellSize.x, cellSize.y);
                var walls = GetWallCellCoords(cellSize.x, cellSize.y);
                foreach (var w in walls)
                {
                    occupied.Add(w);
                }
                return occupied;
            }
        }

        /// <summary>
        /// Fits piece bounds to painted tiles, shifting tiles to start at (0, 0) if needed,
        /// updating cellSize and the room volume.
        /// </summary>
        public void FitBoundsToPaintedTiles()
        {
            AutoFindTilemaps();

            bool hasAnyTile = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            void CheckMap(UnityEngine.Tilemaps.Tilemap tm)
            {
                if (tm == null) return;
                tm.CompressBounds();
                var bounds = tm.cellBounds;
                foreach (var pos in bounds.allPositionsWithin)
                {
                    if (tm.HasTile(pos))
                    {
                        hasAnyTile = true;
                        if (pos.x < minX) minX = pos.x;
                        if (pos.y < minY) minY = pos.y;
                        if (pos.x > maxX) maxX = pos.x;
                        if (pos.y > maxY) maxY = pos.y;
                    }
                }
            }

            CheckMap(groundTilemap);
            CheckMap(wallsTilemap);
            CheckMap(decorationTilemap);

            if (!hasAnyTile) return;

            int shiftX = -minX;
            int shiftY = -minY;
            if (shiftX != 0 || shiftY != 0)
            {
                void ShiftMap(UnityEngine.Tilemaps.Tilemap tm)
                {
                    if (tm == null) return;
                    var tiles = new List<(Vector3Int pos, UnityEngine.Tilemaps.TileBase tile)>();
                    foreach (var pos in tm.cellBounds.allPositionsWithin)
                    {
                        if (tm.HasTile(pos))
                        {
                            tiles.Add((pos, tm.GetTile(pos)));
                        }
                    }
                    tm.ClearAllTiles();
                    foreach (var (pos, tile) in tiles)
                    {
                        tm.SetTile(new Vector3Int(pos.x + shiftX, pos.y + shiftY, pos.z), tile);
                    }
                    tm.CompressBounds();
                }

                ShiftMap(groundTilemap);
                ShiftMap(wallsTilemap);
                ShiftMap(decorationTilemap);

                for (int i = 0; i < buildableCells.Count; i++)
                {
                    buildableCells[i] = new Vector2Int(buildableCells[i].x + shiftX, buildableCells[i].y + shiftY);
                }

                if (openingsList != null)
                {
                    for (int i = 0; i < openingsList.Count; i++)
                    {
                        var op = openingsList[i];
                        openingsList[i] = new PieceOpening(
                            new GridCoord(op.AnchorTile.X + shiftX, op.AnchorTile.Y + shiftY),
                            op.Direction,
                            op.Width
                        );
                    }
                }

                var shiftWorld = new Vector3(shiftX, shiftY, 0);
                void ShiftAnchorList(List<Transform> list)
                {
                    if (list == null) return;
                    foreach (var t in list)
                    {
                        if (t != null) t.localPosition += shiftWorld;
                    }
                }
                ShiftAnchorList(entryAnchors);
                ShiftAnchorList(exitAnchors);
                ShiftAnchorList(hazardAnchors);
                ShiftAnchorList(lootAnchors);
            }

            cellSize = new Vector2Int(maxX - minX + 1, maxY - minY + 1);

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2(cellSize.x, cellSize.y);
                col.offset = new Vector2(cellSize.x * 0.5f, cellSize.y * 0.5f);
            }

            EnsureRoomVolume(
                new Vector3(cellSize.x * 0.5f, cellSize.y * 0.5f, 0f),
                new Vector3(cellSize.x, cellSize.y, 1f)
            );
        }

        /// <summary>
        /// Generates the buildable placement mask: Ground cells, minus cells covered by Walls,
        /// minus an edge margin, minus cells within openingClearance of each declared Opening.
        /// The resulting mask is stored in buildableCells and remains hand-editable.
        /// </summary>
        public void GenerateBuildableMaskFromGround(
            int margin = 1,
            int openingClearance = 2,
            int cellWidth = 0,
            int cellHeight = 0,
            int openingWidth = 2)
        {
            int effW = cellWidth > 0 ? cellWidth : cellSize.x;
            int effH = cellHeight > 0 ? cellHeight : cellSize.y;

            if (groundTilemap == null)
            {
                AutoFindTilemaps();
            }

            if (groundTilemap == null)
            {
                FillBuildableBox(effW, effH, margin);
                return;
            }

            var groundSet = GetGroundCellCoords(effW, effH);
            if (groundSet.Count == 0)
            {
                FillBuildableBox(effW, effH, margin);
                return;
            }

            // Find wall cells
            var wallSet = GetWallCellCoords(effW, effH);

            // Calculate opening clearance cells
            var clearanceSet = new HashSet<GridCoord>();
            if (openingsList != null && openingsList.Count > 0)
            {
                foreach (var op in openingsList)
                {
                    var edgeCoords = op.GetEdgeTiles();
                    foreach (var edge in edgeCoords)
                    {
                        for (int dx = -openingClearance + 1; dx < openingClearance; dx++)
                        {
                            for (int dy = -openingClearance + 1; dy < openingClearance; dy++)
                            {
                                int cx = edge.X + dx;
                                int cy = edge.Y + dy;
                                if (cx >= 0 && cx < effW && cy >= 0 && cy < effH)
                                {
                                    clearanceSet.Add(new GridCoord(cx, cy));
                                }
                            }
                        }
                    }
                }
            }
            else if (OpeningGeometry.CanCenterOpening(effW, effH, openingWidth))
            {
                for (int d = 0; d < 4; d++)
                {
                    var dir = (CardinalDirection)d;
                    if (openings.ContainsDirection(dir))
                    {
                        var edgeCoords = OpeningGeometry.GetOpeningEdgeCoords(effW, effH, openingWidth, dir);
                        foreach (var edge in edgeCoords)
                        {
                            for (int dx = -openingClearance + 1; dx < openingClearance; dx++)
                            {
                                for (int dy = -openingClearance + 1; dy < openingClearance; dy++)
                                {
                                    int cx = edge.X + dx;
                                    int cy = edge.Y + dy;
                                    if (cx >= 0 && cx < effW && cy >= 0 && cy < effH)
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
                if (cell.X < margin || cell.X >= effW - margin || cell.Y < margin || cell.Y >= effH - margin)
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
                Role = role,
                RoomType = roomType,
                CellWidth = cellSize.x,
                CellHeight = cellSize.y,
                Openings = Openings,
                OpeningsList = openingsList != null ? new List<PieceOpening>(openingsList) : new List<PieceOpening>(),
                OccupiedLocalTiles = OccupiedLocalTiles,
                IsRotatable = isRotatable,
                Weight = weight,
                MinCountPerDungeon = minCountPerDungeon,
                MaxCountPerDungeon = maxCountPerDungeon,
                Footprint = new GridCoord(footprint.x, footprint.y),
                BuildableMask = mask,
                HasDirectionalArt = hasDirectionalArt,
                EntryAnchorCount = validEntryAnchors,
                GroundCells = GetGroundCellCoords(cellSize.x, cellSize.y)
            };
        }

        /// <summary>
        /// Synchronizes this component from a pure C# DungeonPieceDescriptor.
        /// </summary>
        public void LoadFromDescriptor(DungeonPieceDescriptor descriptor)
        {
            if (descriptor == null) return;
            pieceId = descriptor.PieceId;
            role = descriptor.Role;
            roomType = descriptor.RoomType;
            if (descriptor.CellWidth > 0 && descriptor.CellHeight > 0)
            {
                cellSize = new Vector2Int(descriptor.CellWidth, descriptor.CellHeight);
            }
            openings = descriptor.Openings;
            if (descriptor.OpeningsList != null)
            {
                openingsList = new List<PieceOpening>(descriptor.OpeningsList);
            }
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
