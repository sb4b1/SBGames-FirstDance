using UnityEngine;
using SB4b1.ComponentUtils;
using SB4b1.Logging;
using ArtificeToolkit.Attributes;

public class CharacterBrain : MonoBehaviour
{
    // =========================
    // Control
    // =========================
    [SerializeField] private bool isPlayerControlled;

    [SerializeField] private GameObject aiAugment;

    public bool IsPlayerControlled => isPlayerControlled;
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


    private void Update()
    {
        if (isPlayerControlled) return;
        if (HasAI == false) return;
        if (aiAugment.activeSelf == false) AIActivation(true);
    }

    // =========================
    // Control API
    // =========================

    public void AIActivation(bool value)
    {
        aiAugment.SetActive(value);


        ELogger.Log(this, nameof(AIActivation), value ? "Activate AI" : "Deactivate AI");
    }

    [Button]
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

    [Button]
    private void PlayerControl()
    {
        isPlayerControlled = true;
        if (aiAugment != null) AIActivation(false);
    }

    [Button]
    private void BotControl()
    {
        isPlayerControlled = false;
        if (aiAugment != null) AIActivation(true);
    }
#endif
}
