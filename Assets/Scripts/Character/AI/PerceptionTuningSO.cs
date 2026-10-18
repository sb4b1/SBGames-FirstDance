using UnityEngine;

public enum PerceptionTier
{
    Calm,
    Engaged,
    Critical,
    Default
}

[CreateAssetMenu(fileName = "PerceptionTuning", menuName = "SBGames/AI/Perception Tuning SO")]
public class PerceptionTuningSO : ScriptableObject
{
    [Header("Pulse Intervals (Seconds)")]
    [Tooltip("Slowest tier interval used when calm (idle, wandering, scouting, following leader without known enemies).")]
    [SerializeField, Range(0.05f, 1.0f)] private float calmInterval = 0.4f;

    [Tooltip("Baseline default interval used when state does not clearly fit other tiers.")]
    [SerializeField, Range(0.05f, 1.0f)] private float defaultInterval = 0.2f;

    [Tooltip("Fast tier interval used when engaged (has target or enemy within attack/threat range).")]
    [SerializeField, Range(0.02f, 0.5f)] private float engagedInterval = 0.12f;

    [Tooltip("Fastest tier interval used during critical events (low health, fleeing, Morale Shock, Survive/FallBack).")]
    [SerializeField, Range(0.0f, 0.2f)] private float criticalInterval = 0.05f;

    [Header("Hysteresis & Rate Limiting")]
    [Tooltip("Minimum dwell time in a tier before dropping to a slower tier (moving to faster tier is immediate).")]
    [SerializeField, Range(0.0f, 2.0f)] private float hysteresisDwellTime = 0.5f;

    [Tooltip("Minimum gap between forced pulses to prevent event bursts from spamming perception ticks.")]
    [SerializeField, Range(0.01f, 0.2f)] private float forcePulseMinGap = 0.05f;

    [Header("State Thresholds")]
    [Tooltip("Health percentage threshold [0..1] at or below which unit enters Critical tier.")]
    [SerializeField, Range(0.05f, 0.6f)] private float criticalHealthThreshold = 0.3f;

    [Tooltip("Fear level at or above which unit enters Critical tier (e.g. Morale Shock / Panic).")]
    [SerializeField, Range(1, 100)] private int criticalFearThreshold = 80;

    [Tooltip("Extra margin added to stat AttackRange to qualify as threat/attack range for Engaged tier.")]
    [SerializeField, Range(0.0f, 10.0f)] private float threatRangeMargin = 3.0f;

    public float CalmInterval => calmInterval;
    public float DefaultInterval => defaultInterval;
    public float EngagedInterval => engagedInterval;
    public float CriticalInterval => criticalInterval;
    public float HysteresisDwellTime => hysteresisDwellTime;
    public float ForcePulseMinGap => forcePulseMinGap;
    public float CriticalHealthThreshold => criticalHealthThreshold;
    public int CriticalFearThreshold => criticalFearThreshold;
    public float ThreatRangeMargin => threatRangeMargin;

    public float GetInterval(PerceptionTier tier)
    {
        switch (tier)
        {
            case PerceptionTier.Calm: return calmInterval;
            case PerceptionTier.Engaged: return engagedInterval;
            case PerceptionTier.Critical: return criticalInterval;
            case PerceptionTier.Default:
            default:
                return defaultInterval;
        }
    }
}
