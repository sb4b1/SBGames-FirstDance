using UnityEngine;
using SB4b1.ComponentUtils;
using SB4b1.Logging;
using ArtificeToolkit.Attributes;
using System;
using System.Runtime.InteropServices;

public class CharacterBrain : MonoBehaviour
{
    // =========================
    // Control
    // =========================

    [SerializeField] private ControlMode controlMode;
    [SerializeField] private AgentStatus agentStatus;

    public ControlMode ControlMode => controlMode;
    public AgentStatus AgentStatus => agentStatus;

    [SerializeField] private GameObject aiAugment;

    // =========================
    // State
    // =========================
    public bool CanMove { get; private set; } = true;
    public bool CanAttack { get; private set; } = true;
    public bool IsDashing { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool HasAI { get; private set; }

    private void Awake()
    {
        aiAugment = ComponentUtil.GetChildGameObjectByTag(this.gameObject, "AI_Augment");
        HasAI = aiAugment != null;
    }

    private void Start()
    {

        if (controlMode == ControlMode.Player)
        {
            AIActivation(false);
        }
        else if (controlMode == ControlMode.AI)
        {
            AIActivation(true);
        }

    }

    // =========================
    // Control API
    // =========================

    public void AIActivation(bool value)
    {
        aiAugment.SetActive(value);

        agentStatus = value ? AgentStatus.Active : AgentStatus.Inactive;
        ELogger.Log(this, nameof(AIActivation), value ? "Activate AI" : "Deactivate AI");
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

        IsAttacking = true;
        CanMove = false;
        return true;
    }

    public void EndAttack()
    {
        IsAttacking = false;
        CanMove = true;
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
        if (!HasAI) return;
        AIActivation(!aiAugment.activeSelf);
    }
#endif
}
