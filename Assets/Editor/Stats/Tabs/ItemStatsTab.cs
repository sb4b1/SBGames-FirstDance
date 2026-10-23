using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Database;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using SBGames.Editor.Validation;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats.Tabs
{
    public class ItemStatsTab : IStatTab
    {
        public string Title => "📦 Items";

        private StatBalanceManagerWindow _window;
        private List<ItemRow> _rows = new();
        private ItemRow _selectedRow;
        private Vector2 _tableScroll;
        private Vector2 _detailScroll;

        // Search & Filter
        private string _searchQuery = "";
        private int _typeFilterIndex = 0; // 0 = All
        private bool _filterIssuesOnly = false;

        // Sorting
        public enum SortColumn { Id, Name, Type, Slot, BaseValue, MaxStack, MaxSockets, ModifiersCount }
        private SortColumn _currentSort = SortColumn.Id;
        private bool _sortAscending = true;

        // Bulk Math Panel
        private bool _showBulkMath = false;
        public enum BulkScope { Filtered, SelectedOnly }
        public enum BulkTargetField { BaseValue, MaxStack, MaxSockets, ModifierValues }
        public enum BulkMathMode { PercentMultiply, FlatAdd }

        private BulkScope _bulkScope = BulkScope.Filtered;
        private BulkTargetField _bulkTargetField = BulkTargetField.BaseValue;
        private BulkMathMode _bulkMode = BulkMathMode.PercentMultiply;
        private float _bulkValue = 10f;

        public bool HasUnsavedChanges => _rows.Any(r => r.IsDirty);
        public int ValidationIssueCount => _rows.Count(r => r.HasAnyIssue);
        public int ErrorCount => _rows.Count(r => r.WorstSeverity == ValidationSeverity.Error);
        public int WarningCount => _rows.Count(r => r.WorstSeverity == ValidationSeverity.Warning);

        public void SetFilterIssuesOnly(bool filterIssues)
        {
            _filterIssuesOnly = filterIssues;
        }

        public void ApplyExternalFindings(List<ValidationFinding> findings)
        {
            var bySubject = findings != null
                ? findings.Where(f => f.SubjectType == ValidationSubjectType.Item && !string.IsNullOrEmpty(f.SubjectId))
                          .GroupBy(f => f.SubjectId, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, List<ValidationFinding>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _rows)
            {
                if (bySubject.TryGetValue(row.Id, out var rowFindings))
                {
                    row.ExternalFindings = rowFindings;
                }
                else
                {
                    row.ExternalFindings.Clear();
                }
            }
        }

        public class ItemRow
        {
            public ItemDataSO Asset;

            // Identity
            public string Id = "";
            public string DisplayName = "";
            public Sprite Icon;
            public string Description = "";

            // Classification & Economy
            public ItemType Type = ItemType.Misc;
            public EquipmentSlotType SlotType = EquipmentSlotType.None;
            public int BaseValue = 10;
            public int MaxStack = 1;
            public int MaxSockets = 0;

            // Modifiers
            public List<ItemStatModifierData> StatModifiers = new();

            // State
            public bool IsSelected = false;
            public bool IsDirty = false;
            public string ValidationError = "";
            public List<ValidationFinding> ExternalFindings = new();

            public ValidationSeverity? WorstSeverity
            {
                get
                {
                    bool hasError = !string.IsNullOrEmpty(ValidationError) ||
                                    ExternalFindings.Any(f => f.Severity == ValidationSeverity.Error);
                    if (hasError) return ValidationSeverity.Error;

                    bool hasWarning = ExternalFindings.Any(f => f.Severity == ValidationSeverity.Warning);
                    if (hasWarning) return ValidationSeverity.Warning;

                    if (ExternalFindings.Any(f => f.Severity == ValidationSeverity.Info)) return ValidationSeverity.Info;

                    return null;
                }
            }

            public bool HasAnyIssue => WorstSeverity != null;

            public string GetCombinedValidationTooltip()
            {
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(ValidationError))
                {
                    lines.Add($"[Data] {ValidationError}");
                }
                foreach (var f in ExternalFindings)
                {
                    lines.Add($"[{f.Source}] {f.Message}");
                }
                return string.Join("\n", lines);
            }

            public void MarkDirty()
            {
                IsDirty = true;
                Validate();
                ValidationService.MarkStale();
            }

            public void Validate()
            {
                ValidationError = "";
                if (string.IsNullOrWhiteSpace(Id))
                {
                    ValidationError = "Item ID is missing.";
                    return;
                }
                if (BaseValue < 0)
                {
                    ValidationError = "Base Value cannot be negative.";
                    return;
                }
                if (MaxStack < 1)
                {
                    ValidationError = "Max Stack must be at least 1.";
                    return;
                }
                if (MaxSockets < 0)
                {
                    ValidationError = "Max Sockets cannot be negative.";
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

            var itemDb = DatabaseAutoIndexer.GetOrCreateCatalog<ItemDatabaseSO>("ItemDatabase");
            var indexedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (itemDb != null && itemDb.Items != null)
            {
                foreach (var item in itemDb.Items)
                {
                    if (item == null) continue;
                    var row = CreateRowFromAsset(item);
                    _rows.Add(row);
                    if (!string.IsNullOrEmpty(row.Id)) indexedIds.Add(row.Id);
                }
            }

            // Find all ItemDataSO assets to include unindexed items
            var itemGuids = AssetDatabase.FindAssets("t:ItemDataSO");
            foreach (var guid in itemGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
                if (asset == null || string.IsNullOrWhiteSpace(asset.Id)) continue;

                if (!indexedIds.Contains(asset.Id))
                {
                    var row = CreateRowFromAsset(asset);
                    _rows.Add(row);
                    indexedIds.Add(asset.Id);
                }
            }

            CheckForDuplicateIds();
        }

        private ItemRow CreateRowFromAsset(ItemDataSO asset)
        {
            var row = new ItemRow
            {
                Asset = asset,
                Id = asset.Id ?? "",
                DisplayName = asset.DisplayName ?? "",
                Icon = asset.Icon,
                Description = asset.Description ?? "",
                Type = asset.ItemType,
                SlotType = asset.EquippableSlotType,
                BaseValue = asset.BaseValue,
                MaxStack = asset.MaxStack,
                MaxSockets = asset.MaxSockets,
                StatModifiers = asset.StatModifiers != null ? new List<ItemStatModifierData>(asset.StatModifiers) : new List<ItemStatModifierData>()
            };
            row.Validate();
            return row;
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
                    r.ValidationError = $"Duplicate Item ID '{r.Id}' detected.";
                }
            }
        }

        public void SaveData()
        {
            const string defaultFolder = "Assets/ScriptableObjects/Items";
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }

            foreach (var row in _rows)
            {
                if (row.Asset == null)
                {
                    string assetPath = $"{defaultFolder}/{row.Id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<ItemDataSO>(assetPath);
                    if (existing == null)
                    {
                        existing = ScriptableObject.CreateInstance<ItemDataSO>();
                        AssetDatabase.CreateAsset(existing, assetPath);
                    }
                    row.Asset = existing;
                }

                if (row.Asset != null)
                {
                    var so = new SerializedObject(row.Asset);
                    so.FindProperty("itemID").stringValue = row.Id;
                    so.FindProperty("displayName").stringValue = row.DisplayName;
                    so.FindProperty("description").stringValue = row.Description;
                    so.FindProperty("icon").objectReferenceValue = row.Icon;
                    so.FindProperty("itemType").enumValueIndex = (int)row.Type;
                    so.FindProperty("equippableSlotType").enumValueIndex = (int)row.SlotType;
                    so.FindProperty("baseValue").intValue = row.BaseValue;
                    so.FindProperty("maxStack").intValue = row.MaxStack;
                    so.FindProperty("maxSockets").intValue = row.MaxSockets;

                    // Serialize Stat Modifiers list
                    var modifiersProp = so.FindProperty("statModifiers");
                    if (modifiersProp != null)
                    {
                        modifiersProp.ClearArray();
                        for (int m = 0; m < row.StatModifiers.Count; m++)
                        {
                            modifiersProp.InsertArrayElementAtIndex(m);
                            var elem = modifiersProp.GetArrayElementAtIndex(m);
                            var mod = row.StatModifiers[m];
                            elem.FindPropertyRelative("statType").enumValueIndex = (int)mod.StatType;
                            elem.FindPropertyRelative("value").floatValue = mod.Value;
                            elem.FindPropertyRelative("modType").enumValueIndex = (int)mod.ModType;
                            elem.FindPropertyRelative("orderOverride").intValue = mod.OrderOverride;
                        }
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(row.Asset);
                }

                row.IsDirty = false;
                row.Validate();
            }

            var itemDb = DatabaseAutoIndexer.GetOrCreateCatalog<ItemDatabaseSO>("ItemDatabase");
            itemDb.SetItemsEditor(_rows.Where(r => r.Asset != null).Select(r => r.Asset));
            EditorUtility.SetDirty(itemDb);

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
                GUILayout.Label("🔍", GUILayout.Width(18));
                _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(180));
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchQuery = "";
                    GUI.FocusControl(null);
                }

                EditorGUILayout.Space(6);

                var types = new[] { "All Types", "Consumable", "Rune", "Artifact", "Material", "Misc" };
                _typeFilterIndex = EditorGUILayout.Popup(_typeFilterIndex, types, EditorStyles.toolbarPopup, GUILayout.Width(100));

                _filterIssuesOnly = GUILayout.Toggle(_filterIssuesOnly, "⚠️ Issues Only", EditorStyles.toolbarButton, GUILayout.Width(95));

                GUILayout.FlexibleSpace();

                _showBulkMath = GUILayout.Toggle(_showBulkMath, "📐 Bulk Math", EditorStyles.toolbarButton, GUILayout.Width(90));

                EditorGUILayout.Space(4);

                if (GUILayout.Button("📤 Export CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    if (HasUnsavedChanges && EditorUtility.DisplayDialog("Unsaved Changes", "Save changes before exporting to CSV?", "Save & Export", "Export As-Is"))
                    {
                        SaveData();
                    }
                    DatabaseCsvPipeline.ExportItemsCsv();
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("CSV Export", "Items exported to Assets/DataTables/Items.csv", "OK");
                }

                if (GUILayout.Button("📥 Import CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    if (EditorUtility.DisplayDialog("Import CSV", "Importing will overwrite current in-editor values with CSV data. Proceed?", "Import", "Cancel"))
                    {
                        DatabaseCsvPipeline.ImportItemsCsv();
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
                    case BulkTargetField.BaseValue:
                        row.BaseValue = Mathf.Max(0, (int)CalculateAdjustedValue(row.BaseValue, _bulkValue, _bulkMode, 0));
                        break;
                    case BulkTargetField.MaxStack:
                        row.MaxStack = Mathf.Max(1, (int)CalculateAdjustedValue(row.MaxStack, _bulkValue, _bulkMode, 1));
                        break;
                    case BulkTargetField.MaxSockets:
                        row.MaxSockets = Mathf.Max(0, (int)CalculateAdjustedValue(row.MaxSockets, _bulkValue, _bulkMode, 0));
                        break;
                    case BulkTargetField.ModifierValues:
                        for (int m = 0; m < row.StatModifiers.Count; m++)
                        {
                            var mod = row.StatModifiers[m];
                            float adjusted = CalculateAdjustedValue(mod.Value, _bulkValue, _bulkMode, 0.01f);
                            row.StatModifiers[m] = new ItemStatModifierData(mod.StatType, adjusted, mod.ModType, mod.OrderOverride);
                        }
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

        private List<ItemRow> GetFilteredRows()
        {
            var list = _rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                list = list.Where(r =>
                    r.Id.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.DisplayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_typeFilterIndex > 0)
            {
                var targetType = (ItemType)(_typeFilterIndex - 1);
                list = list.Where(r => r.Type == targetType);
            }

            if (_filterIssuesOnly)
            {
                list = list.Where(r => r.HasAnyIssue);
            }

            return SortRows(list);
        }

        private List<ItemRow> SortRows(IEnumerable<ItemRow> rows)
        {
            return _currentSort switch
            {
                SortColumn.Id => _sortAscending ? rows.OrderBy(r => r.Id).ToList() : rows.OrderByDescending(r => r.Id).ToList(),
                SortColumn.Name => _sortAscending ? rows.OrderBy(r => r.DisplayName).ToList() : rows.OrderByDescending(r => r.DisplayName).ToList(),
                SortColumn.Type => _sortAscending ? rows.OrderBy(r => r.Type).ToList() : rows.OrderByDescending(r => r.Type).ToList(),
                SortColumn.Slot => _sortAscending ? rows.OrderBy(r => r.SlotType).ToList() : rows.OrderByDescending(r => r.SlotType).ToList(),
                SortColumn.BaseValue => _sortAscending ? rows.OrderBy(r => r.BaseValue).ToList() : rows.OrderByDescending(r => r.BaseValue).ToList(),
                SortColumn.MaxStack => _sortAscending ? rows.OrderBy(r => r.MaxStack).ToList() : rows.OrderByDescending(r => r.MaxStack).ToList(),
                SortColumn.MaxSockets => _sortAscending ? rows.OrderBy(r => r.MaxSockets).ToList() : rows.OrderByDescending(r => r.MaxSockets).ToList(),
                SortColumn.ModifiersCount => _sortAscending ? rows.OrderBy(r => r.StatModifiers.Count).ToList() : rows.OrderByDescending(r => r.StatModifiers.Count).ToList(),
                _ => rows.ToList()
            };
        }

        private void DrawTableHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool allSelected = _rows.Count > 0 && _rows.All(r => r.IsSelected);
                bool newAllSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(22));
                if (newAllSelected != allSelected)
                {
                    foreach (var r in _rows) r.IsSelected = newAllSelected;
                }

                GUILayout.Label("St", EditorStyles.boldLabel, GUILayout.Width(24));
                GUILayout.Label("Icon", EditorStyles.boldLabel, GUILayout.Width(30));
                DrawSortHeader("ID", SortColumn.Id, 130);
                DrawSortHeader("Display Name", SortColumn.Name, 130);
                DrawSortHeader("Type", SortColumn.Type, 75);
                DrawSortHeader("Slot", SortColumn.Slot, 75);
                DrawSortHeader("Value", SortColumn.BaseValue, 55);
                DrawSortHeader("Stack", SortColumn.MaxStack, 50);
                DrawSortHeader("Sockets", SortColumn.MaxSockets, 55);
                DrawSortHeader("Mods", SortColumn.ModifiersCount, 50);

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

                    row.IsSelected = EditorGUILayout.Toggle(row.IsSelected, GUILayout.Width(22));

                    var worst = row.WorstSeverity;
                    if (worst.HasValue)
                    {
                        string icon = worst.Value == ValidationSeverity.Error ? "❌" : "⚠️";
                        string tooltip = row.GetCombinedValidationTooltip();
                        GUILayout.Label(new GUIContent(icon, tooltip), GUILayout.Width(24));
                    }
                    else if (row.IsDirty)
                    {
                        GUILayout.Label(new GUIContent("●", "Unsaved edits"), GUILayout.Width(24));
                    }
                    else
                    {
                        GUILayout.Label("✓", GUILayout.Width(24));
                    }

                    // Icon preview
                    if (row.Icon != null && row.Icon.texture != null)
                    {
                        GUILayout.Label(row.Icon.texture, GUILayout.Width(26), GUILayout.Height(20));
                    }
                    else
                    {
                        GUILayout.Label("—", GUILayout.Width(26));
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

                    // Display Name
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField(row.DisplayName, GUILayout.Width(130));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.DisplayName = newName;
                        row.MarkDirty();
                    }

                    // Type
                    EditorGUI.BeginChangeCheck();
                    var newType = (ItemType)EditorGUILayout.EnumPopup(row.Type, GUILayout.Width(75));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Type = newType;
                        row.MarkDirty();
                    }

                    // Slot Type
                    EditorGUI.BeginChangeCheck();
                    var newSlot = (EquipmentSlotType)EditorGUILayout.EnumPopup(row.SlotType, GUILayout.Width(75));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.SlotType = newSlot;
                        row.MarkDirty();
                    }

                    // Value
                    EditorGUI.BeginChangeCheck();
                    int newVal = EditorGUILayout.IntField(row.BaseValue, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.BaseValue = Mathf.Max(0, newVal);
                        row.MarkDirty();
                    }

                    // Max Stack
                    EditorGUI.BeginChangeCheck();
                    int newStack = EditorGUILayout.IntField(row.MaxStack, GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.MaxStack = Mathf.Max(1, newStack);
                        row.MarkDirty();
                    }

                    // Max Sockets
                    EditorGUI.BeginChangeCheck();
                    int newSockets = EditorGUILayout.IntField(row.MaxSockets, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.MaxSockets = Mathf.Max(0, newSockets);
                        row.MarkDirty();
                    }

                    // Modifiers Count Badge
                    GUILayout.Label($"{row.StatModifiers.Count} mod(s)", EditorStyles.miniLabel, GUILayout.Width(50));

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(isSelectedRow ? "▼ Inspect" : "▶ Select", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        _selectedRow = isSelectedRow ? null : row;
                        if (_selectedRow != null && _selectedRow.Asset != null)
                        {
                            EditorGUIUtility.PingObject(_selectedRow.Asset);
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
                EditorGUILayout.LabelField($"🔍 Inspector: {_selectedRow.Id} ({_selectedRow.DisplayName})", EditorStyles.boldLabel);
                if (GUILayout.Button("Close Inspector ✕", EditorStyles.miniButton, GUILayout.Width(120)))
                {
                    _selectedRow = null;
                    return;
                }
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.Height(170));

            using (new EditorGUILayout.HorizontalScope())
            {
                // Left Column: Visuals & Description
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(350)))
                {
                    EditorGUILayout.LabelField("Asset & Description", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    var newAsset = (ItemDataSO)EditorGUILayout.ObjectField("Item SO", _selectedRow.Asset, typeof(ItemDataSO), false);
                    var newIcon = (Sprite)EditorGUILayout.ObjectField("Icon", _selectedRow.Icon, typeof(Sprite), false);
                    EditorGUILayout.LabelField("Description:");
                    string newDesc = EditorGUILayout.TextArea(_selectedRow.Description, GUILayout.Height(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.Asset = newAsset;
                        _selectedRow.Icon = newIcon;
                        _selectedRow.Description = newDesc;
                        _selectedRow.MarkDirty();
                    }
                }

                EditorGUILayout.Space(12);

                // Right Column: Stat Modifiers
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Static Stat Modifiers (Runes / Artifacts)", EditorStyles.boldLabel);

                    for (int m = 0; m < _selectedRow.StatModifiers.Count; m++)
                    {
                        var mod = _selectedRow.StatModifiers[m];
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                        {
                            EditorGUI.BeginChangeCheck();
                            var newStatType = (StatType)EditorGUILayout.EnumPopup(mod.StatType, GUILayout.Width(110));
                            float newVal = EditorGUILayout.FloatField(mod.Value, GUILayout.Width(60));
                            var newModType = (StatModType)EditorGUILayout.EnumPopup(mod.ModType, GUILayout.Width(95));
                            int newOrder = EditorGUILayout.IntField(mod.OrderOverride, GUILayout.Width(35));

                            if (EditorGUI.EndChangeCheck())
                            {
                                _selectedRow.StatModifiers[m] = new ItemStatModifierData(newStatType, newVal, newModType, newOrder);
                                _selectedRow.MarkDirty();
                            }

                            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                            {
                                _selectedRow.StatModifiers.RemoveAt(m);
                                _selectedRow.MarkDirty();
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Add Stat Modifier", EditorStyles.miniButton, GUILayout.Width(140)))
                    {
                        _selectedRow.StatModifiers.Add(new ItemStatModifierData(StatType.Attack, 5f, StatModType.Flat));
                        _selectedRow.MarkDirty();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
