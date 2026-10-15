using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Markers;
using SBGames.Dungeon.Unity;
using SBGames.Placement;
using SBGames.Loop;

namespace SBGames.Tests
{
    [TestFixture]
    public class DungeonGeneratorTests
    {
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

        private class MockUnitDefinition : IUnitPlacementDefinition
        {
            public string UnitId { get; set; } = "test_unit";
            public string DisplayName { get; set; } = "Test Unit";
            public int BaseCost { get; set; } = 10;
            public int BaseCapacity { get; set; } = 2;
            public Vector2Int Footprint { get; set; } = Vector2Int.one;
            public GameObject Prefab { get; set; } = null;
        }

        [Test]
        public void DungeonLayoutGenerator_DeterministicSeed_ProducesIdenticalLayout()
        {
            var generator = new DungeonLayoutGenerator();
            var config = new DungeonGeneratorConfig(5, 8, branchProbability: 0.4f, loopProbability: 0.3f, safeRoomCount: 1);

            int seed = 42;
            var graphA = generator.Generate(seed, config);
            var graphB = generator.Generate(seed, config);

            Assert.AreEqual(graphA.Rooms.Count, graphB.Rooms.Count, "Room count must match exactly for identical seeds.");
            Assert.AreEqual(graphA.Doors.Count, graphB.Doors.Count, "Door count must match exactly for identical seeds.");

            foreach (var kvp in graphA.Rooms)
            {
                Assert.IsTrue(graphB.TryGetRoom(kvp.Key, out var roomB), $"Room {kvp.Key} must exist in graph B.");
                Assert.AreEqual(kvp.Value.MacroCoord, roomB.MacroCoord, $"Coordinates for room {kvp.Key} must be identical.");
                Assert.AreEqual(kvp.Value.RoomType, roomB.RoomType, $"RoomType for room {kvp.Key} must be identical.");
                Assert.AreEqual(kvp.Value.Depth, roomB.Depth, $"Depth for room {kvp.Key} must be identical.");
            }

            for (int i = 0; i < graphA.Doors.Count; i++)
            {
                var doorA = graphA.Doors[i];
                var doorB = graphB.Doors[i];
                Assert.AreEqual(doorA.DoorId, doorB.DoorId);
                Assert.AreEqual(doorA.RoomAId, doorB.RoomAId);
                Assert.AreEqual(doorA.RoomBId, doorB.RoomBId);
                Assert.AreEqual(doorA.DirectionFromA, doorB.DirectionFromA);
                Assert.AreEqual(doorA.IsBossDoor, doorB.IsBossDoor);
            }
        }

        [Test]
        public void DungeonLayoutGenerator_StructuralInvariants_AlwaysSatisfiedAcrossMultipleSeeds()
        {
            var generator = new DungeonLayoutGenerator();

            for (int seed = 1; seed <= 20; seed++)
            {
                var config = new DungeonGeneratorConfig(5, 8, branchProbability: 0.35f, loopProbability: 0.25f, safeRoomCount: 1);
                var graph = generator.Generate(seed, config);

                // 1. Exactly 1 Entrance at (0, 0)
                Assert.IsNotNull(graph.EntranceNode, $"Seed {seed}: Entrance node must exist.");
                Assert.AreEqual(new GridCoord(0, 0), graph.EntranceNode.MacroCoord, $"Seed {seed}: Entrance must be at (0, 0).");
                Assert.AreEqual(DungeonRoomType.Entrance, graph.EntranceNode.RoomType);

                // 2. Exactly 1 Boss room
                Assert.IsNotNull(graph.BossNode, $"Seed {seed}: Boss node must exist.");
                Assert.AreEqual(DungeonRoomType.Boss, graph.BossNode.RoomType);
                Assert.AreEqual(1, graph.BossNode.DoorIds.Count, $"Seed {seed}: Boss room must have strictly 1 entrance door.");

                // Boss door must be flagged isBossDoor = true
                Assert.IsTrue(graph.TryGetDoor(graph.BossNode.DoorIds[0], out var bossDoor));
                Assert.IsTrue(bossDoor.IsBossDoor, $"Seed {seed}: Door leading to Boss room must be flagged IsBossDoor.");

                // 3. Exactly 1 SafeRoom along deeper half of dungeon
                Assert.IsNotNull(graph.SafeRoomNode, $"Seed {seed}: SafeRoom must exist when safeRoomCount = 1.");
                Assert.AreEqual(DungeonRoomType.SafeRoom, graph.SafeRoomNode.RoomType);
                Assert.GreaterOrEqual(graph.SafeRoomNode.Depth, 1, $"Seed {seed}: SafeRoom depth must be >= 1.");

                // 4. Reachability: all rooms reachable from entrance
                Assert.IsTrue(graph.AreAllRoomsReachableFromEntrance(), $"Seed {seed}: All rooms must be reachable from Entrance.");
            }
        }

