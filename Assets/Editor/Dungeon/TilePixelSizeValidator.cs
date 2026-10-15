using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using SBGames.Dungeon.Unity.Pieces;

namespace SBGames.Editor.Dungeon
{
    public class TilePixelSizeIssue
    {
        public Sprite Sprite { get; }
        public Texture2D Texture { get; }
        public string AssetPath { get; }
        public float CurrentPpu { get; }
        public int ExpectedPpu { get; }
        public Vector2 SpritePixelSize { get; }
        public string PieceId { get; }

        public TilePixelSizeIssue(Sprite sprite, int expectedPpu, string pieceId)
        {
            Sprite = sprite;
            ExpectedPpu = expectedPpu;
            PieceId = pieceId;

            if (sprite != null)
            {
                Texture = sprite.texture;
                AssetPath = AssetDatabase.GetAssetPath(sprite);
                CurrentPpu = sprite.pixelsPerUnit;
                SpritePixelSize = new Vector2(sprite.rect.width, sprite.rect.height);
            }
        }

        public string Description =>
            $"Sprite '{Sprite?.name}' on '{PieceId}': PPU={CurrentPpu:F0} (expected {ExpectedPpu}), Size={SpritePixelSize.x:F0}x{SpritePixelSize.y:F0}";
    }

    /// <summary>
    /// Editor utility for validating that tile sprites match the Piece Set's Tile Pixel Size (PPU and dimensions).
    /// Provides one-click texture importer PPU fix without modifying assets automatically.
    /// </summary>
    public static class TilePixelSizeValidator
    {
        /// <summary>
        /// Collects all sprites referenced across a piece's Ground, Walls, and Decoration Tilemaps.
        /// </summary>
        public static HashSet<Sprite> CollectTileSprites(DungeonPiece piece)
        {
            var sprites = new HashSet<Sprite>();
            if (piece == null) return sprites;

            void ScanTilemap(Tilemap tm)
            {
                if (tm == null) return;
                var bounds = tm.cellBounds;
                foreach (var pos in bounds.allPositionsWithin)
                {
                    var sprite = tm.GetSprite(pos);
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            ScanTilemap(piece.GroundTilemap);
            ScanTilemap(piece.WallsTilemap);
            ScanTilemap(piece.DecorationTilemap);

            // Also check SpriteRenderer if piece has one
            var sr = piece.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                sprites.Add(sr.sprite);
            }

            return sprites;
        }

        /// <summary>
        /// Validates that all sprites on a piece match the expected Tile Pixel Size (PPU and resolution).
        /// </summary>
        public static List<TilePixelSizeIssue> ValidatePieceTilePixelSize(DungeonPiece piece, int targetTilePixelSize)
        {
            var issues = new List<TilePixelSizeIssue>();
            if (piece == null || targetTilePixelSize <= 0) return issues;

            var sprites = CollectTileSprites(piece);
            foreach (var sprite in sprites)
            {
                if (sprite == null) continue;

                bool ppuMismatch = Mathf.Abs(sprite.pixelsPerUnit - targetTilePixelSize) > 0.01f;
                bool sizeMismatch = Mathf.RoundToInt(sprite.rect.width) != targetTilePixelSize ||
                                   Mathf.RoundToInt(sprite.rect.height) != targetTilePixelSize;

                if (ppuMismatch || sizeMismatch)
                {
                    issues.Add(new TilePixelSizeIssue(sprite, targetTilePixelSize, piece.PieceId));
                }
            }

            return issues;
        }

        /// <summary>
        /// Sets the TextureImporter spritePixelsPerUnit to targetPpu and reimports the texture.
        /// Explicit manual user action only. Never called automatically.
        /// </summary>
        public static bool FixTexturePpu(string assetPath, int targetPpu)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            importer.spritePixelsPerUnit = targetPpu;
            importer.SaveAndReimport();
            return true;
        }
    }
}
