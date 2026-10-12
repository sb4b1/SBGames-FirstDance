using System;
using UnityEngine;

[Serializable]
public struct AccuracyTuningEntry
{
    public LeaderAssessmentAccuracy Accuracy;
    [Range(-0.5f, 0.5f)] public float OptimismBias;
    [Range(0f, 0.5f)] public float NoiseAmplitude;

    public AccuracyTuningEntry(LeaderAssessmentAccuracy accuracy, float optimismBias, float noiseAmplitude)
    {
        Accuracy = accuracy;
        OptimismBias = optimismBias;
        NoiseAmplitude = noiseAmplitude;
    }
}

/// <summary>
/// Data-driven tuning tables for party leader checkpoint evaluation and emergent succession.
/// Indexed directly by enum ordinal to avoid rank or enum switch/if-chains at runtime.
/// </summary>
[CreateAssetMenu(fileName = "LeaderCheckpointTuning", menuName = "SBGames/AI/Leader Checkpoint Tuning")]
public class LeaderCheckpointTuningSO : ScriptableObject
{
    private static readonly AccuracyTuningEntry[] DefaultAccuracyTable = new AccuracyTuningEntry[]
    {
        new AccuracyTuningEntry(LeaderAssessmentAccuracy.Optimistic, 0.25f, 0.08f),
        new AccuracyTuningEntry(LeaderAssessmentAccuracy.NearTrue,   0.00f, 0.05f),
        new AccuracyTuningEntry(LeaderAssessmentAccuracy.Accurate,   0.00f, 0.00f)
    };

    private static readonly float[] DefaultSafetyMarginTable = new float[]
    {
        0.05f, // Thin (0)
        0.20f, // Medium (1)
        0.35f  // Thick (2)
    };

    private static readonly float[] DefaultFunctionStepUpWeights = new float[]
    {
        0.55f, // None (0) -> treated like DPS
        0.85f, // Tank (1) -> highest
        0.55f, // DPS (2)  -> mid
        0.30f, // Healer (3) -> low
        0.15f  // Scout (4) -> lowest
    };

    private static readonly bool[] DefaultLeaderlessStayLooseTable = new bool[]
    {
        false, // Dissolve (0)
        true   // StayLoose (1)
    };

    [Header("Assessment Accuracy Table (Indexed by LeaderAssessmentAccuracy)")]
    [SerializeField] private AccuracyTuningEntry[] accuracyTable = CreateDefaultAccuracyArray();

    [Header("Safety Margin Table (Indexed by LeaderSafetyMargin)")]
    [Tooltip("Required normalized advantage [(advanceGain - expectedLoss) / totalStake] to choose Advance over Retreat.")]
    [SerializeField] private float[] safetyMarginTable = CreateDefaultSafetyMarginArray();

    [Header("Succession Step-Up Weights (Indexed by AdventurerFunction)")]
    [Tooltip("Base leadership willingness per function: None=0.55, Tank=0.85, DPS=0.55, Healer=0.30, Scout=0.15.")]
    [SerializeField] private float[] functionStepUpWeights = CreateDefaultFunctionWeightsArray();

    [Header("Leaderless Cohesion Table (Indexed by LeaderlessBehavior)")]
    [SerializeField] private bool[] leaderlessStayLooseTable = new bool[] { false, true };

    [Header("Condition & Recovery Weights")]
    [SerializeField, Range(0f, 1f)] private float hpConditionWeight = 0.65f;
    [SerializeField, Range(0f, 1f)] private float manaConditionWeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float survivalRatioWeight = 0.20f;
    [SerializeField, Range(0.1f, 0.95f)] private float recoveryHpThreshold = 0.65f;
    [SerializeField, Range(0.05f, 0.9f)] private float healerMinManaFraction = 0.25f;

    [Header("Succession Scoring Weights")]
    [SerializeField, Range(0f, 1f)] private float functionWeightContribution = 0.60f;
    [SerializeField, Range(0f, 1f)] private float hpFractionContribution = 0.40f;
    [SerializeField, Range(0f, 0.5f)] private float enemyHesitationPerTarget = 0.10f;

    public float HpConditionWeight => hpConditionWeight;
    public float ManaConditionWeight => manaConditionWeight;
    public float SurvivalRatioWeight => survivalRatioWeight;
    public float RecoveryHpThreshold => recoveryHpThreshold;
    public float HealerMinManaFraction => healerMinManaFraction;
    public float FunctionWeightContribution => functionWeightContribution;
    public float HpFractionContribution => hpFractionContribution;
    public float EnemyHesitationPerTarget => enemyHesitationPerTarget;

