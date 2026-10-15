using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Pure C# interface for querying room boundaries, room capacities, and tracking placed units per room.
    /// Decouples DungeonPlacementSystem from Unity Scene RoomVolume markers.
    /// </summary>
    public interface IRoomCapacityTracker
    {
        /// <summary>
        /// Checks whether a given grid position falls within any registered room.
        /// Corridors or void areas return false.
        /// </summary>
        bool HasRoomAt(Vector2Int gridPosition);

        /// <summary>
        /// Gets the room ID corresponding to a given grid position, or -1 if not inside a room.
        /// </summary>
        int GetRoomId(Vector2Int gridPosition);

        /// <summary>
        /// Total capacity consumed by placed units in the specified room.
        /// </summary>
        int GetRoomCapacityUsed(int roomId);

        /// <summary>
        /// Soft capacity limit for the room. Exceeding this threshold incurs crowding penalties.
        /// </summary>
        int GetRoomSoftCapacity(int roomId);

        /// <summary>
        /// Hard capacity limit for the room. Placements exceeding this limit are rejected.
        /// </summary>
        int GetRoomMaxCapacity(int roomId);

        /// <summary>
        /// Checks whether the room has enough room capacity remaining to accommodate the given capacity.
        /// </summary>
        bool CanAccommodate(int roomId, int capacityRequired);

        /// <summary>
        /// Registers a placed unit instance with the room.
        /// </summary>
        void RegisterUnit(int roomId, PlacedUnitInstance unit);

        /// <summary>
        /// Unregisters a placed unit instance from the room.
        /// </summary>
        void UnregisterUnit(int roomId, PlacedUnitInstance unit);
    }

    /// <summary>
    /// Pure C# in-memory implementation of IRoomCapacityTracker for unit tests and headless simulations.
    /// </summary>
    public class DictionaryRoomCapacityTracker : IRoomCapacityTracker
    {
        public class RoomCapacityInfo
        {
            public int RoomId { get; set; }
            public int SoftCapacity { get; set; }
            public int MaxCapacity { get; set; }
            public HashSet<PlacedUnitInstance> PlacedUnits { get; } = new HashSet<PlacedUnitInstance>();

            public int CapacityUsed
            {
                get
                {
                    int total = 0;
                    foreach (var unit in PlacedUnits)
                    {
                        total += unit.CapacityConsumed;
                    }
                    return total;
                }
            }
        }

        private readonly Dictionary<Vector2Int, int> _tileToRoom = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<int, RoomCapacityInfo> _rooms = new Dictionary<int, RoomCapacityInfo>();

        public void DefineRoom(int roomId, int softCapacity, int maxCapacity, IEnumerable<Vector2Int> tiles)
        {
            var info = new RoomCapacityInfo
            {
                RoomId = roomId,
                SoftCapacity = softCapacity,
                MaxCapacity = maxCapacity
            };
            _rooms[roomId] = info;

            if (tiles != null)
            {
                foreach (var tile in tiles)
                {
                    _tileToRoom[tile] = roomId;
                }
            }
        }

        public bool HasRoomAt(Vector2Int gridPosition) => _tileToRoom.ContainsKey(gridPosition);

        public int GetRoomId(Vector2Int gridPosition) => _tileToRoom.TryGetValue(gridPosition, out int id) ? id : -1;

        public int GetRoomCapacityUsed(int roomId) => _rooms.TryGetValue(roomId, out var info) ? info.CapacityUsed : 0;

        public int GetRoomSoftCapacity(int roomId) => _rooms.TryGetValue(roomId, out var info) ? info.SoftCapacity : int.MaxValue;

        public int GetRoomMaxCapacity(int roomId) => _rooms.TryGetValue(roomId, out var info) ? info.MaxCapacity : int.MaxValue;

        public bool CanAccommodate(int roomId, int capacityRequired)
        {
            if (!_rooms.TryGetValue(roomId, out var info)) return true;
            return (info.CapacityUsed + capacityRequired) <= info.MaxCapacity;
        }

        public void RegisterUnit(int roomId, PlacedUnitInstance unit)
        {
            if (_rooms.TryGetValue(roomId, out var info) && unit != null)
            {
                info.PlacedUnits.Add(unit);
            }
        }

        public void UnregisterUnit(int roomId, PlacedUnitInstance unit)
        {
            if (_rooms.TryGetValue(roomId, out var info) && unit != null)
            {
                info.PlacedUnits.Remove(unit);
            }
        }

        public void Clear()
        {
            _tileToRoom.Clear();
            _rooms.Clear();
        }
    }
}
