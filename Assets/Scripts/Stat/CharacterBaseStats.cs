using UnityEngine;

public enum CharacterRank
{
    E,
    D,
    C,
    B,
    A,
    S
}

[CreateAssetMenu(fileName = "CharacterBaseStats", menuName = "SBGames/Stats/Character")]
public class CharacterBaseStatSO : ScriptableObject
{
    [Min(1)] public int characterLevel = 1;
    public CharacterRank characterRank = CharacterRank.D;
    [Min(1)] public float MaxLife = 100f;
    [Min(0)] public float MaxMana = 50f;
    [Min(0)] public float Movespeed = 5f;
    [Min(0)] public float Damage = 10f;
    [Min(0.1f)] public float AttackRange = 1f;
    [Min(0.1f)] public float AttackTimeInterval = 1f;

    [Header("AI Decision & Reaction Parameters")]
    [Tooltip("Base reaction time / evaluation interval in seconds. Lower values mean faster decision making.")]
    [Min(0.05f)] public float DecisionInterval = 0.3f;

    [Tooltip("Random interval variance (+/-) to stagger evaluations across frames and prevent CPU spikes.")]
    [Range(0f, 0.5f)] public float IntervalJitter = 0.05f;
}
