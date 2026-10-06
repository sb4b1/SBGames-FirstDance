using UnityEngine;
using SB4b1.Logging;
using System.Collections.Generic;
using UnityEngine.AI;
using ArtificeToolkit.Attributes;

[RequireComponent(typeof(CharacterFlipRotator))]
public class AnimationController : AnimationObjectController
{
    [Header("Character References")]
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterMovementController movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterFlipRotator flipRotator;

    private static readonly int HASH_MOVE_X = Animator.StringToHash("MoveX");
    private static readonly int HASH_MOVE_Y = Animator.StringToHash("MoveY");
    private static readonly int HASH_SPEED  = Animator.StringToHash("Speed");
    private static readonly int HASH_ATTACK = Animator.StringToHash("IsAttacking");
    private static readonly int HASH_CASTSkill = Animator.StringToHash("IsCastingSkill");

    private Vector2 lastMoveDir = Vector2.down;

    protected override void Awake()
    {
        base.Awake();

        if (flipRotator == null) flipRotator = GetComponent<CharacterFlipRotator>();
        if (movement == null) movement = GetComponentInParent<CharacterMovementController>();
        if (brain == null) brain = GetComponentInParent<CharacterBrain>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
    }

    protected virtual void Update()
    {
        UpdateMovement();
        UpdateFlip();
    }

    protected override Transform GetSortingPositionSource()
    {
        if (movement != null) return movement.transform;
        if (agent != null) return agent.transform;
        return base.GetSortingPositionSource();
    }

    protected virtual void UpdateMovement()
    {
        Vector2 velocity = brain != null && brain.ControlMode == ControlMode.Player && movement != null ? movement.MoveDirection :
                           (agent != null ? (Vector2)agent.velocity : Vector2.zero);

        if (velocity.sqrMagnitude > 0.01f)
            lastMoveDir = velocity.normalized;

        if (animator != null)
        {
            animator.SetFloat(HASH_MOVE_X, lastMoveDir.x);
            animator.SetFloat(HASH_MOVE_Y, lastMoveDir.y);
            animator.SetFloat(HASH_SPEED, Mathf.Clamp01(velocity.magnitude));
        }
    }

    public override void UpdateAttack(bool isAttacking)
    {
        if (animator != null) animator.SetBool(HASH_ATTACK, isAttacking);
    }

    public override void UpdateCastSkill(bool isCasting)
    {
        if (animator != null) animator.SetBool(HASH_CASTSkill, isCasting);
    }

    protected virtual void UpdateFlip()
    {
        if (flipRotator == null) return;
        if (lastMoveDir.x > 0.1f) flipRotator.ApplyFlip(false);
        else if (lastMoveDir.x < -0.1f) flipRotator.ApplyFlip(true);
    }

    // =========================
    // Animation Events
    // =========================
    public void OnAttackAnimationEnd()
    {
        if (brain != null) brain.EndAttack();
    }

    public void OnCastAnimationEnd()
    {
        if (brain != null) brain.EndCastSkill();
    }

    // Generate parameters for animator
#if UNITY_EDITOR
    [Button]
    private void Validate()
    {
        List<string> names = new()
        {
            "Speed",
            "MoveX",
            "MoveY",
            "IdleUp",
            "IdleDown",
            "IdleRight",
            "IdleLeft",
            "IsWalking",
            "IsAttacking",
            "IsCastingSkill"
        };

        animator = GetComponent<Animator>();
        if (animator == null) return;

        HashSet<string> actualNames = new();
        foreach (var parameter in animator.parameters)
        {
            actualNames.Add(parameter.name);
            if (!names.Contains(parameter.name) && parameter.name != "GetHit")
                ELogger.Warning(this, $"Unexpected Animator parameter: {parameter.name}");
        }

        int missingCount = 0;
        foreach (string name in names)
        {
            if (actualNames.Contains(name)) continue;

            ELogger.Warning(this, $"Missing Animator parameter: {name}");
            missingCount++;
        }

        if (missingCount == 0)
            ELogger.Log(this, "All required Animator parameters are present.");
    }
#endif
}
