using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# deterministic dungeon layout generator.
    /// Builds a connected room graph on a macro grid with entrance, normal rooms,
    /// safe room, dead-end branches, loops, and a single-door boss chamber.
    /// Zero UnityEngine references.
    /// </summary>
    public class DungeonLayoutGenerator
    {
        public DungeonLayoutGraph Generate(int seed, DungeonGeneratorConfig config = null)
        {
            var rng = new GameRngDungeonAdapter((uint)(seed == 0 ? 1 : Math.Abs(seed)));
            return Generate(rng, config ?? new DungeonGeneratorConfig());
        }

        public DungeonLayoutGraph Generate(IDungeonRng rng, DungeonGeneratorConfig config)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            config = config ?? new DungeonGeneratorConfig();

            var graph = new DungeonLayoutGraph();
            var occupied = new Dictionary<GridCoord, DungeonLayoutNode>();
            var allNodes = new List<DungeonLayoutNode>();

            int nextRoomId = 1;
            int nextDoorId = 1;

            // 1. Place Entrance at (0, 0)
            var entrance = new DungeonLayoutNode(nextRoomId++, DungeonRoomType.Entrance, new GridCoord(0, 0), depth: 0);
            graph.AddRoom(entrance);
            occupied[entrance.MacroCoord] = entrance;
            allNodes.Add(entrance);

            // 2. Determine target normal rooms count
            int targetNormalRooms = rng.Range(config.minNormalRooms, config.maxNormalRooms + 1);

            // 3. Grow tree (Main spine + side branches)
            var spineTip = entrance;

            for (int i = 0; i < targetNormalRooms; i++)
            {
                DungeonLayoutNode parentNode = null;

                // Branching decision
                bool branch = allNodes.Count > 2 && rng.Chance(config.branchProbability);

                if (!branch && GetFreeDirections(spineTip.MacroCoord, occupied).Count > 0)
                {
                    parentNode = spineTip;
                }
                else
                {
                    // Find nodes with available neighbors
                    var candidates = new List<DungeonLayoutNode>();
                    for (int n = 0; n < allNodes.Count; n++)
                    {
                        if (GetFreeDirections(allNodes[n].MacroCoord, occupied).Count > 0)
                        {
                            candidates.Add(allNodes[n]);
                        }
                    }

                    if (candidates.Count == 0)
                    {
                        // Grid saturated around current nodes (extremely rare with 5-8 rooms)
                        break;
                    }

                    if (branch)
                    {
                        int idx = rng.Range(0, candidates.Count);
                        parentNode = candidates[idx];
                    }
                    else
                    {
                        // Pick deepest candidate to extend the dungeon
                        candidates.Sort((a, b) => b.Depth.CompareTo(a.Depth));
                        parentNode = candidates[0];
                    }
                }

                var freeDirs = GetFreeDirections(parentNode.MacroCoord, occupied);
                if (freeDirs.Count == 0) continue;

                int dirIdx = rng.Range(0, freeDirs.Count);
                var chosenDir = freeDirs[dirIdx];
                var newCoord = parentNode.MacroCoord.Offset(chosenDir);

                var newRoom = new DungeonLayoutNode(
                    nextRoomId++,
                    DungeonRoomType.Normal,
                    newCoord,
                    depth: parentNode.Depth + 1
                );

                graph.AddRoom(newRoom);
                occupied[newCoord] = newRoom;
                allNodes.Add(newRoom);

                var door = new DungeonDoorConnection(
                    nextDoorId++,
                    parentNode.RoomId,
                    newRoom.RoomId,
                    chosenDir,
                    isBossDoor: false
                );
                graph.AddDoor(door);

                if (parentNode == spineTip)
                {
                    spineTip = newRoom;
                }
            }

            // 4. Place Boss Room at deepest leaf node with strictly 1 entrance door
            PlaceBossRoom(graph, occupied, allNodes, rng, ref nextRoomId, ref nextDoorId);

            // 5. Place Safe Room along the deeper half of the dungeon (if configured)
            if (config.safeRoomCount > 0)
            {
                PlaceSafeRoom(graph, occupied, allNodes, rng, ref nextRoomId, ref nextDoorId);
            }

            // 6. Generate loops between adjacent non-boss rooms
            if (config.loopProbability > 0f)
            {
                CreateLoops(graph, occupied, allNodes, rng, config.loopProbability, ref nextDoorId);
            }

            // 7. Verify reachability & layout contracts
            if (!graph.AreAllRoomsReachableFromEntrance())
            {
                throw new InvalidOperationException("[DungeonLayoutGenerator] Graph generation produced unreachable rooms!");
            }

            if (graph.BossNode == null)
            {
                throw new InvalidOperationException("[DungeonLayoutGenerator] Graph generation failed to place a Boss room!");
            }

            if (graph.BossNode.DoorIds.Count != 1)
            {
                throw new InvalidOperationException($"[DungeonLayoutGenerator] Boss room must have exactly 1 door, but has {graph.BossNode.DoorIds.Count}!");
            }

            return graph;
        }

        private static List<CardinalDirection> GetFreeDirections(
            GridCoord coord,
            Dictionary<GridCoord, DungeonLayoutNode> occupied)
        {
            var free = new List<CardinalDirection>(4);
            for (int i = 0; i < CardinalDirectionExtensions.All.Length; i++)
            {
                var dir = CardinalDirectionExtensions.All[i];
                var neighborCoord = coord.Offset(dir);
                if (!occupied.ContainsKey(neighborCoord))
                {
                    free.Add(dir);
                }
            }
            return free;
        }

        private void PlaceBossRoom(
            DungeonLayoutGraph graph,
            Dictionary<GridCoord, DungeonLayoutNode> occupied,
            List<DungeonLayoutNode> allNodes,
            IDungeonRng rng,
            ref int nextRoomId,
            ref int nextDoorId)
        {
            // Find all leaf nodes (rooms with only 1 door) among Normal rooms
            var leaves = new List<DungeonLayoutNode>();
            for (int i = 0; i < allNodes.Count; i++)
            {
                var node = allNodes[i];
                if (node.RoomType == DungeonRoomType.Normal && node.DoorIds.Count == 1)
                {
                    leaves.Add(node);
                }
            }

            if (leaves.Count == 0)
            {
                // Fallback: pick deepest normal room
                for (int i = 0; i < allNodes.Count; i++)
                {
                    if (allNodes[i].RoomType == DungeonRoomType.Normal) leaves.Add(allNodes[i]);
                }
            }

            // Sort by depth descending
            leaves.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            // Try to find a leaf that has a free neighbor to extend Boss room into
            for (int i = 0; i < leaves.Count; i++)
            {
                var candidate = leaves[i];
                var freeDirs = GetFreeDirections(candidate.MacroCoord, occupied);
                if (freeDirs.Count > 0)
                {
                    int dirIdx = rng.Range(0, freeDirs.Count);
                    var dir = freeDirs[dirIdx];
                    var bossCoord = candidate.MacroCoord.Offset(dir);

                    var bossRoom = new DungeonLayoutNode(
                        nextRoomId++,
                        DungeonRoomType.Boss,
                        bossCoord,
                        depth: candidate.Depth + 1
                    );

                    graph.AddRoom(bossRoom);
                    occupied[bossCoord] = bossRoom;
                    allNodes.Add(bossRoom);

                    var bossDoor = new DungeonDoorConnection(
                        nextDoorId++,
                        candidate.RoomId,
                        bossRoom.RoomId,
                        dir,
                        isBossDoor: true
                    );
                    graph.AddDoor(bossDoor);
                    return;
                }
            }

            // Fallback: deepest leaf itself becomes the Boss room, and its existing door becomes the boss door
            var deepest = leaves[0];
            deepest.RoomType = DungeonRoomType.Boss;
            graph.AddRoom(deepest); // Updates BossNode reference in graph

            if (deepest.DoorIds.Count > 0 && graph.TryGetDoor(deepest.DoorIds[0], out var existingDoor))
            {
                // Replace with boss door flag
                // Since DungeonDoorConnection is immutable, re-add door
                var replacedDoor = new DungeonDoorConnection(
                    existingDoor.DoorId,
                    existingDoor.RoomAId,
                    existingDoor.RoomBId,
                    existingDoor.DirectionFromA,
                    isBossDoor: true
                );
                graph.AddDoor(replacedDoor);
            }
        }

        private void PlaceSafeRoom(
            DungeonLayoutGraph graph,
            Dictionary<GridCoord, DungeonLayoutNode> occupied,
            List<DungeonLayoutNode> allNodes,
            IDungeonRng rng,
            ref int nextRoomId,
            ref int nextDoorId)
        {
            int bossDepth = graph.BossNode != null ? graph.BossNode.Depth : 4;
            int minSafeDepth = Math.Max(1, bossDepth / 2);

            // Find deep normal rooms
            var deepRooms = new List<DungeonLayoutNode>();
            for (int i = 0; i < allNodes.Count; i++)
            {
                var node = allNodes[i];
                if (node.RoomType == DungeonRoomType.Normal && node.Depth >= minSafeDepth)
                {
                    deepRooms.Add(node);
                }
            }

            if (deepRooms.Count == 0)
            {
                for (int i = 0; i < allNodes.Count; i++)
                {
                    if (allNodes[i].RoomType == DungeonRoomType.Normal) deepRooms.Add(allNodes[i]);
                }
            }

            // Sort by depth descending so safe room is near the boss approach
            deepRooms.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            // Try to branch a safe room off one of the deep rooms
            for (int i = 0; i < deepRooms.Count; i++)
            {
                var candidate = deepRooms[i];
                var freeDirs = GetFreeDirections(candidate.MacroCoord, occupied);
                if (freeDirs.Count > 0)
                {
                    int dirIdx = rng.Range(0, freeDirs.Count);
                    var dir = freeDirs[dirIdx];
                    var safeCoord = candidate.MacroCoord.Offset(dir);

                    var safeRoom = new DungeonLayoutNode(
                        nextRoomId++,
                        DungeonRoomType.SafeRoom,
                        safeCoord,
                        depth: candidate.Depth + 1
                    );

                    graph.AddRoom(safeRoom);
                    occupied[safeCoord] = safeRoom;
                    allNodes.Add(safeRoom);

                    var safeDoor = new DungeonDoorConnection(
                        nextDoorId++,
                        candidate.RoomId,
                        safeRoom.RoomId,
                        dir,
                        isBossDoor: false
                    );
                    graph.AddDoor(safeDoor);
                    return;
                }
            }

            // Fallback: convert candidate deep room to SafeRoom
            if (deepRooms.Count > 0)
            {
                deepRooms[0].RoomType = DungeonRoomType.SafeRoom;
                graph.AddRoom(deepRooms[0]);
            }
        }

        private void CreateLoops(
            DungeonLayoutGraph graph,
            Dictionary<GridCoord, DungeonLayoutNode> occupied,
            List<DungeonLayoutNode> allNodes,
            IDungeonRng rng,
            float loopProbability,
            ref int nextDoorId)
        {
            // Iterate all rooms deterministically
            for (int i = 0; i < allNodes.Count; i++)
            {
                var roomA = allNodes[i];
                // Boss room must NEVER participate in loops (strictly 1 entrance!)
                if (roomA.IsBoss) continue;

                for (int d = 0; d < CardinalDirectionExtensions.All.Length; d++)
                {
                    var dir = CardinalDirectionExtensions.All[d];
                    var neighborCoord = roomA.MacroCoord.Offset(dir);

                    if (occupied.TryGetValue(neighborCoord, out var roomB))
                    {
                        // Boss room must NEVER participate in loops
                        if (roomB.IsBoss) continue;

                        // Check each unordered pair once
                        if (roomA.RoomId < roomB.RoomId && !roomA.HasDoor(dir))
                        {
                            if (rng.Chance(loopProbability))
                            {
                                var loopDoor = new DungeonDoorConnection(
                                    nextDoorId++,
                                    roomA.RoomId,
                                    roomB.RoomId,
                                    dir,
                                    isBossDoor: false
                                );
                                graph.AddDoor(loopDoor);
                            }
                        }
                    }
                }
            }
        }
    }
}
