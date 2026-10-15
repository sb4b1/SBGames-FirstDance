using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Unity;
using SBGames.Dungeon.Unity.Pieces;
using SBGames.Dungeon.Markers;

namespace SBGames.Editor.Dungeon
{
    /// <summary>
    /// Dedicated authoring and validation editor window for Dungeon Pieces and Piece Sets.
    /// Modeled on StatBalanceManagerWindow with tab navigation, interactive grid overlay,
    /// sprite sheet importing, real-time validation badges, and 15-door-set coverage matrix.
    /// </summary>
    public class DungeonPieceAuthoringWindow : EditorWindow
    {
        [SerializeField] private PieceSetSO activePieceSet;
        [SerializeField] private int selectedTabIndex = 0;

        private readonly string[] _tabTitles = new[]
        {
            "Pieces Catalog",
            "Piece Editor",
            "Sheet Importer",
            "Coverage & Validation",
            "Batch Simulation"
        };

        // Tab 0 state
        private Vector2 _catalogScroll;
        private string _searchFilter = string.Empty;
        private DungeonRoomType? _typeFilter = null;

        // Tab 1 state
        private int _selectedPieceIndex = -1;
        private Vector2 _editorScroll;

        // Tab 2 state
        private Sprite _importSprite;
        private string _importPieceName = "piece_new";
        private DungeonRoomType _importRoomType = DungeonRoomType.Normal;
        private OpeningFlags _importOpenings = OpeningFlags.North;
        private bool _importRotatable = true;
        private bool _importCreateRotatedVariants = true;
        private string _importTargetFolder = "Assets/Prefabs/Dungeon/Pieces";

        // Tab 3 state
        private Vector2 _coverageScroll;
        private DungeonRoomType _coverageTypeFilter = DungeonRoomType.Normal;

        // Tab 4 state (Batch Simulation)
        private int _batchSeedCount = 20;
        private int _batchStartSeed = 1000;
        private int _batchMaxRetries = 5;
        private int _batchCorridorLength = 0;
        private Vector2 _batchScroll;
        private DungeonBatchReport _lastBatchReport;

        [MenuItem("SBGames/Dungeon Piece Authoring Tool", priority = 3)]
        [MenuItem("Window/SBGames/Dungeon Piece Authoring Tool", priority = 101)]
        public static DungeonPieceAuthoringWindow OpenWindow()
        {
            var window = GetWindow<DungeonPieceAuthoringWindow>("Piece Authoring");
            window.minSize = new Vector2(920, 640);
            window.Show();
            return window;
        }

        [MenuItem("SBGames/Run Dungeon Batch Test", priority = 4)]
        public static void OpenBatchSimulationWindow()
        {
            var win = OpenWindow();
            win.selectedTabIndex = 4;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            DrawGlobalHeaderToolbar();
            EditorGUILayout.Space(2);

            if (activePieceSet == null)
            {
                DrawNoPieceSetPrompt();
                DrawFooterStatusBar();
                return;
            }

            switch (selectedTabIndex)
            {
                case 0:
                    DrawCatalogTab();
                    break;
                case 1:
                    DrawPieceEditorTab();
                    break;
                case 2:
                    DrawSheetImporterTab();
                    break;
                case 3:
                    DrawCoverageTab();
                    break;
                case 4:
                    DrawBatchSimulationTab();
                    break;
            }

            DrawFooterStatusBar();
        }

        private void DrawGlobalHeaderToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Tab Selection
                int newTab = GUILayout.Toolbar(selectedTabIndex, _tabTitles, EditorStyles.toolbarButton, GUILayout.Height(24), GUILayout.Width(580));
                if (newTab != selectedTabIndex)
                {
                    selectedTabIndex = newTab;
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                // Active PieceSet Object Field
                GUILayout.Label("Active Set:", EditorStyles.miniLabel);
                var newSet = (PieceSetSO)EditorGUILayout.ObjectField(activePieceSet, typeof(PieceSetSO), false, GUILayout.Width(180));
                if (newSet != activePieceSet)
                {
                    activePieceSet = newSet;
                    _selectedPieceIndex = -1;
                }

                if (GUILayout.Button("New Set", EditorStyles.toolbarButton, GUILayout.Width(65)))
                {
                    CreateNewPieceSetAsset();
                }
            }
        }

