using UnityEngine;

[DisallowMultipleComponent]
public class FactionMember : MonoBehaviour, IFactionMember
{
    [SerializeField] private FactionId faction = FactionId.Neutral;

    public FactionId Faction
    {
        get => faction;
        set => faction = value;
    }

    public GameObject GameObject => gameObject;

    public bool IsHostileTo(IFactionMember other)
    {
        if (other == null) return false;
        if (faction == FactionId.Neutral || other.Faction == FactionId.Neutral) return false;
        return faction != other.Faction;
    }

    public bool IsAllyOf(IFactionMember other)
    {
        if (other == null) return false;
        if (faction == FactionId.Neutral || other.Faction == FactionId.Neutral) return false;
        return faction == other.Faction;
    }

    /// <summary>
    /// Utility check for an arbitrary GameObject, prioritizing IFactionMember with tag fallback.
    /// </summary>
    public bool IsHostileTo(GameObject otherObj)
    {
        if (otherObj == null) return false;

        if (otherObj.TryGetComponent<IFactionMember>(out var otherMember))
        {
            return IsHostileTo(otherMember);
        }

        // Tag fallbacks
        if (faction == FactionId.Adventurer && otherObj.CompareTag("Monster")) return true;
        if (faction == FactionId.Monster && otherObj.CompareTag("Adventurer")) return true;
        if (faction == FactionId.Faction_01 && otherObj.CompareTag("Faction_02")) return true;
        if (faction == FactionId.Faction_02 && otherObj.CompareTag("Faction_01")) return true;

        return false;
    }

    /// <summary>
    /// Utility check for an arbitrary GameObject, prioritizing IFactionMember with tag fallback.
    /// </summary>
    public bool IsAllyOf(GameObject otherObj)
    {
        if (otherObj == null) return false;

        if (otherObj.TryGetComponent<IFactionMember>(out var otherMember))
        {
            return IsAllyOf(otherMember);
        }

        // Tag fallbacks
        if (faction == FactionId.Adventurer && otherObj.CompareTag("Adventurer")) return true;
        if (faction == FactionId.Monster && otherObj.CompareTag("Monster")) return true;
        if (faction == FactionId.Faction_01 && otherObj.CompareTag("Faction_01")) return true;
        if (faction == FactionId.Faction_02 && otherObj.CompareTag("Faction_02")) return true;

        return false;
    }
}
