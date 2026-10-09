using UnityEngine;

public interface IFactionMember
{
    FactionId Faction { get; }
    GameObject GameObject { get; }
    bool IsHostileTo(IFactionMember other);
    bool IsAllyOf(IFactionMember other);
}
