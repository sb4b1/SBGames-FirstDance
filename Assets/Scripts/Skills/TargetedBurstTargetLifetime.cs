using UnityEngine;

/// <summary>Identifies one active lifetime of a target, including deactivate/reactivate in the same frame.</summary>
[DisallowMultipleComponent]
public sealed class TargetedBurstTargetLifetime : MonoBehaviour
{
    public uint Generation { get; private set; }
    private void OnDisable() { unchecked { Generation++; } }
}
