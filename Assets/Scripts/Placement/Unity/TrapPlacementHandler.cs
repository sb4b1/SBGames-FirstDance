using System;
using SBGames.Characters.Traps;
using SBGames.Loop;
using SBGames.Placement.Bench;
using UnityEngine;
using UnityEngine.Events;

namespace SBGames.Placement
{
    /// <summary>
    /// Handles advanced placement interactions including free grid relocation, grid-to-grid swapping,
    /// bench-to-grid transfers, and selling/refunding traps and monsters during the Planning phase.
    /// Strictly locks all modifications during Combat / Battle phases.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrapPlacementHandler : MonoBehaviour
    {
        public static TrapPlacementHandler Instance { get; private set; }

        [Header("System References")]
        [SerializeField] private DungeonPlacementManager placementManager;
        [SerializeField] private BenchManager benchManager;
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonMaster dungeonMaster;

        [Header("Sell / Refund Settings")]
        [Tooltip("Transform defining the Sell Zone trigger/area for drag-and-drop selling (optional).")]
        [SerializeField] private Collider2D sellZoneCollider;

        [Header("Unity Events for UI & Audio")]
        public UnityEvent<PlacedUnitInstance, Vector2Int, Vector2Int> onUnitRelocated = new(); // (unit, fromPos, toPos)
        public UnityEvent<PlacedUnitInstance, PlacedUnitInstance> onUnitsSwapped = new();      // (unitA, unitB)
        public UnityEvent<IUnitPlacementDefinition, int> onUnitSold = new();                   // (definition, refundAmount)
        public UnityEvent<string> onActionRejected = new();

        public DungeonPlacementManager PlacementManager
        {
            get
            {
                if (placementManager == null) placementManager = DungeonPlacementManager.Instance ?? FindAnyObjectByType<DungeonPlacementManager>();
                return placementManager;
            }
            set => placementManager = value;
        }

        public BenchManager BenchManager
        {
            get
            {
                if (benchManager == null) benchManager = BenchManager.Instance ?? FindAnyObjectByType<BenchManager>();
                return benchManager;
            }
            set => benchManager = value;
        }

        public CoreGameLoop CoreLoop
        {
            get
            {
                if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
                return coreGameLoop;
            }
            set => coreGameLoop = value;
        }

        public DungeonMaster DM
        {
            get
            {
                if (dungeonMaster == null) dungeonMaster = FindAnyObjectByType<DungeonMaster>() ?? PlacementManager?.DungeonMaster;
                return dungeonMaster;
            }
            set => dungeonMaster = value;
        }

        public bool CanModify => IsPlanningPhase();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;

            ResolveDependencies();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Initialize(
            DungeonPlacementManager pm = null,
            BenchManager bm = null,
            CoreGameLoop loop = null,
            DungeonMaster dm = null)
        {
            if (pm != null) placementManager = pm;
            if (bm != null) benchManager = bm;
            if (loop != null) coreGameLoop = loop;
            if (dm != null) dungeonMaster = dm;
        }

        public void ResolveDependencies()
        {
            if (placementManager == null) placementManager = DungeonPlacementManager.Instance ?? FindAnyObjectByType<DungeonPlacementManager>();
            if (benchManager == null) benchManager = BenchManager.Instance ?? FindAnyObjectByType<BenchManager>();
            if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            if (dungeonMaster == null) dungeonMaster = FindAnyObjectByType<DungeonMaster>();
        }

        private bool IsPlanningPhase()
        {
            if (PlacementManager != null && PlacementManager.PhaseManager != null)
            {
                return PlacementManager.PhaseManager.CanPlaceOrModifyUnits;
            }

            if (CoreLoop != null)
            {
                return CoreLoop.CurrentPhase == GamePhase.Planning || CoreLoop.CurrentPhase == GamePhase.NotStarted;
            }

            return true;
        }

        #region Free Grid Relocation & Swapping

        /// <summary>
        /// Relocates a placed unit from one grid cell to another at zero gold cost during the Planning phase.
        /// </summary>
        public bool TryRelocateUnit(Vector2Int fromPos, Vector2Int toPos)
        {
            if (!CanModify)
            {
                RejectAction("Cannot relocate units outside the Planning phase.");
                return false;
            }

            if (fromPos == toPos) return true;

            var tracker = PlacementManager?.OccupancyTracker;
            if (tracker == null) return false;

            if (!tracker.IsWithinBounds(toPos))
            {
                RejectAction($"Target cell {toPos} is out of bounds.");
                return false;
            }

            if (!tracker.TryGetUnitAt(fromPos, out var unit) || unit == null)
            {
                RejectAction($"No unit found at {fromPos}.");
                return false;
            }

            // If target cell is already occupied, perform a swap instead
            if (tracker.IsOccupied(toPos))
            {
                return TrySwapGridUnits(fromPos, toPos);
            }

            // Move unit to target cell
            tracker.UnregisterUnit(fromPos, out _);
            unit.GridPosition = toPos;
            tracker.RegisterUnit(unit);

            // Update world position
            if (unit.GameObjectInstance != null && PlacementManager != null)
            {
                unit.GameObjectInstance.transform.position = PlacementManager.GridToWorldPosition(toPos);
            }

            onUnitRelocated?.Invoke(unit, fromPos, toPos);
            return true;
        }

        /// <summary>
        /// Swaps positions of two units on the grid at zero gold cost during the Planning phase.
        /// </summary>
        public bool TrySwapGridUnits(Vector2Int posA, Vector2Int posB)
        {
            if (!CanModify)
            {
                RejectAction("Cannot swap units outside the Planning phase.");
                return false;
            }

            if (posA == posB) return true;

            var tracker = PlacementManager?.OccupancyTracker;
            if (tracker == null) return false;

            if (!tracker.TryGetUnitAt(posA, out var unitA) || !tracker.TryGetUnitAt(posB, out var unitB))
            {
                RejectAction("Both cells must contain valid units to swap.");
                return false;
            }

            // Unregister both
            tracker.UnregisterUnit(posA, out _);
            tracker.UnregisterUnit(posB, out _);

            // Swap positions
            unitA.GridPosition = posB;
            unitB.GridPosition = posA;

            tracker.RegisterUnit(unitA);
            tracker.RegisterUnit(unitB);

            // Update GameObject transforms
            if (unitA.GameObjectInstance != null && PlacementManager != null)
            {
                unitA.GameObjectInstance.transform.position = PlacementManager.GridToWorldPosition(posB);
            }
            if (unitB.GameObjectInstance != null && PlacementManager != null)
            {
                unitB.GameObjectInstance.transform.position = PlacementManager.GridToWorldPosition(posA);
            }

            onUnitsSwapped?.Invoke(unitA, unitB);
            return true;
        }

        #endregion

        #region Bench & Grid Coordination

        /// <summary>
        /// Moves a unit from the dungeon grid to a bench slot, releasing grid capacity.
        /// </summary>
        public bool TryMoveGridToBench(Vector2Int gridPos, int targetBenchSlot = -1)
        {
            if (!CanModify)
            {
                RejectAction("Cannot move units to bench outside Planning phase.");
                return false;
            }

            if (BenchManager == null)
            {
                RejectAction("BenchManager is not available.");
                return false;
            }

            var tracker = PlacementManager?.OccupancyTracker;
            if (tracker == null || !tracker.TryGetUnitAt(gridPos, out var unit))
            {
                RejectAction($"No unit found at grid position {gridPos}.");
                return false;
            }

            // Check bench capacity
            if (targetBenchSlot >= 0 && targetBenchSlot < BenchManager.Capacity)
            {
                var slot = BenchManager.Slots[targetBenchSlot];
                if (!slot.IsEmpty)
                {
                    // Bench slot is occupied -> Swap bench unit with grid unit
                    return TrySwapGridAndBench(gridPos, targetBenchSlot);
                }
            }
            else if (!BenchManager.HasFreeSlot)
            {
                RejectAction("Bench is full.");
                return false;
            }

            // Unregister from grid and release capacity
            tracker.UnregisterUnit(gridPos, out _);
            PlacementManager.CapacityPool?.Release(unit.CapacityConsumed);

            // Assign to bench
            if (targetBenchSlot >= 0)
            {
                BenchManager.TryAssignToSlot(targetBenchSlot, unit, out _);
            }
            else
            {
                BenchManager.TryAddToBench(unit, out _);
            }

            onUnitRelocated?.Invoke(unit, gridPos, new Vector2Int(-1, -1));
            return true;
        }

        /// <summary>
        /// Moves a unit from a bench slot to a grid cell. Validates capacity and occupancy without re-charging gold cost.
        /// </summary>
        public bool TryMoveBenchToGrid(int benchSlotIndex, Vector2Int targetGridPos)
        {
            if (!CanModify)
            {
                RejectAction("Cannot place units from bench outside Planning phase.");
                return false;
            }

            if (BenchManager == null || PlacementManager == null) return false;

            if (benchSlotIndex < 0 || benchSlotIndex >= BenchManager.Slots.Count)
            {
                RejectAction($"Invalid bench slot index {benchSlotIndex}.");
                return false;
            }

            var slot = BenchManager.Slots[benchSlotIndex];
            if (slot.IsEmpty || slot.UnitInstance == null)
            {
                RejectAction($"Bench slot {benchSlotIndex} is empty.");
                return false;
            }

            var unit = slot.UnitInstance;
            var tracker = PlacementManager.OccupancyTracker;
            var capPool = PlacementManager.CapacityPool;

            if (!tracker.IsWithinBounds(targetGridPos))
            {
                RejectAction($"Target cell {targetGridPos} is out of bounds.");
                return false;
            }

            // If target grid cell is occupied, swap with bench
            if (tracker.IsOccupied(targetGridPos))
            {
                return TrySwapGridAndBench(targetGridPos, benchSlotIndex);
            }

            // Check capacity
            if (capPool != null && !capPool.CanAccommodate(unit.CapacityConsumed))
            {
                RejectAction($"Insufficient capacity to place {unit.Definition.DisplayName} on grid.");
                return false;
            }

            // Remove from bench and allocate capacity
            BenchManager.TryRemoveFromBench(benchSlotIndex, out _);
            capPool?.TryAllocate(unit.CapacityConsumed);

            // Register on grid
            unit.GridPosition = targetGridPos;
            tracker.RegisterUnit(unit);

            if (unit.GameObjectInstance != null)
            {
                unit.GameObjectInstance.transform.position = PlacementManager.GridToWorldPosition(targetGridPos);
            }

            onUnitRelocated?.Invoke(unit, new Vector2Int(-1, -1), targetGridPos);
            return true;
        }

        /// <summary>
        /// Swaps a unit on the grid with a unit currently residing in a bench slot.
        /// </summary>
        public bool TrySwapGridAndBench(Vector2Int gridPos, int benchSlotIndex)
        {
            if (!CanModify)
            {
                RejectAction("Cannot swap between bench and grid outside Planning phase.");
                return false;
            }

            if (BenchManager == null || PlacementManager == null) return false;

            var tracker = PlacementManager.OccupancyTracker;
            var capPool = PlacementManager.CapacityPool;

            if (!tracker.TryGetUnitAt(gridPos, out var gridUnit))
            {
                RejectAction($"No unit found on grid at {gridPos}.");
                return false;
            }

            if (benchSlotIndex < 0 || benchSlotIndex >= BenchManager.Slots.Count) return false;
            var benchUnit = BenchManager.Slots[benchSlotIndex].UnitInstance;
            if (benchUnit == null)
            {
                return TryMoveGridToBench(gridPos, benchSlotIndex);
            }

            // Capacity delta check
            int capDelta = benchUnit.CapacityConsumed - gridUnit.CapacityConsumed;
            if (capDelta > 0 && capPool != null && !capPool.CanAccommodate(capDelta))
            {
                RejectAction($"Cannot swap: Not enough capacity (Requires +{capDelta}).");
                return false;
            }

            // Unregister grid unit
            tracker.UnregisterUnit(gridPos, out _);

            // Adjust capacity
            if (capDelta != 0 && capPool != null)
            {
                if (capDelta > 0) capPool.TryAllocate(capDelta);
                else capPool.Release(-capDelta);
            }

            // Bench assign grid unit
            BenchManager.TryAssignToSlot(benchSlotIndex, gridUnit, out _);

            // Place bench unit onto grid
            benchUnit.GridPosition = gridPos;
            tracker.RegisterUnit(benchUnit);

            if (benchUnit.GameObjectInstance != null)
            {
                benchUnit.GameObjectInstance.transform.position = PlacementManager.GridToWorldPosition(gridPos);
            }

            onUnitsSwapped?.Invoke(gridUnit, benchUnit);
            return true;
        }

        #endregion

        #region Sell / Refund Management

        /// <summary>
        /// Sells a placed unit on the grid, refunding gold/energy to the player and destroying the GameObject.
        /// </summary>
        public bool TrySellGridUnit(Vector2Int gridPos)
        {
            if (!CanModify)
            {
                RejectAction("Cannot sell units outside the Planning phase.");
                return false;
            }

            var tracker = PlacementManager?.OccupancyTracker;
            if (tracker == null || !tracker.TryGetUnitAt(gridPos, out var unit))
            {
                RejectAction($"No unit found at {gridPos} to sell.");
                return false;
            }

            // Unregister and release capacity
            tracker.UnregisterUnit(gridPos, out _);
            PlacementManager.CapacityPool?.Release(unit.CapacityConsumed);

            // Calculate refund
            int refundAmount = CalculateRefundAmount(unit.Definition, unit.CostPaid);
            AwardRefund(refundAmount);

            // Destroy GameObject
            if (unit.GameObjectInstance != null)
            {
                Destroy(unit.GameObjectInstance);
            }

            onUnitSold?.Invoke(unit.Definition, refundAmount);
            return true;
        }

        /// <summary>
        /// Sells a unit stored on the bench, refunding gold/energy and destroying the GameObject.
        /// </summary>
        public bool TrySellBenchUnit(int benchSlotIndex)
        {
            if (!CanModify)
            {
                RejectAction("Cannot sell bench units outside the Planning phase.");
                return false;
            }

            if (BenchManager == null) return false;

            if (!BenchManager.TryRemoveFromBench(benchSlotIndex, out var unit) || unit == null)
            {
                RejectAction($"Bench slot {benchSlotIndex} is empty.");
                return false;
            }

            int refundAmount = CalculateRefundAmount(unit.Definition, unit.CostPaid);
            AwardRefund(refundAmount);

            if (unit.GameObjectInstance != null)
            {
                Destroy(unit.GameObjectInstance);
            }

            onUnitSold?.Invoke(unit.Definition, refundAmount);
            return true;
        }

        private int CalculateRefundAmount(IUnitPlacementDefinition def, int costPaid)
        {
            if (def is TrapDataSO trapData)
            {
                return trapData.SellRefundAmount;
            }

            return costPaid > 0 ? costPaid : def.BaseCost;
        }

        private void AwardRefund(int amount)
        {
            if (amount <= 0) return;

            if (DM != null)
            {
                DM.GainEnergy(amount);
            }
        }

        private void RejectAction(string message)
        {
            Debug.LogWarning($"[TrapPlacementHandler] Action Rejected: {message}");
            onActionRejected?.Invoke(message);
        }

        #endregion
    }
}
