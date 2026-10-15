using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Configuration for running a deterministic batch dungeon generation & piece selection simulation.
    /// Pure C#, zero UnityEngine references.
    /// </summary>
    public class DungeonBatchConfig
    {
        public int SeedCount { get; set; } = 20;
        public int StartSeed { get; set; } = 1000;
        public int MaxRetriesPerSeed { get; set; } = 5;
        public DungeonGeneratorConfig GeneratorConfig { get; set; }

        public DungeonBatchConfig(
            int seedCount = 20,
            int startSeed = 1000,
            int maxRetriesPerSeed = 5,
            DungeonGeneratorConfig generatorConfig = null)
        {
            SeedCount = Math.Max(1, seedCount);
            StartSeed = startSeed;
            MaxRetriesPerSeed = Math.Max(1, maxRetriesPerSeed);
            GeneratorConfig = generatorConfig ?? new DungeonGeneratorConfig(
                minNormalRooms: 5,
                maxNormalRooms: 8,
                branchProbability: 0.35f,
                loopProbability: 0f,
                safeRoomCount: 1,
                roomWidth: 10,
                roomHeight: 10,
                corridorLength: 0,
                corridorWidth: 2
            );
        }
    }

    /// <summary>
    /// Individual outcome for a single seed during batch simulation.
    /// </summary>
    public class DungeonBatchSeedResult
    {
        public int InitialSeed { get; set; }
        public int UsedSeed { get; set; }
        public int Attempts { get; set; }
        public bool Success { get; set; }
        public int RoomCount { get; set; }
        public int CappedSidesCount { get; set; }
        public OpeningFlags TotalCappedSides { get; set; }
        public List<string> SelectedPieceIds { get; set; } = new List<string>();
        public string FailureMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Aggregated statistical report across N simulated seeds.
    /// </summary>
    public class DungeonBatchReport
    {
        public int TotalSeeds { get; set; }
        public int SuccessfulSeeds { get; set; }
        public int FailedSeeds { get; set; }
        public int MaxRetriesPerSeed { get; set; }
        public float SuccessRate => TotalSeeds > 0 ? (SuccessfulSeeds * 100f / TotalSeeds) : 0f;
        public float AverageAttempts { get; set; }
        public List<int> SeedsRequiringCaps { get; set; } = new List<int>();
        public Dictionary<string, int> PieceSelectionCounts { get; set; } = new Dictionary<string, int>();
        public List<string> NeverSelectedPieceIds { get; set; } = new List<string>();
        public List<DungeonBatchSeedResult> SeedResults { get; set; } = new List<DungeonBatchSeedResult>();
    }

    /// <summary>
    /// Pure C# batch test and analysis engine for Dungeon Piece selection over multiple seeds.
    /// Evaluates success rates, average retries, seeds needing opening caps, and unused piece coverage gaps.
    /// </summary>
    public static class DungeonBatchSimulator
    {
        public static DungeonBatchReport RunSimulation(
            DungeonBatchConfig config,
            IReadOnlyList<DungeonPieceDescriptor> pieces)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            pieces = pieces ?? Array.Empty<DungeonPieceDescriptor>();

            var report = new DungeonBatchReport
            {
                TotalSeeds = config.SeedCount,
                MaxRetriesPerSeed = config.MaxRetriesPerSeed
            };

            // Initialize piece selection counters
            for (int p = 0; p < pieces.Count; p++)
            {
                string id = pieces[p].PieceId;
                if (!string.IsNullOrEmpty(id) && !report.PieceSelectionCounts.ContainsKey(id))
                {
                    report.PieceSelectionCounts[id] = 0;
                }
            }

            var generator = new DungeonLayoutGenerator();
            int totalAttemptsAcrossAll = 0;

            for (int i = 0; i < config.SeedCount; i++)
            {
                int initialSeed = config.StartSeed + i;
                int usedSeed = initialSeed;
                bool seedSucceeded = false;
                int attemptsNeeded = 0;

                var seedResult = new DungeonBatchSeedResult
                {
                    InitialSeed = initialSeed
                };

                for (int attempt = 0; attempt < config.MaxRetriesPerSeed; attempt++)
                {
                    attemptsNeeded++;
                    usedSeed = (attempt == 0) ? initialSeed : unchecked(initialSeed * 397 ^ (attempt * 10007 + 54321));
                    var graph = generator.Generate(usedSeed, config.GeneratorConfig);

                    if (pieces.Count == 0)
                    {
                        // No pieces to match: raw layout generation succeeds
                        seedSucceeded = true;
                        seedResult.RoomCount = graph.Rooms.Count;
                        break;
                    }

                    var rng = new GameRngDungeonAdapter((uint)Math.Abs(usedSeed + 101));
                    var usageCounts = new Dictionary<string, int>();
                    var chosenPieces = new List<string>();
                    OpeningFlags cumulativeCaps = OpeningFlags.None;
                    int capsCount = 0;
                    bool allRoomsMatched = true;

                    foreach (var node in graph.Rooms.Values)
                    {
                        var requiredDoors = OpeningFlagsExtensions.FromDirections(node.DoorsByDirection.Keys);
                        var selection = DungeonPieceSelector.SelectPiece(node.RoomType, requiredDoors, pieces, usageCounts, rng);

                        if (!selection.Success)
                        {
                            allRoomsMatched = false;
                            seedResult.FailureMessage = selection.FailureReason;
                            break;
                        }

                        string pieceId = selection.SelectedPiece.PieceId;
                        chosenPieces.Add(pieceId);
                        usageCounts[pieceId] = usageCounts.TryGetValue(pieceId, out int cnt) ? cnt + 1 : 1;

                        if (selection.CappedSides != OpeningFlags.None)
                        {
                            cumulativeCaps |= selection.CappedSides;
                            capsCount += selection.CappedSides.CountOpenings();
                        }
                    }

                    if (allRoomsMatched)
                    {
                        seedSucceeded = true;
                        seedResult.RoomCount = graph.Rooms.Count;
                        seedResult.CappedSidesCount = capsCount;
                        seedResult.TotalCappedSides = cumulativeCaps;
                        seedResult.SelectedPieceIds = chosenPieces;

                        // Increment global piece usage
                        for (int c = 0; c < chosenPieces.Count; c++)
                        {
                            string pid = chosenPieces[c];
                            if (report.PieceSelectionCounts.ContainsKey(pid))
                            {
                                report.PieceSelectionCounts[pid]++;
                            }
                        }
                        break;
                    }
                }

                seedResult.UsedSeed = usedSeed;
                seedResult.Attempts = attemptsNeeded;
                seedResult.Success = seedSucceeded;
                totalAttemptsAcrossAll += attemptsNeeded;

                if (seedSucceeded)
                {
                    report.SuccessfulSeeds++;
                    if (seedResult.CappedSidesCount > 0)
                    {
                        report.SeedsRequiringCaps.Add(usedSeed);
                    }
                }
                else
                {
                    report.FailedSeeds++;
                }

                report.SeedResults.Add(seedResult);
            }

            report.AverageAttempts = config.SeedCount > 0 ? (float)totalAttemptsAcrossAll / config.SeedCount : 0f;

            // Find never-selected pieces (coverage gaps)
            foreach (var kvp in report.PieceSelectionCounts)
            {
                if (kvp.Value == 0)
                {
                    report.NeverSelectedPieceIds.Add(kvp.Key);
                }
            }

            return report;
        }
    }
}
