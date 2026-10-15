namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# doorway connection between two adjacent dungeon rooms on the macro grid.
    /// Zero UnityEngine references.
    /// </summary>
    public class DungeonDoorConnection
    {
        public int DoorId { get; }
        public int RoomAId { get; }
        public int RoomBId { get; }
        public CardinalDirection DirectionFromA { get; }
        public bool IsBossDoor { get; }

        public DungeonDoorConnection(
            int doorId,
            int roomAId,
            int roomBId,
            CardinalDirection directionFromA,
            bool isBossDoor = false)
        {
            DoorId = doorId;
            RoomAId = roomAId;
            RoomBId = roomBId;
            DirectionFromA = directionFromA;
            IsBossDoor = isBossDoor;
        }

        public int GetOppositeRoomId(int roomId)
        {
            if (roomId == RoomAId) return RoomBId;
            if (roomId == RoomBId) return RoomAId;
            return -1;
        }

        public CardinalDirection GetDirectionFrom(int roomId)
        {
            if (roomId == RoomAId) return DirectionFromA;
            if (roomId == RoomBId) return DirectionFromA.GetOpposite();
            return DirectionFromA;
        }
    }
}
