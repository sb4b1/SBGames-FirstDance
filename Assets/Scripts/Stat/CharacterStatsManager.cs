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
    private AdventurerFunction? functionOverride;
    private BasicAttackMode? basicAttackModeOverride;
    public CharacterBaseStatSO BaseStats => baseStats;
    public CharacterClass CharacterClass => baseStats != null ? baseStats.characterClass : CharacterClass.None;
    public AdventurerFunction CharacterFunction => functionOverride ?? (baseStats != null ? baseStats.characterFunction : AdventurerFunction.None);
    public AdventurerFunction EffectiveFunction => (functionOverride.HasValue && functionOverride.Value != AdventurerFunction.None) ? functionOverride.Value : ((baseStats != null && baseStats.characterFunction != AdventurerFunction.None) ? baseStats.characterFunction : AdventurerFunction.DPS);
    public AdventurerWealth CharacterWealth => baseStats != null ? baseStats.characterWealth : AdventurerWealth.Average;
    public BasicAttackMode BasicAttackMode => basicAttackModeOverride ?? (baseStats != null ? baseStats.basicAttackMode : BasicAttackMode.Auto);
    public void SetFunctionOverride(AdventurerFunction func) => functionOverride = func;
    public void SetBasicAttackModeOverride(BasicAttackMode mode) => basicAttackModeOverride = mode;

    [Header("Immortality / Training")]
    [SerializeField] private bool isImmortal;
    public bool IsImmortal { get => isImmortal; set => isImmortal = value; }

    [Header("Regeneration")]
    [SerializeField] private RegenTuningSO regenTuning;
    public RegenTuningSO RegenTuning => regenTuning;
    public void SetRegenTuning(RegenTuningSO tuning) => regenTuning = tuning;

    private float timeSinceLastDamageTakenOrDealt;
    private bool isApplyingRegen;
    private static SBGames.Loop.CoreGameLoop cachedGameLoop;
    private static int cachedGameLoopFrame = -1;

    public float TimeSinceLastDamageTakenOrDealt => timeSinceLastDamageTakenOrDealt;
    public float EffectiveOutOfCombatDelay => regenTuning != null ? regenTuning.OutOfCombatDelay : RegenTuningSO.DefaultOutOfCombatDelay;
    public float EffectiveOutOfCombatRegenMultiplier => regenTuning != null ? regenTuning.OutOfCombatRegenMultiplier : RegenTuningSO.DefaultOutOfCombatRegenMultiplier;
    public bool IsOutOfCombat => timeSinceLastDamageTakenOrDealt >= EffectiveOutOfCombatDelay;

    private Coroutine attackCooldownCoroutine;

    // Runtime life
    [SerializeField] private float life;
    [SerializeField] private float mana;

    public int Level => level;
    public float Life { get => life; private set => life = value; }
    public float Mana { get => mana; private set => mana = value; }
    public bool IsDead => Life <= 0;

    public event System.Action<int> OnLevelChanged;
    public event System.Action<DamageData> OnDamaged;
    public event System.Action<float> OnLifeRestored;
    public event System.Action<float> OnRegenLifeRestored;

    // Core stats (private set = safer)

#if UNITY_EDITOR
    [SerializeField, ReadOnly] private float maxLife;
    [SerializeField, ReadOnly] private float maxMana;
    [SerializeField, ReadOnly] private float lifeRegen;
    [SerializeField, ReadOnly] private float manaRegen;
    [SerializeField, ReadOnly] private float movespeed;
    [SerializeField, ReadOnly] private float damage;
    [SerializeField, ReadOnly] private float attackRange;
    [SerializeField, ReadOnly] private float attackTimeInterval;
    [SerializeField, ReadOnly] private float decisionInterval;
    [SerializeField, ReadOnly] private float intervalJitter;
