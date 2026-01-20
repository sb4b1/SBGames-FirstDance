using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SB4b1.Logging;
using UnityEngine;

public class SkillInstance : MonoBehaviour, IPoolable
{
    public SkillData Data { get; private set; }
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

    private void Awake()
    {
        targeting   = GetComponent<SkillTargeting>();
        hitResolver = GetComponent<SkillHitResolver>();
        movement    = GetComponent<ISkillMovement>();
        detonation  = GetComponent<ISkillDetonation>();
    }

    public void Initialize(SkillData data, Vector3 direction)
    {
        Data = data;
        CurrentDamage = data.baseDamage;

        RemainingChains = data.maxChainTargets;
        HasDetonated = false;

        HitTargets.Clear();

        lifetimeTimer = data.projectileLifetime;
        isActive = true;

        movement?.Initialize(this, direction);
    }

    private void Update()
    {
        if (!isActive || Data == null)
            return;

        float dt = Time.deltaTime;

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

        hitResolver.ResolveHit(this, target);
        RegisterHit(target);

        if (context == HitContext.Direct &&
            RemainingChains > 0 &&
            targeting.TryGetNextTarget(this, out var next))
        {
            RemainingChains--;
            CurrentDamage *= Data.chainDamageMultiplier;
            movement?.Redirect(this, next);
            return;
        }

        if (context == HitContext.Direct || context == HitContext.Chain)
        {
            Detonate();
            ReturnToPool();
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
        Data = null;
        CurrentDamage = 0f;
        RemainingChains = 0;
        HasDetonated = false;
        HitTargets.Clear();
    }

#if UNITY_EDITOR
    [Button]
    private void OnValidate()
    {
        if (Data == null) return;

        if (Data.maxChainTargets > 0 && targeting == null)
            ELogger.Warning("Chain enabled but no SkillTargeting component", this);

        if (Data.aoeRadius > 0 && detonation == null)
            ELogger.Warning("AoE configured but no ISkillDetonation", this);
    }
#endif
}
