using System;
using System.Collections.Generic;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Shop.Core;
using SBGames.Shop.Data;
using SBGames.Shop.Mock;
using UnityEngine;

namespace SBGames.Shop.Examples
{
    /// <summary>
    /// Functional test harness and integration example for the Monster Shop system.
    /// Demonstrates full lifecycle setup, simulated rounds, purchasing, slot locking, and phase restrictions.
    /// Can be executed in Unity via Inspector context menu or via automated test suites.
    /// </summary>
    public class MonsterShopTestRunner : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private int initialResources = 25;
        [SerializeField] private int simulatedRounds = 5;
        [SerializeField] private bool runOnStart = false;

        private MonsterShopSystem _shop;
        private IResourceWallet _wallet;
        private MockMonsterCatalog _catalog;
        private SimpleDungeonPhaseManager _phaseManager;

        private void Start()
        {
            if (runOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Shop Tests")]
        public void RunAllTests()
        {
            Debug.Log("==================== STARTING MONSTER SHOP TEST SUITE ====================");

            SetupSystem(initialResources);

            TestInitialRefresh();
            TestPurchasingSuccessAndFailure();
            TestSlotLockingAcrossRefreshes();
            TestPhaseLockingRestrictions();
            TestManualRerolls();
            TestDistributionSimulation(rounds: Mathf.Max(10, simulatedRounds));

            Debug.Log("==================== ALL MONSTER SHOP TESTS COMPLETED SUCCESSFULLY ====================");
        }

        public void SetupSystem(int startingFunds)
        {
            _catalog = new MockMonsterCatalog();
            _wallet = new ResourceWallet(startingFunds);
            _phaseManager = new SimpleDungeonPhaseManager(DungeonPhase.PlanningPhase);

            var config = new ShopConfig(
                slotCount: 5,
                manualRerollCost: 2,
                levelCostMultiplier: 0.5f
            );

            _shop = new MonsterShopSystem(
                catalog: _catalog,
                wallet: _wallet,
                config: config,
                rng: new SystemRandomProvider(1337), // Deterministic seed for reproducible tests
                phaseManager: _phaseManager
            );

            // Hook into events for audit logging
            _shop.OnShopRefreshed += (slots, isManual) =>
            {
                Debug.Log($"[Event: OnShopRefreshed] Reroll: {isManual}, SlotCount: {slots.Count}");
            };

            _shop.OnItemPurchased += (slotIdx, item) =>
            {
                Debug.Log($"[Event: OnItemPurchased] Slot: {slotIdx}, Bought: {item.DisplayName} (Lvl {item.Level}, Rank {item.Rank}, Cost: {item.Cost})");
            };

            _shop.OnSlotLockToggled += (slotIdx, locked) =>
            {
                Debug.Log($"[Event: OnSlotLockToggled] Slot: {slotIdx}, Locked: {locked}");
            };

            _shop.OnTransactionRejected += (result) =>
            {
                Debug.LogWarning($"[Event: OnTransactionRejected] {result}");
            };
        }

        private void TestInitialRefresh()
        {
            Debug.Log("\n--- Test 1: Initial Free Refresh (Round 1, Rank E) ---");
            bool success = _shop.TryRefresh(
                roundNumber: 1,
                playerRank: DungeonRank.E,
                isManualReroll: false,
                out var result
            );

            Assert(success, "Initial shop refresh should succeed.");
            Assert(_shop.GetAvailableOfferCount() == 5, "Shop should contain 5 available items.");
            Debug.Log(_shop.GetShopInspectionSummary());
        }

        private void TestPurchasingSuccessAndFailure()
        {
            Debug.Log("\n--- Test 2: Purchasing & Wallet Deductions ---");
            int initialBalance = _wallet.CurrentResources;
            var itemToBuy = _shop.Slots[0].CurrentItem;
            int itemCost = itemToBuy.Cost;

            // Successful purchase
            bool buySuccess = _shop.TryPurchase(0, out var purchasedItem, out var buyResult);
            Assert(buySuccess, $"Purchasing slot 0 should succeed. Error: {buyResult.Message}");
            Assert(purchasedItem != null, "Purchased item should not be null.");
            Assert(_wallet.CurrentResources == initialBalance - itemCost, "Wallet balance should decrease by item cost.");
            Assert(_shop.Slots[0].IsPurchased, "Slot 0 should be marked as purchased.");
            Assert(_shop.GetAvailableOfferCount() == 4, "Available offer count should now be 4.");

            // Duplicate purchase attempt on same slot
            bool duplicateSuccess = _shop.TryPurchase(0, out _, out var duplicateResult);
            Assert(!duplicateSuccess, "Purchasing an already bought slot must fail.");
            Assert(duplicateResult.Status == ShopStatus.SlotAlreadyPurchased, "Status should be SlotAlreadyPurchased.");

            // Out-of-bounds slot purchase attempt
            bool oobSuccess = _shop.TryPurchase(99, out _, out var oobResult);
            Assert(!oobSuccess, "Purchasing out-of-bounds slot must fail.");
            Assert(oobResult.Status == ShopStatus.InvalidSlot, "Status should be InvalidSlot.");

            // Insufficient funds purchase attempt
            _wallet.TryDeduct(_wallet.CurrentResources); // Drain wallet to 0
            bool brokeSuccess = _shop.TryPurchase(1, out _, out var brokeResult);
            Assert(!brokeSuccess, "Purchasing with 0 funds must fail.");
            Assert(brokeResult.Status == ShopStatus.InsufficientCurrency, "Status should be InsufficientCurrency.");

            // Restore wallet
            _wallet.Add(initialResources);
        }

        private void TestSlotLockingAcrossRefreshes()
        {
            Debug.Log("\n--- Test 3: Slot Locking Across Refreshes ---");
            // Refresh to fresh state
            _shop.TryRefresh(roundNumber: 2, playerRank: DungeonRank.E, isManualReroll: false, out _);

            var slot1Item = _shop.Slots[1].CurrentItem;
            string expectedInstanceId = slot1Item.InstanceId;

            // Lock slot 1
            bool lockSuccess = _shop.TryToggleLock(1, out bool isLocked, out var lockResult);
            Assert(lockSuccess && isLocked, "Slot 1 should be successfully locked.");
            Assert(_shop.Slots[1].IsLocked, "Slot 1 IsLocked flag should be true.");

            // Refresh to Round 3
            _shop.TryRefresh(roundNumber: 3, playerRank: DungeonRank.D, isManualReroll: false, out _);

            // Slot 1 must retain the exact same item instance
            Assert(_shop.Slots[1].CurrentItem.InstanceId == expectedInstanceId, "Locked slot 1 must retain identical item instance across refresh.");
            Assert(_shop.Slots[1].IsLocked, "Locked slot 1 must remain locked across refresh.");

            // Unlock slot 1
            _shop.TryToggleLock(1, out bool isNowLocked, out _);
            Assert(!isNowLocked, "Slot 1 should be unlocked now.");
        }

        private void TestPhaseLockingRestrictions()
        {
            Debug.Log("\n--- Test 4: Phase Locking (Combat Phase Restrictions) ---");
            _phaseManager.SetPhase(DungeonPhase.CombatPhase);

            // Refresh attempt during combat
            bool refreshAttempt = _shop.TryRefresh(4, DungeonRank.D, false, out var refreshResult);
            Assert(!refreshAttempt, "Refresh must be rejected during CombatPhase.");
            Assert(refreshResult.Status == ShopStatus.PhaseLocked, "Status should be PhaseLocked.");

            // Purchase attempt during combat
            bool purchaseAttempt = _shop.TryPurchase(2, out _, out var purchaseResult);
            Assert(!purchaseAttempt, "Purchase must be rejected during CombatPhase.");
            Assert(purchaseResult.Status == ShopStatus.PhaseLocked, "Status should be PhaseLocked.");

            // Unlock phase
            _phaseManager.SetPhase(DungeonPhase.PlanningPhase);
        }

        private void TestManualRerolls()
        {
            Debug.Log("\n--- Test 5: Manual Reroll Currency Deduction ---");
            int balanceBefore = _wallet.CurrentResources;
            int rerollCost = _shop.Config.ManualRerollCost;

            bool rerollSuccess = _shop.TryRefresh(
                roundNumber: 5,
                playerRank: DungeonRank.C,
                isManualReroll: true,
                out var rerollResult
            );

            Assert(rerollSuccess, "Manual reroll should succeed with sufficient funds.");
            Assert(_wallet.CurrentResources == balanceBefore - rerollCost, "Manual reroll must deduct Config.ManualRerollCost.");
        }

        private void TestDistributionSimulation(int rounds)
        {
            Debug.Log($"\n--- Test 6: Statistical Distribution Simulation ({rounds} Rounds) ---");

            var earlyRankCounts = new Dictionary<DungeonRank, int>();
            var lateRankCounts = new Dictionary<DungeonRank, int>();
            int highLevelSpawns = 0;

            foreach (DungeonRank r in Enum.GetValues(typeof(DungeonRank)))
            {
                earlyRankCounts[r] = 0;
                lateRankCounts[r] = 0;
            }

            // Simulate Round 1 (Early game)
            for (int i = 0; i < 100; i++)
            {
                var item = _shop.GenerateRandomShopItem(roundNumber: 1, playerRank: DungeonRank.E);
                earlyRankCounts[item.Rank]++;
                if (item.Level > 1) highLevelSpawns++;
            }

            // Simulate Round 18 (Endgame S-tier)
            for (int i = 0; i < 100; i++)
            {
                var item = _shop.GenerateRandomShopItem(roundNumber: 18, playerRank: DungeonRank.S);
                lateRankCounts[item.Rank]++;
                if (item.Level > 1) highLevelSpawns++;
            }

            Debug.Log($"[Round 1 (Tier E) Odds Sample / 100]: E={earlyRankCounts[DungeonRank.E]}, D={earlyRankCounts[DungeonRank.D]}, C={earlyRankCounts[DungeonRank.C]}, B={earlyRankCounts[DungeonRank.B]}, A={earlyRankCounts[DungeonRank.A]}, S={earlyRankCounts[DungeonRank.S]}");
            Debug.Log($"[Round 18 (Tier S) Odds Sample / 100]: E={lateRankCounts[DungeonRank.E]}, D={lateRankCounts[DungeonRank.D]}, C={lateRankCounts[DungeonRank.C]}, B={lateRankCounts[DungeonRank.B]}, A={lateRankCounts[DungeonRank.A]}, S={lateRankCounts[DungeonRank.S]}");

            Assert(earlyRankCounts[DungeonRank.E] > 50, "Early rounds must be heavily dominated by Rank E.");
            Assert(earlyRankCounts[DungeonRank.S] == 0, "Early rounds must have 0% S-rank monsters.");
            Assert(lateRankCounts[DungeonRank.A] + lateRankCounts[DungeonRank.S] > 20, "Late rounds must yield significant A and S rank monsters.");
            Assert(highLevelSpawns > 0, "High level monsters (> level 1) should have successfully spawned during simulation.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[TEST FAILED] {message}");
                throw new Exception($"Assertion Failed: {message}");
            }
        }
    }
}
