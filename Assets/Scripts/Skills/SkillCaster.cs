using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    [Header("Skill Configuration")]
    [SerializeField]
    private SkillData skillData;
    [SerializeField]
    private Transform skillSpawnPoint;

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

    private Transform casterTransform;


    private enum SpawnTypes
    {
        AtCaster,
        AtTarget
    }

    private void Awake()
    {
        casterTransform = transform.root;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    // =========================
    // CAST ENTRY POINT
    // =========================
    public bool TryCastSkill()
    {
        if (!IsReady())
            return false;

        if (skillData == null || skillData.skillPrefab == null)
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

        if (skillObject.TryGetComponent(out SkillInstance skillInstance))
        {
            skillInstance.Initialize(skillData, direction, target);
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
        if (spawnType == SpawnTypes.AtCaster || targeting == null)
        {
            return skillSpawnPoint != null
                ? skillSpawnPoint.position
                : casterTransform.position;
        }
        else // AtTarget
        {
            return target.transform.position;
        }
    }

    private GameObject FindFirstTarget()
    {
        if (targeting == null)
            return null;

        var targets = targeting.GetTargets();
        return targets.Count > 0 ? targets[0] : null;
    }

    public bool IsReady() => cooldownTimer <= 0f;

    private void OnValidate()
    {
        if (targeting == null)
            targeting = GetComponent<SkillTargeting>();
    }

    private void OnDrawGizmosSelected()
    {
        if (skillSpawnPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(skillSpawnPoint.position, 0.15f);
        Gizmos.DrawLine(
            skillSpawnPoint.position,
            skillSpawnPoint.position + transform.up * forwardSpawnOffset
        );
    }

    private void TryGetSensor()
    {
        targeting = transform.root.Find("Sensor").GetComponent<SkillTargeting>();
    }
}
