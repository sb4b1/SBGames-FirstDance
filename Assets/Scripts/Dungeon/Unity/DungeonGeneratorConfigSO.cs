using UnityEngine;
using SBGames.Dungeon.Core;

namespace SBGames.Dungeon.Unity
{
    [CreateAssetMenu(fileName = "DungeonGeneratorConfig", menuName = "SBGames/Dungeon/Generator Config", order = 100)]
    public class DungeonGeneratorConfigSO : ScriptableObject
    {
        [Header("Room Count & Topology")]
        [Tooltip("Minimum number of normal rooms (excluding Entrance, SafeRoom, and Boss).")]
        [SerializeField, Range(1, 20)] private int minNormalRooms = 5;

        [Tooltip("Maximum number of normal rooms (excluding Entrance, SafeRoom, and Boss).")]
        [SerializeField, Range(1, 30)] private int maxNormalRooms = 8;

        [Tooltip("Probability [0..1] of branching off an earlier room to create dead-end side paths.")]
        [SerializeField, Range(0f, 1f)] private float branchProbability = 0.35f;

        [Tooltip("Probability [0..1] of connecting adjacent non-boss rooms to create cyclical loops.")]
        [SerializeField, Range(0f, 1f)] private float loopProbability = 0f;

        [Tooltip("Number of safe rooms to generate (0 or 1, placed along the deeper half).")]
        [SerializeField, Range(0, 1)] private int safeRoomCount = 1;

        [Header("Piece Set (Optional)")]
        [Tooltip("Optional authored piece set. If null, per-room-type prefab slots and procedural fallbacks are used.")]
        [SerializeField] private SBGames.Dungeon.Unity.Pieces.PieceSetSO pieceSet;

        [Header("Spatial Dimensions (in Grid Cells)")]
        [Tooltip("Room width in grid cells.")]
        [SerializeField, Min(4)] private int roomWidth = 16;

        [Tooltip("Room height in grid cells.")]
        [SerializeField, Min(4)] private int roomHeight = 16;

        [Tooltip("Corridor length between adjacent rooms in grid cells.")]
        [SerializeField, Min(0)] private int corridorLength = 4;

        [Tooltip("Corridor width in grid cells.")]
        [SerializeField, Min(1)] private int corridorWidth = 2;

        [Tooltip("Size in Unity world units per grid cell.")]
        [SerializeField, Min(0.1f)] private float cellSize = 1.0f;

        [Header("Room Capacity from Buildable Mask")]
        [Tooltip("Number of buildable mask cells per soft capacity slot (default 49).")]
        [SerializeField, Min(1f)] private float cellsPerSoftSlot = 49f;

        [Tooltip("Ratio of max capacity relative to soft capacity (default 1.5).")]
        [SerializeField, Min(1f)] private float maxCapacityMultiplier = 1.5f;

        [Tooltip("Soft capacity multiplier for Normal rooms (default 1.0).")]
        [SerializeField, Range(0.1f, 5f)] private float normalSoftMultiplier = 1.0f;

        [Tooltip("Soft capacity multiplier for Boss rooms (default 1.5).")]
        [SerializeField, Range(0.1f, 5f)] private float bossSoftMultiplier = 1.5f;

        [Tooltip("Max capacity multiplier for Boss rooms relative to soft capacity (default 1.67).")]
        [SerializeField, Range(0.1f, 5f)] private float bossMaxMultiplier = 1.67f;

        [Header("Room Prefab Templates (Optional - Fallbacks to procedural generation if null)")]
        [SerializeField] private GameObject entranceRoomPrefab;
        [SerializeField] private GameObject normalRoomPrefab;
        [SerializeField] private GameObject safeRoomPrefab;
        [SerializeField] private GameObject bossRoomPrefab;

        [Header("Door & Wall Prefabs (Optional - Fallbacks to procedural generation if null)")]
        [SerializeField] private GameObject doorPrefab;
        [SerializeField] private GameObject bossDoorPrefab;
        [SerializeField] private GameObject wallPrefab;

        public int MinNormalRooms => minNormalRooms;
        public int MaxNormalRooms => maxNormalRooms;
        public float BranchProbability => branchProbability;
        public float LoopProbability => loopProbability;
        public int SafeRoomCount => safeRoomCount;
        public float CellsPerSoftSlot => cellsPerSoftSlot;
        public float MaxCapacityMultiplier => maxCapacityMultiplier;
        public float NormalSoftMultiplier => normalSoftMultiplier;
        public float BossSoftMultiplier => bossSoftMultiplier;
        public float BossMaxMultiplier => bossMaxMultiplier;
        public SBGames.Dungeon.Unity.Pieces.PieceSetSO PieceSet => pieceSet;
        public int RoomWidth => roomWidth;
        public int RoomHeight => roomHeight;
        public int CorridorLength => corridorLength;
        public int CorridorWidth => corridorWidth;
        public float CellSize => cellSize;

        public GameObject EntranceRoomPrefab => entranceRoomPrefab;
        public GameObject NormalRoomPrefab => normalRoomPrefab;
        public GameObject SafeRoomPrefab => safeRoomPrefab;
        public GameObject BossRoomPrefab => bossRoomPrefab;
        public GameObject DoorPrefab => doorPrefab;
        public GameObject BossDoorPrefab => bossDoorPrefab;
        public GameObject WallPrefab => wallPrefab;

        public DungeonGeneratorConfig ToCoreConfig()
        {
            return new DungeonGeneratorConfig(
                minNormalRooms,
                maxNormalRooms,
                branchProbability,
                loopProbability,
                safeRoomCount,
                roomWidth,
                roomHeight,
                corridorLength,
                corridorWidth
            );
        }

        public void SetPieceSetForTesting(SBGames.Dungeon.Unity.Pieces.PieceSetSO set) => pieceSet = set;
        public void SetCorridorLengthForTesting(int length) => corridorLength = length;
    }
}
