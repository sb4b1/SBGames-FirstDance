using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SBGames.Database;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using SBGames.Editor.Validation;
using UnityEditor;
using UnityEditorInternal;
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
        private ReorderableList _skillList;
        private CharacterRow _skillListRow;

        // Search & Filter
        private string _searchQuery = "";
        private int _rankFilterIndex = 0; // 0 = All
        private bool _filterIssuesOnly = false;

        // Sorting
        public enum SortColumn { Id, Name, Rank, AtkMode, Level, MaxLife, MaxMana, LifeRegen, ManaRegen, Movespeed, Damage, AttackRange, AttackInterval }
        private SortColumn _currentSort = SortColumn.Id;
        private bool _sortAscending = true;

        // Bulk Math Panel
        private bool _showBulkMath = false;
        public enum BulkScope { Filtered, SelectedOnly }
        public enum BulkTargetField { MaxLife, MaxMana, LifeRegen, ManaRegen, Movespeed, Damage, AttackRange, AttackInterval, DecisionInterval }
        public enum BulkMathMode { PercentMultiply, FlatAdd }

        private BulkScope _bulkScope = BulkScope.Filtered;
        private BulkTargetField _bulkTargetField = BulkTargetField.MaxLife;
        private BulkMathMode _bulkMode = BulkMathMode.PercentMultiply;
        private float _bulkValue = 10f; // e.g. +10%

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
                ? findings.Where(f => f.SubjectType == ValidationSubjectType.Character && !string.IsNullOrEmpty(f.SubjectId))
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

        public class CharacterRow
        {
            public CharacterEntry Entry;
            public CharacterBaseStatSO BaseStatsSO;

            // Identity
            public string OriginalId = "";
            public string Id = "";
            public string Name = "";
            public GameObject Prefab;

            // Stats
            public int Level = 1;
            public CharacterRank Rank = CharacterRank.D;
            public BasicAttackMode BasicAttackMode = BasicAttackMode.Auto;
            public KeepDistanceMode KeepDistanceMode = KeepDistanceMode.Auto;
            public float ComfortDistanceOverride = 0f;
            public float MaxLife = 100f;
            public float MaxMana = 50f;
            public float LifeRegen = 0f;
            public float ManaRegen = 0f;
            public float Movespeed = 5f;
            public float Damage = 10f;
            public float AttackRange = 1f;
            public float AttackTimeInterval = 1f;

            // Recovery & AI
            public float PostAttackRecovery = 0.3f;
            public float PostCastRecovery = 0.3f;
            public float DecisionInterval = 0.3f;
            public float IntervalJitter = 0.05f;

            // Skills: Default Skill IDs are the source of truth; prefab skills are derived on Save All.
            public PendingSkillEdits SkillEdits = new(null);
            public List<string> DefaultSkillIds => SkillEdits.Ids;

            // State
            public bool IsSelected = false;
            public bool IsDirty = false;
            public string ValidationError = "";

            /// <summary>Prefab/ID drift warning (Validation Badge). Reflects the saved prefab vs. the current IDs.</summary>
            public string SkillSyncWarning = "";
            public bool HasSkillSyncFixes;

            public List<ValidationFinding> ExternalFindings = new();

            public ValidationSeverity? WorstSeverity
            {
                get
                {
                    bool hasError = !string.IsNullOrEmpty(ValidationError) ||
                                    ExternalFindings.Any(f => f.Severity == ValidationSeverity.Error);
                    if (hasError) return ValidationSeverity.Error;

                    bool hasWarning = !string.IsNullOrEmpty(SkillSyncWarning) ||
                                      ExternalFindings.Any(f => f.Severity == ValidationSeverity.Warning);
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
                if (!string.IsNullOrEmpty(SkillSyncWarning))
                {
                    lines.Add($"[AI Setup] {SkillSyncWarning}");
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

            public void RefreshSkillSync()
            {
                var issues = CharacterPrefabSkillSync.Analyze(Prefab, DefaultSkillIds, CharacterPrefabSkillSync.SkillExistsInDatabase);
                SkillSyncWarning = CharacterPrefabSkillSync.Summarize(issues);
                HasSkillSyncFixes = issues.Exists(i => i.IsFixableBySync);
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
                if (LifeRegen < 0f)
                {
                    ValidationError = "LifeRegen cannot be negative.";
                    return;
                }
                if (ManaRegen < 0f)
                {
                    ValidationError = "ManaRegen cannot be negative.";
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
                if (BasicAttackMode == BasicAttackMode.None)
                {
                    bool hasPrefabSkills = Prefab != null && Prefab.GetComponentInChildren<CharacterSkillController>() != null &&
                                          Prefab.GetComponentInChildren<CharacterSkillController>().Skills != null &&
                                          Prefab.GetComponentInChildren<CharacterSkillController>().Skills.Count > 0;
                    bool hasConfiguredSkills = DefaultSkillIds != null && DefaultSkillIds.Any(s => !string.IsNullOrWhiteSpace(s));
                    if (!hasPrefabSkills && !hasConfiguredSkills)
                    {
                        ValidationError = "BasicAttackMode is None but character has no usable skill.";
                        return;
                    }
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
                        OriginalId = entry.id ?? "",
                        Id = entry.id ?? "",
                        Name = entry.characterName ?? "",
                        Prefab = entry.GetPrefabDirect(),
                        SkillEdits = new PendingSkillEdits(entry.defaultSkillIds)
                    };

                    if (row.BaseStatsSO != null)
                    {
                        row.Level = row.BaseStatsSO.characterLevel;
                        row.Rank = row.BaseStatsSO.characterRank;
                        row.BasicAttackMode = row.BaseStatsSO.basicAttackMode;
                        row.KeepDistanceMode = row.BaseStatsSO.keepDistanceMode;
                        row.ComfortDistanceOverride = row.BaseStatsSO.comfortDistanceOverride;
                        row.MaxLife = row.BaseStatsSO.MaxLife;
                        row.MaxMana = row.BaseStatsSO.MaxMana;
                        row.LifeRegen = row.BaseStatsSO.LifeRegen;
                        row.ManaRegen = row.BaseStatsSO.ManaRegen;
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
                    row.RefreshSkillSync();
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
                    OriginalId = assetName,
                    Id = assetName,
                    Name = assetName,
                    Level = statAsset.characterLevel,
                    Rank = statAsset.characterRank,
                    BasicAttackMode = statAsset.basicAttackMode,
                    KeepDistanceMode = statAsset.keepDistanceMode,
                    ComfortDistanceOverride = statAsset.comfortDistanceOverride,
                    MaxLife = statAsset.MaxLife,
                    MaxMana = statAsset.MaxMana,
                    LifeRegen = statAsset.LifeRegen,
                    ManaRegen = statAsset.ManaRegen,
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
                    row.BaseStatsSO.basicAttackMode = row.BasicAttackMode;
                    row.BaseStatsSO.keepDistanceMode = row.KeepDistanceMode;
                    row.BaseStatsSO.comfortDistanceOverride = row.ComfortDistanceOverride;
                    row.BaseStatsSO.MaxLife = row.MaxLife;
                    row.BaseStatsSO.MaxMana = row.MaxMana;
                    row.BaseStatsSO.LifeRegen = Mathf.Max(0f, row.LifeRegen);
                    row.BaseStatsSO.ManaRegen = Mathf.Max(0f, row.ManaRegen);
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
            CommitPendingPrefabSkills();
            AssetDatabase.Refresh();
            CheckForDuplicateIds();
        }

        /// <summary>
        /// Applies queued skill edits to each affected character prefab. Never throws; a failing
        /// character is logged, left dirty for retry, and the remaining characters continue.
        /// </summary>
        private void CommitPendingPrefabSkills()
        {
            CharacterPrefabSkillSync.ClearCasterPrefabCache();
            foreach (var row in _rows)
            {
                if (!row.SkillEdits.HasPrefabChanges)
                {
                    row.RefreshSkillSync();
                    continue;
                }

                if (row.Prefab == null)
                {
                    Debug.LogWarning($"[CharacterStatsTab] '{row.Id}' has no prefab assigned; prefab skills were not synced.");
                    row.SkillEdits.MarkSaved();
                    row.RefreshSkillSync();
                    continue;
                }

                var result = row.SkillEdits.Commit(row.Prefab, CharacterPrefabSkillSync.ResolveCasterPrefab, ConfirmSkillChildDelete);
                string log = $"[CharacterStatsTab] Prefab skill sync for '{row.Id}':\n" + string.Join("\n", result.Messages.Select(m => "• " + m));
                if (result.Success)
                {
                    if (result.Messages.Count > 0) Debug.Log(log, row.Prefab);
                }
                else
                {
                    Debug.LogError(log, row.Prefab);
                    row.IsDirty = true;
                }
                row.RefreshSkillSync();
            }
        }

        private static bool ConfirmSkillChildDelete(GameObject child, string reason) =>
            EditorUtility.DisplayDialog(
                "Delete Skill Object?",
                $"The skill object '{child.name}' under {CharacterPrefabSkillSync.SkillListName} will be deleted, but {reason}.\n\nDelete it anyway?",
                "Delete",
                "Keep");

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

            if (_selectedRow != null && _selectedRow.ExternalFindings.Count > 0)
            {
                EditorGUILayout.Space(2);
                DrawExternalFindingsPanel(_selectedRow);
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

                // Bulk prefab skill sync (selected rows)
                int selectedCount = _rows.Count(r => r.IsSelected);
                using (new EditorGUI.DisabledScope(selectedCount == 0))
                {
                    var syncContent = new GUIContent($"🔗 Sync Skills ({selectedCount})",
                        "Queue prefab skill sync for selected rows. Applied to prefabs on Save All.");
                    if (GUILayout.Button(syncContent, EditorStyles.toolbarButton, GUILayout.Width(115)))
                    {
                        foreach (var r in _rows)
                        {
                            if (!r.IsSelected) continue;
                            r.SkillEdits.RequestSync();
                            r.MarkDirty();
                        }
                    }

                    var prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    var deleteContent = new GUIContent($"🗑️ Delete ({selectedCount})",
                        "Delete selected characters from database and delete their linked prefab/BaseStats assets from disk.");
                    if (GUILayout.Button(deleteContent, EditorStyles.toolbarButton, GUILayout.Width(105)))
                    {
                        var selectedRows = _rows.Where(r => r.IsSelected).ToList();
                        DeleteCharacters(selectedRows);
                    }
                    GUI.backgroundColor = prevColor;
                }

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
                    case BulkTargetField.LifeRegen:
                        row.LifeRegen = CalculateAdjustedValue(row.LifeRegen, _bulkValue, _bulkMode, 0f);
                        break;
                    case BulkTargetField.ManaRegen:
                        row.ManaRegen = CalculateAdjustedValue(row.ManaRegen, _bulkValue, _bulkMode, 0f);
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
                list = list.Where(r => r.HasAnyIssue);
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
                SortColumn.AtkMode => _sortAscending ? rows.OrderBy(r => r.BasicAttackMode).ToList() : rows.OrderByDescending(r => r.BasicAttackMode).ToList(),
                SortColumn.Level => _sortAscending ? rows.OrderBy(r => r.Level).ToList() : rows.OrderByDescending(r => r.Level).ToList(),
                SortColumn.MaxLife => _sortAscending ? rows.OrderBy(r => r.MaxLife).ToList() : rows.OrderByDescending(r => r.MaxLife).ToList(),
                SortColumn.MaxMana => _sortAscending ? rows.OrderBy(r => r.MaxMana).ToList() : rows.OrderByDescending(r => r.MaxMana).ToList(),
                SortColumn.LifeRegen => _sortAscending ? rows.OrderBy(r => r.LifeRegen).ToList() : rows.OrderByDescending(r => r.LifeRegen).ToList(),
                SortColumn.ManaRegen => _sortAscending ? rows.OrderBy(r => r.ManaRegen).ToList() : rows.OrderByDescending(r => r.ManaRegen).ToList(),
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
                DrawSortHeader("AtkMode", SortColumn.AtkMode, 65);
                DrawSortHeader("Lvl", SortColumn.Level, 45);
                DrawSortHeader("HP", SortColumn.MaxLife, 65);
                DrawSortHeader("Mana", SortColumn.MaxMana, 55);
                DrawSortHeader("LifeRegen", SortColumn.LifeRegen, 65);
                DrawSortHeader("ManaRegen", SortColumn.ManaRegen, 65);
                DrawSortHeader("Spd", SortColumn.Movespeed, 55);
                DrawSortHeader("Dmg", SortColumn.Damage, 55);
                DrawSortHeader("Range", SortColumn.AttackRange, 55);
                DrawSortHeader("Interval", SortColumn.AttackInterval, 60);

                GUILayout.FlexibleSpace();
                GUILayout.Label("Actions", EditorStyles.boldLabel, GUILayout.Width(88));
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

                    // Basic Attack Mode
                    EditorGUI.BeginChangeCheck();
                    var newMode = (BasicAttackMode)EditorGUILayout.EnumPopup(row.BasicAttackMode, GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.BasicAttackMode = newMode;
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

                    // Life Regen
                    EditorGUI.BeginChangeCheck();
                    float newLifeRegen = EditorGUILayout.FloatField(row.LifeRegen, GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.LifeRegen = newLifeRegen;
                        row.MarkDirty();
                    }

                    // Mana Regen
                    EditorGUI.BeginChangeCheck();
                    float newManaRegen = EditorGUILayout.FloatField(row.ManaRegen, GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck())
                    {
                        row.ManaRegen = newManaRegen;
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
                    if (GUILayout.Button(isSelectedRow ? "▼ Inspect" : "▶ Select", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                    {
                        _selectedRow = isSelectedRow ? null : row;
                        if (_selectedRow != null && _selectedRow.BaseStatsSO != null)
                        {
                            EditorGUIUtility.PingObject(_selectedRow.BaseStatsSO);
                        }
                    }

                    // Delete button
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                    {
                        DeleteCharacters(new List<CharacterRow> { row });
                        GUIUtility.ExitGUI();
                    }
                    GUI.backgroundColor = prevBg;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawExternalFindingsPanel(CharacterRow row)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"🔍 Validation Findings for '{row.Id}' ({row.ExternalFindings.Count})", EditorStyles.boldLabel);
                foreach (var finding in row.ExternalFindings)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string icon = finding.Severity switch
                        {
                            ValidationSeverity.Error => "❌ [ERROR]",
                            ValidationSeverity.Warning => "⚠️ [WARNING]",
                            _ => "ℹ️ [INFO]"
                        };
                        GUILayout.Label($"{icon} [{finding.Source}] {finding.Message}", EditorStyles.wordWrappedMiniLabel);

                        GUILayout.FlexibleSpace();

                        if (finding.FixAction != null)
                        {
                            if (GUILayout.Button("Fix", EditorStyles.miniButton, GUILayout.Width(50)))
                            {
                                finding.FixAction.Invoke();
                            }
                        }
                        else if (!string.IsNullOrEmpty(finding.AssetPath))
                        {
                            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55)))
                            {
                                var asset = AssetDatabase.LoadMainAssetAtPath(finding.AssetPath);
                                if (asset != null) EditorGUIUtility.PingObject(asset);
                            }
                        }
                    }
                }
            }
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
                        _selectedRow.RefreshSkillSync();
                    }

                    EditorGUI.BeginChangeCheck();
                    var newSo = (CharacterBaseStatSO)EditorGUILayout.ObjectField("BaseStats SO", _selectedRow.BaseStatsSO, typeof(CharacterBaseStatSO), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.BaseStatsSO = newSo;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUI.BeginChangeCheck();
                    var newInspectorMode = (BasicAttackMode)EditorGUILayout.EnumPopup("Basic Attack Mode", _selectedRow.BasicAttackMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.BasicAttackMode = newInspectorMode;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUI.BeginChangeCheck();
                    var newKeepMode = (KeepDistanceMode)EditorGUILayout.EnumPopup("Keep Distance Mode", _selectedRow.KeepDistanceMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.KeepDistanceMode = newKeepMode;
                        _selectedRow.MarkDirty();
                    }

                    EditorGUI.BeginChangeCheck();
                    float newComfortOverride = EditorGUILayout.FloatField("Comfort Distance Override", _selectedRow.ComfortDistanceOverride);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _selectedRow.ComfortDistanceOverride = Mathf.Max(0f, newComfortOverride);
                        _selectedRow.MarkDirty();
                    }

                    // Display resolved Auto status
                    float longestSkillRange = 0f;
                    if (_selectedRow.Prefab != null)
                    {
                        var skCtrl = _selectedRow.Prefab.GetComponentInChildren<CharacterSkillController>(true);
                        if (skCtrl != null && skCtrl.Skills != null)
                        {
                            foreach (var sk in skCtrl.Skills)
                            {
                                if (sk != null && sk.Data != null && sk.Data.EffectiveRange > longestSkillRange)
                                    longestSkillRange = sk.Data.EffectiveRange;
                            }
                        }
                    }
                    float resolvedRefRange = SBGames.Character.KeepDistanceResolver.ResolveReferenceRange(_selectedRow.AttackRange, _selectedRow.BasicAttackMode, longestSkillRange);
                    bool resolvedAutoEnabled = SBGames.Character.KeepDistanceResolver.ResolveIsEnabled(_selectedRow.KeepDistanceMode, resolvedRefRange, 3.0f);
                    EditorGUILayout.LabelField("Resolved Keep Distance", resolvedAutoEnabled ? $"Enabled (Ref Range: {resolvedRefRange:F1}m)" : $"Disabled (Ref Range: {resolvedRefRange:F1}m)", EditorStyles.miniBoldLabel);

                    if (!string.IsNullOrEmpty(_selectedRow.Id) &&
                        !string.IsNullOrEmpty(_selectedRow.OriginalId) &&
                        !string.Equals(_selectedRow.Id, _selectedRow.OriginalId, StringComparison.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.Space(2);
                        GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                        if (GUILayout.Button($"🔄 Rename Linked Assets to '{_selectedRow.Id}'", GUILayout.Height(24)))
                        {
                            ExecuteCascadeRename(_selectedRow);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else if (!string.IsNullOrEmpty(_selectedRow.Id))
                    {
                        EditorGUILayout.Space(2);
                        if (GUILayout.Button("🔄 Rename Linked Assets...", GUILayout.Height(22)))
                        {
                            ExecuteCascadeRenameManualPrompt(_selectedRow);
                        }
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
                    DrawSkillSection(_selectedRow);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSkillSection(CharacterRow row)
        {
            if (_skillList == null || _skillListRow != row)
            {
                _skillListRow = row;
                _skillList = BuildSkillList(row);
            }

            _skillList.DoLayoutList();

            if (row.SkillEdits.RemovedIds.Count > 0)
            {
                EditorGUILayout.LabelField("Pending removal: " + string.Join(", ", row.SkillEdits.RemovedIds), EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(row.Prefab == null))
                {
                    var content = new GUIContent(
                        row.SkillEdits.SyncRequested ? "🔗 Sync queued (Save All)" : "🔗 Sync prefab skills",
                        row.Prefab == null ? "No prefab assigned; cannot sync." : "Queue prefab fixes so Skills/SkillList and CharacterSkillController.skills match the IDs. Applied on Save All.");
                    if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(170)))
                    {
                        row.SkillEdits.RequestSync();
                        row.MarkDirty();
                    }
                }
                if (row.SkillEdits.HasPrefabChanges)
                {
                    var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.65f, 0f) } };
                    GUILayout.Label("● pending prefab change", style);
                }
            }

            if (!string.IsNullOrEmpty(row.SkillSyncWarning))
            {
                EditorGUILayout.HelpBox(row.SkillSyncWarning, row.HasSkillSyncFixes ? MessageType.Warning : MessageType.Info);
            }
        }

        private ReorderableList BuildSkillList(CharacterRow row)
        {
            var list = new ReorderableList(row.DefaultSkillIds, typeof(string), true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Default Skill IDs (order = controller skill index)", EditorStyles.boldLabel);
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index < 0 || index >= row.DefaultSkillIds.Count) return;
                string id = row.DefaultSkillIds[index];
                rect.height = EditorGUIUtility.singleLineHeight;
                rect.y += 1;

                EditorGUI.LabelField(new Rect(rect.x, rect.y, 24, rect.height), $"[{index}]", EditorStyles.miniLabel);
                EditorGUI.LabelField(new Rect(rect.x + 28, rect.y, 170, rect.height), id);

                var tagRect = new Rect(rect.x + 200, rect.y, rect.width - 200, rect.height);
                if (!CharacterPrefabSkillSync.SkillExistsInDatabase(id))
                {
                    var red = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
                    EditorGUI.LabelField(tagRect, new GUIContent("⚠️ not in Skill database", "This ID has no SkillData in the Skill database."), red);
                }
                else if (row.SkillEdits.IsPendingAdd(id))
                {
                    var orange = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.65f, 0f) } };
                    EditorGUI.LabelField(tagRect, new GUIContent("● pending prefab change", "Added in memory; the prefab is updated on Save All."), orange);
                }
            };
            list.onAddDropdownCallback = (buttonRect, l) =>
            {
                PopupWindow.Show(buttonRect, new SkillPickerPopup(row.SkillEdits.Contains, id =>
                {
                    if (row.SkillEdits.TryAdd(id))
                    {
                        // Auto-wire / ensure the UtilitySkill asset exists so missing-utility warning stops recurring
                        var skillDb = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
                        if (skillDb != null && skillDb.TryGet(id, out var skillData) && skillData != null)
                        {
                            UtilitySkillGenerator.EnsureUtilityAssetExists(skillData, out _);
                        }
                        OnSkillListChanged(row);
                    }
                    _window.Repaint();
                }));
            };
            list.onRemoveCallback = l =>
            {
                row.SkillEdits.RemoveAt(l.index);
                OnSkillListChanged(row);
            };
            list.onReorderCallback = l => OnSkillListChanged(row);
            return list;
        }

        private static void OnSkillListChanged(CharacterRow row)
        {
            row.MarkDirty();
            row.RefreshSkillSync();
        }

        private void ExecuteCascadeRenameManualPrompt(CharacterRow row)
        {
            string oldId = row.OriginalId;
            if (string.IsNullOrEmpty(oldId)) oldId = row.Id;

            // Simple prompt dialog or just confirm renaming with current Id
            if (EditorUtility.DisplayDialog(
                "Rename Linked Assets",
                $"Do you want to search and rename all assets linked with '{row.Id}'?",
                "Yes, Rename",
                "Cancel"))
            {
                ExecuteRenameOperation(row, oldId, row.Id);
            }
        }

        private void ExecuteCascadeRename(CharacterRow row)
        {
            string oldId = row.OriginalId;
            string newId = row.Id;

            if (string.IsNullOrWhiteSpace(oldId) || string.IsNullOrWhiteSpace(newId))
            {
                EditorUtility.DisplayDialog("Rename Assets", "Original ID or New ID is invalid.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Confirm Rename Character Assets",
                $"Are you sure you want to rename all linked assets from '{oldId}' to '{newId}'?\n\n" +
                "This will update:\n" +
                "• Prefab & Root GameObject\n" +
                "• BaseStats ScriptableObject\n" +
                "• Animator Controller & Animation folder\n" +
                "• Resources sprite folder (if matching)",
                "Yes, Rename Assets",
                "Cancel");

            if (!confirm) return;

            ExecuteRenameOperation(row, oldId, newId);
        }

        private void ExecuteRenameOperation(CharacterRow row, string oldId, string newId)
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                var renamedItems = new List<string>();

                // 1. Rename Prefab Asset and root GameObject name inside
                if (row.Prefab != null)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(row.Prefab);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        // Update root GameObject name inside prefab
                        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                        {
                            var root = editingScope.prefabContentsRoot;
                            if (root != null)
                            {
                                root.name = newId;
                                var charIdentifier = root.GetComponent<CharacterIdentifier>();
                                if (charIdentifier != null)
                                {
                                    var serializedId = new SerializedObject(charIdentifier);
                                    var nameProp = serializedId.FindProperty("characterName");
                                    if (nameProp != null)
                                    {
                                        nameProp.stringValue = newId;
                                        serializedId.ApplyModifiedPropertiesWithoutUndo();
                                    }
                                }
                            }
                        }

                        // Rename prefab file
                        string prefabDir = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
                        string newPrefabPath = $"{prefabDir}/{newId}.prefab";
                        if (!string.Equals(prefabPath, newPrefabPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string err = AssetDatabase.RenameAsset(prefabPath, newId);
                            if (string.IsNullOrEmpty(err))
                            {
                                renamedItems.Add($"Prefab: {newId}.prefab");
                                row.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
                            }
                            else
                            {
                                Debug.LogWarning($"[CharacterStatsTab] Could not rename prefab: {err}");
                            }
                        }
                    }
                }

                // 2. Rename BaseStats ScriptableObject
                if (row.BaseStatsSO != null)
                {
                    string soPath = AssetDatabase.GetAssetPath(row.BaseStatsSO);
                    if (!string.IsNullOrEmpty(soPath))
                    {
                        string soDir = Path.GetDirectoryName(soPath)?.Replace('\\', '/');
                        string currentSoName = Path.GetFileNameWithoutExtension(soPath);
                        string targetSoName = currentSoName.Contains("_BaseStats") ? $"{newId}_BaseStats" : newId;
                        string newSoPath = $"{soDir}/{targetSoName}.asset";

                        if (!string.Equals(soPath, newSoPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string err = AssetDatabase.RenameAsset(soPath, targetSoName);
                            if (string.IsNullOrEmpty(err))
                            {
                                renamedItems.Add($"BaseStats: {targetSoName}.asset");
                                row.BaseStatsSO = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(newSoPath);
                            }
                            else
                            {
                                Debug.LogWarning($"[CharacterStatsTab] Could not rename BaseStats SO: {err}");
                            }
                        }
                    }
                }

                // 3. Rename Animator Controller & Animator Folder
                // Derive subcode (e.g., ADVENTURER_N002 -> N002)
                string oldSubCode = ExtractSubCode(oldId);
                string newSubCode = ExtractSubCode(newId);

                RenameAnimatorAssets(oldId, newId, oldSubCode, newSubCode, renamedItems);

                // 4. Rename Resources sprite folder
                RenameResourcesFolder(oldSubCode, newSubCode, oldId, newId, renamedItems);

                row.OriginalId = newId;
                row.MarkDirty();

                string report = renamedItems.Count > 0
                    ? string.Join("\n", renamedItems.Select(item => "• " + item))
                    : "No matching asset names or folders needed renaming.";

                Debug.Log($"[CharacterStatsTab] Cascade rename completed for {oldId} -> {newId}:\n{report}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static string ExtractSubCode(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int idx = id.LastIndexOf('_');
            if (idx >= 0 && idx < id.Length - 1)
            {
                return id.Substring(idx + 1);
            }
            return id;
        }

        private static void RenameAnimatorAssets(string oldId, string newId, string oldSubCode, string newSubCode, List<string> renamedItems)
        {
            // Search inside Assets/Animators/Characters
            const string animatorsRoot = "Assets/Animators/Characters";
            if (!Directory.Exists(animatorsRoot)) return;

            // Search directories matching oldId or oldSubCode
            var matchingDirs = Directory.GetDirectories(animatorsRoot, "*", SearchOption.AllDirectories)
                .Where(d =>
                {
                    string dirName = Path.GetFileName(d);
                    return string.Equals(dirName, oldId, StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrEmpty(oldSubCode) && string.Equals(dirName, oldSubCode, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            foreach (var dir in matchingDirs)
            {
                string unityPath = dir.Replace('\\', '/');
                string targetDirName = !string.IsNullOrEmpty(newSubCode) && string.Equals(Path.GetFileName(dir), oldSubCode, StringComparison.OrdinalIgnoreCase)
                    ? newSubCode
                    : newId;

                // Also rename controller files inside this directory first
                var controllerFiles = Directory.GetFiles(dir, "*.controller", SearchOption.TopDirectoryOnly);
                foreach (var cf in controllerFiles)
                {
                    string cfUnityPath = cf.Replace('\\', '/');
                    string cfName = Path.GetFileNameWithoutExtension(cfUnityPath);
                    string targetCfName = cfName.Replace(oldId, newId, StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(oldSubCode) && !string.IsNullOrEmpty(newSubCode))
                    {
                        targetCfName = targetCfName.Replace(oldSubCode, newSubCode, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!string.Equals(cfName, targetCfName, StringComparison.OrdinalIgnoreCase))
                    {
                        string err = AssetDatabase.RenameAsset(cfUnityPath, targetCfName);
                        if (string.IsNullOrEmpty(err))
                        {
                            renamedItems.Add($"Animator Controller: {targetCfName}.controller");
                        }
                    }
                }

                // Rename directory
                if (!string.Equals(Path.GetFileName(dir), targetDirName, StringComparison.OrdinalIgnoreCase))
                {
                    string err = AssetDatabase.RenameAsset(unityPath, targetDirName);
                    if (string.IsNullOrEmpty(err))
                    {
                        renamedItems.Add($"Animator Folder: {targetDirName}");
                    }
                }
            }
        }

        private static void RenameResourcesFolder(string oldSubCode, string newSubCode, string oldId, string newId, List<string> renamedItems)
        {
            const string resourcesRoot = "Assets/Resources/Characters";
            if (!Directory.Exists(resourcesRoot)) return;

            var matchingDirs = Directory.GetDirectories(resourcesRoot, "*", SearchOption.AllDirectories)
                .Where(d =>
                {
                    string dirName = Path.GetFileName(d);
                    return string.Equals(dirName, oldId, StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrEmpty(oldSubCode) && string.Equals(dirName, oldSubCode, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            foreach (var dir in matchingDirs)
            {
                string unityPath = dir.Replace('\\', '/');
                string targetDirName = !string.IsNullOrEmpty(newSubCode) && string.Equals(Path.GetFileName(dir), oldSubCode, StringComparison.OrdinalIgnoreCase)
                    ? newSubCode
                    : newId;

                if (!string.Equals(Path.GetFileName(dir), targetDirName, StringComparison.OrdinalIgnoreCase))
                {
                    string err = AssetDatabase.RenameAsset(unityPath, targetDirName);
                    if (string.IsNullOrEmpty(err))
                    {
                        renamedItems.Add($"Resources Folder: {targetDirName}");
                    }
                }
            }
        }

        private void DeleteCharacters(List<CharacterRow> rowsToDelete)
        {
            if (rowsToDelete == null || rowsToDelete.Count == 0) return;

            string idListStr = string.Join("\n", rowsToDelete.Take(10).Select(r => $"• {r.Id} ({r.Name})"));
            if (rowsToDelete.Count > 10) idListStr += $"\n... and {rowsToDelete.Count - 10} more";

            bool confirm = EditorUtility.DisplayDialog(
                "Delete Character(s)?",
                $"Are you sure you want to delete {rowsToDelete.Count} character(s)?\n\n" +
                $"{idListStr}\n\n" +
                "This will remove them from CharacterDatabase and permanently delete their linked Prefab and BaseStats ScriptableObject from disk.",
                "Delete",
                "Cancel");

            if (!confirm) return;

            try
            {
                AssetDatabase.StartAssetEditing();

                var deletedAssets = new List<string>();
                var idsToDelete = new HashSet<string>(rowsToDelete.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

                foreach (var row in rowsToDelete)
                {
                    // 1. Delete Prefab if present
                    if (row.Prefab != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(row.Prefab);
                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            if (AssetDatabase.DeleteAsset(prefabPath))
                            {
                                deletedAssets.Add(prefabPath);
                            }
                        }
                    }

                    // 2. Delete BaseStats SO if present
                    if (row.BaseStatsSO != null)
                    {
                        string statsPath = AssetDatabase.GetAssetPath(row.BaseStatsSO);
                        if (!string.IsNullOrEmpty(statsPath))
                        {
                            if (AssetDatabase.DeleteAsset(statsPath))
                            {
                                deletedAssets.Add(statsPath);
                            }
                        }
                    }

                    if (_selectedRow == row)
                    {
                        _selectedRow = null;
                    }
                }

                // 3. Remove entries from CharacterDatabase catalog
                var charDb = DatabaseAutoIndexer.GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase");
                if (charDb != null && charDb.characters != null)
                {
                    charDb.characters.RemoveAll(entry => entry != null && idsToDelete.Contains(entry.id));
                    EditorUtility.SetDirty(charDb);
                }

                // Remove from in-memory row list
                _rows.RemoveAll(r => idsToDelete.Contains(r.Id));

                // Log outcome
                Debug.Log($"[CharacterStatsTab] Successfully deleted {rowsToDelete.Count} character(s). Deleted {deletedAssets.Count} linked asset file(s).");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CheckForDuplicateIds();
            _window?.Repaint();
        }
    }
}
