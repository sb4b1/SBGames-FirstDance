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
        [SerializeField] private List<ItemDefinition> items = new();

        private Dictionary<string, ItemDefinition> _lookup;

        public IReadOnlyList<ItemDefinition> Items => items;

        /// <summary>
        /// Initializes runtime dictionary for O(1) lookups.
        /// </summary>
        public void Initialize()
        {
            _lookup = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

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
            return items.Where(i => i != null && i.ItemType == type);
        }

        public IEnumerable<ItemDefinition> GetRunes() => GetByType(ItemType.Rune);
        public IEnumerable<ItemDefinition> GetArtifacts() => GetByType(ItemType.Artifact);
        public IEnumerable<ItemDefinition> GetConsumables() => GetByType(ItemType.Consumable);

        /// <summary>
        /// Editor-only assignment method for auto-indexing.
        /// </summary>
        public void SetItemsEditor(List<ItemDefinition> newItems)
        {
            items = newItems ?? new List<ItemDefinition>();
        }
    }
}
