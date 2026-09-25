using UnityEngine;

public class DamageReceiver : MonoBehaviour
{
    protected IDamageable damageable;

    protected virtual void Awake()
    {
        damageable = GetComponentInParent<IDamageable>();
    }

    public IDamageable Damageable => damageable;
}