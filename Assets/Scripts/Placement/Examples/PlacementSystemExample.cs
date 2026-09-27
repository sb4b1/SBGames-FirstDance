using System;
using UnityEngine;

namespace SBGames.Placement.Examples
{
    /// <summary>
    /// Demonstrates and validates the three integrated placement constraints:
    /// 1. Placement Cost System (Resource deduction & validation)
    /// 2. Unit Capacity Pool (Dungeon ceiling & tracking)
    /// 3. Dynamic Capacity Multiplier (Combat Power factor scaling)
    /// </summary>
    public class PlacementSystemExample : MonoBehaviour
    {
        [Header("Run on Start")]
        [SerializeField] private bool runDemonstrationOnStart = true;

        private void Start()
        {
            if (runDemonstrationOnStart)
            {
                RunConstraintDemonstration();
            }
        }

        [ContextMenu("Run Placement Demonstration")]
        public void RunConstraintDemonstration()
        {
            Debug.Log("===============================================================");
            Debug.Log("=== ROGUELIKE DUNGEON BUILDER: UNIT PLACEMENT SYSTEM TEST   ===");
            Debug.Log("===============================================================");

            // 1. Setup Environment
            // Initial Economy: 100 Gold, 6 Dungeon Capacity, 5x5 Grid
            var wallet = new ResourceWallet(initialResources: 100);
            var capacityPool = new CapacityPool(maxCapacity: 6);
            var occupancyTracker = new GridOccupancyTracker(5, 5);
            var multiplier = new LinearCapacityMultiplierStrategy();
            var phaseManager = new SimpleDungeonPhaseManager(DungeonPhase.BuildPhase);

            var system = new DungeonPlacementSystem(wallet, capacityPool, occupancyTracker, multiplier, phaseManager);

            // Unit Type Definitions (In-memory mock for testing, or ScriptableObjects in production)
            var scoutDef = new SimpleUnitDefinition("unit_scout", "Goblin Scout", baseCost: 15, baseCapacity: 1);
            var berserkerDef = new SimpleUnitDefinition("unit_berserker", "Orc Berserker", baseCost: 40, baseCapacity: 2);
            var dragonDef = new SimpleUnitDefinition("unit_dragon", "Shadow Dragon", baseCost: 90, baseCapacity: 4);

            LogStatus(system, "Initial State");

            // ---------------------------------------------------------------------------------
            // SCENARIO 1: Standard Placement (Combat Power Factor = 1.0)
            // Base Cost: 15, Base Cap: 1 -> Consumes 15 Gold and 1 Capacity
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 1] Standard Placement: Goblin Scout at (1, 1), CP Factor 1.0 ---");
            var req1 = new PlacementRequest(scoutDef, new Vector2Int(1, 1), combatPowerFactor: 1.0f);

            // Validation before commit (side-effect free)
            var val1 = system.ValidatePlacement(req1);
            Debug.Log($"Pre-validation: {val1}");

            if (system.TryPlaceUnit(req1, out var placedUnit1, out var res1))
            {
                Debug.Log($"SUCCESS: Placed {placedUnit1}");
            }
            LogStatus(system, "After Scenario 1");

            // ---------------------------------------------------------------------------------
            // SCENARIO 2: Capacity Multiplier Scaling (Combat Power Factor = 2.5)
            // Base Cost: 40, Base Cap: 2 -> Scaled Cap = Ceil(2 * 2.5) = 5 Capacity!
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 2] Dynamic Multiplier: Orc Berserker at (2, 2), CP Factor 2.5 ---");
            Debug.Log($"Orc Base Capacity: {berserkerDef.BaseCapacity}, External CP Factor: 2.5 -> Expected Capacity: 5");
            var req2 = new PlacementRequest(berserkerDef, new Vector2Int(2, 2), combatPowerFactor: 2.5f);

            if (system.TryPlaceUnit(req2, out var placedUnit2, out var res2))
            {
                Debug.Log($"SUCCESS: Placed {placedUnit2}");
            }
            LogStatus(system, "After Scenario 2");