        [Test]
        public void DungeonLayoutGenerator_BranchingAndLoops_GeneratesExpectedTopology()
        {
            var generator = new DungeonLayoutGenerator();
            // High branch probability creates dead ends
            var config = new DungeonGeneratorConfig(7, 8, branchProbability: 0.8f, loopProbability: 0.5f, safeRoomCount: 1);
            var graph = generator.Generate(123, config);

            // Find dead-end rooms (leaf nodes with degree 1)
            int leafCount = 0;
            foreach (var room in graph.Rooms.Values)
            {
                if (room.DoorIds.Count == 1) leafCount++;
            }

            // At least the Boss room is a leaf, and with high branch probability there are additional dead ends
            Assert.GreaterOrEqual(leafCount, 1, "Graph must have at least 1 leaf node.");
            Assert.AreEqual(1, graph.BossNode.DoorIds.Count, "Boss room must never receive extra loop connections.");
        }

        [Test]
        public void DungeonPlacementValidation_EntranceRoomRejected_NormalAndBossRoomsAccepted()
        {
            var generator = new DungeonLayoutGenerator();
            var config = new DungeonGeneratorConfig(5, 6, safeRoomCount: 1, roomWidth: 10, roomHeight: 10, corridorLength: 4);
            var graph = generator.Generate(777, config);

            // 1. Check coordinates inside Entrance room
            graph.GetRoomTileBounds(graph.EntranceNode, config, out var eMin, out var eMax);
            int eMidX = (eMin.X + eMax.X) / 2;
            int eMidY = (eMin.Y + eMax.Y) / 2;
            Assert.IsFalse(graph.IsCellBuildable(eMidX, eMidY, config), "Entrance room center must NOT be buildable.");
            Assert.IsFalse(graph.IsCellBuildable(eMin.X, eMin.Y, config), "Entrance room corner must NOT be buildable.");

            // 2. Check coordinates inside Boss room
            graph.GetRoomTileBounds(graph.BossNode, config, out var bMin, out var bMax);
            int bMidX = (bMin.X + bMax.X) / 2;
            int bMidY = (bMin.Y + bMax.Y) / 2;
            Assert.IsTrue(graph.IsCellBuildable(bMidX, bMidY, config), "Boss room center must be buildable.");

            // 3. Check coordinates inside Safe room
            graph.GetRoomTileBounds(graph.SafeRoomNode, config, out var sMin, out var sMax);
            int sMidX = (sMin.X + sMax.X) / 2;
            int sMidY = (sMin.Y + sMax.Y) / 2;
            Assert.IsTrue(graph.IsCellBuildable(sMidX, sMidY, config), "Safe room center must be buildable.");

            // 4. Check coordinates outside dungeon
            Assert.IsFalse(graph.IsCellBuildable(9999, 9999, config), "Coordinates far outside dungeon must NOT be buildable.");
        }

