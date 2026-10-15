using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Unity.Pieces;

namespace SBGames.Editor.Dungeon
{
    [CustomEditor(typeof(DungeonPiece))]
    public class DungeonPieceEditor : UnityEditor.Editor
    {
        private bool _showBuildableGrid = true;
        private const int DefaultGridDim = 16;
        private List<PieceValidationBadge> _manualValidationBadges = null;

        public static bool EditMaskInScene
        {
            get => EditorPrefs.GetBool("SBGames_DungeonPiece_EditMaskInScene", false);
            set => EditorPrefs.SetBool("SBGames_DungeonPiece_EditMaskInScene", value);
        }

        public static bool ShowMaskOverlay
        {
            get => EditorPrefs.GetBool("SBGames_DungeonPiece_ShowMaskOverlay", false);
            set => EditorPrefs.SetBool("SBGames_DungeonPiece_ShowMaskOverlay", value);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var piece = (DungeonPiece)target;
            int cellW = piece.CellSize.x;
            int cellH = piece.CellSize.y;

            EditorGUILayout.Space(4);

            // Validation Badges Header
            var badges = _manualValidationBadges ?? PieceValidator.ValidatePiece(
                piece.ToDescriptor(),
                cellW,
                cellH,
                openingWidth: 2,
                piece.GetGroundCellCoords(cellW, cellH));

            if (badges.Count > 0)
            {
                for (int i = 0; i < badges.Count; i++)
                {
                    var b = badges[i];
                    var boxType = b.IsError ? MessageType.Error : MessageType.Warning;
                    EditorGUILayout.HelpBox(b.Message, boxType);
                }
                EditorGUILayout.Space(4);
            }
            else
            {
                EditorGUILayout.HelpBox("✓ Dungeon Piece configuration is healthy and valid.", MessageType.Info);
                EditorGUILayout.Space(2);
            }

            // Tile Pixel Size validation
            int targetPpu = 16;
            var ppuIssues = TilePixelSizeValidator.ValidatePieceTilePixelSize(piece, targetPpu);
            if (ppuIssues.Count > 0)
            {
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
                EditorGUILayout.Space(2);
            }

            // Quick Actions: Fit Bounds, Generate Mask & Validate
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fit Bounds to Tiles", GUILayout.Height(28)))
                {
                    Undo.RecordObject(piece, "Fit Bounds to Painted Tiles");
                    piece.FitBoundsToPaintedTiles();
                    EditorUtility.SetDirty(piece);
                    _manualValidationBadges = PieceValidator.ValidatePiece(
                        piece.ToDescriptor(),
                        piece.CellSize.x,
                        piece.CellSize.y,
                        openingWidth: 2,
                        piece.GetGroundCellCoords(piece.CellSize.x, piece.CellSize.y));
                }

                if (GUILayout.Button("Generate Mask", GUILayout.Height(28)))
                {
                    Undo.RecordObject(piece, "Generate Buildable Mask from Ground");
                    piece.GenerateBuildableMaskFromGround(margin: 1, openingClearance: 2, cellWidth: piece.CellSize.x, cellHeight: piece.CellSize.y, openingWidth: 2);
                    EditorUtility.SetDirty(piece);
                    _manualValidationBadges = PieceValidator.ValidatePiece(
                        piece.ToDescriptor(),
                        piece.CellSize.x,
                        piece.CellSize.y,
                        openingWidth: 2,
                        piece.GetGroundCellCoords(piece.CellSize.x, piece.CellSize.y));
                }

                if (GUILayout.Button("Validate", GUILayout.Height(28), GUILayout.Width(75)))
                {
                    _manualValidationBadges = PieceValidator.ValidatePiece(
                        piece.ToDescriptor(),
                        piece.CellSize.x,
                        piece.CellSize.y,
                        openingWidth: 2,
                        piece.GetGroundCellCoords(piece.CellSize.x, piece.CellSize.y));
                }
            }

            EditorGUILayout.Space(6);

