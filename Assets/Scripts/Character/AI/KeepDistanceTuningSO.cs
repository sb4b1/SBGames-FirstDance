using UnityEngine;

[CreateAssetMenu(fileName = "KeepDistanceTuning_Default", menuName = "SBGames/AI/Keep Distance Tuning SO")]
public class KeepDistanceTuningSO : ScriptableObject
{
    [Tooltip("The basic attack range (or reference skill range) threshold above which Auto mode resolves to enabled.")]
    [SerializeField, Range(1.0f, 10.0f)] private float autoEnableRangeThreshold = 3.0f;

    [Tooltip("Fraction of reference range at which kiting/retreat starts (comfort distance inner boundary).")]
    [SerializeField, Range(0.1f, 0.9f)] private float startFraction = 0.6f;

    [Tooltip("Fraction of reference range at which kiting/retreat stops (comfort distance outer boundary, giving hysteresis).")]
    [SerializeField, Range(0.5f, 1.2f)] private float stopFraction = 0.9f;

    public float AutoEnableRangeThreshold => autoEnableRangeThreshold;
    public float StartFraction => startFraction;
    public float StopFraction => stopFraction;

    public static KeepDistanceTuningSO CreateRuntimeInstance(float autoThreshold = 3.0f, float start = 0.6f, float stop = 0.9f)
    {
        var inst = CreateInstance<KeepDistanceTuningSO>();
        inst.autoEnableRangeThreshold = autoThreshold;
        inst.startFraction = start;
        inst.stopFraction = stop;
        return inst;
    }
}
