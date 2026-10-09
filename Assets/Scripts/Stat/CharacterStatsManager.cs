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
    [SerializeField] private CharacterBaseStatSO baseStats;
    public CharacterBaseStatSO BaseStats => baseStats;

    [Header("Immortality / Training")]
    [SerializeField] private bool isImmortal;
    public bool IsImmortal { get => isImmortal; set => isImmortal = value; }

    private Coroutine attackCooldownCoroutine;

    // Runtime life
    [SerializeField] private float life;
    [SerializeField] private float mana;

    public int Level => level;
    public float Life { get => life; private set => life = value; }
    public float Mana { get => mana; private set => mana = value; }

    public event System.Action<int> OnLevelChanged;
    public event System.Action<DamageData> OnDamaged;

    // Core stats (private set = safer)

#if UNITY_EDITOR
    [SerializeField, ReadOnly] private float maxLife;
    [SerializeField, ReadOnly] private float maxMana;
    [SerializeField, ReadOnly] private float movespeed;
    [SerializeField, ReadOnly] private float damage;
    [SerializeField, ReadOnly] private float attackRange;
    [SerializeField, ReadOnly] private float attackTimeInterval;
    [SerializeField, ReadOnly] private float decisionInterval;
    [SerializeField, ReadOnly] private float intervalJitter;
#endif

    public Stat MaxLife { get; private set; } = new Stat();
    public Stat MaxMana { get; private set; } = new Stat();
    public Stat Movespeed { get; private set; } = new Stat();
    public Stat Damage { get; private set; } = new Stat();
    public Stat AttackRange { get; private set; } = new Stat();
    public Stat AttackTimeInterval { get; private set; } = new Stat();
    public Stat DecisionInterval { get; private set; } = new Stat();
    public Stat IntervalJitter { get; private set; } = new Stat();

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
        DecisionInterval.BaseValue = baseStats.DecisionInterval;
        IntervalJitter.BaseValue = baseStats.IntervalJitter;

#if UNITY_EDITOR
        maxLife = MaxLife.Value;
        maxMana = MaxMana.Value;
        movespeed = Movespeed.Value;
        damage = Damage.Value;
        attackRange = AttackRange.Value;
        attackTimeInterval = AttackTimeInterval.Value;
        decisionInterval = DecisionInterval.Value;
        intervalJitter = IntervalJitter.Value;
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

        if (isImmortal)
        {
            Life = Mathf.Max(Life - finalDamage, 1f);
        }
        else
        {
            Life = Mathf.Max(Life - finalDamage, 0);
        }

        NotifyLifeChanged();
        OnDamaged?.Invoke(damage);

        ELogger.Log(this, nameof(TakeDamage),
            $"{name} took <color=red>{finalDamage}</color> {damage.Type} damage");

        if (!isImmortal && Life <= 0)
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
        CancelAttackTimer();
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

    private void OnDisable()
    {
        CancelAttackTimer();
    }

    // =========================

    public void StartAttackTimer(float customDuration = -1f)
    {
        float duration = customDuration > 0f ? customDuration : AttackTimeInterval.Value;
        CancelAttackTimer();
        attackCooldownCoroutine = StartCoroutine(AttackCooldownCoroutine(duration));
    }

    /// <summary>
    /// Enforces a minimum cooldown before the next basic attack is ready.
    /// Used by post-cast recovery / action buffer to prevent robotic combos.
    /// </summary>
    public void EnsureAttackCooldown(float minDuration)
    {
        if (minDuration <= 0f) return;
        CancelAttackTimer();
        attackCooldownCoroutine = StartCoroutine(AttackCooldownCoroutine(minDuration));
    }

    public void CancelAttackTimer()
    {
        if (attackCooldownCoroutine != null)
        {
            StopCoroutine(attackCooldownCoroutine);
            attackCooldownCoroutine = null;
        }
    }

    private IEnumerator AttackCooldownCoroutine(float interval)
    {
        AttackReady = false;
        yield return new WaitForSeconds(interval);
        AttackReady = true;
        attackCooldownCoroutine = null;
    }

    public Stat GetStat(StatType type)
    {
        return type switch
        {
            StatType.Health => MaxLife,
            StatType.MaxHealth => MaxLife,
            StatType.Attack => Damage,
            StatType.MoveSpeed => Movespeed,
            StatType.AttackSpeed => AttackTimeInterval,
            _ => null
        };
    }

    public Stat GetStatByModifier(StatModifier modifier)
    {
        if (modifier == null) return null;
        if (MaxLife.Modifiers.Contains(modifier)) return MaxLife;
        if (Damage.Modifiers.Contains(modifier)) return Damage;
        if (Movespeed.Modifiers.Contains(modifier)) return Movespeed;
        if (AttackTimeInterval.Modifiers.Contains(modifier)) return AttackTimeInterval;
        if (MaxMana.Modifiers.Contains(modifier)) return MaxMana;
        if (AttackRange.Modifiers.Contains(modifier)) return AttackRange;
        if (DecisionInterval.Modifiers.Contains(modifier)) return DecisionInterval;
        if (IntervalJitter.Modifiers.Contains(modifier)) return IntervalJitter;
        return null;
    }

    private float CalculateFinalDamage(in DamageData damage)
    {
        float amount = damage.Amount;
        if (amount <= 0f) return 0f;

        switch (damage.Type)
        {
            case DamageType.Physical:
                // later: armor stat mitigation
                break;

            case DamageType.True:
                return amount;
        }

        return Mathf.Max(0f, amount);
    }
}
