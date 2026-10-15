using System;
using System.Collections.Generic;
using UnityEngine;
using SBGames.Dungeon.Core.Pieces;

namespace SBGames.Dungeon.Unity.Pieces
{
    /// <summary>
    /// ScriptableObject defining a themed collection of Dungeon Piece prefabs and cell dimensions.
    /// Immutable at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPieceSet", menuName = "SBGames/Dungeon/Piece Set", order = 101)]
    public class PieceSetSO : ScriptableObject
    {
        [Header("Theme Configuration")]
        [SerializeField] private string themeName = "StoneDungeon";

        [Tooltip("Macro-cell dimensions in placement cells (default 10x10).")]
        [SerializeField] private Vector2Int cellSize = new Vector2Int(10, 10);

        [Header("Opening Cap Prefab")]
        [Tooltip("Prefab instantiated to close/seal an unused Opening on a placed Dungeon Piece.")]
        [SerializeField] private GameObject openingCapPrefab;

        [Header("Dungeon Piece Prefabs")]
        [Tooltip("List of authored piece prefabs (each must have a DungeonPiece component).")]
        [SerializeField] private List<GameObject> piecePrefabs = new List<GameObject>();

        public string ThemeName => themeName;
        public Vector2Int CellSize => cellSize;
        public GameObject OpeningCapPrefab => openingCapPrefab;
        public IReadOnlyList<GameObject> PiecePrefabs => piecePrefabs;

        public void AddPiecePrefab(GameObject prefab)
        {
            if (prefab != null && !piecePrefabs.Contains(prefab))
            {
                piecePrefabs.Add(prefab);
            }
        }

        public void RemovePiecePrefab(GameObject prefab)
        {
            piecePrefabs.Remove(prefab);
        }

        public void ClearPrefabs()
        {
            piecePrefabs.Clear();
        }

        /// <summary>
        /// Converts all authored piece prefabs into pure C# DungeonPieceDescriptors.
        /// </summary>
        public List<DungeonPieceDescriptor> GetDescriptors()
        {
            var descriptors = new List<DungeonPieceDescriptor>();
            for (int i = 0; i < piecePrefabs.Count; i++)
            {
                var go = piecePrefabs[i];
                if (go == null) continue;

                var piece = go.GetComponent<DungeonPiece>();
                if (piece != null)
                {
                    descriptors.Add(piece.ToDescriptor());
                }
            }
            return descriptors;
        }

        /// <summary>
        /// Looks up the prefab associated with a pieceId.
        /// </summary>
        public GameObject GetPrefabForPiece(string pieceId)
        {
            if (string.IsNullOrEmpty(pieceId)) return null;
            for (int i = 0; i < piecePrefabs.Count; i++)
            {
                var go = piecePrefabs[i];
                if (go == null) continue;
                var piece = go.GetComponent<DungeonPiece>();
                if (piece != null)
                {
                    string id = string.IsNullOrEmpty(piece.PieceId) ? go.name : piece.PieceId;
                    if (id == pieceId) return go;
                }
            }
            return null;
        }

        /// <summary>
        /// Validates the piece set against its cell dimensions and returns any validation badges.
        /// </summary>
        public List<PieceValidationBadge> ValidateSet()
        {
            return PieceValidator.ValidatePieceSet(GetDescriptors(), cellSize.x, cellSize.y);
        }

        /// <summary>
        /// Calculates the full 4 room types x 15 doorway opening subsets coverage report.
        /// </summary>
        public PieceSetCoverageReport GetCoverageReport()
        {
            return PieceSetCoverageCalculator.CalculateCoverage(GetDescriptors(), cellSize.x, cellSize.y);
        }
    }
}
