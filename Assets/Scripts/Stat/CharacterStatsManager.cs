using UnityEngine;
using SB4b1.Logging;

public class CharacterStatsManager : MonoBehaviour, IDamagable
{
    [Header("Base Stats")]
    [SerializeField] private CharacterBaseStats baseStats;

    // Runtime life
    public float Life { get; private set; }

    // Core stats (private set = safer)
    public Stat MaxLife { get; private set; } = new();
    public Stat Movespeed { get; private set; } = new();
    public Stat Damage { get; private set; } = new();
    public Stat AttackRange { get; private set; } = new();

    public event System.Action<float, float> OnLifeChanged;

    private void Awake()
    {
        InitializeStats();
    }

    // =========================
    // Initialization
    // =========================
    private void InitializeStats()
    {
        if (baseStats == null)
        {
            ELogger.Log(this, nameof(InitializeStats), "Missing CharacterBaseStats");
            return;
        }

        ApplyBaseStats();
        Life = MaxLife.Value;
        NotifyLifeChanged();
    }

    private void ApplyBaseStats()
    {
        MaxLife.BaseValue = baseStats.MaxLife;
        Movespeed.BaseValue = baseStats.Movespeed;
        Damage.BaseValue = baseStats.Damage;
        AttackRange.BaseValue = baseStats.AttackRange;
    }

    // =========================
    // Life Handling
    // =========================
    public void RestoreLife(float value)
    {
        if (value <= 0) return;

        Life = Mathf.Min(Life + value, MaxLife.Value);
        NotifyLifeChanged();

        ELogger.Log(this, nameof(RestoreLife),
            $"{name} healed <color=green>{value}</color>");
    }

    public bool TakeDamage(in DamageData damage)
    {
        float finalDamage = CalculateFinalDamage(damage);

        Life = Mathf.Max(Life - finalDamage, 0);
        NotifyLifeChanged();

        ELogger.Log(this, nameof(TakeDamage),
            $"{name} took <color=red>{finalDamage}</color> {damage.Type} damage");

        if (Life <= 0)
        {
            OnDeath();
            return true;
        }

        return false;
    }

    // Legacy support
    public void TakeDamage(float damage)
    {
        TakeDamage(new DamageData(damage, DamageType.Physical));
    }

    private float CalculateFinalDamage(in DamageData damage)
    {
        float amount = damage.Amount;

        switch (damage.Type)
        {
            case DamageType.Physical:
                // later: armor stat
                break;

            //case DamageType.Poison:
                // later: resist stat
                //break;

            case DamageType.True:
                return amount;
        }

        return Mathf.Max(1f, amount);
    }


    // =========================
    // Generic Handling
    // =========================

    private void NotifyLifeChanged()
    {
        OnLifeChanged?.Invoke(Life, MaxLife.Value);
    }

    private void OnDeath()
    {
        ELogger.Log(this, nameof(OnDeath), $"{name} died");
        // Hook death logic here
    }
}
