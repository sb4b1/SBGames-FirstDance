namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Structural role of a Dungeon Piece.
    /// Room: functional room (Entrance, Normal, SafeRoom, Boss).
    /// Pathway: corridor connector between rooms.
    /// Both can be placed and registered in DungeonRoomRegistry so AI can traverse them.
    /// </summary>
    public enum PieceRole
    {
        Room = 0,
        Pathway = 1
    }
}
