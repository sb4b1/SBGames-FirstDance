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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var piece = (DungeonPiece)target;

            EditorGUILayout.Space(4);

            // Validation Badges Header
            var badges = _manualValidationBadges ?? PieceValidator.ValidatePiece(
                piece.ToDescriptor(),
                DefaultGridDim,
                DefaultGridDim,
                openingWidth: 2,
                piece.GetGroundCellCoords(DefaultGridDim, DefaultGridDim));

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

            // Quick Actions: Generate Mask & Validate
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Buildable Mask from Ground", GUILayout.Height(28)))
                {
                    Undo.RecordObject(piece, "Generate Buildable Mask from Ground");
                    piece.GenerateBuildableMaskFromGround(margin: 1, openingClearance: 2, cellWidth: DefaultGridDim, cellHeight: DefaultGridDim, openingWidth: 2);
                    EditorUtility.SetDirty(piece);
                    _manualValidationBadges = PieceValidator.ValidatePiece(
                        piece.ToDescriptor(),
                        DefaultGridDim,
                        DefaultGridDim,
                        openingWidth: 2,
                        piece.GetGroundCellCoords(DefaultGridDim, DefaultGridDim));
                }

                if (GUILayout.Button("Validate", GUILayout.Height(28), GUILayout.Width(80)))
                {
                    _manualValidationBadges = PieceValidator.ValidatePiece(
                        piece.ToDescriptor(),
                        DefaultGridDim,
                        DefaultGridDim,
                        openingWidth: 2,
                        piece.GetGroundCellCoords(DefaultGridDim, DefaultGridDim));
                }
            }

            EditorGUILayout.Space(6);

            // Core Identity
            EditorGUILayout.LabelField("Identity & Room Type", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pieceId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("roomType"));

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

            // Openings & Cardinal Direction Toggles
            EditorGUILayout.LabelField("Doorway Openings", EditorStyles.boldLabel);
            var openingsProp = serializedObject.FindProperty("openings");
            var currentOpenings = (OpeningFlags)openingsProp.intValue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Cardinal Openings:", EditorStyles.miniBoldLabel);

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
                    EditorGUILayout.HelpBox("Click cells to toggle buildable status (green = buildable by DM). Or paint in Scene View (click-drag, Shift to erase).", MessageType.None);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Fill All", EditorStyles.miniButtonLeft))
                        {
                            Undo.RecordObject(piece, "Fill Buildable Mask");
                            piece.FillBuildableBox(DefaultGridDim, DefaultGridDim, margin: 0);
                            EditorUtility.SetDirty(piece);
                        }
                        if (GUILayout.Button("Fill Interior (Inset 1)", EditorStyles.miniButtonMid))
                        {
                            Undo.RecordObject(piece, "Fill Interior Buildable Mask");
                            piece.FillBuildableBox(DefaultGridDim, DefaultGridDim, margin: 1);
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
                    DrawInteractiveMaskGrid(piece, DefaultGridDim, DefaultGridDim);
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

            Vector3 center = piece.transform.position;
            float size = DefaultGridDim;
            float half = size * 0.5f;

            // 1. Draw outer 16x16 cell boundary
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Vector3 p0 = center + new Vector3(-half, -half, 0f);
            Vector3 p1 = center + new Vector3(half, -half, 0f);
            Vector3 p2 = center + new Vector3(half, half, 0f);
            Vector3 p3 = center + new Vector3(-half, half, 0f);
            Handles.DrawPolyLine(p0, p1, p2, p3, p0);

            // 2. Draw 2-tile Opening markers and interactive toggle handles
            float openingWidth = 2.0f;
            DrawOpeningHandle(piece, CardinalDirection.North, center + new Vector3(0f, half, 0f), new Vector3(openingWidth, 0.4f, 0.1f));
            DrawOpeningHandle(piece, CardinalDirection.South, center + new Vector3(0f, -half, 0f), new Vector3(openingWidth, 0.4f, 0.1f));
            DrawOpeningHandle(piece, CardinalDirection.East, center + new Vector3(half, 0f, 0f), new Vector3(0.4f, openingWidth, 0.1f));
            DrawOpeningHandle(piece, CardinalDirection.West, center + new Vector3(-half, 0f, 0f), new Vector3(0.4f, openingWidth, 0.1f));

            // 3. Draw buildable mask overlay
            Vector3 origin = center - new Vector3(half, half, 0f);
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
                    new Color(0.2f, 0.9f, 0.3f, 0.25f),
                    new Color(0.2f, 0.9f, 0.3f, 0.6f)
                );
            }

            // 4. Interactive Scene View Mask Painting (Click-Drag to paint, Shift+Click-Drag to erase)
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

                    if (cx >= 0 && cx < DefaultGridDim && cy >= 0 && cy < DefaultGridDim)
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
    }
}
