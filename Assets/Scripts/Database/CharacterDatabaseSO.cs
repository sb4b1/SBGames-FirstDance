using System;
using System.Collections.Generic;
using SBGames.Database.Hybrid;
using UnityEngine;

namespace SBGames.Database
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "SBGames/Database/Character Database")]
    public class CharacterDatabaseSO : ScriptableObject
    {
        public List<CharacterEntry> characters = new();

        private Dictionary<string, CharacterEntry> _lookup;

        public IReadOnlyList<CharacterEntry> Characters => characters;

        public void Initialize()
        {
            _lookup = new Dictionary<string, CharacterEntry>(StringComparer.OrdinalIgnoreCase);

            if (characters == null) return;

            for (int i = 0; i < characters.Count; i++)
            {
                var entry = characters[i];
                if (entry == null) continue;

                string id = entry.id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogWarning($"[CharacterDatabaseSO] Character at index {i} has an empty ID.", this);
                    continue;
                }

                if (_lookup.ContainsKey(id))
                {
                    Debug.LogWarning($"[CharacterDatabaseSO] Duplicate character ID '{id}' found in database. Overwriting previous entry.", this);
                }

                _lookup[id] = entry;
            }
        }

        private void EnsureInitialized()
        {
            if (_lookup == null)
            {
                Initialize();
            }
        }

        public CharacterEntry Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureInitialized();
            return _lookup.GetValueOrDefault(id);
        }

        public bool TryGet(string id, out CharacterEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            EnsureInitialized();
            return _lookup.TryGetValue(id, out entry);
        }

        public void SetCharactersEditor(List<CharacterEntry> newEntries)
        {
            characters = newEntries ?? new List<CharacterEntry>();
        }
    }

    [Serializable]
    public class CharacterEntry
    {
        public string id;
        public string characterName;
        public GameObject prefab;
        public CharacterBaseStatSO baseStats;
        public List<string> defaultSkillIds = new();
        public List<string> extraItemIds = new();
        public HybridGameObjectReference hybridPrefab = new();

        /// <summary>
        /// Helper to get the character prefab (direct reference or hybrid).
        /// </summary>
        public GameObject GetPrefabDirect()
        {
            if (prefab != null) return prefab;
            if (hybridPrefab != null && hybridPrefab.HasDirectAsset) return hybridPrefab.DirectAsset;
            return null;
        }
    }
}