using System.Collections.Generic;

namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# node representing a room on the macro dungeon layout grid.
    /// Zero UnityEngine references.
    /// </summary>
    public class DungeonLayoutNode
    {
        public int RoomId { get; }
        public DungeonRoomType RoomType { get; set; }
        public GridCoord MacroCoord { get; }
        public int Depth { get; set; }

        private readonly List<int> _doorIds = new List<int>();
        private readonly Dictionary<CardinalDirection, int> _doorsByDirection = new Dictionary<CardinalDirection, int>();

        public IReadOnlyList<int> DoorIds => _doorIds;
        public IReadOnlyDictionary<CardinalDirection, int> DoorsByDirection => _doorsByDirection;

        public bool IsEntrance => RoomType == DungeonRoomType.Entrance;
        public bool IsNormal => RoomType == DungeonRoomType.Normal;
        public bool IsSafeRoom => RoomType == DungeonRoomType.SafeRoom;
        public bool IsBoss => RoomType == DungeonRoomType.Boss;

        public DungeonLayoutNode(int roomId, DungeonRoomType roomType, GridCoord macroCoord, int depth = 0)
        {
            RoomId = roomId;
            RoomType = roomType;
            MacroCoord = macroCoord;
            Depth = depth;
        }

        public void AddDoor(int doorId, CardinalDirection direction)
        {
            if (!_doorIds.Contains(doorId))
            {
                _doorIds.Add(doorId);
            }
            _doorsByDirection[direction] = doorId;
        }

        public bool HasDoor(CardinalDirection direction) => _doorsByDirection.ContainsKey(direction);

        public bool TryGetDoor(CardinalDirection direction, out int doorId) =>
            _doorsByDirection.TryGetValue(direction, out doorId);
    }
}
