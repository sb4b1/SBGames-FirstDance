using UnityEngine;
using SB4b1.Logging;
using System.Collections;
using ArtificeToolkit.Attributes;

public class CharacterStatsManager : MonoBehaviour, IDamageable
{
    [Header("Base Stats")]
    [SerializeField] private CharacterBaseStats baseStats;

    // Runtime life
    [SerializeField] private float life;

    public float Life { get => life; private set => life = value; }

    // Core stats (private set = safer)

#if UNITY_EDITOR
    [SerializeField, ReadOnly] private float maxLife;
    [SerializeField, ReadOnly] private float movespeed;
    [SerializeField, ReadOnly] private float damage;
    [SerializeField, ReadOnly] private float attackRange;
    [SerializeField, ReadOnly] private float attackTimeInterval;
#endif

    public Stat MaxLife { get; private set; } = new Stat();
    public Stat Movespeed { get; private set; } = new Stat();
    public Stat Damage { get; private set; } = new Stat();
    public Stat AttackRange { get; private set; } = new Stat();
    public Stat AttackTimeInterval { get; private set; } = new Stat();

    public bool AttackReady { get; private set; } = true;

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
        AttackTimeInterval.BaseValue = baseStats.AttackTimeInterval;

#if UNITY_EDITOR
        maxLife = MaxLife.Value;
        movespeed = Movespeed.Value;
        damage = Damage.Value;
        attackRange = AttackRange.Value;
        attackTimeInterval = AttackTimeInterval.Value;
#endif
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

    // =========================

    public void StartAttackTimer()
    {
        if (AttackReady)
        {
            StartCoroutine(AttackCooldownCoroutine(AttackTimeInterval.Value));
        }
    }

    private IEnumerator AttackCooldownCoroutine(float interval)
    {
        AttackReady = false;
        yield return new WaitForSeconds(interval);
        AttackReady = true;
    }
}
