using System.Collections.Generic;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [SerializeField]
    public Dictionary<StatType, Stat> Stats = new();

    public Stat GetStat(StatType type)
    {
        return Stats[type];
    }

    public Stat GetStatByModifier(StatModifier modifier)
    {
        foreach (var stat in Stats.Values)
        {
            if (stat.Modifiers.Contains(modifier))
                return stat;
        }
        return null;
    }
}
