using System;
using System.Collections.Generic;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Core placement engine for the roguelike dungeon builder.
    /// Coordinates unit energy costs directly with DungeonMaster, capacity limits, combat power multipliers, and grid occupancy.
    /// Pure C# system designed for zero side-effect pre-validation and deterministic execution.
    /// </summary>
    public class DungeonPlacementSystem
    {
        public DungeonMaster DungeonMaster { get; }
        public ICapacityPool CapacityPool { get; }
        public IGridOccupancyTracker OccupancyTracker { get; }
        public ICapacityMultiplierStrategy MultiplierStrategy { get; set; }
        public IDungeonPhaseManager PhaseManager { get; set; }
        public IBuildableCellValidator BuildableCellValidator { get; set; }
        public IRoomCapacityTracker RoomCapacityTracker { get; set; }
        public IRoomCrowdingPenaltyStrategy CrowdingStrategy { get; set; }

        public event Action<PlacedUnitInstance> OnUnitPlaced;
        public event Action<PlacedUnitInstance, bool> OnUnitRemoved; // (unit, wasRefunded)
        public event Action<PlacementRequest, PlacementValidationResult> OnPlacementRejected;

        public DungeonPlacementSystem(
            DungeonMaster dungeonMaster,
            ICapacityPool capacityPool,
            IGridOccupancyTracker occupancyTracker,
            ICapacityMultiplierStrategy multiplierStrategy = null,
            IDungeonPhaseManager phaseManager = null,
            IBuildableCellValidator buildableCellValidator = null,
            IRoomCapacityTracker roomCapacityTracker = null,
            IRoomCrowdingPenaltyStrategy crowdingStrategy = null)
        {
            DungeonMaster = dungeonMaster ?? throw new ArgumentNullException(nameof(dungeonMaster));
            CapacityPool = capacityPool ?? throw new ArgumentNullException(nameof(capacityPool));
            OccupancyTracker = occupancyTracker ?? throw new ArgumentNullException(nameof(occupancyTracker));
            MultiplierStrategy = multiplierStrategy ?? new LinearCapacityMultiplierStrategy();
            PhaseManager = phaseManager;
            BuildableCellValidator = buildableCellValidator;
            RoomCapacityTracker = roomCapacityTracker;
            CrowdingStrategy = crowdingStrategy ?? new LinearRoomCrowdingPenaltyStrategy();
        }

        /// <summary>
        /// Pure validation of a placement request without mutating any system state.
        /// Evaluates grid constraints, energy costs via DungeonMaster, and capacity consumption with CP multiplier.
        /// </summary>
        /// <param name="request">Unit placement intent details.</param>
        /// <returns>Validation outcome detailing success or precise failure constraint.</returns>
        public PlacementValidationResult ValidatePlacement(in PlacementRequest request)
        {
            int availableEnergy = DungeonMaster != null ? DungeonMaster.Energy : 0;

            // 1. Basic structural validity
            if (request.UnitDefinition == null)
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.InvalidRequest,
                    "Unit definition is null or missing.",
                    0, 0, availableEnergy, CapacityPool.AvailableCapacity
                );
            }

            // 2. Phase restrictions (Build / Planning Phase vs Combat Phase)
            if (PhaseManager != null && !PhaseManager.CanPlaceOrModifyUnits)
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.PhaseLocked,
                    $"Placement locked during {PhaseManager.CurrentPhase}. Only allowed during Planning/Build phase.",
                    request.UnitDefinition.BaseCost,
                    0,
                    availableEnergy,
                    CapacityPool.AvailableCapacity
                );
            }

            // 3. Spatial grid & footprint constraints
            Vector2Int fp = request.UnitDefinition.Footprint;
            for (int fx = 0; fx < fp.x; fx++)
            {
                for (int fy = 0; fy < fp.y; fy++)
                {
                    Vector2Int checkPos = new Vector2Int(request.GridPosition.x + fx, request.GridPosition.y + fy);
                    if (!OccupancyTracker.IsWithinBounds(checkPos))
                    {
                        return PlacementValidationResult.FailureResult(
                            PlacementStatus.OutOfBounds,
                            $"Footprint tile {checkPos} is out of dungeon bounds {OccupancyTracker.GridSize}.",
                            request.UnitDefinition.BaseCost,
                            0,
                            availableEnergy,
                            CapacityPool.AvailableCapacity
                        );
                    }

                    if (BuildableCellValidator != null && !BuildableCellValidator.IsCellBuildable(checkPos))
                    {
                        return PlacementValidationResult.FailureResult(
                            PlacementStatus.NonBuildableCell,
                            $"Grid cell {checkPos} is non-buildable (e.g. Entrance room or restricted area).",
                            request.UnitDefinition.BaseCost,
                            0,
                            availableEnergy,
                            CapacityPool.AvailableCapacity
                        );
                    }

                    if (OccupancyTracker.IsOccupied(checkPos))
                    {
                        return PlacementValidationResult.FailureResult(
                            PlacementStatus.CellOccupied,
                            $"Grid cell {checkPos} is already occupied.",
                            request.UnitDefinition.BaseCost,
                            0,
                            availableEnergy,
                            CapacityPool.AvailableCapacity
                        );
                    }
                }
            }

            // 4. Room capacity & crowding constraints
            int effectiveCapacity = MultiplierStrategy.CalculateEffectiveCapacity(
                request.UnitDefinition.BaseCapacity,
                request.CombatPowerFactor
            );

            int crowdingSurcharge = 0;
            if (RoomCapacityTracker != null && RoomCapacityTracker.HasRoomAt(request.GridPosition))
            {
                int roomId = RoomCapacityTracker.GetRoomId(request.GridPosition);
                if (!RoomCapacityTracker.CanAccommodate(roomId, effectiveCapacity))
                {
                    return PlacementValidationResult.FailureResult(
                        PlacementStatus.ExceedsRoomCapacity,
                        $"Room #{roomId} has reached its maximum monster capacity limit ({RoomCapacityTracker.GetRoomCapacityUsed(roomId)}/{RoomCapacityTracker.GetRoomMaxCapacity(roomId)}).",
                        costRequired: request.UnitDefinition.BaseCost,
                        capacityRequired: effectiveCapacity,
                        availableResources: availableEnergy,
                        availableCapacity: CapacityPool.AvailableCapacity
                    );
                }

                if (CrowdingStrategy != null)
                {
                    crowdingSurcharge = CrowdingStrategy.CalculateCrowdingCostSurcharge(
                        RoomCapacityTracker.GetRoomCapacityUsed(roomId),
                        RoomCapacityTracker.GetRoomSoftCapacity(roomId),
                        RoomCapacityTracker.GetRoomMaxCapacity(roomId),
                        request.UnitDefinition.BaseCost
                    );
                }
            }

            // 5. Energy Cost Check (via DungeonMaster, including crowding surcharge)
            int requiredCost = request.UnitDefinition.BaseCost + crowdingSurcharge;
            if (availableEnergy < requiredCost)
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.InsufficientEnergy,
                    crowdingSurcharge > 0
                        ? $"Insufficient Energy. Required: {requiredCost} (Base: {request.UnitDefinition.BaseCost} + Crowding Surcharge: {crowdingSurcharge}), Available: {availableEnergy}."
                        : $"Insufficient Energy. Required: {requiredCost}, Available: {availableEnergy}.",
                    costRequired: requiredCost,
                    capacityRequired: effectiveCapacity,
                    availableResources: availableEnergy,
                    availableCapacity: CapacityPool.AvailableCapacity
                );
            }

            // 6. Global Dungeon Capacity Limit Check
            if (!CapacityPool.CanAccommodate(effectiveCapacity))
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.ExceedsCapacityLimit,
                    $"Exceeds dungeon capacity pool. Required: {effectiveCapacity} (Base: {request.UnitDefinition.BaseCapacity} x CP Factor: {request.CombatPowerFactor:F2}), Available: {CapacityPool.AvailableCapacity}/{CapacityPool.MaxCapacity}.",
                    costRequired: requiredCost,
                    capacityRequired: effectiveCapacity,
                    availableResources: availableEnergy,
                    availableCapacity: CapacityPool.AvailableCapacity
                );
            }

            // All constraints and spatial checks passed!
            return PlacementValidationResult.SuccessResult(
                costRequired: requiredCost,
                capacityRequired: effectiveCapacity,
                availableResources: availableEnergy,
                availableCapacity: CapacityPool.AvailableCapacity
            );
        }

        /// <summary>
        /// Attempts to execute unit placement.
        /// Atomically applies energy deduction from DungeonMaster, capacity consumption, and grid registration if valid.
        /// Leaves state completely untouched if any constraint check fails.
        /// </summary>
        public bool TryPlaceUnit(
            in PlacementRequest request,
            out PlacedUnitInstance placedUnit,
            out PlacementValidationResult validationResult)
        {
            validationResult = ValidatePlacement(request);

            if (!validationResult.IsValid)
            {
                placedUnit = null;
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            // All checks passed -> atomically apply mutations
            int cost = validationResult.CostRequired;
            int capacity = validationResult.CapacityRequired;

            bool deducted = DungeonMaster.SpendEnergy(cost);
            if (!deducted)
            {
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.InsufficientEnergy,
                    "Failed to deduct energy cost from DungeonMaster.",
                    cost, capacity, DungeonMaster.Energy, CapacityPool.AvailableCapacity
                );
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            bool allocated = CapacityPool.TryAllocate(capacity);
            if (!allocated)
            {
                // Rollback energy deduction
                DungeonMaster.GainEnergy(cost);
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.ExceedsCapacityLimit,
                    "Failed to allocate capacity during execution.",
                    cost, capacity, DungeonMaster.Energy, CapacityPool.AvailableCapacity
                );
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            placedUnit = new PlacedUnitInstance(
                instanceId: null,
                definition: request.UnitDefinition,
                gridPosition: request.GridPosition,
                combatPowerFactor: request.CombatPowerFactor,
                costPaid: cost,
                capacityConsumed: capacity
            );

            bool registered = OccupancyTracker.RegisterUnit(placedUnit);
            if (!registered)
            {
                // Rollback both capacity and energy
                CapacityPool.Release(capacity);
                DungeonMaster.GainEnergy(cost);
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.CellOccupied,
                    "Failed to register unit on grid.",
                    cost, capacity, DungeonMaster.Energy, CapacityPool.AvailableCapacity
                );
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            // Register unit with room capacity tracker if inside a room
            if (RoomCapacityTracker != null && RoomCapacityTracker.HasRoomAt(request.GridPosition))
            {
                int roomId = RoomCapacityTracker.GetRoomId(request.GridPosition);
                RoomCapacityTracker.RegisterUnit(roomId, placedUnit);
            }

            OnUnitPlaced?.Invoke(placedUnit);
            return true;
        }

        /// <summary>
        /// Removes an existing placed unit from the grid, frees its capacity, and optionally refunds energy to DungeonMaster.
        /// </summary>
        /// <param name="gridPosition">Tile location of the unit.</param>
        /// <param name="removedUnit">The unit that was removed, or null if none was found.</param>
        /// <param name="refundRatio">0.0 for no refund, 1.0 for full refund.</param>
        /// <returns>True if a unit was found and removed, false otherwise.</returns>
        public bool TryRemoveUnit(
            Vector2Int gridPosition,
            out PlacedUnitInstance removedUnit,
            float refundRatio = 0f)
        {
            if (PhaseManager != null && !PhaseManager.CanPlaceOrModifyUnits)
            {
                removedUnit = null;
                return false;
            }

            if (!OccupancyTracker.UnregisterUnit(gridPosition, out removedUnit))
            {
                return false;
            }

            // Release exact capacity consumed by this unit instance
            CapacityPool.Release(removedUnit.CapacityConsumed);

            // Unregister unit from room capacity tracker if inside a room
            if (RoomCapacityTracker != null && RoomCapacityTracker.HasRoomAt(gridPosition))
            {
                int roomId = RoomCapacityTracker.GetRoomId(gridPosition);
                RoomCapacityTracker.UnregisterUnit(roomId, removedUnit);
            }

            // Process optional energy refund directly to DungeonMaster
            bool isRefunded = false;
            if (refundRatio > 0f && removedUnit.CostPaid > 0)
            {
                int refundAmount = Mathf.RoundToInt(removedUnit.CostPaid * Mathf.Clamp01(refundRatio));
                if (refundAmount > 0)
                {
                    DungeonMaster.GainEnergy(refundAmount);
                    isRefunded = true;
                }
            }

            OnUnitRemoved?.Invoke(removedUnit, isRefunded);
            return true;
        }

        /// <summary>
        /// Returns all currently placed units in the dungeon.
        /// </summary>
        public IReadOnlyCollection<PlacedUnitInstance> GetAllPlacedUnits()
        {
            return OccupancyTracker.GetAllPlacedUnits();
        }
    }
}
