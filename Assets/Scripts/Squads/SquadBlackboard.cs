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
    [SerializeField] private System.Collections.Generic.List<KnownEnemyRecord> knownEnemies = new(8);
    [SerializeField] private System.Collections.Generic.List<CharacterAIMemory.HazardMemoryRecord> sharedHazards = new(16);

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
    public System.Collections.Generic.List<KnownEnemyRecord> KnownEnemies => knownEnemies;
    public bool HasKnownEnemies => knownEnemies.Count > 0;
    public System.Collections.Generic.List<CharacterAIMemory.HazardMemoryRecord> SharedHazards => sharedHazards;
    public bool HasSharedHazards => sharedHazards.Count > 0;

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
}
