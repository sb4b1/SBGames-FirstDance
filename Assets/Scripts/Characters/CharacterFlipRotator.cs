using ArtificeToolkit.Attributes;
using UnityEngine;

/// <summary>
/// Rotates a sensor transform when the character sprite flips.
/// Supports both SpriteRenderer.flipX and negative X scale flips.
/// Choose which axis to rotate (Z for 2D, Y for 3D).
/// </summary>
public class CharacterFlipRotator : MonoBehaviour
{
    [SerializeField]
    private Transform rootTransform;

    [Header("Flip Settings")]
    public Axis flipAxis = Axis.X; // 2D default

    public enum Axis { X, Y, Z }
    private void Start()
    {
        if (rootTransform == null)
            rootTransform = transform.root;
    }

    public void ApplyRotation(bool flipped)
    {
        rootTransform.localScale = new Vector3(
            flipAxis == Axis.X ? (flipped ? -1f : 1f) : 1f,
            flipAxis == Axis.Y ? (flipped ? -1f : 1f) : 1f,
            flipAxis == Axis.Z ? (flipped ? -1f : 1f) : 1f
        );
    }

#if UNITY_EDITOR
    [Button]
    private void ToggleFlip()
    {
        ApplyRotation(flipAxis == Axis.X ? rootTransform.localScale.x > 0 :
                      flipAxis == Axis.Y ? rootTransform.localScale.y > 0 :
                      rootTransform.localScale.z > 0);
    }
#endif
}
