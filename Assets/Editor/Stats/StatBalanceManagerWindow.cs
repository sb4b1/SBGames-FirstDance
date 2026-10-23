using System;
using System.Collections.Generic;
using System.Linq;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using SBGames.Editor.Stats.Tabs;
using SBGames.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// Centralized editor window for managing, balancing, and comparing Character, Item, and Skill statistics.
    /// Provides in-place editing, batch multipliers, validation badges, AI Setup verification, and two-way CSV pipeline synchronization.
    /// </summary>
    public class StatBalanceManagerWindow : EditorWindow
    {
        private IStatTab[] _tabs;
        private string[] _tabTitles;
        [SerializeField] private int _selectedTabIndex = 0;

        private List<ValidationFinding> _globalFindings = new();
        private bool _showGlobalFindings = true;
        private bool _issuesOnlyFilterActive = false;

        [MenuItem("SBGames/Database", priority = 2)]
        [MenuItem("Window/SBGames/Database", priority = 100)]
        public static StatBalanceManagerWindow OpenWindow()
        {
            var window = GetWindow<StatBalanceManagerWindow>("Database");
            window.minSize = new Vector2(850, 600);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            RunValidation();
        }

        private void EnsureInitialized()
        {
            var charTab = new CharacterStatsTab();
            var itemTab = new ItemStatsTab();
            var skillTab = new SkillStatsTab();

            _tabs = new IStatTab[] { charTab, itemTab, skillTab };
            _tabTitles = new[] { charTab.Title, itemTab.Title, skillTab.Title };

            foreach (var tab in _tabs)
            {
                tab.Initialize(this);
            }
        }

        public void SelectTab(int index)
        {
            if (_tabs == null || _tabs.Length == 0)
            {
                EnsureInitialized();
            }
            _selectedTabIndex = Mathf.Clamp(index, 0, _tabs.Length - 1);
            Repaint();
        }

        public void RunValidation()
        {
            if (_tabs == null || _tabs.Length == 0)
            {
                EnsureInitialized();
            }

            var findings = ValidationService.RunFullValidation((progress, step) =>
            {
                EditorUtility.DisplayProgressBar("Validating Database...", step, progress);
            });
            EditorUtility.ClearProgressBar();

            _globalFindings = findings.Where(f => f.SubjectType == ValidationSubjectType.Global || string.IsNullOrEmpty(f.SubjectId)).ToList();

            foreach (var tab in _tabs)
            {
                tab.ApplyExternalFindings(findings);
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (_tabs == null || _tabs.Length != 3)
            {
                EnsureInitialized();
            }

            // Top Global Toolbar
            EditorGUILayout.Space(4);
            DrawGlobalHeaderToolbar();
            EditorGUILayout.Space(2);

            // Global Findings Collapsible Strip
            DrawGlobalFindingsStrip();

            // Active Tab Content
            if (_selectedTabIndex >= 0 && _selectedTabIndex < _tabs.Length)
            {
                _tabs[_selectedTabIndex].DrawGUI();
            }

            // Bottom Status & Footer Bar
            DrawFooterStatusBar();
        }

        private void DrawGlobalHeaderToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Tab Selection Toolbar
                int newTab = GUILayout.Toolbar(_selectedTabIndex, _tabTitles, EditorStyles.toolbarButton, GUILayout.Height(24), GUILayout.Width(340));
                if (newTab != _selectedTabIndex)
                {
                    _selectedTabIndex = newTab;
                    GUI.FocusControl(null);
                }

                EditorGUILayout.Space(6);

                // Validate Button
                if (GUILayout.Button("🔍 Validate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RunValidation();
                }

                // Summary & Stale Marker
                int totalErrors = 0;
                int totalWarnings = 0;
                if (_tabs != null)
                {
                    foreach (var tab in _tabs)
                    {
                        totalErrors += tab.ErrorCount;
                        totalWarnings += tab.WarningCount;
                    }
                }
                totalErrors += _globalFindings.Count(f => f.Severity == ValidationSeverity.Error);
                totalWarnings += _globalFindings.Count(f => f.Severity == ValidationSeverity.Warning);

                string summaryText = $"{totalErrors} errors, {totalWarnings} warnings";
                GUIContent summaryContent = new GUIContent(summaryText, "Click to filter Issues Only");
                if (GUILayout.Button(summaryContent, EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    _issuesOnlyFilterActive = !_issuesOnlyFilterActive;
                    if (_tabs != null)
                    {
                        foreach (var tab in _tabs)
                        {
                            tab.SetFilterIssuesOnly(_issuesOnlyFilterActive);
                        }
                    }
                }

                if (ValidationService.IsStale)
                {
                    var staleStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.65f, 0f) } };
                    GUILayout.Label("⚠️ results may be stale", staleStyle, GUILayout.Width(140));
                }

                GUILayout.FlexibleSpace();

                bool anyDirty = false;
                for (int i = 0; i < _tabs.Length; i++)
                {
                    if (_tabs[i].HasUnsavedChanges) anyDirty = true;
                }

                if (anyDirty)
                {
                    var dirtyStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(1f, 0.65f, 0f) } };
                    GUILayout.Label("● Unsaved Edits", dirtyStyle, GUILayout.Width(110));
                }

                // Global Save / Revert
                GUI.enabled = anyDirty;
                if (GUILayout.Button("💾 Save All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    foreach (var tab in _tabs) tab.SaveData();
                    RunValidation();
                }

                if (GUILayout.Button("↺ Revert", EditorStyles.toolbarButton, GUILayout.Width(65)))
                {
                    if (EditorUtility.DisplayDialog("Revert All", "Discard all unsaved edits and reload from assets?", "Discard", "Cancel"))
                    {
                        foreach (var tab in _tabs) tab.DiscardChanges();
                        RunValidation();
                    }
                }
                GUI.enabled = true;

                EditorGUILayout.Space(4);

                // Global CSV Sync Menu
                if (GUILayout.Button("Sync CSV ▼", EditorStyles.toolbarDropDown, GUILayout.Width(95)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Export All Catalogs to CSV"), false, () => DatabaseCsvPipeline.ExportAllToCsv(true));
                    menu.AddItem(new GUIContent("Import All Catalogs from CSV"), false, () =>
                    {
                        DatabaseCsvPipeline.ImportAllFromCsv(true);
                        foreach (var tab in _tabs) tab.ReloadData();
                        RunValidation();
                    });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Rebuild All Database Catalogs"), false, () =>
                    {
                        DatabaseAutoIndexer.RebuildAllCatalogs();
                        foreach (var tab in _tabs) tab.ReloadData();
                        RunValidation();
                    });
                    menu.ShowAsContext();
                }
            }
        }

        private void DrawGlobalFindingsStrip()
        {
            if (_globalFindings == null || _globalFindings.Count == 0) return;

            int errors = _globalFindings.Count(f => f.Severity == ValidationSeverity.Error);
            int warnings = _globalFindings.Count(f => f.Severity == ValidationSeverity.Warning);
            string header = $"🌐 Global Setup Findings ({errors} Errors, {warnings} Warnings)";

            _showGlobalFindings = EditorGUILayout.Foldout(_showGlobalFindings, header, true, EditorStyles.foldoutHeader);
            if (!_showGlobalFindings) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var finding in _globalFindings)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string badge = finding.Severity switch
                        {
                            ValidationSeverity.Error => "❌ [ERROR]",
                            ValidationSeverity.Warning => "⚠️ [WARNING]",
                            _ => "ℹ️ [INFO]"
                        };

                        GUILayout.Label($"{badge} [{finding.Category}] {finding.Message}", EditorStyles.wordWrappedMiniLabel);

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
            EditorGUILayout.Space(2);
        }

        private void DrawFooterStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int totalIssues = 0;
                for (int i = 0; i < _tabs.Length; i++)
                {
                    totalIssues += _tabs[i].ValidationIssueCount;
                }
                totalIssues += _globalFindings.Count;

                if (totalIssues > 0)
                {
                    var issueStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
                    GUILayout.Label($"⚠️ {totalIssues} validation issue(s) detected across catalogs.", issueStyle);
                }
                else
                {
                    GUILayout.Label("✓ All catalogs healthy and valid.", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("SBGames Stat Balance Matrix v1.0", EditorStyles.miniLabel);
            }
        }
    }
}
