using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct FunctionFormationSlot
{
    [SerializeField] private AdventurerFunction function;

    [Tooltip("Local offset (X = lateral right, Y = forward along leader heading) while travelling out of combat.")]
    [SerializeField] private Vector2 travelOffset;

    [Tooltip("Local offset (X = lateral right, Y = forward along leader heading) when combat starts.")]
    [SerializeField] private Vector2 combatOffset;

    public AdventurerFunction Function => function;
    public Vector2 TravelOffset => travelOffset;
    public Vector2 CombatOffset => combatOffset;

    public FunctionFormationSlot(AdventurerFunction function, Vector2 travelOffset, Vector2 combatOffset)
    {
        this.function = function;
        this.travelOffset = travelOffset;
        this.combatOffset = combatOffset;
    }
}

/// <summary>
/// Data-driven formation slot offsets per <see cref="AdventurerFunction"/>.
/// Out of combat marching order: Scout ahead of Tank, then Tank, DPS (and None), Healer at the rear.
/// In combat: Scout falls back to the side/rear.
/// </summary>
[CreateAssetMenu(fileName = "SquadFormationConfig", menuName = "SBGames/AI/Squad Formation Config")]
public class SquadFormationConfigSO : ScriptableObject
{
    [SerializeField] private List<FunctionFormationSlot> slots = new()
    {
        new FunctionFormationSlot(AdventurerFunction.Scout,  travelOffset: new Vector2(0f,  2.5f), combatOffset: new Vector2(1.8f, -1.5f)),
        new FunctionFormationSlot(AdventurerFunction.Tank,   travelOffset: new Vector2(0f,  1.0f), combatOffset: new Vector2(0f,    1.2f)),
        new FunctionFormationSlot(AdventurerFunction.DPS,    travelOffset: new Vector2(0f, -1.0f), combatOffset: new Vector2(0f,   -1.0f)),
        new FunctionFormationSlot(AdventurerFunction.Healer, travelOffset: new Vector2(0f, -2.5f), combatOffset: new Vector2(0f,   -2.5f))
    };

    [SerializeField] private FunctionFormationSlot defaultSlot =
        new FunctionFormationSlot(AdventurerFunction.DPS, travelOffset: new Vector2(0f, -1.0f), combatOffset: new Vector2(0f, -1.0f));

    public IReadOnlyList<FunctionFormationSlot> Slots => slots;

    public static FunctionFormationSlot[] CreateDefaultSlotsArray()
    {
        return new[]
        {
            new FunctionFormationSlot(AdventurerFunction.Scout,  travelOffset: new Vector2(0f,  2.5f), combatOffset: new Vector2(1.8f, -1.5f)),
            new FunctionFormationSlot(AdventurerFunction.Tank,   travelOffset: new Vector2(0f,  1.0f), combatOffset: new Vector2(0f,    1.2f)),
            new FunctionFormationSlot(AdventurerFunction.DPS,    travelOffset: new Vector2(0f, -1.0f), combatOffset: new Vector2(0f,   -1.0f)),
            new FunctionFormationSlot(AdventurerFunction.Healer, travelOffset: new Vector2(0f, -2.5f), combatOffset: new Vector2(0f,   -2.5f))
        };
    }

    public Vector2 GetSlotOffset(AdventurerFunction function, bool inCombat)
    {
        AdventurerFunction effective = function != AdventurerFunction.None ? function : AdventurerFunction.DPS;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Function == effective)
                {
                    return inCombat ? slots[i].CombatOffset : slots[i].TravelOffset;
                }
            }
        }

        return inCombat ? defaultSlot.CombatOffset : defaultSlot.TravelOffset;
    }
}
