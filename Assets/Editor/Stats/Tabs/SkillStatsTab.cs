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
    public class SkillStatsTab : IStatTab
    {
        public string Title => "⚡ Skills";

        private StatBalanceManagerWindow _window;
        private List<SkillRow> _rows = new();
        private SkillRow _selectedRow;
        private Vector2 _tableScroll;
        private Vector2 _detailScroll;

        // Search & Filter
        private string _searchQuery = "";
        private int _typeFilterIndex = 0; // 0 = All
        private bool _filterIssuesOnly = false;

        // Sorting
        public enum SortColumn { Id, Name, Type, DamageType, BaseDamage, ManaCost, CastRange, AreaRadius, ProjectileSpeed }
        private SortColumn _currentSort = SortColumn.Id;
        private bool _sortAscending = true;

        // Bulk Math Panel
        private bool _showBulkMath = false;
        public enum BulkScope { Filtered, SelectedOnly }
        public enum BulkTargetField { BaseDamage, BaseManaCost, CastRange, ProjectileSpeed, AreaRadius, PostCastRecovery }
        public enum BulkMathMode { PercentMultiply, FlatAdd }

        private BulkScope _bulkScope = BulkScope.Filtered;
        private BulkTargetField _bulkTargetField = BulkTargetField.BaseDamage;
        private BulkMathMode _bulkMode = BulkMathMode.PercentMultiply;
        private float _bulkValue = 10f;

        public bool HasUnsavedChanges => _rows.Any(r => r.IsDirty);
        public int ValidationIssueCount => _rows.Count(r => !string.IsNullOrEmpty(r.ValidationError));

        public class SkillRow
        {
            public SkillData Asset;

            // Identity
            public string Id = "";
            public string Name = "";
            public Sprite Icon;
            public string Description = "";
            public GameObject Prefab;

            // Stats
            public SkillType Type = SkillType.Default;
            public SkillData.SkillTargetFilter TargetFilter = SkillData.SkillTargetFilter.EnemiesOnly;
            public DamageType DamageType = DamageType.Physical;
            public float BaseDamage = 10f;
            public float BaseManaCost = 10f;
            public float CastRange = 0f;

            // Projectile
            public float ProjectileSpeed = 10f;
            public float ProjectileLifetime = 5f;

            // Chain
            public bool Chain = false;
            public int MaxChainTargets = 0;
            public float ChainDamageMultiplier = 0.7f;

            // Area & Beam
            public bool Area = false;
            public float AreaRadius = 0f;
            public float BeamTickInterval = 0.1f;

            // Recovery
            public bool UseCustomRecovery = true;
            public float PostCastRecoveryDuration = 0.35f;

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
                    ValidationError = "Skill ID is missing.";
                    return;
                }
                if (BaseDamage < 0f)
                {
                    ValidationError = "Base Damage cannot be negative.";
                    return;
                }
                if (BaseManaCost < 0f)
                {
                    ValidationError = "Base Mana Cost cannot be negative.";
                    return;
                }
                if (CastRange < 0f)
                {
                    ValidationError = "Cast Range cannot be negative.";
                    return;
                }
                if (Chain && MaxChainTargets < 1)
                {
                    ValidationError = "Chain enabled but Max Chain Targets is less than 1.";
                    return;
                }
                if (Area && AreaRadius <= 0f)
                {
                    ValidationError = "Area enabled but Area Radius is 0 or less.";
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

            var skillDb = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            var indexedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (skillDb != null && skillDb.Skills != null)
            {
                foreach (var skill in skillDb.Skills)
                {
                    if (skill == null) continue;
                    var row = CreateRowFromAsset(skill);
                    _rows.Add(row);
                    if (!string.IsNullOrEmpty(row.Id)) indexedIds.Add(row.Id);
                }
            }

            // Also check all SkillData assets on disk
            var skillGuids = AssetDatabase.FindAssets("t:SkillData");
            foreach (var guid in skillGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (asset == null || string.IsNullOrWhiteSpace(asset.skillID)) continue;

                if (!indexedIds.Contains(asset.skillID))
                {
                    var row = CreateRowFromAsset(asset);
                    _rows.Add(row);
                    indexedIds.Add(asset.skillID);
                }
            }

            CheckForDuplicateIds();
        }

        private SkillRow CreateRowFromAsset(SkillData asset)
        {
            var row = new SkillRow
            {
                Asset = asset,
                Id = asset.skillID ?? "",
                Name = asset.skillName ?? "",
                Icon = asset.icon,
                Description = asset.description ?? "",
                Prefab = asset.skillPrefab,
                Type = asset.type,
                TargetFilter = asset.targetFilter,
                DamageType = asset.damageType,
                BaseDamage = asset.baseDamage,
                BaseManaCost = asset.baseManaCost,
                CastRange = asset.castRange,
                ProjectileSpeed = asset.projectileSpeed,
                ProjectileLifetime = asset.projectileLifetime,
                Chain = asset.chain,
                MaxChainTargets = asset.maxChainTargets,
                ChainDamageMultiplier = asset.chainDamageMultiplier,
                Area = asset.area,
                AreaRadius = asset.areaRadius,
                BeamTickInterval = asset.beamTickInterval,
                UseCustomRecovery = asset.useCustomRecoveryDuration,
                PostCastRecoveryDuration = asset.postCastRecoveryDuration
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
                    r.ValidationError = $"Duplicate Skill ID '{r.Id}' detected.";
                }
            }
        }

        public void SaveData()
        {
            const string defaultFolder = "Assets/ScriptableObjects/Skills";
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }

            foreach (var row in _rows)
            {
                if (row.Asset == null)
                {
                    string assetPath = $"{defaultFolder}/{row.Id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
                    if (existing == null)
                    {
                        existing = ScriptableObject.CreateInstance<SkillData>();
                        AssetDatabase.CreateAsset(existing, assetPath);
                    }
                    row.Asset = existing;
                }

                if (row.Asset != null)
                {
                    row.Asset.skillID = row.Id;
                    row.Asset.skillName = row.Name;
                    row.Asset.icon = row.Icon;
                    row.Asset.description = row.Description;
                    row.Asset.skillPrefab = row.Prefab;
                    row.Asset.type = row.Type;
                    row.Asset.targetFilter = row.TargetFilter;
                    row.Asset.damageType = row.DamageType;
                    row.Asset.baseDamage = row.BaseDamage;
                    row.Asset.baseManaCost = row.BaseManaCost;
                    row.Asset.castRange = row.CastRange;
                    row.Asset.projectileSpeed = row.ProjectileSpeed;
                    row.Asset.projectileLifetime = row.ProjectileLifetime;
                    row.Asset.chain = row.Chain;
                    row.Asset.maxChainTargets = row.MaxChainTargets;
                    row.Asset.chainDamageMultiplier = row.ChainDamageMultiplier;
                    row.Asset.area = row.Area;
                    row.Asset.areaRadius = row.AreaRadius;
                    row.Asset.beamTickInterval = row.BeamTickInterval;
                    row.Asset.useCustomRecoveryDuration = row.UseCustomRecovery;
                    row.Asset.postCastRecoveryDuration = row.PostCastRecoveryDuration;

                    EditorUtility.SetDirty(row.Asset);
                }

                row.IsDirty = false;
                row.Validate();
            }

            var skillDb = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            skillDb.SetSkillsEditor(_rows.Where(r => r.Asset != null).Select(r => r.Asset).ToList());
            EditorUtility.SetDirty(skillDb);

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

                var types = new[] { "All Types", "Default", "Projectile", "Area", "Channel", "Buff", "Summon" };
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
                    DatabaseCsvPipeline.ExportSkillsCsv();
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("CSV Export", "Skills exported to Assets/DataTables/Skills.csv", "OK");
                }

                if (GUILayout.Button("📥 Import CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
                {
                    if (EditorUtility.DisplayDialog("Import CSV", "Importing will overwrite current in-editor values with CSV data. Proceed?", "Import", "Cancel"))
                    {
                        DatabaseCsvPipeline.ImportSkillsCsv();
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
                    case BulkTargetField.BaseDamage:
                        row.BaseDamage = CalculateAdjustedValue(row.BaseDamage, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.BaseManaCost:
                        row.BaseManaCost = CalculateAdjustedValue(row.BaseManaCost, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.CastRange:
                        row.CastRange = CalculateAdjustedValue(row.CastRange, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.ProjectileSpeed:
                        row.ProjectileSpeed = CalculateAdjustedValue(row.ProjectileSpeed, _bulkValue, _bulkMode, 0.5f);
                        break;
                    case BulkTargetField.AreaRadius:
                        row.AreaRadius = CalculateAdjustedValue(row.AreaRadius, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.PostCastRecovery:
                        row.PostCastRecoveryDuration = CalculateAdjustedValue(row.PostCastRecoveryDuration, _bulkValue, _bulkMode, 0.05f);
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

        private List<SkillRow> GetFilteredRows()
        {
            var list = _rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                list = list.Where(r =>
                    r.Id.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.Name.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (_typeFilterIndex > 0)
            {
                var targetType = (SkillType)(_typeFilterIndex - 1);
                list = list.Where(r => r.Type == targetType);
            }

            if (_filterIssuesOnly)
            {
                list = list.Where(r => !string.IsNullOrEmpty(r.ValidationError));
            }

            return SortRows(list);
        }

        private List<SkillRow> SortRows(IEnumerable<SkillRow> rows)
        {
            return _currentSort switch
            {
                SortColumn.Id => _sortAscending ? rows.OrderBy(r => r.Id).ToList() : rows.OrderByDescending(r => r.Id).ToList(),
                SortColumn.Name => _sortAscending ? rows.OrderBy(r => r.Name).ToList() : rows.OrderByDescending(r => r.Name).ToList(),
                SortColumn.Type => _sortAscending ? rows.OrderBy(r => r.Type).ToList() : rows.OrderByDescending(r => r.Type).ToList(),
                SortColumn.DamageType => _sortAscending ? rows.OrderBy(r => r.DamageType).ToList() : rows.OrderByDescending(r => r.DamageType).ToList(),
                SortColumn.BaseDamage => _sortAscending ? rows.OrderBy(r => r.BaseDamage).ToList() : rows.OrderByDescending(r => r.BaseDamage).ToList(),
                SortColumn.ManaCost => _sortAscending ? rows.OrderBy(r => r.BaseManaCost).ToList() : rows.OrderByDescending(r => r.BaseManaCost).ToList(),
                SortColumn.CastRange => _sortAscending ? rows.OrderBy(r => r.CastRange).ToList() : rows.OrderByDescending(r => r.CastRange).ToList(),
                SortColumn.AreaRadius => _sortAscending ? rows.OrderBy(r => r.AreaRadius).ToList() : rows.OrderByDescending(r => r.AreaRadius).ToList(),
                SortColumn.ProjectileSpeed => _sortAscending ? rows.OrderBy(r => r.ProjectileSpeed).ToList() : rows.OrderByDescending(r => r.ProjectileSpeed).ToList(),
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
                DrawSortHeader("Skill Name", SortColumn.Name, 130);
                DrawSortHeader("Type", SortColumn.Type, 75);
                DrawSortHeader("Element", SortColumn.DamageType, 70);
                DrawSortHeader("Damage", SortColumn.BaseDamage, 60);
                DrawSortHeader("Mana", SortColumn.ManaCost, 55);
                DrawSortHeader("Range", SortColumn.CastRange, 55);
                DrawSortHeader("Area Rad", SortColumn.AreaRadius, 60);

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

                    // Name
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.TextField(row.Name, GUILayout.Width(130));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Name = newName;
                        row.MarkDirty();
                    }

                    // Type
                    EditorGUI.BeginChangeCheck();
                    var newType = (SkillType)EditorGUILayout.EnumPopup(row.Type, GUILayout.Width(75));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.Type = newType;
                        row.MarkDirty();
                    }

                    // Damage Type
                    EditorGUI.BeginChangeCheck();
                    var newDmgType = (DamageType)EditorGUILayout.EnumPopup(row.DamageType, GUILayout.Width(70));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.DamageType = newDmgType;
                        row.MarkDirty();
                    }

                    // Base Damage
                    EditorGUI.BeginChangeCheck();
                    float newDmg = EditorGUILayout.FloatField(row.BaseDamage, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.BaseDamage = Mathf.Max(0f, newDmg);
                        row.MarkDirty();
                    }

                    // Mana Cost
                    EditorGUI.BeginChangeCheck();
                    float newMana = EditorGUILayout.FloatField(row.BaseManaCost, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.BaseManaCost = Mathf.Max(0f, newMana);
                        row.MarkDirty();
                    }

                    // Cast Range
                    EditorGUI.BeginChangeCheck();
                    float newRange = EditorGUILayout.FloatField(row.CastRange, GUILayout.Width(55));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.CastRange = Mathf.Max(0f, newRange);
                        row.MarkDirty();
                    }

                    // Area Radius
                    EditorGUI.BeginChangeCheck();
                    float newRadius = EditorGUILayout.FloatField(row.AreaRadius, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.AreaRadius = Mathf.Max(0f, newRadius);
                        if (row.AreaRadius > 0f) row.Area = true;
                        row.MarkDirty();
                    }

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
                // Left Column: Asset & Description & Target Filter
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(350)))
                {
                    EditorGUILayout.LabelField("Asset & Targeting", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    var newAsset = (SkillData)EditorGUILayout.ObjectField("Skill SO", _selectedRow.Asset, typeof(SkillData), false);
                    var newIcon = (Sprite)EditorGUILayout.ObjectField("Icon", _selectedRow.Icon, typeof(Sprite), false);
                    var newPrefab = (GameObject)EditorGUILayout.ObjectField("Skill Prefab", _selectedRow.Prefab, typeof(GameObject), false);
                    var newTargetFilter = (SkillData.SkillTargetFilter)EditorGUILayout.EnumPopup("Target Filter", _selectedRow.TargetFilter);
                    EditorGUILayout.LabelField("Description:");
                    string newDesc = EditorGUILayout.TextArea(_selectedRow.Description, GUILayout.Height(35));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.Asset = newAsset;
                        _selectedRow.Icon = newIcon;
                        _selectedRow.Prefab = newPrefab;
                        _selectedRow.TargetFilter = newTargetFilter;
                        _selectedRow.Description = newDesc;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string genReason = null;
                        bool canGen = _selectedRow.Asset != null && SkillEffectPrefabGenerator.CanGenerate(_selectedRow.Asset, out genReason);
                        using (new EditorGUI.DisabledScope(!canGen))
                        {
                            if (GUILayout.Button("⚡ Regenerate Prefab", EditorStyles.miniButton))
                            {
                                bool proceed = true;
                                if (_selectedRow.Asset.skillPrefab != null)
                                {
                                    bool hasCustom = SkillEffectPrefabGenerator.HasCustomModifications(_selectedRow.Asset.skillPrefab, _selectedRow.Asset);
                                    string warn = hasCustom
                                        ? $"The existing prefab '{_selectedRow.Asset.skillPrefab.name}' has custom modifications (sprites/animators/scripts). Overwriting it will reset it to the template.\n\nDo you want to proceed?"
                                        : $"Regenerate effect prefab for '{_selectedRow.Id}' from template?";
                                    proceed = EditorUtility.DisplayDialog("Regenerate Skill Prefab", warn, "Regenerate", "Cancel");
                                }

                                if (proceed)
                                {
                                    var generated = SkillEffectPrefabGenerator.GenerateEffectPrefab(_selectedRow.Asset, overwrite: true);
                                    if (generated != null)
                                    {
                                        _selectedRow.Prefab = generated;
                                        _selectedRow.MarkDirty();
                                        if (SkillEffectPrefabGenerator.ValidatePrefab(generated, _selectedRow.Asset, out var valErrors))
                                        {
                                            EditorUtility.DisplayDialog("Success", $"Regenerated prefab for '{_selectedRow.Id}' successfully.", "OK");
                                        }
                                        else
                                        {
                                            EditorUtility.DisplayDialog("Sanity Warning", $"Prefab generated with warnings:\n• " + string.Join("\n• ", valErrors), "OK");
                                        }
                                    }
                                }
                            }
                        }

                        if (!canGen && !string.IsNullOrEmpty(genReason))
                        {
                            EditorGUILayout.HelpBox(genReason, MessageType.Warning);
                        }
                    }
                }

                EditorGUILayout.Space(12);

                // Middle Column: Projectile & Recovery
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(260)))
                {
                    EditorGUILayout.LabelField("Projectile Settings", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    float newSpd = EditorGUILayout.FloatField("Projectile Speed", _selectedRow.ProjectileSpeed);
                    float newLife = EditorGUILayout.FloatField("Lifetime (s)", _selectedRow.ProjectileLifetime);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.ProjectileSpeed = Mathf.Max(0f, newSpd);
                        _selectedRow.ProjectileLifetime = Mathf.Max(0.1f, newLife);
                        _selectedRow.MarkDirty();
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Recovery Duration", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    bool newCustomRec = EditorGUILayout.Toggle("Custom Recovery", _selectedRow.UseCustomRecovery);
                    float newRecDur = EditorGUILayout.FloatField("Post-Cast (s)", _selectedRow.PostCastRecoveryDuration);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.UseCustomRecovery = newCustomRec;
                        _selectedRow.PostCastRecoveryDuration = Mathf.Max(0f, newRecDur);
                        _selectedRow.MarkDirty();
                    }
                }

                EditorGUILayout.Space(12);

                // Right Column: Chain & Area & Beam
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Chain & Area Settings", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    bool newChain = EditorGUILayout.Toggle("Enable Chain", _selectedRow.Chain);
                    int newChainTargets = EditorGUILayout.IntField("Max Chain Targets", _selectedRow.MaxChainTargets);
                    float newChainMult = EditorGUILayout.FloatField("Chain Multiplier", _selectedRow.ChainDamageMultiplier);

                    EditorGUILayout.Space(4);
                    bool newArea = EditorGUILayout.Toggle("Enable Area", _selectedRow.Area);
                    float newBeamTick = EditorGUILayout.FloatField("Beam Tick Interval", _selectedRow.BeamTickInterval);

                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.Chain = newChain;
                        _selectedRow.MaxChainTargets = Mathf.Max(0, newChainTargets);
                        _selectedRow.ChainDamageMultiplier = Mathf.Clamp01(newChainMult);
                        _selectedRow.Area = newArea;
                        _selectedRow.BeamTickInterval = Mathf.Max(0.01f, newBeamTick);
                        _selectedRow.MarkDirty();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
