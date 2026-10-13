using System;
using UnityEngine;

[Serializable]
public class SquadBlackboard
{
    [SerializeField] private Transform leaderTransform;
    [SerializeField] private SquadOrderType currentOrder = SquadOrderType.None;
    [SerializeField] private Transform focusFireTarget;
    [SerializeField] private Vector3 rallyPoint;
    [SerializeField] private float averageSquadHealth = 1f;
    [SerializeField] private int livingMemberCount = 1;
    [SerializeField] private int orderVersion = 0;
    [SerializeField] private bool retreatBlocked = false;
    [SerializeField] private System.Collections.Generic.List<KnownEnemyRecord> knownEnemies = new(8);
    [SerializeField] private System.Collections.Generic.List<CharacterAIMemory.HazardMemoryRecord> sharedHazards = new(16);

    [System.Serializable]
    public struct DungeonRoomRecord
    {
        public int RoomId;
        public bool Seen;
        public bool Visited;
        public bool FullyExplored;
        public bool DeadEnd;
        public bool Dangerous;
        public Vector3 Center;

        public DungeonRoomRecord(int roomId, Vector3 center)
        {
            RoomId = roomId;
            Seen = true;
            Visited = false;
            FullyExplored = false;
            DeadEnd = false;
            Dangerous = false;
            Center = center;
        }
    }

    [System.Serializable]
    public struct DungeonDoorRecord
    {
        public int DoorId;
        public bool Seen;
        public bool Traversed;
        public bool IsBossDoor;
        public Vector3 Position;
        public int ConnectedRoomId;

        public DungeonDoorRecord(int doorId, Vector3 position, bool isBossDoor, int connectedRoomId)
        {
            DoorId = doorId;
            Seen = true;
            Traversed = false;
            IsBossDoor = isBossDoor;
            Position = position;
            ConnectedRoomId = connectedRoomId;
        }
    }

    [SerializeField] private Vector3 entrancePosition;
    [SerializeField] private bool hasEntrancePosition;
    [SerializeField] private bool bossDoorRecognized;
    [SerializeField] private Vector3 bossDoorPosition;
    [SerializeField] private int currentRoomId = -1;
    [SerializeField] private System.Collections.Generic.List<DungeonRoomRecord> knownRooms = new(32);
    [SerializeField] private System.Collections.Generic.List<DungeonDoorRecord> knownDoors = new(64);
    [SerializeField] private int[] visitedRoomHistory = new int[16];
    [SerializeField] private int visitedHistoryCount = 0;

    public Transform LeaderTransform
    {
        get => leaderTransform;
        set => leaderTransform = value;
    }

    public SquadOrderType CurrentOrder => currentOrder;
    public Transform FocusFireTarget => focusFireTarget;
    public Vector3 RallyPoint => rallyPoint;
    public float AverageSquadHealth => averageSquadHealth;
    public int LivingMemberCount => livingMemberCount;
    public int OrderVersion => orderVersion;
    public bool RetreatBlocked
    {
        get => retreatBlocked;
        set => retreatBlocked = value;
    }
    public System.Collections.Generic.List<KnownEnemyRecord> KnownEnemies => knownEnemies;
    public bool HasKnownEnemies => knownEnemies.Count > 0;
    public System.Collections.Generic.List<CharacterAIMemory.HazardMemoryRecord> SharedHazards => sharedHazards;
    public bool HasSharedHazards => sharedHazards.Count > 0;

    public Vector3 EntrancePosition
    {
        get => entrancePosition;
        set
        {
            entrancePosition = value;
            hasEntrancePosition = true;
        }
    }

    public bool HasEntrancePosition => hasEntrancePosition;
    public bool BossDoorRecognized => bossDoorRecognized;
    public Vector3 BossDoorPosition => bossDoorPosition;
    public int CurrentRoomId => currentRoomId;
    public System.Collections.Generic.IReadOnlyList<DungeonRoomRecord> KnownRooms => knownRooms;
    public System.Collections.Generic.IReadOnlyList<DungeonDoorRecord> KnownDoors => knownDoors;

    public void SetBossDoorRecognized(Vector3 position)
    {
        bossDoorRecognized = true;
        bossDoorPosition = position;
    }

