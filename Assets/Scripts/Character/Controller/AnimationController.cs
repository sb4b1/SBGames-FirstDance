using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SB4b1.Logging;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterFlipRotator))]
public class AnimationController : AnimationObjectController
{
    [Header("Character References")]
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterMovementController movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private CharacterFlipRotator flipRotator;




    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");
    private static readonly int HASH_CAST_SKILL = Animator.StringToHash("CastSkill");
    private static readonly int HASH_HIT = Animator.StringToHash("Hit");
    private static readonly int HASH_IS_DEAD = Animator.StringToHash("IsDead");
    private static readonly int HASH_IS_MOVING = Animator.StringToHash("IsMoving");

    //TODO: Deprecated
    private static readonly int HASH_MOVE_X = Animator.StringToHash("MoveX");
    private static readonly int HASH_MOVE_Y = Animator.StringToHash("MoveY");
    private static readonly int HASH_SPEED = Animator.StringToHash("Speed");
    private static readonly int HASH_IsATTACK = Animator.StringToHash("IsAttacking");
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
            if (velocity.sqrMagnitude > 0.01f) animator.SetBool(HASH_IS_MOVING, true);
            else animator.SetBool(HASH_IS_MOVING, false);
        }
    }

    public override void UpdateAttack(bool isAttacking)
    {
        SetBoolOrTrigger(HASH_ATTACK, isAttacking);
        SetBoolOrTrigger(HASH_IsATTACK, isAttacking);
    }

    public override void UpdateCastSkill(bool isCasting)
    {
        SetBoolOrTrigger(HASH_CASTSkill, isCasting);
        SetBoolOrTrigger(HASH_CAST_SKILL, isCasting);
    }

    protected virtual void UpdateFlip()
    {
        if (flipRotator == null) return;

        // 1. If actively engaging a target in combat, continuously face towards target
        Transform target = brain != null && brain.BaseAI != null ? brain.BaseAI.CurrentTarget : null;
        if (target != null)
        {
            float dx = target.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                // SPUM convention: flipped == true gives scale.x < 0 (facing right)
                //                  flipped == false gives scale.x > 0 (facing left)
                flipRotator.ApplyFlip(dx > 0f);
                return;
            }
        }

        // 2. Continuously face movement direction
        if (lastMoveDir.x > 0.05f)
        {
            flipRotator.ApplyFlip(true);
        }
        else if (lastMoveDir.x < -0.05f)
        {
            flipRotator.ApplyFlip(false);
        }
    }

    // =========================
    // Animation Events
    // =========================
    public void OnAttackAnimationEnd()
    {
        if (brain != null) brain.EndAttack();
    }

    public void AnimationEvent_AttackComplete()
    {
        OnAttackAnimationEnd();
    }

    public void OnCastAnimationEnd()
    {
        if (brain != null) brain.EndCastSkill();
    }

    public void AnimationEvent_CastSkillComplete()
    {
        OnCastAnimationEnd();
    }

    // Generate parameters for animator
#if UNITY_EDITOR
    [Button]
    private void Validate()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            ELogger.Warning(this, "Animator component is missing.");
            return;
        }

        UnityEditor.Animations.AnimatorController controller = null;
        if (animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController directAc)
        {
            controller = directAc;
        }
        else if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
        {
            controller = overrideController.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        }

        if (controller == null)
        {
            ELogger.Warning(this, "No valid AnimatorController asset found on Animator.");
            return;
        }

        var requiredParameters = new Dictionary<string, AnimatorControllerParameterType>
        {
            { "Speed", AnimatorControllerParameterType.Float },
            { "MoveX", AnimatorControllerParameterType.Float },
            { "MoveY", AnimatorControllerParameterType.Float },
            { "IdleUp", AnimatorControllerParameterType.Bool },
            { "IdleDown", AnimatorControllerParameterType.Bool },
            { "IdleRight", AnimatorControllerParameterType.Bool },
            { "IdleLeft", AnimatorControllerParameterType.Bool },
            { "IsMoving", AnimatorControllerParameterType.Bool },
            { "IsAttacking", AnimatorControllerParameterType.Bool },
            { "IsCastingSkill", AnimatorControllerParameterType.Bool },
            { "GetHit", AnimatorControllerParameterType.Trigger }
        };

        HashSet<string> actualNames = new();
        foreach (var parameter in controller.parameters)
        {
            actualNames.Add(parameter.name);
            if (!requiredParameters.ContainsKey(parameter.name))
            {
                ELogger.Warning(this, $"Unexpected Animator parameter: {parameter.name}");
            }
        }

        int createdCount = 0;
        UnityEditor.Undo.RecordObject(controller, "Add Missing Animator Parameters");

        foreach (var kvp in requiredParameters)
        {
            if (actualNames.Contains(kvp.Key)) continue;

            controller.AddParameter(kvp.Key, kvp.Value);
            ELogger.Log(this, $"Created missing Animator parameter: {kvp.Key} ({kvp.Value})");
            createdCount++;
        }

        if (createdCount > 0)
        {
            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.AssetDatabase.SaveAssets();
            ELogger.Log(this, $"Successfully added {createdCount} missing Animator parameter(s) to '{controller.name}'.");
        }
        else
        {
            ELogger.Log(this, "All required Animator parameters are present.");
        }
    }
#endif
}
