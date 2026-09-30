using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Database.CSV;
using SBGames.Inventory.Core;
using UnityEditor;
using UnityEngine;

namespace SBGames.Database.Editor.CSV
{
    /// <summary>
    /// Two-way CSV import and export pipeline for Items, Skills, and Characters.
    /// Supports smart updating of stats/metadata while preserving assigned visual assets.
    /// </summary>
    public static class DatabaseCsvPipeline
    {
        private const string DataTableFolder = "Assets/DataTables";
        private const string ItemsCsvPath = DataTableFolder + "/Items.csv";
        private const string SkillsCsvPath = DataTableFolder + "/Skills.csv";
        private const string CharactersCsvPath = DataTableFolder + "/Characters.csv";

        private const string DefaultItemAssetFolder = "Assets/ScriptableObjects/Items";
        private const string DefaultSkillAssetFolder = "Assets/ScriptableObjects/Skills";
        private const string DefaultCharacterAssetFolder = "Assets/ScriptableObjects/Characters";

        #region Menu Items

        [MenuItem("SBGames/Database/Export All Catalogs to CSV", priority = 101)]
        public static void ExportAllToCsvMenuItem() => ExportAllToCsv(true);

        public static void ExportAllToCsv(bool showDialog = false)
        {
            EnsureDataTableDirectory();
            ExportItemsCsv();
            ExportSkillsCsv();
            ExportCharactersCsv();

            AssetDatabase.Refresh();
            if (showDialog)
            {
                EditorUtility.DisplayDialog("CSV Export Complete", $"All catalogs exported successfully to '{DataTableFolder}'.", "OK");
            }
        }

        [MenuItem("SBGames/Database/Import All Catalogs from CSV", priority = 102)]
        public static void ImportAllFromCsvMenuItem() => ImportAllFromCsv(true);

        public static void ImportAllFromCsv(bool showDialog = false)
        {
            EnsureDataTableDirectory();
            int itemsCount = ImportItemsCsv();
            int skillsCount = ImportSkillsCsv();
            int charsCount = ImportCharactersCsv();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Rebuild catalogs automatically after import
            DatabaseAutoIndexer.RebuildAllCatalogs();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("CSV Import Complete",
                    $"Imported/Updated:\n- {itemsCount} Items\n- {skillsCount} Skills\n- {charsCount} Characters\n\nAll catalogs auto-rebuilt!", "OK");
            }
        }

        #endregion

        #region Items CSV

        public static void ExportItemsCsv()
        {
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            var headers = new[] { "Id", "ItemName", "Description", "ItemType", "EquippableSlotType", "MaxStack" };
            var rows = new List<Dictionary<string, string>>();

            foreach (var guid in itemGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;

                var row = new Dictionary<string, string>
                {
                    ["Id"] = item.Id ?? string.Empty,
                    ["ItemName"] = item.ItemName ?? string.Empty,
                    ["Description"] = item.Description ?? string.Empty,
                    ["ItemType"] = item.ItemType.ToString(),
                    ["EquippableSlotType"] = item.EquippableSlotType.ToString(),
                    ["MaxStack"] = item.MaxStack.ToString()
                };
                rows.Add(row);
            }

            string csvContent = CsvParser.SerializeTable(headers, rows);
            File.WriteAllText(ItemsCsvPath, csvContent);
            Debug.Log($"[DatabaseCsvPipeline] Exported {rows.Count} items to {ItemsCsvPath}");
        }

        public static int ImportItemsCsv()
        {
            if (!File.Exists(ItemsCsvPath))
            {
                Debug.LogWarning($"[DatabaseCsvPipeline] {ItemsCsvPath} not found. Skipping item import.");
                return 0;
            }

            string csvContent = File.ReadAllText(ItemsCsvPath);
            var table = CsvParser.ParseTable(csvContent);
            if (table == null || table.Count == 0) return 0;

            EnsureDirectory(DefaultItemAssetFolder);

            // Index existing assets by ID
            var existingItems = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var guid in itemGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(p);
                if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                {
                    existingItems[item.Id] = item;
                }
            }

