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
