using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CharacterClassStyleEntry
{
    [SerializeField] private CharacterClass characterClass;

    [Tooltip("Preferred standing distance band as a fraction (0.1..1.0) of AttackRange to keep from the target.")]
    [SerializeField, Range(0.1f, 1.0f)] private float preferredRangeBand;

    [Tooltip("Preference (0..1) for casting skills over basic attacks. Tuning only; never restricts which skills can be used.")]
    [SerializeField, Range(0f, 1f)] private float skillReliance;

    public CharacterClass CharacterClass => characterClass;
    public float PreferredRangeBand => Mathf.Clamp(preferredRangeBand, 0.1f, 1.0f);
    public float SkillReliance => Mathf.Clamp01(skillReliance);

    public CharacterClassStyleEntry(CharacterClass characterClass, float preferredRangeBand, float skillReliance)
    {
        this.characterClass = characterClass;
        this.preferredRangeBand = Mathf.Clamp(preferredRangeBand, 0.1f, 1.0f);
        this.skillReliance = Mathf.Clamp01(skillReliance);
    }
}

/// <summary>
/// Data-driven style table mapping <see cref="CharacterClass"/> to combat style defaults
/// (preferred range band and skill reliance) without switch statements.
/// Class labels only describe combat style and never restrict which skills a character can use.
/// </summary>
[CreateAssetMenu(fileName = "CharacterClassStyleTable", menuName = "SBGames/AI/Character Class Style Table")]
public class CharacterClassStyleTableSO : ScriptableObject
{
    [Header("Fallback Style (When Class Not Found)")]
    [SerializeField] private CharacterClassStyleEntry fallbackStyle =
        new CharacterClassStyleEntry(CharacterClass.None, 0.85f, 0.50f);

    [Header("Class Style Entries")]
    [SerializeField] private List<CharacterClassStyleEntry> entries = new()
    {
        new CharacterClassStyleEntry(CharacterClass.None,    preferredRangeBand: 0.85f, skillReliance: 0.50f),
        new CharacterClassStyleEntry(CharacterClass.Warrior, preferredRangeBand: 0.65f, skillReliance: 0.35f),
        new CharacterClassStyleEntry(CharacterClass.Mage,    preferredRangeBand: 0.95f, skillReliance: 0.85f),
        new CharacterClassStyleEntry(CharacterClass.Rogue,   preferredRangeBand: 0.60f, skillReliance: 0.45f),
        new CharacterClassStyleEntry(CharacterClass.Scout,   preferredRangeBand: 0.85f, skillReliance: 0.50f),
        new CharacterClassStyleEntry(CharacterClass.Cleric,  preferredRangeBand: 0.85f, skillReliance: 0.70f)
    };

    public CharacterClassStyleEntry FallbackStyle => fallbackStyle;
    public IReadOnlyList<CharacterClassStyleEntry> Entries => entries;

    /// <summary>
    /// Looks up the style entry for <paramref name="characterClass"/> by table scan (no switch statements, zero allocations).
    /// Returns <see cref="fallbackStyle"/> if not found.
    /// </summary>
    public CharacterClassStyleEntry GetStyle(CharacterClass characterClass)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].CharacterClass == characterClass)
                {
                    return entries[i];
                }
            }
        }

        return fallbackStyle;
    }

    public float GetPreferredRangeBand(CharacterClass characterClass)
    {
        return GetStyle(characterClass).PreferredRangeBand;
    }

    public float GetSkillReliance(CharacterClass characterClass)
    {
        return GetStyle(characterClass).SkillReliance;
    }
}
