using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using UnityEngine;

[CreateAssetMenu(menuName = "SBGames/Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Basic Info")]
    public string skillID;
    public string skillName;
    [TextArea] public string description;
    public Sprite icon;
    public GameObject skillPrefab;
    public UtilitySkill utilityData;

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

    [Header("Stats")]
    public SkillType type = SkillType.Default;
    public float baseDamage = 10f;
    public DamageType damageType = DamageType.Physical;

    [EnableIf("type", SkillType.Projectile), FoldoutGroup("Projectile Settings")]
    public float projectileSpeed = 10f;
    [EnableIf("type", SkillType.Projectile), FoldoutGroup("Projectile Settings")]
    public float projectileLifetime = 5f;

    public bool chain = false;

    [EnableIf("chain"), FoldoutGroup("Chain Settings")]
    public int maxChainTargets = 0;
    [EnableIf("chain"), FoldoutGroup("Chain Settings")]
    public float chainDamageMultiplier = 0.7f;

    public bool area = false;
    [EnableIf("area"), FoldoutGroup("Area Settings")]
    public float areaRadius = 0f;

    public float beamTickInterval = 0.1f;

    [Header("On-Hit Status Effects")]
    public List<StatusEffectApplication> statusEffects = new();
}
