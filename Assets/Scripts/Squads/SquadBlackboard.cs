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
                return;
            }
        }

        knownEnemies.Add(new KnownEnemyRecord(target, position, currentTime, duration));
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
}
