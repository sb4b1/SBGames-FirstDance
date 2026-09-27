using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInputDebugDisplay : MonoBehaviour
{
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private Vector2 moveInput;
    [SerializeField] private string activeMoveControl;
    [SerializeField] private bool spaceHeld;
    [SerializeField] private bool spacePressedThisFrame;

    private InputAction moveAction;

    private void OnEnable()
    {
        moveAction = InputSystem.actions.FindAction(moveActionName, false);
        if (moveAction == null)
        {
            Debug.LogError($"Input action '{moveActionName}' was not found.", this);
        }
    }

    private void Update()
    {
        if (moveAction != null && moveAction.enabled)
        {
            moveInput = moveAction.ReadValue<Vector2>();
            activeMoveControl = moveAction.activeControl?.path ?? string.Empty;
        }
        else
        {
            moveInput = Vector2.zero;
            activeMoveControl = string.Empty;
        }

        var spaceKey = Keyboard.current?.spaceKey;
        spaceHeld = spaceKey != null && spaceKey.isPressed;
        spacePressedThisFrame = spaceKey != null && spaceKey.wasPressedThisFrame;
    }

    private void OnDisable()
    {
        moveInput = Vector2.zero;
        activeMoveControl = string.Empty;
        spaceHeld = false;
        spacePressedThisFrame = false;
    }
}
