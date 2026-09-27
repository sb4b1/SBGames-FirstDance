using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Core placement engine for the roguelike dungeon builder.
    /// Coordinates resource costs, capacity limits, combat power multipliers, and grid occupancy.
    /// Pure C# system designed for zero side-effect pre-validation and deterministic execution.
    /// </summary>
    public class DungeonPlacementSystem
    {
        public IResourceWallet Wallet { get; }
        public ICapacityPool CapacityPool { get; }
        public IGridOccupancyTracker OccupancyTracker { get; }
        public ICapacityMultiplierStrategy MultiplierStrategy { get; set; }
        public IDungeonPhaseManager PhaseManager { get; set; }

        public event Action<PlacedUnitInstance> OnUnitPlaced;
        public event Action<PlacedUnitInstance, bool> OnUnitRemoved; // (unit, wasRefunded)
        public event Action<PlacementRequest, PlacementValidationResult> OnPlacementRejected;

        public DungeonPlacementSystem(
            IResourceWallet wallet,
            ICapacityPool capacityPool,
            IGridOccupancyTracker occupancyTracker,
            ICapacityMultiplierStrategy multiplierStrategy = null,
            IDungeonPhaseManager phaseManager = null)
        {
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            CapacityPool = capacityPool ?? throw new ArgumentNullException(nameof(capacityPool));
            OccupancyTracker = occupancyTracker ?? throw new ArgumentNullException(nameof(occupancyTracker));
            MultiplierStrategy = multiplierStrategy ?? new LinearCapacityMultiplierStrategy();
            PhaseManager = phaseManager;
        }

        /// <summary>
        /// Pure validation of a placement request without mutating any system state.
        /// Evaluates grid constraints, resource costs, and capacity consumption with CP multiplier.
        /// </summary>
        /// <param name="request">Unit placement intent details.</param>
        /// <returns>Validation outcome detailing success or precise failure constraint.</returns>
        public PlacementValidationResult ValidatePlacement(in PlacementRequest request)
        {
            // 1. Basic structural validity
            if (request.UnitDefinition == null)
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.InvalidRequest,
                    "Unit definition is null or missing.",
                    0, 0, Wallet.CurrentResources, CapacityPool.AvailableCapacity
                );
            }

            // 2. Phase restrictions (Build Phase vs Combat Phase)
            if (PhaseManager != null && !PhaseManager.CanPlaceOrModifyUnits)
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.PhaseLocked,
                    $"Placement locked during {PhaseManager.CurrentPhase}. Only allowed during BuildPhase.",
                    request.UnitDefinition.BaseCost,
                    0,
                    Wallet.CurrentResources,
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
                            Wallet.CurrentResources,
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
                            Wallet.CurrentResources,
                            CapacityPool.AvailableCapacity
                        );
                    }
                }
            }

            // 3. Constraint 1: Placement Cost Check
            int requiredCost = request.UnitDefinition.BaseCost;
            if (!Wallet.CanAfford(requiredCost))
            {
                int effectiveCapPreview = MultiplierStrategy.CalculateEffectiveCapacity(
                    request.UnitDefinition.BaseCapacity,
                    request.CombatPowerFactor
                );

                return PlacementValidationResult.FailureResult(
                    PlacementStatus.InsufficientResources,
                    $"Insufficient resources. Required: {requiredCost}, Available: {Wallet.CurrentResources}.",
                    costRequired: requiredCost,
                    capacityRequired: effectiveCapPreview,
                    availableResources: Wallet.CurrentResources,
                    availableCapacity: CapacityPool.AvailableCapacity
                );
            }

            // 4. Constraint 3 & Constraint 2: Dynamic Capacity Multiplier & Capacity Limit Check
            int effectiveCapacity = MultiplierStrategy.CalculateEffectiveCapacity(
                request.UnitDefinition.BaseCapacity,
                request.CombatPowerFactor
            );

            if (!CapacityPool.CanAccommodate(effectiveCapacity))
            {
                return PlacementValidationResult.FailureResult(
                    PlacementStatus.ExceedsCapacityLimit,
                    $"Exceeds dungeon capacity pool. Required: {effectiveCapacity} (Base: {request.UnitDefinition.BaseCapacity} x CP Factor: {request.CombatPowerFactor:F2}), Available: {CapacityPool.AvailableCapacity}/{CapacityPool.MaxCapacity}.",
                    costRequired: requiredCost,
                    capacityRequired: effectiveCapacity,
                    availableResources: Wallet.CurrentResources,
                    availableCapacity: CapacityPool.AvailableCapacity
                );
            }

            // All 3 constraints and spatial checks passed!
            return PlacementValidationResult.SuccessResult(
                costRequired: requiredCost,
                capacityRequired: effectiveCapacity,
                availableResources: Wallet.CurrentResources,
                availableCapacity: CapacityPool.AvailableCapacity
            );
        }

        /// <summary>
        /// Attempts to execute unit placement.
        /// Atomically applies cost deduction, capacity consumption, and grid registration if valid.
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

            bool deducted = Wallet.TryDeduct(cost);
            if (!deducted)
            {
                // Safety guard in case of external race condition
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.InsufficientResources,
                    "Failed to deduct placement cost.",
                    cost, capacity, Wallet.CurrentResources, CapacityPool.AvailableCapacity
                );
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            bool allocated = CapacityPool.TryAllocate(capacity);
            if (!allocated)
            {
                // Rollback wallet deduction
                Wallet.Add(cost);
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.ExceedsCapacityLimit,
                    "Failed to allocate capacity during execution.",
                    cost, capacity, Wallet.CurrentResources, CapacityPool.AvailableCapacity
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
                // Rollback both capacity and wallet
                CapacityPool.Release(capacity);
                Wallet.Add(cost);
                placedUnit = null;
                validationResult = PlacementValidationResult.FailureResult(
                    PlacementStatus.CellOccupied,
                    "Failed to register unit on grid.",
                    cost, capacity, Wallet.CurrentResources, CapacityPool.AvailableCapacity
                );
                OnPlacementRejected?.Invoke(request, validationResult);
                return false;
            }

            OnUnitPlaced?.Invoke(placedUnit);
            return true;
        }

        /// <summary>
        /// Removes an existing placed unit from the grid, frees its capacity, and optionally refunds cost.
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

            // Process optional resource refund
            bool isRefunded = false;
            if (refundRatio > 0f && removedUnit.CostPaid > 0)
            {
                int refundAmount = Mathf.RoundToInt(removedUnit.CostPaid * Mathf.Clamp01(refundRatio));
                if (refundAmount > 0)
                {
                    Wallet.Add(refundAmount);
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
