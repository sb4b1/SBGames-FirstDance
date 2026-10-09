using UnityEngine;

public interface ISquadMember
{
    GameObject GameObject { get; }
    PartyLeader Leader { get; }
    bool HasLeader { get; }
    SquadOrderType CurrentOrder { get; }
    Transform FocusFireTarget { get; }
    Transform LeaderTransform { get; }
    void AssignLeader(PartyLeader newLeader);
    void LeaveSquad();
}
