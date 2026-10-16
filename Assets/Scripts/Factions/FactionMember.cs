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
    /// Resolves from parents when the detected object is a child collider.
    /// </summary>
    public bool IsHostileTo(GameObject otherObj)
    {
        if (otherObj == null) return false;

        var otherMember = otherObj.GetComponentInParent<IFactionMember>();
        if (otherMember != null)
        {
            return IsHostileTo(otherMember);
        }

        // Tag fallbacks (checking otherObj, CharacterBrain, and root)
        string otherTag = otherObj.tag;
        if (otherTag == "Untagged")
        {
            var otherBrain = otherObj.GetComponentInParent<CharacterBrain>();
            if (otherBrain != null && otherBrain.tag != "Untagged")
            {
                otherTag = otherBrain.tag;
            }
            else if (otherObj.transform.parent != null)
            {
                otherTag = otherObj.transform.root.tag;
            }
        }

        string myTag = tag;
        if (myTag == "Untagged")
        {
            var myBrain = GetComponentInParent<CharacterBrain>();
            if (myBrain != null && myBrain.tag != "Untagged")
            {
                myTag = myBrain.tag;
            }
            else if (transform.parent != null)
            {
                myTag = transform.root.tag;
            }
        }

        if (faction == FactionId.Adventurer && otherTag == "Monster") return true;
        if (faction == FactionId.Monster && otherTag == "Adventurer") return true;
        if (faction == FactionId.Faction_01 && otherTag == "Faction_02") return true;
        if (faction == FactionId.Faction_02 && otherTag == "Faction_01") return true;

        if (myTag == "Adventurer" && otherTag == "Monster") return true;
        if (myTag == "Monster" && otherTag == "Adventurer") return true;

        return false;
    }

    /// <summary>
    /// Utility check for an arbitrary GameObject, prioritizing IFactionMember with tag fallback.
    /// Resolves from parents when the detected object is a child collider.
    /// </summary>
    public bool IsAllyOf(GameObject otherObj)
    {
        if (otherObj == null) return false;

        var otherMember = otherObj.GetComponentInParent<IFactionMember>();
        if (otherMember != null)
        {
            return IsAllyOf(otherMember);
        }

        // Tag fallbacks (checking otherObj, CharacterBrain, and root)
        string otherTag = otherObj.tag;
        if (otherTag == "Untagged")
        {
            var otherBrain = otherObj.GetComponentInParent<CharacterBrain>();
            if (otherBrain != null && otherBrain.tag != "Untagged")
            {
                otherTag = otherBrain.tag;
            }
            else if (otherObj.transform.parent != null)
            {
                otherTag = otherObj.transform.root.tag;
            }
        }

        string myTag = tag;
        if (myTag == "Untagged")
        {
            var myBrain = GetComponentInParent<CharacterBrain>();
            if (myBrain != null && myBrain.tag != "Untagged")
            {
                myTag = myBrain.tag;
            }
            else if (transform.parent != null)
            {
                myTag = transform.root.tag;
            }
        }

        if (faction == FactionId.Adventurer && otherTag == "Adventurer") return true;
        if (faction == FactionId.Monster && otherTag == "Monster") return true;
        if (faction == FactionId.Faction_01 && otherTag == "Faction_01") return true;
        if (faction == FactionId.Faction_02 && otherTag == "Faction_02") return true;

        if (myTag == "Adventurer" && otherTag == "Adventurer") return true;
        if (myTag == "Monster" && otherTag == "Monster") return true;

        return false;
    }
}
