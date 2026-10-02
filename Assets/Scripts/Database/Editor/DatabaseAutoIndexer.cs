using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Inventory.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Database.Editor
{
    /// <summary>
    /// Automatic scanner and integrity validator for all game data catalogs.
    /// Scans project folders, links ScriptableObjects into catalogs, and reports duplicate/invalid IDs.
    /// </summary>
    public static class DatabaseAutoIndexer
    {
        private const string DatabaseFolder = "Assets/ScriptableObjects/Database";
        private const string ResourcesFolder = "Assets/Resources";

        [MenuItem("SBGames/Database/Rebuild All Catalogs", priority = 100)]
        public static void RebuildAllCatalogs()
        {
            EnsureDirectory(DatabaseFolder);
            EnsureDirectory(ResourcesFolder);

            int validationErrors = 0;

            // 1. Rebuild Item Database
            var itemDb = GetOrCreateCatalog<ItemDatabaseSO>("ItemDatabase", DatabaseFolder);
            var items = FindAllAssets<ItemDataSO>();
            itemDb.SetItemsEditor(items);
            EditorUtility.SetDirty(itemDb);
            validationErrors += ValidateUniqueIds(items.Select(i => (i.Id, (UnityEngine.Object)i)), "Item");

            // 2. Rebuild Skill Database
            var skillDb = GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase", DatabaseFolder);
            var skills = FindAllAssets<SkillData>();
            skillDb.SetSkillsEditor(skills);
            EditorUtility.SetDirty(skillDb);
            validationErrors += ValidateUniqueIds(skills.Select(s => (s.skillID, (UnityEngine.Object)s)), "Skill");

            // 3. Rebuild Character Database
            var charDb = GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase", DatabaseFolder);
            if (charDb.Characters != null)
            {
                validationErrors += ValidateUniqueIds(
                    charDb.Characters.Where(c => c != null).Select(c => (c.id, (UnityEngine.Object)charDb)),
                    "Character");
            }
            EditorUtility.SetDirty(charDb);

            // 4. Link into MasterGameDatabase in Assets/Resources so it auto-loads at runtime
            var masterDb = GetOrCreateCatalog<MasterGameDatabaseSO>("MasterGameDatabase", ResourcesFolder);
            masterDb.SetSubCatalogsEditor(itemDb, skillDb, charDb);
            EditorUtility.SetDirty(masterDb);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string statusMsg = validationErrors == 0
                ? $"<color=green><b>Database Rebuild Successful!</b></color>\n- Items: {items.Count}\n- Skills: {skills.Count}\n- Characters: {charDb.Characters?.Count ?? 0}\n- 0 Duplicate/Empty IDs."
                : $"<color=orange><b>Database Rebuild Complete with {validationErrors} Warnings/Errors.</b></color> Check Console for details.\n- Items: {items.Count}\n- Skills: {skills.Count}\n- Characters: {charDb.Characters?.Count ?? 0}";

            Debug.Log($"[DatabaseAutoIndexer] {statusMsg}");
        }

        public static T GetOrCreateCatalog<T>(string assetName, string folder = DatabaseFolder) where T : ScriptableObject
        {
            EnsureDirectory(folder);
            string assetPath = $"{folder}/{assetName}.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(catalog, assetPath);
                Debug.Log($"[DatabaseAutoIndexer] Created new catalog asset at '{assetPath}'.");
            }

            return catalog;
        }

        private static List<T> FindAllAssets<T>() where T : ScriptableObject
        {
            var results = new List<T>();
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static int ValidateUniqueIds(IEnumerable<(string id, UnityEngine.Object asset)> entries, string categoryName)
        {
            int errorCount = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (id, asset) in entries)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogError($"[DatabaseAutoIndexer] {categoryName} asset has a null or empty ID.", asset);
                    errorCount++;
                    continue;
                }

                if (!seen.Add(id))
                {
                    Debug.LogError($"[DatabaseAutoIndexer] Duplicate {categoryName} ID detected: '{id}'.", asset);
                    errorCount++;
                }
            }

            return errorCount;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
