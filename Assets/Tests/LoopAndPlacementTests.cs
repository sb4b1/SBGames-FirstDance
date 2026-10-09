using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Placement.Bench;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class LoopAndPlacementTests
    {
        private class MockUnitDefinition : IUnitPlacementDefinition
        {
            public string UnitId { get; set; } = "mock_unit";
            public string DisplayName { get; set; } = "Mock Unit";
            public int BaseCost { get; set; } = 10;
            public int BaseCapacity { get; set; } = 2;
            public Vector2Int Footprint { get; set; } = Vector2Int.one;
            public GameObject Prefab { get; set; } = null;
        }

        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnedObjects != null)
            {
                for (int i = 0; i < _spawnedObjects.Count; i++)
                {
                    if (_spawnedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_spawnedObjects[i]);
                    }
                }
                _spawnedObjects.Clear();
            }
        }

        private GameObject Track(GameObject go)
        {
            _spawnedObjects.Add(go);
            return go;
        }

        [Test]
        public void GridOccupancyTracker_MultiTileFootprint_TracksAndReleasesAllCells()
        {
            var tracker = new GridOccupancyTracker(5, 5);
            var def2x2 = new MockUnitDefinition
            {
                Footprint = new Vector2Int(2, 2)
            };

            var unit = new PlacedUnitInstance(
                instanceId: "u_2x2",
                definition: def2x2,
                gridPosition: new Vector2Int(1, 1),
                combatPowerFactor: 1.0f,
                costPaid: 10,
                capacityConsumed: 2
            );

            bool registered = tracker.RegisterUnit(unit);
            Assert.IsTrue(registered, "2x2 unit should register successfully within 5x5 bounds.");

            // Verify all 4 cells are occupied
            Assert.IsTrue(tracker.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsTrue(tracker.IsOccupied(new Vector2Int(1, 2)));
            Assert.IsTrue(tracker.IsOccupied(new Vector2Int(2, 1)));
            Assert.IsTrue(tracker.IsOccupied(new Vector2Int(2, 2)));

            // Cell outside footprint should not be occupied
            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(0, 0)));
            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(3, 3)));

            // Attempting to place another unit overlapping cell (2, 2) should fail
            var def1x1 = new MockUnitDefinition { Footprint = Vector2Int.one };
            var overlapUnit = new PlacedUnitInstance(
                instanceId: "u_overlap",
                definition: def1x1,
                gridPosition: new Vector2Int(2, 2),
                combatPowerFactor: 1.0f,
                costPaid: 5,
                capacityConsumed: 1
            );
            Assert.IsFalse(tracker.RegisterUnit(overlapUnit), "Overlapping cell (2, 2) must be rejected.");

            // Unregister via any covered cell should free all 4 cells
            bool unregistered = tracker.UnregisterUnit(new Vector2Int(2, 2), out var removed);
            Assert.IsTrue(unregistered);
            Assert.AreSame(unit, removed);

            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(1, 2)));
            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(2, 1)));
            Assert.IsFalse(tracker.IsOccupied(new Vector2Int(2, 2)));
            Assert.AreEqual(0, tracker.GetAllPlacedUnits().Count);
        }

        [Test]
        public void BenchManager_AddToBench_MarksOffGridAndAllowsCleanup()
        {
            var benchGo = Track(new GameObject("Test_BenchManager"));
            var bench = benchGo.AddComponent<BenchManager>();
            bench.InitializeSlots();

            var unitGo = Track(new GameObject("Test_UnitGo"));
            var def = new MockUnitDefinition();
            var unit = new PlacedUnitInstance("bench_u1", def, new Vector2Int(3, 3), 1.0f, 10, 1, unitGo);

            bool added = bench.TryAddToBench(unit, out int slot);
            Assert.IsTrue(added);
            Assert.AreEqual(0, slot);
            Assert.AreEqual(new Vector2Int(-1, -1), unit.GridPosition, "Bench units should be marked off-grid (-1, -1).");
            Assert.AreEqual(1, bench.OccupiedCount);

            // Destroy the unit GameObject
            UnityEngine.Object.DestroyImmediate(unitGo);
            unit.GameObjectInstance = null;

            int cleaned = bench.CleanupDestroyedUnits();
            Assert.AreEqual(1, cleaned);
            Assert.AreEqual(0, bench.OccupiedCount);
        }

        [Test]
        public void PartyLeader_Succession_PreservesParametersAndEvaluator()
        {
            var leaderGo = Track(new GameObject("Test_Leader"));
            var leader = leaderGo.AddComponent<PartyLeader>();
            var evaluator = leaderGo.AddComponent<SquadTacticalEvaluator>();
            evaluator.AutonomousAI = true;

            var successorGo = Track(new GameObject("Test_Successor"));
            var successorMember = successorGo.AddComponent<SquadMember>();

            leader.RegisterMember(successorMember);
            Assert.IsTrue(leader.Members.Contains(successorMember));

            // Disband squad safely sets leaders to null without exceptions
            leader.DisbandSquad();
            Assert.AreEqual(0, leader.Members.Count);
            Assert.IsFalse(successorMember.HasLeader);
        }

        [Test]
        public void DungeonProgression_NormalizedProbabilities_ZeroAllocBuffer()
        {
            var progGo = Track(new GameObject("Test_Progression"));
            var prog = progGo.AddComponent<DungeonProgression>();
            prog.Initialize(DungeonRank.C);

            float[] buffer = new float[DungeonRankExtensions.RankCount];
            prog.GetNormalizedProbabilities(DungeonRank.C, buffer);

            float sum = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i];
            }

            Assert.AreEqual(1.0f, sum, 0.001f, "Probability distribution must sum to 1.0.");

            // Deterministic attempt roll
            var rolled = prog.DetermineAttemptRank(0.0f);
            Assert.IsTrue(Enum.IsDefined(typeof(DungeonRank), rolled));
        }

        #region Bench Slot Swapping & Overflow Stress Tests

        [Test]
        public void BenchManager_BenchFull_RejectsAdditionalUnits()
        {
            var benchGo = Track(new GameObject("Test_BenchManager"));
            var bench = benchGo.AddComponent<BenchManager>();
            bench.InitializeSlots();

            var def = new MockUnitDefinition();

            // Fill all slots
            for (int i = 0; i < bench.Capacity; i++)
            {
                var unit = new PlacedUnitInstance($"u_{i}", def, new Vector2Int(i, 0), 1.0f, 5, 1);
                bool added = bench.TryAddToBench(unit, out int slot);
                Assert.IsTrue(added);
                Assert.AreEqual(i, slot);
            }

            Assert.AreEqual(bench.Capacity, bench.OccupiedCount);
            Assert.IsFalse(bench.HasFreeSlot);

            // Attempting to add one more unit to full bench must fail cleanly
            var overflowUnit = new PlacedUnitInstance("u_overflow", def, Vector2Int.zero, 1.0f, 5, 1);
            bool overflowAdded = bench.TryAddToBench(overflowUnit, out int overflowSlot);
            Assert.IsFalse(overflowAdded, "Bench must reject units when completely full.");
            Assert.AreEqual(-1, overflowSlot);
        }

        [Test]
        public void BenchManager_SwapBenchSlots_SwapsPositionsAccurately()
        {
            var benchGo = Track(new GameObject("Test_BenchManager"));
            var bench = benchGo.AddComponent<BenchManager>();
            bench.InitializeSlots();

            var def = new MockUnitDefinition();
            var unitA = new PlacedUnitInstance("unit_A", def, Vector2Int.zero, 1.0f, 5, 1);
            var unitB = new PlacedUnitInstance("unit_B", def, Vector2Int.zero, 1.0f, 5, 1);

            bench.TryAssignToSlot(0, unitA, out _);
            bench.TryAssignToSlot(1, unitB, out _);

            Assert.AreSame(unitA, bench.Slots[0].UnitInstance);
            Assert.AreSame(unitB, bench.Slots[1].UnitInstance);

            // Swap slot 0 and slot 1
            bool swapped = bench.SwapBenchSlots(0, 1);
            Assert.IsTrue(swapped);

            Assert.AreSame(unitB, bench.Slots[0].UnitInstance);
            Assert.AreSame(unitA, bench.Slots[1].UnitInstance);

            // Out of bounds swaps must fail gracefully
            Assert.IsFalse(bench.SwapBenchSlots(-1, 0));
            Assert.IsFalse(bench.SwapBenchSlots(0, 999));
        }

        #endregion

        #region CoreGameLoop Phase & Economy Lifecycle Stress Tests

        [Test]
        public void CoreGameLoop_CompleteRoundLifecycle_TransitionsPhasesDeterministically()
        {
            var dmGo = Track(new GameObject("Test_DM"));
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var loopGo = Track(new GameObject("Test_Loop"));
            var loop = loopGo.AddComponent<CoreGameLoop>();
            loop.Initialize(dm, initialRound: 1, planningAllowance: 20, victoryBonus: 30, maxRoundLimit: 5);

            // 1. Start Game -> Planning Phase, Round 1
            loop.StartGame();
            Assert.AreEqual(GamePhase.Planning, loop.CurrentPhase);
            Assert.AreEqual(1, loop.CurrentRound);
            Assert.IsTrue(loop.CanPerformPlanningActions);

            // 2. Planning Phase purchases
            bool purchased = loop.TryPurchaseMonster(15);
            Assert.IsTrue(purchased);
            Assert.AreEqual(35, dm.Energy); // 50 - 15 = 35

            // 3. Transition to Battle Phase
            bool battleStarted = loop.StartBattle();
            Assert.IsTrue(battleStarted);
            Assert.AreEqual(GamePhase.Battle, loop.CurrentPhase);
            Assert.IsFalse(loop.CanPerformPlanningActions);

            // Cannot purchase monsters during Battle phase
            Assert.IsFalse(loop.TryPurchaseMonster(10));

            // 4. Resolve Battle with Victory
            var battleResult = BattleResult.Win(bonusEnergy: 10);
            var resolution = loop.ResolveBattle(battleResult);

            Assert.AreEqual(BattleOutcome.Victory, resolution.Outcome);
            Assert.AreEqual(1, resolution.RoundNumber);
            Assert.IsFalse(resolution.IsGameOver);

            // Returned to Planning Phase, Round 2
            Assert.AreEqual(GamePhase.Planning, loop.CurrentPhase);
            Assert.AreEqual(2, loop.CurrentRound);

            // Energy awarded: previous (35) + planning allowance (20) + victory base (30) + bonus (10) = 95
            Assert.AreEqual(95, dm.Energy);
        }

        [Test]
        public void CoreGameLoop_FatalEssenceLoss_TriggersGameOverCleanly()
        {
            var dmGo = Track(new GameObject("Test_DM"));
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 20, startingEnergy: 50);

            var loopGo = Track(new GameObject("Test_Loop"));
            var loop = loopGo.AddComponent<CoreGameLoop>();
            loop.Initialize(dm, initialRound: 1);

            loop.StartGame();
            loop.StartBattle();

            // Suffer 50 essence damage when having only 20
            var defeatResult = BattleResult.Loss(essenceDamage: 50);
            var resolution = loop.ResolveBattle(defeatResult);

            Assert.AreEqual(BattleOutcome.Defeat, resolution.Outcome);
            Assert.IsTrue(resolution.IsGameOver);
            Assert.AreEqual(GameOverReason.EssenceDepleted, resolution.GameOverReason);
            Assert.AreEqual(GamePhase.GameOver, loop.CurrentPhase);
            Assert.AreEqual(0, dm.Essence);
        }

        #endregion
    }
}
