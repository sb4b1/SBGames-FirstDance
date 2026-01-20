using UnityEngine;

/// <summary>
/// Rotates a sensor transform when the character sprite flips.
/// Supports both SpriteRenderer.flipX and negative X scale flips.
/// Choose which axis to rotate (Z for 2D, Y for 3D).
/// </summary>
public class SensorFlipRotator : MonoBehaviour
{
    public Transform sensorTransform;
    public SpriteRenderer spriteRenderer;

    [Header("Flip Detection")]
    public bool useSpriteRendererFlipX = true;

    [Header("Rotation Settings")]
    public Axis rotationAxis = Axis.Z; // 2D default
    public float normalAngle = 0f;
    public float flippedAngle = 180f;

    private bool _lastFlipped;
    private Vector3 _baseEuler;

    public enum Axis { X, Y, Z }

    private void Awake()
    {
        if (sensorTransform == null)
            sensorTransform = transform;

        _baseEuler = sensorTransform.localEulerAngles;
        _lastFlipped = IsFlipped();
        ApplyRotation(_lastFlipped);
    }

    private void Update()
    {
        bool flipped = IsFlipped();
        if (flipped != _lastFlipped)
        {
            _lastFlipped = flipped;
            ApplyRotation(flipped);
        }
    }

    private bool IsFlipped()
    {
        if (useSpriteRendererFlipX && spriteRenderer != null)
            return spriteRenderer.flipX;

        // Fallback: check lossyScale to capture parent negative scaling
        return transform.lossyScale.x < 0f;
    }

    private void ApplyRotation(bool flipped)
    {
        float angle = flipped ? flippedAngle : normalAngle;
        Vector3 euler = _baseEuler;

        switch (rotationAxis)
        {
            case Axis.X:
                euler.x = _baseEuler.x + angle;
                break;
            case Axis.Y:
                euler.y = _baseEuler.y + angle;
                break;
            case Axis.Z:
                euler.z = _baseEuler.z + angle;
                break;
        }

        sensorTransform.localEulerAngles = euler;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sensorTransform == null)
            sensorTransform = transform;
        _baseEuler = sensorTransform.localEulerAngles;
        ApplyRotation(IsFlipped());
    }
#endif
}
