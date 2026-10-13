using System;
using System.Collections.Generic;
using SBGames.Characters.AI;
using UnityEngine;

namespace SBGames.Loop
{
    [Serializable]
    public struct BlockedRetreatRecord
    {
        public string LeaderName;
        public CheckpointReason Reason;
        public float Timestamp;

        public BlockedRetreatRecord(string leaderName, CheckpointReason reason, float timestamp)
        {
            LeaderName = leaderName;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct PotionUsageRecord
    {
        public string UserName;
        public ConsumableItemCategory Category;
        public string ItemId;
        public float Timestamp;

        public PotionUsageRecord(string userName, ConsumableItemCategory category, string itemId, float timestamp)
        {
            UserName = userName;
            Category = category;
            ItemId = itemId;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct RoomExplorationRecord
    {
        public int RoomId;
        public bool Cleared;
        public float Timestamp;

        public RoomExplorationRecord(int roomId, bool cleared, float timestamp)
        {
            RoomId = roomId;
            Cleared = cleared;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct CheckpointReportRecord
    {
        public string LeaderName;
        public CheckpointDecision Decision;
        public CheckpointReason Reason;
        public float Timestamp;

        public CheckpointReportRecord(string leaderName, CheckpointDecision decision, CheckpointReason reason, float timestamp)
        {
            LeaderName = leaderName;
            Decision = decision;
            Reason = reason;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct UnitDeathReportRecord
    {
        public string UnitName;
        public Vector3 Position;
        public float Timestamp;

        public UnitDeathReportRecord(string unitName, Vector3 position, float timestamp)
        {
            UnitName = unitName;
            Position = position;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct UnitEscapeReportRecord
    {
        public string UnitId;
        public string UnitName;
        public float Timestamp;

        public UnitEscapeReportRecord(string unitId, string unitName, float timestamp)
        {
            UnitId = unitId;
            UnitName = unitName;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct LeaderChangeReportRecord
    {
        public string OldLeaderName;
        public string NewLeaderName;
        public float Timestamp;

        public LeaderChangeReportRecord(string oldLeaderName, string newLeaderName, float timestamp)
        {
            OldLeaderName = oldLeaderName;
            NewLeaderName = newLeaderName;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct WarpUsageRecord
    {
        public string UserName;
        public string ItemId;
        public string Outcome;
        public float Timestamp;

        public WarpUsageRecord(string userName, string itemId, string outcome, float timestamp)
        {
            UserName = userName;
            ItemId = itemId;
            Outcome = outcome;
            Timestamp = timestamp;
        }
    }

    [Serializable]
    public struct WarpForfeitRecord
    {
        public string UserName;
        public string ItemId;
        public int EnergyAwarded;
        public float Timestamp;

        public WarpForfeitRecord(string userName, string itemId, int energyAwarded, float timestamp)
        {
            UserName = userName;
            ItemId = itemId;
            EnergyAwarded = energyAwarded;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Pure C# telemetry report model captured by subscribing to <see cref="AdventurerEventHub"/> during combat.
    /// Uses preallocated, capacity-limited lists to eliminate runtime heap allocations.
    /// </summary>
    [Serializable]
    public class PostBattleReport
    {
        public const int MAX_CAPACITY = 32;

        [SerializeField] private List<BlockedRetreatRecord> blockedRetreats = new(8);
        [SerializeField] private List<PotionUsageRecord> potionsUsed = new(MAX_CAPACITY);
        [SerializeField] private List<RoomExplorationRecord> roomsExplored = new(MAX_CAPACITY);
        [SerializeField] private List<CheckpointReportRecord> checkpoints = new(MAX_CAPACITY);
        [SerializeField] private List<UnitDeathReportRecord> deaths = new(MAX_CAPACITY);
        [SerializeField] private List<UnitEscapeReportRecord> escapes = new(MAX_CAPACITY);
        [SerializeField] private List<LeaderChangeReportRecord> leaderChanges = new(8);
        [SerializeField] private List<WarpUsageRecord> warpUsages = new(MAX_CAPACITY);
        [SerializeField] private List<WarpForfeitRecord> warpForfeits = new(MAX_CAPACITY);

        public IReadOnlyList<BlockedRetreatRecord> BlockedRetreats => blockedRetreats;
        public IReadOnlyList<PotionUsageRecord> PotionsUsed => potionsUsed;
        public IReadOnlyList<RoomExplorationRecord> RoomsExplored => roomsExplored;
        public IReadOnlyList<CheckpointReportRecord> Checkpoints => checkpoints;
        public IReadOnlyList<UnitDeathReportRecord> Deaths => deaths;
        public IReadOnlyList<UnitEscapeReportRecord> Escapes => escapes;
        public IReadOnlyList<LeaderChangeReportRecord> LeaderChanges => leaderChanges;
        public IReadOnlyList<WarpUsageRecord> WarpUsages => warpUsages;
        public IReadOnlyList<WarpForfeitRecord> WarpForfeits => warpForfeits;

        public void RecordBlockedRetreat(string leaderName, CheckpointReason reason, float timestamp)
        {
            if (blockedRetreats.Count < MAX_CAPACITY)
            {
                blockedRetreats.Add(new BlockedRetreatRecord(leaderName, reason, timestamp));
            }
        }

        public void RecordPotionUsed(string userName, ConsumableItemCategory category, string itemId, float timestamp)
        {
            if (potionsUsed.Count < MAX_CAPACITY)
            {
                potionsUsed.Add(new PotionUsageRecord(userName, category, itemId, timestamp));
            }
        }

        public void RecordRoomEntered(int roomId, float timestamp)
        {
            if (roomsExplored.Count < MAX_CAPACITY)
            {
                roomsExplored.Add(new RoomExplorationRecord(roomId, false, timestamp));
            }
        }

        public void RecordRoomCleared(int roomId, float timestamp)
        {
            for (int i = 0; i < roomsExplored.Count; i++)
            {
                if (roomsExplored[i].RoomId == roomId)
                {
                    var r = roomsExplored[i];
                    r.Cleared = true;
                    roomsExplored[i] = r;
                    return;
                }
            }
            if (roomsExplored.Count < MAX_CAPACITY)
            {
                roomsExplored.Add(new RoomExplorationRecord(roomId, true, timestamp));
            }
        }

        public void RecordCheckpoint(string leaderName, CheckpointDecision decision, CheckpointReason reason, float timestamp)
        {
            if (checkpoints.Count < MAX_CAPACITY)
            {
                checkpoints.Add(new CheckpointReportRecord(leaderName, decision, reason, timestamp));
            }
        }

        public void RecordDeath(string unitName, Vector3 pos, float timestamp)
        {
            if (deaths.Count < MAX_CAPACITY)
            {
                deaths.Add(new UnitDeathReportRecord(unitName, pos, timestamp));
            }
        }

        public void RecordEscape(string unitId, string unitName, float timestamp)
        {
            if (escapes.Count < MAX_CAPACITY)
            {
                escapes.Add(new UnitEscapeReportRecord(unitId, unitName, timestamp));
            }
        }

        public void RecordLeaderChange(string oldLeader, string newLeader, float timestamp)
        {
            if (leaderChanges.Count < MAX_CAPACITY)
            {
                leaderChanges.Add(new LeaderChangeReportRecord(oldLeader, newLeader, timestamp));
            }
        }

        public void RecordWarpUsage(string userName, string itemId, string outcome, float timestamp)
        {
            if (warpUsages.Count < MAX_CAPACITY)
            {
                warpUsages.Add(new WarpUsageRecord(userName, itemId, outcome, timestamp));
            }
        }

        public void RecordWarpForfeit(string userName, string itemId, int energyAwarded, float timestamp)
        {
            if (warpForfeits.Count < MAX_CAPACITY)
            {
                warpForfeits.Add(new WarpForfeitRecord(userName, itemId, energyAwarded, timestamp));
            }
        }

        public void Clear()
        {
            blockedRetreats.Clear();
            potionsUsed.Clear();
            roomsExplored.Clear();
            checkpoints.Clear();
            deaths.Clear();
            escapes.Clear();
            leaderChanges.Clear();
            warpUsages.Clear();
            warpForfeits.Clear();
        }
    }
}
