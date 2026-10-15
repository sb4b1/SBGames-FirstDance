using System.Collections.Generic;
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
    /// Builder utility for generating the canonical Starter Piece Set shells,
    /// complete with Ground interiors, walled borders with Opening cutouts,
    /// buildable masks, entry anchors, and Opening Caps.
    /// </summary>
    public static class StarterPieceSetBuilder
    {
        private const string RootFolder = "Assets/DungeonPiece/StarterSet";
        private const string PiecesFolder = "Assets/DungeonPiece/StarterSet/Pieces";
        private const string PieceSetAssetPath = "Assets/DungeonPiece/StarterSet/StarterPieceSet.asset";
        private const string GroundTilePath = "Assets/Resources/World/Tiles/plains_30.asset";
        private const string WallTilePath = "Assets/Resources/World/Tiles/plains_36.asset";

        [MenuItem("Tools/SBGames/Build Starter Piece Set")]
        public static PieceSetSO BuildStarterPieceSet()
        {
            if (!Directory.Exists(RootFolder)) Directory.CreateDirectory(RootFolder);
            if (!Directory.Exists(PiecesFolder)) Directory.CreateDirectory(PiecesFolder);
            AssetDatabase.Refresh();

            var groundTile = AssetDatabase.LoadAssetAtPath<TileBase>(GroundTilePath);
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath);

            if (groundTile == null || wallTile == null)
            {
                Debug.LogError($"[StarterPieceSetBuilder] Missing tile assets: Ground={groundTile != null}, Wall={wallTile != null}");
                return null;
            }

            var pieceSet = AssetDatabase.LoadAssetAtPath<PieceSetSO>(PieceSetAssetPath);
            if (pieceSet == null)
            {
                pieceSet = ScriptableObject.CreateInstance<PieceSetSO>();
                pieceSet.SetThemeName("StoneDungeon");
                pieceSet.SetCellSize(new Vector2Int(16, 16));
                pieceSet.SetTilePixelSize(16);
                pieceSet.SetOpeningWidth(2);
                AssetDatabase.CreateAsset(pieceSet, PieceSetAssetPath);
            }
            else
            {
                pieceSet.SetThemeName("StoneDungeon");
                pieceSet.SetCellSize(new Vector2Int(16, 16));
                pieceSet.SetTilePixelSize(16);
                pieceSet.SetOpeningWidth(2);
                pieceSet.ClearPrefabs();
            }

            // Create Opening Cap
            var capPrefab = BuildOpeningCap(wallTile, pieceSet.OpeningWidth);
            if (capPrefab != null)
            {
                pieceSet.SetOpeningCapPrefab(capPrefab);
            }

            // Piece definitions: (name, roomType, openings)
            var definitions = new List<(string name, DungeonRoomType roomType, OpeningFlags openings)>
            {
                // Normal
                ("normal_deadend", DungeonRoomType.Normal, OpeningFlags.North),
                ("normal_straight", DungeonRoomType.Normal, OpeningFlags.North | OpeningFlags.South),
                ("normal_corner", DungeonRoomType.Normal, OpeningFlags.North | OpeningFlags.East),
                ("normal_t", DungeonRoomType.Normal, OpeningFlags.North | OpeningFlags.East | OpeningFlags.West),
                ("normal_cross", DungeonRoomType.Normal, OpeningFlags.All),

                // Entrance
                ("entrance_deadend", DungeonRoomType.Entrance, OpeningFlags.North),
                ("entrance_straight", DungeonRoomType.Entrance, OpeningFlags.North | OpeningFlags.South),
                ("entrance_corner", DungeonRoomType.Entrance, OpeningFlags.North | OpeningFlags.East),
                ("entrance_t", DungeonRoomType.Entrance, OpeningFlags.North | OpeningFlags.East | OpeningFlags.West),
                ("entrance_cross", DungeonRoomType.Entrance, OpeningFlags.All),

                // Boss
                ("boss_deadend", DungeonRoomType.Boss, OpeningFlags.North),
                ("boss_straight", DungeonRoomType.Boss, OpeningFlags.North | OpeningFlags.South),
                ("boss_corner", DungeonRoomType.Boss, OpeningFlags.North | OpeningFlags.East),
                ("boss_t", DungeonRoomType.Boss, OpeningFlags.North | OpeningFlags.East | OpeningFlags.West),
                ("boss_cross", DungeonRoomType.Boss, OpeningFlags.All),

                // SafeRoom
                ("saferoom_deadend", DungeonRoomType.SafeRoom, OpeningFlags.North),
                ("saferoom_straight", DungeonRoomType.SafeRoom, OpeningFlags.North | OpeningFlags.South),
                ("saferoom_corner", DungeonRoomType.SafeRoom, OpeningFlags.North | OpeningFlags.East),
                ("saferoom_t", DungeonRoomType.SafeRoom, OpeningFlags.North | OpeningFlags.East | OpeningFlags.West),
                ("saferoom_cross", DungeonRoomType.SafeRoom, OpeningFlags.All)
            };

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                var prefab = BuildPiecePrefab(def.name, def.roomType, def.openings, groundTile, wallTile, 16, 16, 2);
                if (prefab != null)
                {
                    pieceSet.AddPiecePrefab(prefab);
                }
            }

            EditorUtility.SetDirty(pieceSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StarterPieceSetBuilder] Successfully built Starter Piece Set at '{PieceSetAssetPath}' with {pieceSet.PiecePrefabs.Count} pieces and Opening Cap!");
            return pieceSet;
        }

        private static GameObject BuildPiecePrefab(
            string pieceId,
            DungeonRoomType roomType,
            OpeningFlags openings,
            TileBase groundTile,
            TileBase wallTile,
            int cellW,
            int cellH,
            int openingWidth)
        {
            var root = new GameObject(pieceId);

            // BoxCollider2D trigger
            var col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellW, cellH);
            col.isTrigger = true;

            // RoomVolume
            var roomVol = root.AddComponent<RoomVolume>();
            roomVol.SetBounds(new Bounds(Vector3.zero, new Vector3(cellW, cellH, 2f)));
            roomVol.IsSafeRoom = (roomType == DungeonRoomType.SafeRoom);
            roomVol.IsBossRoom = (roomType == DungeonRoomType.Boss);

            // DungeonPiece
            var pieceComp = root.AddComponent<DungeonPiece>();
            pieceComp.PieceId = pieceId;
            pieceComp.RoomType = roomType;
            pieceComp.Openings = openings;
            pieceComp.IsRotatable = true;
            pieceComp.Footprint = Vector2Int.one;

            // Child Grid
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform);
            gridGo.transform.localPosition = new Vector3(-cellW * 0.5f, -cellH * 0.5f, 0f);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            // Ground Tilemap
            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            groundGo.transform.localPosition = Vector3.zero;
            var groundTm = groundGo.AddComponent<Tilemap>();
            var groundTr = groundGo.AddComponent<TilemapRenderer>();
            groundTr.sortingLayerName = "Ground";
            groundTr.sortingOrder = 1;
            var gMod = groundGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            gMod.overrideArea = true;
            gMod.area = 0; // Walkable

            // Walls Tilemap
            var wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(gridGo.transform);
            wallsGo.transform.localPosition = Vector3.zero;
            var wallsTm = wallsGo.AddComponent<Tilemap>();
            var wallsTr = wallsGo.AddComponent<TilemapRenderer>();
            wallsTr.sortingLayerName = "Ground";
            wallsTr.sortingOrder = 2;
            var wallsCol = wallsGo.AddComponent<TilemapCollider2D>();
            var wMod = wallsGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            wMod.overrideArea = true;
            wMod.area = 1; // Not Walkable

            // Decoration Tilemap
            var decGo = new GameObject("Decoration");
            decGo.transform.SetParent(gridGo.transform);
            decGo.transform.localPosition = Vector3.zero;
            var decTm = decGo.AddComponent<Tilemap>();
            var decTr = decGo.AddComponent<TilemapRenderer>();
            decTr.sortingLayerName = "Ground";
            decTr.sortingOrder = 3;
            var dMod = decGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            dMod.ignoreFromBuild = true;

            pieceComp.GroundTilemap = groundTm;
            pieceComp.WallsTilemap = wallsTm;
            pieceComp.DecorationTilemap = decTm;

            // If Entrance: Add EntryAnchor
            if (roomType == DungeonRoomType.Entrance)
            {
                var anchorGo = new GameObject("EntryAnchor");
                anchorGo.transform.SetParent(root.transform);
                anchorGo.transform.localPosition = Vector3.zero;
                var anchorField = typeof(DungeonPiece).GetField("entryAnchors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (anchorField != null && anchorField.GetValue(pieceComp) is List<Transform> list)
                {
                    list.Add(anchorGo.transform);
                }
            }

            // Calculate opening edge tiles
            var openingCoords = new HashSet<GridCoord>();
            if ((openings & OpeningFlags.North) != 0)
            {
                foreach (var c in OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingWidth, CardinalDirection.North))
                    openingCoords.Add(c);
            }
            if ((openings & OpeningFlags.South) != 0)
            {
                foreach (var c in OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingWidth, CardinalDirection.South))
                    openingCoords.Add(c);
            }
            if ((openings & OpeningFlags.West) != 0)
            {
                foreach (var c in OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingWidth, CardinalDirection.West))
                    openingCoords.Add(c);
            }
            if ((openings & OpeningFlags.East) != 0)
            {
                foreach (var c in OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingWidth, CardinalDirection.East))
                    openingCoords.Add(c);
            }

            // Paint Ground interior + opening tiles
            for (int x = 0; x < cellW; x++)
            {
                for (int y = 0; y < cellH; y++)
                {
                    bool isBorder = (x == 0 || x == cellW - 1 || y == 0 || y == cellH - 1);
                    if (!isBorder || openingCoords.Contains(new GridCoord(x, y)))
                    {
                        groundTm.SetTile(new Vector3Int(x, y, 0), groundTile);
                    }
                }
            }

            // Paint Walls on perimeter (except where opening is present)
            for (int x = 0; x < cellW; x++)
            {
                for (int y = 0; y < cellH; y++)
                {
                    bool isBorder = (x == 0 || x == cellW - 1 || y == 0 || y == cellH - 1);
                    if (isBorder && !openingCoords.Contains(new GridCoord(x, y)))
                    {
                        wallsTm.SetTile(new Vector3Int(x, y, 0), wallTile);
                    }
                }
            }

            // Generate buildable mask
            pieceComp.GenerateBuildableMaskFromGround(margin: 1, openingClearance: 2, cellWidth: cellW, cellHeight: cellH, openingWidth: openingWidth);

            // Save Prefab
            string prefabPath = $"{PiecesFolder}/{pieceId}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static GameObject BuildOpeningCap(TileBase wallTile, int openingWidth)
        {
            string capPath = $"{RootFolder}/opening_cap.prefab";
            var capGo = new GameObject("opening_cap");

            var col = capGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(openingWidth, 1f);
            col.isTrigger = false;

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(capGo.transform);
            gridGo.transform.localPosition = new Vector3(-openingWidth * 0.5f, -0.5f, 0f);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(gridGo.transform);
            wallsGo.transform.localPosition = Vector3.zero;
            var wallsTm = wallsGo.AddComponent<Tilemap>();
            var wallsTr = wallsGo.AddComponent<TilemapRenderer>();
            wallsTr.sortingLayerName = "Ground";
            wallsTr.sortingOrder = 2;
            var wCol = wallsGo.AddComponent<TilemapCollider2D>();
            var wMod = wallsGo.AddComponent<NavMeshPlus.Components.NavMeshModifier>();
            wMod.overrideArea = true;
            wMod.area = 1; // Not Walkable

            for (int x = 0; x < openingWidth; x++)
            {
                wallsTm.SetTile(new Vector3Int(x, 0, 0), wallTile);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(capGo, capPath);
            Object.DestroyImmediate(capGo);
            return prefab;
        }
    }
}
