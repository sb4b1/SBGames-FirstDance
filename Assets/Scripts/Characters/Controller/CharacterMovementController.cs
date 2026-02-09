using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterBrain))]
[RequireComponent(typeof(CharacterStatsManager))]
public class CharacterMovementController : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private CharacterBrain brain;
    [SerializeField] private CharacterStatsManager stats;

    private InputAction moveAction;
    private Vector2 moveInput;

    public Vector2 MoveDirection => moveInput;

    private void Awake()
    {
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void OnEnable()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        moveAction.Enable();
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void ReadInput()
    {
        if (brain.ControlMode != ControlMode.Player || brain.IsDashing)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = moveAction.ReadValue<Vector2>().normalized;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (brain.TryStartDash())
                StartCoroutine(DashRoutine());
        }
    }

    private void ApplyMovement()
    {
        if (!brain.CanMove) return;

        rb.linearVelocity = moveInput * stats.Movespeed.Value;
    }

    private IEnumerator DashRoutine()
    {
        rb.linearVelocity = moveInput * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero;
        brain.EndDash();

        yield return new WaitForSeconds(dashCooldown);
    }
}