    public void RecordRoomSeen(int roomId, Vector3 center, bool isDangerous = false)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId)
            {
                var r = knownRooms[i];
                r.Seen = true;
                if (isDangerous) r.Dangerous = true;
                knownRooms[i] = r;
                return;
            }
        }
        var record = new DungeonRoomRecord(roomId, center) { Dangerous = isDangerous };
        knownRooms.Add(record);
    }

    public void RecordRoomVisited(int roomId, Vector3 center)
    {
        currentRoomId = roomId;
        bool found = false;
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId)
            {
                var r = knownRooms[i];
                r.Visited = true;
                r.Seen = true;
                knownRooms[i] = r;
                found = true;
                break;
            }
        }
        if (!found)
        {
            var record = new DungeonRoomRecord(roomId, center) { Visited = true };
            knownRooms.Add(record);
        }

        // Add to ring buffer history
        if (visitedHistoryCount < visitedRoomHistory.Length)
        {
            visitedRoomHistory[visitedHistoryCount++] = roomId;
        }
        else
        {
            for (int i = 0; i < visitedRoomHistory.Length - 1; i++)
            {
                visitedRoomHistory[i] = visitedRoomHistory[i + 1];
            }
            visitedRoomHistory[visitedRoomHistory.Length - 1] = roomId;
        }
    }

    public void MarkRoomFullyExplored(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId)
            {
                var r = knownRooms[i];
                r.FullyExplored = true;
                knownRooms[i] = r;
                return;
            }
        }
        knownRooms.Add(new DungeonRoomRecord(roomId, Vector3.zero) { FullyExplored = true });
    }

    public void MarkRoomDeadEnd(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId)
            {
                var r = knownRooms[i];
                r.DeadEnd = true;
                r.FullyExplored = true;
                knownRooms[i] = r;
                return;
            }
        }
        knownRooms.Add(new DungeonRoomRecord(roomId, Vector3.zero) { DeadEnd = true, FullyExplored = true });
    }

    public bool IsRoomVisited(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId) return knownRooms[i].Visited;
        }
        return false;
    }

    public bool IsRoomFullyExplored(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId) return knownRooms[i].FullyExplored;
        }
        return false;
    }

    public bool IsRoomDeadEnd(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId) return knownRooms[i].DeadEnd;
        }
        return false;
    }

    public bool IsRoomDangerous(int roomId)
    {
        for (int i = 0; i < knownRooms.Count; i++)
        {
            if (knownRooms[i].RoomId == roomId) return knownRooms[i].Dangerous;
        }
        return false;
    }

    public void RecordDoorSeen(int doorId, Vector3 position, bool isBossDoor, int connectedRoomId)
    {
        for (int i = 0; i < knownDoors.Count; i++)
        {
            if (knownDoors[i].DoorId == doorId)
            {
                var d = knownDoors[i];
                d.Seen = true;
                if (isBossDoor) d.IsBossDoor = true;
                if (connectedRoomId != -1) d.ConnectedRoomId = connectedRoomId;
                knownDoors[i] = d;
                if (isBossDoor) SetBossDoorRecognized(position);
                return;
            }
        }
        knownDoors.Add(new DungeonDoorRecord(doorId, position, isBossDoor, connectedRoomId));
        if (isBossDoor) SetBossDoorRecognized(position);
    }

    public void MarkDoorTraversed(int doorId)
    {
        for (int i = 0; i < knownDoors.Count; i++)
        {
            if (knownDoors[i].DoorId == doorId)
            {
                var d = knownDoors[i];
                d.Traversed = true;
                knownDoors[i] = d;
                return;
            }
        }
        knownDoors.Add(new DungeonDoorRecord(doorId, Vector3.zero, false, -1) { Traversed = true });
    }

    public bool IsDoorTraversed(int doorId)
    {
        for (int i = 0; i < knownDoors.Count; i++)
        {
            if (knownDoors[i].DoorId == doorId) return knownDoors[i].Traversed;
        }
        return false;
    }

    public bool WasRoomVisitedRecently(int roomId, int withinLastN = 5)
    {
        int checkCount = Mathf.Min(withinLastN, visitedHistoryCount);
        for (int i = 0; i < checkCount; i++)
        {
            int index = visitedHistoryCount - 1 - i;
            if (index >= 0 && index < visitedRoomHistory.Length && visitedRoomHistory[index] == roomId)
            {
                return true;
            }
        }
        return false;
    }

    public event Action<Transform, Vector3> OnEnemyReported;
    public event Action<Vector2Int, Vector3, float> OnHazardReported;

    public void ReportEnemy(Transform target, Vector3 position, float currentTime, float duration = 6f)
    {
        if (target == null) return;

        for (int i = 0; i < knownEnemies.Count; i++)
        {
            if (knownEnemies[i].Target == target)
            {
                var record = knownEnemies[i];
                record.UpdatePosition(position, currentTime);
                knownEnemies[i] = record;
                OnEnemyReported?.Invoke(target, position);
                return;
            }
        }

        knownEnemies.Add(new KnownEnemyRecord(target, position, currentTime, duration));
        OnEnemyReported?.Invoke(target, position);
    }

    public void PruneExpiredEnemies(float currentTime)
    {
        for (int i = knownEnemies.Count - 1; i >= 0; i--)
        {
            if (!knownEnemies[i].IsValid(currentTime))
            {
                if (focusFireTarget == knownEnemies[i].Target)
                {
                    focusFireTarget = null;
                }
                knownEnemies.RemoveAt(i);
            }
        }
    }

    public void SetOrder(SquadOrderType order, Transform focusTarget = null, Vector3 customRallyPoint = default)
    {
        currentOrder = order;
        focusFireTarget = focusTarget;
        rallyPoint = customRallyPoint != default ? customRallyPoint : (leaderTransform != null ? leaderTransform.position : Vector3.zero);
        orderVersion++;
    }

    public void ClearOrder()
    {
        currentOrder = SquadOrderType.None;
        focusFireTarget = null;
        orderVersion++;
    }

    public void UpdateSquadMetrics(float avgHealth, int livingCount)
    {
        averageSquadHealth = avgHealth;
        livingMemberCount = livingCount;
    }

    public void ReportHazard(Vector2Int gridPos, Vector3 worldPos, float dangerLevel, float duration = 30f)
    {
        float expireTime = Time.time + duration;
        for (int i = 0; i < sharedHazards.Count; i++)
        {
            if (sharedHazards[i].GridPos == gridPos || (sharedHazards[i].WorldPos - worldPos).sqrMagnitude < 0.25f)
            {
                var existing = sharedHazards[i];
                existing.ExpirationTime = Mathf.Max(existing.ExpirationTime, expireTime);
                existing.DangerLevel = Mathf.Max(existing.DangerLevel, dangerLevel);
                sharedHazards[i] = existing;
                OnHazardReported?.Invoke(gridPos, worldPos, dangerLevel);
                return;
            }
        }

        if (sharedHazards.Count < 16)
        {
            sharedHazards.Add(new CharacterAIMemory.HazardMemoryRecord(gridPos, worldPos, dangerLevel, expireTime));
        }
        else
        {
            int oldestIndex = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < sharedHazards.Count; i++)
            {
                if (sharedHazards[i].ExpirationTime < oldestTime)
                {
                    oldestTime = sharedHazards[i].ExpirationTime;
                    oldestIndex = i;
                }
            }
            sharedHazards[oldestIndex] = new CharacterAIMemory.HazardMemoryRecord(gridPos, worldPos, dangerLevel, expireTime);
        }

        OnHazardReported?.Invoke(gridPos, worldPos, dangerLevel);
    }

    public bool IsPositionHazardous(Vector3 worldPos, float safetyRadius = 1.2f)
    {
        float sqrRadius = safetyRadius * safetyRadius;
        float currentTime = Time.time;
        for (int i = 0; i < sharedHazards.Count; i++)
        {
            var hazard = sharedHazards[i];
            if (hazard.ExpirationTime < currentTime) continue;
            if ((hazard.WorldPos - worldPos).sqrMagnitude <= sqrRadius)
            {
                return true;
            }
        }
        return false;
    }

    public void PruneExpiredHazards(float currentTime)
    {
        for (int i = sharedHazards.Count - 1; i >= 0; i--)
        {
            if (sharedHazards[i].ExpirationTime <= currentTime)
            {
                sharedHazards.RemoveAt(i);
            }
        }
    }

    public void ClearHazards()
    {
        sharedHazards.Clear();
    }

    public void CopyStateFrom(SquadBlackboard source)
    {
        if (source == null || ReferenceEquals(this, source)) return;

        currentOrder = source.currentOrder;
        focusFireTarget = source.focusFireTarget;
        rallyPoint = source.rallyPoint;
        averageSquadHealth = source.averageSquadHealth;
        livingMemberCount = source.livingMemberCount;
        orderVersion = source.orderVersion + 1;
        retreatBlocked = source.retreatBlocked;

        knownEnemies.Clear();
        for (int i = 0; i < source.knownEnemies.Count; i++)
        {
            knownEnemies.Add(source.knownEnemies[i]);
        }

        sharedHazards.Clear();
        for (int i = 0; i < source.sharedHazards.Count; i++)
        {
            sharedHazards.Add(source.sharedHazards[i]);
        }

        entrancePosition = source.entrancePosition;
        hasEntrancePosition = source.hasEntrancePosition;
        bossDoorRecognized = source.bossDoorRecognized;
        bossDoorPosition = source.bossDoorPosition;
        currentRoomId = source.currentRoomId;

        knownRooms.Clear();
        for (int i = 0; i < source.knownRooms.Count; i++)
        {
            knownRooms.Add(source.knownRooms[i]);
        }

        knownDoors.Clear();
        for (int i = 0; i < source.knownDoors.Count; i++)
        {
            knownDoors.Add(source.knownDoors[i]);
        }

        visitedHistoryCount = source.visitedHistoryCount;
        for (int i = 0; i < visitedRoomHistory.Length && i < source.visitedRoomHistory.Length; i++)
        {
            visitedRoomHistory[i] = source.visitedRoomHistory[i];
        }
    }
}
