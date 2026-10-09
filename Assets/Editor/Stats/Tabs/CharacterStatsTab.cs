using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Database;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats.Tabs
{
    public class CharacterStatsTab : IStatTab
    {
        public string Title => "⚔️ Characters";

        private StatBalanceManagerWindow _window;
        private List<CharacterRow> _rows = new();
        private CharacterRow _selectedRow;
        private Vector2 _tableScroll;
        private Vector2 _detailScroll;

        // Search & Filter
        private string _searchQuery = "";
        private int _rankFilterIndex = 0; // 0 = All
        private bool _filterIssuesOnly = false;

        // Sorting
        public enum SortColumn { Id, Name, Rank, Level, MaxLife, MaxMana, Movespeed, Damage, AttackRange, AttackInterval }
        private SortColumn _currentSort = SortColumn.Id;
        private bool _sortAscending = true;

        // Bulk Math Panel
        private bool _showBulkMath = false;
        public enum BulkScope { Filtered, SelectedOnly }
        public enum BulkTargetField { MaxLife, MaxMana, Movespeed, Damage, AttackRange, AttackInterval, DecisionInterval }
        public enum BulkMathMode { PercentMultiply, FlatAdd }

        private BulkScope _bulkScope = BulkScope.Filtered;
        private BulkTargetField _bulkTargetField = BulkTargetField.MaxLife;
        private BulkMathMode _bulkMode = BulkMathMode.PercentMultiply;
        private float _bulkValue = 10f; // e.g. +10%

        public bool HasUnsavedChanges => _rows.Any(r => r.IsDirty);
        public int ValidationIssueCount => _rows.Count(r => !string.IsNullOrEmpty(r.ValidationError));

        public class CharacterRow
        {
            public CharacterEntry Entry;
            public CharacterBaseStatSO BaseStatsSO;

            // Identity
            public string Id = "";
            public string Name = "";
            public GameObject Prefab;

            // Stats
            public int Level = 1;
            public CharacterRank Rank = CharacterRank.D;
            public float MaxLife = 100f;
            public float MaxMana = 50f;
            public float Movespeed = 5f;
            public float Damage = 10f;
            public float AttackRange = 1f;
            public float AttackTimeInterval = 1f;

            // Recovery & AI
            public float PostAttackRecovery = 0.3f;
            public float PostCastRecovery = 0.3f;
            public float DecisionInterval = 0.3f;
            public float IntervalJitter = 0.05f;

            // Skills
            public List<string> DefaultSkillIds = new();

            // State
            public bool IsSelected = false;
            public bool IsDirty = false;
            public string ValidationError = "";

            public void MarkDirty()
            {
                IsDirty = true;
                Validate();
            }

            public void Validate()
            {
                ValidationError = "";
                if (string.IsNullOrWhiteSpace(Id))
                {
                    ValidationError = "ID is missing or whitespace.";
                    return;
                }
                if (BaseStatsSO == null)
                {
                    ValidationError = "BaseStats ScriptableObject is unassigned.";
                    return;
                }
                if (MaxLife <= 0f)
                {
                    ValidationError = "MaxLife must be greater than 0.";
                    return;
                }
                if (Movespeed < 0f)
                {
                    ValidationError = "Movespeed cannot be negative.";
                    return;
                }
                if (AttackRange <= 0f)
                {
                    ValidationError = "AttackRange must be greater than 0.";
                    return;
                }
                if (AttackTimeInterval <= 0f)
                {
                    ValidationError = "AttackTimeInterval must be greater than 0.";
                    return;
                }
            }
        }

        public void Initialize(StatBalanceManagerWindow window)
        {
            _window = window;
            ReloadData();
        }

        public void ReloadData()
        {
            _rows.Clear();
            _selectedRow = null;

            var charDb = DatabaseAutoIndexer.GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase");
            var indexedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (charDb != null && charDb.Characters != null)
            {
                foreach (var entry in charDb.Characters)
                {
                    if (entry == null) continue;
                    var row = new CharacterRow
                    {
                        Entry = entry,
                        BaseStatsSO = entry.baseStats,
                        Id = entry.id ?? "",
                        Name = entry.characterName ?? "",
                        Prefab = entry.GetPrefabDirect(),
                        DefaultSkillIds = entry.defaultSkillIds != null ? new List<string>(entry.defaultSkillIds) : new List<string>()
                    };

                    if (row.BaseStatsSO != null)
                    {
                        row.Level = row.BaseStatsSO.characterLevel;
                        row.Rank = row.BaseStatsSO.characterRank;
                        row.MaxLife = row.BaseStatsSO.MaxLife;
                        row.MaxMana = row.BaseStatsSO.MaxMana;
                        row.Movespeed = row.BaseStatsSO.Movespeed;
                        row.Damage = row.BaseStatsSO.Damage;
                        row.AttackRange = row.BaseStatsSO.AttackRange;
                        row.AttackTimeInterval = row.BaseStatsSO.AttackTimeInterval;
                        row.PostAttackRecovery = row.BaseStatsSO.DefaultPostAttackRecovery;
                        row.PostCastRecovery = row.BaseStatsSO.DefaultPostCastRecovery;
                        row.DecisionInterval = row.BaseStatsSO.DecisionInterval;
                        row.IntervalJitter = row.BaseStatsSO.IntervalJitter;
                    }

                    row.Validate();
                    _rows.Add(row);
                    if (!string.IsNullOrEmpty(row.Id)) indexedIds.Add(row.Id);
                }
            }

            // Also check for any orphaned CharacterBaseStatSO assets not in database
            var statGuids = AssetDatabase.FindAssets("t:CharacterBaseStatSO");
            foreach (var guid in statGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var statAsset = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(path);
                if (statAsset == null) continue;

                string assetName = Path.GetFileNameWithoutExtension(path).Replace("_BaseStats", "");
                if (indexedIds.Contains(assetName)) continue;

                // Orphaned or standalone stat asset
                var orphanRow = new CharacterRow
                {
                    Entry = null,
                    BaseStatsSO = statAsset,
                    Id = assetName,
                    Name = assetName,
                    Level = statAsset.characterLevel,
                    Rank = statAsset.characterRank,
                    MaxLife = statAsset.MaxLife,
                    MaxMana = statAsset.MaxMana,
                    Movespeed = statAsset.Movespeed,
                    Damage = statAsset.Damage,
                    AttackRange = statAsset.AttackRange,
                    AttackTimeInterval = statAsset.AttackTimeInterval,
                    PostAttackRecovery = statAsset.DefaultPostAttackRecovery,
                    PostCastRecovery = statAsset.DefaultPostCastRecovery,
                    DecisionInterval = statAsset.DecisionInterval,
                    IntervalJitter = statAsset.IntervalJitter
                };
                orphanRow.Validate();
                _rows.Add(orphanRow);
                indexedIds.Add(assetName);
            }

            CheckForDuplicateIds();
        }

        private void CheckForDuplicateIds()
        {
            var duplicates = _rows
                .Where(r => !string.IsNullOrEmpty(r.Id))
                .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var r in _rows)
            {
                if (duplicates.Contains(r.Id))
                {
                    r.ValidationError = $"Duplicate Character ID '{r.Id}' detected.";
                }
            }
        }

        public void SaveData()
        {
            var charDb = DatabaseAutoIndexer.GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase");
            var updatedEntries = new List<CharacterEntry>();

            const string defaultFolder = "Assets/ScriptableObjects/Characters";
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }

            foreach (var row in _rows)
            {
                if (row.BaseStatsSO == null)
                {
                    string statsPath = $"{defaultFolder}/{row.Id}_BaseStats.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(statsPath);
                    if (existing == null)
                    {
                        existing = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
                        AssetDatabase.CreateAsset(existing, statsPath);
                    }
                    row.BaseStatsSO = existing;
                }

                if (row.BaseStatsSO != null)
                {
                    row.BaseStatsSO.characterLevel = row.Level;
                    row.BaseStatsSO.characterRank = row.Rank;
                    row.BaseStatsSO.MaxLife = row.MaxLife;
                    row.BaseStatsSO.MaxMana = row.MaxMana;
                    row.BaseStatsSO.Movespeed = row.Movespeed;
                    row.BaseStatsSO.Damage = row.Damage;
                    row.BaseStatsSO.AttackRange = row.AttackRange;
                    row.BaseStatsSO.AttackTimeInterval = row.AttackTimeInterval;
                    row.BaseStatsSO.DefaultPostAttackRecovery = row.PostAttackRecovery;
                    row.BaseStatsSO.DefaultPostCastRecovery = row.PostCastRecovery;
                    row.BaseStatsSO.DecisionInterval = row.DecisionInterval;
                    row.BaseStatsSO.IntervalJitter = row.IntervalJitter;
                    EditorUtility.SetDirty(row.BaseStatsSO);
                }

                var entry = row.Entry ?? new CharacterEntry();
                entry.id = row.Id;
                entry.characterName = row.Name;
                entry.prefab = row.Prefab;
                entry.baseStats = row.BaseStatsSO;
                entry.defaultSkillIds = new List<string>(row.DefaultSkillIds);

                updatedEntries.Add(entry);
                row.Entry = entry;
                row.IsDirty = false;
                row.Validate();
            }

            charDb.SetCharactersEditor(updatedEntries);
            EditorUtility.SetDirty(charDb);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CheckForDuplicateIds();
        }

        public void DiscardChanges()
        {
            ReloadData();
        }

        public void DrawGUI()
        {
            DrawToolbar();

            if (_showBulkMath)
            {
                DrawBulkMathPanel();
            }

            EditorGUILayout.Space(2);

            // Split View: Table on top, Inspector on bottom
            float totalHeight = _window.position.height;
            float tableHeight = _selectedRow != null ? Mathf.Max(220, totalHeight - 340) : totalHeight - 90;

            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Height(tableHeight)))
            {
                DrawTableHeader();
                DrawTableBody();
            }

            if (_selectedRow != null)
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawDetailInspector();
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Search Field
                GUILayout.Label("🔍", GUILayout.Width(18));
                _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(180));
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchQuery = "";
                    GUI.FocusControl(null);
                }

                EditorGUILayout.Space(6);

                // Rank Filter
                var ranks = new[] { "All Ranks", "Rank E", "Rank D", "Rank C", "Rank B", "Rank A", "Rank S" };
                _rankFilterIndex = EditorGUILayout.Popup(_rankFilterIndex, ranks, EditorStyles.toolbarPopup, GUILayout.Width(95));

                // Issues Filter Toggle
                _filterIssuesOnly = GUILayout.Toggle(_filterIssuesOnly, "⚠️ Issues Only", EditorStyles.toolbarButton, GUILayout.Width(95));

                GUILayout.FlexibleSpace();

                // Bulk Math Toggle
                _showBulkMath = GUILayout.Toggle(_showBulkMath, "📐 Bulk Math", EditorStyles.toolbarButton, GUILayout.Width(90));

                EditorGUILayout.Space(4);

                // CSV Buttons
                if (GUILayout.Button("📤 Export CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    if (HasUnsavedChanges && EditorUtility.DisplayDialog("Unsaved Changes", "Save changes before exporting to CSV?", "Save & Export", "Export As-Is"))
                    {
                        SaveData();
                    }
                    DatabaseCsvPipeline.ExportCharactersCsv();
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("CSV Export", "Characters exported to Assets/DataTables/Characters.csv", "OK");
                }

                if (GUILayout.Button("📥 Import CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    if (EditorUtility.DisplayDialog("Import CSV", "Importing will overwrite current in-editor values with CSV data. Proceed?", "Import", "Cancel"))
                    {
                        DatabaseCsvPipeline.ImportCharactersCsv();
                        ReloadData();
                    }
                }
            }
        }

        private void DrawBulkMathPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("📐 Bulk Scaling Utility", EditorStyles.boldLabel, GUILayout.Width(160));
                    _bulkScope = (BulkScope)EditorGUILayout.EnumPopup(_bulkScope, GUILayout.Width(110));
                    EditorGUILayout.LabelField("Field:", GUILayout.Width(40));
                    _bulkTargetField = (BulkTargetField)EditorGUILayout.EnumPopup(_bulkTargetField, GUILayout.Width(110));
                    _bulkMode = (BulkMathMode)EditorGUILayout.EnumPopup(_bulkMode, GUILayout.Width(120));
                    _bulkValue = EditorGUILayout.FloatField(_bulkValue, GUILayout.Width(60));

                    string unitLabel = _bulkMode == BulkMathMode.PercentMultiply ? "%" : "pts";
                    GUILayout.Label(unitLabel, GUILayout.Width(25));

                    if (GUILayout.Button("Apply Bulk Math", EditorStyles.miniButtonRight, GUILayout.Width(110)))
                    {
                        ApplyBulkMath();
                    }
                }
            }
        }

        private void ApplyBulkMath()
        {
            var targets = GetFilteredRows().Where(r => _bulkScope == BulkScope.Filtered || r.IsSelected).ToList();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Bulk Scaling", "No matching rows found to adjust.", "OK");
                return;
            }

            foreach (var row in targets)
            {
                switch (_bulkTargetField)
                {
                    case BulkTargetField.MaxLife:
                        row.MaxLife = CalculateAdjustedValue(row.MaxLife, _bulkValue, _bulkMode, 1f);
                        break;
                    case BulkTargetField.MaxMana:
                        row.MaxMana = CalculateAdjustedValue(row.MaxMana, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.Movespeed:
                        row.Movespeed = CalculateAdjustedValue(row.Movespeed, _bulkValue, _bulkMode, 0.5f);
                        break;
                    case BulkTargetField.Damage:
                        row.Damage = CalculateAdjustedValue(row.Damage, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.AttackRange:
                        row.AttackRange = CalculateAdjustedValue(row.AttackRange, _bulkValue, _bulkMode, 0.1f);
                        break;
                    case BulkTargetField.AttackInterval:
                        row.AttackTimeInterval = CalculateAdjustedValue(row.AttackTimeInterval, _bulkValue, _bulkMode, 0.05f);
                        break;
                    case BulkTargetField.DecisionInterval:
                        row.DecisionInterval = CalculateAdjustedValue(row.DecisionInterval, _bulkValue, _bulkMode, 0.05f);
                        break;
                }
                row.MarkDirty();
            }
        }

        private float CalculateAdjustedValue(float current, float value, BulkMathMode mode, float minClamp)
        {
            float result = mode == BulkMathMode.PercentMultiply
                ? current * (1f + value / 100f)
                : current + value;
            return Mathf.Max(minClamp, (float)Math.Round(result, 2));
        }

        private List<CharacterRow> GetFilteredRows()
        {
            var list = _rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                list = list.Where(r =>
                    r.Id.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.Name.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_rankFilterIndex > 0)
            {
                var targetRank = (CharacterRank)(_rankFilterIndex - 1);
                list = list.Where(r => r.Rank == targetRank);
            }

            if (_filterIssuesOnly)
            {
                list = list.Where(r => !string.IsNullOrEmpty(r.ValidationError));
            }

            return SortRows(list);
        }

        private List<CharacterRow> SortRows(IEnumerable<CharacterRow> rows)
        {
            return _currentSort switch
            {
                SortColumn.Id => _sortAscending ? rows.OrderBy(r => r.Id).ToList() : rows.OrderByDescending(r => r.Id).ToList(),
                SortColumn.Name => _sortAscending ? rows.OrderBy(r => r.Name).ToList() : rows.OrderByDescending(r => r.Name).ToList(),
                SortColumn.Rank => _sortAscending ? rows.OrderBy(r => r.Rank).ToList() : rows.OrderByDescending(r => r.Rank).ToList(),
                SortColumn.Level => _sortAscending ? rows.OrderBy(r => r.Level).ToList() : rows.OrderByDescending(r => r.Level).ToList(),
                SortColumn.MaxLife => _sortAscending ? rows.OrderBy(r => r.MaxLife).ToList() : rows.OrderByDescending(r => r.MaxLife).ToList(),
                SortColumn.MaxMana => _sortAscending ? rows.OrderBy(r => r.MaxMana).ToList() : rows.OrderByDescending(r => r.MaxMana).ToList(),
                SortColumn.Movespeed => _sortAscending ? rows.OrderBy(r => r.Movespeed).ToList() : rows.OrderByDescending(r => r.Movespeed).ToList(),
                SortColumn.Damage => _sortAscending ? rows.OrderBy(r => r.Damage).ToList() : rows.OrderByDescending(r => r.Damage).ToList(),
                SortColumn.AttackRange => _sortAscending ? rows.OrderBy(r => r.AttackRange).ToList() : rows.OrderByDescending(r => r.AttackRange).ToList(),
                SortColumn.AttackInterval => _sortAscending ? rows.OrderBy(r => r.AttackTimeInterval).ToList() : rows.OrderByDescending(r => r.AttackTimeInterval).ToList(),
                _ => rows.ToList()
            };
        }

        private void DrawTableHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Multi-select toggle
                bool allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
                bool newAllSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(22));
                if (newAllSelected != allSelected)
                {
                    foreach (var r in _rows) r.IsSelected = newAllSelected;
                }

                GUILayout.Label("St", EditorStyles.boldLabel, GUILayout.Width(24));
                DrawSortHeader("ID", SortColumn.Id, 130);
                DrawSortHeader("Name", SortColumn.Name, 120);
                DrawSortHeader("Rank", SortColumn.Rank, 55);
                DrawSortHeader("Lvl", SortColumn.Level, 45);
                DrawSortHeader("HP", SortColumn.MaxLife, 65);
                DrawSortHeader("Mana", SortColumn.MaxMana, 55);
                DrawSortHeader("Spd", SortColumn.Movespeed, 55);
                DrawSortHeader("Dmg", SortColumn.Damage, 55);
                DrawSortHeader("Range", SortColumn.AttackRange, 55);
                DrawSortHeader("Interval", SortColumn.AttackInterval, 60);

                GUILayout.FlexibleSpace();
                GUILayout.Label("Actions", EditorStyles.boldLabel, GUILayout.Width(60));
            }
        }

        private void DrawSortHeader(string label, SortColumn column, float width)
        {
            string sortIndicator = _currentSort == column ? (_sortAscending ? " ▲" : " ▼") : "";
            if (GUILayout.Button(label + sortIndicator, EditorStyles.toolbarButton, GUILayout.Width(width)))
            {
                if (_currentSort == column)
                {
                    _sortAscending = !_sortAscending;
                }
                else
                {
                    _currentSort = column;
                    _sortAscending = true;
                }
            }
        }

        private void DrawTableBody()
        {
            var filtered = GetFilteredRows();
            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll);

            for (int i = 0; i < filtered.Count; i++)
            {
                var row = filtered[i];
                bool isSelectedRow = row == _selectedRow;

                GUI.backgroundColor = isSelectedRow ? new Color(0.25f, 0.45f, 0.7f, 1f) : (i % 2 == 0 ? new Color(0.2f, 0.2f, 0.2f, 0.4f) : Color.clear);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUI.backgroundColor = Color.white;

                    // Checkbox
                    row.IsSelected = EditorGUILayout.Toggle(row.IsSelected, GUILayout.Width(22));

                    // Status / Validation Badge
                    if (!string.IsNullOrEmpty(row.ValidationError))
                    {
                        GUILayout.Label(new GUIContent("⚠️", row.ValidationError), GUILayout.Width(24));
                    }
                    else if (row.IsDirty)
                    {
                        GUILayout.Label(new GUIContent("●", "Unsaved edits"), GUILayout.Width(24));
                    }
                    else
                    {
                        GUILayout.Label("✓", GUILayout.Width(24));
                    }

                    // ID
                    EditorGUI.BeginChangeCheck();
                    string newId = EditorGUILayout.TextField(row.Id, GUILayout.Width(130));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Id = newId;
                        row.MarkDirty();
                        CheckForDuplicateIds();
                    }

                    // Name
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField(row.Name, GUILayout.Width(120));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Name = newName;
                        row.MarkDirty();
                    }

                    // Rank
                    EditorGUI.BeginChangeCheck();
                    var newRank = (CharacterRank)EditorGUILayout.EnumPopup(row.Rank, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Rank = newRank;
                        row.MarkDirty();
                    }

                    // Level
                    EditorGUI.BeginChangeCheck();
                    int newLvl = EditorGUILayout.IntField(row.Level, GUILayout.Width(45));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Level = Mathf.Max(1, newLvl);
                        row.MarkDirty();
                    }

                    // HP
                    EditorGUI.BeginChangeCheck();
                    float newHp = EditorGUILayout.FloatField(row.MaxLife, GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.MaxLife = Mathf.Max(1f, newHp);
                        row.MarkDirty();
                    }

                    // Mana
                    EditorGUI.BeginChangeCheck();
                    float newMana = EditorGUILayout.FloatField(row.MaxMana, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.MaxMana = Mathf.Max(0f, newMana);
                        row.MarkDirty();
                    }

                    // Speed
                    EditorGUI.BeginChangeCheck();
                    float newSpd = EditorGUILayout.FloatField(row.Movespeed, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Movespeed = Mathf.Max(0f, newSpd);
                        row.MarkDirty();
                    }

                    // Damage
                    EditorGUI.BeginChangeCheck();
                    float newDmg = EditorGUILayout.FloatField(row.Damage, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Damage = Mathf.Max(0f, newDmg);
                        row.MarkDirty();
                    }

                    // Attack Range
                    EditorGUI.BeginChangeCheck();
                    float newRange = EditorGUILayout.FloatField(row.AttackRange, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.AttackRange = Mathf.Max(0.1f, newRange);
                        row.MarkDirty();
                    }

                    // Attack Interval
                    EditorGUI.BeginChangeCheck();
                    float newIntv = EditorGUILayout.FloatField(row.AttackTimeInterval, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.AttackTimeInterval = Mathf.Max(0.05f, newIntv);
                        row.MarkDirty();
                    }

                    GUILayout.FlexibleSpace();

                    // Inspect / Select button
                    if (GUILayout.Button(isSelectedRow ? "▼ Inspect" : "▶ Select", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        _selectedRow = isSelectedRow ? null : row;
                        if (_selectedRow != null && _selectedRow.BaseStatsSO != null)
                        {
                            EditorGUIUtility.PingObject(_selectedRow.BaseStatsSO);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetailInspector()
        {
            if (_selectedRow == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"🔍 Inspector: {_selectedRow.Id} ({_selectedRow.Name})", EditorStyles.boldLabel);
                if (GUILayout.Button("Close Inspector ✕", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    _selectedRow = null;
                    return;
                }
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.Height(170));

            using (new EditorGUILayout.HorizontalScope())
            {
                // Left Column: Asset references & Prefabs
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(350)))
                {
                    EditorGUILayout.LabelField("Asset References", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    var newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", _selectedRow.Prefab, typeof(GameObject), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.Prefab = newPrefab;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUI.BeginChangeCheck();
                    var newSo = (CharacterBaseStatSO)EditorGUILayout.ObjectField("BaseStats SO", _selectedRow.BaseStatsSO, typeof(CharacterBaseStatSO), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.BaseStatsSO = newSo;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Recovery Delays", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    float newPostAtk = EditorGUILayout.FloatField("Post-Attack Recovery", _selectedRow.PostAttackRecovery);
                    float newPostCast = EditorGUILayout.FloatField("Post-Cast Recovery", _selectedRow.PostCastRecovery);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.PostAttackRecovery = Mathf.Max(0f, newPostAtk);
                        _selectedRow.PostCastRecovery = Mathf.Max(0f, newPostCast);
                        _selectedRow.MarkDirty();
                    }
                }

                EditorGUILayout.Space(12);

                // Right Column: AI Parameters & Skills
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("AI Decision Parameters", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    float newDec = EditorGUILayout.FloatField("Decision Interval (s)", _selectedRow.DecisionInterval);
                    float newJitter = EditorGUILayout.Slider("Interval Jitter (s)", _selectedRow.IntervalJitter, 0f, 0.5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.DecisionInterval = Mathf.Max(0.05f, newDec);
                        _selectedRow.IntervalJitter = newJitter;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Default Skill IDs", EditorStyles.boldLabel);

                    for (int s = 0; s < _selectedRow.DefaultSkillIds.Count; s++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUI.BeginChangeCheck();
                            string skillId = EditorGUILayout.TextField(_selectedRow.DefaultSkillIds[s]);
                            if (EditorGUI.EndChangeCheck())
                            {
                                _selectedRow.DefaultSkillIds[s] = skillId;
                                _selectedRow.MarkDirty();
                            }

                            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24)))
                            {
                                _selectedRow.DefaultSkillIds.RemoveAt(s);
                                _selectedRow.MarkDirty();
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Add Skill ID", EditorStyles.miniButton, GUILayout.Width(110)))
                    {
                        _selectedRow.DefaultSkillIds.Add("SKILL_FIREBALL");
                        _selectedRow.MarkDirty();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