        [Test]
        public void DungeonPlacementSystem_WithBuildableCellValidator_RejectsEntranceAndValidatesNormal()
        {
            var dmGo = Track(new GameObject("[DM]"));
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var capacityPool = new CapacityPool(20);
            var occupancyTracker = new GridOccupancyTracker(30, 30);

            // Mark cell (5, 5) as Normal (buildable), cell (0, 0) as Entrance (non-buildable)
            var buildableSet = new HashSet<Vector2Int> { new Vector2Int(5, 5), new Vector2Int(5, 6) };
            var validator = new SetBasedBuildableCellValidator(buildableSet);

            var system = new DungeonPlacementSystem(
                dm,
                capacityPool,
                occupancyTracker,
                buildableCellValidator: validator
            );

            var def = new MockUnitDefinition { BaseCost = 10, BaseCapacity = 2, Footprint = Vector2Int.one };

            // 1. Try placing in buildable cell (5, 5) -> Success
            var requestValid = new PlacementRequest(def, new Vector2Int(5, 5));
            var resultValid = system.ValidatePlacement(requestValid);
            Assert.IsTrue(resultValid.IsValid);
            Assert.AreEqual(PlacementStatus.Success, resultValid.Status);

            // 2. Try placing in Entrance cell (0, 0) -> NonBuildableCell
            var requestEntrance = new PlacementRequest(def, new Vector2Int(0, 0));
            var resultEntrance = system.ValidatePlacement(requestEntrance);
            Assert.IsFalse(resultEntrance.IsValid);
            Assert.AreEqual(PlacementStatus.NonBuildableCell, resultEntrance.Status);

            // 3. Try placing out of bounds (-1, -1) -> OutOfBounds
            var requestOob = new PlacementRequest(def, new Vector2Int(-1, -1));
            var resultOob = system.ValidatePlacement(requestOob);
            Assert.IsFalse(resultOob.IsValid);
            Assert.AreEqual(PlacementStatus.OutOfBounds, resultOob.Status);
        }

        [Test]
        public void DungeonRuntimeBuilder_InstantiationAndRegistry_CorrectlyRegistersMarkers()
        {
            var builderGo = Track(new GameObject("DungeonBuilder"));
            var builder = builderGo.AddComponent<DungeonRuntimeBuilder>();

            var graph = builder.GenerateDungeonForRun(999);

            Assert.IsNotNull(graph);
            Assert.IsTrue(builder.HasGenerated);

            // Registry checks
            Assert.IsTrue(DungeonRoomRegistry.HasEntrance, "Registry must have entrance flagged true.");
            Assert.AreEqual(Vector3.zero, DungeonRoomRegistry.EntrancePosition, "Entrance position must be at origin.");
            Assert.AreEqual(graph.Rooms.Count, DungeonRoomRegistry.Rooms.Count, "All rooms must be registered in DungeonRoomRegistry.");
            Assert.AreEqual(graph.Doors.Count, DungeonRoomRegistry.Doors.Count, "All doors must be registered in DungeonRoomRegistry.");

            // Check Boss door marker
            bool foundBossDoor = false;
            for (int i = 0; i < DungeonRoomRegistry.Doors.Count; i++)
            {
                if (DungeonRoomRegistry.Doors[i].IsBossDoor)
                {
                    foundBossDoor = true;
                    Assert.IsNotNull(DungeonRoomRegistry.Doors[i].GetComponent<BossDoorMarker>());
                    break;
                }
            }
            Assert.IsTrue(foundBossDoor, "At least one door must be registered as BossDoorMarker.");

            // Check Safe room marker
            bool foundSafeRoom = false;
            for (int i = 0; i < DungeonRoomRegistry.Rooms.Count; i++)
            {
                if (DungeonRoomRegistry.Rooms[i].IsSafeRoom)
                {
                    foundSafeRoom = true;
                    Assert.IsNotNull(DungeonRoomRegistry.Rooms[i].GetComponent<SafeRoomMarker>());
                    break;
                }
            }
            Assert.IsTrue(foundSafeRoom, "At least one room must be registered as SafeRoomMarker.");
        }
    }
}
