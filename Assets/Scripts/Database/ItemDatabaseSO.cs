using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Inventory.Core;
using UnityEngine;

namespace SBGames.Database
{
    /// <summary>
    /// ScriptableObject database containing all ItemDefinitions (Items, Runes, Artifacts, Consumables).
    /// Provides fast O(1) runtime lookups and category queries.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "SBGames/Database/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [Header("Artifacts")]
        [SerializeField] private List<ItemDefinition> artifacts = new();

        [Header("Runes")]
        [SerializeField] private List<ItemDefinition> runes = new();

        [Header("Consumables")]
        [SerializeField] private List<ItemDefinition> consumables = new();

        [Header("Other Items")]
        [SerializeField] private List<ItemDefinition> otherItems = new();

        [HideInInspector]
        [SerializeField] private List<ItemDefinition> items = new();

        private Dictionary<string, ItemDefinition> _lookup;

        public IReadOnlyList<ItemDefinition> Artifacts => artifacts;
        public IReadOnlyList<ItemDefinition> Runes => runes;
        public IReadOnlyList<ItemDefinition> Consumables => consumables;
        public IReadOnlyList<ItemDefinition> OtherItems => otherItems;

        public IReadOnlyList<ItemDefinition> Items
        {
            get
            {
                if (items == null || items.Count == 0)
                {
                    RebuildCombinedItemList();
                }
                return items;
            }
        }

        private void OnValidate()
        {
            // If legacy items exist and category lists are empty, distribute them
            if (items != null && items.Count > 0 &&
                (artifacts == null || artifacts.Count == 0) &&
                (runes == null || runes.Count == 0) &&
                (consumables == null || consumables.Count == 0) &&
                (otherItems == null || otherItems.Count == 0))
            {
                SetItemsEditor(new List<ItemDefinition>(items));
            }
            else
            {
                RebuildCombinedItemList();
            }
        }

        private void RebuildCombinedItemList()
        {
            if (items == null) items = new List<ItemDefinition>();
            else items.Clear();

            if (artifacts != null) items.AddRange(artifacts.Where(i => i != null));
            if (runes != null) items.AddRange(runes.Where(i => i != null));
            if (consumables != null) items.AddRange(consumables.Where(i => i != null));
            if (otherItems != null) items.AddRange(otherItems.Where(i => i != null));
        }

        /// <summary>
        /// Initializes runtime dictionary for O(1) lookups.
        /// </summary>
        public void Initialize()
        {
            _lookup = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            RebuildCombinedItemList();

            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                string id = item.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"[ItemDatabaseSO] Item at index {i} has an empty ID.", this);
                    continue;
                }

                if (_lookup.ContainsKey(id))
                {
                    Debug.LogWarning($"[ItemDatabaseSO] Duplicate item ID '{id}' found in database. Overwriting previous entry.", this);
                }

                _lookup[id] = item;
            }
        }

        private void EnsureInitialized()
        {
            if (_lookup == null)
            {
                Initialize();
            }
        }

        public ItemDefinition Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureInitialized();
            return _lookup.GetValueOrDefault(id);
        }

        public bool TryGet(string id, out ItemDefinition item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureInitialized();
            return _lookup.TryGetValue(id, out item);
        }

        public IEnumerable<ItemDefinition> GetByType(ItemType type)
        {
            return type switch
            {
                ItemType.Artifact => artifacts ?? Enumerable.Empty<ItemDefinition>(),
                ItemType.Rune => runes ?? Enumerable.Empty<ItemDefinition>(),
                ItemType.Consumable => consumables ?? Enumerable.Empty<ItemDefinition>(),
                _ => (otherItems ?? Enumerable.Empty<ItemDefinition>()).Where(i => i != null && i.ItemType == type)
            };
        }

        public IEnumerable<ItemDefinition> GetRunes() => runes ?? Enumerable.Empty<ItemDefinition>();
        public IEnumerable<ItemDefinition> GetArtifacts() => artifacts ?? Enumerable.Empty<ItemDefinition>();
        public IEnumerable<ItemDefinition> GetConsumables() => consumables ?? Enumerable.Empty<ItemDefinition>();

        /// <summary>
        /// Editor-only assignment method for auto-indexing.
        /// </summary>
        public void SetItemsEditor(List<ItemDefinition> newItems)
        {
            artifacts = new List<ItemDefinition>();
            runes = new List<ItemDefinition>();
            consumables = new List<ItemDefinition>();
            otherItems = new List<ItemDefinition>();

            if (newItems != null)
            {
                foreach (var item in newItems)
                {
                    if (item == null) continue;
                    switch (item.ItemType)
                    {
                        case ItemType.Artifact:
                            artifacts.Add(item);
                            break;
                        case ItemType.Rune:
                            runes.Add(item);
                            break;
                        case ItemType.Consumable:
                            consumables.Add(item);
                            break;
                        default:
                            otherItems.Add(item);
                            break;
                    }
                }
            }

            RebuildCombinedItemList();
        }
    }
}
