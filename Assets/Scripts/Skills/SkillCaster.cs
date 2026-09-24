using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    [Header("Skill Configuration")]
    [SerializeField]
    private SkillData data;
    [SerializeField]
    private Transform skillSpawnPoint;

    public SkillData Data => data;

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
    [SerializeField] private float cooldownDuration = 1f;
    private float cooldownTimer;

    private string casterID;
    private Transform casterTransform;


    public bool IsReady() => cooldownTimer <= 0f;

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
        if (casterTransform == null)
        {
            casterTransform = transform.root;
        }
        if (casterTransform != null && casterTransform.TryGetComponent(out CharacterIdentifier identifier))
        {
            casterID = identifier.ID;
        }

        if (spawnType == SpawnTypes.AtCasterSpawnPoint && skillSpawnPoint == null)
        {
            skillSpawnPoint = transform.parent.Find("SpawnPoint");
            if (skillSpawnPoint == null)
            {
                Debug.LogError("SkillCaster missing spawn point for AtCaster spawn type", this);
            }
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

        if (data == null || data.skillPrefab == null)
        {
            Debug.LogError("SkillCaster missing SkillData or prefab", this);
            return;
        }

        if (casterTransform == null)
        {
            Debug.LogError("SkillCaster missing caster transform", this);
            return;
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

        Debug.Log($"[SkillCaster] Debug casting skill '{data.skillName}' from '{casterTransform.name}' at position {spawnPosition} with direction {direction}.", this);
        CastSkill(direction, spawnPosition, target);
        cooldownTimer = cooldownDuration;
    }

    public bool TryCastSkill()
    {
        if (!IsReady())
            return false;

        if (data == null || data.skillPrefab == null)
        {
            Debug.LogError("SkillCaster missing SkillData or prefab", this);
            return false;
        }

        Vector3 direction;
        Vector3 spawnPosition;


        GameObject target = null;
        if (requireTarget)
        {
            if (targeting == null)
                TryGetSensor();
        
            if (targeting == null)
            {
                Debug.LogError("SkillCaster missing SkillTargeting for target-required skill", this);
                return false;
            }

            target = FindFirstTarget();
            if (target == null)
                return false;

            // ✅ HAS TARGET → CAST TOWARDS TARGET
            Vector3 toTarget = target.transform.position - GetSpawnPosition(target);
            direction = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized
                : casterTransform.up;

            spawnPosition = GetSpawnPosition(target);
        }
        else
        {
            // ✅ NO TARGET → CAST IN FRONT
            direction = casterTransform.up;
            spawnPosition = GetSpawnPosition() + direction * forwardSpawnOffset;
        }

        CastSkill(direction, spawnPosition, target);
        cooldownTimer = cooldownDuration;
        return true;
    }

    // =========================
    // CAST IMPLEMENTATION
    // =========================
    private void CastSkill(Vector3 direction, Vector3 spawnPosition, GameObject target = null)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = casterTransform != null ? casterTransform.up : Vector3.up;

        // Compute 2D rotation (Z) so projectile faces travel direction according to skill settings
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float zRot;
        if (data != null)
        {
            zRot = data.projectileForward switch
            {
                SkillData.ProjectileForward.Right => angle + data.projectileRotationOffset,
                SkillData.ProjectileForward.CustomAngle => angle + data.projectileRotationOffset,
                _ => angle - 90f + data.projectileRotationOffset,
            };
        }
        else
        {
            zRot = angle - 90f;
        }

        Quaternion rotation = Quaternion.Euler(0f, 0f, zRot);

        GameObject skillObject =
            ObjectPoolManager.Instance.Spawn(
                data.skillPrefab,
                spawnPosition,
                rotation
            );

        if (skillObject.TryGetComponent(out SkillInstance skillInstance))
        {
            skillInstance.Initialize(data, direction, casterID, target);
        }
        else
        {
            Debug.LogError(
                $"Skill prefab {data.skillPrefab.name} missing SkillInstance",
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
            SpawnTypes.AtCaster => casterTransform.position,
            SpawnTypes.AtCasterSpawnPoint => skillSpawnPoint != null ? skillSpawnPoint.position : casterTransform.position,
            SpawnTypes.AtTarget => target != null ? target.transform.position : casterTransform.position,
            _ => casterTransform.position,
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
    }

    private void TryGetSensor()
    {
        targeting = transform.Find("Sensor").GetComponent<SkillTargeting>();
    }
}
