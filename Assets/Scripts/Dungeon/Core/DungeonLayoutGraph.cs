using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# complete graph representation of the generated dungeon layout.
    /// Provides room/door lookup, pathfinding, and tile-level spatial occupancy queries.
    /// Zero UnityEngine references.
    /// </summary>
    public class DungeonLayoutGraph
    {
        private readonly Dictionary<int, DungeonLayoutNode> _roomsById = new Dictionary<int, DungeonLayoutNode>();
        private readonly Dictionary<GridCoord, DungeonLayoutNode> _roomsByCoord = new Dictionary<GridCoord, DungeonLayoutNode>();
        private readonly List<DungeonDoorConnection> _doors = new List<DungeonDoorConnection>();
        private readonly Dictionary<int, DungeonDoorConnection> _doorsById = new Dictionary<int, DungeonDoorConnection>();

        public IReadOnlyDictionary<int, DungeonLayoutNode> Rooms => _roomsById;
        public IReadOnlyList<DungeonDoorConnection> Doors => _doors;

        public DungeonLayoutNode EntranceNode { get; private set; }
        public DungeonLayoutNode BossNode { get; private set; }
        public DungeonLayoutNode SafeRoomNode { get; private set; }

        public int MinMacroX { get; private set; }
        public int MaxMacroX { get; private set; }
        public int MinMacroY { get; private set; }
        public int MaxMacroY { get; private set; }

        public void AddRoom(DungeonLayoutNode room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            _roomsById[room.RoomId] = room;
            _roomsByCoord[room.MacroCoord] = room;

            if (_roomsById.Count == 1)
            {
                MinMacroX = MaxMacroX = room.MacroCoord.X;
                MinMacroY = MaxMacroY = room.MacroCoord.Y;
            }
            else
            {
                MinMacroX = Math.Min(MinMacroX, room.MacroCoord.X);
                MaxMacroX = Math.Max(MaxMacroX, room.MacroCoord.X);
                MinMacroY = Math.Min(MinMacroY, room.MacroCoord.Y);
                MaxMacroY = Math.Max(MaxMacroY, room.MacroCoord.Y);
            }

            if (room.RoomType == DungeonRoomType.Entrance) EntranceNode = room;
            else if (room.RoomType == DungeonRoomType.Boss) BossNode = room;
            else if (room.RoomType == DungeonRoomType.SafeRoom) SafeRoomNode = room;
        }

        public void AddDoor(DungeonDoorConnection door)
        {
            if (door == null) throw new ArgumentNullException(nameof(door));
            _doors.Add(door);
            _doorsById[door.DoorId] = door;

            if (_roomsById.TryGetValue(door.RoomAId, out var roomA))
            {
                roomA.AddDoor(door.DoorId, door.DirectionFromA);
            }

            if (_roomsById.TryGetValue(door.RoomBId, out var roomB))
            {
                roomB.AddDoor(door.DoorId, door.DirectionFromA.GetOpposite());
            }
        }

        public DungeonLayoutNode GetRoomAtMacro(GridCoord coord) =>
            _roomsByCoord.TryGetValue(coord, out var room) ? room : null;

        public DungeonLayoutNode GetRoomAtMacro(int x, int y) => GetRoomAtMacro(new GridCoord(x, y));

        public bool TryGetRoom(int roomId, out DungeonLayoutNode room) =>
            _roomsById.TryGetValue(roomId, out room);

        public bool TryGetDoor(int doorId, out DungeonDoorConnection door) =>
            _doorsById.TryGetValue(doorId, out door);

        /// <summary>
        /// Checks whether targetRoomId is reachable from startRoomId using BFS traversal.
        /// </summary>
        public bool IsReachable(int startRoomId, int targetRoomId)
        {
            if (startRoomId == targetRoomId) return true;
            if (!_roomsById.ContainsKey(startRoomId) || !_roomsById.ContainsKey(targetRoomId)) return false;

            var visited = new HashSet<int> { startRoomId };
            var queue = new Queue<int>();
            queue.Enqueue(startRoomId);

            while (queue.Count > 0)
            {
                int currentId = queue.Dequeue();
                if (currentId == targetRoomId) return true;

                if (_roomsById.TryGetValue(currentId, out var currentRoom))
                {
                    for (int i = 0; i < currentRoom.DoorIds.Count; i++)
                    {
                        int doorId = currentRoom.DoorIds[i];
                        if (_doorsById.TryGetValue(doorId, out var door))
                        {
                            int neighborId = door.GetOppositeRoomId(currentId);
                            if (neighborId != -1 && visited.Add(neighborId))
                            {
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies that all rooms in the graph are reachable from the entrance room.
        /// </summary>
        public bool AreAllRoomsReachableFromEntrance()
        {
            if (EntranceNode == null) return false;

            var visited = new HashSet<int> { EntranceNode.RoomId };
            var queue = new Queue<int>();
            queue.Enqueue(EntranceNode.RoomId);

            while (queue.Count > 0)
            {
                int currentId = queue.Dequeue();
                if (_roomsById.TryGetValue(currentId, out var currentRoom))
                {
                    for (int i = 0; i < currentRoom.DoorIds.Count; i++)
                    {
                        int doorId = currentRoom.DoorIds[i];
                        if (_doorsById.TryGetValue(doorId, out var door))
                        {
                            int neighborId = door.GetOppositeRoomId(currentId);
                            if (neighborId != -1 && visited.Add(neighborId))
                            {
                                queue.Enqueue(neighborId);
                            }
                        }
                    }
                }
            }

            return visited.Count == _roomsById.Count;
        }

        // =========================================================================
        // Spatial Tile-Grid Mapping & Placement Bounds
        // =========================================================================

        public void GetRoomTileBounds(
            DungeonLayoutNode room,
            DungeonGeneratorConfig config,
            out GridCoord minTile,
            out GridCoord maxTile)
        {
            int stepX = config.roomWidth + config.corridorLength;
            int stepY = config.roomHeight + config.corridorLength;

            int centerX = room.MacroCoord.X * stepX;
            int centerY = room.MacroCoord.Y * stepY;

            int halfW = config.roomWidth / 2;
            int halfH = config.roomHeight / 2;

            int minX = centerX - halfW;
            int maxX = minX + config.roomWidth - 1;
            int minY = centerY - halfH;
            int maxY = minY + config.roomHeight - 1;

            minTile = new GridCoord(minX, minY);
            maxTile = new GridCoord(maxX, maxY);
        }

        public void GetCorridorTileBounds(
            DungeonDoorConnection door,
            DungeonGeneratorConfig config,
            out GridCoord minTile,
            out GridCoord maxTile)
        {
            if (!_roomsById.TryGetValue(door.RoomAId, out var roomA) ||
                !_roomsById.TryGetValue(door.RoomBId, out var roomB) ||
                config.corridorLength <= 0)
            {
                minTile = new GridCoord(0, 0);
                maxTile = new GridCoord(-1, -1);
                return;
            }

            GetRoomTileBounds(roomA, config, out var minA, out var maxA);
            GetRoomTileBounds(roomB, config, out var minB, out var maxB);

            int halfCorridor = Math.Max(1, config.corridorWidth) / 2;

            if (door.DirectionFromA == CardinalDirection.East)
            {
                int startX = maxA.X + 1;
                int endX = minB.X - 1;
                int midY = (minA.Y + maxA.Y) / 2;
                minTile = new GridCoord(startX, midY - halfCorridor);
                maxTile = new GridCoord(endX, minTile.Y + config.corridorWidth - 1);
            }
            else if (door.DirectionFromA == CardinalDirection.West)
            {
                int startX = maxB.X + 1;
                int endX = minA.X - 1;
                int midY = (minA.Y + maxA.Y) / 2;
                minTile = new GridCoord(startX, midY - halfCorridor);
                maxTile = new GridCoord(endX, minTile.Y + config.corridorWidth - 1);
            }
            else if (door.DirectionFromA == CardinalDirection.North)
            {
                int startY = maxA.Y + 1;
                int endY = minB.Y - 1;
                int midX = (minA.X + maxA.X) / 2;
                minTile = new GridCoord(midX - halfCorridor, startY);
                maxTile = new GridCoord(minTile.X + config.corridorWidth - 1, endY);
            }
            else // South
            {
                int startY = maxB.Y + 1;
                int endY = minA.Y - 1;
                int midX = (minA.X + maxA.X) / 2;
                minTile = new GridCoord(midX - halfCorridor, startY);
                maxTile = new GridCoord(minTile.X + config.corridorWidth - 1, endY);
            }
        }

        /// <summary>
        /// Calculates the continuous tile-space coordinate of a doorway.
        /// If corridorLength > 0, returns the center of the corridor.
        /// If corridorLength == 0, returns the exact seam between the two adjacent touching rooms.
        /// Zero UnityEngine references.
        /// </summary>
        public bool TryGetDoorTilePosition(
            DungeonDoorConnection door,
            DungeonGeneratorConfig config,
            out float posX,
            out float posY)
        {
            if (!_roomsById.TryGetValue(door.RoomAId, out var roomA) ||
                !_roomsById.TryGetValue(door.RoomBId, out var roomB))
            {
                posX = posY = 0f;
                return false;
            }

            if (config.corridorLength == 0)
            {
                int stepX = config.roomWidth;
                int stepY = config.roomHeight;
                float centerAX = roomA.MacroCoord.X * stepX;
                float centerAY = roomA.MacroCoord.Y * stepY;
                float centerBX = roomB.MacroCoord.X * stepX;
                float centerBY = roomB.MacroCoord.Y * stepY;
                posX = (centerAX + centerBX) * 0.5f;
                posY = (centerAY + centerBY) * 0.5f;
                return true;
            }
            else
            {
                GetCorridorTileBounds(door, config, out var cMin, out var cMax);
                posX = (cMin.X + cMax.X) * 0.5f;
                posY = (cMin.Y + cMax.Y) * 0.5f;
                return true;
            }
        }

        public void GetTotalTileBounds(
            DungeonGeneratorConfig config,
            out GridCoord minTile,
            out GridCoord maxTile)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var room in _roomsById.Values)
            {
                GetRoomTileBounds(room, config, out var rMin, out var rMax);
                minX = Math.Min(minX, rMin.X);
                minY = Math.Min(minY, rMin.Y);
                maxX = Math.Max(maxX, rMax.X);
                maxY = Math.Max(maxY, rMax.Y);
            }

            minTile = new GridCoord(minX, minY);
            maxTile = new GridCoord(maxX, maxY);
        }

        /// <summary>
        /// Pure C# placement rule check:
        /// Cells inside Normal, SafeRoom, Boss rooms and connecting corridors are buildable.
        /// Cells inside Entrance room and outside dungeon bounds are non-buildable.
        /// </summary>
        public bool IsCellBuildable(int cellX, int cellY, DungeonGeneratorConfig config)
        {
            // 1. Check if inside Entrance room -> strictly NON-buildable
            if (EntranceNode != null)
            {
                GetRoomTileBounds(EntranceNode, config, out var eMin, out var eMax);
                if (cellX >= eMin.X && cellX <= eMax.X && cellY >= eMin.Y && cellY <= eMax.Y)
                {
                    return false;
                }
            }

            // 2. Check if inside any Normal, SafeRoom, or Boss room -> buildable
            foreach (var room in _roomsById.Values)
            {
                if (room.RoomType == DungeonRoomType.Entrance) continue;

                GetRoomTileBounds(room, config, out var rMin, out var rMax);
                if (cellX >= rMin.X && cellX <= rMax.X && cellY >= rMin.Y && cellY <= rMax.Y)
                {
                    return true;
                }
            }

            // 3. Check if inside any corridor -> buildable
            if (config.corridorLength > 0)
            {
                for (int i = 0; i < _doors.Count; i++)
                {
                    GetCorridorTileBounds(_doors[i], config, out var cMin, out var cMax);
                    if (cellX >= cMin.X && cellX <= cMax.X && cellY >= cMin.Y && cellY <= cMax.Y)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Collects all buildable tile coordinates across the dungeon.
        /// </summary>
        public HashSet<GridCoord> GetBuildableCells(DungeonGeneratorConfig config)
        {
            var buildableCells = new HashSet<GridCoord>();

            // All rooms except Entrance
            foreach (var room in _roomsById.Values)
            {
                if (room.RoomType == DungeonRoomType.Entrance) continue;

                GetRoomTileBounds(room, config, out var rMin, out var rMax);
                for (int x = rMin.X; x <= rMax.X; x++)
                {
                    for (int y = rMin.Y; y <= rMax.Y; y++)
                    {
                        buildableCells.Add(new GridCoord(x, y));
                    }
                }
            }

            // All corridors
            for (int i = 0; i < _doors.Count; i++)
            {
                GetCorridorTileBounds(_doors[i], config, out var cMin, out var cMax);
                for (int x = cMin.X; x <= cMax.X; x++)
                {
                    for (int y = cMin.Y; y <= cMax.Y; y++)
                    {
                        buildableCells.Add(new GridCoord(x, y));
                    }
                }
            }

            return buildableCells;
        }
    }
}
