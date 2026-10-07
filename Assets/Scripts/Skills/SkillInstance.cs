using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SB4b1.Logging;
using UnityEngine;

[RequireComponent(typeof(SkillHitResolver))]
public class SkillInstance : MonoBehaviour, IPoolable
{
    private static readonly int OnHitHash = Animator.StringToHash("OnHit");
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private GameObject hitboxObject;

    [ReadOnly]
    public GameObject currentTarget;

    public string casterID { get; private set; }
    public GameObject CasterGameObject { get; private set; }
    public SkillData.SkillTargetFilter TargetFilter => Data != null ? Data.targetFilter : SkillData.SkillTargetFilter.EnemiesOnly;

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

    private bool hasExitedCaster;
    private float launchArmingTimer;
    private const float LAUNCH_ARMING_DELAY = 0.1f;

    private void Awake()
    {
        hitResolver = GetComponent<SkillHitResolver>();
        movement    = GetComponent<ISkillMovement>();
        detonation  = GetComponent<ISkillDetonation>();
        
        targeting   = GetComponentInChildren<SkillTargeting>();
    }

    public void Initialize(SkillStatsManager stats, Vector3 direction, string casterID, GameObject target = null, GameObject casterGameObject = null)
    {
        Stats = stats;
        Data = stats != null ? stats.SkillData : null;
        this.casterID = casterID;
        CasterGameObject = casterGameObject;
        currentTarget = target;
        CurrentDamage = stats != null ? stats.Damage.Value : (Data != null ? Data.baseDamage : 0f);
        RemainingChains = stats != null ? (int)stats.MaxChainTargets.Value : (Data != null ? Data.maxChainTargets : 0);
        HasDetonated = false;
        hasExitedCaster = false;
        launchArmingTimer = 0f;

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

    public void Initialize(SkillData data, Vector3 direction, string casterID, GameObject target = null, GameObject casterGameObject = null)
    {
        Stats = null;
        Data = data;
        this.casterID = casterID;
        CasterGameObject = casterGameObject;
        currentTarget = target;
        CurrentDamage = data != null ? data.baseDamage : 0f;
        RemainingChains = data != null ? data.maxChainTargets : 0;
        HasDetonated = false;
        hasExitedCaster = false;
        launchArmingTimer = 0f;

        HitTargets.Clear();
        lifetimeTimer = data != null ? data.projectileLifetime : 0f;
        isActive = true;
        isHit = false;

        movement?.Initialize(this, direction);

        if (data != null && data.type == SkillType.Area)
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
        launchArmingTimer += dt;

        if (!isHit)
            movement?.Tick(this, dt);

        lifetimeTimer -= dt;
        if (lifetimeTimer <= 0f)
        {
            ReturnToPool();
        }
    }

    public void NotifyExitedCaster()
    {
        hasExitedCaster = true;
    }

    public bool CanHitTarget(GameObject target, HitContext context)
    {
        if (target == null || !isActive)
            return false;

        if (HitTargets.Contains(target))
            return false;

        bool isCaster = (CasterGameObject != null && target == CasterGameObject) ||
                        (!string.IsNullOrEmpty(casterID) && target.TryGetComponent(out IIdentifier objID) && objID.UniqueId == casterID);

        SkillData.SkillTargetFilter filter = TargetFilter;

        if (isCaster)
        {
            if (filter == SkillData.SkillTargetFilter.EnemiesOnly || filter == SkillData.SkillTargetFilter.AllExceptCaster)
                return false;

            if (filter == SkillData.SkillTargetFilter.Everyone)
            {
                // For direct projectile collisions, prevent instant self-collision on spawn before exiting caster hitbox or arming delay
                if (context == HitContext.Direct && !hasExitedCaster && launchArmingTimer < LAUNCH_ARMING_DELAY)
                    return false;

                return true;
            }

            if (filter == SkillData.SkillTargetFilter.SelfOnly)
                return true;

            if (filter == SkillData.SkillTargetFilter.AlliesOnly)
                return true;

            return false;
        }

        // If target is NOT the caster:
        if (filter == SkillData.SkillTargetFilter.SelfOnly)
            return false;

        if (filter == SkillData.SkillTargetFilter.Everyone || filter == SkillData.SkillTargetFilter.AllExceptCaster)
            return true;

        // Faction checking for EnemiesOnly vs AlliesOnly
        bool isAlly = IsAlly(target);

        if (filter == SkillData.SkillTargetFilter.EnemiesOnly)
            return !isAlly;

        if (filter == SkillData.SkillTargetFilter.AlliesOnly)
            return isAlly;

        return true;
    }

    private bool IsAlly(GameObject target)
    {
        if (CasterGameObject == null || target == null)
            return false;

        // Check tags (Adventurer, Monster)
        if (CasterGameObject.CompareTag("Adventurer") && target.CompareTag("Adventurer"))
            return true;

        if (CasterGameObject.CompareTag("Monster") && target.CompareTag("Monster"))
            return true;

        return false;
    }

    public void OnHit(GameObject target, HitContext context)
    {
        if (!isActive)
            return;

        if (animator != null)
            animator.SetTrigger(OnHitHash);
            
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
        hasExitedCaster = false;
        launchArmingTimer = 0f;
        HitTargets.Clear();
    }

    public void OnDespawn()
    {
        currentTarget = null;
        CasterGameObject = null;
        casterID = null;
        hasExitedCaster = false;
        launchArmingTimer = 0f;
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
