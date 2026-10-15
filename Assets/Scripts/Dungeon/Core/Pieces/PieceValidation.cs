using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    public enum PieceValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    /// <summary>
    /// Pure C# validation badge model indicating data anomalies or constraint violations.
    /// Follows the project's Validation Badge convention.
    /// </summary>
    public readonly struct PieceValidationBadge
    {
        public readonly PieceValidationSeverity Severity;
        public readonly string Message;
        public readonly string PieceId;

        public bool IsError => Severity == PieceValidationSeverity.Error;
        public bool IsWarning => Severity == PieceValidationSeverity.Warning;

        public PieceValidationBadge(PieceValidationSeverity severity, string message, string pieceId = null)
        {
            Severity = severity;
            Message = message;
            PieceId = pieceId ?? string.Empty;
        }

        public override string ToString() => $"[{Severity}] {(string.IsNullOrEmpty(PieceId) ? "" : $"({PieceId}) ")}{Message}";
    }

    /// <summary>
    /// Pure C# validator for individual Dungeon Pieces and complete Piece Sets.
    /// </summary>
    public static class PieceValidator
    {
        /// <summary>
        /// Validates a single DungeonPieceDescriptor against cell dimensions.
        /// </summary>
        public static List<PieceValidationBadge> ValidatePiece(
            DungeonPieceDescriptor piece,
            int cellWidth,
            int cellHeight)
        {
            var badges = new List<PieceValidationBadge>();
            if (piece == null)
            {
                badges.Add(new PieceValidationBadge(PieceValidationSeverity.Error, "Piece descriptor is null."));
                return badges;
            }

            string id = piece.PieceId ?? "<Unnamed>";

            // 1. Error: No Openings
            if (piece.Openings == OpeningFlags.None)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    "Piece has no openings configured (must have at least 1 cardinal opening).",
                    id
                ));
            }

            // 2. Error: Footprint larger than 1x1 in v1
            if (piece.Footprint.X != 1 || piece.Footprint.Y != 1)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    $"Footprint {piece.Footprint} is larger than 1x1 (multi-cell pieces are unsupported in v1).",
                    id
                ));
            }

            // 3. Error: Rotatable on non-square cell
            if (piece.IsRotatable && cellWidth != cellHeight)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    $"Piece is marked rotatable, but cell dimensions ({cellWidth}x{cellHeight}) are not square.",
                    id
                ));
            }

            // 4. Error: Buildable Mask outside cell boundaries
            if (piece.BuildableMask != null)
            {
                foreach (var coord in piece.BuildableMask)
                {
                    if (coord.X < 0 || coord.X >= cellWidth || coord.Y < 0 || coord.Y >= cellHeight)
                    {
                        badges.Add(new PieceValidationBadge(
                            PieceValidationSeverity.Error,
                            $"Buildable Mask coordinate {coord} is outside cell bounds (0..{cellWidth - 1}, 0..{cellHeight - 1}).",
                            id
                        ));
                        break; // Report once per piece to avoid flood
                    }
                }
            }

            // 5. Error: Boss piece with no Opening
            if (piece.RoomType == DungeonRoomType.Boss && piece.Openings == OpeningFlags.None)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    "Boss piece must have at least one opening for the boss doorway.",
                    id
                ));
            }

            // 6. Warning: Rotatable piece with directional art
            if (piece.IsRotatable && piece.HasDirectionalArt)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Warning,
                    "Piece is marked rotatable but contains directional art (may look abnormal when rotated).",
                    id
                ));
            }

            return badges;
        }

        /// <summary>
        /// Validates an entire collection of pieces belonging to a Piece Set.
        /// Checks uniqueness of piece IDs and aggregates per-piece validation badges.
        /// </summary>
        public static List<PieceValidationBadge> ValidatePieceSet(
            IEnumerable<DungeonPieceDescriptor> pieces,
            int cellWidth,
            int cellHeight)
        {
            var badges = new List<PieceValidationBadge>();
            if (pieces == null) return badges;

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var piece in pieces)
            {
                if (piece == null) continue;

                // Duplicate ID check
                if (!string.IsNullOrEmpty(piece.PieceId))
                {
                    if (!seenIds.Add(piece.PieceId))
                    {
                        badges.Add(new PieceValidationBadge(
                            PieceValidationSeverity.Error,
                            $"Duplicate piece ID '{piece.PieceId}' detected in Piece Set.",
                            piece.PieceId
                        ));
                    }
                }
                else
                {
                    badges.Add(new PieceValidationBadge(
                        PieceValidationSeverity.Error,
                        "Piece has an empty or null PieceId.",
                        "<Missing ID>"
                    ));
                }

                // Per-piece checks
                badges.AddRange(ValidatePiece(piece, cellWidth, cellHeight));
            }

            return badges;
        }

        /// <summary>
        /// Checks if a piece has any blocking validation errors (excluding warnings).
        /// </summary>
        public static bool HasErrors(IEnumerable<PieceValidationBadge> badges)
        {
            if (badges == null) return false;
            foreach (var b in badges)
            {
                if (b.IsError) return true;
            }
            return false;
        }
    }
}
