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

    private bool hasLaunchFaction;
    private FactionId launchFaction;
    private string launchTag;

    private void CaptureCasterAlignment(GameObject caster)
    {
        hasLaunchFaction = false;
        launchTag = null;
        if (caster == null) return;
        var member = caster.GetComponentInParent<IFactionMember>();
        if (member != null) { hasLaunchFaction = true; launchFaction = member.Faction; }
        var brain = caster.GetComponentInParent<CharacterBrain>();
        launchTag = brain != null ? brain.tag : caster.tag;
    }

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
    private bool isDespawningFromPool;
    private Collider2D hitboxCollider;
    private readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private static ContactFilter2D skillContactFilter;
    private static bool filterInitialized;

    private void Awake()
    {
        hitResolver = GetComponent<SkillHitResolver>();
        movement    = GetComponent<ISkillMovement>();
        detonation  = GetComponent<ISkillDetonation>();
        targeting   = GetComponentInChildren<SkillTargeting>();
        if (hitboxObject != null)
            hitboxCollider = hitboxObject.GetComponent<Collider2D>();

        if (!filterInitialized)
        {
            filterInitialized = true;
            skillContactFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = (1 << 6) | (1 << 7) // Character (6) and HitboxTrigger (7)
            };
        }
    }

    public void Initialize(SkillStatsManager stats, Vector3 direction, string casterID, GameObject target = null, GameObject casterGameObject = null)
    {
        Stats = stats;
        Data = stats != null ? stats.SkillData : null;
        this.casterID = casterID;
        CasterGameObject = casterGameObject;
        CaptureCasterAlignment(casterGameObject);
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

        if (hitResolver == null) hitResolver = GetComponent<SkillHitResolver>();
        if (detonation == null) detonation = GetComponent<ISkillDetonation>();
        if (movement == null) movement = GetComponent<ISkillMovement>();
        movement?.Initialize(this, direction);

        if (Data != null && (Data.type == SkillType.Area || Data.type == SkillType.TargetedBurst))
        {
            DeactivateHitbox();
            if (Data.type == SkillType.TargetedBurst)
            {
                Detonate();
            }
        }
        else
        {
            ActivateHitbox();
            CheckOverlapHits();
        }
    }

    public void Initialize(SkillData data, Vector3 direction, string casterID, GameObject target = null, GameObject casterGameObject = null)
    {
        Stats = null;
        Data = data;
        this.casterID = casterID;
        CasterGameObject = casterGameObject;
        CaptureCasterAlignment(casterGameObject);
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

        if (hitResolver == null) hitResolver = GetComponent<SkillHitResolver>();
        if (detonation == null) detonation = GetComponent<ISkillDetonation>();
        if (movement == null) movement = GetComponent<ISkillMovement>();
        movement?.Initialize(this, direction);

        if (data != null && (data.type == SkillType.Area || data.type == SkillType.TargetedBurst))
        {
            DeactivateHitbox();
            if (data.type == SkillType.TargetedBurst)
            {
                Detonate();
            }
        }
        else
        {
            ActivateHitbox();
            CheckOverlapHits();
        }
    }

    private void Update()
    {
        if (!isActive || Data == null)
            return;

        float dt = Time.deltaTime;
        launchArmingTimer += dt;

        if (!isHit)
        {
            movement?.Tick(this, dt);
            CheckOverlapHits();
        }

        lifetimeTimer -= dt;
        if (lifetimeTimer <= 0f)
        {
            ReturnToPool();
        }
    }

    private void CheckOverlapHits()
    {
        if (!isActive || isHit || hitboxObject == null || !hitboxObject.activeInHierarchy)
            return;

        // 1. Direct target check for AtTarget / homing skills (e.g. AlliesOnly heals spawned directly at target)
        if (currentTarget != null)
        {
            float distSq = (transform.position - currentTarget.transform.position).sqrMagnitude;
            if (distSq <= 0.36f && CanHitTarget(currentTarget, HitContext.Direct))
            {
                OnHit(currentTarget, HitContext.Direct);
                if (isHit) return;
            }
        }

        // 2. Zero-allocation Physics2D overlap check on hitbox (handles static-collider projectiles vs kinematic character triggers)
        int count = 0;
        if (hitboxCollider != null && hitboxCollider.enabled)
        {
            count = hitboxCollider.Overlap(skillContactFilter, overlapBuffer);
        }
        else
        {
            count = Physics2D.OverlapCircle(hitboxObject.transform.position, 0.35f, skillContactFilter, overlapBuffer);
        }

        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            var brain = col.GetComponentInParent<CharacterBrain>();
            GameObject target = brain != null ? brain.gameObject : col.transform.root.gameObject;

            if (CanHitTarget(target, HitContext.Direct))
            {
                OnHit(target, HitContext.Direct);
                if (isHit && RemainingChains <= 0)
                    return;
            }
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

        SkillData.SelfTarget selfRule = Data != null ? Data.selfTarget : SkillData.SelfTarget.Allowed;
        if (isCaster && selfRule == SkillData.SelfTarget.Excluded)
            return false;
        if (!isCaster && selfRule == SkillData.SelfTarget.Only)
            return false;

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
        if (Data != null && Data.type == SkillType.TargetedBurst)
        {
            if (target == null) return false;
            var member = target.GetComponentInParent<IFactionMember>();
            if (hasLaunchFaction && member != null)
                return launchFaction != FactionId.Neutral && member.Faction == launchFaction;
            var targetBrainAtBurst = target.GetComponentInParent<CharacterBrain>();
            string targetTagAtBurst = targetBrainAtBurst != null ? targetBrainAtBurst.tag : target.tag;
            return (launchTag == "Adventurer" || launchTag == "Monster" || launchTag == "Faction_01" || launchTag == "Faction_02") && launchTag == targetTagAtBurst;
        }
        if (CasterGameObject == null || target == null)
            return false;

        // Check IFactionMember first
        var casterFaction = CasterGameObject.GetComponentInParent<IFactionMember>();
        var targetFaction = target.GetComponentInParent<IFactionMember>();
        if (casterFaction != null && targetFaction != null)
        {
            return casterFaction.IsAllyOf(targetFaction);
        }

        // Check tags fallback (Adventurer, Monster, Factions)
        var casterBrain = CasterGameObject.GetComponentInParent<CharacterBrain>();
        var targetBrain = target.GetComponentInParent<CharacterBrain>();
        string casterTag = casterBrain != null ? casterBrain.gameObject.tag : CasterGameObject.tag;
        string targetTag = targetBrain != null ? targetBrain.gameObject.tag : target.tag;

        if (casterTag == "Adventurer" && targetTag == "Adventurer")
            return true;

        if (casterTag == "Monster" && targetTag == "Monster")
            return true;

        if (casterTag == "Faction_01" && targetTag == "Faction_01")
            return true;

        if (casterTag == "Faction_02" && targetTag == "Faction_02")
            return true;

        return false;
    }

    public void OnHit(GameObject target, HitContext context)
    {
        if (!isActive)
            return;

        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(OnHitHash);
            
        isHit = true;
        hitResolver.ResolveHit(this, target);
        RegisterHit(target);

        if (context == HitContext.Direct &&
            RemainingChains > 0 &&
            targeting != null &&
            targeting.TryGetTarget(this, out var next))
        {
            isHit = false;
            RemainingChains--;
            float multiplier = Stats != null ? Stats.ChainDamageMultiplier.Value : (Data != null ? Data.chainDamageMultiplier : 1f);
            CurrentDamage *= multiplier;
            movement?.Redirect(this, next);
            return;
        }

        if (context == HitContext.Direct || context == HitContext.Chain)
        {
            Detonate();
            // If there is no detonation component and no hit animation controller, return to pool immediately
            if (detonation == null && (animator == null || animator.runtimeAnimatorController == null))
            {
                ReturnToPool();
            }
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
        if (!isActive && !gameObject.activeSelf)
            return;

        isActive = false;
        isDespawningFromPool = true;
        if (ObjectPoolManager.Instance != null && TryGetComponent<PooledObject>(out _))
        {
            ObjectPoolManager.Instance.Despawn(gameObject);
        }
        else
        {
            OnDespawn();
            gameObject.SetActive(false);
        }
        isDespawningFromPool = false;
    }

    public void OnSpawn()
    {
        isActive = true;
        isDespawningFromPool = false;
        HasDetonated = false;
        hasExitedCaster = false;
        launchArmingTimer = 0f;
        HitTargets.Clear();
    }

    public void OnDespawn()
    {
        // If an AnimationEvent (e.g. WIS001_H.anim) or AoEDetonation calls OnDespawn() directly,
        // route through ObjectPoolManager so the GameObject is deactivated and returned to pool.
        if (!isDespawningFromPool && gameObject.activeSelf && ObjectPoolManager.Instance != null)
        {
            ReturnToPool();
            return;
        }

        isActive = false;
        currentTarget = null;
        CasterGameObject = null;
        hasLaunchFaction = false;
        launchTag = null;
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
    }

    public void ActivateHitbox()
    {
        if (hitboxObject != null)
        {
            hitboxObject.SetActive(true);
            CheckOverlapHits();
        }
    }

    public void DeactivateHitbox()
    {
        if (hitboxObject != null)
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
