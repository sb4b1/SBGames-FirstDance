using UnityEngine;

public class AttackPointGizmo : MonoBehaviour
{
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.15f;

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        DevTool.Gizmo.DrawWireSphere(attackPoint.position, attackRadius, Color.red);
    }
}