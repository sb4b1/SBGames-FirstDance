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
    private void Awake()
    {
        if (rootTransform == null)
            rootTransform = transform.root;
    }

    private void Start()
    {
        if (rootTransform == null)
            rootTransform = transform.root;
    }

    public void ApplyFlip(bool flipped)
    {
        if (rootTransform == null)
            rootTransform = transform.root;

        if (rootTransform == null) return;

        Vector3 scale = rootTransform.localScale;
        float sign = flipped ? -1f : 1f;

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
    }

#if UNITY_EDITOR
    [Button]
    private void ToggleFlip()
    {
        if (rootTransform == null)
            rootTransform = transform.root;

        if (rootTransform == null) return;

        bool isCurrentlyPositive = flipAxis switch
        {
            Axis.X => rootTransform.localScale.x > 0f,
            Axis.Y => rootTransform.localScale.y > 0f,
            Axis.Z => rootTransform.localScale.z > 0f,
            _ => true
        };

        ApplyFlip(isCurrentlyPositive);
    }
#endif
}
