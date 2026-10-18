using UnityEngine;

/// <summary>
/// Immutable configuration asset defining out-of-combat life and mana regeneration parameters.
/// </summary>
[CreateAssetMenu(fileName = "RegenTuning", menuName = "SBGames/Stats/Regen Tuning")]
public class RegenTuningSO : ScriptableObject
{
    public const float DefaultOutOfCombatDelay = 5f;
    public const float DefaultOutOfCombatRegenMultiplier = 3.0f;

    [Tooltip("Seconds since last dealing or taking damage before the out-of-combat regen boost activates.")]
    [SerializeField, Min(0f)] private float outOfCombatDelay = DefaultOutOfCombatDelay;

    [Tooltip("Multiplier applied to LifeRegen and ManaRegen when out of combat.")]
    [SerializeField, Min(0f)] private float outOfCombatRegenMultiplier = DefaultOutOfCombatRegenMultiplier;

    public float OutOfCombatDelay => outOfCombatDelay;
    public float OutOfCombatRegenMultiplier => outOfCombatRegenMultiplier;

    public static RegenTuningSO CreateRuntimeInstance(
        float delay = DefaultOutOfCombatDelay,
        float multiplier = DefaultOutOfCombatRegenMultiplier)
    {
        var instance = CreateInstance<RegenTuningSO>();
        instance.outOfCombatDelay = Mathf.Max(0f, delay);
        instance.outOfCombatRegenMultiplier = Mathf.Max(0f, multiplier);
        return instance;
    }
}
