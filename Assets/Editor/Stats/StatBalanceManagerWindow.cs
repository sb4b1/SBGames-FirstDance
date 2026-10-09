using System;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using SBGames.Editor.Stats.Tabs;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// Centralized editor window for managing, balancing, and comparing Character, Item, and Skill statistics.
    /// Provides in-place editing, batch multipliers, validation badges, and two-way CSV pipeline synchronization.
    /// </summary>
    public class StatBalanceManagerWindow : EditorWindow
    {
        private IStatTab[] _tabs;
        private string[] _tabTitles;
        [SerializeField] private int _selectedTabIndex = 0;

        [MenuItem("SBGames/Stats & Balance Manager", priority = 2)]
        [MenuItem("Window/SBGames/Stats & Balance Manager", priority = 100)]
        public static StatBalanceManagerWindow OpenWindow()
        {
            var window = GetWindow<StatBalanceManagerWindow>("Stats & Balance");
            window.minSize = new Vector2(850, 600);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            EnsureInitialized();
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
                }

                if (GUILayout.Button("↺ Revert", EditorStyles.toolbarButton, GUILayout.Width(65)))
                {
                    if (EditorUtility.DisplayDialog("Revert All", "Discard all unsaved edits and reload from assets?", "Discard", "Cancel"))
                    {
                        foreach (var tab in _tabs) tab.DiscardChanges();
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
                    });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Rebuild All Database Catalogs"), false, () =>
                    {
                        DatabaseAutoIndexer.RebuildAllCatalogs();
                        foreach (var tab in _tabs) tab.ReloadData();
                    });
                    menu.ShowAsContext();
                }
            }
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
