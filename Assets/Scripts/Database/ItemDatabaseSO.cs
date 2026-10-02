using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using UnityEngine;

namespace SBGames.Database
{
    /// <summary>
    /// ScriptableObject database containing all ItemDataSO entries (Items, Runes, Artifacts, Consumables).
    /// Provides fast O(1) runtime lookups and category queries.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "SBGames/Database/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [Header("Artifacts")]
        [SerializeField] private List<ItemDataSO> artifacts = new();

        [Header("Runes")]
        [SerializeField] private List<ItemDataSO> runes = new();

        [Header("Consumables")]
        [SerializeField] private List<ItemDataSO> consumables = new();

        [Header("Other Items")]
        [SerializeField] private List<ItemDataSO> otherItems = new();

        [HideInInspector]
        [SerializeField] private List<ItemDataSO> items = new();

        private Dictionary<string, ItemDataSO> _lookup;

        public IReadOnlyList<ItemDataSO> Artifacts => artifacts;
        public IReadOnlyList<ItemDataSO> Runes => runes;
        public IReadOnlyList<ItemDataSO> Consumables => consumables;
        public IReadOnlyList<ItemDataSO> OtherItems => otherItems;

        public IReadOnlyList<ItemDataSO> Items
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
                SetItemsEditor(new List<ItemDataSO>(items));
            }
            else
            {
                RebuildCombinedItemList();
            }
        }

        private void RebuildCombinedItemList()
        {
            if (items == null) items = new List<ItemDataSO>();
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
            _lookup = new Dictionary<string, ItemDataSO>(StringComparer.OrdinalIgnoreCase);
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

        public ItemDataSO Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureInitialized();
            return _lookup.GetValueOrDefault(id);
        }

        public ItemDataSO GetItem(string id) => Get(id);

        public bool TryGet(string id, out ItemDataSO item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureInitialized();
            return _lookup.TryGetValue(id, out item);
        }

        public bool TryGetItem(string id, out ItemDataSO item) => TryGet(id, out item);

        public bool TryGetItem<T>(string id, out T item) where T : ItemDataSO
        {
            item = Get(id) as T;
            return item != null;
        }

        public IEnumerable<ItemDataSO> GetByType(ItemType type)
        {
            return type switch
            {
                ItemType.Artifact => artifacts ?? Enumerable.Empty<ItemDataSO>(),
                ItemType.Rune => runes ?? Enumerable.Empty<ItemDataSO>(),
                ItemType.Consumable => consumables ?? Enumerable.Empty<ItemDataSO>(),
                _ => (otherItems ?? Enumerable.Empty<ItemDataSO>()).Where(i => i != null && i.ItemType == type)
            };
        }

        public IEnumerable<ItemDataSO> GetRunes() => runes ?? Enumerable.Empty<ItemDataSO>();
        public IEnumerable<ItemDataSO> GetArtifacts() => artifacts ?? Enumerable.Empty<ItemDataSO>();
        public IEnumerable<ItemDataSO> GetConsumables() => consumables ?? Enumerable.Empty<ItemDataSO>();

        /// <summary>
        /// Registers a runtime-generated ItemDataSO (useful for unit testing or procedural loot).
        /// </summary>
        public void RegisterRuntimeItem(ItemDataSO item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;
            EnsureInitialized();

            switch (item.ItemType)
            {
                case ItemType.Artifact:
                    if (!artifacts.Contains(item)) artifacts.Add(item);
                    break;
                case ItemType.Rune:
                    if (!runes.Contains(item)) runes.Add(item);
                    break;
                case ItemType.Consumable:
                    if (!consumables.Contains(item)) consumables.Add(item);
                    break;
                default:
                    if (!otherItems.Contains(item)) otherItems.Add(item);
                    break;
            }

            _lookup[item.Id] = item;
            RebuildCombinedItemList();
        }

        /// <summary>
        /// Editor and test assignment method for auto-indexing and catalog population.
        /// </summary>
        public void SetItemsEditor(IEnumerable<ItemDataSO> newItems)
        {
            artifacts = new List<ItemDataSO>();
            runes = new List<ItemDataSO>();
            consumables = new List<ItemDataSO>();
            otherItems = new List<ItemDataSO>();

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
            Initialize();
        }
    }
}
