using System;
using System.Collections.Generic;
using SBGames.Characters.Traps;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Placement.Bench;
using UnityEngine;

namespace SBGames.Placement.Examples
{
    /// <summary>
    /// Functional test harness and integration verification suite for the Trap System.
    /// Validates TrapDataSO definitions, TrapController triggers, IDamageable resolution,
    /// living trap destruction, bench coordination, free relocation/swapping, sell/refund, and phase locking.
    /// Can be executed via Inspector ContextMenu or automated test suites.
    /// </summary>
    public class TrapSystemTestRunner : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool runOnStart = false;

        private void Start()
        {
            if (runOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Trap System Tests")]
        public void RunAllTests()
        {
            Debug.Log("==================== STARTING TRAP SYSTEM TEST SUITE ====================");

            TestTrapDataInitialization();
            TestTrapInstantBurstExecution();
            TestTrapCooldownAndCharges();
            TestTrapPhaseLifecycleReset();
            TestLivingTrapDestructibility();
            TestFreeGridRelocationAndSwapping();
            TestBenchCoordinationAndSwapping();
            TestSellRefundCalculations();
            TestPhaseLockingRestrictions();

            Debug.Log("==================== ALL TRAP SYSTEM TESTS COMPLETED SUCCESSFULLY ====================");
        }

        private void TestTrapDataInitialization()
        {
            Debug.Log("\n--- Test 1: TrapDataSO Initialization & Payloads ---");

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            // Set private/serialized fields via reflection or verify defaults
            var payload = trapData.CreateDamagePayload();

            Assert(trapData.Category == EntityCategory.Trap, "EntityCategory must be Trap.");
            Assert(payload.Amount == 25f, "Default damage payload should be 25.");
            Assert(payload.Type == DamageType.Physical, "Default damage type should be Physical.");
            Assert(trapData.SellRefundAmount >= trapData.BaseCost, "SellRefundAmount should default to BaseCost.");

            DestroyImmediate(trapData);
            Debug.Log("Test 1 Passed: TrapDataSO correctly authors attributes and payloads.");
        }

        private void TestTrapInstantBurstExecution()
        {
            Debug.Log("\n--- Test 2: Trap Instant Burst & IDamageable Payload Delivery ---");

            var go = new GameObject("[Test_Trap_Burst]");
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var controller = go.AddComponent<TrapController>();

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            controller.Initialize(trapData);
            controller.ArmForCombatPhase();

            // Create dummy damageable target
            var dummyTarget = new DummyDamageable();

            Assert(controller.IsArmed, "Trap should be armed in Combat phase.");
            Assert(!dummyTarget.WasHit, "Dummy should not be hit initially.");

            // Execute trigger payload
            controller.ExecutePayload(dummyTarget);

            Assert(dummyTarget.WasHit, "Dummy target should have received damage.");
            Assert(dummyTarget.LastDamageTaken.Amount == 25f, "Damage amount received should match trap payload.");
            Assert(controller.IsCoolingDown, "Trap should enter cooldown after firing.");

            DestroyImmediate(trapData);
            DestroyImmediate(go);
            Debug.Log("Test 2 Passed: Instant burst delivers structured IDamageable damage.");
        }

        private void TestTrapCooldownAndCharges()
        {
            Debug.Log("\n--- Test 3: Trap Cooldown & Charge Depletion ---");

            var go = new GameObject("[Test_Trap_Charges]");
            go.AddComponent<BoxCollider2D>();
            var controller = go.AddComponent<TrapController>();

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            // Use reflection or helper to test limited charges
            var field = typeof(TrapDataSO).GetField("maxChargesPerRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(trapData, 2);

            controller.Initialize(trapData);
            controller.ArmForCombatPhase();

            Assert(controller.RemainingCharges == 2, "Remaining charges should initialize to 2.");
            Assert(controller.IsArmed, "Trap should be armed.");

            var dummy = new DummyDamageable();

            // Fire charge 1
            controller.ExecutePayload(dummy);
            Assert(controller.RemainingCharges == 1, "Remaining charges should decrement to 1.");
            Assert(controller.IsArmed, "Trap should remain armed while charges remain.");

            // Fire charge 2
            controller.ExecutePayload(dummy);
            Assert(controller.RemainingCharges == 0, "Remaining charges should be 0.");
            Assert(controller.IsDepleted, "Trap should be flagged as depleted.");
            Assert(!controller.IsArmed, "Trap should automatically disarm when charges deplete.");

            DestroyImmediate(trapData);
            DestroyImmediate(go);
            Debug.Log("Test 3 Passed: Charges decrement accurately and disarm on exhaustion.");
        }

        private void TestTrapPhaseLifecycleReset()
        {
            Debug.Log("\n--- Test 4: Phase Lifecycle & Automatic Recharge ---");

            var go = new GameObject("[Test_Trap_Lifecycle]");
            go.AddComponent<BoxCollider2D>();
            var controller = go.AddComponent<TrapController>();

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            var field = typeof(TrapDataSO).GetField("maxChargesPerRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(trapData, 3);

            controller.Initialize(trapData);
            controller.ArmForCombatPhase();

            var dummy = new DummyDamageable();
            controller.ExecutePayload(dummy);
            controller.ExecutePayload(dummy);
            controller.ExecutePayload(dummy);

            Assert(controller.IsDepleted, "Trap should be depleted after 3 triggers.");

            // Transition to Planning phase
            controller.ResetForPlanningPhase();

            Assert(!controller.IsArmed, "Trap should be disarmed during Planning phase.");
            Assert(controller.RemainingCharges == 3, "Charges must be fully restored upon Planning phase reset.");
            Assert(!controller.IsCoolingDown, "Cooldown should be cleared upon Planning phase reset.");

            // Transition to Battle phase
            controller.ArmForCombatPhase();
            Assert(controller.IsArmed, "Trap should rearm when entering Battle phase.");

            DestroyImmediate(trapData);
            DestroyImmediate(go);
            Debug.Log("Test 4 Passed: Traps automatically recharge and sync state with game phases.");
        }

        private void TestLivingTrapDestructibility()
        {
            Debug.Log("\n--- Test 5: Living Traps vs Mechanical Hazard Destructibility ---");

            // 1. Mechanical Hazard (Immune)
            var mechGo = new GameObject("[Test_Mech_Trap]");
            mechGo.AddComponent<BoxCollider2D>();
            var mechController = mechGo.AddComponent<TrapController>();
            var mechData = ScriptableObject.CreateInstance<TrapDataSO>();
            mechController.Initialize(mechData);

            bool mechDamaged = mechController.TakeDamage(new DamageData(50f, DamageType.Physical));
            Assert(!mechDamaged, "Mechanical hazard must be immune to damage.");

            // 2. Living Unit Trap (Mimic / Totem)
            var livingGo = new GameObject("[Test_Living_Trap]");
            livingGo.AddComponent<BoxCollider2D>();
            var livingController = livingGo.AddComponent<TrapController>();
            var livingData = ScriptableObject.CreateInstance<TrapDataSO>();
            var typeField = typeof(TrapDataSO).GetField("trapType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (typeField != null) typeField.SetValue(livingData, TrapCategoryType.LivingUnit);

            livingController.Initialize(livingData);
            Assert(livingController.IsLivingTrap, "Trap should be classified as LivingUnit.");
            Assert(livingController.CurrentHealth == 100f, "Living trap should have 100 HP.");

            livingController.TakeDamage(new DamageData(40f, DamageType.Physical));
            Assert(livingController.CurrentHealth == 60f, "Living trap HP should reduce to 60.");

            bool fatal = livingController.TakeDamage(new DamageData(70f, DamageType.Physical));
            Assert(fatal, "Fatal damage should return true.");

            DestroyImmediate(mechData);
            DestroyImmediate(livingData);
            DestroyImmediate(mechGo);
            Debug.Log("Test 5 Passed: Living traps are destructible via IDamageable while mechanical hazards are immune.");
        }

        private void TestFreeGridRelocationAndSwapping()
        {
            Debug.Log("\n--- Test 6: Free Grid Relocation & Cell Swapping ---");

            var dmGo = new GameObject("[Test_DM]");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var dpmGo = new GameObject("[Test_DPM]");
            var dpm = dpmGo.AddComponent<DungeonPlacementManager>();

            var handlerGo = new GameObject("[Test_Handler]");
            var handler = handlerGo.AddComponent<TrapPlacementHandler>();
            handler.Initialize(pm: dpm, dm: dm);

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();

            // Place unit A at (2, 2)
            bool placedA = dpm.TryPlaceUnit(trapData, new Vector2Int(2, 2), 1.0f, out var unitA, out _);
            Assert(placedA && unitA != null, "Unit A should be placed at (2, 2).");

            int energyAfterPlacement = dm.Energy;

            // Relocate unit A to (4, 4) at ZERO gold cost
            bool moved = handler.TryRelocateUnit(new Vector2Int(2, 2), new Vector2Int(4, 4));
            Assert(moved, "Relocating unit A to (4, 4) should succeed.");
            Assert(unitA.GridPosition == new Vector2Int(4, 4), "Unit A GridPosition should be (4, 4).");
            Assert(dm.Energy == energyAfterPlacement, "Relocation must cost 0 energy.");
            Assert(!dpm.OccupancyTracker.IsOccupied(new Vector2Int(2, 2)), "Previous cell (2, 2) should now be empty.");
            Assert(dpm.OccupancyTracker.IsOccupied(new Vector2Int(4, 4)), "New cell (4, 4) should be occupied.");

            // Place unit B at (2, 2)
            dpm.TryPlaceUnit(trapData, new Vector2Int(2, 2), 1.0f, out var unitB, out _);

            // Swap Unit A (4, 4) and Unit B (2, 2)
            bool swapped = handler.TrySwapGridUnits(new Vector2Int(4, 4), new Vector2Int(2, 2));
            Assert(swapped, "Swapping units should succeed.");
            Assert(unitA.GridPosition == new Vector2Int(2, 2), "Unit A should now be at (2, 2).");
            Assert(unitB.GridPosition == new Vector2Int(4, 4), "Unit B should now be at (4, 4).");

            DestroyImmediate(trapData);
            DestroyImmediate(handlerGo);
            DestroyImmediate(dpmGo);
            DestroyImmediate(dmGo);
            Debug.Log("Test 6 Passed: Free relocation and swapping executed with zero cost.");
        }

        private void TestBenchCoordinationAndSwapping()
        {
            Debug.Log("\n--- Test 7: Bench Storage & Grid-to-Bench Coordination ---");

            var dmGo = new GameObject("[Test_DM_Bench]");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(100, 50);

            var dpmGo = new GameObject("[Test_DPM_Bench]");
            var dpm = dpmGo.AddComponent<DungeonPlacementManager>();

            var benchGo = new GameObject("[Test_Bench]");
            var bench = benchGo.AddComponent<BenchManager>();

            var handlerGo = new GameObject("[Test_Handler_Bench]");
            var handler = handlerGo.AddComponent<TrapPlacementHandler>();
            handler.Initialize(pm: dpm, bm: bench, dm: dm);

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();

            // Place unit on grid at (1, 1)
            dpm.TryPlaceUnit(trapData, new Vector2Int(1, 1), 1.0f, out var placedUnit, out _);
            Assert(dpm.OccupancyTracker.IsOccupied(new Vector2Int(1, 1)), "Grid (1, 1) should be occupied.");
            Assert(dpm.CapacityPool.CurrentCapacityUsed == trapData.BaseCapacity, "Grid capacity should be consumed.");

            // Move from Grid to Bench
            bool movedToBench = handler.TryMoveGridToBench(new Vector2Int(1, 1), 0);
            Assert(movedToBench, "Moving unit from grid to bench should succeed.");
            Assert(!dpm.OccupancyTracker.IsOccupied(new Vector2Int(1, 1)), "Grid cell should be released.");
            Assert(dpm.CapacityPool.CurrentCapacityUsed == 0, "Grid capacity should be freed when moved to bench.");
            Assert(!bench.Slots[0].IsEmpty, "Bench slot 0 should now hold the unit.");

            // Move back from Bench to Grid at (3, 3)
            bool movedToGrid = handler.TryMoveBenchToGrid(0, new Vector2Int(3, 3));
            Assert(movedToGrid, "Moving from bench back to grid should succeed.");
            Assert(bench.Slots[0].IsEmpty, "Bench slot 0 should now be empty.");
            Assert(dpm.OccupancyTracker.IsOccupied(new Vector2Int(3, 3)), "Grid (3, 3) should now be occupied.");
            Assert(dpm.CapacityPool.CurrentCapacityUsed == trapData.BaseCapacity, "Grid capacity should be allocated again.");

            DestroyImmediate(trapData);
            DestroyImmediate(handlerGo);
            DestroyImmediate(benchGo);
            DestroyImmediate(dpmGo);
            DestroyImmediate(dmGo);
            Debug.Log("Test 7 Passed: Bench storage and grid transfers operate seamlessly.");
        }

        private void TestSellRefundCalculations()
        {
            Debug.Log("\n--- Test 8: Sell / Refund Calculations ---");

            var dmGo = new GameObject("[Test_DM_Sell]");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(100, 50);

            var dpmGo = new GameObject("[Test_DPM_Sell]");
            var dpm = dpmGo.AddComponent<DungeonPlacementManager>();

            var handlerGo = new GameObject("[Test_Handler_Sell]");
            var handler = handlerGo.AddComponent<TrapPlacementHandler>();
            handler.Initialize(pm: dpm, dm: dm);

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            var refundField = typeof(TrapDataSO).GetField("sellRefundAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (refundField != null) refundField.SetValue(trapData, 15);

            dpm.TryPlaceUnit(trapData, new Vector2Int(5, 5), 1.0f, out var placedUnit, out _);
            int energyBeforeSell = dm.Energy;

            // Sell placed trap
            bool sold = handler.TrySellGridUnit(new Vector2Int(5, 5));
            Assert(sold, "Selling placed trap should succeed.");
            Assert(!dpm.OccupancyTracker.IsOccupied(new Vector2Int(5, 5)), "Grid cell should be cleared.");
            Assert(dm.Energy == energyBeforeSell + 15, "DM Energy should be refunded by 15.");

            DestroyImmediate(trapData);
            DestroyImmediate(handlerGo);
            DestroyImmediate(dpmGo);
            DestroyImmediate(dmGo);
            Debug.Log("Test 8 Passed: Selling traps refunds exact configured energy.");
        }

        private void TestPhaseLockingRestrictions()
        {
            Debug.Log("\n--- Test 9: Phase Locking Restrictions During Combat ---");

            var dmGo = new GameObject("[Test_DM_Lock]");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(100, 50);

            var dpmGo = new GameObject("[Test_DPM_Lock]");
            var dpm = dpmGo.AddComponent<DungeonPlacementManager>();

            var handlerGo = new GameObject("[Test_Handler_Lock]");
            var handler = handlerGo.AddComponent<TrapPlacementHandler>();
            handler.Initialize(pm: dpm, dm: dm);

            var trapData = ScriptableObject.CreateInstance<TrapDataSO>();
            dpm.TryPlaceUnit(trapData, new Vector2Int(2, 2), 1.0f, out _, out _);

            // Switch to Combat Phase
            dpm.SetPhase(DungeonPhase.CombatPhase);

            Assert(!handler.CanModify, "CanModify must return false during Combat Phase.");

            // Relocate attempt
            bool moveAttempt = handler.TryRelocateUnit(new Vector2Int(2, 2), new Vector2Int(3, 3));
            Assert(!moveAttempt, "Relocation must be rejected during Combat Phase.");

            // Sell attempt
            bool sellAttempt = handler.TrySellGridUnit(new Vector2Int(2, 2));
            Assert(!sellAttempt, "Selling must be rejected during Combat Phase.");

            DestroyImmediate(trapData);
            DestroyImmediate(handlerGo);
            DestroyImmediate(dpmGo);
            DestroyImmediate(dmGo);
            Debug.Log("Test 9 Passed: All modification actions are strictly locked during Combat Phase.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[TEST FAILED] {message}");
                throw new Exception($"Assertion Failed: {message}");
            }
        }

        private class DummyDamageable : IDamageable
        {
            public bool WasHit { get; private set; }
            public DamageData LastDamageTaken { get; private set; }

            public bool TakeDamage(in DamageData damage)
            {
                WasHit = true;
                LastDamageTaken = damage;
                return false;
            }

            public void TakeDamage(float damage)
            {
                TakeDamage(new DamageData(damage, DamageType.Physical));
            }

            public void ForceDeath()
            {
                WasHit = true;
            }
        }
    }
}
