using UnityEngine;
using SB4b1.ComponentUtils;
using SB4b1.Logging;
using System.Collections;

[RequireComponent(typeof(CharacterStatsManager))]
public class CharacterBrain : MonoBehaviour, IPoolable
{
    // =========================
    // Control
    // =========================

    [SerializeField] private ControlMode controlMode;
    [SerializeField] private AgentStatus agentStatus;

    public ControlMode ControlMode => controlMode;
    public AgentStatus AgentStatus => agentStatus;

    [SerializeField] private GameObject aiAugment;
    [SerializeField] private CharacterStatsManager statsManager;
    [SerializeField] private AnimationObjectController animController;
    [SerializeField] private CharacterSkillController skillController;

    // =========================
    // State
    // =========================
    public bool CanMove { get; private set; } = true;
    public bool CanAttack => statsManager != null && statsManager.AttackReady && !IsBusy;
    public bool CanCastSkill => !IsBusy;

    public bool IsDashing { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsCastingSkill { get; private set; }
    public bool IsRecovering { get; private set; }
    public bool IsBusy => IsAttacking || IsCastingSkill || IsRecovering;
    public bool HasAI { get; private set; }

    private Coroutine recoveryCoroutine;
    private Coroutine castFallbackCoroutine;

    public CharacterStatsManager StatsManager => statsManager != null ? statsManager : (statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>());
    public AnimationObjectController AnimController => animController != null ? animController : (animController = GetComponent<AnimationObjectController>() ?? GetComponentInChildren<AnimationObjectController>());
    public CharacterSkillController SkillController => skillController != null ? skillController : (skillController = GetComponent<CharacterSkillController>() ?? GetComponentInChildren<CharacterSkillController>());

    private void Awake()
    {
        aiAugment = ComponentUtil.GetChildGameObjectByTag(this.gameObject, "AI_Augment");
        HasAI = aiAugment != null;

        if (statsManager == null)
        {
            statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
        }

        if (animController == null)
        {
            animController = GetComponent<AnimationObjectController>() ?? GetComponentInChildren<AnimationObjectController>();
        }

        if (skillController == null)
        {
            skillController = GetComponent<CharacterSkillController>() ?? GetComponentInChildren<CharacterSkillController>();
        }
    }

    private void Start()
    {
        OnSpawn();

        if (aiAugment != null)
        {
            bool activeAI = aiAugment.activeSelf;
            AIActivation(true);
            DelayAction(0.1f, () => AIActivation(controlMode == ControlMode.AI ? true : activeAI));
        }
    }

    private void OnEnable()
    {
        if (StatsManager != null)
        {
            StatsManager.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (statsManager != null)
        {
            statsManager.OnDamaged -= HandleDamaged;
        }
        InterruptAllActions();
    }

    private void HandleDamaged(DamageData damage)
    {
        if (AnimController != null)
        {
            AnimController.PlayHit();
        }
    }

    // =========================
    // Pooling
    // =========================

    public void OnSpawn()
    {
        // Register this character when spawned
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.RegisterCharacter(this);
        }
    }

    public void OnDespawn()
    {
        InterruptAllActions();

        // Unregister this character when despawned
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.UnregisterCharacter(this);
        }
    }

    // =========================
    // Control API
    // =========================

    public void AIActivation(bool value)
    {
        if (aiAugment != null)
        {
            aiAugment.SetActive(value);
        }

        agentStatus = value ? AgentStatus.Active : AgentStatus.Inactive;
        //ELogger.Log(this, nameof(AIActivation), value ? "Activate AI" : "Deactivate AI");
    }

    public void AddAIAugment()
    {
        aiAugment = ComponentUtil.GetChildGameObjectByTag(this.gameObject, "AI_Augment");

        if (aiAugment == null)
        {
            ELogger.Warning(this, nameof(AddAIAugment), "No AI Augment found in children with tag 'AI_Augment'!");
            return;
        }

        HasAI = true;
        ELogger.Log(this, nameof(AddAIAugment), "AI Augment added successfully. : " + aiAugment.name);
    }
    
    public void AddAIAugment(GameObject ai)
    {
        if (ai == null)
        {
            ELogger.Warning(this, nameof(AddAIAugment), "Provided AI Augment is null!");
            return;
        }

        aiAugment = ai;
        HasAI = true;
    }
    // =========================
    // Dash
    // =========================
    public bool TryStartDash()
    {
        if (IsDashing || !CanMove || IsBusy) return false;

        IsDashing = true;
        CanMove = false;
        return true;
    }

    public void EndDash()
    {
        IsDashing = false;
        CanMove = true;
    }

    // =========================
    // Attack
    // =========================
    public bool TryStartAttack()
    {
        if (IsAttacking || !CanAttack) return false;

        if (statsManager != null)
        {
            statsManager.StartAttackTimer();
        }

        CanMove = false;
        IsAttacking = true;
        if (animController != null)
        {
            animController.UpdateAttack(true);
        }
        return true;
    }

