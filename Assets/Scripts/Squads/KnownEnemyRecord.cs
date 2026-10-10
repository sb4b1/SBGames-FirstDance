using System;
using UnityEngine;

[Serializable]
public struct KnownEnemyRecord
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 lastKnownPosition;
    [SerializeField] private float lastSpottedTime;
    [SerializeField] private float expirationDuration;

    public Transform Target => target;
    public Vector3 LastKnownPosition => lastKnownPosition;
    public float LastSpottedTime => lastSpottedTime;
    public float ExpirationDuration => expirationDuration;

    public KnownEnemyRecord(Transform target, Vector3 lastKnownPosition, float lastSpottedTime, float expirationDuration = 6f)
    {
        this.target = target;
        this.lastKnownPosition = lastKnownPosition;
        this.lastSpottedTime = lastSpottedTime;
        this.expirationDuration = expirationDuration;
    }

    public void UpdatePosition(Vector3 newPosition, float currentTime)
    {
        lastKnownPosition = newPosition;
        lastSpottedTime = currentTime;
    }

    public bool IsValid(float currentTime)
    {
        if (target == null) return false;

        // Check if dead through CharacterStatsManager
        if (target.TryGetComponent<CharacterStatsManager>(out var stats) && stats.Life <= 0f)
        {
            return false;
        }

        return (currentTime - lastSpottedTime) <= expirationDuration;
    }
}