            int count = 0;
            foreach (var row in table)
            {
                if (!row.TryGetValue("Id", out var id) || string.IsNullOrWhiteSpace(id)) continue;

                ItemDefinition targetItem;
                if (!existingItems.TryGetValue(id, out targetItem))
                {
                    // Create new asset
                    targetItem = ScriptableObject.CreateInstance<ItemDefinition>();
                    string assetPath = $"{DefaultItemAssetFolder}/{SanitizeFilename(id)}.asset";
                    AssetDatabase.CreateAsset(targetItem, assetPath);
                    existingItems[id] = targetItem;
                }

                var so = new SerializedObject(targetItem);
                so.FindProperty("id").stringValue = id;

                if (row.TryGetValue("ItemName", out var name))
                    so.FindProperty("itemName").stringValue = name;

                if (row.TryGetValue("Description", out var desc))
                    so.FindProperty("description").stringValue = desc;

                if (row.TryGetValue("ItemType", out var typeStr) && Enum.TryParse<ItemType>(typeStr, true, out var itemType))
                    so.FindProperty("itemType").enumValueIndex = (int)itemType;

                if (row.TryGetValue("EquippableSlotType", out var slotStr) && Enum.TryParse<EquipmentSlotType>(slotStr, true, out var slotType))
                    so.FindProperty("equippableSlotType").enumValueIndex = (int)slotType;

                if (row.TryGetValue("MaxStack", out var stackStr) && int.TryParse(stackStr, out var stack))
                    so.FindProperty("maxStack").intValue = Mathf.Max(1, stack);

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetItem);
                count++;
            }

            return count;
        }

        #endregion

        #region Skills CSV

        public static void ExportSkillsCsv()
        {
            var skillGuids = AssetDatabase.FindAssets("t:SkillData");
            var headers = new[]
            {
                "SkillID", "SkillName", "Description", "SkillType", "DamageType", "BaseDamage", "BaseManaCost",
                "ProjectileSpeed", "ProjectileLifetime", "Chain", "MaxChainTargets", "ChainDamageMultiplier",
                "Area", "AreaRadius", "BeamTickInterval"
            };
            var rows = new List<Dictionary<string, string>>();

            foreach (var guid in skillGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill == null) continue;

                var row = new Dictionary<string, string>
                {
                    ["SkillID"] = skill.skillID ?? string.Empty,
                    ["SkillName"] = skill.skillName ?? string.Empty,
                    ["Description"] = skill.description ?? string.Empty,
                    ["SkillType"] = skill.type.ToString(),
                    ["DamageType"] = skill.damageType.ToString(),
                    ["BaseDamage"] = skill.baseDamage.ToString("F2"),
                    ["BaseManaCost"] = skill.baseManaCost.ToString("F2"),
                    ["ProjectileSpeed"] = skill.projectileSpeed.ToString("F2"),
                    ["ProjectileLifetime"] = skill.projectileLifetime.ToString("F2"),
                    ["Chain"] = skill.chain.ToString(),
                    ["MaxChainTargets"] = skill.maxChainTargets.ToString(),
                    ["ChainDamageMultiplier"] = skill.chainDamageMultiplier.ToString("F2"),
                    ["Area"] = skill.area.ToString(),
                    ["AreaRadius"] = skill.areaRadius.ToString("F2"),
                    ["BeamTickInterval"] = skill.beamTickInterval.ToString("F2")
                };
                rows.Add(row);
            }

            string csvContent = CsvParser.SerializeTable(headers, rows);
            File.WriteAllText(SkillsCsvPath, csvContent);
            Debug.Log($"[DatabaseCsvPipeline] Exported {rows.Count} skills to {SkillsCsvPath}");
        }

        public static int ImportSkillsCsv()
        {
            if (!File.Exists(SkillsCsvPath))
            {
                Debug.LogWarning($"[DatabaseCsvPipeline] {SkillsCsvPath} not found. Skipping skill import.");
                return 0;
            }

            string csvContent = File.ReadAllText(SkillsCsvPath);
            var table = CsvParser.ParseTable(csvContent);
            if (table == null || table.Count == 0) return 0;

            EnsureDirectory(DefaultSkillAssetFolder);

            var existingSkills = new Dictionary<string, SkillData>(StringComparer.OrdinalIgnoreCase);
            var skillGuids = AssetDatabase.FindAssets("t:SkillData");
            foreach (var guid in skillGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(p);
                if (skill != null && !string.IsNullOrWhiteSpace(skill.skillID))
                {
                    existingSkills[skill.skillID] = skill;
                }
            }

            int count = 0;
            foreach (var row in table)
            {
                if (!row.TryGetValue("SkillID", out var id) || string.IsNullOrWhiteSpace(id)) continue;

                SkillData targetSkill;
                if (!existingSkills.TryGetValue(id, out targetSkill))
                {
                    targetSkill = ScriptableObject.CreateInstance<SkillData>();
                    targetSkill.skillID = id;
                    string assetPath = $"{DefaultSkillAssetFolder}/{SanitizeFilename(id)}.asset";
                    AssetDatabase.CreateAsset(targetSkill, assetPath);
                    existingSkills[id] = targetSkill;
                }

                targetSkill.skillID = id;
                if (row.TryGetValue("SkillName", out var name)) targetSkill.skillName = name;
                if (row.TryGetValue("Description", out var desc)) targetSkill.description = desc;
                if (row.TryGetValue("SkillType", out var typeStr) && Enum.TryParse<SkillType>(typeStr, true, out var st)) targetSkill.type = st;
                if (row.TryGetValue("DamageType", out var dtStr) && Enum.TryParse<DamageType>(dtStr, true, out var dt)) targetSkill.damageType = dt;
                if (row.TryGetValue("BaseDamage", out var dmgStr) && float.TryParse(dmgStr, out var dmg)) targetSkill.baseDamage = dmg;
                if (row.TryGetValue("BaseManaCost", out var manaStr) && float.TryParse(manaStr, out var mana)) targetSkill.baseManaCost = mana;
                if (row.TryGetValue("ProjectileSpeed", out var spdStr) && float.TryParse(spdStr, out var spd)) targetSkill.projectileSpeed = spd;
                if (row.TryGetValue("ProjectileLifetime", out var ltStr) && float.TryParse(ltStr, out var lt)) targetSkill.projectileLifetime = lt;
                if (row.TryGetValue("Chain", out var chainStr) && bool.TryParse(chainStr, out var chain)) targetSkill.chain = chain;
                if (row.TryGetValue("MaxChainTargets", out var mctStr) && int.TryParse(mctStr, out var mct)) targetSkill.maxChainTargets = mct;
                if (row.TryGetValue("ChainDamageMultiplier", out var cdmStr) && float.TryParse(cdmStr, out var cdm)) targetSkill.chainDamageMultiplier = cdm;
                if (row.TryGetValue("Area", out var areaStr) && bool.TryParse(areaStr, out var area)) targetSkill.area = area;
                if (row.TryGetValue("AreaRadius", out var radStr) && float.TryParse(radStr, out var rad)) targetSkill.areaRadius = rad;
                if (row.TryGetValue("BeamTickInterval", out var btiStr) && float.TryParse(btiStr, out var bti)) targetSkill.beamTickInterval = bti;

                EditorUtility.SetDirty(targetSkill);
                count++;
            }

            return count;
        }

        #endregion

        #region Characters CSV

        public static void ExportCharactersCsv()
        {
            var charDbGuids = AssetDatabase.FindAssets("t:CharacterDatabaseSO");
            var headers = new[] { "Id", "CharacterName", "MaxLife", "MaxMana", "Movespeed", "Damage", "AttackRange", "AttackTimeInterval", "DefaultSkillIds" };
            var rows = new List<Dictionary<string, string>>();

            foreach (var guid in charDbGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var charDb = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(path);
                if (charDb == null || charDb.Characters == null) continue;

                foreach (var c in charDb.Characters)
                {
                    if (c == null) continue;
                    var row = new Dictionary<string, string>
                    {
                        ["Id"] = c.id ?? string.Empty,
                        ["CharacterName"] = c.characterName ?? string.Empty,
                        ["MaxLife"] = c.baseStats != null ? c.baseStats.MaxLife.ToString("F1") : "100",
                        ["MaxMana"] = c.baseStats != null ? c.baseStats.MaxMana.ToString("F1") : "50",
                        ["Movespeed"] = c.baseStats != null ? c.baseStats.Movespeed.ToString("F1") : "5",
                        ["Damage"] = c.baseStats != null ? c.baseStats.Damage.ToString("F1") : "10",
                        ["AttackRange"] = c.baseStats != null ? c.baseStats.AttackRange.ToString("F1") : "1",
                        ["AttackTimeInterval"] = c.baseStats != null ? c.baseStats.AttackTimeInterval.ToString("F1") : "1",
                        ["DefaultSkillIds"] = c.defaultSkillIds != null ? string.Join(";", c.defaultSkillIds) : string.Empty
                    };
                    rows.Add(row);
                }
            }

            string csvContent = CsvParser.SerializeTable(headers, rows);
            File.WriteAllText(CharactersCsvPath, csvContent);
            Debug.Log($"[DatabaseCsvPipeline] Exported {rows.Count} characters to {CharactersCsvPath}");
        }

        public static int ImportCharactersCsv()
        {
            if (!File.Exists(CharactersCsvPath))
            {
                Debug.LogWarning($"[DatabaseCsvPipeline] {CharactersCsvPath} not found. Skipping character import.");
                return 0;
            }

            string csvContent = File.ReadAllText(CharactersCsvPath);
            var table = CsvParser.ParseTable(csvContent);
            if (table == null || table.Count == 0) return 0;

            EnsureDirectory(DefaultCharacterAssetFolder);

            // Find or create primary CharacterDatabaseSO
            var charDb = DatabaseAutoIndexer.GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase");
            var entriesMap = charDb.Characters != null
                ? charDb.Characters.Where(c => c != null && !string.IsNullOrWhiteSpace(c.id)).ToDictionary(c => c.id, c => c, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, CharacterEntry>(StringComparer.OrdinalIgnoreCase);

            int count = 0;
            var updatedList = new List<CharacterEntry>();

            foreach (var row in table)
            {
                if (!row.TryGetValue("Id", out var id) || string.IsNullOrWhiteSpace(id)) continue;

                if (!entriesMap.TryGetValue(id, out var entry))
                {
                    entry = new CharacterEntry { id = id };
                }

                if (row.TryGetValue("CharacterName", out var name)) entry.characterName = name;

                if (row.TryGetValue("DefaultSkillIds", out var skillsStr))
                {
                    entry.defaultSkillIds = skillsStr
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToList();
                }

                // If CharacterBaseStats asset doesn't exist, create it
                if (entry.baseStats == null)
                {
                    string statsPath = $"{DefaultCharacterAssetFolder}/{SanitizeFilename(id)}_BaseStats.asset";
                    var existingStats = AssetDatabase.LoadAssetAtPath<CharacterBaseStats>(statsPath);
                    if (existingStats == null)
                    {
                        existingStats = ScriptableObject.CreateInstance<CharacterBaseStats>();
                        AssetDatabase.CreateAsset(existingStats, statsPath);
                    }
                    entry.baseStats = existingStats;
                }

                if (entry.baseStats != null)
                {
                    if (row.TryGetValue("MaxLife", out var hpStr) && float.TryParse(hpStr, out var hp)) entry.baseStats.MaxLife = hp;
                    if (row.TryGetValue("MaxMana", out var manaStr) && float.TryParse(manaStr, out var mana)) entry.baseStats.MaxMana = mana;
                    if (row.TryGetValue("Movespeed", out var spdStr) && float.TryParse(spdStr, out var spd)) entry.baseStats.Movespeed = spd;
                    if (row.TryGetValue("Damage", out var dmgStr) && float.TryParse(dmgStr, out var dmg)) entry.baseStats.Damage = dmg;
                    if (row.TryGetValue("AttackRange", out var rangeStr) && float.TryParse(rangeStr, out var range)) entry.baseStats.AttackRange = range;
                    if (row.TryGetValue("AttackTimeInterval", out var atiStr) && float.TryParse(atiStr, out var ati)) entry.baseStats.AttackTimeInterval = ati;
                    EditorUtility.SetDirty(entry.baseStats);
                }

                updatedList.Add(entry);
                count++;
            }

            charDb.SetCharactersEditor(updatedList);
            EditorUtility.SetDirty(charDb);
            return count;
        }

        #endregion

        #region Helpers

        private static void EnsureDataTableDirectory()
        {
            EnsureDirectory(DataTableFolder);
        }

        private static void EnsureDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
        }

        private static string SanitizeFilename(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }

        #endregion
    }
}
