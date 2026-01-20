using SB4b1.GameMath;

public static class CombatResolver
{
    public static CombatResult Resolve(
        Stat attack,
        Stat defense,
        float critChance,
        float critMultiplier,
        ref GameRng rng)
    {
        bool crit = rng.Chance(critChance);

        float baseDamage = CombatMath.CalculateDamage(
            attack.Value,
            defense.Value
        );

        float finalDamage = crit
            ? CombatMath.CriticalDamage(baseDamage, critMultiplier)
            : baseDamage;

        return new CombatResult(finalDamage, crit);
    }
}

public readonly struct CombatResult
{
    public readonly float Damage;
    public readonly bool IsCrit;

    public CombatResult(float damage, bool isCrit)
    {
        Damage = damage;
        IsCrit = isCrit;
    }
}
