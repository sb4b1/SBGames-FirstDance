using UnityEngine;
using SB4b1.Logging;
using System.Collections;
using ArtificeToolkit.Attributes;

[RequireComponent(typeof(CharacterBrain))]
[DisallowMultipleComponent]
public class CharacterStatsManager : MonoBehaviour, IDamageable
{
    private CharacterBrain brain;


    [Header("Character Progression")]
    [SerializeField, Min(1)] private int level = 1;

    [Header("Base Stats")]
    [SerializeField] private CharacterBaseStats baseStats;

    // Runtime life
    [SerializeField] private float life;
    [SerializeField] private float mana;

    public int Level => level;
    public float Life { get => life; private set => life = value; }
    public float Mana { get => mana; private set => mana = value; }

    public event System.Action<int> OnLevelChanged;

    // Core stats (private set = safer)

#if UNITY_EDITOR
    [SerializeField, ReadOnly] private float maxLife;
    [SerializeField, ReadOnly] private float maxMana;
    [SerializeField, ReadOnly] private float movespeed;
    [SerializeField, ReadOnly] private float damage;
    [SerializeField, ReadOnly] private float attackRange;
    [SerializeField, ReadOnly] private float attackTimeInterval;
#endif

    public Stat MaxLife { get; private set; } = new Stat();
    public Stat MaxMana { get; private set; } = new Stat();
    public Stat Movespeed { get; private set; } = new Stat();
    public Stat Damage { get; private set; } = new Stat();
    public Stat AttackRange { get; private set; } = new Stat();
    public Stat AttackTimeInterval { get; private set; } = new Stat();

    public bool AttackReady { get; private set; } = true;

    public event System.Action<float, float> OnLifeChanged;
    public event System.Action<CharacterStatsManager> OnCharacterDeath;

    private void Awake()
    {
        brain = GetComponent<CharacterBrain>();

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
        Mana = MaxMana.Value;
        NotifyLifeChanged();
    }

    private void ApplyBaseStats()
    {
        MaxLife.BaseValue = baseStats.MaxLife;
        MaxMana.BaseValue = baseStats.MaxMana;
        Movespeed.BaseValue = baseStats.Movespeed;
        Damage.BaseValue = baseStats.Damage;
        AttackRange.BaseValue = baseStats.AttackRange;
        AttackTimeInterval.BaseValue = baseStats.AttackTimeInterval;

#if UNITY_EDITOR
        maxLife = MaxLife.Value;
        maxMana = MaxMana.Value;
        movespeed = Movespeed.Value;
        damage = Damage.Value;
        attackRange = AttackRange.Value;
        attackTimeInterval = AttackTimeInterval.Value;
#endif
    }

    // =========================
    // Level Handling
    // =========================
    public void SetLevel(int newLevel)
    {
        int clampedLevel = Mathf.Max(1, newLevel);
        if (level != clampedLevel)
        {
            level = clampedLevel;
            OnLevelChanged?.Invoke(level);
            ELogger.Log(this, nameof(SetLevel), $"{name} level set to {level}");
        }
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

    public void RestoreMana(float value)
    {
        if (value <= 0) return;

        Mana = Mathf.Min(Mana + value, MaxMana.Value);

        ELogger.Log(this, nameof(RestoreMana),
            $"{name} restored <color=blue>{value}</color> mana");
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

    public bool TryConsumeMana(float amount)
    {
        if (amount <= 0) return true;

        if (Mana >= amount)
        {
            Mana -= amount;
            ELogger.Log(this, nameof(TryConsumeMana),
                $"{name} consumed <color=blue>{amount}</color> mana");
            return true;
        }
        else
        {
            ELogger.Log(this, nameof(TryConsumeMana),
                $"{name} failed to consume <color=blue>{amount}</color> mana (not enough)");
            return false;
        }
    }

    // Legacy support
    public void TakeDamage(float damage)
    {
        TakeDamage(new DamageData(damage, DamageType.Physical));
    }

    public void ForceDeath()
    {
        Life = 0;
        NotifyLifeChanged();
        OnDeath();
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
        OnCharacterDeath?.Invoke(this);

        if (brain == null)
        {
            brain = GetComponent<CharacterBrain>();
        }
        brain?.OnDespawn();

        ELogger.Log(this, nameof(OnDeath), $"{name} died");
        Destroy(gameObject); // Temporary death handling - destroy the GameObject. Later: add death animation, ragdoll, etc.
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
}
