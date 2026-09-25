using UnityEngine;
using SB4b1.Logging;
using ArtificeToolkit.Attributes;

public class SkillStatsManager : MonoBehaviour
{
    private SkillCaster caster;

    [Header("Base Data")]
    [SerializeField] private SkillData baseData;

    [Header("Cooldown Settings")]
    [SerializeField] private float baseCooldown = 1f;

    public SkillData BaseData
    {
        get => baseData;
        set
        {
            baseData = value;
            InitializeStats();
        }
    }

    public SkillData SkillData => baseData;
    public SkillData Data => baseData;

    public float BaseCooldown
    {
        get => baseCooldown;
        set
        {
            baseCooldown = value;
            Cooldown.BaseValue = value;
            UpdateInspectorValues();
        }
    }

    // Core stats (private set = safer)

#if UNITY_EDITOR
    [Header("Runtime Stats (Read Only)")]
    [SerializeField, ReadOnly] private float damage;
    [SerializeField, ReadOnly] private float manaCost;
    [SerializeField, ReadOnly] private float cooldown;
    [SerializeField, ReadOnly] private float projectileSpeed;
    [SerializeField, ReadOnly] private float projectileLifetime;
    [SerializeField, ReadOnly] private float areaRadius;
    [SerializeField, ReadOnly] private float beamTickInterval;
    [SerializeField, ReadOnly] private float chainDamageMultiplier;
    [SerializeField, ReadOnly] private float maxChainTargets;
#endif

    public Stat Damage { get; private set; } = new Stat();
    public Stat ManaCost { get; private set; } = new Stat();
    public Stat Cooldown { get; private set; } = new Stat();
    public Stat ProjectileSpeed { get; private set; } = new Stat();
    public Stat ProjectileLifetime { get; private set; } = new Stat();
    public Stat AreaRadius { get; private set; } = new Stat();
    public Stat BeamTickInterval { get; private set; } = new Stat();
    public Stat ChainDamageMultiplier { get; private set; } = new Stat();
    public Stat MaxChainTargets { get; private set; } = new Stat();

    private void Awake()
    {
        caster = GetComponent<SkillCaster>();
        InitializeStats();
    }

    // =========================
    // Initialization
    // =========================
    public void InitializeStats()
    {
        if (baseData == null)
        {
            ELogger.Warning(this, nameof(InitializeStats), "Missing SkillData");
            return;
        }

        ApplyBaseStats();
    }

    public void ApplyBaseStats()
    {
        Damage.BaseValue = baseData.baseDamage;
        ManaCost.BaseValue = baseData.baseManaCost;
        Cooldown.BaseValue = baseCooldown;
        ProjectileSpeed.BaseValue = baseData.projectileSpeed;
        ProjectileLifetime.BaseValue = baseData.projectileLifetime;
        AreaRadius.BaseValue = baseData.areaRadius;
        BeamTickInterval.BaseValue = baseData.beamTickInterval;
        ChainDamageMultiplier.BaseValue = baseData.chainDamageMultiplier;
        MaxChainTargets.BaseValue = baseData.maxChainTargets;

        UpdateInspectorValues();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (baseData != null)
        {
            ApplyBaseStats();
        }
    }
#endif

    public void UpdateInspectorValues()
    {
#if UNITY_EDITOR
        damage = Damage.Value;
        manaCost = ManaCost.Value;
        cooldown = Cooldown.Value;
        projectileSpeed = ProjectileSpeed.Value;
        projectileLifetime = ProjectileLifetime.Value;
        areaRadius = AreaRadius.Value;
        beamTickInterval = BeamTickInterval.Value;
        chainDamageMultiplier = ChainDamageMultiplier.Value;
        maxChainTargets = MaxChainTargets.Value;
#endif
    }

    // =========================
    // Modifier Helpers
    // =========================
    public void ClearAllModifiers()
    {
        Damage.ClearModifiers();
        ManaCost.ClearModifiers();
        Cooldown.ClearModifiers();
        ProjectileSpeed.ClearModifiers();
        ProjectileLifetime.ClearModifiers();
        AreaRadius.ClearModifiers();
        BeamTickInterval.ClearModifiers();
        ChainDamageMultiplier.ClearModifiers();
        MaxChainTargets.ClearModifiers();

        UpdateInspectorValues();
    }
}
