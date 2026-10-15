using System;
using System.Collections.Generic;
using UnityEngine;
using NavMeshPlus.Components;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Markers;
using SBGames.Dungeon.Unity.Pieces;
using SBGames.Placement;
using SBGames.Loop;
using SB4b1.GameMath;

namespace SBGames.Dungeon.Unity
{
    /// <summary>
    /// Runtime MonoBehaviour coordinator for procedural dungeon instantiation.
    /// Wires pure C# DungeonLayoutGraph to Unity GameObjects, RoomVolume/DoorMarker markers,
    /// DungeonRoomRegistry, DungeonPlacementManager buildable grids, and 2D NavMesh rebuilding.
    /// Supports authored PieceSet selection, rotation, opening caps, and seed retries.
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

        [Header("Piece Selection & Retries")]
        [Tooltip("Maximum attempts to generate a layout and select matching pieces before falling back.")]
        [SerializeField, Range(1, 20)] private int maxGenerationRetries = 15;

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

        public void SetConfigSOForTesting(DungeonGeneratorConfigSO config) => configSO = config;

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
        /// Retries with a derived seed if piece selection coverage fails.
        /// </summary>
        public DungeonLayoutGraph GenerateDungeonForRun(int seed)
        {
            // 1. Clear room registry and previous geometry
            DungeonRoomRegistry.Clear();
            ClearSpawnedObjects();

            // 2. Prepare Pure C# configuration and generator
            var coreConfig = configSO != null ? configSO.ToCoreConfig() : new DungeonGeneratorConfig();
            var generator = new DungeonLayoutGenerator();

            PieceSetSO pieceSet = configSO != null ? configSO.PieceSet : null;

            var pieceDescriptors = pieceSet != null ? pieceSet.GetDescriptors() : null;
            if (pieceSet != null && (pieceDescriptors == null || pieceDescriptors.Count == 0))
            {
                Debug.LogWarning($"[DungeonRuntimeBuilder] PieceSet '{pieceSet.ThemeName}' has no piece prefabs. Falling back to procedural room templates.");
                pieceSet = null;
            }

            int usedSeed = seed;
            DungeonLayoutGraph generatedGraph = null;
            DungeonSeamLayoutResult activeSeamLayout = null;

            // Retry loop up to maxGenerationRetries
            for (int attempt = 0; attempt < maxGenerationRetries; attempt++)
            {
                usedSeed = (attempt == 0) ? seed : unchecked(seed * 397 ^ (attempt * 10007 + 54321));
                var graph = generator.Generate(usedSeed, coreConfig);

                if (pieceSet != null && pieceDescriptors != null && pieceDescriptors.Count > 0)
                {
                    var rng = new GameRngDungeonAdapter((uint)Math.Abs(usedSeed + 101));
                    var seamResult = DungeonSeamLayoutSolver.Solve(
                        graph,
                        pieceDescriptors,
                        coreConfig.pathwayProbability,
                        pieceSet.OpeningWidth,
                        rng
                    );

                    if (seamResult.Success)
                    {
                        generatedGraph = graph;
                        activeSeamLayout = seamResult;
                        Debug.Log($"[DungeonRuntimeBuilder] Dungeon generated successfully with seam snapping using seed: {usedSeed} (attempt {attempt + 1}/{maxGenerationRetries}) with PieceSet '{pieceSet.ThemeName}'.");
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"[DungeonRuntimeBuilder] Attempt {attempt + 1}/{maxGenerationRetries} with seed {usedSeed} failed seam placement ({seamResult.FailureReason}). Retrying with derived seed...");
                    }
                }
                else
                {
                    generatedGraph = graph;
                    Debug.Log($"[DungeonRuntimeBuilder] Dungeon generated successfully using seed: {usedSeed} (attempt {attempt + 1}/{maxGenerationRetries}).");
                    break;
                }
            }

            if (generatedGraph == null)
            {
                Debug.LogError($"[DungeonRuntimeBuilder] All {maxGenerationRetries} generation attempts failed piece selection. Generating fallback layout without PieceSet.");
                generatedGraph = generator.Generate(seed, coreConfig);
                activeSeamLayout = null;
            }

            _currentGraph = generatedGraph;

