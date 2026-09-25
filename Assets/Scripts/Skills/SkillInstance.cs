using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SB4b1.Logging;
using UnityEngine;

[RequireComponent(typeof(SkillHitResolver))]
public class SkillInstance : MonoBehaviour, IPoolable
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private GameObject hitboxObject;

    [ReadOnly]
    public GameObject currentTarget;

    public string casterID { get; private set; }

    public SkillData Data { get; private set; }
    public SkillStatsManager Stats { get; private set; }
    public float CurrentDamage { get; set; }

    private SkillTargeting targeting;
    private SkillHitResolver hitResolver;
    private ISkillMovement movement;
    private ISkillDetonation detonation;

    public HashSet<GameObject> HitTargets { get; } = new();

    public int RemainingChains { get; private set; }
    public bool HasDetonated { get; private set; }

    private float lifetimeTimer;
    private bool isActive;
    private bool isHit;

    private void Awake()
    {
        hitResolver = GetComponent<SkillHitResolver>();
        movement    = GetComponent<ISkillMovement>();
        detonation  = GetComponent<ISkillDetonation>();
        
        targeting   = GetComponentInChildren<SkillTargeting>();
    }

    public void Initialize(SkillStatsManager stats, Vector3 direction, string casterID, GameObject target = null)
    {
        Stats = stats;
        Data = stats != null ? stats.SkillData : null;
        this.casterID = casterID;
        currentTarget = target;
        CurrentDamage = stats != null ? stats.Damage.Value : (Data != null ? Data.baseDamage : 0f);
        RemainingChains = stats != null ? (int)stats.MaxChainTargets.Value : (Data != null ? Data.maxChainTargets : 0);
        HasDetonated = false;

        HitTargets.Clear();
        lifetimeTimer = stats != null ? stats.ProjectileLifetime.Value : (Data != null ? Data.projectileLifetime : 0f);
        isActive = true;
        isHit = false;

        movement?.Initialize(this, direction);

        if (Data != null && Data.type == SkillType.Area)
        {
            DeactivateHitbox();
        }
        else
        {
            ActivateHitbox();
        }
    }

    public void Initialize(SkillData data, Vector3 direction,string casterID, GameObject target = null)
    {
        Stats = null;
        Data = data;
        this.casterID = casterID;
        currentTarget = target;
        CurrentDamage = data.baseDamage;
        RemainingChains = data.maxChainTargets;
        HasDetonated = false;

        HitTargets.Clear();
        lifetimeTimer = data.projectileLifetime;
        isActive = true;
        isHit = false;

        movement?.Initialize(this, direction);

        if (data.type == SkillType.Area)
        {
            DeactivateHitbox();
        }
        else
        {
            ActivateHitbox();
        }
    }

    private void Update()
    {
        if (!isActive || Data == null)
            return;

        float dt = Time.deltaTime;

        if (!isHit)
            movement?.Tick(this, dt);

        lifetimeTimer -= dt;
        if (lifetimeTimer <= 0f)
        {
            ReturnToPool();
        }
    }

    public void OnHit(GameObject target, HitContext context)
    {
        if (!isActive)
            return;

        animator?.SetTrigger("OnHit");
        isHit = true;
        hitResolver.ResolveHit(this, target);
        RegisterHit(target);

        if (context == HitContext.Direct &&
            RemainingChains > 0 &&
            targeting.TryGetTarget(this, out var next))
        {
            RemainingChains--;
            float multiplier = Stats != null ? Stats.ChainDamageMultiplier.Value : (Data != null ? Data.chainDamageMultiplier : 1f);
            CurrentDamage *= multiplier;
            movement?.Redirect(this, next);
            return;
        }

        if (context == HitContext.Direct || context == HitContext.Chain)
        {
            Detonate();
        }
    }

    public void Detonate()
    {
        if (HasDetonated) return;
        HasDetonated = true;

        detonation?.Detonate(this);
    }

    public void RegisterHit(GameObject target)
    {
        HitTargets.Add(target);
    }

    private void ReturnToPool()
    {
        if (!isActive)
            return;

        isActive = false;
        ObjectPoolManager.Instance.Despawn(gameObject);
    }

    public void OnSpawn()
    {
        isActive = true;
        HasDetonated = false;
        HitTargets.Clear();
    }

    public void OnDespawn()
    {
        currentTarget = null;
        isHit = false;
        Data = null;
        Stats = null;
        CurrentDamage = 0f;
        RemainingChains = 0;
        HasDetonated = false;
        HitTargets.Clear();
        ReturnToPool();
    }

    public void ActivateHitbox()
    {
        hitboxObject.SetActive(true);
    }

    public void DeactivateHitbox()
    {
        hitboxObject.SetActive(false);
    }

#if UNITY_EDITOR
    [Button]
    private void OnValidate()
    {
        if (Data == null) return;

        if (Data.maxChainTargets > 0 && targeting == null)
            ELogger.Warning("Chain enabled but no SkillTargeting component", this);

        if (Data.areaRadius > 0 && detonation == null)
            ELogger.Warning("AoE configured but no ISkillDetonation", this);
    }
#endif
}
