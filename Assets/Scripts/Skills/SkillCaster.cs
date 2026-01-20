using UnityEngine;

public class SkillCaster : MonoBehaviour
{
    [Header("Skill Configuration")]
    [SerializeField] private SkillData skillData;
    [SerializeField] private Transform skillSpawnPoint;

    [Header("Targeting")]
    [SerializeField] private SkillTargeting targeting;
    [SerializeField] private bool requireTarget = true;

    [Header("Cast Settings")]
    [SerializeField] private float forwardSpawnOffset = 0.6f;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDuration = 1f;
    private float cooldownTimer;

    private Transform casterTransform;

    private void Awake()
    {
        casterTransform = transform.root;

        if (skillSpawnPoint == null)
            skillSpawnPoint = casterTransform;
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

        if (requireTarget)
        {
            if (targeting == null)
                return false;

            GameObject target = FindFirstTarget();
            if (target == null)
                return false;

            Vector3 toTarget = target.transform.position - GetSpawnPosition();
            direction = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized
                : casterTransform.up;

            spawnPosition = GetSpawnPosition();
        }
        else
        {
            // ✅ NO TARGET → CAST IN FRONT
            direction = casterTransform.up;
            spawnPosition = GetSpawnPosition() + direction * forwardSpawnOffset;
        }

        CastSkill(direction, spawnPosition);
        cooldownTimer = cooldownDuration;
        return true;
    }

    // =========================
    // CAST IMPLEMENTATION
    // =========================
    private void CastSkill(Vector3 direction, Vector3 spawnPosition)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = casterTransform != null ? casterTransform.up : Vector3.up;

        // Compute 2D rotation (Z) so projectile faces travel direction according to skill settings
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float zRot;
        if (skillData != null)
        {
            switch (skillData.projectileForward)
            {
                case SkillData.ProjectileForward.Right:
                    zRot = angle + skillData.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.CustomAngle:
                    zRot = angle + skillData.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.Up:
                default:
                    zRot = angle - 90f + skillData.projectileRotationOffset;
                    break;
            }
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
            skillInstance.Initialize(skillData, direction);
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
    private Vector3 GetSpawnPosition()
    {
        return skillSpawnPoint != null
            ? skillSpawnPoint.position
            : casterTransform.position;
    }

    private GameObject FindFirstTarget()
    {
        if (targeting == null)
            return null;

        var targets = targeting.GetAOETargets();
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
}