        private void DrawNoPieceSetPrompt()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("No Piece Set Selected", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Select an existing PieceSet ScriptableObject above, or create a new theme.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(10);

                if (GUILayout.Button("Create New Piece Set", GUILayout.Width(180), GUILayout.Height(30)))
                {
                    CreateNewPieceSetAsset();
                }
                EditorGUILayout.Space(20);
            }
        }

        // =========================================================================
        // TAB 0: PIECES CATALOG
        // =========================================================================

        private void DrawCatalogTab()
        {
            // Search & Filter Toolbar
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search:", GUILayout.Width(50));
                _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(160));

                GUILayout.Space(10);
                GUILayout.Label("Filter Room:", GUILayout.Width(75));

                string[] typeOptions = new[] { "All", "Entrance", "Normal", "SafeRoom", "Boss" };
                int currentTypeIdx = _typeFilter.HasValue ? ((int)_typeFilter.Value + 1) : 0;
                int newTypeIdx = EditorGUILayout.Popup(currentTypeIdx, typeOptions, EditorStyles.toolbarPopup, GUILayout.Width(100));
                if (newTypeIdx != currentTypeIdx)
                {
                    _typeFilter = newTypeIdx == 0 ? (DungeonRoomType?)null : (DungeonRoomType)(newTypeIdx - 1);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+ New Piece", EditorStyles.toolbarButton, GUILayout.Width(95)))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Room / Normal (Default Size)"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.Normal));
                    menu.AddItem(new GUIContent("Room / Normal Small (10x10)"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.Normal, 10, 10));
                    menu.AddItem(new GUIContent("Room / Normal Large (24x18)"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.Normal, 24, 18));
                    menu.AddItem(new GUIContent("Pathway / Straight (6x8)"), false, () => CreatePieceFromTemplate(null, PieceRole.Pathway, DungeonRoomType.Normal, 6, 8));
                    menu.AddItem(new GUIContent("Pathway / Straight (8x6)"), false, () => CreatePieceFromTemplate(null, PieceRole.Pathway, DungeonRoomType.Normal, 8, 6));
                    menu.AddItem(new GUIContent("Pathway / Corner (8x8)"), false, () => CreatePieceFromTemplate(null, PieceRole.Pathway, DungeonRoomType.Normal, 8, 8));
                    menu.AddItem(new GUIContent("Pathway / T-Junction (10x10)"), false, () => CreatePieceFromTemplate(null, PieceRole.Pathway, DungeonRoomType.Normal, 10, 10));
                    menu.AddItem(new GUIContent("Special / Entrance Piece"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.Entrance));
                    menu.AddItem(new GUIContent("Special / Boss Piece"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.Boss));
                    menu.AddItem(new GUIContent("Special / Safe Room Piece"), false, () => CreatePieceFromTemplate(null, PieceRole.Room, DungeonRoomType.SafeRoom));
                    menu.ShowAsContext();
                }

                if (GUILayout.Button("+ New Opening Cap", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    CreateAndAssignOpeningCapPrefab();
                }
            }

            // Table Header
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(45));
                GUILayout.Label("Preview", EditorStyles.boldLabel, GUILayout.Width(45));
                GUILayout.Label("Piece ID", EditorStyles.boldLabel, GUILayout.Width(140));
                GUILayout.Label("Role", EditorStyles.boldLabel, GUILayout.Width(65));
                GUILayout.Label("Room Type", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Size", EditorStyles.boldLabel, GUILayout.Width(55));
                GUILayout.Label("Openings", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label("Rotatable", EditorStyles.boldLabel, GUILayout.Width(65));
                GUILayout.Label("Weight", EditorStyles.boldLabel, GUILayout.Width(50));
                GUILayout.Label("Min/Max", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Actions", EditorStyles.boldLabel, GUILayout.Width(110));
            }

            // Table Rows
            var prefabs = activePieceSet.PiecePrefabs;
            _catalogScroll = EditorGUILayout.BeginScrollView(_catalogScroll);

            for (int i = 0; i < prefabs.Count; i++)
            {
                var go = prefabs[i];
                if (go == null) continue;

                var piece = go.GetComponent<DungeonPiece>();
                if (piece == null) continue;

                // Filtering
                if (!string.IsNullOrEmpty(_searchFilter) && !piece.PieceId.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase).Equals(-1))
                {
                    continue;
                }
                if (_typeFilter.HasValue && piece.RoomType != _typeFilter.Value)
                {
                    continue;
                }

                var desc = piece.ToDescriptor();
                var badges = PieceValidator.ValidatePiece(desc, activePieceSet.CellSize.x, activePieceSet.CellSize.y);
                bool hasErrors = PieceValidator.HasErrors(badges);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    // Validation Badge
                    if (hasErrors)
                    {
                        var redStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                        GUILayout.Label(new GUIContent("❌", badges[0].Message), redStyle, GUILayout.Width(45));
                    }
                    else if (badges.Count > 0)
                    {
                        var yellowStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.yellow } };
                        GUILayout.Label(new GUIContent("⚠️", badges[0].Message), yellowStyle, GUILayout.Width(45));
                    }
                    else
                    {
                        var greenStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.green } };
                        GUILayout.Label("✓", greenStyle, GUILayout.Width(45));
                    }

                    // Asset Preview (Zero scene pollution)
                    Texture2D previewTex = AssetPreview.GetAssetPreview(go);
                    if (previewTex != null)
                    {
                        GUILayout.Label(previewTex, GUILayout.Width(36), GUILayout.Height(36));
                    }
                    else
                    {
                        GUILayout.Box(GUIContent.none, GUILayout.Width(36), GUILayout.Height(36));
                    }

                    // ID & Select Button
                    if (GUILayout.Button(piece.PieceId, EditorStyles.linkLabel, GUILayout.Width(140)))
                    {
                        _selectedPieceIndex = i;
                        selectedTabIndex = 1; // switch to editor tab
                    }

                    // Role
                    GUILayout.Label(piece.Role.ToString(), GUILayout.Width(65));

                    // Room Type
                    GUILayout.Label(piece.RoomType.ToString(), GUILayout.Width(80));

                    // Size
                    GUILayout.Label($"{piece.CellSize.x}x{piece.CellSize.y}", GUILayout.Width(55));

                    // Openings
                    GUILayout.Label(piece.Openings.ToString(), GUILayout.Width(70));

                    // Rotatable
                    GUILayout.Label(piece.IsRotatable ? "Yes" : "No", GUILayout.Width(65));

                    // Weight
                    GUILayout.Label(piece.Weight.ToString("F1"), GUILayout.Width(50));

                    // Min/Max
                    string minMax = $"{piece.MinCountPerDungeon}/{(piece.MaxCountPerDungeon == 0 ? "∞" : piece.MaxCountPerDungeon.ToString())}";
                    GUILayout.Label(minMax, GUILayout.Width(60));

                    GUILayout.FlexibleSpace();

                    // Edit, Fit & Ping Buttons
                    if (GUILayout.Button("Edit", EditorStyles.miniButtonLeft, GUILayout.Width(35)))
                    {
                        _selectedPieceIndex = i;
                        selectedTabIndex = 1;
                    }
                    if (GUILayout.Button("Fit", EditorStyles.miniButtonMid, GUILayout.Width(30)))
                    {
                        Undo.RecordObject(piece, "Fit Bounds");
                        piece.FitBoundsToPaintedTiles();
                        EditorUtility.SetDirty(piece);
                    }
                    if (GUILayout.Button("Ping", EditorStyles.miniButtonRight, GUILayout.Width(35)))
                    {
                        EditorGUIUtility.PingObject(go);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // =========================================================================
        // TAB 1: PIECE EDITOR
        // =========================================================================

        private void DrawPieceEditorTab()
        {
            var prefabs = activePieceSet.PiecePrefabs;
            if (_selectedPieceIndex < 0 || _selectedPieceIndex >= prefabs.Count || prefabs[_selectedPieceIndex] == null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("No Piece Selected for Editing", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Select a piece from the Pieces Catalog tab to inspect and edit its properties.", EditorStyles.wordWrappedLabel);
                }
                return;
            }

            var prefab = prefabs[_selectedPieceIndex];
            var piece = prefab.GetComponent<DungeonPiece>();
            if (piece == null) return;

            _editorScroll = EditorGUILayout.BeginScrollView(_editorScroll);

            // Back button and header
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("◀ Back to Catalog", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    selectedTabIndex = 0;
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Editing Piece: {piece.PieceId}", EditorStyles.boldLabel);
                if (GUILayout.Button("Ping Prefab", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    EditorGUIUtility.PingObject(prefab);
                }
            }

            EditorGUILayout.Space(6);

            // Validation Badges
            var desc = piece.ToDescriptor();
            var badges = PieceValidator.ValidatePiece(desc, activePieceSet.CellSize.x, activePieceSet.CellSize.y);
            if (badges.Count > 0)
            {
                for (int i = 0; i < badges.Count; i++)
                {
                    var b = badges[i];
                    EditorGUILayout.HelpBox(b.Message, b.IsError ? MessageType.Error : MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("✓ Dungeon Piece configuration is healthy and valid.", MessageType.Info);
            }

            EditorGUILayout.Space(4);

            // Two-column layout: Left = Properties, Right = Buildable Mask Painter
            using (new EditorGUILayout.HorizontalScope())
            {
                // Left Column: Properties
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(360)))
                {
                    EditorGUILayout.LabelField("Piece Properties", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    string newId = EditorGUILayout.TextField("Piece ID", piece.PieceId);
                    var newRole = (PieceRole)EditorGUILayout.EnumPopup("Role", piece.Role);
                    var newType = (DungeonRoomType)EditorGUILayout.EnumPopup("Room Type", piece.RoomType);
                    var newSize = EditorGUILayout.Vector2IntField("Cell Size (WxH)", piece.CellSize);
                    var newOpenings = (OpeningFlags)EditorGUILayout.EnumFlagsField("Openings", piece.Openings);
                    bool newRotatable = EditorGUILayout.Toggle("Is Rotatable", piece.IsRotatable);
                    bool newDirectional = EditorGUILayout.Toggle("Has Directional Art", piece.HasDirectionalArt);
                    float newWeight = EditorGUILayout.FloatField("Weight", piece.Weight);
                    int newMin = EditorGUILayout.IntField("Min Count", piece.MinCountPerDungeon);
                    int newMax = EditorGUILayout.IntField("Max Count", piece.MaxCountPerDungeon);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(piece, "Modify Piece Properties");
                        piece.PieceId = newId;
                        piece.Role = newRole;
                        piece.RoomType = newType;
                        piece.CellSize = newSize;
                        piece.Openings = newOpenings;
                        piece.IsRotatable = newRotatable;
                        piece.HasDirectionalArt = newDirectional;
                        piece.Weight = newWeight;
                        piece.MinCountPerDungeon = newMin;
                        piece.MaxCountPerDungeon = newMax;
                        EditorUtility.SetDirty(piece);
                    }

                    EditorGUILayout.Space(6);
                    if (GUILayout.Button("Fit Bounds to Painted Tiles", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(piece, "Fit Bounds");
                        piece.FitBoundsToPaintedTiles();
                        EditorUtility.SetDirty(piece);
                    }

                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField("Asset Preview:", EditorStyles.miniBoldLabel);
                    Texture2D preview = AssetPreview.GetAssetPreview(prefab);
                    if (preview != null)
                    {
                        GUILayout.Label(preview, GUILayout.Width(128), GUILayout.Height(128));
                    }
                }

                GUILayout.Space(8);

                // Right Column: Interactive Buildable Mask Matrix
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Buildable Mask Grid Painter ({piece.CellSize.x}x{piece.CellSize.y})", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Active Placement Cells: {piece.BuildableCells.Count}", EditorStyles.miniLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var oldBg = GUI.backgroundColor;
                        if (DungeonPieceEditor.EditMaskInScene) GUI.backgroundColor = new Color(0.3f, 0.95f, 0.4f);
                        if (GUILayout.Button(DungeonPieceEditor.EditMaskInScene ? "✓ Scene Mask Paint: ON" : "✎ Scene Mask Paint: OFF", GUILayout.Height(24)))
                        {
                            DungeonPieceEditor.EditMaskInScene = !DungeonPieceEditor.EditMaskInScene;
                            if (DungeonPieceEditor.EditMaskInScene) DungeonPieceEditor.ShowMaskOverlay = true;
                            SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = oldBg;

                        if (DungeonPieceEditor.ShowMaskOverlay) GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
                        if (GUILayout.Button(DungeonPieceEditor.ShowMaskOverlay ? "👁 Overlay: ON" : "👁 Overlay: OFF", GUILayout.Width(100), GUILayout.Height(24)))
                        {
                            DungeonPieceEditor.ShowMaskOverlay = !DungeonPieceEditor.ShowMaskOverlay;
                            SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = oldBg;
                    }

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Fill All", EditorStyles.miniButtonLeft))
                        {
                            Undo.RecordObject(piece, "Fill All Mask");
                            piece.FillBuildableBox(piece.CellSize.x, piece.CellSize.y, margin: 0);
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("Fill Interior (Margin 1)", EditorStyles.miniButtonMid))
                        {
                            Undo.RecordObject(piece, "Fill Interior Mask");
                            piece.FillBuildableBox(piece.CellSize.x, piece.CellSize.y, margin: 1);
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("Clear", EditorStyles.miniButtonRight))
                        {
                            Undo.RecordObject(piece, "Clear Mask");
                            piece.ClearBuildableCells();
                            EditorUtility.SetDirty(piece);
                        }
                    }

                    EditorGUILayout.Space(4);
                    DrawMaskMatrix(piece, piece.CellSize.x, piece.CellSize.y);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMaskMatrix(DungeonPiece piece, int width, int height)
        {
            float btnSize = 20f;
            for (int y = height - 1; y >= 0; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    for (int x = 0; x < width; x++)
                    {
                        var coord = new Vector2Int(x, y);
                        bool isBuildable = piece.IsCellBuildable(coord);

                        var oldBg = GUI.backgroundColor;
                        GUI.backgroundColor = isBuildable ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.25f, 0.25f, 0.25f, 0.6f);

                        if (GUILayout.Button(GUIContent.none, GUILayout.Width(btnSize), GUILayout.Height(btnSize)))
                        {
                            Undo.RecordObject(piece, "Toggle Mask Cell");
                            if (isBuildable) piece.RemoveBuildableCell(coord);
                            else piece.AddBuildableCell(coord);
                            EditorUtility.SetDirty(piece);
                        }

                        GUI.backgroundColor = oldBg;
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        // =========================================================================
        // TAB 2: SHEET IMPORTER
        // =========================================================================

        private void DrawSheetImporterTab()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Sprite Sheet Piece Authoring", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Author new Dungeon Piece prefabs from sprites, mark Openings, and generate rotated variants.", EditorStyles.wordWrappedLabel);

                EditorGUILayout.Space(6);

                _importSprite = (Sprite)EditorGUILayout.ObjectField("Source Sprite", _importSprite, typeof(Sprite), false);
                _importPieceName = EditorGUILayout.TextField("Piece Name", _importPieceName);
                _importRoomType = (DungeonRoomType)EditorGUILayout.EnumPopup("Room Type", _importRoomType);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Mark Openings (Click Cardinal Sides):", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool n = GUILayout.Toggle((_importOpenings & OpeningFlags.North) != 0, "North [N]", "Button");
                    bool e = GUILayout.Toggle((_importOpenings & OpeningFlags.East) != 0, "East [E]", "Button");
                    bool s = GUILayout.Toggle((_importOpenings & OpeningFlags.South) != 0, "South [S]", "Button");
                    bool w = GUILayout.Toggle((_importOpenings & OpeningFlags.West) != 0, "West [W]", "Button");

                    _importOpenings = OpeningFlags.None;
                    if (n) _importOpenings |= OpeningFlags.North;
                    if (e) _importOpenings |= OpeningFlags.East;
                    if (s) _importOpenings |= OpeningFlags.South;
                    if (w) _importOpenings |= OpeningFlags.West;
                }

                EditorGUILayout.Space(4);
                _importRotatable = EditorGUILayout.Toggle("Is Rotatable", _importRotatable);
                _importCreateRotatedVariants = EditorGUILayout.Toggle("Generate Rotated Variants (4x)", _importCreateRotatedVariants);
                _importTargetFolder = EditorGUILayout.TextField("Save Folder", _importTargetFolder);

                EditorGUILayout.Space(10);

                GUI.enabled = _importSprite != null && !string.IsNullOrEmpty(_importPieceName) && _importOpenings != OpeningFlags.None;
                if (GUILayout.Button("Create Prefab & Add To Set", GUILayout.Height(32)))
                {
                    CreateImportedPiece();
                }
                GUI.enabled = true;
            }
        }

        private void CreateImportedPiece()
        {
            int w = activePieceSet.CellSize.x;
            int h = activePieceSet.CellSize.y;

            var basePrefab = DungeonSheetImporter.CreatePiecePrefab(
                _importSprite,
                _importPieceName,
                _importRoomType,
                _importOpenings,
                _importRotatable,
                _importTargetFolder,
                w, h
            );

            if (basePrefab != null)
            {
                activePieceSet.AddPiecePrefab(basePrefab);
                EditorUtility.SetDirty(activePieceSet);

                if (_importCreateRotatedVariants && _importRotatable)
                {
                    var variants = DungeonSheetImporter.CreateRotatedVariants(
                        _importSprite,
                        _importPieceName,
                        _importRoomType,
                        _importOpenings,
                        _importTargetFolder,
                        w, h
                    );
                    foreach (var v in variants)
                    {
                        activePieceSet.AddPiecePrefab(v);
                    }
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success", $"Created Dungeon Piece prefabs and added to {activePieceSet.name}.", "OK");
                selectedTabIndex = 0; // return to catalog
            }
        }

        // =========================================================================
        // TAB 3: COVERAGE & VALIDATION REPORT
        // =========================================================================

        private void DrawCoverageTab()
        {
            var report = activePieceSet.GetCoverageReport();
            var setBadges = activePieceSet.ValidateSet();

            if (setBadges.Count > 0)
            {
                EditorGUILayout.LabelField($"Piece Set Validation Badges ({setBadges.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < setBadges.Count; i++)
                {
                    var b = setBadges[i];
                    var boxType = b.IsError ? MessageType.Error : MessageType.Warning;
                    EditorGUILayout.HelpBox(b.Message, boxType);
                }
                EditorGUILayout.Space(4);
            }

            // Tile Pixel Size validation
            int targetPpu = activePieceSet.TilePixelSize;
            var ppuIssues = new List<TilePixelSizeIssue>();
            for (int i = 0; i < activePieceSet.PiecePrefabs.Count; i++)
            {
                var p = activePieceSet.PiecePrefabs[i];
                if (p == null) continue;
                var pieceComp = p.GetComponent<DungeonPiece>();
                if (pieceComp != null)
                {
                    ppuIssues.AddRange(TilePixelSizeValidator.ValidatePieceTilePixelSize(pieceComp, targetPpu));
                }
            }

            if (ppuIssues.Count > 0)
            {
                EditorGUILayout.LabelField($"Tile Pixel Size Issues ({ppuIssues.Count}) - Expected PPU: {targetPpu}", EditorStyles.boldLabel);
                for (int i = 0; i < ppuIssues.Count; i++)
                {
                    var issue = ppuIssues[i];
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(issue.Description, EditorStyles.wordWrappedMiniLabel);
                        if (GUILayout.Button($"Fix PPU ({issue.ExpectedPpu})", EditorStyles.miniButton, GUILayout.Width(90)))
                        {
                            TilePixelSizeValidator.FixTexturePpu(issue.AssetPath, issue.ExpectedPpu);
                        }
                    }
                }
                EditorGUILayout.Space(4);
            }

            // Summary Cards
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                var cardExact = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
                using (new EditorGUILayout.VerticalScope(cardExact))
                {
                    EditorGUILayout.LabelField("Exact Matches", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{report.ExactMatchCount} / {report.Entries.Count} ({(report.ExactCoverageRatio * 100f):F0}%)");
                }

                var cardCaps = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
                using (new EditorGUILayout.VerticalScope(cardCaps))
                {
                    EditorGUILayout.LabelField("Superset With Caps", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{report.SupersetWithCapsCount} / {report.Entries.Count}");
                }

                var cardMissing = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
                using (new EditorGUILayout.VerticalScope(cardMissing))
                {
                    var missingStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = report.NoneCount > 0 ? Color.red : Color.green } };
                    EditorGUILayout.LabelField("Missing (Errors)", missingStyle);
                    EditorGUILayout.LabelField($"{report.NoneCount} / {report.Entries.Count}", missingStyle);
                }
            }

            EditorGUILayout.Space(4);

            // Filter by Room Type
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Filter Room Type:", GUILayout.Width(120));
                _coverageTypeFilter = (DungeonRoomType)EditorGUILayout.EnumPopup(_coverageTypeFilter, EditorStyles.toolbarPopup, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
            }

            // Matrix Table Header
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUILayout.Label("Room Type", EditorStyles.boldLabel, GUILayout.Width(90));
                GUILayout.Label("Required Doors", EditorStyles.boldLabel, GUILayout.Width(110));
                GUILayout.Label("Match Status", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.Label("Matching Piece", EditorStyles.boldLabel, GUILayout.Width(160));
                GUILayout.Label("Quarter Turns", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Opening Caps Needed", EditorStyles.boldLabel, GUILayout.Width(140));
                GUILayout.FlexibleSpace();
            }

            _coverageScroll = EditorGUILayout.BeginScrollView(_coverageScroll);

            for (int i = 0; i < report.Entries.Count; i++)
            {
                var entry = report.Entries[i];
                if (entry.RoomType != _coverageTypeFilter) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(entry.RoomType.ToString(), GUILayout.Width(90));
                    GUILayout.Label(entry.RequiredDoors.ToString(), GUILayout.Width(110));

                    if (entry.IsExact)
                    {
                        var exactStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.85f, 0.2f) } };
                        GUILayout.Label("Exact Match", exactStyle, GUILayout.Width(120));
                    }
                    else if (entry.IsCapped)
                    {
                        var capStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.75f, 0.2f) } };
                        GUILayout.Label("Superset (Caps)", capStyle, GUILayout.Width(120));
                    }
                    else
                    {
                        var missingStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                        GUILayout.Label("NONE (Error)", missingStyle, GUILayout.Width(120));
                    }

                    GUILayout.Label(string.IsNullOrEmpty(entry.MatchingPieceId) ? "-" : entry.MatchingPieceId, GUILayout.Width(160));
                    GUILayout.Label(entry.IsMissing ? "-" : $"{entry.MatchingQuarterTurns} ({entry.MatchingQuarterTurns * 90}°)", GUILayout.Width(80));
                    GUILayout.Label(entry.CappedSides == OpeningFlags.None ? "None" : entry.CappedSides.ToString(), GUILayout.Width(140));
                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // =========================================================================
        // FOOTER STATUS BAR
        // =========================================================================

        private void DrawFooterStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (activePieceSet != null)
                {
                    var badges = activePieceSet.ValidateSet();
                    int errors = 0;
                    int warnings = 0;
                    for (int i = 0; i < badges.Count; i++)
                    {
                        if (badges[i].IsError) errors++;
                        else warnings++;
                    }

                    if (errors > 0)
                    {
                        var errStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } };
                        GUILayout.Label($"❌ {errors} error(s) detected in '{activePieceSet.ThemeName}' Piece Set.", errStyle);
                    }
                    else if (warnings > 0)
                    {
                        var warnStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } };
                        GUILayout.Label($"⚠️ {warnings} warning(s) in Piece Set.", warnStyle);
                    }
                    else
                    {
                        GUILayout.Label($"✓ '{activePieceSet.ThemeName}' Piece Set is healthy and valid.", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    GUILayout.Label("No Piece Set selected.", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("SBGames Dungeon Piece Authoring Tool v1.0", EditorStyles.miniLabel);
            }
        }

        private void CreateNewPieceSetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Piece Set", "NewPieceSet", "asset", "Select location for new PieceSetSO");
            if (!string.IsNullOrEmpty(path))
            {
                var newSet = CreateInstance<PieceSetSO>();
                AssetDatabase.CreateAsset(newSet, path);
                AssetDatabase.SaveAssets();
                activePieceSet = newSet;
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = newSet;
            }
        }

        public GameObject CreatePieceFromTemplate(
            string pieceName = null,
            PieceRole role = PieceRole.Room,
            DungeonRoomType roomType = DungeonRoomType.Normal,
            int? customWidth = null,
            int? customHeight = null,
            OpeningFlags openings = OpeningFlags.North,
            bool openInPrefabMode = true)
        {
            if (activePieceSet == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select or create an active Piece Set first.", "OK");
                return null;
            }

            string setPath = AssetDatabase.GetAssetPath(activePieceSet);
            string folder = string.IsNullOrEmpty(setPath)
                ? "Assets/Prefabs/Dungeon/Pieces"
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(setPath), "Pieces").Replace("\\", "/");

            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            int cellW = customWidth.HasValue && customWidth.Value > 0 ? customWidth.Value : activePieceSet.CellSize.x;
            int cellH = customHeight.HasValue && customHeight.Value > 0 ? customHeight.Value : activePieceSet.CellSize.y;

            if (string.IsNullOrEmpty(pieceName))
            {
                string rolePrefix = role == PieceRole.Pathway ? "corridor" : (roomType == DungeonRoomType.Normal ? "piece" : roomType.ToString().ToLowerInvariant());
                pieceName = $"{rolePrefix}_{cellW}x{cellH}_{activePieceSet.PiecePrefabs.Count + 1:D2}";
            }

            // 1. Root GameObject
            var rootGo = new GameObject(pieceName);

            // BoxCollider2D trigger
            var col = rootGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellW, cellH);
            col.isTrigger = true;
            col.offset = Vector2.zero;

            // RoomVolume
            var roomVol = rootGo.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            roomVol.SetBounds(new Bounds(Vector3.zero, new Vector3(cellW, cellH, 2f)));
            roomVol.IsSafeRoom = (roomType == DungeonRoomType.SafeRoom);
            roomVol.IsBossRoom = (roomType == DungeonRoomType.Boss);

            // DungeonPiece component
            var pieceComp = rootGo.AddComponent<DungeonPiece>();
            pieceComp.PieceId = pieceName;
            pieceComp.Role = role;
            pieceComp.RoomType = roomType;
            pieceComp.CellSize = new Vector2Int(cellW, cellH);
            pieceComp.Openings = openings;
            pieceComp.IsRotatable = true;
            pieceComp.Footprint = Vector2Int.one;

            // 2. Child Grid
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(rootGo.transform);
            gridGo.transform.localPosition = new Vector3(-cellW * 0.5f, -cellH * 0.5f, 0f);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            // Child Ground
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            groundGo.transform.localPosition = Vector3.zero;
            var groundTm = groundGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var groundTr = groundGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            groundTr.sortingLayerName = "Ground";
            groundTr.sortingOrder = 1;
            var gMod = groundGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            gMod.overrideArea = true;
            gMod.area = 0; // Walkable
            gMod.ignoreFromBuild = false;

            // Child Walls
            var wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(gridGo.transform);
            wallsGo.transform.localPosition = Vector3.zero;
            var wallsTm = wallsGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var wallsTr = wallsGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            wallsTr.sortingLayerName = "Ground";
            wallsTr.sortingOrder = 2;
            var wallsCol = wallsGo.AddComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
            var wMod = wallsGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            wMod.overrideArea = true;
            wMod.area = 1; // Not Walkable
            wMod.ignoreFromBuild = false;

            // Child Decoration
            var decGo = new GameObject("Decoration");
            decGo.transform.SetParent(gridGo.transform);
            decGo.transform.localPosition = Vector3.zero;
            var decTm = decGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var decTr = decGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            decTr.sortingLayerName = "Ground";
            decTr.sortingOrder = 3;
            var dMod = decGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            dMod.ignoreFromBuild = true;

            // Connect references
            pieceComp.GroundTilemap = groundTm;
            pieceComp.WallsTilemap = wallsTm;
            pieceComp.DecorationTilemap = decTm;

            // If Entrance, create EntryAnchor child
            if (roomType == DungeonRoomType.Entrance)
            {
                var anchorGo = new GameObject("EntryAnchor");
                anchorGo.transform.SetParent(rootGo.transform);
                anchorGo.transform.localPosition = Vector3.zero;
                var anchorField = typeof(DungeonPiece).GetField("entryAnchors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (anchorField != null && anchorField.GetValue(pieceComp) is List<Transform> list)
                {
                    list.Add(anchorGo.transform);
                }
            }

            // Save Prefab Asset
            string assetPath = System.IO.Path.Combine(folder, $"{pieceName}.prefab").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            DestroyImmediate(rootGo);

            if (prefab != null)
            {
                activePieceSet.AddPiecePrefab(prefab);
                EditorUtility.SetDirty(activePieceSet);
                AssetDatabase.SaveAssets();

                if (openInPrefabMode)
                {
                    AssetDatabase.OpenAsset(prefab);
                }
            }

            return prefab;
        }

        public GameObject CreateAndAssignOpeningCapPrefab(bool openInPrefabMode = true)
        {
            if (activePieceSet == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select or create an active Piece Set first.", "OK");
                return null;
            }

            string setPath = AssetDatabase.GetAssetPath(activePieceSet);
            string folder = string.IsNullOrEmpty(setPath)
                ? "Assets/Prefabs/Dungeon/Pieces"
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(setPath), "Pieces").Replace("\\", "/");

            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string capName = $"OpeningCap_{activePieceSet.ThemeName}";
            int openW = activePieceSet.OpeningWidth;

            var rootGo = new GameObject(capName);
            var col = rootGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(openW, 1f);
            col.offset = Vector2.zero;
            col.isTrigger = false;

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(rootGo.transform);
            gridGo.transform.localPosition = new Vector3(-openW * 0.5f, -0.5f, 0f);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(gridGo.transform);
            wallsGo.transform.localPosition = Vector3.zero;
            var wallsTm = wallsGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var wallsTr = wallsGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            wallsTr.sortingLayerName = "Ground";
            wallsTr.sortingOrder = 2;
            var wMod = wallsGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            wMod.overrideArea = true;
            wMod.area = 1; // Not Walkable
            wMod.ignoreFromBuild = false;

            string assetPath = System.IO.Path.Combine(folder, $"{capName}.prefab").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            DestroyImmediate(rootGo);

            if (prefab != null)
            {
                activePieceSet.SetOpeningCapPrefab(prefab);
                EditorUtility.SetDirty(activePieceSet);
                AssetDatabase.SaveAssets();

                if (openInPrefabMode)
                {
                    AssetDatabase.OpenAsset(prefab);
                }
            }

            return prefab;
        }

        private void DrawBatchSimulationTab()
        {
            EditorGUILayout.LabelField("Dungeon Batch Generation & Piece Set Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Run N seeds through the full dungeon generator and piece selector pipeline. " +
                "Reports success rates, average retries, seeds that required Opening Caps, and pieces that were never selected (coverage gaps).",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            // Simulation Config Card
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Simulation Parameters", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                _batchSeedCount = EditorGUILayout.IntSlider("Seed Count (N)", _batchSeedCount, 5, 200);
                _batchStartSeed = EditorGUILayout.IntField("Start Seed", _batchStartSeed);
                _batchMaxRetries = EditorGUILayout.IntSlider("Max Retries Per Seed", _batchMaxRetries, 1, 10);
                _batchCorridorLength = EditorGUILayout.IntSlider("Corridor Length (0 = touching)", _batchCorridorLength, 0, 8);

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                    if (GUILayout.Button($"▶ Run Batch Simulation ({_batchSeedCount} seeds)", GUILayout.Height(30)))
                    {
                        RunBatchSimulation();
                    }
                    GUI.backgroundColor = Color.white;

                    if (GUILayout.Button("🎮 Test in Combat_Sandbox Scene", GUILayout.Height(30), GUILayout.Width(220)))
                    {
                        TestInCombatSandboxScene();
                    }

                    if (GUILayout.Button("🧭 Verify NavMesh Connectivity", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        bool navPassed = VerifyNavMeshConnectivity(out string navMsg);
                        if (_lastBatchReport != null)
                        {
                            _lastBatchReport.NavMeshConnectivityPassed = navPassed;
                            _lastBatchReport.NavMeshConnectivityMessage = navMsg;
                        }
                        EditorUtility.DisplayDialog("NavMesh Connectivity", navMsg, "OK");
                    }
                }
            }

            EditorGUILayout.Space(6);

            if (_lastBatchReport != null)
            {
                DrawBatchReportSummary(_lastBatchReport);
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Run Batch Simulation' above to test the active Piece Set across multiple seeds.", MessageType.None);
            }
        }

        private void DrawBatchReportSummary(DungeonBatchReport report)
        {
            // Row 1: KPI Cards (Success Rate, Attempts, Opening Caps)
            using (new EditorGUILayout.HorizontalScope())
            {
                Color successColor = report.SuccessRate >= 95f ? new Color(0.2f, 0.7f, 0.2f) : (report.SuccessRate >= 75f ? Color.yellow : Color.red);
                DrawKpiCard("Success Rate", $"{report.SuccessRate:F1}%", $"{report.SuccessfulSeeds}/{report.TotalSeeds} passed", successColor);

                Color retryColor = report.AverageAttempts <= 1.2f ? new Color(0.2f, 0.7f, 0.2f) : Color.yellow;
                DrawKpiCard("Avg Attempts", $"{report.AverageAttempts:F2}", $"{report.MaxRetriesPerSeed} max allowed", retryColor);

                int cappedSeedsCount = report.SeedsRequiringCaps.Count;
                Color capColor = cappedSeedsCount == 0 ? new Color(0.2f, 0.7f, 0.2f) : Color.cyan;
                DrawKpiCard("Seeds with Caps", $"{cappedSeedsCount}", $"{cappedSeedsCount * 100f / report.TotalSeeds:F0}% of seeds", capColor);
            }

            EditorGUILayout.Space(4);

            // Row 2: KPI Cards (Seam Check, NavMesh Connectivity, Coverage Gaps)
            using (new EditorGUILayout.HorizontalScope())
            {
                // Seam Check Card
                Color seamColor = report.SeamCheckFailedSeeds == 0 ? new Color(0.2f, 0.7f, 0.2f) : Color.red;
                string seamVal = report.SeamCheckFailedSeeds == 0 ? "100% Valid" : $"{report.SeamCheckFailedSeeds} Failed";
                string seamSub = $"{report.SeamCheckPassedSeeds}/{report.SuccessfulSeeds} passed seam check";
                DrawKpiCard("Seam Check", seamVal, seamSub, seamColor);

                // NavMesh Connectivity Card
                Color navColor = report.NavMeshConnectivityPassed == true
                    ? new Color(0.2f, 0.7f, 0.2f)
                    : (report.NavMeshConnectivityPassed == false ? Color.red : Color.gray);
                string navVal = report.NavMeshConnectivityPassed == true
                    ? "Pass"
                    : (report.NavMeshConnectivityPassed == false ? "Failed" : "Not Run");
                string navSub = report.NavMeshConnectivityPassed.HasValue
                    ? (report.NavMeshConnectivityPassed.Value ? "Entrance -> Boss connected" : "Path disconnected")
                    : "Run scene build test";
                DrawKpiCard("NavMesh Path", navVal, navSub, navColor);

                // Coverage Gaps Card
                int gapCount = report.NeverSelectedPieceIds.Count;
                Color gapColor = gapCount == 0 ? new Color(0.2f, 0.7f, 0.2f) : Color.yellow;
                DrawKpiCard("Unused Pieces", $"{gapCount}", gapCount == 0 ? "Full coverage" : "Coverage gaps", gapColor);
            }

            EditorGUILayout.Space(6);

            // NavMesh Connectivity Message
            if (!string.IsNullOrEmpty(report.NavMeshConnectivityMessage))
            {
                var boxType = report.NavMeshConnectivityPassed == true ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox($"NavMesh Connectivity: {report.NavMeshConnectivityMessage}", boxType);
            }

            // Seam Failures Warning
            if (report.AllSeamFailures.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"❌ Seam Check Failures ({report.AllSeamFailures.Count}):\n" + string.Join("\n", report.AllSeamFailures),
                    MessageType.Error
                );
            }

            // Coverage Gaps Warning
            if (report.NeverSelectedPieceIds.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"⚠️ Pieces never selected across all {report.TotalSeeds} seeds: {string.Join(", ", report.NeverSelectedPieceIds)}. " +
                    "Check if their opening combinations are too restrictive or if weights/min constraints are low.",
                    MessageType.Warning
                );
            }

            // Seeds requiring caps info
            if (report.SeedsRequiringCaps.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"ℹ️ Seeds requiring Opening Caps: {string.Join(", ", report.SeedsRequiringCaps)}. " +
                    "These seeds used pieces whose openings were a superset of the required doorway sockets.",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(4);

            // Results Table
            EditorGUILayout.LabelField($"Detailed Seed Results ({report.SeedResults.Count} seeds)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Table Header
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("Initial Seed", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    GUILayout.Label("Used Seed", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    GUILayout.Label("Attempts", EditorStyles.miniBoldLabel, GUILayout.Width(65));
                    GUILayout.Label("Rooms", EditorStyles.miniBoldLabel, GUILayout.Width(55));
                    GUILayout.Label("Caps", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                    GUILayout.Label("Seam", EditorStyles.miniBoldLabel, GUILayout.Width(65));
                    GUILayout.Label("Status", EditorStyles.miniBoldLabel, GUILayout.Width(95));
                    GUILayout.Label("Selected Pieces", EditorStyles.miniBoldLabel);
                }

                _batchScroll = EditorGUILayout.BeginScrollView(_batchScroll, GUILayout.MaxHeight(260));

                for (int s = 0; s < report.SeedResults.Count; s++)
                {
                    var res = report.SeedResults[s];
                    using (new EditorGUILayout.HorizontalScope(s % 2 == 0 ? GUIStyle.none : EditorStyles.helpBox))
                    {
                        GUILayout.Label(res.InitialSeed.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                        GUILayout.Label(res.UsedSeed.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                        GUILayout.Label(res.Attempts.ToString(), EditorStyles.miniLabel, GUILayout.Width(65));
                        GUILayout.Label(res.RoomCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(55));
                        GUILayout.Label(res.CappedSidesCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(50));

                        if (!res.Success)
                        {
                            GUILayout.Label("-", EditorStyles.miniLabel, GUILayout.Width(65));
                        }
                        else if (res.SeamCheckPassed)
                        {
                            var seamPass = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.7f, 0.2f) } };
                            GUILayout.Label("✓ Valid", seamPass, GUILayout.Width(65));
                        }
                        else
                        {
                            var seamFail = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.red } };
                            GUILayout.Label("❌ Fail", seamFail, GUILayout.Width(65));
                        }

                        if (res.Success)
                        {
                            var passStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.7f, 0.2f) } };
                            GUILayout.Label(res.Attempts > 1 ? $"✓ (retry #{res.Attempts})" : "✓ Passed", passStyle, GUILayout.Width(95));
                        }
                        else
                        {
                            var failStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.red } };
                            GUILayout.Label("❌ Failed", failStyle, GUILayout.Width(95));
                        }

                        string piecesStr = res.SelectedPieceIds.Count > 0 ? string.Join(", ", res.SelectedPieceIds) : res.FailureMessage;
                        GUILayout.Label(piecesStr, EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawKpiCard(string title, string value, string subtitle, Color color)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(65)))
            {
                GUILayout.Label(title, EditorStyles.miniLabel);
                var valStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    normal = { textColor = color }
                };
                GUILayout.Label(value, valStyle);
                GUILayout.Label(subtitle, EditorStyles.miniLabel);
            }
        }

        private void RunBatchSimulation()
        {
            if (activePieceSet == null) return;
            var descriptors = activePieceSet.GetDescriptors();
            var genConfig = new DungeonGeneratorConfig(
                minNormalRooms: 5,
                maxNormalRooms: 8,
                branchProbability: 0.35f,
                loopProbability: 0f,
                safeRoomCount: 1,
                roomWidth: activePieceSet.CellSize.x,
                roomHeight: activePieceSet.CellSize.y,
                corridorLength: _batchCorridorLength,
                corridorWidth: 2
            );

            var batchConfig = new DungeonBatchConfig(
                seedCount: _batchSeedCount,
                startSeed: _batchStartSeed,
                maxRetriesPerSeed: _batchMaxRetries,
                openingWidth: activePieceSet.OpeningWidth,
                generatorConfig: genConfig
            );

            _lastBatchReport = DungeonBatchSimulator.RunSimulation(batchConfig, descriptors);
            Debug.Log($"[DungeonBatchSimulation] Completed {_batchSeedCount} seeds. Success: {_lastBatchReport.SuccessRate:F1}%, Avg attempts: {_lastBatchReport.AverageAttempts:F2}, Seams valid: {_lastBatchReport.SeamCheckPassedSeeds}/{_lastBatchReport.SuccessfulSeeds}");
        }

        public static bool VerifyNavMeshConnectivity(out string message)
        {
            var rooms = UnityEngine.Object.FindObjectsByType<SBGames.Dungeon.Markers.RoomVolume>(FindObjectsSortMode.None);
            Vector3? entrancePos = null;
            Vector3? bossPos = null;

            if (DungeonRoomRegistry.EntrancePosition != Vector3.zero)
            {
                entrancePos = DungeonRoomRegistry.EntrancePosition;
            }

            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                var piece = r.GetComponent<DungeonPiece>();
                if (piece != null && piece.RoomType == DungeonRoomType.Entrance)
                {
                    entrancePos = r.transform.position;
                }
                else if (r.gameObject.name.Contains("Entrance"))
                {
                    entrancePos = r.transform.position;
                }

                if (r.IsBossRoom || (piece != null && piece.RoomType == DungeonRoomType.Boss) || r.gameObject.name.Contains("Boss"))
                {
                    bossPos = r.transform.position;
                }
            }

            if (!entrancePos.HasValue)
            {
                message = "No Entrance room found in scene.";
                return false;
            }

            if (!bossPos.HasValue)
            {
                message = "No Boss room found in scene.";
                return false;
            }

            var path = new UnityEngine.AI.NavMeshPath();
            bool hasPath = UnityEngine.AI.NavMesh.CalculatePath(entrancePos.Value, bossPos.Value, UnityEngine.AI.NavMesh.AllAreas, path);

            if (hasPath && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                float totalDistance = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    totalDistance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                message = $"✓ NavMesh path valid! Entrance to Boss is connected ({totalDistance:F1}m, {path.corners.Length} corners).";
                return true;
            }
            else if (hasPath && path.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
            {
                message = "❌ NavMesh path partial: unreachable path from Entrance to Boss.";
                return false;
            }
            else
            {
                message = "❌ NavMesh path invalid: no path found between Entrance and Boss.";
                return false;
            }
        }

        private void TestInCombatSandboxScene()
        {
            const string scenePath = "Assets/Scenes/Combat_Sandbox.unity";
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (currentScene.path != scenePath)
            {
                if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
                }
                else
                {
                    return;
                }
            }

            var builder = UnityEngine.Object.FindAnyObjectByType<DungeonRuntimeBuilder>();
            if (builder != null)
            {
                var graph = builder.GenerateDungeonForRun(_batchStartSeed);
                bool navPassed = VerifyNavMeshConnectivity(out string navMsg);
                if (_lastBatchReport != null)
                {
                    _lastBatchReport.NavMeshConnectivityPassed = navPassed;
                    _lastBatchReport.NavMeshConnectivityMessage = navMsg;
                }
                Debug.Log($"[DungeonBatchRunner] Generated dungeon in Combat_Sandbox with seed {_batchStartSeed}. Rooms: {graph.Rooms.Count}, Doors: {graph.Doors.Count}. NavMesh: {navMsg}");
                EditorUtility.DisplayDialog("Dungeon Test Complete", $"Dungeon generated in Combat_Sandbox for seed {_batchStartSeed}!\nRooms: {graph.Rooms.Count}\nDoors: {graph.Doors.Count}\n\nNavMesh Connectivity:\n{navMsg}", "OK");
            }
            else
            {
                Debug.LogError("[DungeonBatchRunner] No DungeonRuntimeBuilder found in Combat_Sandbox scene!");
                EditorUtility.DisplayDialog("Error", "No DungeonRuntimeBuilder found in Combat_Sandbox scene!", "OK");
            }
        }
    }
}