            // Core Identity
            EditorGUILayout.LabelField("Identity & Role", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pieceId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("role"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roomType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSize"));

            EditorGUILayout.Space(6);

            // Tilemaps
            EditorGUILayout.LabelField("Tilemaps (Ground, Walls, Decoration)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundTilemap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wallsTilemap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("decorationTilemap"));

            if (piece.GroundTilemap == null || piece.WallsTilemap == null)
            {
                if (GUILayout.Button("Auto-Find Child Tilemaps", EditorStyles.miniButton))
                {
                    Undo.RecordObject(piece, "Auto-Find Child Tilemaps");
                    piece.AutoFindTilemaps();
                    EditorUtility.SetDirty(piece);
                }
            }

            EditorGUILayout.Space(6);

            // Doorway Openings (Freeform & Cardinal)
            EditorGUILayout.LabelField($"Doorway Openings ({piece.OpeningsList.Count} freeform sockets)", EditorStyles.boldLabel);

            if (piece.OpeningsList.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < piece.OpeningsList.Count; i++)
                    {
                        var op = piece.OpeningsList[i];
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField($"Opening #{i + 1}", EditorStyles.boldLabel, GUILayout.Width(75));

                                var newDir = (CardinalDirection)EditorGUILayout.EnumPopup(op.Direction, GUILayout.Width(70));
                                if (newDir != op.Direction)
                                {
                                    Undo.RecordObject(piece, "Change Opening Direction");
                                    int nx = (newDir == CardinalDirection.East) ? (cellW - 1) : ((newDir == CardinalDirection.West) ? 0 : Mathf.Clamp(op.AnchorTile.X, 0, cellW - op.Width));
                                    int ny = (newDir == CardinalDirection.North) ? (cellH - 1) : ((newDir == CardinalDirection.South) ? 0 : Mathf.Clamp(op.AnchorTile.Y, 0, cellH - op.Width));
                                    op.Direction = newDir;
                                    op.AnchorTile = new GridCoord(nx, ny);
                                    piece.OpeningsList[i] = op;
                                    EditorUtility.SetDirty(piece);
                                    break;
                                }

                                if (GUILayout.Button("Center", EditorStyles.miniButton, GUILayout.Width(50)))
                                {
                                    Undo.RecordObject(piece, "Center Opening");
                                    if (op.Direction == CardinalDirection.North || op.Direction == CardinalDirection.South)
                                    {
                                        int cx = (cellW - op.Width) / 2;
                                        int cy = (op.Direction == CardinalDirection.North) ? (cellH - 1) : 0;
                                        op.AnchorTile = new GridCoord(cx, cy);
                                    }
                                    else
                                    {
                                        int cx = (op.Direction == CardinalDirection.East) ? (cellW - 1) : 0;
                                        int cy = (cellH - op.Width) / 2;
                                        op.AnchorTile = new GridCoord(cx, cy);
                                    }
                                    piece.OpeningsList[i] = op;
                                    EditorUtility.SetDirty(piece);
                                    break;
                                }

                                GUILayout.FlexibleSpace();

                                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                                {
                                    Undo.RecordObject(piece, "Remove Opening");
                                    piece.OpeningsList.RemoveAt(i);
                                    EditorUtility.SetDirty(piece);
                                    break;
                                }
                            }

                            // Interactive Position Slider along the wall
                            if (op.Direction == CardinalDirection.North || op.Direction == CardinalDirection.South)
                            {
                                int maxPos = Mathf.Max(0, cellW - op.Width);
                                int curX = Mathf.Clamp(op.AnchorTile.X, 0, maxPos);
                                int edgeY = (op.Direction == CardinalDirection.North) ? (cellH - 1) : 0;

                                EditorGUI.BeginChangeCheck();
                                int newX = EditorGUILayout.IntSlider($"Tile Position ({curX} - {curX + op.Width - 1})", curX, 0, maxPos);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(piece, "Move Opening");
                                    op.AnchorTile = new GridCoord(newX, edgeY);
                                    piece.OpeningsList[i] = op;
                                    EditorUtility.SetDirty(piece);
                                }
                            }
                            else
                            {
                                int maxPos = Mathf.Max(0, cellH - op.Width);
                                int curY = Mathf.Clamp(op.AnchorTile.Y, 0, maxPos);
                                int edgeX = (op.Direction == CardinalDirection.East) ? (cellW - 1) : 0;

                                EditorGUI.BeginChangeCheck();
                                int newY = EditorGUILayout.IntSlider($"Tile Position ({curY} - {curY + op.Width - 1})", curY, 0, maxPos);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(piece, "Move Opening");
                                    op.AnchorTile = new GridCoord(edgeX, newY);
                                    piece.OpeningsList[i] = op;
                                    EditorUtility.SetDirty(piece);
                                }
                            }
                        }
                    }

