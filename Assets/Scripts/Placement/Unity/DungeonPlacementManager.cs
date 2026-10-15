using System;
using System.Collections.Generic;
using SBGames.Loop;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Placement
{
    public enum GridPlaneOrientation
    {
        /// <summary>Standard 2D plane: X = Horizontal, Y = Vertical, Z = Sorting/Depth.</summary>
        XY_2D = 0,

        /// <summary>Top-down 3D plane: X = Horizontal, Z = Depth, Y = Elevation.</summary>
        XZ_3D = 1
    }

    /// <summary>
    /// Unity MonoBehaviour adapter and scene coordinator for DungeonPlacementSystem.
    /// Exposes inspector parameters, manages GameObject lifecycles on the grid,
    /// and dispatches UnityEvents for UI and audio systems.
    /// </summary>
    public class DungeonPlacementManager : MonoBehaviour
    {
        public static DungeonPlacementManager Instance { get; private set; }

        [Header("Grid Configuration")]
        [Tooltip("Orientation of the grid. Use XY_2D for 2D games (X horizontal, Y vertical).")]
        [SerializeField] private GridPlaneOrientation planeOrientation = GridPlaneOrientation.XY_2D;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(10, 10);
        [SerializeField] private float cellSize = 1.0f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        public GridPlaneOrientation PlaneOrientation => planeOrientation;
        public Vector2Int GridSize => gridSize;
        public float CellSize => cellSize;
        public Vector3 GridOrigin => gridOrigin;

        [Header("Starting Economy & Limits")]
        [Tooltip("Fallback resources if no DungeonMaster is present.")]
        [SerializeField, Min(0)] private int startingResources = 100;
        [SerializeField, Min(1)] private int maxCapacity = 20;
        [Range(0f, 1f)]
        [Tooltip("Refund ratio when selling or removing units during Planning phase (1.0 = 100% full refund).")]
        [SerializeField] private float removalRefundRatio = 1.0f;

        [Header("Dungeon Master Economy")]
        [SerializeField] private SBGames.Loop.DungeonMaster dungeonMaster;
        [SerializeField] private SBGames.Loop.CoreGameLoop coreGameLoop;

        [Header("Dungeon Phase")]
        [SerializeField] private DungeonPhase initialPhase = DungeonPhase.BuildPhase;

        [Header("Scene Hierarchy")]
        [SerializeField] private Transform unitsParent;

        [Header("Unity Events for UI Hooks")]
        public UnityEvent<int, int> onResourcesChanged = new UnityEvent<int, int>(); // (current, delta)
        public UnityEvent<int, int> onCapacityChanged = new UnityEvent<int, int>();  // (used, max)
        public UnityEvent<DungeonPhase> onPhaseChanged = new UnityEvent<DungeonPhase>();
        public UnityEvent<PlacedUnitInstance> onUnitPlaced = new UnityEvent<PlacedUnitInstance>();
        public UnityEvent<PlacedUnitInstance> onUnitRemoved = new UnityEvent<PlacedUnitInstance>();
        public UnityEvent<string> onPlacementFailed = new UnityEvent<string>();

        // Core system instances
        private DungeonPlacementSystem _system;
        public DungeonPlacementSystem System
        {
            get
            {
                if (_system == null) InitializeSystem();
                return _system;
            }
            private set => _system = value;
        }

        public DungeonMaster DungeonMaster => System?.DungeonMaster ?? dungeonMaster;
        public int CurrentEnergy => DungeonMaster != null ? DungeonMaster.Energy : 0;
        public ICapacityPool CapacityPool => System?.CapacityPool;
        public IGridOccupancyTracker OccupancyTracker => System?.OccupancyTracker;
        public IDungeonPhaseManager PhaseManager { get; private set; }
        public ICombatPowerProvider CombatPowerProvider { get; set; }
        public IBuildableCellValidator BuildableCellValidator
        {
            get => System?.BuildableCellValidator;
            set
            {
                if (System != null) System.BuildableCellValidator = value;
            }
        }
        public DungeonRoomCapacityTracker RoomCapacityTracker { get; private set; }

        /// <summary>
        /// Reconfigures placement grid dimensions, origin, and cell validator to match generated dungeon bounds.
        /// </summary>
        public void ConfigurePlacementGrid(Vector2Int newGridSize, Vector3 newGridOrigin, float newCellSize, IBuildableCellValidator cellValidator = null)
        {
            gridSize = new Vector2Int(Mathf.Max(1, newGridSize.x), Mathf.Max(1, newGridSize.y));
            gridOrigin = newGridOrigin;
            cellSize = Mathf.Max(0.1f, newCellSize);

            if (_system != null)
            {
                var newTracker = new GridOccupancyTracker(gridSize.x, gridSize.y);
                var existingUnits = _system.OccupancyTracker?.GetAllPlacedUnits();
                if (existingUnits != null)
                {
                    foreach (var unit in existingUnits)
                    {
                        if (newTracker.IsWithinBounds(unit.GridPosition))
                        {
                            newTracker.RegisterUnit(unit);
                        }
                    }
                }

                System = new DungeonPlacementSystem(
                    DungeonMaster,
                    CapacityPool,
                    newTracker,
                    _system.MultiplierStrategy,
                    PhaseManager,
                    cellValidator ?? _system.BuildableCellValidator,
                    RoomCapacityTracker ?? _system.RoomCapacityTracker,
                    _system.CrowdingStrategy
                );

                System.OnUnitPlaced += HandleUnitPlaced;
                System.OnUnitRemoved += HandleUnitRemoved;
                System.OnPlacementRejected += HandlePlacementRejected;
            }
            else
            {
                InitializeSystem();
                if (cellValidator != null && System != null)
                {
                    System.BuildableCellValidator = cellValidator;
                }
            }

            var visualizer = GetComponent<DungeonGridVisualizer>();
            if (visualizer != null)
            {
                visualizer.RebuildGridMesh();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;

            InitializeSystem();

            if (GetComponent<DungeonGridVisualizer>() == null)
            {
                gameObject.AddComponent<DungeonGridVisualizer>();
            }
        }

        public void InitializeSystem()
        {
            if (_system != null) return;
            if (dungeonMaster == null) dungeonMaster = FindAnyObjectByType<SBGames.Loop.DungeonMaster>();
            if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<SBGames.Loop.CoreGameLoop>();

            if (dungeonMaster == null)
            {
                var dmGo = new GameObject("[DungeonMaster]");
                dungeonMaster = dmGo.AddComponent<SBGames.Loop.DungeonMaster>();
                dungeonMaster.Initialize(startingEssence: 100, startingEnergy: startingResources > 0 ? startingResources : 50);
            }

            var capacityPool = new CapacityPool(maxCapacity);
            var occupancyTracker = new GridOccupancyTracker(gridSize.x, gridSize.y);
            var multiplierStrategy = new LinearCapacityMultiplierStrategy();
            PhaseManager = new SimpleDungeonPhaseManager(initialPhase);
            CombatPowerProvider = new DefaultCombatPowerProvider();
            RoomCapacityTracker = new DungeonRoomCapacityTracker(this);

            System = new DungeonPlacementSystem(
                dungeonMaster,
                capacityPool,
                occupancyTracker,
                multiplierStrategy,
                PhaseManager,
                null,
                RoomCapacityTracker
            );

            // Wire internal events to UnityEvents for UI responsiveness
            dungeonMaster.OnEnergyChanged += HandleDungeonMasterEnergyChanged;
            capacityPool.OnCapacityChanged += (used, max) => onCapacityChanged?.Invoke(used, max);
            PhaseManager.OnPhaseChanged += (phase) => onPhaseChanged?.Invoke(phase);
            System.OnUnitPlaced += HandleUnitPlaced;
            System.OnUnitRemoved += HandleUnitRemoved;
            System.OnPlacementRejected += HandlePlacementRejected;

            // Sync with CoreGameLoop phases if active
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandleCoreLoopPhaseChanged;
                SyncPhaseWithCoreLoop(coreGameLoop.CurrentPhase);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (dungeonMaster != null)
            {
                dungeonMaster.OnEnergyChanged -= HandleDungeonMasterEnergyChanged;
            }

            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandleCoreLoopPhaseChanged;
            }
        }

        private void HandleDungeonMasterEnergyChanged(int currentEnergy, int maxEnergy)
        {
            onResourcesChanged?.Invoke(currentEnergy, 0);
        }

        private void HandleCoreLoopPhaseChanged(SBGames.Loop.GamePhase loopPhase)
        {
            SyncPhaseWithCoreLoop(loopPhase);
        }

        public void SetCoreGameLoop(SBGames.Loop.CoreGameLoop loop)
        {
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged -= HandleCoreLoopPhaseChanged;
            }
            coreGameLoop = loop;
            if (coreGameLoop != null)
            {
                coreGameLoop.OnPhaseChanged += HandleCoreLoopPhaseChanged;
                SyncPhaseWithCoreLoop(coreGameLoop.CurrentPhase);
            }
        }

        private void SyncPhaseWithCoreLoop(SBGames.Loop.GamePhase loopPhase)
        {
            if (PhaseManager == null) return;

            // Planning and NotStarted phases unlock building; Battle locks placement
            var targetDungeonPhase = (loopPhase == SBGames.Loop.GamePhase.Planning || loopPhase == SBGames.Loop.GamePhase.NotStarted)
                ? DungeonPhase.BuildPhase
                : DungeonPhase.CombatPhase;

            PhaseManager.SetPhase(targetDungeonPhase);

            if (targetDungeonPhase == DungeonPhase.BuildPhase)
            {
                CleanupDestroyedUnits();
                if (Bench.BenchManager.Instance != null)
                {
                    Bench.BenchManager.Instance.CleanupDestroyedUnits();
                }
            }
        }

        private void Start()
        {
            // Initial UI sync
            onResourcesChanged?.Invoke(CurrentEnergy, 0);
            onCapacityChanged?.Invoke(CapacityPool.CurrentCapacityUsed, CapacityPool.MaxCapacity);
            onPhaseChanged?.Invoke(PhaseManager.CurrentPhase);
        }

        /// <summary>
        /// Transitions the game between Build and Combat phases.
        /// </summary>
        public void SetPhase(DungeonPhase phase)
        {
            PhaseManager.SetPhase(phase);
        }

        /// <summary>
        /// Validates placement without side effects, querying CombatPowerProvider automatically.
        /// </summary>
        public PlacementValidationResult ValidatePlacementWithCombatPower(
            IUnitPlacementDefinition unitDefinition,
            Vector2Int gridPosition)
        {
            float cp = CombatPowerProvider != null ? CombatPowerProvider.GetCombatPowerFactor(unitDefinition) : 1.0f;
            return ValidatePlacement(unitDefinition, gridPosition, cp);
        }

        /// <summary>
        /// Attempts to place a unit, querying CombatPowerProvider automatically.
        /// </summary>
        public bool TryPlaceUnitWithCombatPower(
            IUnitPlacementDefinition unitDefinition,
            Vector2Int gridPosition,
            out PlacedUnitInstance placedUnit,
            out PlacementValidationResult result)
        {
            float cp = CombatPowerProvider != null ? CombatPowerProvider.GetCombatPowerFactor(unitDefinition) : 1.0f;
            return TryPlaceUnit(unitDefinition, gridPosition, cp, out placedUnit, out result);
        }

        /// <summary>
        /// Validates placement without side effects.
        /// </summary>
        public PlacementValidationResult ValidatePlacement(
            IUnitPlacementDefinition unitDefinition,
            Vector2Int gridPosition,
            float combatPowerFactor = 1.0f)
        {
            var request = new PlacementRequest(unitDefinition, gridPosition, combatPowerFactor);
            return System.ValidatePlacement(request);
        }

        /// <summary>
        /// Attempts to place a unit. Instantiates prefab in scene if successful.
        /// </summary>
        public bool TryPlaceUnit(
            IUnitPlacementDefinition unitDefinition,
            Vector2Int gridPosition,
            float combatPowerFactor,
            out PlacedUnitInstance placedUnit,
            out PlacementValidationResult result)
        {
            var request = new PlacementRequest(unitDefinition, gridPosition, combatPowerFactor);
            bool success = System.TryPlaceUnit(request, out placedUnit, out result);

            if (success && placedUnit != null && unitDefinition.Prefab != null)
            {
                Vector3 worldPos = GridToWorldPosition(gridPosition);
                Transform parent = unitsParent != null ? unitsParent : transform;
                GameObject go = Instantiate(unitDefinition.Prefab, worldPos, Quaternion.identity, parent);
                go.name = $"{unitDefinition.DisplayName}_{placedUnit.InstanceId.Substring(0, 6)}";
                placedUnit.GameObjectInstance = go;
            }

            return success;
        }

        /// <summary>
        /// Removes a placed unit from the grid, releasing capacity and destroying the game object.
        /// </summary>
        public bool TryRemoveUnit(Vector2Int gridPosition)
        {
            if (System.TryRemoveUnit(gridPosition, out var removedUnit, removalRefundRatio))
            {
                if (removedUnit.GameObjectInstance != null)
                {
                    Destroy(removedUnit.GameObjectInstance);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cleans up any placed units whose GameObject instances were destroyed (e.g. killed during battle),
        /// releasing their grid occupancy and capacity pool allocation without refunding energy.
        /// </summary>
        public int CleanupDestroyedUnits()
        {
            if (System == null || OccupancyTracker == null) return 0;

            var allUnits = OccupancyTracker.GetAllPlacedUnits();
            List<PlacedUnitInstance> destroyedList = null;

            foreach (var unit in allUnits)
            {
                if (unit != null && unit.GameObjectInstance == null)
                {
                    destroyedList ??= new List<PlacedUnitInstance>();
                    destroyedList.Add(unit);
                }
            }

            if (destroyedList == null) return 0;

            int count = 0;
            for (int i = 0; i < destroyedList.Count; i++)
            {
                var unit = destroyedList[i];
                if (OccupancyTracker.UnregisterUnit(unit.GridPosition, out _))
                {
                    CapacityPool?.Release(unit.CapacityConsumed);
                    if (RoomCapacityTracker != null)
                    {
                        RoomCapacityTracker.UnregisterUnitFromAnyRoom(unit);
                    }
                    else if (System?.RoomCapacityTracker != null && System.RoomCapacityTracker.HasRoomAt(unit.GridPosition))
                    {
                        System.RoomCapacityTracker.UnregisterUnit(System.RoomCapacityTracker.GetRoomId(unit.GridPosition), unit);
                    }
                    count++;
                }
            }

            if (count > 0 && CapacityPool != null)
            {
                onCapacityChanged?.Invoke(CapacityPool.CurrentCapacityUsed, CapacityPool.MaxCapacity);
            }

            return count;
        }

        private void HandleUnitPlaced(PlacedUnitInstance unit)
        {
            onUnitPlaced?.Invoke(unit);
        }

        private void HandleUnitRemoved(PlacedUnitInstance unit, bool refunded)
        {
            onUnitRemoved?.Invoke(unit);
        }

        private void HandlePlacementRejected(PlacementRequest request, PlacementValidationResult result)
        {
            onPlacementFailed?.Invoke(result.Message);
        }

        /// <summary>
        /// Converts integer grid coordinates into world space coordinates.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            if (planeOrientation == GridPlaneOrientation.XY_2D)
            {
                return gridOrigin + new Vector3(gridPos.x * cellSize, gridPos.y * cellSize, 0f);
            }
            return gridOrigin + new Vector3(gridPos.x * cellSize, 0f, gridPos.y * cellSize);
        }

        /// <summary>
        /// Converts world position into nearest integer grid cell coordinates.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            Vector3 local = worldPos - gridOrigin;
            int x = Mathf.RoundToInt(local.x / cellSize);
            int y = planeOrientation == GridPlaneOrientation.XY_2D
                ? Mathf.RoundToInt(local.y / cellSize)
                : Mathf.RoundToInt(local.z / cellSize);
            return new Vector2Int(x, y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f);
            Vector3 cubeSize = planeOrientation == GridPlaneOrientation.XY_2D
                ? new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.05f)
                : new Vector3(cellSize * 0.95f, 0.05f, cellSize * 0.95f);

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    Vector3 cellCenter = GridToWorldPosition(new Vector2Int(x, y));
                    Gizmos.DrawWireCube(cellCenter, cubeSize);
                }
            }
        }
    }
}
