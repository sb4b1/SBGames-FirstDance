using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    public enum PieceCoverageStatus
    {
        ExactMatch = 0,
        SupersetWithCaps = 1,
        None = 2
    }

    /// <summary>
    /// Pure C# entry in the coverage matrix for a specific (RoomType, RequiredDoors) combination.
    /// </summary>
    public readonly struct PieceCoverageEntry
    {
        public readonly DungeonRoomType RoomType;
        public readonly OpeningFlags RequiredDoors;
        public readonly PieceCoverageStatus Status;
        public readonly string MatchingPieceId;
        public readonly int MatchingQuarterTurns;
        public readonly OpeningFlags CappedSides;

        public bool IsExact => Status == PieceCoverageStatus.ExactMatch;
        public bool IsCapped => Status == PieceCoverageStatus.SupersetWithCaps;
        public bool IsMissing => Status == PieceCoverageStatus.None;

        public PieceCoverageEntry(
            DungeonRoomType roomType,
            OpeningFlags requiredDoors,
            PieceCoverageStatus status,
            string matchingPieceId = null,
            int matchingQuarterTurns = 0,
            OpeningFlags cappedSides = OpeningFlags.None)
        {
            RoomType = roomType;
            RequiredDoors = requiredDoors;
            Status = status;
            MatchingPieceId = matchingPieceId ?? string.Empty;
            MatchingQuarterTurns = matchingQuarterTurns;
            CappedSides = cappedSides;
        }

        public override string ToString()
        {
            if (IsExact) return $"[{RoomType}] {RequiredDoors}: Exact ({MatchingPieceId}, rot={MatchingQuarterTurns})";
            if (IsCapped) return $"[{RoomType}] {RequiredDoors}: Superset with caps on {CappedSides} ({MatchingPieceId}, rot={MatchingQuarterTurns})";
            return $"[{RoomType}] {RequiredDoors}: NONE (Missing piece!)";
        }
    }

    /// <summary>
    /// Aggregated coverage report for a Piece Set across all 4 room types and 15 door subsets.
    /// </summary>
    public class PieceSetCoverageReport
    {
        public List<PieceCoverageEntry> Entries { get; } = new List<PieceCoverageEntry>();

        public int ExactMatchCount { get; set; }
        public int SupersetWithCapsCount { get; set; }
        public int NoneCount { get; set; }

        public bool HasErrors => NoneCount > 0;
        public bool HasWarnings => SupersetWithCapsCount > 0;

        public float ExactCoverageRatio => Entries.Count > 0 ? (float)ExactMatchCount / Entries.Count : 0f;
        public float TotalUsableRatio => Entries.Count > 0 ? (float)(ExactMatchCount + SupersetWithCapsCount) / Entries.Count : 0f;
    }

    /// <summary>
    /// Pure C# calculator that evaluates a Piece Set against all 15 door-set permutations
    /// for Entrance, Normal, SafeRoom, and Boss room types.
    /// </summary>
    public static class PieceSetCoverageCalculator
    {
        public static readonly DungeonRoomType[] RoomTypes = new[]
        {
            DungeonRoomType.Entrance,
            DungeonRoomType.Normal,
            DungeonRoomType.SafeRoom,
            DungeonRoomType.Boss
        };

        public static PieceSetCoverageReport CalculateCoverage(
            IEnumerable<DungeonPieceDescriptor> pieces,
            int cellWidth,
            int cellHeight)
        {
            var report = new PieceSetCoverageReport();
            var pieceList = new List<DungeonPieceDescriptor>();

            if (pieces != null)
            {
                foreach (var p in pieces)
                {
                    if (p != null)
                    {
                        var badges = PieceValidator.ValidatePiece(p, cellWidth, cellHeight);
                        if (!PieceValidator.HasErrors(badges))
                        {
                            pieceList.Add(p);
                        }
                    }
                }
            }

            // Loop 4 room types
            for (int t = 0; t < RoomTypes.Length; t++)
            {
                var rType = RoomTypes[t];

                // Loop 15 non-empty opening flags (1..15)
                for (int maskVal = 1; maskVal <= 15; maskVal++)
                {
                    var requiredDoors = (OpeningFlags)maskVal;
                    var entry = EvaluateCombination(rType, requiredDoors, pieceList);
                    report.Entries.Add(entry);

                    if (entry.IsExact) report.ExactMatchCount++;
                    else if (entry.IsCapped) report.SupersetWithCapsCount++;
                    else report.NoneCount++;
                }
            }

            return report;
        }

        private static PieceCoverageEntry EvaluateCombination(
            DungeonRoomType roomType,
            OpeningFlags requiredDoors,
            List<DungeonPieceDescriptor> pieces)
        {
            DungeonPieceDescriptor bestSupersetPiece = null;
            int bestSupersetRot = 0;
            OpeningFlags bestSupersetCaps = OpeningFlags.None;
            int minCappedCount = int.MaxValue;

            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.RoomType != roomType) continue;

                int maxRot = piece.IsRotatable ? 4 : 1;
                for (int rot = 0; rot < maxRot; rot++)
                {
                    var rotatedOpenings = piece.GetRotatedOpenings(rot);

                    // 1. Exact match test
                    if (rotatedOpenings == requiredDoors)
                    {
                        return new PieceCoverageEntry(
                            roomType,
                            requiredDoors,
                            PieceCoverageStatus.ExactMatch,
                            piece.PieceId,
                            rot,
                            OpeningFlags.None
                        );
                    }

                    // 2. Superset test
                    if ((rotatedOpenings & requiredDoors) == requiredDoors)
                    {
                        var cappedSides = rotatedOpenings ^ requiredDoors;
                        int capCount = cappedSides.CountOpenings();
                        if (capCount < minCappedCount)
                        {
                            minCappedCount = capCount;
                            bestSupersetPiece = piece;
                            bestSupersetRot = rot;
                            bestSupersetCaps = cappedSides;
                        }
                    }
                }
            }

            if (bestSupersetPiece != null)
            {
                return new PieceCoverageEntry(
                    roomType,
                    requiredDoors,
                    PieceCoverageStatus.SupersetWithCaps,
                    bestSupersetPiece.PieceId,
                    bestSupersetRot,
                    bestSupersetCaps
                );
            }

            return new PieceCoverageEntry(
                roomType,
                requiredDoors,
                PieceCoverageStatus.None
            );
        }
    }
}
