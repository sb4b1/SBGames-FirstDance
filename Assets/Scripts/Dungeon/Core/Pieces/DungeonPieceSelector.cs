using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Pure C# outcome of a Dungeon Piece selection request.
    /// </summary>
    public readonly struct DungeonPieceSelectionResult
    {
        public readonly bool Success;
        public readonly DungeonPieceDescriptor SelectedPiece;
        public readonly int QuarterTurns;
        public readonly OpeningFlags CappedSides;
        public readonly bool IsExactMatch;
        public readonly string FailureReason;

        private DungeonPieceSelectionResult(
            bool success,
            DungeonPieceDescriptor piece,
            int quarterTurns,
            OpeningFlags cappedSides,
            bool isExactMatch,
            string failureReason)
        {
            Success = success;
            SelectedPiece = piece;
            QuarterTurns = quarterTurns;
            CappedSides = cappedSides;
            IsExactMatch = isExactMatch;
            FailureReason = failureReason ?? string.Empty;
        }

        public static DungeonPieceSelectionResult Exact(
            DungeonPieceDescriptor piece,
            int quarterTurns)
        {
            return new DungeonPieceSelectionResult(
                success: true,
                piece: piece,
                quarterTurns: quarterTurns,
                cappedSides: OpeningFlags.None,
                isExactMatch: true,
                failureReason: null
            );
        }

        public static DungeonPieceSelectionResult SupersetWithCaps(
            DungeonPieceDescriptor piece,
            int quarterTurns,
            OpeningFlags cappedSides)
        {
            return new DungeonPieceSelectionResult(
                success: true,
                piece: piece,
                quarterTurns: quarterTurns,
                cappedSides: cappedSides,
                isExactMatch: false,
                failureReason: null
            );
        }

        public static DungeonPieceSelectionResult Failure(string reason)
        {
            return new DungeonPieceSelectionResult(
                success: false,
                piece: null,
                quarterTurns: 0,
                cappedSides: OpeningFlags.None,
                isExactMatch: false,
                failureReason: reason
            );
        }

        public override string ToString()
        {
            if (Success)
            {
                return IsExactMatch
                    ? $"[Piece Selected] '{SelectedPiece.PieceId}' rot={QuarterTurns} (Exact Match)"
                    : $"[Piece Selected] '{SelectedPiece.PieceId}' rot={QuarterTurns} (Superset with caps on {CappedSides})";
            }
            return $"[Piece Selection Failed] {FailureReason}";
        }
    }

    /// <summary>
    /// Pure C# selection engine that matches a graph node's room type and doorway configuration
    /// to an authored Dungeon Piece and rotation, honoring weights, min/max limits, and exact-over-superset priority.
    /// Zero UnityEngine references.
    /// </summary>
    public static class DungeonPieceSelector
    {
        private class CandidateMatch
        {
            public readonly DungeonPieceDescriptor Piece;
            public readonly int QuarterTurns;
            public readonly OpeningFlags CappedSides;
            public readonly float Weight;

            public CandidateMatch(DungeonPieceDescriptor piece, int quarterTurns, OpeningFlags cappedSides, float weight)
            {
                Piece = piece;
                QuarterTurns = quarterTurns;
                CappedSides = cappedSides;
                Weight = Math.Max(0.001f, weight);
            }
        }

        /// <summary>
        /// Selects the best Dungeon Piece for a room node.
        /// </summary>
        public static DungeonPieceSelectionResult SelectPiece(
            DungeonRoomType roomType,
            OpeningFlags requiredDoors,
            IReadOnlyList<DungeonPieceDescriptor> pieces,
            IDictionary<string, int> usageCounts,
            IDungeonRng rng)
        {
            if (pieces == null || pieces.Count == 0)
            {
                return DungeonPieceSelectionResult.Failure("Piece set is null or empty.");
            }

            var exactMatches = new List<CandidateMatch>();
            var supersetMatches = new List<CandidateMatch>();

            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece == null) continue;
                if (piece.RoomType != roomType) continue;
                if (piece.Openings == OpeningFlags.None) continue;

                // Max count constraint check
                int currentUsage = 0;
                if (usageCounts != null && !string.IsNullOrEmpty(piece.PieceId))
                {
                    usageCounts.TryGetValue(piece.PieceId, out currentUsage);
                }

                if (piece.MaxCountPerDungeon > 0 && currentUsage >= piece.MaxCountPerDungeon)
                {
                    continue;
                }

                // Min count deficit priority weighting: if under min count, boost weight
                float effectiveWeight = piece.Weight;
                if (piece.MinCountPerDungeon > 0 && currentUsage < piece.MinCountPerDungeon)
                {
                    effectiveWeight *= 2.0f;
                }

                int maxRot = piece.IsRotatable ? 4 : 1;
                for (int rot = 0; rot < maxRot; rot++)
                {
                    var rotatedOpenings = piece.GetRotatedOpenings(rot);

                    if (rotatedOpenings == requiredDoors)
                    {
                        exactMatches.Add(new CandidateMatch(piece, rot, OpeningFlags.None, effectiveWeight));
                    }
                    else if ((rotatedOpenings & requiredDoors) == requiredDoors)
                    {
                        var cappedSides = rotatedOpenings ^ requiredDoors;
                        // Slight penalty for pieces that require many caps to favor tighter fits
                        float capPenalty = 1f / (1f + cappedSides.CountOpenings());
                        supersetMatches.Add(new CandidateMatch(piece, rot, cappedSides, effectiveWeight * capPenalty));
                    }
                }
            }

            // 1. Prefer Exact matches over all rotations
            if (exactMatches.Count > 0)
            {
                var chosen = PickWeighted(exactMatches, rng);
                return DungeonPieceSelectionResult.Exact(chosen.Piece, chosen.QuarterTurns);
            }

            // 2. Fallback to Superset with Opening Caps
            if (supersetMatches.Count > 0)
            {
                var chosen = PickWeighted(supersetMatches, rng);
                return DungeonPieceSelectionResult.SupersetWithCaps(chosen.Piece, chosen.QuarterTurns, chosen.CappedSides);
            }

            return DungeonPieceSelectionResult.Failure(
                $"No piece in Piece Set satisfies room type '{roomType}' with doorway openings '{requiredDoors}'."
            );
        }

        private static CandidateMatch PickWeighted(List<CandidateMatch> candidates, IDungeonRng rng)
        {
            if (candidates.Count == 1 || rng == null)
            {
                return candidates[0];
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            if (totalWeight <= 0f)
            {
                return candidates[0];
            }

            float roll = rng.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].Weight;
                if (roll < cumulative)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }
    }
}
