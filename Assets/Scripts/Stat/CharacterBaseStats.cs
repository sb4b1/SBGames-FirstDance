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
}
