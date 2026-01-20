using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public string skillID;
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Damage")]
    public float baseDamage = 10f;
    public DamageType damageType = DamageType.Physical;

    [Header("Delivery")]
    public GameObject skillPrefab;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 5f;

    public enum ProjectileForward
    {
        Up,
        Right,
        CustomAngle
    }

    [Tooltip("Which local axis of the projectile prefab should point in the travel direction.")]
    public ProjectileForward projectileForward = ProjectileForward.Up;

    [Tooltip("Additional rotation (degrees) applied around Z after aligning to travel direction.")]
    public float projectileRotationOffset = 0f;

    [Header("Chaining")]
    public int maxChainTargets = 0;
    public float chainDamageMultiplier = 0.7f;

    [Header("Area of Effect")]
    public float aoeRadius = 0f;

    [Header("Beam")]
    public float beamTickInterval = 0.1f;

    [Header("On-Hit Status Effects")]
    public List<StatusEffectApplication> statusEffects = new();
}
