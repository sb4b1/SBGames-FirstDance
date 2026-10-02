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
    [SerializeField] private AnimationController animController;

    // =========================
    // State
    // =========================
    public bool CanMove { get; private set; } = true;
    public bool CanAttack => statsManager.AttackReady;


    public bool IsDashing { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsCastingSkill { get; private set; }
    public bool HasAI { get; private set; }

    private void Awake()
    {
        aiAugment = ComponentUtil.GetChildGameObjectByTag(this.gameObject, "AI_Augment");
        HasAI = aiAugment != null;

        if (statsManager == null)
        {
            ELogger.Warning(this, nameof(Awake), "CharacterStatsManager reference is missing! Attempting to find in children...");
            statsManager = GetComponent<CharacterStatsManager>();

            if (statsManager == null)
            {
                ELogger.Error(this, nameof(Awake), "CharacterStatsManager not found Please assign it in the inspector.");
            }
        }

        if (animController == null)
        {
            ELogger.Warning(this, nameof(Awake), "AnimationController reference is missing! Attempting to find in children...");
            animController = GetComponentInChildren<AnimationController>();

            if (animController == null)
            {
                ELogger.Error(this, nameof(Awake), "AnimationController not found Please assign it in the inspector.");
            }
        }
    }

    private void Start()
    {
        OnSpawn();

        
        bool activeAI = aiAugment.activeSelf;
        AIActivation(true);
        DelayAction(0.1f, () => AIActivation(activeAI));
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
        aiAugment.SetActive(value);

        agentStatus = value ? AgentStatus.Active : AgentStatus.Inactive;
        //ELogger.Log(this, nameof(AIActivation), value ? "Activate AI" : "Deactivate AI");
    }

    public void AddAIAugment()
    {
        aiAugment = ComponentUtil.GetChildGameObjectByTag(this.gameObject, "AI_Augment");

        if(aiAugment == null)
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
        if (IsDashing || !CanMove) return false;

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

        statsManager.StartAttackTimer();

        CanMove = false;
        IsAttacking = true;
        animController.UpdateAttack(true);
        return true;
    }

    // =========================
    // Skill
    // =========================
    public bool TryCastSkill()
    {
        if (IsCastingSkill) return false;

        CanMove = false;
        IsCastingSkill = true;
        animController.UpdateCastSkill(true);
        return true;
    }

    public void EndAttack()
    {
        CanMove = true;
        IsAttacking = false;
        animController.UpdateAttack(false);
    }

    public void EndCastSkill()
    {
        CanMove = true;
        IsCastingSkill = false;
        animController.UpdateCastSkill(false);
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




#if UNITY_EDITOR

    public void PlayerControl()
    {
        controlMode = ControlMode.Player;
        if (aiAugment != null) AIActivation(false);
    }

    public void AgentControl()
    {
        controlMode = ControlMode.AI;
        if (aiAugment != null) AIActivation(true);
    }

    public void ToggleAI()
    {
        if (aiAugment != null) AIActivation(!aiAugment.activeSelf);
    }
#endif
}
