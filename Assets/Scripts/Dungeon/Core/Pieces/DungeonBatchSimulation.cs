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
        public int OpeningWidth { get; set; } = 2;
        public DungeonGeneratorConfig GeneratorConfig { get; set; }

        public DungeonBatchConfig(
            int seedCount = 20,
            int startSeed = 1000,
            int maxRetriesPerSeed = 5,
            int openingWidth = 2,
            DungeonGeneratorConfig generatorConfig = null)
        {
            SeedCount = Math.Max(1, seedCount);
            StartSeed = startSeed;
            MaxRetriesPerSeed = Math.Max(1, maxRetriesPerSeed);
            OpeningWidth = Math.Max(1, openingWidth);
            GeneratorConfig = generatorConfig ?? new DungeonGeneratorConfig(
                minNormalRooms: 5,
                maxNormalRooms: 8,
                branchProbability: 0.35f,
                loopProbability: 0f,
                safeRoomCount: 1,
                roomWidth: 16,
                roomHeight: 16,
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
        public bool SeamCheckPassed { get; set; } = true;
        public List<string> SeamCheckFailures { get; set; } = new List<string>();
        public DungeonSeamLayoutResult SeamLayout { get; set; }
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

        // Seam Check & NavMesh verification
        public int SeamCheckPassedSeeds { get; set; }
        public int SeamCheckFailedSeeds { get; set; }
        public List<string> AllSeamFailures { get; set; } = new List<string>();
        public bool? NavMeshConnectivityPassed { get; set; }
        public string NavMeshConnectivityMessage { get; set; }
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
                    var seamLayout = DungeonSeamLayoutSolver.Solve(graph, pieces, config.GeneratorConfig.pathwayProbability, config.OpeningWidth, rng);

                    if (seamLayout != null && seamLayout.Success)
                    {
                        seedSucceeded = true;
                        seedResult.SeamLayout = seamLayout;
                        seedResult.RoomCount = seamLayout.PlacedRooms.Count + seamLayout.PlacedPathways.Count;
                        seedResult.CappedSidesCount = seamLayout.UnusedSockets.Count;
                        seedResult.TotalCappedSides = OpeningFlags.None;

                        var chosenPieces = new List<string>();
                        foreach (var r in seamLayout.PlacedRooms.Values)
                        {
                            chosenPieces.Add(r.Descriptor.PieceId);
                            if (report.PieceSelectionCounts.ContainsKey(r.Descriptor.PieceId))
                            {
                                report.PieceSelectionCounts[r.Descriptor.PieceId]++;
                            }
                        }
                        foreach (var p in seamLayout.PlacedPathways)
                        {
                            chosenPieces.Add(p.Descriptor.PieceId);
                            if (report.PieceSelectionCounts.ContainsKey(p.Descriptor.PieceId))
                            {
                                report.PieceSelectionCounts[p.Descriptor.PieceId]++;
                            }
                        }
                        seedResult.SelectedPieceIds = chosenPieces;

                        bool seedSeamPassed = true;
                        var seedSeamFailures = new List<string>();

                        for (int s = 0; s < seamLayout.Seams.Count; s++)
                        {
                            var seam = seamLayout.Seams[s];
                            if (seam.PieceA.RotatedGroundCells != null && seam.PieceA.RotatedGroundCells.Count > 0)
                            {
                                var edgesA = seam.OpeningA.GetEdgeTiles();
                                for (int e = 0; e < edgesA.Count; e++)
                                {
                                    if (!seam.PieceA.RotatedGroundCells.Contains(edgesA[e]))
                                    {
                                        seedSeamPassed = false;
                                        seedSeamFailures.Add($"Seed {usedSeed}: Piece {seam.PieceA.Descriptor.PieceId} missing Ground at seam edge {edgesA[e]} (dir {seam.OpeningA.Direction}) connecting to {seam.PieceB.Descriptor.PieceId}");
                                    }
                                }
                            }

                            if (seam.PieceB.RotatedGroundCells != null && seam.PieceB.RotatedGroundCells.Count > 0)
                            {
                                var edgesB = seam.OpeningB.GetEdgeTiles();
                                for (int e = 0; e < edgesB.Count; e++)
                                {
                                    if (!seam.PieceB.RotatedGroundCells.Contains(edgesB[e]))
                                    {
                                        seedSeamPassed = false;
                                        seedSeamFailures.Add($"Seed {usedSeed}: Piece {seam.PieceB.Descriptor.PieceId} missing Ground at seam edge {edgesB[e]} (dir {seam.OpeningB.Direction}) connecting to {seam.PieceA.Descriptor.PieceId}");
                                    }
                                }
                            }
                        }

                        seedResult.SeamCheckPassed = seedSeamPassed;
                        seedResult.SeamCheckFailures = seedSeamFailures;
                        break;
                    }

                    // Fallback to standalone piece selector if seam placement did not resolve
                    var usageCounts = new Dictionary<string, int>();
                    var fallbackChosenPieces = new List<string>();
                    var roomSelections = new Dictionary<int, DungeonPieceSelectionResult>();
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

                        roomSelections[node.RoomId] = selection;
                        string pieceId = selection.SelectedPiece.PieceId;
                        fallbackChosenPieces.Add(pieceId);
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
                        seedResult.SelectedPieceIds = fallbackChosenPieces;

                        // Increment global piece usage
                        for (int c = 0; c < fallbackChosenPieces.Count; c++)
                        {
                            string pid = fallbackChosenPieces[c];
                            if (report.PieceSelectionCounts.ContainsKey(pid))
                            {
                                report.PieceSelectionCounts[pid]++;
                            }
                        }

                        // Seam Check: for every adjacent placed pair, both pieces' Opening edge tiles contain Ground
                        bool seedSeamPassed = true;
                        var seedSeamFailures = new List<string>();

                        for (int d = 0; d < graph.Doors.Count; d++)
                        {
                            var door = graph.Doors[d];
                            if (!graph.Rooms.TryGetValue(door.RoomAId, out var nodeA) ||
                                !graph.Rooms.TryGetValue(door.RoomBId, out var nodeB))
                            {
                                continue;
                            }

                            if (roomSelections.TryGetValue(nodeA.RoomId, out var selA) &&
                                roomSelections.TryGetValue(nodeB.RoomId, out var selB))
                            {
                                var dirA = door.DirectionFromA;
                                var dirB = dirA.GetOpposite();
                                var pieceA = selA.SelectedPiece;
                                var pieceB = selB.SelectedPiece;

                                if (pieceA.GroundCells != null && pieceA.GroundCells.Count > 0)
                                {
                                    var rotGroundA = pieceA.GetRotatedGroundCells(selA.QuarterTurns, config.GeneratorConfig.roomWidth);
                                    var edgesA = OpeningGeometry.GetOpeningEdgeCoords(config.GeneratorConfig.roomWidth, config.GeneratorConfig.roomHeight, config.OpeningWidth, dirA);
                                    for (int e = 0; e < edgesA.Count; e++)
                                    {
                                        if (!rotGroundA.Contains(edgesA[e]))
                                        {
                                            seedSeamPassed = false;
                                            seedSeamFailures.Add($"Seed {usedSeed}: Room {nodeA.RoomId} ({pieceA.PieceId}) missing Ground at seam edge {edgesA[e]} (dir {dirA}) connecting to Room {nodeB.RoomId}");
                                        }
                                    }
                                }

                                if (pieceB.GroundCells != null && pieceB.GroundCells.Count > 0)
                                {
                                    var rotGroundB = pieceB.GetRotatedGroundCells(selB.QuarterTurns, config.GeneratorConfig.roomWidth);
                                    var edgesB = OpeningGeometry.GetOpeningEdgeCoords(config.GeneratorConfig.roomWidth, config.GeneratorConfig.roomHeight, config.OpeningWidth, dirB);
                                    for (int e = 0; e < edgesB.Count; e++)
                                    {
                                        if (!rotGroundB.Contains(edgesB[e]))
                                        {
                                            seedSeamPassed = false;
                                            seedSeamFailures.Add($"Seed {usedSeed}: Room {nodeB.RoomId} ({pieceB.PieceId}) missing Ground at seam edge {edgesB[e]} (dir {dirB}) connecting to Room {nodeA.RoomId}");
                                        }
                                    }
                                }
                            }
                        }

                        seedResult.SeamCheckPassed = seedSeamPassed;
                        seedResult.SeamCheckFailures = seedSeamFailures;
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
                    if (seedResult.SeamCheckPassed)
                    {
                        report.SeamCheckPassedSeeds++;
                    }
                    else
                    {
                        report.SeamCheckFailedSeeds++;
                        report.AllSeamFailures.AddRange(seedResult.SeamCheckFailures);
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
