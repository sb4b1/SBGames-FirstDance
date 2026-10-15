using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Unity.Pieces;

namespace SBGames.Editor.Dungeon
{
    /// <summary>
    /// Editor utility and importer for authoring Dungeon Pieces from sprite sheets or sliced textures.
    /// Supports grid slicing, manual edge-click opening configuration, rotated variant generation,
    /// and automated prefab construction.
    /// </summary>
    public class DungeonSheetImporter
    {
        public class ImportPieceSlot
        {
            public Sprite SourceSprite;
            public string PieceName;
            public DungeonRoomType RoomType = DungeonRoomType.Normal;
            public OpeningFlags Openings = OpeningFlags.None;
            public bool IsRotatable = true;
            public bool GenerateRotatedVariants = false;
        }

        public static GameObject CreatePiecePrefab(
            Sprite sprite,
            string pieceName,
            DungeonRoomType roomType,
            OpeningFlags openings,
            bool isRotatable,
            string targetFolder,
            int cellWidth = 10,
            int cellHeight = 10,
            float cellSize = 1.0f)
        {
            if (string.IsNullOrEmpty(targetFolder))
            {
                targetFolder = "Assets/Prefabs/Dungeon/Pieces";
            }

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            // Create temporary root GameObject
            var rootGo = new GameObject(pieceName);

            // Sprite visual
            if (sprite != null)
            {
                var sr = rootGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = "Ground";
            }

            // BoxCollider2D boundary
            var col = rootGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellWidth * cellSize, cellHeight * cellSize);
            col.isTrigger = true;

            // RoomVolume component
            var roomVol = rootGo.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            roomVol.SetBounds(new Bounds(Vector3.zero, new Vector3(col.size.x, col.size.y, 2f)));
            roomVol.IsSafeRoom = (roomType == DungeonRoomType.SafeRoom);
            roomVol.IsBossRoom = (roomType == DungeonRoomType.Boss);

            // DungeonPiece component
            var pieceComp = rootGo.AddComponent<DungeonPiece>();
            pieceComp.PieceId = pieceName;
            pieceComp.RoomType = roomType;
            pieceComp.Openings = openings;
            pieceComp.IsRotatable = isRotatable;
            pieceComp.Footprint = Vector2Int.one;
            pieceComp.GenerateBuildableMaskFromGround(margin: 1, openingClearance: 2, cellWidth: cellWidth, cellHeight: cellHeight);

            // Save as Prefab Asset
            string assetPath = Path.Combine(targetFolder, $"{pieceName}.prefab").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
            UnityEngine.Object.DestroyImmediate(rootGo);

            return prefab;
        }

        /// <summary>
        /// Generates 3 rotated variants (90, 180, 270 deg) copying rotated openings correctly.
        /// </summary>
        public static List<GameObject> CreateRotatedVariants(
            Sprite baseSprite,
            string basePieceName,
            DungeonRoomType roomType,
            OpeningFlags baseOpenings,
            string targetFolder,
            int cellWidth = 10,
            int cellHeight = 10,
            float cellSize = 1.0f)
        {
            var results = new List<GameObject>();
            for (int k = 1; k <= 3; k++)
            {
                int deg = k * 90;
                string variantName = $"{basePieceName}_rot{deg}";
                var rotatedOpenings = baseOpenings.Rotate(k);

                // Note: Rotated variant is not marked rotatable since its specific orientation is baked
                var prefab = CreatePiecePrefab(
                    baseSprite,
                    variantName,
                    roomType,
                    rotatedOpenings,
                    isRotatable: false,
                    targetFolder,
                    cellWidth,
                    cellHeight,
                    cellSize
                );

                if (prefab != null)
                {
                    results.Add(prefab);
                }
            }
            return results;
        }
    }
}
