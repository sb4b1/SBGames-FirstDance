using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

[RequireComponent(typeof(SkillStatsManager))]
public class SkillCaster : MonoBehaviour
{
    [Header("Stats & Configuration")]
    [SerializeField] private SkillStatsManager stats;
    [SerializeField, HideInInspector] private SkillData data; // Retained for serialized prefab migration
    [SerializeField] private Transform skillSpawnPoint;

    public SkillStatsManager Stats => stats;
    public SkillData Data => stats != null && stats.SkillData != null ? stats.SkillData : data;

    [Header("Targeting")]
    [SerializeField]
    private SkillTargeting targeting;
    [SerializeField]
    private bool requireTarget = true;
    [SerializeField]
    private SpawnTypes spawnType;

    [Header("Cast Settings")]
    [SerializeField] private float forwardSpawnOffset = 0.6f;

    [Header("Cooldown")]
    private float cooldownTimer;

    public float CooldownDuration => stats != null ? stats.Cooldown.Value : cooldownDuration;
    public float CooldownTimer => cooldownTimer;
    [SerializeField, HideInInspector] private float cooldownDuration = 1f;

    private string casterID;
    private Transform casterTransform;
    private CharacterStatsManager casterStats;

    public bool IsReady() => cooldownTimer <= 0f;
    public event Action<SkillData, GameObject> OnSkillCast;

    #region FLAGS
    public bool SkillTargetComponentExists() => targeting != null;
    public bool CasterTransformExists() => casterTransform != null;

    public byte Flags
    {
        get
        {
            byte flags = 0;
            if (SkillTargetComponentExists()) flags |= 1 << 0; // Bit 1: SkillTargetComponentExists
            if (CasterTransformExists()) flags |= 1 << 1; // Bit 2: CasterTransformExists
            return flags;
        }

        // Flag 
        // 0 : No flags set
        // 1 : Only SkillTargetComponentExists
        // 2 : Only CasterTransformExists
        // 3 : Both flags set
    }

    public void CheckFlags()
    {
        if (Flags == 0)
        {
            Debug.LogWarning("SkillCaster: No flags are set. Check if the skill is ready, if the SkillTargeting component exists, and if the caster transform is assigned.", this);
        }
    }

    public void SetupSkillCaster(SkillTargeting skillTargeting, Transform spawnPoint, Transform caster)
    {
        targeting = skillTargeting;
        skillSpawnPoint = spawnPoint;
        casterTransform = caster;

        if (casterTransform != null)
        {
            casterStats = casterTransform.GetComponent<CharacterStatsManager>();
            if (casterTransform.TryGetComponent(out CharacterIdentifier identifier))
            {
                casterID = identifier.UniqueId;
            }
        }
    }

    #endregion

    private enum SpawnTypes
    {
        AtCaster,
        AtCasterSpawnPoint,
        AtTarget
    }

    private void Awake()
    {
        InitializeStatsReference();

        if (casterTransform == null)
        {
            casterTransform = transform.root;
        }

        if (casterTransform != null)
        {
            casterStats = casterTransform.GetComponent<CharacterStatsManager>();
            if (casterTransform.TryGetComponent(out CharacterIdentifier identifier))
            {
                casterID = identifier.UniqueId;
            }
        }

        if (spawnType == SpawnTypes.AtCasterSpawnPoint && skillSpawnPoint == null)
        {
            skillSpawnPoint = transform.parent != null ? transform.parent.Find("SpawnPoint") : null;
            if (skillSpawnPoint == null)
            {
                Debug.LogError("SkillCaster missing spawn point for AtCaster spawn type", this);
            }
        }
    }

