using UnityEngine;

public class LinearProjectileMovement : MonoBehaviour, ISkillMovement
{
    private Vector3 direction;

    public void Initialize(SkillInstance skill, Vector3 dir)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.up;

        // Apply rotation based on skill's per-skill orientation settings so visuals face movement
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float zRot;
        if (skill?.Data != null)
        {
            switch (skill.Data.projectileForward)
            {
                case SkillData.ProjectileForward.Right:
                    zRot = angle + skill.Data.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.CustomAngle:
                    zRot = angle + skill.Data.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.Up:
                default:
                    zRot = angle - 90f + skill.Data.projectileRotationOffset;
                    break;
            }
        }
        else
        {
            zRot = angle - 90f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, zRot);
    }

    public void Tick(SkillInstance skill, float dt)
    {
        float speed = skill?.Stats != null ? skill.Stats.ProjectileSpeed.Value : (skill?.Data != null ? skill.Data.projectileSpeed : 0f);
        transform.position += direction * speed * dt;
    }

    public void Redirect(SkillInstance skill, GameObject target)
    {
        Vector3 newDir = target.transform.position - transform.position;
        if (newDir.sqrMagnitude < 0.0001f)
            return;

        direction = newDir.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float zRot;
        if (skill?.Data != null)
        {
            switch (skill.Data.projectileForward)
            {
                case SkillData.ProjectileForward.Right:
                    zRot = angle + skill.Data.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.CustomAngle:
                    zRot = angle + skill.Data.projectileRotationOffset;
                    break;
                case SkillData.ProjectileForward.Up:
                default:
                    zRot = angle - 90f + skill.Data.projectileRotationOffset;
                    break;
            }
        }
        else
        {
            zRot = angle - 90f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, zRot);
    }
}