#endif

    public Stat MaxLife { get; private set; } = new Stat(100f);
    public Stat MaxMana { get; private set; } = new Stat(50f);
    public Stat LifeRegen { get; private set; } = new Stat(0f);
    public Stat ManaRegen { get; private set; } = new Stat(0f);
    public Stat Movespeed { get; private set; } = new Stat(5f);
    public Stat Damage { get; private set; } = new Stat(10f);
    public Stat AttackRange { get; private set; } = new Stat(1f);
    public Stat AttackTimeInterval { get; private set; } = new Stat(1f);
    public Stat DecisionInterval { get; private set; } = new Stat(0.3f);
    public Stat IntervalJitter { get; private set; } = new Stat(0.05f);

    public bool AttackReady { get; private set; } = true;

    public event System.Action<float, float> OnLifeChanged;
    public event System.Action<CharacterStatsManager> OnCharacterDeath;

    private void Awake()
    {
        brain = GetComponent<CharacterBrain>();

        InitializeStats();
    }

    private void Update()
    {
        TickRegen(Time.deltaTime);
    }

    // =========================
    // Initialization
    // =========================
    private void InitializeStats()
    {
        timeSinceLastDamageTakenOrDealt = 0f;

        if (baseStats == null)
        {
            Life = MaxLife.Value;
            Mana = MaxMana.Value;
            NotifyLifeChanged();
            ELogger.Log(this, nameof(InitializeStats), "Missing CharacterBaseStats");
            return;
        }

        ApplyBaseStats();
        Life = MaxLife.Value;
        Mana = MaxMana.Value;
        NotifyLifeChanged();
    }

    public void SetBaseStats(CharacterBaseStatSO newBaseStats)
    {
        baseStats = newBaseStats;
        InitializeStats();
    }

    private void ApplyBaseStats()
    {
        MaxLife.BaseValue = baseStats.MaxLife;
        MaxMana.BaseValue = baseStats.MaxMana;
        LifeRegen.BaseValue = baseStats.LifeRegen;
        ManaRegen.BaseValue = baseStats.ManaRegen;
        Movespeed.BaseValue = baseStats.Movespeed;
        Damage.BaseValue = baseStats.Damage;
        AttackRange.BaseValue = baseStats.AttackRange;
        AttackTimeInterval.BaseValue = baseStats.AttackTimeInterval;
        DecisionInterval.BaseValue = baseStats.DecisionInterval;
        IntervalJitter.BaseValue = baseStats.IntervalJitter;

#if UNITY_EDITOR
        maxLife = MaxLife.Value;
        maxMana = MaxMana.Value;
        lifeRegen = LifeRegen.Value;
        manaRegen = ManaRegen.Value;
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
    // Life & Regen Handling
    // =========================
    public void NotifyDamageDealt()
    {
        timeSinceLastDamageTakenOrDealt = 0f;
    }

    public void TickRegen(float dt)
    {
        if (dt <= 0f) return;
        if (Life <= 0f) return;
        if (!IsInBattlePhase()) return;

        timeSinceLastDamageTakenOrDealt += dt;

        float multiplier = timeSinceLastDamageTakenOrDealt >= EffectiveOutOfCombatDelay
            ? EffectiveOutOfCombatRegenMultiplier
            : 1f;

        float lifeRegenRate = LifeRegen.Value;
        if (lifeRegenRate > 0f && Life < MaxLife.Value)
        {
            float lifeGain = lifeRegenRate * multiplier * dt;
            if (lifeGain > 0f)
            {
                isApplyingRegen = true;
                RestoreLife(lifeGain);
                isApplyingRegen = false;
            }
        }

        float manaRegenRate = ManaRegen.Value;
        if (manaRegenRate > 0f && Mana < MaxMana.Value)
        {
            float manaGain = manaRegenRate * multiplier * dt;
            if (manaGain > 0f)
            {
                RestoreMana(manaGain);
            }
        }
    }

    private static bool IsInBattlePhase()
    {
        if (cachedGameLoop != null && cachedGameLoop.isActiveAndEnabled)
        {
            return cachedGameLoop.CurrentPhase == SBGames.Loop.GamePhase.Battle;
        }

        int frame = Time.frameCount;
        if (!Application.isPlaying || cachedGameLoopFrame != frame || cachedGameLoop == null)
        {
            cachedGameLoopFrame = frame;
            cachedGameLoop = FindAnyObjectByType<SBGames.Loop.CoreGameLoop>();
        }

        if (cachedGameLoop != null && cachedGameLoop.isActiveAndEnabled)
        {
            return cachedGameLoop.CurrentPhase == SBGames.Loop.GamePhase.Battle;
        }

        return true;
    }

    public void RestoreLife(float value)
    {
        if (value <= 0) return;

        float oldLife = Life;
        Life = Mathf.Min(Life + value, MaxLife.Value);
        float actualRestored = Mathf.Max(0f, Life - oldLife);
        NotifyLifeChanged();
        if (actualRestored > 0f)
        {
            if (isApplyingRegen)
            {
                OnRegenLifeRestored?.Invoke(actualRestored);
            }
            else
            {
                OnLifeRestored?.Invoke(actualRestored);
            }
        }

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
        timeSinceLastDamageTakenOrDealt = 0f;
        NotifySourceDamageDealt(damage.Source);

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

    private static void NotifySourceDamageDealt(object source)
    {
        if (source == null) return;

        if (source is CharacterStatsManager dealerStats)
        {
            dealerStats.NotifyDamageDealt();
        }
        else if (source is SkillInstance skillInst && skillInst.CasterGameObject != null)
        {
            var casterStats = skillInst.CasterGameObject.GetComponent<CharacterStatsManager>()
                ?? skillInst.CasterGameObject.GetComponentInParent<CharacterStatsManager>();
            if (casterStats != null)
            {
                casterStats.NotifyDamageDealt();
            }
        }
        else if (source is Component comp)
        {
            var compStats = comp.GetComponent<CharacterStatsManager>()
                ?? comp.GetComponentInParent<CharacterStatsManager>();
            if (compStats != null)
            {
                compStats.NotifyDamageDealt();
            }
        }
        else if (source is GameObject go)
        {
            var goStats = go.GetComponent<CharacterStatsManager>()
                ?? go.GetComponentInParent<CharacterStatsManager>();
            if (goStats != null)
            {
                goStats.NotifyDamageDealt();
            }
        }
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
        SBGames.Characters.AI.AdventurerEventHub.RaiseCharacterDied(this, gameObject);

        if (brain == null)
        {
            brain = GetComponent<CharacterBrain>();
        }
        brain?.OnDespawn();

        ELogger.Log(this, nameof(OnDeath), $"{name} died");
        if (Application.isPlaying)
        {
            Destroy(gameObject); // Temporary death handling - destroy the GameObject. Later: add death animation, ragdoll, etc.
        }
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
            StatType.LifeRegen => LifeRegen,
            StatType.ManaRegen => ManaRegen,
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
        if (LifeRegen.Modifiers.Contains(modifier)) return LifeRegen;
        if (ManaRegen.Modifiers.Contains(modifier)) return ManaRegen;
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
