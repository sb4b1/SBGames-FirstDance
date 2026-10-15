using System;

namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# configuration parameters for the deterministic dungeon layout generator.
    /// Zero UnityEngine references.
    /// </summary>
    [Serializable]
    public class DungeonGeneratorConfig
    {
        public int minNormalRooms = 5;
        public int maxNormalRooms = 8;
        public float branchProbability = 0.35f;
        public float loopProbability = 0f;
        public int safeRoomCount = 1;

        // Spatial metrics for tile placement and world positioning
        public int roomWidth = 10;
        public int roomHeight = 10;
        public int corridorLength = 4;
        public int corridorWidth = 2;

        public DungeonGeneratorConfig() { }

        public DungeonGeneratorConfig(
            int minNormalRooms,
            int maxNormalRooms,
            float branchProbability = 0.35f,
            float loopProbability = 0f,
            int safeRoomCount = 1,
            int roomWidth = 10,
            int roomHeight = 10,
            int corridorLength = 4,
            int corridorWidth = 2)
        {
            this.minNormalRooms = Math.Max(1, minNormalRooms);
            this.maxNormalRooms = Math.Max(this.minNormalRooms, maxNormalRooms);
            this.branchProbability = Math.Max(0f, Math.Min(1f, branchProbability));
            this.loopProbability = Math.Max(0f, Math.Min(1f, loopProbability));
            this.safeRoomCount = Math.Max(0, Math.Min(1, safeRoomCount));
            this.roomWidth = Math.Max(4, roomWidth);
            this.roomHeight = Math.Max(4, roomHeight);
            this.corridorLength = Math.Max(0, corridorLength);
            this.corridorWidth = Math.Max(1, corridorWidth);
        }
    }
}
