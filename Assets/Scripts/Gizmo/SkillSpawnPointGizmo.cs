using UnityEngine;

public class SkillSpawnPointGizmo : MonoBehaviour
{
    [SerializeField] private Transform skillSpawnPoint;
    [SerializeField] private float spawnRadius = 0.15f;

    private void OnDrawGizmosSelected()
    {
        if (skillSpawnPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(skillSpawnPoint.transform.position, spawnRadius);
    }
}