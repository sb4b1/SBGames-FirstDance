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
    public CharacterClass characterClass = CharacterClass.None;
    public AdventurerFunction characterFunction = AdventurerFunction.None;

    public AdventurerFunction CharacterFunction => characterFunction;
    public AdventurerFunction EffectiveFunction => characterFunction != AdventurerFunction.None ? characterFunction : AdventurerFunction.DPS;

    [Min(1)] public float MaxLife = 100f;
    [Min(0)] public float MaxMana = 50f;
    [Min(0)] public float Movespeed = 5f;
    [Min(0)] public float Damage = 10f;
    [Min(0.1f)] public float AttackRange = 1f;
    [Min(0.1f)] public float AttackTimeInterval = 1f;

    [Header("Combat Recovery & Action Buffer")]
    [Tooltip("Default post-attack recovery delay in seconds before another attack or skill can be initiated.")]
    [Min(0f)] public float DefaultPostAttackRecovery = 0.3f;

    [Tooltip("Default post-cast backswing recovery delay in seconds if skill does not specify a custom duration.")]
    [Min(0f)] public float DefaultPostCastRecovery = 0.3f;

    [Header("AI Decision & Reaction Parameters")]
    [Tooltip("Base reaction time / evaluation interval in seconds. Lower values mean faster decision making.")]
    [Min(0.05f)] public float DecisionInterval = 0.3f;

    [Tooltip("Random interval variance (+/-) to stagger evaluations across frames and prevent CPU spikes.")]
    [Range(0f, 0.5f)] public float IntervalJitter = 0.05f;
}

