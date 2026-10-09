using System;
using SBGames.Editor.Creator.Tabs;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Creator
{
    public class SBGamesCreatorWindow : EditorWindow
    {
        [SerializeField] private CharacterCreatorTab characterTab = new();
        [SerializeField] private ItemCreatorTab itemTab = new();
        [SerializeField] private SkillCreatorTab skillTab = new();
        [SerializeField] private int selectedTabIndex = 0;

        public CharacterCreatorTab CharacterTab => characterTab;
        public ItemCreatorTab ItemTab => itemTab;
        public SkillCreatorTab SkillTab => skillTab;

        private ICreatorTab[] _tabs;
        private string[] _tabTitles;

        [MenuItem("SBGames/Creator Hub", priority = 1)]
        [MenuItem("Assets/Create/SBGames/Creator Hub", priority = 49)]
        public static SBGamesCreatorWindow OpenWindow()
        {
            var window = GetWindow<SBGamesCreatorWindow>("SBGames Creator");
            window.minSize = new Vector2(550, 700);
            window.Show();
            return window;
        }

        [MenuItem("SBGames/Characters/Character Creator Wizard", priority = 10)]
        [MenuItem("Assets/Create/SBGames/Character Creator Wizard", priority = 50)]
        public static void OpenCharacterCreator()
        {
            var window = OpenWindow();
            window.SelectTab(0);
        }

        [MenuItem("SBGames/Items/Item & Object Creator Wizard", priority = 20)]
        [MenuItem("Assets/Create/SBGames/Item & Object Creator Wizard", priority = 51)]
        public static void OpenItemCreator()
        {
            var window = OpenWindow();
            window.SelectTab(1);
        }

        [MenuItem("SBGames/Skills/Skill Creator Wizard", priority = 30)]
        [MenuItem("Assets/Create/SBGames/Skill Creator Wizard", priority = 52)]
        public static void OpenSkillCreator()
        {
            var window = OpenWindow();
            window.SelectTab(2);
        }

        private void OnEnable()
        {
            EnsureTabsInitialized();
        }

        private void EnsureTabsInitialized()
        {
            if (characterTab == null) characterTab = new CharacterCreatorTab();
            if (itemTab == null) itemTab = new ItemCreatorTab();
            if (skillTab == null) skillTab = new SkillCreatorTab();

            _tabs = new ICreatorTab[] { characterTab, itemTab, skillTab };
            _tabTitles = new[] { characterTab.Title, itemTab.Title, skillTab.Title };

            foreach (var tab in _tabs)
            {
                tab.Initialize(this);
                tab.OnEnable();
            }
        }

        public void SelectTab(int index)
        {
            if (_tabs == null || _tabs.Length == 0)
            {
                EnsureTabsInitialized();
            }
            selectedTabIndex = Mathf.Clamp(index, 0, _tabs.Length - 1);
            Repaint();
        }

        private void OnGUI()
        {
            if (_tabs == null || _tabs.Length != 3)
            {
                EnsureTabsInitialized();
            }

            // Top Hub Toolbar
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int newTab = GUILayout.Toolbar(selectedTabIndex, _tabTitles, EditorStyles.toolbarButton, GUILayout.Height(24));
                if (newTab != selectedTabIndex)
                {
                    selectedTabIndex = newTab;
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("📊 Stats & Balance Manager", EditorStyles.toolbarButton, GUILayout.Height(24), GUILayout.Width(170)))
                {
                    SBGames.Editor.Stats.StatBalanceManagerWindow.OpenWindow();
                }
            }
            EditorGUILayout.Space(6);

            // Render active tab
            if (selectedTabIndex >= 0 && selectedTabIndex < _tabs.Length)
            {
                _tabs[selectedTabIndex].OnGUI();
            }
        }
    }
}