                    EditorGUILayout.Space(2);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Add:", GUILayout.Width(35));
                        if (GUILayout.Button("+ North", EditorStyles.miniButtonLeft))
                        {
                            Undo.RecordObject(piece, "Add North Opening");
                            piece.OpeningsList.Add(new PieceOpening((cellW - 2) / 2, cellH - 1, CardinalDirection.North, 2));
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("+ East", EditorStyles.miniButtonMid))
                        {
                            Undo.RecordObject(piece, "Add East Opening");
                            piece.OpeningsList.Add(new PieceOpening(cellW - 1, (cellH - 2) / 2, CardinalDirection.East, 2));
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("+ South", EditorStyles.miniButtonMid))
                        {
                            Undo.RecordObject(piece, "Add South Opening");
                            piece.OpeningsList.Add(new PieceOpening((cellW - 2) / 2, 0, CardinalDirection.South, 2));
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("+ West", EditorStyles.miniButtonRight))
                        {
                            Undo.RecordObject(piece, "Add West Opening");
                            piece.OpeningsList.Add(new PieceOpening(0, (cellH - 2) / 2, CardinalDirection.West, 2));
                            EditorUtility.SetDirty(piece);
                        }
                    }

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("✨ Auto-Detect Openings from Painted Ground", EditorStyles.miniButton))
                    {
                        Undo.RecordObject(piece, "Auto-Detect Openings");
                        AutoDetectOpeningsFromGround(piece, cellW, cellH);
                        EditorUtility.SetDirty(piece);
                    }
                }
            }
            else
            {
                // Legacy Cardinal Direction Toggles
                var openingsProp = serializedObject.FindProperty("openings");
                var currentOpenings = (OpeningFlags)openingsProp.intValue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Cardinal Openings (Centered):", EditorStyles.miniBoldLabel);

                    // North
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        bool n = DrawDirectionToggle("N", (currentOpenings & OpeningFlags.North) != 0);
                        if (n != ((currentOpenings & OpeningFlags.North) != 0))
                        {
                            currentOpenings = n ? (currentOpenings | OpeningFlags.North) : (currentOpenings & ~OpeningFlags.North);
                        }
                        GUILayout.FlexibleSpace();
                    }

                    // West / East
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        bool w = DrawDirectionToggle("W", (currentOpenings & OpeningFlags.West) != 0);
                        if (w != ((currentOpenings & OpeningFlags.West) != 0))
                        {
                            currentOpenings = w ? (currentOpenings | OpeningFlags.West) : (currentOpenings & ~OpeningFlags.West);
                        }

                        GUILayout.Space(30);

                        bool e = DrawDirectionToggle("E", (currentOpenings & OpeningFlags.East) != 0);
                        if (e != ((currentOpenings & OpeningFlags.East) != 0))
                        {
                            currentOpenings = e ? (currentOpenings | OpeningFlags.East) : (currentOpenings & ~OpeningFlags.East);
                        }
                        GUILayout.FlexibleSpace();
                    }

                    // South
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        bool s = DrawDirectionToggle("S", (currentOpenings & OpeningFlags.South) != 0);
                        if (s != ((currentOpenings & OpeningFlags.South) != 0))
                        {
                            currentOpenings = s ? (currentOpenings | OpeningFlags.South) : (currentOpenings & ~OpeningFlags.South);
                        }
                        GUILayout.FlexibleSpace();
                    }

                    openingsProp.intValue = (int)currentOpenings;

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("Convert to Freeform Openings", EditorStyles.miniButton))
                    {
                        Undo.RecordObject(piece, "Convert to Freeform Openings");
                        for (int d = 0; d < 4; d++)
                        {
                            var dir = (CardinalDirection)d;
                            if (currentOpenings.ContainsDirection(dir))
                            {
                                int ax = (dir == CardinalDirection.North || dir == CardinalDirection.South) ? (cellW - 2) / 2 : (dir == CardinalDirection.East ? cellW - 1 : 0);
                                int ay = (dir == CardinalDirection.East || dir == CardinalDirection.West) ? (cellH - 2) / 2 : (dir == CardinalDirection.North ? cellH - 1 : 0);
                                piece.OpeningsList.Add(new PieceOpening(ax, ay, dir, 2));
                            }
                        }
                        EditorUtility.SetDirty(piece);
                    }
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("isRotatable"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hasDirectionalArt"));

            EditorGUILayout.Space(6);

            // Weights & Limits
            EditorGUILayout.LabelField("Generation Weights & Constraints", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minCountPerDungeon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxCountPerDungeon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("footprint"));

            EditorGUILayout.Space(6);

            // Buildable Mask Matrix Editor
            _showBuildableGrid = EditorGUILayout.Foldout(_showBuildableGrid, $"Buildable Placement Mask ({piece.BuildableCells.Count} cells)", true);
            if (_showBuildableGrid)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var oldBg = GUI.backgroundColor;
                        if (EditMaskInScene) GUI.backgroundColor = new Color(0.3f, 0.95f, 0.4f);
                        if (GUILayout.Button(EditMaskInScene ? "✓ Scene Mask Painting: ENABLED" : "✎ Enable Scene Mask Painting", GUILayout.Height(26)))
                        {
                            EditMaskInScene = !EditMaskInScene;
                            if (EditMaskInScene) ShowMaskOverlay = true;
                            SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = oldBg;

                        if (ShowMaskOverlay) GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
                        if (GUILayout.Button(ShowMaskOverlay ? "👁 Overlay: ON" : "👁 Overlay: OFF", GUILayout.Width(110), GUILayout.Height(26)))
                        {
                            ShowMaskOverlay = !ShowMaskOverlay;
                            SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = oldBg;
                    }

                    if (EditMaskInScene)
                    {
                        EditorGUILayout.HelpBox("Scene View mask painting is ACTIVE. Click-drag in Scene View to paint buildable cells (Shift to erase). Tilemap brush painting is paused.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Scene View mask painting is DISABLED. You can freely paint tiles on Tilemaps. Click 'Enable Scene Mask Painting' when you want to edit the mask in Scene View.", MessageType.Info);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Fill All", EditorStyles.miniButtonLeft))
                        {
                            Undo.RecordObject(piece, "Fill Buildable Mask");
                            piece.FillBuildableBox(cellW, cellH, margin: 0);
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("Fill Interior (Inset 1)", EditorStyles.miniButtonMid))
                        {
                            Undo.RecordObject(piece, "Fill Interior Buildable Mask");
                            piece.FillBuildableBox(cellW, cellH, margin: 1);
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("Clear All", EditorStyles.miniButtonRight))
                        {
                            Undo.RecordObject(piece, "Clear Buildable Mask");
                            piece.ClearBuildableCells();
                            EditorUtility.SetDirty(piece);
                        }
                    }

                    EditorGUILayout.Space(4);
                    DrawInteractiveMaskGrid(piece, cellW, cellH);
                }
            }

            EditorGUILayout.Space(6);

            // Anchors & Room Volumes
            EditorGUILayout.LabelField("Anchors & Physical Boundaries", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entryAnchors"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exitAnchors"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roomVolumes"), true);

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawDirectionToggle(string label, bool active)
        {
            var oldColor = GUI.backgroundColor;
            if (active) GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);
            bool clicked = GUILayout.Toggle(active, label, "Button", GUILayout.Width(36), GUILayout.Height(24));
            GUI.backgroundColor = oldColor;
            return clicked;
        }

        private void DrawInteractiveMaskGrid(DungeonPiece piece, int width, int height)
        {
            float cellSize = 18f;
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
                        GUI.backgroundColor = isBuildable ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.25f, 0.25f, 0.25f, 0.5f);

                        if (GUILayout.Button(GUIContent.none, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                        {
                            Undo.RecordObject(piece, "Toggle Buildable Cell");
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

        private void OnSceneGUI()
        {
            var piece = (DungeonPiece)target;
            if (piece == null) return;

            int cellW = piece.CellSize.x;
            int cellH = piece.CellSize.y;
            Vector3 center = piece.transform.position;
            float halfW = cellW * 0.5f;
            float halfH = cellH * 0.5f;
            Vector3 origin = center - new Vector3(halfW, halfH, 0f);

            // 0. Floating Scene View HUD for toggling Mask Painting
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12, 12, 280, 56), EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldCol = GUI.backgroundColor;
                if (EditMaskInScene) GUI.backgroundColor = new Color(0.3f, 0.95f, 0.4f);
                if (GUILayout.Button(EditMaskInScene ? "✓ Mask Paint: ON" : "✎ Mask Paint: OFF", GUILayout.Height(24)))
                {
                    EditMaskInScene = !EditMaskInScene;
                    if (EditMaskInScene) ShowMaskOverlay = true;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = oldCol;

                if (ShowMaskOverlay) GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
                if (GUILayout.Button(ShowMaskOverlay ? "👁 Overlay: ON" : "👁 Overlay: OFF", GUILayout.Width(100), GUILayout.Height(24)))
                {
                    ShowMaskOverlay = !ShowMaskOverlay;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = oldCol;
            }
            GUILayout.Label(EditMaskInScene ? "Active: Click-drag paints mask (Shift=erase)" : "Tilemap brush enabled (Mask disabled)", EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();

            // 1. Draw outer dynamic cell frame
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Vector3 p0 = center + new Vector3(-halfW, -halfH, 0f);
            Vector3 p1 = center + new Vector3(halfW, -halfH, 0f);
            Vector3 p2 = center + new Vector3(halfW, halfH, 0f);
            Vector3 p3 = center + new Vector3(-halfW, halfH, 0f);
            Handles.DrawPolyLine(p0, p1, p2, p3, p0);

            // 2. Draw 2-tile Opening markers and interactive handles
            if (piece.OpeningsList.Count > 0)
            {
                for (int i = 0; i < piece.OpeningsList.Count; i++)
                {
                    var op = piece.OpeningsList[i];
                    Vector3 opPos;
                    Vector3 opSize;
                    if (op.Direction == CardinalDirection.North)
                    {
                        opPos = origin + new Vector3(op.AnchorTile.X + (op.Width * 0.5f), cellH, 0f);
                        opSize = new Vector3(op.Width, 0.4f, 0.1f);
                    }
                    else if (op.Direction == CardinalDirection.South)
                    {
                        opPos = origin + new Vector3(op.AnchorTile.X + (op.Width * 0.5f), 0f, 0f);
                        opSize = new Vector3(op.Width, 0.4f, 0.1f);
                    }
                    else if (op.Direction == CardinalDirection.East)
                    {
                        opPos = origin + new Vector3(cellW, op.AnchorTile.Y + (op.Width * 0.5f), 0f);
                        opSize = new Vector3(0.4f, op.Width, 0.1f);
                    }
                    else // West
                    {
                        opPos = origin + new Vector3(0f, op.AnchorTile.Y + (op.Width * 0.5f), 0f);
                        opSize = new Vector3(0.4f, op.Width, 0.1f);
                    }

                    // Draw opening socket boundary box and drag slider
                    Handles.color = new Color(0.2f, 0.95f, 0.2f, 0.9f);
                    float hSize = HandleUtility.GetHandleSize(opPos) * 0.18f;

                    if (op.Direction == CardinalDirection.North)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 movedPos = Handles.Slider(opPos, Vector3.right, hSize * 0.9f, Handles.ConeHandleCap, 1.0f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(piece, "Drag Opening Along North Wall");
                            int newX = Mathf.Clamp(Mathf.RoundToInt(movedPos.x - origin.x - (op.Width * 0.5f)), 0, cellW - op.Width);
                            op.AnchorTile = new GridCoord(newX, cellH - 1);
                            piece.OpeningsList[i] = op;
                            EditorUtility.SetDirty(piece);
                        }
                        Handles.Label(opPos + new Vector3(-0.5f, 0.5f, 0f), $"North [{op.AnchorTile.X}..{op.AnchorTile.X + op.Width - 1}]");
                    }
                    else if (op.Direction == CardinalDirection.South)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 movedPos = Handles.Slider(opPos, Vector3.right, hSize * 0.9f, Handles.ConeHandleCap, 1.0f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(piece, "Drag Opening Along South Wall");
                            int newX = Mathf.Clamp(Mathf.RoundToInt(movedPos.x - origin.x - (op.Width * 0.5f)), 0, cellW - op.Width);
                            op.AnchorTile = new GridCoord(newX, 0);
                            piece.OpeningsList[i] = op;
                            EditorUtility.SetDirty(piece);
                        }
                        Handles.Label(opPos + new Vector3(-0.5f, -0.7f, 0f), $"South [{op.AnchorTile.X}..{op.AnchorTile.X + op.Width - 1}]");
                    }
                    else if (op.Direction == CardinalDirection.East)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 movedPos = Handles.Slider(opPos, Vector3.up, hSize * 0.9f, Handles.ConeHandleCap, 1.0f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(piece, "Drag Opening Along East Wall");
                            int newY = Mathf.Clamp(Mathf.RoundToInt(movedPos.y - origin.y - (op.Width * 0.5f)), 0, cellH - op.Width);
                            op.AnchorTile = new GridCoord(cellW - 1, newY);
                            piece.OpeningsList[i] = op;
                            EditorUtility.SetDirty(piece);
                        }
                        Handles.Label(opPos + new Vector3(0.5f, -0.3f, 0f), $"East [{op.AnchorTile.Y}..{op.AnchorTile.Y + op.Width - 1}]");
                    }
                    else // West
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 movedPos = Handles.Slider(opPos, Vector3.up, hSize * 0.9f, Handles.ConeHandleCap, 1.0f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(piece, "Drag Opening Along West Wall");
                            int newY = Mathf.Clamp(Mathf.RoundToInt(movedPos.y - origin.y - (op.Width * 0.5f)), 0, cellH - op.Width);
                            op.AnchorTile = new GridCoord(0, newY);
                            piece.OpeningsList[i] = op;
                            EditorUtility.SetDirty(piece);
                        }
                        Handles.Label(opPos + new Vector3(-1.8f, -0.3f, 0f), $"West [{op.AnchorTile.Y}..{op.AnchorTile.Y + op.Width - 1}]");
                    }

                    // Green line indicator for opening width
                    Handles.color = Color.green;
                    if (op.Direction == CardinalDirection.North || op.Direction == CardinalDirection.South)
                    {
                        Handles.DrawLine(opPos - new Vector3(opSize.x * 0.5f, 0f, 0f), opPos + new Vector3(opSize.x * 0.5f, 0f, 0f), 5f);
                    }
                    else
                    {
                        Handles.DrawLine(opPos - new Vector3(0f, opSize.y * 0.5f, 0f), opPos + new Vector3(0f, opSize.y * 0.5f, 0f), 5f);
                    }
                }
            }
            else
            {
                float openingWidth = 2.0f;
                DrawOpeningHandle(piece, CardinalDirection.North, center + new Vector3(0f, halfH, 0f), new Vector3(openingWidth, 0.4f, 0.1f));
                DrawOpeningHandle(piece, CardinalDirection.South, center + new Vector3(0f, -halfH, 0f), new Vector3(openingWidth, 0.4f, 0.1f));
                DrawOpeningHandle(piece, CardinalDirection.East, center + new Vector3(halfW, 0f, 0f), new Vector3(0.4f, openingWidth, 0.1f));
                DrawOpeningHandle(piece, CardinalDirection.West, center + new Vector3(-halfW, 0f, 0f), new Vector3(0.4f, openingWidth, 0.1f));
            }

            // 3. Draw buildable mask overlay (only if enabled or overlay turned on)
            if (ShowMaskOverlay || EditMaskInScene)
            {
                var buildableCells = piece.BuildableCells;
                for (int i = 0; i < buildableCells.Count; i++)
                {
                    var cell = buildableCells[i];
                    Vector3 cellPos = origin + new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                    Handles.DrawSolidRectangleWithOutline(
                        new[]
                        {
                            cellPos + new Vector3(-0.45f, -0.45f, 0f),
                            cellPos + new Vector3(0.45f, -0.45f, 0f),
                            cellPos + new Vector3(0.45f, 0.45f, 0f),
                            cellPos + new Vector3(-0.45f, 0.45f, 0f)
                        },
                        new Color(0.2f, 0.9f, 0.3f, EditMaskInScene ? 0.3f : 0.15f),
                        new Color(0.2f, 0.9f, 0.3f, EditMaskInScene ? 0.7f : 0.4f)
                    );
                }
            }

            // 4. Interactive Scene View Mask Painting (ONLY active when EditMaskInScene == true)
            if (EditMaskInScene)
            {
                Event e = Event.current;
                if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    var plane = new Plane(Vector3.forward, piece.transform.position);
                    if (plane.Raycast(ray, out float enter))
                    {
                        Vector3 hit = ray.GetPoint(enter);
                        int cx = Mathf.FloorToInt(hit.x - origin.x);
                        int cy = Mathf.FloorToInt(hit.y - origin.y);

                        if (cx >= 0 && cx < cellW && cy >= 0 && cy < cellH)
                        {
                            var cellCoord = new Vector2Int(cx, cy);
                            bool erase = e.shift;

                            if (erase)
                            {
                                if (piece.IsCellBuildable(cellCoord))
                                {
                                    Undo.RecordObject(piece, "Erase Buildable Mask Cell");
                                    piece.RemoveBuildableCell(cellCoord);
                                    EditorUtility.SetDirty(piece);
                                    e.Use();
                                }
                            }
                            else
                            {
                                if (!piece.IsCellBuildable(cellCoord))
                                {
                                    Undo.RecordObject(piece, "Paint Buildable Mask Cell");
                                    piece.AddBuildableCell(cellCoord);
                                    EditorUtility.SetDirty(piece);
                                    e.Use();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DrawOpeningHandle(DungeonPiece piece, CardinalDirection dir, Vector3 pos, Vector3 size)
        {
            bool hasOpening = piece.Openings.ContainsDirection(dir);
            Handles.color = hasOpening ? new Color(0.2f, 0.95f, 0.2f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.35f);

            float handleSize = HandleUtility.GetHandleSize(pos) * 0.15f;
            if (Handles.Button(pos, Quaternion.identity, handleSize, handleSize * 1.5f, Handles.RectangleHandleCap))
            {
                Undo.RecordObject(piece, "Toggle Opening Direction");
                piece.Openings = piece.Openings ^ dir.ToOpeningFlag();
                EditorUtility.SetDirty(piece);
            }

            // Draw line indicator for opening width
            Handles.color = hasOpening ? Color.green : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            if (dir == CardinalDirection.North || dir == CardinalDirection.South)
            {
                Handles.DrawLine(pos - new Vector3(size.x * 0.5f, 0f, 0f), pos + new Vector3(size.x * 0.5f, 0f, 0f), hasOpening ? 4f : 2f);
            }
            else
            {
                Handles.DrawLine(pos - new Vector3(0f, size.y * 0.5f, 0f), pos + new Vector3(0f, size.y * 0.5f, 0f), hasOpening ? 4f : 2f);
            }
        }

        private void AutoDetectOpeningsFromGround(DungeonPiece piece, int cellW, int cellH)
        {
            var ground = piece.GetGroundCellCoords(cellW, cellH);
            if (ground == null || ground.Count == 0) return;

            var detected = new List<PieceOpening>();

            // North edge (Y = cellH - 1)
            for (int x = 0; x <= cellW - 2; x++)
            {
                if (ground.Contains(new GridCoord(x, cellH - 1)) && ground.Contains(new GridCoord(x + 1, cellH - 1)))
                {
                    detected.Add(new PieceOpening(x, cellH - 1, CardinalDirection.North, 2));
                    x++;
                }
            }

            // South edge (Y = 0)
            for (int x = 0; x <= cellW - 2; x++)
            {
                if (ground.Contains(new GridCoord(x, 0)) && ground.Contains(new GridCoord(x + 1, 0)))
                {
                    detected.Add(new PieceOpening(x, 0, CardinalDirection.South, 2));
                    x++;
                }
            }

            // East edge (X = cellW - 1)
            for (int y = 0; y <= cellH - 2; y++)
            {
                if (ground.Contains(new GridCoord(cellW - 1, y)) && ground.Contains(new GridCoord(cellW - 1, y + 1)))
                {
                    detected.Add(new PieceOpening(cellW - 1, y, CardinalDirection.East, 2));
                    y++;
                }
            }

            // West edge (X = 0)
            for (int y = 0; y <= cellH - 2; y++)
            {
                if (ground.Contains(new GridCoord(0, y)) && ground.Contains(new GridCoord(0, y + 1)))
                {
                    detected.Add(new PieceOpening(0, y, CardinalDirection.West, 2));
                    y++;
                }
            }

            if (detected.Count > 0)
            {
                piece.OpeningsList.Clear();
                piece.OpeningsList.AddRange(detected);
            }
        }
    }
}
