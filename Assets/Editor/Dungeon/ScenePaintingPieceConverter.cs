using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Markers;
using SBGames.Dungeon.Unity.Pieces;

namespace SBGames.Editor.Dungeon
{
    /// <summary>
    /// Utility for converting scene-painted Grids/Tilemaps into authored Dungeon Piece prefabs,
    /// and unpacking piece prefabs into the scene for live Tile Palette painting.
    /// </summary>
    public static class ScenePaintingPieceConverter
    {
        [MenuItem("GameObject/Dungeon/Convert Scene Painting to Piece Prefab", false, 20)]
        public static void ConvertSelectionToPiece()
        {
            var selectedGo = Selection.activeGameObject;
            if (selectedGo == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a GameObject (Grid or root with Tilemaps) to convert.", "OK");
                return;
            }

            int cellW = 16;
            int cellH = 16;
            int openingW = 2;

            // Find tilemaps
            var groundTm = FindTilemapByName(selectedGo, "Ground");
            var wallsTm = FindTilemapByName(selectedGo, "Walls");
            var decTm = FindTilemapByName(selectedGo, "Decoration");

            if (groundTm == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected hierarchy must have a Tilemap named 'Ground'.", "OK");
                return;
            }

            string pieceName = EditorUtility.SaveFilePanelInProject("Save Dungeon Piece Prefab", selectedGo.name + "_piece", "prefab", "Select location for piece prefab");
            if (string.IsNullOrEmpty(pieceName)) return;

            // Detect openings from Ground tilemap edge tiles
            OpeningFlags detectedOpenings = OpeningFlags.None;
            var northEdges = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.North);
            var southEdges = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.South);
            var westEdges = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.West);
            var eastEdges = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.East);

            bool HasAll(System.Collections.Generic.List<GridCoord> edges)
            {
                foreach (var e in edges)
                {
                    if (!groundTm.HasTile(new Vector3Int(e.X, e.Y, 0))) return false;
                }
                return true;
            }

            if (HasAll(northEdges)) detectedOpenings |= OpeningFlags.North;
            if (HasAll(southEdges)) detectedOpenings |= OpeningFlags.South;
            if (HasAll(westEdges)) detectedOpenings |= OpeningFlags.West;
            if (HasAll(eastEdges)) detectedOpenings |= OpeningFlags.East;

            if (detectedOpenings == OpeningFlags.None)
            {
                detectedOpenings = OpeningFlags.North; // fallback default
            }

            // Create piece root clone
            var rootGo = new GameObject(Path.GetFileNameWithoutExtension(pieceName));
            var col = rootGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellW, cellH);
            col.isTrigger = true;

            var roomVol = rootGo.AddComponent<RoomVolume>();
            roomVol.SetBounds(new Bounds(Vector3.zero, new Vector3(cellW, cellH, 2f)));

            var pieceComp = rootGo.AddComponent<DungeonPiece>();
            pieceComp.PieceId = rootGo.name;
            pieceComp.RoomType = DungeonRoomType.Normal;
            pieceComp.Openings = detectedOpenings;
            pieceComp.IsRotatable = true;
            pieceComp.Footprint = Vector2Int.one;

            // Clone grid or hierarchy
            var clonedGrid = Object.Instantiate(selectedGo, rootGo.transform);
            clonedGrid.name = "Grid";
            clonedGrid.transform.localPosition = new Vector3(-cellW * 0.5f, -cellH * 0.5f, 0f);

            pieceComp.GroundTilemap = FindTilemapByName(clonedGrid, "Ground");
            pieceComp.WallsTilemap = FindTilemapByName(clonedGrid, "Walls");
            pieceComp.DecorationTilemap = FindTilemapByName(clonedGrid, "Decoration");

            pieceComp.GenerateBuildableMaskFromGround(margin: 1, openingClearance: 2, cellWidth: cellW, cellHeight: cellH, openingWidth: openingW);

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, pieceName);
            Object.DestroyImmediate(rootGo);

            if (prefab != null)
            {
                EditorUtility.DisplayDialog("Success", $"Created piece prefab at '{pieceName}' with openings: {detectedOpenings}!", "OK");
                Selection.activeObject = prefab;
            }
        }

        [MenuItem("Assets/Dungeon/Load Piece Into Scene For Painting", false, 30)]
        public static void LoadPieceIntoScene()
        {
            var selectedPrefab = Selection.activeGameObject;
            if (selectedPrefab == null || selectedPrefab.GetComponent<DungeonPiece>() == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a DungeonPiece prefab in the Project window.", "OK");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
            instance.transform.position = Vector3.zero;
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Undo.RegisterCreatedObjectUndo(instance, "Load Piece Into Scene");
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static Tilemap FindTilemapByName(GameObject root, string name)
        {
            var tms = root.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tms.Length; i++)
            {
                if (tms[i].gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return tms[i];
                }
            }
            return null;
        }
    }
}
