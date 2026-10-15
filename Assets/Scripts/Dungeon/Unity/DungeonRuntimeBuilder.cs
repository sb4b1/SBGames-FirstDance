using System;
using System.Collections.Generic;
using UnityEngine;
using NavMeshPlus.Components;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Markers;
using SBGames.Placement;
using SBGames.Loop;
using SB4b1.GameMath;

namespace SBGames.Dungeon.Unity
{
    /// <summary>
    /// Runtime MonoBehaviour coordinator for procedural dungeon instantiation.
    /// Wires pure C# DungeonLayoutGraph to Unity GameObjects, RoomVolume/DoorMarker markers,
    /// DungeonRoomRegistry, DungeonPlacementManager buildable grids, and 2D NavMesh rebuilding.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SBGames/Dungeon/Dungeon Runtime Builder")]
    public class DungeonRuntimeBuilder : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private DungeonGeneratorConfigSO configSO;
        [SerializeField] private int initialSeed = 12345;
        [SerializeField] private bool useRandomSeed = true;
        [SerializeField] private bool autoGenerateOnStart = true;

        [Header("NavMesh")]
        [Tooltip("If true, triggers 2D NavMesh surface bake after instantiating dungeon geometry.")]
        [SerializeField] private bool rebuildNavMesh = true;
        [SerializeField] private NavMeshSurface navMeshSurface;

        [Header("Scene Hierarchy")]
        [SerializeField] private Transform dungeonParent;

        [Header("References")]
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonPlacementManager placementManager;

        private DungeonLayoutGraph _currentGraph;
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private bool _hasGenerated = false;

        public DungeonLayoutGraph CurrentGraph => _currentGraph;
        public bool HasGenerated => _hasGenerated;

        private void Awake()
        {
            if (dungeonParent == null) dungeonParent = transform;
            if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            if (placementManager == null) placementManager = FindAnyObjectByType<DungeonPlacementManager>();
            if (navMeshSurface == null) navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
        }

