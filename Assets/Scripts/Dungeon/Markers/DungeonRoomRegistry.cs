using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Dungeon.Markers
{
    public static class DungeonRoomRegistry
    {
        private static readonly List<RoomVolume> rooms = new List<RoomVolume>(32);
        private static readonly List<DoorMarker> doors = new List<DoorMarker>(64);
        private static Vector3 entrancePosition = Vector3.zero;
        private static bool hasEntrance = false;

        public static IReadOnlyList<RoomVolume> Rooms => rooms;
        public static IReadOnlyList<DoorMarker> Doors => doors;

        public static Vector3 EntrancePosition
        {
            get => entrancePosition;
            set
            {
                entrancePosition = value;
                hasEntrance = true;
            }
        }

        public static bool HasEntrance => hasEntrance;

        public static void RegisterRoom(RoomVolume room)
        {
            if (room != null && !rooms.Contains(room))
            {
                rooms.Add(room);
            }
        }

        public static void UnregisterRoom(RoomVolume room)
        {
            if (room != null)
            {
                rooms.Remove(room);
            }
        }

        public static void RegisterDoor(DoorMarker door)
        {
            if (door != null && !doors.Contains(door))
            {
                doors.Add(door);
            }
        }

        public static void UnregisterDoor(DoorMarker door)
        {
            if (door != null)
            {
                doors.Remove(door);
            }
        }

        public static RoomVolume GetRoomAt(Vector3 position)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room != null && room.Contains(position))
                {
                    return room;
                }
            }
            return null;
        }

        public static DoorMarker GetClosestDoor(Vector3 position, float maxRadius = 100f)
        {
            DoorMarker best = null;
            float bestDistSq = maxRadius * maxRadius;

            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door == null) continue;

                float distSq = (door.Position - position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = door;
                }
            }
            return best;
        }

        public static void Clear()
        {
            rooms.Clear();
            doors.Clear();
            hasEntrance = false;
            entrancePosition = Vector3.zero;
        }
    }
}
