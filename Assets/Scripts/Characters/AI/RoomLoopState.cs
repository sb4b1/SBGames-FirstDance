namespace SBGames.Characters.AI
{
    public enum RoomLoopState
    {
        None = 0,
        Approach = 1,
        Staging = 2,
        ScoutPeek = 3,
        TankEnters = 4,
        Fight = 5,
        Regroup = 6,
        Checkpoint = 7,
        SafeRoomStop = 8
    }
}
