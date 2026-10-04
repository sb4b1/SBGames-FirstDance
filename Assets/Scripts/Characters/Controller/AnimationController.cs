using UnityEngine;
using SB4b1.Logging;
using System.Collections.Generic;
using UnityEngine.AI;
using ArtificeToolkit.Attributes;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
[RequireComponent(typeof(CharacterFlipRotator))]
public class AnimationController : MonoBehaviour
{
    private const float SortingOrdersPerWorldUnit = 100f;
    private const string CharacterSortingLayer = "Character";

    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer sprite;
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

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (flipRotator == null) flipRotator = GetComponent<CharacterFlipRotator>();
        if (movement == null) movement = GetComponentInParent<CharacterMovementController>();
        if (brain == null) brain = GetComponentInParent<CharacterBrain>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
    }

    private void Update()
    {
        UpdateMovement();
        UpdateFlip();
    }

    private void LateUpdate()
    {
        if (sprite == null || sprite.sortingLayerName != CharacterSortingLayer) return;

        Transform positionSource = movement != null ? movement.transform :
            agent != null ? agent.transform : transform;
        sprite.sortingOrder = Mathf.RoundToInt(-positionSource.position.y * SortingOrdersPerWorldUnit);
    }

    private void UpdateMovement()
    {
        Vector2 velocity = brain.ControlMode == ControlMode.Player ? movement.MoveDirection:
                           (agent != null ? (Vector2)agent.velocity : Vector2.zero);

        if (velocity.sqrMagnitude > 0.01f)
            lastMoveDir = velocity.normalized;

        animator.SetFloat(HASH_MOVE_X, lastMoveDir.x);
        animator.SetFloat(HASH_MOVE_Y, lastMoveDir.y);
        animator.SetFloat(HASH_SPEED, Mathf.Clamp01(velocity.magnitude));
    }

    public void UpdateAttack(bool isAttacking)
    {
        animator.SetBool(HASH_ATTACK, isAttacking);
    }

    public void UpdateCastSkill(bool isCasting)
    {
        animator.SetBool(HASH_CASTSkill, isCasting);
    }

    private void UpdateFlip()
    {
        if (lastMoveDir.x > 0.1f) flipRotator.ApplyFlip(false);
        else if (lastMoveDir.x < -0.1f) flipRotator.ApplyFlip(true);
    }

    // =========================
    // Animation Events
    // =========================
    public void OnAttackAnimationEnd()
    {
        brain.EndAttack();
    }

    public void OnCastAnimationEnd()
    {
        brain.EndCastSkill();
    }

// Generate paremeters for animator
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

        HashSet<string> actualNames = new();
        foreach (var parameter in animator.parameters)
        {
            actualNames.Add(parameter.name);
            if(!names.Contains(parameter.name))
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
