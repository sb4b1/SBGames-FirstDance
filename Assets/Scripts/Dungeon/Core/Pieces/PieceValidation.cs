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
        /// Validates a single DungeonPieceDescriptor against cell dimensions and opening geometry.
        /// </summary>
        public static List<PieceValidationBadge> ValidatePiece(
            DungeonPieceDescriptor piece,
            int cellWidth,
            int cellHeight,
            int openingWidth = 2,
            HashSet<GridCoord> groundCells = null)
        {
            var badges = new List<PieceValidationBadge>();
            if (piece == null)
            {
                badges.Add(new PieceValidationBadge(PieceValidationSeverity.Error, "Piece descriptor is null."));
                return badges;
            }

            string id = piece.PieceId ?? "<Unnamed>";

            int effCellW = piece.CellWidth > 0 ? piece.CellWidth : cellWidth;
            int effCellH = piece.CellHeight > 0 ? piece.CellHeight : cellHeight;

            bool hasFreeformOpenings = piece.OpeningsList != null && piece.OpeningsList.Count > 0;

            // 1. Error: Opening width parity cannot center symmetrically on cell dimensions (legacy centered mode only)
            if (!hasFreeformOpenings && !OpeningGeometry.CanCenterOpening(effCellW, effCellH, openingWidth))
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    $"Opening width ({openingWidth}) parity cannot center symmetrically on cell dimensions ({effCellW}x{effCellH}).",
                    id
                ));
            }

            // 2. Error: No Openings
            bool hasOpenings = hasFreeformOpenings || piece.Openings != OpeningFlags.None;
            if (!hasOpenings)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    "Piece has no openings configured (must have at least 1 cardinal opening).",
                    id
                ));
            }

            // 3. Error: Footprint larger than 1x1 in v1
            if (piece.Footprint.X != 1 || piece.Footprint.Y != 1)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    $"Footprint {piece.Footprint} is larger than 1x1 (multi-cell pieces are unsupported in v1).",
                    id
                ));
            }

            // 4. Error: Buildable Mask outside cell boundaries
            if (piece.BuildableMask != null)
            {
                foreach (var coord in piece.BuildableMask)
                {
                    if (coord.X < 0 || coord.X >= effCellW || coord.Y < 0 || coord.Y >= effCellH)
                    {
                        badges.Add(new PieceValidationBadge(
                            PieceValidationSeverity.Error,
                            $"Buildable Mask coordinate {coord} is outside cell bounds (0..{effCellW - 1}, 0..{effCellH - 1}).",
                            id
                        ));
                        break; // Report once per piece to avoid flood
                    }
                }
            }

            // 5. Error: Boss piece with no Opening
            if (piece.RoomType == DungeonRoomType.Boss && !hasOpenings)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    "Boss piece must have at least one opening for the boss doorway.",
                    id
                ));
            }

            // 6. Error: Entrance piece must have at least one entry anchor
            if (piece.RoomType == DungeonRoomType.Entrance && piece.EntryAnchorCount < 1)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Error,
                    "Entrance piece must have at least one entry anchor for adventurer spawning.",
                    id
                ));
            }

            // 7. Warning: Rotatable piece with directional art
            if (piece.IsRotatable && piece.HasDirectionalArt)
            {
                badges.Add(new PieceValidationBadge(
                    PieceValidationSeverity.Warning,
                    "Piece is marked rotatable but contains directional art (may look abnormal when rotated).",
                    id
                ));
            }

            // 8. Freeform Openings Validation
            if (hasFreeformOpenings)
            {
                // Error: Overlapping openings
                for (int i = 0; i < piece.OpeningsList.Count; i++)
                {
                    for (int j = i + 1; j < piece.OpeningsList.Count; j++)
                    {
                        if (piece.OpeningsList[i].Overlaps(piece.OpeningsList[j]))
                        {
                            badges.Add(new PieceValidationBadge(
                                PieceValidationSeverity.Error,
                                $"Openings at index {i} ({piece.OpeningsList[i]}) and {j} ({piece.OpeningsList[j]}) overlap.",
                                id
                            ));
                        }
                    }
                }

                if (groundCells != null)
                {
                    var allOpeningEdgeTiles = new HashSet<GridCoord>();
                    foreach (var opening in piece.OpeningsList)
                    {
                        var edgeTiles = opening.GetEdgeTiles();
                        bool missingGround = false;
                        for (int t = 0; t < edgeTiles.Count; t++)
                        {
                            allOpeningEdgeTiles.Add(edgeTiles[t]);
                            if (!groundCells.Contains(edgeTiles[t]))
                            {
                                missingGround = true;
                            }
                        }

                        if (missingGround)
                        {
                            badges.Add(new PieceValidationBadge(
                                PieceValidationSeverity.Error,
                                $"Declared opening {opening} does not have painted Ground on all {opening.Width} tiles.",
                                id
                            ));
                        }
                    }

                    // Warning if Ground reaches the outer border of the piece where no PieceOpening is declared
                    bool outerBorderUndeclaredGround = false;
                    GridCoord firstUndeclaredCoord = default;

                    for (int x = 0; x < effCellW && !outerBorderUndeclaredGround; x++)
                    {
                        var coordN = new GridCoord(x, effCellH - 1);
                        if (groundCells.Contains(coordN) && !allOpeningEdgeTiles.Contains(coordN))
                        {
                            outerBorderUndeclaredGround = true;
                            firstUndeclaredCoord = coordN;
                            break;
                        }
                        var coordS = new GridCoord(x, 0);
                        if (groundCells.Contains(coordS) && !allOpeningEdgeTiles.Contains(coordS))
                        {
                            outerBorderUndeclaredGround = true;
                            firstUndeclaredCoord = coordS;
                            break;
                        }
                    }

                    for (int y = 0; y < effCellH && !outerBorderUndeclaredGround; y++)
                    {
                        var coordW = new GridCoord(0, y);
                        if (groundCells.Contains(coordW) && !allOpeningEdgeTiles.Contains(coordW))
                        {
                            outerBorderUndeclaredGround = true;
                            firstUndeclaredCoord = coordW;
                            break;
                        }
                        var coordE = new GridCoord(effCellW - 1, y);
                        if (groundCells.Contains(coordE) && !allOpeningEdgeTiles.Contains(coordE))
                        {
                            outerBorderUndeclaredGround = true;
                            firstUndeclaredCoord = coordE;
                            break;
                        }
                    }

                    if (outerBorderUndeclaredGround)
                    {
                        badges.Add(new PieceValidationBadge(
                            PieceValidationSeverity.Warning,
                            $"Ground reaches the outer border of the piece (e.g. at {firstUndeclaredCoord}) where no PieceOpening is declared.",
                            id
                        ));
                    }
                }
            }
            // 9. Legacy Centered Opening Ground tile validation
            else if (groundCells != null && OpeningGeometry.CanCenterOpening(effCellW, effCellH, openingWidth))
            {
                for (int d = 0; d < 4; d++)
                {
                    var dir = (CardinalDirection)d;
                    var edgeCoords = OpeningGeometry.GetOpeningEdgeCoords(effCellW, effCellH, openingWidth, dir);
                    bool isDeclared = piece.Openings.ContainsDirection(dir);

                    if (isDeclared)
                    {
                        // Error if a declared Opening has no Ground at its edge tiles
                        bool hasMissingGround = false;
                        for (int i = 0; i < edgeCoords.Count; i++)
                        {
                            if (!groundCells.Contains(edgeCoords[i]))
                            {
                                hasMissingGround = true;
                                break;
                            }
                        }

                        if (hasMissingGround)
                        {
                            badges.Add(new PieceValidationBadge(
                                PieceValidationSeverity.Error,
                                $"Declared {dir} Opening has no Ground at its edge tiles.",
                                id
                            ));
                        }
                    }
                    else
                    {
                        // Warning if Ground reaches an undeclared edge's Opening tiles
                        bool reachesUndeclared = false;
                        for (int i = 0; i < edgeCoords.Count; i++)
                        {
                            if (groundCells.Contains(edgeCoords[i]))
                            {
                                reachesUndeclared = true;
                                break;
                            }
                        }

                        if (reachesUndeclared)
                        {
                            badges.Add(new PieceValidationBadge(
                                PieceValidationSeverity.Warning,
                                $"Ground reaches undeclared {dir} edge Opening tiles.",
                                id
                            ));
                        }
                    }
                }
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
            int cellHeight,
            int openingWidth = 2,
            Dictionary<string, HashSet<GridCoord>> groundCellsByPieceId = null)
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
                HashSet<GridCoord> pieceGround = null;
                if (groundCellsByPieceId != null && !string.IsNullOrEmpty(piece.PieceId))
                {
                    groundCellsByPieceId.TryGetValue(piece.PieceId, out pieceGround);
                }

                badges.AddRange(ValidatePiece(piece, cellWidth, cellHeight, openingWidth, pieceGround));
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