    public static AccuracyTuningEntry[] CreateDefaultAccuracyArray()
    {
        var copy = new AccuracyTuningEntry[DefaultAccuracyTable.Length];
        Array.Copy(DefaultAccuracyTable, copy, DefaultAccuracyTable.Length);
        return copy;
    }

    public static float[] CreateDefaultSafetyMarginArray()
    {
        var copy = new float[DefaultSafetyMarginTable.Length];
        Array.Copy(DefaultSafetyMarginTable, copy, DefaultSafetyMarginTable.Length);
        return copy;
    }

    public static float[] CreateDefaultFunctionWeightsArray()
    {
        var copy = new float[DefaultFunctionStepUpWeights.Length];
        Array.Copy(DefaultFunctionStepUpWeights, copy, DefaultFunctionStepUpWeights.Length);
        return copy;
    }

    public AccuracyTuningEntry GetAccuracyTuning(LeaderAssessmentAccuracy accuracy)
    {
        int index = Mathf.Clamp((int)accuracy, 0, DefaultAccuracyTable.Length - 1);
        if (accuracyTable != null && index < accuracyTable.Length)
        {
            return accuracyTable[index];
        }
        return DefaultAccuracyTable[index];
    }

    public static AccuracyTuningEntry GetAccuracyTuningOrDefault(LeaderCheckpointTuningSO tuning, LeaderAssessmentAccuracy accuracy)
    {
        if (tuning != null)
        {
            return tuning.GetAccuracyTuning(accuracy);
        }
        int index = Mathf.Clamp((int)accuracy, 0, DefaultAccuracyTable.Length - 1);
        return DefaultAccuracyTable[index];
    }

    public float GetRequiredSafetyMargin(LeaderSafetyMargin margin)
    {
        int index = Mathf.Clamp((int)margin, 0, DefaultSafetyMarginTable.Length - 1);
        if (safetyMarginTable != null && index < safetyMarginTable.Length)
        {
            return safetyMarginTable[index];
        }
        return DefaultSafetyMarginTable[index];
    }

    public static float GetRequiredSafetyMarginOrDefault(LeaderCheckpointTuningSO tuning, LeaderSafetyMargin margin)
    {
        if (tuning != null)
        {
            return tuning.GetRequiredSafetyMargin(margin);
        }
        int index = Mathf.Clamp((int)margin, 0, DefaultSafetyMarginTable.Length - 1);
        return DefaultSafetyMarginTable[index];
    }

    public float GetFunctionStepUpWeight(AdventurerFunction function)
    {
        int index = Mathf.Clamp((int)function, 0, DefaultFunctionStepUpWeights.Length - 1);
        if (functionStepUpWeights != null && index < functionStepUpWeights.Length)
        {
            return functionStepUpWeights[index];
        }
        return DefaultFunctionStepUpWeights[index];
    }

    public static float GetFunctionStepUpWeightOrDefault(LeaderCheckpointTuningSO tuning, AdventurerFunction function)
    {
        if (tuning != null)
        {
            return tuning.GetFunctionStepUpWeight(function);
        }
        int index = Mathf.Clamp((int)function, 0, DefaultFunctionStepUpWeights.Length - 1);
        return DefaultFunctionStepUpWeights[index];
    }

    public bool ShouldStayLooseWhenLeaderless(LeaderlessBehavior behavior)
    {
        int index = Mathf.Clamp((int)behavior, 0, DefaultLeaderlessStayLooseTable.Length - 1);
        if (leaderlessStayLooseTable != null && index < leaderlessStayLooseTable.Length)
        {
            return leaderlessStayLooseTable[index];
        }
        return DefaultLeaderlessStayLooseTable[index];
    }

    public static bool ShouldStayLooseWhenLeaderlessOrDefault(LeaderCheckpointTuningSO tuning, LeaderlessBehavior behavior)
    {
        if (tuning != null)
        {
            return tuning.ShouldStayLooseWhenLeaderless(behavior);
        }
        int index = Mathf.Clamp((int)behavior, 0, DefaultLeaderlessStayLooseTable.Length - 1);
        return DefaultLeaderlessStayLooseTable[index];
    }
}
