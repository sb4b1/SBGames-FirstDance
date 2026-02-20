public interface IDamageable
{
    /// <summary>
    /// Apply structured damage. Returns true if this hit caused death.
    /// </summary>
    bool TakeDamage(in DamageData damage);

    /// <summary>
    /// Backward compatibility (legacy calls).
    /// </summary>
    void TakeDamage(float damage);
}
