using System;
using System.Collections.Generic;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Data;
using UnityEngine;

public enum ConsumableItemCategory
{
    None = 0,
    HealthPotion = 1,
    ManaPotion = 2,
    StatusCure = 3,
    Warp = 4
}

[Serializable]
public struct AdventurerItemKitEntry
{
    [SerializeField] private ItemDataSO item;
    [SerializeField, Min(0)] private int count;

    public ItemDataSO Item => item;
    public int Count => count;

    public AdventurerItemKitEntry(ItemDataSO item, int count)
    {
        this.item = item;
        this.count = Mathf.Max(0, count);
    }
}

/// <summary>
/// Data-driven baseline consumable item kit per adventurer rank.
/// </summary>
[CreateAssetMenu(fileName = "ItemKit_Rank_E", menuName = "SBGames/AI/Adventurer Item Kit")]
public class AdventurerItemKit : ScriptableObject
{
    [SerializeField] private List<AdventurerItemKitEntry> entries = new();

    public IReadOnlyList<AdventurerItemKitEntry> Entries => entries;

    public void SetEntries(IReadOnlyList<AdventurerItemKitEntry> newEntries)
    {
        entries.Clear();
        if (newEntries == null) return;
        for (int i = 0; i < newEntries.Count; i++)
        {
            entries.Add(newEntries[i]);
        }
    }

    /// <summary>
    /// Classifies a consumable <see cref="ItemDataSO"/> into a <see cref="ConsumableItemCategory"/>
    /// based on its <see cref="ItemDataSO.ConsumableEffect"/> strategy (or fallback ID/name check).
    /// Zero managed allocations.
    /// </summary>
    public static ConsumableItemCategory ClassifyItem(ItemDataSO item)
    {
        if (item == null) return ConsumableItemCategory.None;

        var effect = item.ConsumableEffect;
        if (effect is RestoreHealthEffect) return ConsumableItemCategory.HealthPotion;
        if (effect is RestoreManaEffect) return ConsumableItemCategory.ManaPotion;
        if (effect is CureStatusEffect) return ConsumableItemCategory.StatusCure;
        if (effect is WarpEscapeEffect) return ConsumableItemCategory.Warp;

        string id = item.ItemID;
        if (!string.IsNullOrEmpty(id))
        {
            if (id.IndexOf("HP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("HEALTH", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ConsumableItemCategory.HealthPotion;
            }
            if (id.IndexOf("MANA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("MP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ConsumableItemCategory.ManaPotion;
            }
            if (id.IndexOf("CURE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("STATUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("ANTIDOTE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ConsumableItemCategory.StatusCure;
            }
            if (id.IndexOf("WARP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("ESCAPE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("TELEPORT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ConsumableItemCategory.Warp;
            }
        }

        return ConsumableItemCategory.None;
    }
}
