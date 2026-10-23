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

    public enum SelfTarget
    {
        Allowed = 0,
        Excluded = 1,
        Only = 2
    }

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

    public enum SkillTargetFilter
    {
        EnemiesOnly = 0,
        AllExceptCaster = 1,
        Everyone = 2,
        AlliesOnly = 3,
        SelfOnly = 4
    }

    [Header("Stats")]
    public SkillType type = SkillType.Default;
    [Tooltip("Defines which entities this skill can hit (EnemiesOnly, AllExceptCaster, Everyone, AlliesOnly, SelfOnly).")]
    public SkillTargetFilter targetFilter = SkillTargetFilter.EnemiesOnly;
    [Tooltip("Effective casting range. If 0, automatically derived from projectile speed/lifetime or defaults to 5.")]
    public float castRange = 0f;
    public float baseDamage = 10f;
    public float baseHeal = 0f;
    public SelfTarget selfTarget = SelfTarget.Allowed;
    public VisualAnchor visualAnchor = VisualAnchor.Feet;
    public float windupDuration = 0.5f;
    public DamageType damageType = DamageType.Physical;
    public float baseManaCost = 10f;

    [Header("Recovery Settings")]
    [Tooltip("Whether to use custom recovery duration for this skill or fallback to character base stat default.")]
    public bool useCustomRecoveryDuration = true;

    [Tooltip("Post-cast recovery / backswing duration (seconds) before this character can attack or act again.")]
    [Min(0f)] public float postCastRecoveryDuration = 0.35f;


    /// <summary>
    /// Gets the effective range of this skill for AI decision making and GOAP distance checks.
    /// </summary>
    public float EffectiveRange
    {
        get
        {
            if (castRange > 0f) return castRange;
            if (type == SkillType.Projectile && projectileSpeed > 0f && projectileLifetime > 0f)
                return projectileSpeed * projectileLifetime;
            if (area && areaRadius > 0f)
                return areaRadius;
            return 5f;
        }
    }

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