            float cell = configSO != null ? configSO.CellSize : 1.0f;
            int stepTileX = coreConfig.roomWidth + coreConfig.corridorLength;
            int stepTileY = coreConfig.roomHeight + coreConfig.corridorLength;
            float stepWorldX = stepTileX * cell;
            float stepWorldY = stepTileY * cell;

            var roomInstances = new Dictionary<int, RoomVolume>();

            // 3. Instantiate Rooms & Pathways via Seam Layout
            if (activeSeamLayout != null && activeSeamLayout.Success)
            {
                var allPieces = new List<PlacedPiece>();
                allPieces.AddRange(activeSeamLayout.PlacedRooms.Values);
                allPieces.AddRange(activeSeamLayout.PlacedPathways);

                foreach (var placed in allPieces)
                {
                    Vector3 roomCenterWorld = new Vector3(
                        (placed.WorldOrigin.X + placed.CellWidth * 0.5f) * cell,
                        (placed.WorldOrigin.Y + placed.CellHeight * 0.5f) * cell,
                        0f
                    );

                    Quaternion pieceRot = Quaternion.Euler(0f, 0f, -90f * placed.QuarterTurns);
                    GameObject piecePrefab = pieceSet.GetPrefabForPiece(placed.Descriptor.PieceId);
                    GameObject roomGo = null;

                    if (piecePrefab != null)
                    {
                        roomGo = Instantiate(piecePrefab, roomCenterWorld, pieceRot, dungeonParent);
                    }
                    else
                    {
                        roomGo = new GameObject($"Piece_{placed.RoomId}_{placed.Descriptor.PieceId}");
                        roomGo.transform.SetParent(dungeonParent);
                        roomGo.transform.position = roomCenterWorld;

                        var boxCol = roomGo.AddComponent<BoxCollider2D>();
                        boxCol.size = new Vector2(placed.CellWidth * cell, placed.CellHeight * cell);
                        boxCol.isTrigger = true;
                    }

                    _spawnedObjects.Add(roomGo);

                    var roomVolume = roomGo.GetComponent<RoomVolume>() ?? roomGo.AddComponent<RoomVolume>();
                    roomVolume.RoomId = placed.RoomId;
                    roomVolume.IsSafeRoom = (placed.Descriptor.RoomType == DungeonRoomType.SafeRoom);
                    roomVolume.IsBossRoom = (placed.Descriptor.RoomType == DungeonRoomType.Boss);
                    roomVolume.SetBounds(new Bounds(
                        roomCenterWorld,
                        new Vector3(placed.CellWidth * cell, placed.CellHeight * cell, 2f)
                    ));

                    var pieceComp = roomGo.GetComponent<DungeonPiece>();
                    int maskCount = placed.RotatedBuildableMask != null && placed.RotatedBuildableMask.Count > 0
                        ? placed.RotatedBuildableMask.Count
                        : (placed.CellWidth - 2) * (placed.CellHeight - 2);

                    float cellsPerSoft = configSO != null ? configSO.CellsPerSoftSlot : 49f;
                    float maxMult = configSO != null ? configSO.MaxCapacityMultiplier : 1.5f;
                    float normalSoftMult = configSO != null ? configSO.NormalSoftMultiplier : 1.0f;
                    float bossSoftMult = configSO != null ? configSO.BossSoftMultiplier : 1.5f;
                    float bossMaxMult = configSO != null ? configSO.BossMaxMultiplier : 1.67f;

                    if (placed.Descriptor.Role == PieceRole.Pathway)
                    {
                        int pathSoft = Mathf.FloorToInt((maskCount / cellsPerSoft) * normalSoftMult);
                        int pathMax = Mathf.CeilToInt(pathSoft * maxMult);
                        roomVolume.SoftMonsterCapacity = pathSoft;
                        roomVolume.MaxMonsterCapacity = pathMax;
                    }
                    else
                    {
                        switch (placed.Descriptor.RoomType)
                        {
                            case DungeonRoomType.Entrance:
                                roomVolume.SoftMonsterCapacity = 0;
                                roomVolume.MaxMonsterCapacity = 0;
                                break;
                            case DungeonRoomType.SafeRoom:
                                roomVolume.SoftMonsterCapacity = 0;
                                roomVolume.MaxMonsterCapacity = 2;
                                break;
                            case DungeonRoomType.Boss:
                                int bSoft = Mathf.FloorToInt((maskCount / cellsPerSoft) * bossSoftMult);
                                int bMax = Mathf.CeilToInt(bSoft * bossMaxMult);
                                roomVolume.SoftMonsterCapacity = bSoft;
                                roomVolume.MaxMonsterCapacity = bMax;
                                break;
                            default:
                                int nSoft = Mathf.FloorToInt((maskCount / cellsPerSoft) * normalSoftMult);
                                int nMax = Mathf.CeilToInt(nSoft * maxMult);
                                roomVolume.SoftMonsterCapacity = nSoft;
                                roomVolume.MaxMonsterCapacity = nMax;
                                break;
                        }
                    }

                    if (placed.Descriptor.RoomType == DungeonRoomType.SafeRoom && placed.Descriptor.Role == PieceRole.Room)
                    {
                        if (roomGo.GetComponent<SafeRoomMarker>() == null) roomGo.AddComponent<SafeRoomMarker>();
                    }
                    else if (placed.Descriptor.RoomType == DungeonRoomType.Entrance && placed.Descriptor.Role == PieceRole.Room)
                    {
                        if (pieceComp != null && pieceComp.EntryAnchors.Count > 0 && pieceComp.EntryAnchors[0] != null)
                        {
                            DungeonRoomRegistry.EntrancePosition = pieceComp.EntryAnchors[0].position;
                        }
                        else
                        {
                            DungeonRoomRegistry.EntrancePosition = roomCenterWorld;
                        }
                    }

                    DungeonRoomRegistry.RegisterRoom(roomVolume);
                    roomInstances[placed.RoomId] = roomVolume;
                }

                // 4. Instantiate Doors at active seams
                int doorCounter = 0;
                foreach (var seam in activeSeamLayout.Seams)
                {
                    doorCounter++;
                    if (!roomInstances.TryGetValue(seam.PieceA.RoomId, out var roomA) ||
                        !roomInstances.TryGetValue(seam.PieceB.RoomId, out var roomB))
                    {
                        continue;
                    }

                    var edgeTile = seam.PieceA.LocalToWorld(seam.OpeningA.AnchorTile);
                    var (dx, dy) = seam.OpeningA.Direction.ToOffset();
                    float seamX = (edgeTile.X + (seam.OpeningA.Direction == CardinalDirection.North || seam.OpeningA.Direction == CardinalDirection.South ? 1.0f : (dx > 0 ? 1.0f : 0f))) * cell;
                    float seamY = (edgeTile.Y + (seam.OpeningA.Direction == CardinalDirection.East || seam.OpeningA.Direction == CardinalDirection.West ? 1.0f : (dy > 0 ? 1.0f : 0f))) * cell;
                    Vector3 doorWorldPos = new Vector3(seamX, seamY, 0f);

                    GameObject doorPrefab = seam.IsBossDoor
                        ? (configSO?.BossDoorPrefab ?? configSO?.DoorPrefab)
                        : configSO?.DoorPrefab;

                    GameObject doorGo = null;
                    if (doorPrefab != null)
                    {
                        doorGo = Instantiate(doorPrefab, doorWorldPos, Quaternion.identity, dungeonParent);
                    }
                    else
                    {
                        doorGo = new GameObject($"Door_{doorCounter}" + (seam.IsBossDoor ? "_Boss" : ""));
                        doorGo.transform.SetParent(dungeonParent);
                        doorGo.transform.position = doorWorldPos;
                    }

                    _spawnedObjects.Add(doorGo);

                    var doorMarker = doorGo.GetComponent<DoorMarker>() ?? doorGo.AddComponent<DoorMarker>();
                    doorMarker.DoorId = doorCounter;
                    doorMarker.RoomA = roomA;
                    doorMarker.RoomB = roomB;
                    doorMarker.IsBossDoor = seam.IsBossDoor;

                    if (seam.IsBossDoor && doorGo.GetComponent<BossDoorMarker>() == null)
                    {
                        doorGo.AddComponent<BossDoorMarker>();
                    }

                    roomA.AddExit(doorMarker);
                    roomB.AddExit(doorMarker);
                    DungeonRoomRegistry.RegisterDoor(doorMarker);
                }

                // 5. Spawn Opening Caps for unused sockets
                SpawnSeamOpeningCaps(activeSeamLayout.UnusedSockets, cell, dungeonParent);

                // 6. Configure placement grid
                if (placementManager != null)
                {
                    int minX = int.MaxValue, minY = int.MaxValue;
                    int maxX = int.MinValue, maxY = int.MinValue;
                    var worldBuildableCells = new HashSet<GridCoord>();

                    foreach (var p in allPieces)
                    {
                        minX = Math.Min(minX, p.WorldOrigin.X);
                        minY = Math.Min(minY, p.WorldOrigin.Y);
                        maxX = Math.Max(maxX, p.WorldOrigin.X + p.CellWidth);
                        maxY = Math.Max(maxY, p.WorldOrigin.Y + p.CellHeight);

                        if (p.Descriptor.Role != PieceRole.Pathway && p.Descriptor.RoomType == DungeonRoomType.Entrance)
                        {
                            continue; // Entrance is strictly non-buildable
                        }

                        foreach (var local in p.RotatedBuildableMask)
                        {
                            worldBuildableCells.Add(p.LocalToWorld(local));
                        }
                    }

                    if (minX == int.MaxValue)
                    {
                        minX = 0; minY = 0; maxX = 16; maxY = 16;
                    }

                    int padding = 1;
                    int originTileX = minX - padding;
                    int originTileY = minY - padding;
                    int gridTilesW = (maxX - minX + 1) + (padding * 2);
                    int gridTilesH = (maxY - minY + 1) + (padding * 2);

                    Vector3 gridOrigin = new Vector3(originTileX * cell, originTileY * cell, 0f);
                    Vector2Int gridSize = new Vector2Int(gridTilesW, gridTilesH);

                    var gridBuildableCells = new HashSet<Vector2Int>();
                    foreach (var c in worldBuildableCells)
                    {
                        gridBuildableCells.Add(new Vector2Int(c.X - originTileX, c.Y - originTileY));
                    }

                    var validator = new SetBasedBuildableCellValidator(gridBuildableCells);
                    placementManager.ConfigurePlacementGrid(gridSize, gridOrigin, cell, validator);
                }
            }
            else
            {
                // Procedural box-room fallback (when no PieceSet configured)
                foreach (var node in _currentGraph.Rooms.Values)
                {
                    Vector3 roomCenterWorld = new Vector3(
                        node.MacroCoord.X * stepWorldX,
                        node.MacroCoord.Y * stepWorldY,
                        0f
                    );

                    GameObject prefab = GetRoomPrefab(node.RoomType);
                    GameObject roomGo = null;

                    if (prefab != null)
                    {
                        roomGo = Instantiate(prefab, roomCenterWorld, Quaternion.identity, dungeonParent);
                    }
                    else
                    {
                        roomGo = new GameObject($"Room_{node.RoomId}_{node.RoomType}");
                        roomGo.transform.SetParent(dungeonParent);
                        roomGo.transform.position = roomCenterWorld;

                        var boxCol = roomGo.AddComponent<BoxCollider2D>();
                        boxCol.size = new Vector2(coreConfig.roomWidth * cell, coreConfig.roomHeight * cell);
                        boxCol.isTrigger = true;
                    }

                    _spawnedObjects.Add(roomGo);

                    var roomVolume = roomGo.GetComponent<RoomVolume>() ?? roomGo.AddComponent<RoomVolume>();
                    roomVolume.RoomId = node.RoomId;
                    roomVolume.IsSafeRoom = (node.RoomType == DungeonRoomType.SafeRoom);
                    roomVolume.IsBossRoom = (node.RoomType == DungeonRoomType.Boss);
                    roomVolume.SetBounds(new Bounds(
                        roomCenterWorld,
                        new Vector3(coreConfig.roomWidth * cell, coreConfig.roomHeight * cell, 2f)
                    ));

                    int maskCount = (coreConfig.roomWidth - 2) * (coreConfig.roomHeight - 2);
                    float cellsPerSoft = configSO != null ? configSO.CellsPerSoftSlot : 49f;
                    float maxMult = configSO != null ? configSO.MaxCapacityMultiplier : 1.5f;

                    switch (node.RoomType)
                    {
                        case DungeonRoomType.Entrance:
                            roomVolume.SoftMonsterCapacity = 0;
                            roomVolume.MaxMonsterCapacity = 0;
                            break;
                        case DungeonRoomType.SafeRoom:
                            roomVolume.SoftMonsterCapacity = 0;
                            roomVolume.MaxMonsterCapacity = 2;
                            break;
                        case DungeonRoomType.Boss:
                            int bSoft = Mathf.FloorToInt(maskCount / cellsPerSoft * 1.5f);
                            roomVolume.SoftMonsterCapacity = bSoft;
                            roomVolume.MaxMonsterCapacity = Mathf.CeilToInt(bSoft * 1.67f);
                            break;
                        default:
                            int nSoft = Mathf.FloorToInt(maskCount / cellsPerSoft);
                            roomVolume.SoftMonsterCapacity = nSoft;
                            roomVolume.MaxMonsterCapacity = Mathf.CeilToInt(nSoft * maxMult);
                            break;
                    }

                    if (node.RoomType == DungeonRoomType.SafeRoom && roomGo.GetComponent<SafeRoomMarker>() == null)
                    {
                        roomGo.AddComponent<SafeRoomMarker>();
                    }
                    else if (node.RoomType == DungeonRoomType.Entrance)
                    {
                        DungeonRoomRegistry.EntrancePosition = roomCenterWorld;
                    }

                    DungeonRoomRegistry.RegisterRoom(roomVolume);
                    roomInstances[node.RoomId] = roomVolume;
                }

                // Fallback doors
                for (int i = 0; i < _currentGraph.Doors.Count; i++)
                {
                    var door = _currentGraph.Doors[i];
                    if (!roomInstances.TryGetValue(door.RoomAId, out var roomA) ||
                        !roomInstances.TryGetValue(door.RoomBId, out var roomB))
                    {
                        continue;
                    }

                    _currentGraph.TryGetDoorTilePosition(door, coreConfig, out float doorTileX, out float doorTileY);
                    Vector3 doorWorldPos = new Vector3(doorTileX * cell, doorTileY * cell, 0f);

                    GameObject doorPrefab = door.IsBossDoor ? (configSO?.BossDoorPrefab ?? configSO?.DoorPrefab) : configSO?.DoorPrefab;
                    GameObject doorGo = doorPrefab != null
                        ? Instantiate(doorPrefab, doorWorldPos, Quaternion.identity, dungeonParent)
                        : new GameObject($"Door_{door.DoorId}");

                    _spawnedObjects.Add(doorGo);

                    var doorMarker = doorGo.GetComponent<DoorMarker>() ?? doorGo.AddComponent<DoorMarker>();
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

                // 6. Integrate with DungeonPlacementManager
                ConfigurePlacementGrid(_currentGraph, null, coreConfig, cell);
            }

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

        private void SpawnOpeningCaps(
            DungeonLayoutNode node,
            Vector3 roomCenterWorld,
            OpeningFlags cappedSides,
            DungeonGeneratorConfig config,
            float cell,
            Transform parent)
        {
            float halfW = (config.roomWidth * 0.5f) * cell;
            float halfH = (config.roomHeight * 0.5f) * cell;
            float wallThickness = 0.5f * cell;
            int openingWidth = configSO?.PieceSet != null ? configSO.PieceSet.OpeningWidth : config.corridorWidth;
            float capLength = openingWidth * cell;

            GameObject capPrefab = configSO?.PieceSet?.OpeningCapPrefab != null
                ? configSO.PieceSet.OpeningCapPrefab
                : configSO?.WallPrefab;

            if (capPrefab == null)
            {
                Debug.LogWarning($"[DungeonRuntimeBuilder] OpeningCapPrefab is null on PieceSet '{(configSO?.PieceSet != null ? configSO.PieceSet.ThemeName : "None")}'. Using invisible-collider fallback.");
            }

            var directions = cappedSides.ToDirections();
            for (int i = 0; i < directions.Count; i++)
            {
                var dir = directions[i];
                var (dx, dy) = dir.ToOffset();
                Vector3 capPos = roomCenterWorld + new Vector3(dx * halfW, dy * halfH, 0f);

                float angle = 0f;
                if (dir == CardinalDirection.East) angle = -90f;
                else if (dir == CardinalDirection.South) angle = 180f;
                else if (dir == CardinalDirection.West) angle = 90f;

                Quaternion capRot = Quaternion.Euler(0f, 0f, angle);
                GameObject capGo = null;

                if (capPrefab != null)
                {
                    capGo = Instantiate(capPrefab, capPos, capRot, parent);
                }
                else
                {
                    capGo = new GameObject($"OpeningCap_{node.RoomId}_{dir}");
                    capGo.transform.SetParent(parent);
                    capGo.transform.position = capPos;
                    capGo.transform.rotation = capRot;

                    var col = capGo.AddComponent<BoxCollider2D>();
                    if (dir == CardinalDirection.North || dir == CardinalDirection.South)
                    {
                        col.size = new Vector2(capLength, wallThickness);
                    }
                    else
                    {
                        col.size = new Vector2(wallThickness, capLength);
                    }
                }

                _spawnedObjects.Add(capGo);
            }
        }

        private void SpawnSeamOpeningCaps(List<UnusedSocket> unusedSockets, float cell, Transform parent)
        {
            if (unusedSockets == null || unusedSockets.Count == 0) return;

            GameObject capPrefab = configSO?.PieceSet?.OpeningCapPrefab != null
                ? configSO.PieceSet.OpeningCapPrefab
                : configSO?.WallPrefab;

            if (capPrefab == null)
            {
                Debug.LogWarning($"[DungeonRuntimeBuilder] OpeningCapPrefab is null on PieceSet '{(configSO?.PieceSet != null ? configSO.PieceSet.ThemeName : "None")}'. Using invisible-collider fallback.");
            }

            int capIndex = 0;
            foreach (var socket in unusedSockets)
            {
                capIndex++;
                var p = socket.Piece;
                var op = socket.Opening;
                var edgeWorld = p.LocalToWorld(op.AnchorTile);

                float capX = (edgeWorld.X + (op.Direction == CardinalDirection.North || op.Direction == CardinalDirection.South ? op.Width * 0.5f : (op.Direction == CardinalDirection.East ? 1.0f : 0f))) * cell;
                float capY = (edgeWorld.Y + (op.Direction == CardinalDirection.East || op.Direction == CardinalDirection.West ? op.Width * 0.5f : (op.Direction == CardinalDirection.North ? 1.0f : 0f))) * cell;
                Vector3 capPos = new Vector3(capX, capY, 0f);

                float angle = 0f;
                if (op.Direction == CardinalDirection.East) angle = -90f;
                else if (op.Direction == CardinalDirection.South) angle = 180f;
                else if (op.Direction == CardinalDirection.West) angle = 90f;

                Quaternion capRot = Quaternion.Euler(0f, 0f, angle);
                GameObject capGo = null;

                if (capPrefab != null)
                {
                    capGo = Instantiate(capPrefab, capPos, capRot, parent);
                }
                else
                {
                    capGo = new GameObject($"OpeningCap_{p.RoomId}_{capIndex}");
                    capGo.transform.SetParent(parent);
                    capGo.transform.position = capPos;
                    capGo.transform.rotation = capRot;

                    var col = capGo.AddComponent<BoxCollider2D>();
                    float capLength = op.Width * cell;
                    float wallThickness = 0.5f * cell;
                    if (op.Direction == CardinalDirection.North || op.Direction == CardinalDirection.South)
                    {
                        col.size = new Vector2(capLength, wallThickness);
                    }
                    else
                    {
                        col.size = new Vector2(wallThickness, capLength);
                    }
                }

                _spawnedObjects.Add(capGo);
            }
        }

        private void SpawnWallColliders(
            DungeonLayoutGraph graph,
            Dictionary<int, DungeonPieceSelectionResult> selectedPieces,
            DungeonGeneratorConfig config,
            float cell,
            Transform parent)
        {
            float halfW = (config.roomWidth * 0.5f) * cell;
            float halfH = (config.roomHeight * 0.5f) * cell;
            float wallThickness = 0.5f * cell;

            foreach (var node in graph.Rooms.Values)
            {
                int stepX = config.roomWidth + config.corridorLength;
                int stepY = config.roomHeight + config.corridorLength;
                Vector3 roomCenter = new Vector3(node.MacroCoord.X * stepX * cell, node.MacroCoord.Y * stepY * cell, 0f);

                OpeningFlags pieceOpenings = OpeningFlags.None;
                bool hasPiece = false;
                if (selectedPieces != null && selectedPieces.TryGetValue(node.RoomId, out var sel) && sel.Success)
                {
                    hasPiece = true;
                    pieceOpenings = sel.SelectedPiece.GetRotatedOpenings(sel.QuarterTurns);
                }

                for (int d = 0; d < CardinalDirectionExtensions.All.Length; d++)
                {
                    var dir = CardinalDirectionExtensions.All[d];
                    if (!node.HasDoor(dir))
                    {
                        // If a piece is placed and has an opening in this direction, it was already sealed by SpawnOpeningCaps
                        if (hasPiece && pieceOpenings.ContainsDirection(dir))
                        {
                            continue;
                        }

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
            Dictionary<int, DungeonPieceSelectionResult> selectedPieces,
            DungeonGeneratorConfig config,
            float cell)
        {
            if (placementManager == null)
            {
                placementManager = FindAnyObjectByType<DungeonPlacementManager>();
            }

            if (placementManager == null) return;

            graph.GetTotalTileBounds(config, out var minTile, out var maxTile);

            int padding = 1;
            int originTileX = minTile.X - padding;
            int originTileY = minTile.Y - padding;
            int gridTilesW = (maxTile.X - minTile.X + 1) + (padding * 2);
            int gridTilesH = (maxTile.Y - minTile.Y + 1) + (padding * 2);

            Vector3 gridOrigin = new Vector3(originTileX * cell, originTileY * cell, 0f);
            Vector2Int gridSize = new Vector2Int(gridTilesW, gridTilesH);

            var buildableTiles = new HashSet<GridCoord>();

            // 1. Process rooms (Entrance is strictly NON-buildable)
            foreach (var node in graph.Rooms.Values)
            {
                if (node.RoomType == DungeonRoomType.Entrance) continue;

                graph.GetRoomTileBounds(node, config, out var rMin, out var rMax);

                if (selectedPieces != null && selectedPieces.TryGetValue(node.RoomId, out var sel) && sel.Success)
                {
                    var rotatedMask = sel.SelectedPiece.GetRotatedBuildableMask(sel.QuarterTurns, config.roomWidth);
                    if (rotatedMask != null && rotatedMask.Count > 0)
                    {
                        foreach (var localCoord in rotatedMask)
                        {
                            buildableTiles.Add(new GridCoord(rMin.X + localCoord.X, rMin.Y + localCoord.Y));
                        }
                    }
                    else
                    {
                        // Fallback: all room cells if mask is empty
                        for (int x = rMin.X; x <= rMax.X; x++)
                        {
                            for (int y = rMin.Y; y <= rMax.Y; y++)
                            {
                                buildableTiles.Add(new GridCoord(x, y));
                            }
                        }
                    }
                }
                else
                {
                    for (int x = rMin.X; x <= rMax.X; x++)
                    {
                        for (int y = rMin.Y; y <= rMax.Y; y++)
                        {
                            buildableTiles.Add(new GridCoord(x, y));
                        }
                    }
                }
            }

            // 2. Process connecting corridors (when corridorLength > 0)
            if (config.corridorLength > 0)
            {
                for (int i = 0; i < graph.Doors.Count; i++)
                {
                    graph.GetCorridorTileBounds(graph.Doors[i], config, out var cMin, out var cMax);
                    for (int x = cMin.X; x <= cMax.X; x++)
                    {
                        for (int y = cMin.Y; y <= cMax.Y; y++)
                        {
                            buildableTiles.Add(new GridCoord(x, y));
                        }
                    }
                }
            }

            // 3. Convert to grid space
            var gridBuildableCells = new HashSet<Vector2Int>();
            foreach (var c in buildableTiles)
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

        public void ClearSpawnedObjects()
        {
            DungeonRoomRegistry.Clear();
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i] != null)
                {
                    if (Application.isPlaying) Destroy(_spawnedObjects[i]);
                    else DestroyImmediate(_spawnedObjects[i]);
                }
            }
            _spawnedObjects.Clear();
            _hasGenerated = false;
        }
    }
}