        private void Start()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandleGamePhaseChanged;
            }

            if (autoGenerateOnStart && !_hasGenerated)
            {
                int seed = useRandomSeed ? unchecked((int)System.DateTime.UtcNow.Ticks) : initialSeed;
                GenerateDungeonForRun(seed);
            }
        }

        private void OnDestroy()
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandleGamePhaseChanged;
            }
        }

        private void HandleGamePhaseChanged(GamePhase phase)
        {
            // If run starts and dungeon hasn't been generated yet, generate before Planning Phase begins
            if ((phase == GamePhase.Planning || phase == GamePhase.NotStarted) && !_hasGenerated)
            {
                int seed = useRandomSeed ? unchecked((int)System.DateTime.UtcNow.Ticks) : initialSeed;
                GenerateDungeonForRun(seed);
            }
        }

        /// <summary>
        /// Generates the dungeon layout, instantiates rooms and doors, configures registry,
        /// builds placement validation rules, and bakes NavMesh.
        /// </summary>
        public DungeonLayoutGraph GenerateDungeonForRun(int seed)
        {
            // 1. Clear room registry and previous geometry
            DungeonRoomRegistry.Clear();
            ClearSpawnedObjects();

            // 2. Prepare Pure C# configuration and generator
            var coreConfig = configSO != null ? configSO.ToCoreConfig() : new DungeonGeneratorConfig();
            var generator = new DungeonLayoutGenerator();
            _currentGraph = generator.Generate(seed, coreConfig);

            float cell = configSO != null ? configSO.CellSize : 1.0f;
            int stepTileX = coreConfig.roomWidth + coreConfig.corridorLength;
            int stepTileY = coreConfig.roomHeight + coreConfig.corridorLength;
            float stepWorldX = stepTileX * cell;
            float stepWorldY = stepTileY * cell;

            var roomInstances = new Dictionary<int, RoomVolume>();

            // 3. Instantiate Rooms
            foreach (var node in _currentGraph.Rooms.Values)
            {
                Vector3 roomCenterWorld = new Vector3(
                    node.MacroCoord.X * stepWorldX,
                    node.MacroCoord.Y * stepWorldY,
                    0f
                );

                GameObject roomGo = null;
                GameObject prefab = GetRoomPrefab(node.RoomType);

                if (prefab != null)
                {
                    roomGo = Instantiate(prefab, roomCenterWorld, Quaternion.identity, dungeonParent);
                }
                else
                {
                    // Fallback procedural room GameObject
                    roomGo = new GameObject($"Room_{node.RoomId}_{node.RoomType}");
                    roomGo.transform.SetParent(dungeonParent);
                    roomGo.transform.position = roomCenterWorld;

                    var boxCol = roomGo.AddComponent<BoxCollider2D>();
                    boxCol.size = new Vector2(coreConfig.roomWidth * cell, coreConfig.roomHeight * cell);
                    boxCol.isTrigger = true;
                }

                _spawnedObjects.Add(roomGo);

                var roomVolume = roomGo.GetComponent<RoomVolume>();
                if (roomVolume == null)
                {
                    roomVolume = roomGo.AddComponent<RoomVolume>();
                }

                roomVolume.RoomId = node.RoomId;
                roomVolume.IsSafeRoom = (node.RoomType == DungeonRoomType.SafeRoom);
                roomVolume.IsBossRoom = (node.RoomType == DungeonRoomType.Boss);
                roomVolume.SetBounds(new Bounds(
                    roomCenterWorld,
                    new Vector3(coreConfig.roomWidth * cell, coreConfig.roomHeight * cell, 2f)
                ));

                if (node.RoomType == DungeonRoomType.SafeRoom)
                {
                    if (roomGo.GetComponent<SafeRoomMarker>() == null)
                    {
                        roomGo.AddComponent<SafeRoomMarker>();
                    }
                }
                else if (node.RoomType == DungeonRoomType.Entrance)
                {
                    DungeonRoomRegistry.EntrancePosition = roomCenterWorld;
                }

                DungeonRoomRegistry.RegisterRoom(roomVolume);
                roomInstances[node.RoomId] = roomVolume;
            }

            // 4. Instantiate Doors & Connecting Corridors
            for (int i = 0; i < _currentGraph.Doors.Count; i++)
            {
                var door = _currentGraph.Doors[i];
                if (!roomInstances.TryGetValue(door.RoomAId, out var roomA) ||
                    !roomInstances.TryGetValue(door.RoomBId, out var roomB))
                {
                    continue;
                }

                _currentGraph.GetCorridorTileBounds(door, coreConfig, out var cMinTile, out var cMaxTile);
                Vector3 corridorCenterWorld = new Vector3(
                    ((cMinTile.X + cMaxTile.X) * 0.5f) * cell,
                    ((cMinTile.Y + cMaxTile.Y) * 0.5f) * cell,
                    0f
                );

                GameObject doorGo = null;
                GameObject doorPrefab = door.IsBossDoor
                    ? (configSO?.BossDoorPrefab ?? configSO?.DoorPrefab)
                    : configSO?.DoorPrefab;

                if (doorPrefab != null)
                {
                    doorGo = Instantiate(doorPrefab, corridorCenterWorld, Quaternion.identity, dungeonParent);
                }
                else
                {
                    doorGo = new GameObject($"Door_{door.DoorId}" + (door.IsBossDoor ? "_Boss" : ""));
                    doorGo.transform.SetParent(dungeonParent);
                    doorGo.transform.position = corridorCenterWorld;
                }

                _spawnedObjects.Add(doorGo);

                var doorMarker = doorGo.GetComponent<DoorMarker>();
                if (doorMarker == null)
                {
                    doorMarker = doorGo.AddComponent<DoorMarker>();
                }

                doorMarker.DoorId = door.DoorId;
                doorMarker.RoomA = roomA;
                doorMarker.RoomB = roomB;
                doorMarker.IsBossDoor = door.IsBossDoor;

                if (door.IsBossDoor && doorGo.GetComponent<BossDoorMarker>() == null)
                {
                    doorGo.AddComponent<BossDoorMarker>();
                }

                roomA.AddExit(doorMarker);
                roomB.AddExit(doorMarker);
                DungeonRoomRegistry.RegisterDoor(doorMarker);
            }

            // 5. Seal unused doorway sockets with wall colliders
            SpawnWallColliders(_currentGraph, coreConfig, cell, dungeonParent);

            // 6. Integrate with DungeonPlacementManager
            ConfigurePlacementGrid(_currentGraph, coreConfig, cell);

            // 7. Rebuild NavMesh if surface is present
            if (rebuildNavMesh)
            {
                Rebuild2DNavMesh();
            }

            _hasGenerated = true;
            return _currentGraph;
        }

        private GameObject GetRoomPrefab(DungeonRoomType type)
        {
            if (configSO == null) return null;
            switch (type)
            {
                case DungeonRoomType.Entrance: return configSO.EntranceRoomPrefab;
                case DungeonRoomType.SafeRoom: return configSO.SafeRoomPrefab;
                case DungeonRoomType.Boss:     return configSO.BossRoomPrefab;
                case DungeonRoomType.Normal:   return configSO.NormalRoomPrefab;
                default:                       return configSO.NormalRoomPrefab;
            }
        }

        private void SpawnWallColliders(
            DungeonLayoutGraph graph,
            DungeonGeneratorConfig config,
            float cell,
            Transform parent)
        {
            // Seal perimeter for every room where no doorway exists
            float halfW = (config.roomWidth * 0.5f) * cell;
            float halfH = (config.roomHeight * 0.5f) * cell;
            float wallThickness = 0.5f * cell;

            foreach (var node in graph.Rooms.Values)
            {
                int stepX = config.roomWidth + config.corridorLength;
                int stepY = config.roomHeight + config.corridorLength;
                Vector3 roomCenter = new Vector3(node.MacroCoord.X * stepX * cell, node.MacroCoord.Y * stepY * cell, 0f);

                for (int d = 0; d < CardinalDirectionExtensions.All.Length; d++)
                {
                    var dir = CardinalDirectionExtensions.All[d];
                    if (!node.HasDoor(dir))
                    {
                        // No door in this direction -> seal with wall collider
                        var wallGo = new GameObject($"Wall_{node.RoomId}_{dir}");
                        wallGo.transform.SetParent(parent);
                        _spawnedObjects.Add(wallGo);

                        var (dx, dy) = dir.ToOffset();
                        Vector3 wallPos = roomCenter + new Vector3(dx * halfW, dy * halfH, 0f);
                        wallGo.transform.position = wallPos;

                        var col = wallGo.AddComponent<BoxCollider2D>();
                        if (dir == CardinalDirection.North || dir == CardinalDirection.South)
                        {
                            col.size = new Vector2(config.roomWidth * cell, wallThickness);
                        }
                        else
                        {
                            col.size = new Vector2(wallThickness, config.roomHeight * cell);
                        }
                    }
                }
            }
        }

        private void ConfigurePlacementGrid(
            DungeonLayoutGraph graph,
            DungeonGeneratorConfig config,
            float cell)
        {
            if (placementManager == null)
            {
                placementManager = FindAnyObjectByType<DungeonPlacementManager>();
            }

            if (placementManager == null) return;

            graph.GetTotalTileBounds(config, out var minTile, out var maxTile);

            // Compute grid bounds with 1-tile outer padding
            int padding = 1;
            int originTileX = minTile.X - padding;
            int originTileY = minTile.Y - padding;
            int gridTilesW = (maxTile.X - minTile.X + 1) + (padding * 2);
            int gridTilesH = (maxTile.Y - minTile.Y + 1) + (padding * 2);

            Vector3 gridOrigin = new Vector3(originTileX * cell, originTileY * cell, 0f);
            Vector2Int gridSize = new Vector2Int(gridTilesW, gridTilesH);

            // Collect all buildable cells (Normal, Safe, Boss rooms & corridors) in grid coordinates
            var coreBuildable = graph.GetBuildableCells(config);
            var gridBuildableCells = new HashSet<Vector2Int>();

            foreach (var c in coreBuildable)
            {
                int gx = c.X - originTileX;
                int gy = c.Y - originTileY;
                gridBuildableCells.Add(new Vector2Int(gx, gy));
            }

            var validator = new SetBasedBuildableCellValidator(gridBuildableCells);
            placementManager.ConfigurePlacementGrid(gridSize, gridOrigin, cell, validator);
        }

        /// <summary>
        /// Rebuilds the NavMeshPlus 2D surface if present in the scene.
        /// </summary>
        public void Rebuild2DNavMesh()
        {
            if (navMeshSurface == null)
            {
                navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
            }

            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
        }

        private void ClearSpawnedObjects()
        {
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i] != null)
                {
                    if (Application.isPlaying) Destroy(_spawnedObjects[i]);
                    else DestroyImmediate(_spawnedObjects[i]);
                }
            }
            _spawnedObjects.Clear();
        }
    }
}