    private void InitializeStatsReference()
    {
        if (stats == null)
        {
            stats = GetComponent<SkillStatsManager>();
            if (stats == null)
            {
                stats = gameObject.AddComponent<SkillStatsManager>();
            }
        }

        if (stats != null && stats.SkillData == null && data != null)
        {
            stats.BaseData = data;
        }
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    // =========================
    // CAST ENTRY POINT
    // =========================
    [Button]
    public void DebugCastSkill()
    {
        if (!IsReady())
        {
            Debug.LogWarning("SkillCaster: Skill is on cooldown. Cannot cast skill.", this);
            return;
        }

        SkillData skillData = Data;
        if (skillData == null || skillData.skillPrefab == null)
        {
            Debug.LogError("SkillCaster missing SkillData or prefab", this);
            return;
        }

        if (casterTransform == null)
        {
            Debug.LogError("SkillCaster missing caster transform", this);
            return;
        }

        if (stats != null && stats.ManaCost.Value > 0f && casterStats != null)
        {
            if (!casterStats.TryConsumeMana(stats.ManaCost.Value))
            {
                Debug.LogWarning($"[SkillCaster] Not enough mana to cast '{skillData.skillName}'.", this);
                return;
            }
        }

        GameObject target = targeting != null ? FindFirstTarget() : null;
        Vector3 direction;
        Vector3 spawnPosition;

        if (target != null)
        {
            Vector3 toTarget = target.transform.position - GetSpawnPosition(target);
            direction = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized
                : casterTransform.up;
            spawnPosition = GetSpawnPosition(target);
        }
        else
        {
            direction = casterTransform.up;
            spawnPosition = GetSpawnPosition() + direction * forwardSpawnOffset;
        }

        Debug.Log($"[SkillCaster] Debug casting skill '{skillData.skillName}' from '{casterTransform.name}' at position {spawnPosition} with direction {direction}.", this);
        CastSkill(direction, spawnPosition, target);
        cooldownTimer = CooldownDuration;
    }

    public bool TryCastSkill(GameObject targetOverride = null)
    {
        if (!IsReady())
            return false;

        SkillData skillData = Data;
        if (skillData == null || skillData.skillPrefab == null)
        {
            Debug.LogError("SkillCaster missing SkillData or prefab", this);
            return false;
        }

        // Mana check
        if (stats != null && stats.ManaCost.Value > 0f && casterStats != null)
        {
            if (!casterStats.TryConsumeMana(stats.ManaCost.Value))
            {
                return false;
            }
        }

        Vector3 direction;
        Vector3 spawnPosition;

        GameObject target = targetOverride;
        if (target == null && targeting != null)
        {
            target = FindFirstTarget();
        }

        // Fallback: query BaseAI current target on caster
        if (target == null && casterTransform != null)
        {
            var brain = casterTransform.GetComponentInChildren<BaseAI>();
            if (brain != null && brain.CurrentTarget != null)
            {
                target = brain.CurrentTarget.gameObject;
            }
        }

        if (target != null)
        {
            // ✅ HAS TARGET → CAST TOWARDS TARGET
            Vector3 toTarget = target.transform.position - GetSpawnPosition(target);
            direction = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized
                : (casterTransform != null ? casterTransform.up : Vector3.up);

            spawnPosition = GetSpawnPosition(target);
        }
        else if (requireTarget && targeting != null)
        {
            // Target is required and a targeting sensor exists, but no target is in range
            return false;
        }
        else
        {
            // ✅ NO TARGET → CAST IN FACING DIRECTION
            direction = casterTransform != null ? casterTransform.up : Vector3.up;
            spawnPosition = GetSpawnPosition() + direction * forwardSpawnOffset;
        }

        CastSkill(direction, spawnPosition, target);
        cooldownTimer = CooldownDuration;
        return true;
    }

    // =========================
    // CAST IMPLEMENTATION
    // =========================
    private void CastSkill(Vector3 direction, Vector3 spawnPosition, GameObject target = null)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = casterTransform != null ? casterTransform.up : Vector3.up;

        SkillData skillData = Data;

        // Compute 2D rotation (Z) so projectile faces travel direction according to skill settings
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float zRot;
        if (skillData != null)
        {
            zRot = skillData.projectileForward switch
            {
                SkillData.ProjectileForward.Right => angle + skillData.projectileRotationOffset,
                SkillData.ProjectileForward.CustomAngle => angle + skillData.projectileRotationOffset,
                _ => angle - 90f + skillData.projectileRotationOffset,
            };
        }
        else
        {
            zRot = angle - 90f;
        }

        Quaternion rotation = Quaternion.Euler(0f, 0f, zRot);

        GameObject skillObject =
            ObjectPoolManager.Instance.Spawn(
                skillData.skillPrefab,
                spawnPosition,
                rotation
            );

        GameObject casterGo = casterTransform != null ? casterTransform.gameObject : (transform.root != null ? transform.root.gameObject : gameObject);
        string resolvedCasterId = casterID;
        if (casterTransform != null && casterTransform.TryGetComponent(out CharacterIdentifier identifier) && !string.IsNullOrEmpty(identifier.UniqueId))
        {
            resolvedCasterId = identifier.UniqueId;
        }

        if (skillObject.TryGetComponent(out SkillInstance skillInstance))
        {
            if (stats != null)
            {
                skillInstance.Initialize(stats, direction, resolvedCasterId, target, casterGo);
            }
            else
            {
                skillInstance.Initialize(skillData, direction, resolvedCasterId, target, casterGo);
            }
            OnSkillCast?.Invoke(skillData, target);
        }
        else
        {
            Debug.LogError(
                $"Skill prefab {skillData.skillPrefab.name} missing SkillInstance",
                this
            );
        }
    }

    // =========================
    // HELPERS
    // =========================
    private Vector3 GetSpawnPosition(GameObject target = null)
    {
        return spawnType switch
        {
            SpawnTypes.AtCaster => casterTransform != null ? casterTransform.position : transform.position,
            SpawnTypes.AtCasterSpawnPoint => skillSpawnPoint != null ? skillSpawnPoint.position : (casterTransform != null ? casterTransform.position : transform.position),
            SpawnTypes.AtTarget => target != null ? target.transform.position : (casterTransform != null ? casterTransform.position : transform.position),
            _ => casterTransform != null ? casterTransform.position : transform.position,
        };
    }

    private GameObject FindFirstTarget()
    {
        if (targeting == null)
            return null;

        var targets = targeting.GetTargets();
        return targets.Count > 0 ? targets[0] : null;
    }

    private void OnValidate()
    {
        if (targeting == null)
            targeting = GetComponent<SkillTargeting>();

        if (stats == null)
            stats = GetComponent<SkillStatsManager>();

        if (stats != null && stats.SkillData == null && data != null)
            stats.BaseData = data;
    }

    private void TryGetSensor()
    {
        targeting = transform.Find("Sensor")?.GetComponent<SkillTargeting>();
    }
}
