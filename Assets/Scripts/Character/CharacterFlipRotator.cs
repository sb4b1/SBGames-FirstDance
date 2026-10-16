using ArtificeToolkit.Attributes;
using UnityEngine;

/// <summary>
/// Rotates a sensor transform when the character sprite flips.
/// Supports both SpriteRenderer.flipX and negative X scale flips.
/// Choose which axis to rotate (Z for 2D, Y for 3D).
/// </summary>
[DisallowMultipleComponent]
public class CharacterFlipRotator : MonoBehaviour
{
    [SerializeField]
    private Transform rootTransform;

    [Header("Flip Settings")]
    public Axis flipAxis = Axis.X; // 2D default

    public enum Axis { X, Y, Z }
    private bool naturalFacesRight;
    private bool facingResolved;
    private Transform sensorTransform;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (rootTransform == null)
            rootTransform = GetComponentInParent<CharacterBrain>()?.transform ?? transform.root;

        if (sensorTransform == null && rootTransform != null)
        {
            sensorTransform = rootTransform.Find("Sensor");
            if (sensorTransform == null)
            {
                var vision = rootTransform.GetComponentInChildren<VisionController>();
                if (vision != null) sensorTransform = vision.transform;
            }
        }

        if (rootTransform != null && !facingResolved)
        {
            facingResolved = true;

            var combat = rootTransform.GetComponent<CharacterCombatController>();
            Transform attackPoint = combat != null ? combat.AttackPoint : null;
            if (attackPoint == null)
            {
                var gizmo = rootTransform.GetComponentInChildren<AttackPointGizmo>();
                if (gizmo != null) attackPoint = gizmo.transform;
            }

            // If AttackPoint is authored at localPosition.x > 0, positive scale.x faces Right (+X).
            // If AttackPoint is authored at localPosition.x < 0 (e.g. SPUM Adventurer), positive scale.x faces Left (-X).
            if (attackPoint != null && Mathf.Abs(attackPoint.localPosition.x) > 0.001f)
            {
                naturalFacesRight = attackPoint.localPosition.x > 0f;
            }
            else
            {
                naturalFacesRight = false;
            }
        }
    }

    public void ApplyFlip(bool faceRight)
    {
        ResolveReferences();
        if (rootTransform == null) return;

        Vector3 scale = rootTransform.localScale;
        float sign = (faceRight == naturalFacesRight) ? 1f : -1f;

        switch (flipAxis)
        {
            case Axis.X:
                float magX = Mathf.Abs(scale.x);
                if (Mathf.Approximately(magX, 0f)) magX = 1f;
                scale.x = magX * sign;
                break;
            case Axis.Y:
                float magY = Mathf.Abs(scale.y);
                if (Mathf.Approximately(magY, 0f)) magY = 1f;
                scale.y = magY * sign;
                break;
            case Axis.Z:
                float magZ = Mathf.Abs(scale.z);
                if (Mathf.Approximately(magZ, 0f)) magZ = 1f;
                scale.z = magZ * sign;
                break;
        }

        rootTransform.localScale = scale;
        if (rootTransform.localRotation != Quaternion.identity)
        {
            rootTransform.localRotation = Quaternion.identity;
        }

        // Keep Sensor transform world rotation aligned with 2D facing direction (SensorToolkit LOSSensor2D uses transform.up).
        // Using world rotation (sensor.rotation) ensures sensor.up always points along +/- Vector3.right
        // regardless of whether the parent rootTransform has positive or negative localScale.x.
        if (sensorTransform != null)
        {
            sensorTransform.rotation = Quaternion.Euler(0f, 0f, faceRight ? -90f : 90f);
        }
    }

#if UNITY_EDITOR
    [Button]
    private void ToggleFlip()
    {
        ResolveReferences();
        if (rootTransform == null) return;

        bool isCurrentlyPositive = flipAxis switch
        {
            Axis.X => rootTransform.localScale.x > 0f,
            Axis.Y => rootTransform.localScale.y > 0f,
            Axis.Z => rootTransform.localScale.z > 0f,
            _ => true
        };

        bool currentlyFacingRight = isCurrentlyPositive ? naturalFacesRight : !naturalFacesRight;
        ApplyFlip(!currentlyFacingRight);
    }
#endif
}