    // =========================
    // Skill
    // =========================
    public bool TryCastSkill(float fallbackCastDuration = -1f)
    {
        if (!CanCastSkill) return false;

        CanMove = false;
        IsCastingSkill = true;
        if (animController != null)
        {
            animController.UpdateCastSkill(true);
        }

        // Determine effective cast duration / timeout
        float castDuration = fallbackCastDuration;
        if (castDuration <= 0f)
        {
            var anim = AnimController;
            if (anim != null && anim.CastAnimationDuration > 0f)
            {
                float clipLength = anim.CastAnimationDuration;
                // If the animation clip contains a completion event, the fallback timer is strictly
                // a safety timeout and must not fire before the animation completes.
                castDuration = anim.HasCastCompleteEvent ? clipLength + 0.5f : clipLength;
            }
            else
            {
                castDuration = 1.5f;
            }
        }

        if (statsManager != null)
        {
            // Keep basic attack on cooldown throughout the casting animation
            statsManager.EnsureAttackCooldown(castDuration + 1f);
        }

        if (castFallbackCoroutine != null)
        {
            StopCoroutine(castFallbackCoroutine);
            castFallbackCoroutine = null;
        }
        castFallbackCoroutine = StartCoroutine(CastFallbackCoroutine(castDuration));

        return true;
    }

    private IEnumerator CastFallbackCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        castFallbackCoroutine = null;
        if (IsCastingSkill)
        {
            EndCastSkill();
        }
    }

    public void EndAttack(float customRecoveryDelay = -1f)
    {
        CanMove = true;
        IsAttacking = false;
        if (animController != null)
        {
            animController.UpdateAttack(false);
        }

        // Determine post-attack recovery delay
        float recoveryDelay = customRecoveryDelay;
        if (recoveryDelay < 0f)
        {
            if (statsManager != null && statsManager.BaseStats != null)
            {
                recoveryDelay = statsManager.BaseStats.DefaultPostAttackRecovery;
            }
            else
            {
                recoveryDelay = 0.3f;
            }
        }

        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }

        if (recoveryDelay > 0.001f)
        {
            IsRecovering = true;
            recoveryCoroutine = StartCoroutine(RecoveryDelayCoroutine(recoveryDelay));
        }
        else
        {
            IsRecovering = false;
        }
    }

    public void EndCastSkill(float customRecoveryDelay = -1f)
    {
        if (castFallbackCoroutine != null)
        {
            StopCoroutine(castFallbackCoroutine);
            castFallbackCoroutine = null;
        }

        // If cast animation ended without triggering the projectile event, fire the pending cast now
        if (skillController != null && skillController.HasPendingCast)
        {
            skillController.ExecutePendingCast();
        }

        IsCastingSkill = false;
        if (animController != null)
        {
            animController.UpdateCastSkill(false);
        }

        // Determine recovery delay
        float recoveryDelay = customRecoveryDelay;
        if (recoveryDelay < 0f)
        {
            if (skillController != null && skillController.LastExecutedSkillData != null && skillController.LastExecutedSkillData.useCustomRecoveryDuration)
            {
                recoveryDelay = skillController.LastExecutedSkillData.postCastRecoveryDuration;
            }
            else if (statsManager != null && statsManager.BaseStats != null)
            {
                recoveryDelay = statsManager.BaseStats.DefaultPostCastRecovery;
            }
            else
            {
                recoveryDelay = 0.3f;
            }
        }

        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }

        // Enforce basic attack cooldown after skill: Recovery Duration + AttackTimeInterval
        float baseAttackInterval = statsManager != null ? statsManager.AttackTimeInterval.Value : 1.0f;
        float totalPostSkillAttackCooldown = recoveryDelay + baseAttackInterval;
        if (statsManager != null)
        {
            statsManager.StartAttackTimer(totalPostSkillAttackCooldown);
        }

        if (recoveryDelay > 0.001f)
        {
            IsRecovering = true;
            CanMove = true;
            recoveryCoroutine = StartCoroutine(RecoveryDelayCoroutine(recoveryDelay));
        }
        else
        {
            IsRecovering = false;
            CanMove = true;
        }
    }

    private IEnumerator RecoveryDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        IsRecovering = false;
        CanMove = true;
        recoveryCoroutine = null;
    }

    /// <summary>
    /// Immediately interrupts all ongoing casting, attacking, and recovery actions.
    /// Used by stuns, knockback, death, or manual cancellations.
    /// </summary>
    public void InterruptAllActions()
    {
        if (castFallbackCoroutine != null)
        {
            StopCoroutine(castFallbackCoroutine);
            castFallbackCoroutine = null;
        }

        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }

        IsAttacking = false;
        IsCastingSkill = false;
        IsRecovering = false;
        IsDashing = false;
        CanMove = true;

        if (skillController != null)
        {
            skillController.CancelPendingCast();
        }

        if (animController != null)
        {
            animController.UpdateAttack(false);
            animController.UpdateCastSkill(false);
        }

        var throttler = GetComponentInChildren<AIDecisionThrottler>();
        if (throttler != null)
        {
            throttler.InterruptDecisionLock();
        }
    }

    private void DelayAction(float delay, System.Action action)
    {
        StartCoroutine(DelayActionCoroutine(delay, action));
    }

    // Helper method
    private IEnumerator DelayActionCoroutine(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }




    /// <summary>
    /// Sets the runtime control mode and activates or deactivates the AI augment accordingly.
    /// </summary>
    public void SetControlMode(ControlMode mode)
    {
        controlMode = mode;
        if (aiAugment != null)
        {
            AIActivation(mode == ControlMode.AI);
        }
    }

#if UNITY_EDITOR

    public void PlayerControl()
    {
        SetControlMode(ControlMode.Player);
    }

    public void AgentControl()
    {
        SetControlMode(ControlMode.AI);
    }

    public void ToggleAI()
    {
        if (aiAugment != null) AIActivation(!aiAugment.activeSelf);
    }
#endif
}