            // ---------------------------------------------------------------------------------
            // SCENARIO 3: Constraint 2 Rejection - Dungeon Capacity Limit Exceeded
            // Current Used Capacity: 1 + 5 = 6/6. Any new unit will exceed capacity limit!
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 3] Capacity Rejection: Attempt Goblin Scout at (3, 3), CP Factor 1.0 ---");
            var req3 = new PlacementRequest(scoutDef, new Vector2Int(3, 3), combatPowerFactor: 1.0f);

            var val3 = system.ValidatePlacement(req3);
            Debug.Log($"Pre-validation Check: IsValid={val3.IsValid}, Reason={val3.Status}\nMessage: {val3.Message}");

            bool success3 = system.TryPlaceUnit(req3, out var placedUnit3, out var res3);
            Debug.Log($"TryPlaceUnit Result: {success3} (Expected: false). Reason: {res3.Status}");
            LogStatus(system, "After Scenario 3 (State unchanged)");

            // ---------------------------------------------------------------------------------
            // SCENARIO 4: Constraint 1 Rejection - Insufficient Resources
            // Player has 45 Gold left (100 - 15 - 40). Dragon costs 90 Gold.
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 4] Resource Rejection: Attempt Shadow Dragon at (0, 0), Cost 90 vs 45 Gold ---");
            var req4 = new PlacementRequest(dragonDef, new Vector2Int(0, 0), combatPowerFactor: 1.0f);

            var val4 = system.ValidatePlacement(req4);
            Debug.Log($"Pre-validation Check: IsValid={val4.IsValid}, Reason={val4.Status}\nMessage: {val4.Message}");

            bool success4 = system.TryPlaceUnit(req4, out var placedUnit4, out var res4);
            Debug.Log($"TryPlaceUnit Result: {success4} (Expected: false). Reason: {res4.Status}");
            LogStatus(system, "After Scenario 4 (State unchanged)");

            // ---------------------------------------------------------------------------------
            // SCENARIO 5: Spatial Rejection - Cell Already Occupied
            // Target (1, 1) already has Goblin Scout from Scenario 1.
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 5] Spatial Rejection: Placing on occupied tile (1, 1) ---");
            var req5 = new PlacementRequest(scoutDef, new Vector2Int(1, 1), combatPowerFactor: 1.0f);

            var val5 = system.ValidatePlacement(req5);
            Debug.Log($"Pre-validation Check: IsValid={val5.IsValid}, Reason={val5.Status}\nMessage: {val5.Message}");

            // ---------------------------------------------------------------------------------
            // SCENARIO 6: Removal and Capacity Reclamation
            // Remove Orc Berserker at (2, 2) -> Frees 5 Capacity and provides 50% refund (20 Gold)
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 6] Unit Removal: Selling Orc Berserker at (2, 2) with 50% refund ---");
            if (system.TryRemoveUnit(new Vector2Int(2, 2), out var removedUnit, refundRatio: 0.5f))
            {
                Debug.Log($"Removed {removedUnit.Definition.DisplayName}. Reclaimed {removedUnit.CapacityConsumed} Capacity!");
            }
            LogStatus(system, "After Scenario 6 (Orc removed)");

            // ---------------------------------------------------------------------------------
            // SCENARIO 7: Placement now succeeds with reclaimed capacity!
            // Placing Goblin Scout at (3, 3) was rejected in Scenario 3, now it should succeed.
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 7] Retry Placement: Goblin Scout at (3, 3) after capacity freed ---");
            if (system.TryPlaceUnit(req3, out var placedUnit7, out var res7))
            {
                Debug.Log($"SUCCESS: Placed {placedUnit7}");
            }
            LogStatus(system, "After Scenario 7");

            // ---------------------------------------------------------------------------------
            // SCENARIO 8: Phase Restriction (Combat Phase Lock)
            // Transition dungeon phase to CombatPhase -> All placement and selling locked!
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 8] Phase Lock: Transition to CombatPhase ---");
            phaseManager.SetPhase(DungeonPhase.CombatPhase);
            var req8 = new PlacementRequest(scoutDef, new Vector2Int(0, 0), combatPowerFactor: 1.0f);
            var val8 = system.ValidatePlacement(req8);
            Debug.Log($"Pre-validation in CombatPhase: IsValid={val8.IsValid}, Reason={val8.Status}\nMessage: {val8.Message}");
            bool success8 = system.TryPlaceUnit(req8, out var placed8, out var res8);
            Debug.Log($"TryPlaceUnit Result during CombatPhase: {success8} (Expected: false). Reason: {res8.Status}");

            // Return to BuildPhase
            phaseManager.SetPhase(DungeonPhase.BuildPhase);

            // ---------------------------------------------------------------------------------
            // SCENARIO 9: Multi-Tile Footprint Checking (2x2 Boss)
            // A 2x2 unit placed at (0, 0) occupies (0,0), (1,0), (0,1), (1,1).
            // Tile (1, 1) has Goblin Scout from Scenario 1 -> Multi-tile check rejects!
            // ---------------------------------------------------------------------------------
            Debug.Log("\n--- [Scenario 9] Footprint Collision: 2x2 Boss overlaps occupied (1, 1) ---");
            var bossDef = new SimpleUnitDefinition("unit_golem", "Ancient Golem", baseCost: 30, baseCapacity: 2, new Vector2Int(2, 2));
            var req9 = new PlacementRequest(bossDef, new Vector2Int(0, 0), combatPowerFactor: 1.0f);
            var val9 = system.ValidatePlacement(req9);
            Debug.Log($"Pre-validation for 2x2 Footprint: IsValid={val9.IsValid}, Reason={val9.Status}\nMessage: {val9.Message}");

            LogStatus(system, "Final State");
            Debug.Log("===============================================================");
        }

        private void LogStatus(DungeonPlacementSystem sys, string label)
        {
            Debug.Log($"[{label}] Gold: {sys.Wallet.CurrentResources} | Capacity: {sys.CapacityPool.CurrentCapacityUsed}/{sys.CapacityPool.MaxCapacity} (Free: {sys.CapacityPool.AvailableCapacity}) | Units Placed: {sys.GetAllPlacedUnits().Count}");
        }

        /// <summary>
        /// Lightweight mock unit definition for standalone testing without assets.
        /// </summary>
        private class SimpleUnitDefinition : IUnitPlacementDefinition
        {
            public string UnitId { get; }
            public string DisplayName { get; }
            public int BaseCost { get; }
            public int BaseCapacity { get; }
            public Vector2Int Footprint { get; }
            public GameObject Prefab => null;

            public SimpleUnitDefinition(string id, string name, int baseCost, int baseCapacity, Vector2Int? footprint = null)
            {
                UnitId = id;
                DisplayName = name;
                BaseCost = baseCost;
                BaseCapacity = baseCapacity;
                Footprint = footprint ?? Vector2Int.one;
            }
        }
    }
}
