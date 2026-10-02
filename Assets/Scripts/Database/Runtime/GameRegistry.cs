using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Inventory.Data;
using UnityEngine;

namespace SBGames.Database.Runtime
{
    /// <summary>
    /// Static facade providing high-performance O(1) runtime lookups for Items, Skills, Characters, and other game data.
    /// Safely handles missing keys, null checks, and fallbacks.
    /// </summary>
    public static class GameRegistry
    {
        private static MasterGameDatabaseSO _activeDatabase;

        public static MasterGameDatabaseSO ActiveDatabase
        {
            get
            {
                if (_activeDatabase == null)
                {
                    // Check GameServices if registered
                    if (GameServices.MasterGameDatabase != null)
                    {
                        _activeDatabase = GameServices.MasterGameDatabase;
                    }
                    else
                    {
                        // Automatic fallback from Resources if present
                        _activeDatabase = Resources.Load<MasterGameDatabaseSO>("MasterGameDatabase");
                    }

                    if (_activeDatabase != null && !_activeDatabase.IsInitialized)
                    {
                        _activeDatabase.InitializeAll();
                    }
                }

                return _activeDatabase;
            }
        }

        public static void SetDatabase(MasterGameDatabaseSO database)
        {
            _activeDatabase = database;
            if (_activeDatabase != null)
            {
                _activeDatabase.InitializeAll();
            }
        }

        #region Item Queries

        public static ItemDataSO GetItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[GameRegistry] GetItem requested with null or empty ID.");
                return null;
            }

            var db = ActiveDatabase?.Items;
            if (db == null)
            {
                Debug.LogError("[GameRegistry] ItemDatabase is not loaded or assigned in MasterGameDatabase.");
                return null;
            }

            var item = db.Get(id);
            if (item == null)
            {
                Debug.LogWarning($"[GameRegistry] Item with ID '{id}' was not found in database.");
            }

            return item;
        }

        public static bool TryGetItem(string id, out ItemDataSO item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var db = ActiveDatabase?.Items;
            if (db == null) return false;

            return db.TryGet(id, out item);
        }

        public static IEnumerable<ItemDataSO> GetAllItems()
        {
            return ActiveDatabase?.Items?.Items ?? Enumerable.Empty<ItemDataSO>();
        }

        public static IEnumerable<ItemDataSO> GetRunes()
        {
            return ActiveDatabase?.Items?.GetRunes() ?? Enumerable.Empty<ItemDataSO>();
        }

        public static IEnumerable<ItemDataSO> GetArtifacts()
        {
            return ActiveDatabase?.Items?.GetArtifacts() ?? Enumerable.Empty<ItemDataSO>();
        }

        public static IEnumerable<ItemDataSO> GetConsumables()
        {
            return ActiveDatabase?.Items?.GetConsumables() ?? Enumerable.Empty<ItemDataSO>();
        }

        #endregion

        #region Skill Queries

        public static SkillData GetSkill(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[GameRegistry] GetSkill requested with null or empty ID.");
                return null;
            }

            var db = ActiveDatabase?.Skills;
            if (db == null)
            {
                Debug.LogError("[GameRegistry] SkillDatabase is not loaded or assigned in MasterGameDatabase.");
                return null;
            }

            var skill = db.Get(id);
            if (skill == null)
            {
                Debug.LogWarning($"[GameRegistry] Skill with ID '{id}' was not found in database.");
            }

            return skill;
        }

        public static bool TryGetSkill(string id, out SkillData skill)
        {
            skill = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var db = ActiveDatabase?.Skills;
            if (db == null) return false;

            return db.TryGet(id, out skill);
        }

        public static IEnumerable<SkillData> GetAllSkills()
        {
            return ActiveDatabase?.Skills?.Skills ?? Enumerable.Empty<SkillData>();
        }

        #endregion

        #region Character Queries

        public static CharacterEntry GetCharacter(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[GameRegistry] GetCharacter requested with null or empty ID.");
                return null;
            }

            var db = ActiveDatabase?.Characters;
            if (db == null)
            {
                Debug.LogError("[GameRegistry] CharacterDatabase is not loaded or assigned in MasterGameDatabase.");
                return null;
            }

            var character = db.Get(id);
            if (character == null)
            {
                Debug.LogWarning($"[GameRegistry] Character with ID '{id}' was not found in database.");
            }

            return character;
        }

        public static bool TryGetCharacter(string id, out CharacterEntry character)
        {
            character = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var db = ActiveDatabase?.Characters;
            if (db == null) return false;

            return db.TryGet(id, out character);
        }

        public static IEnumerable<CharacterEntry> GetAllCharacters()
        {
            return ActiveDatabase?.Characters?.Characters ?? Enumerable.Empty<CharacterEntry>();
        }

        #endregion
    }
}
