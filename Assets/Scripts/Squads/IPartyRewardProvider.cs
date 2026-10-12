using System;
using UnityEngine;

/// <summary>
/// Abstraction for evaluating party loot/reward state at leader checkpoints.
/// Stand-in implementation is used in Phase 4; the real loot system will implement this interface later
/// without modifying checkpoint decision logic.
/// </summary>
public interface IPartyRewardProvider
{
    /// <summary>
    /// Gold-equivalent value this individual member would lose on death.
    /// </summary>
    float GetCarriedValue(SquadMember member);

    /// <summary>
    /// Total gold-equivalent value the party is currently carrying and would lose on a wipe,
    /// or bank upon retreating.
    /// </summary>
    float GetPartyCarriedValue(PartyLeader party);

    /// <summary>
    /// Gold-equivalent value of loot sources the party already knows about ahead and has not collected.
    /// </summary>
    float GetKnownPotentialAhead(PartyLeader party);

    /// <summary>
    /// Rough gold-equivalent expectation for unseen rewards per unexplored room.
    /// </summary>
    float GetUnknownExpectationPerRoom(PartyLeader party);
}

/// <summary>
/// Serializable stand-in implementation of <see cref="IPartyRewardProvider"/> with configurable values
/// and an optional room counter. Zero allocations per query.
/// </summary>
[Serializable]
public class StandInPartyRewardProvider : IPartyRewardProvider
{
    [Tooltip("Gold-equivalent value carried per living member when explicitPartyCarriedValue is negative.")]
    [SerializeField] private float carriedValuePerMember = 20f;

    [Tooltip("If >= 0, overrides the sum of per-member carried values for the party.")]
    [SerializeField] private float explicitPartyCarriedValue = -1f;

    [Tooltip("Gold-equivalent value of uncollected loot sources the party knows about ahead.")]
    [SerializeField] private float knownPotentialAhead = 50f;

    [Tooltip("Rough expected gold-equivalent reward per unseen room.")]
    [SerializeField] private float unknownExpectationPerRoom = 40f;

    [Tooltip("Stand-in counter of rooms cleared, optionally adding carried value per room.")]
    [SerializeField] private int roomsCleared = 0;

    [Tooltip("Additional party carried value accumulated per cleared room in stand-in mode.")]
    [SerializeField] private float carriedValuePerClearedRoom = 25f;

    public float CarriedValuePerMember
    {
        get => carriedValuePerMember;
        set => carriedValuePerMember = Mathf.Max(0f, value);
    }

    public float ExplicitPartyCarriedValue
    {
        get => explicitPartyCarriedValue;
        set => explicitPartyCarriedValue = value;
    }

    public float KnownPotentialAhead
    {
        get => knownPotentialAhead;
        set => knownPotentialAhead = Mathf.Max(0f, value);
    }

    public float UnknownExpectationPerRoom
    {
        get => unknownExpectationPerRoom;
        set => unknownExpectationPerRoom = Mathf.Max(0f, value);
    }

    public int RoomsCleared
    {
        get => roomsCleared;
        set => roomsCleared = Mathf.Max(0, value);
    }

    public float CarriedValuePerClearedRoom
    {
        get => carriedValuePerClearedRoom;
        set => carriedValuePerClearedRoom = Mathf.Max(0f, value);
    }

    public StandInPartyRewardProvider() { }

    public StandInPartyRewardProvider(
        float carriedPerMember,
        float knownAhead,
        float unknownPerRoom,
        float explicitPartyCarried = -1f,
        int clearedRooms = 0,
        float valuePerClearedRoom = 25f)
    {
        carriedValuePerMember = Mathf.Max(0f, carriedPerMember);
        knownPotentialAhead = Mathf.Max(0f, knownAhead);
        unknownExpectationPerRoom = Mathf.Max(0f, unknownPerRoom);
        explicitPartyCarriedValue = explicitPartyCarried;
        roomsCleared = Mathf.Max(0, clearedRooms);
        carriedValuePerClearedRoom = Mathf.Max(0f, valuePerClearedRoom);
    }

    public float GetCarriedValue(SquadMember member)
    {
        if (member == null || !member.IsAlive) return 0f;
        return carriedValuePerMember;
    }

    public float GetPartyCarriedValue(PartyLeader party)
    {
        if (explicitPartyCarriedValue >= 0f)
        {
            return explicitPartyCarriedValue + (roomsCleared * carriedValuePerClearedRoom);
        }

        if (party == null)
        {
            return roomsCleared * carriedValuePerClearedRoom;
        }

        float total = party.IsAlive ? carriedValuePerMember : 0f;
        var members = party.Members;
        if (members != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                total += GetCarriedValue(members[i]);
            }
        }

        total += roomsCleared * carriedValuePerClearedRoom;
        return total;
    }

    public float GetKnownPotentialAhead(PartyLeader party)
    {
        return knownPotentialAhead;
    }

    public float GetUnknownExpectationPerRoom(PartyLeader party)
    {
        return unknownExpectationPerRoom;
    }
}
