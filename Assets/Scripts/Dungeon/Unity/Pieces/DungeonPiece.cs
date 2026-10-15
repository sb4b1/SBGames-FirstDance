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

        public IReadOnlyList<Vector2Int> BuildableCells => buildableCells;
        public IReadOnlyList<Transform> EntryAnchors => entryAnchors;
        public IReadOnlyList<Transform> ExitAnchors => exitAnchors;
        public IReadOnlyList<Transform> HazardAnchors => hazardAnchors;
        public IReadOnlyList<Transform> LootAnchors => lootAnchors;
        public IReadOnlyList<RoomVolume> RoomVolumes => roomVolumes;

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
        /// Converts this MonoBehaviour piece into a pure C# DungeonPieceDescriptor.
        /// </summary>
        public DungeonPieceDescriptor ToDescriptor()
        {
            var mask = new HashSet<GridCoord>();
            for (int i = 0; i < buildableCells.Count; i++)
            {
                mask.Add(new GridCoord(buildableCells[i].x, buildableCells[i].y));
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
                HasDirectionalArt = hasDirectionalArt
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
