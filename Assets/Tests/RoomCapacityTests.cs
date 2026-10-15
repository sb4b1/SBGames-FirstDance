using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Dungeon.Markers;
using SBGames.Loop;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class RoomCapacityTests
    {
        private class TestUnitDefinition : IUnitPlacementDefinition
        {
            public string UnitId { get; set; } = "test_unit";
            public string DisplayName { get; set; } = "Test Monster";
            public int BaseCost { get; set; } = 10;
            public int BaseCapacity { get; set; } = 1;
            public Vector2Int Footprint { get; set; } = Vector2Int.one;
            public GameObject Prefab { get; set; } = null;
        }

        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
            DungeonRoomRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DungeonRoomRegistry.Clear();
            if (_spawnedObjects != null)
            {
                for (int i = 0; i < _spawnedObjects.Count; i++)
                {
                    if (_spawnedObjects[i] != null)
                    {
                        Object.DestroyImmediate(_spawnedObjects[i]);
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

        private (DungeonPlacementSystem system, DungeonMaster dm, DictionaryRoomCapacityTracker roomTracker) CreateTestSetup(
            int startingEnergy = 100,
            int maxGlobalCapacity = 20)
        {
            var dmGo = Track(new GameObject("DM"));
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: startingEnergy);

            var capacityPool = new CapacityPool(maxGlobalCapacity);
            var occupancyTracker = new GridOccupancyTracker(10, 10);
            var roomTracker = new DictionaryRoomCapacityTracker();
            var crowdingStrategy = new LinearRoomCrowdingPenaltyStrategy(0.25f);

            var system = new DungeonPlacementSystem(
                dm,
                capacityPool,
                occupancyTracker,
                multiplierStrategy: null,
                phaseManager: null,
                buildableCellValidator: null,
                roomCapacityTracker: roomTracker,
                crowdingStrategy: crowdingStrategy
            );

            return (system, dm, roomTracker);
        }

        [Test]
        public void Placement_WithinSoftCapacity_StandardCostAndSuccess()
        {
            var (system, dm, roomTracker) = CreateTestSetup(startingEnergy: 100);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 2, maxCapacity: 4, tiles: new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            // Place 1st unit
            var req1 = new PlacementRequest(unitDef, new Vector2Int(0, 0));
            var val1 = system.ValidatePlacement(req1);
            Assert.IsTrue(val1.IsValid);
            Assert.AreEqual(10, val1.CostRequired);
            Assert.IsTrue(system.TryPlaceUnit(req1, out _, out _));

            // Place 2nd unit (room occupancy reaches soft capacity 2)
            var req2 = new PlacementRequest(unitDef, new Vector2Int(0, 1));
            var val2 = system.ValidatePlacement(req2);
            Assert.IsTrue(val2.IsValid);
            Assert.AreEqual(10, val2.CostRequired);
            Assert.IsTrue(system.TryPlaceUnit(req2, out _, out _));

            Assert.AreEqual(2, roomTracker.GetRoomCapacityUsed(1));
            Assert.AreEqual(80, dm.Energy);
        }

        [Test]
        public void Placement_AboveSoftCapacity_AppliesCrowdingSurcharge()
        {
            var (system, dm, roomTracker) = CreateTestSetup(startingEnergy: 100);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 2, maxCapacity: 4, tiles: new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 0)), out _, out _);
            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 1)), out _, out _);

            // 3rd unit: room has 2 units, softCapacity is 2.
            // Excess = (2 - 2) + 1 = 1 point.
            // Surcharge = 10 * 1 * 0.25 = 2.5 -> rounded to 3.
            // Total cost = 13.
            var req3 = new PlacementRequest(unitDef, new Vector2Int(1, 0));
            var val3 = system.ValidatePlacement(req3);
            Assert.IsTrue(val3.IsValid);
            Assert.AreEqual(13, val3.CostRequired);

            bool placed = system.TryPlaceUnit(req3, out var placedUnit, out _);
            Assert.IsTrue(placed);
            Assert.AreEqual(13, placedUnit.CostPaid);
            Assert.AreEqual(3, roomTracker.GetRoomCapacityUsed(1));
        }

        [Test]
        public void Placement_CrowdingSurcharge_RejectsWhenEnergyInsufficient()
        {
            // DM has 12 energy; 3rd unit costs 10 base + 3 surcharge = 13
            var (system, dm, roomTracker) = CreateTestSetup(startingEnergy: 32);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 2, maxCapacity: 4, tiles: new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 0)), out _, out _); // spends 10, 22 left
            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 1)), out _, out _); // spends 10, 12 left

            Assert.AreEqual(12, dm.Energy);

            var req3 = new PlacementRequest(unitDef, new Vector2Int(1, 0));
            var val3 = system.ValidatePlacement(req3);
            Assert.IsFalse(val3.IsValid);
            Assert.AreEqual(PlacementStatus.InsufficientEnergy, val3.Status);
            Assert.AreEqual(13, val3.CostRequired);
        }

        [Test]
        public void Placement_BeyondHardCapacity_RejectsWithExceedsRoomCapacity()
        {
            var (system, _, roomTracker) = CreateTestSetup(startingEnergy: 100);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 1, maxCapacity: 2, tiles: new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 0)), out _, out _);
            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 1)), out _, out _);

            Assert.AreEqual(2, roomTracker.GetRoomCapacityUsed(1));

            // Room hard limit is 2. Attempting to place 3rd unit must fail
            var req3 = new PlacementRequest(unitDef, new Vector2Int(1, 0));
            var val3 = system.ValidatePlacement(req3);
            Assert.IsFalse(val3.IsValid);
            Assert.AreEqual(PlacementStatus.ExceedsRoomCapacity, val3.Status);

            bool placed = system.TryPlaceUnit(req3, out _, out var res3);
            Assert.IsFalse(placed);
            Assert.AreEqual(PlacementStatus.ExceedsRoomCapacity, res3.Status);
        }

        [Test]
        public void Placement_RemoveUnit_RestoresCapacityAndRemovesCrowding()
        {
            var (system, dm, roomTracker) = CreateTestSetup(startingEnergy: 100);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 2, maxCapacity: 4, tiles: new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 0)), out _, out _);
            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 1)), out _, out _);

            // Removing unit at (0, 1) with 100% refund
            bool removed = system.TryRemoveUnit(new Vector2Int(0, 1), out _, refundRatio: 1.0f);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, roomTracker.GetRoomCapacityUsed(1));
            Assert.AreEqual(90, dm.Energy);

            // Now placing at (1, 0) is only the 2nd unit in room -> standard cost 10, no crowding surcharge
            var reqNew = new PlacementRequest(unitDef, new Vector2Int(1, 0));
            var valNew = system.ValidatePlacement(reqNew);
            Assert.IsTrue(valNew.IsValid);
            Assert.AreEqual(10, valNew.CostRequired);
        }

        [Test]
        public void Placement_CorridorTile_HasNoRoomCapacityLimit()
        {
            var (system, _, roomTracker) = CreateTestSetup(startingEnergy: 100);
            roomTracker.DefineRoom(roomId: 1, softCapacity: 1, maxCapacity: 1, tiles: new[]
            {
                new Vector2Int(0, 0)
            });

            var unitDef = new TestUnitDefinition { BaseCost = 10, BaseCapacity = 1 };

            // Fill room 1
            system.TryPlaceUnit(new PlacementRequest(unitDef, new Vector2Int(0, 0)), out _, out _);

            // Place in corridor (5, 5) which is not in room 1 (HasRoomAt is false)
            var reqCorridor = new PlacementRequest(unitDef, new Vector2Int(5, 5));
            var valCorridor = system.ValidatePlacement(reqCorridor);
            Assert.IsTrue(valCorridor.IsValid);
            Assert.AreEqual(10, valCorridor.CostRequired);
            Assert.IsTrue(system.TryPlaceUnit(reqCorridor, out _, out _));
        }

        [Test]
        public void DungeonRoomCapacityTracker_WithRoomVolume_CalculatesCapacityCorrectly()
        {
            var mgrGo = Track(new GameObject("PlacementMgr"));
            var mgr = mgrGo.AddComponent<DungeonPlacementManager>();
            mgr.ConfigurePlacementGrid(new Vector2Int(10, 10), Vector3.zero, 1.0f);

            var roomGo = Track(new GameObject("Room_1"));
            roomGo.transform.position = new Vector3(2f, 2f, 0f);
            var roomVolume = roomGo.AddComponent<RoomVolume>();
            roomVolume.RoomId = 1;
            roomVolume.SoftMonsterCapacity = 2;
            roomVolume.MaxMonsterCapacity = 5;
            roomVolume.SetBounds(new Bounds(new Vector3(2f, 2f, 0f), new Vector3(4f, 4f, 2f)));

            var tracker = new DungeonRoomCapacityTracker(mgr);

            Assert.IsTrue(tracker.HasRoomAt(new Vector2Int(2, 2)));
            Assert.AreEqual(1, tracker.GetRoomId(new Vector2Int(2, 2)));
            Assert.AreEqual(2, tracker.GetRoomSoftCapacity(1));
            Assert.AreEqual(5, tracker.GetRoomMaxCapacity(1));
            Assert.AreEqual(0, tracker.GetRoomCapacityUsed(1));
            Assert.IsTrue(tracker.CanAccommodate(1, 3));
            Assert.IsFalse(tracker.CanAccommodate(1, 6));
        }
    }
}
