using System;
using System.Collections.Generic;
using SBGames.Dungeon.Markers;
using UnityEngine;

namespace SBGames.Placement
{
    /// <summary>
    /// Unity adapter implementing IRoomCapacityTracker.
    /// Bridges DungeonPlacementSystem with DungeonRoomRegistry and RoomVolume markers in the scene.
    /// </summary>
    public class DungeonRoomCapacityTracker : IRoomCapacityTracker
    {
        private readonly DungeonPlacementManager _placementManager;
        private readonly Dictionary<int, HashSet<PlacedUnitInstance>> _unitsByRoom = new Dictionary<int, HashSet<PlacedUnitInstance>>(32);
        private readonly Dictionary<int, RoomVolume> _roomCache = new Dictionary<int, RoomVolume>(32);

        public DungeonRoomCapacityTracker(DungeonPlacementManager placementManager)
        {
            _placementManager = placementManager;
        }

        private RoomVolume GetRoomForGridPos(Vector2Int gridPosition)
        {
            Vector3 worldPos = _placementManager != null
                ? _placementManager.GridToWorldPosition(gridPosition)
                : new Vector3(gridPosition.x, gridPosition.y, 0f);

            var room = DungeonRoomRegistry.GetRoomAt(worldPos);
            if (room != null && !_roomCache.ContainsKey(room.RoomId))
            {
                _roomCache[room.RoomId] = room;
            }
            return room;
        }

        private RoomVolume GetRoomById(int roomId)
        {
            if (_roomCache.TryGetValue(roomId, out var cached) && cached != null)
            {
                return cached;
            }

            var rooms = DungeonRoomRegistry.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] != null && rooms[i].RoomId == roomId)
                {
                    _roomCache[roomId] = rooms[i];
                    return rooms[i];
                }
            }

            return null;
        }

        public bool HasRoomAt(Vector2Int gridPosition)
        {
            return GetRoomForGridPos(gridPosition) != null;
        }

        public int GetRoomId(Vector2Int gridPosition)
        {
            var room = GetRoomForGridPos(gridPosition);
            return room != null ? room.RoomId : -1;
        }

        public int GetRoomCapacityUsed(int roomId)
        {
            if (!_unitsByRoom.TryGetValue(roomId, out var units) || units == null || units.Count == 0)
            {
                return 0;
            }

            int total = 0;
            foreach (var unit in units)
            {
                if (unit != null)
                {
                    total += unit.CapacityConsumed;
                }
            }
            return total;
        }

        public int GetRoomSoftCapacity(int roomId)
        {
            var room = GetRoomById(roomId);
            return room != null ? room.SoftMonsterCapacity : 4;
        }

        public int GetRoomMaxCapacity(int roomId)
        {
            var room = GetRoomById(roomId);
            return room != null ? room.MaxMonsterCapacity : 6;
        }

        public bool CanAccommodate(int roomId, int capacityRequired)
        {
            int max = GetRoomMaxCapacity(roomId);
            int current = GetRoomCapacityUsed(roomId);
            return (current + capacityRequired) <= max;
        }

        public void RegisterUnit(int roomId, PlacedUnitInstance unit)
        {
            if (unit == null) return;

            if (!_unitsByRoom.TryGetValue(roomId, out var units))
            {
                units = new HashSet<PlacedUnitInstance>();
                _unitsByRoom[roomId] = units;
            }

            units.Add(unit);
        }

        public void UnregisterUnit(int roomId, PlacedUnitInstance unit)
        {
            if (unit == null) return;

            if (_unitsByRoom.TryGetValue(roomId, out var units) && units != null)
            {
                units.Remove(unit);
            }
        }

        public void UnregisterUnitFromAnyRoom(PlacedUnitInstance unit)
        {
            if (unit == null) return;

            foreach (var pair in _unitsByRoom)
            {
                if (pair.Value != null && pair.Value.Remove(unit))
                {
                    return;
                }
            }
        }

        public void Clear()
        {
            _unitsByRoom.Clear();
            _roomCache.Clear();
        }
    }
}
