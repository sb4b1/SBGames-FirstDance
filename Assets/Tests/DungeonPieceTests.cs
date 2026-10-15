using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Dungeon.Core;
using SBGames.Dungeon.Core.Pieces;

namespace SBGames.Tests
{
    [TestFixture]
    public class DungeonPieceTests
    {
        private class MockRng : IDungeonRng
        {
            public int Range(int min, int max) => min;
            public float Range(float min, float max) => min;
            public bool Chance(float chance01) => true;
        }

        [Test]
        public void OpeningFlags_Rotation_MapsClockwiseCorrectly()
        {
            var north = OpeningFlags.North;

            Assert.AreEqual(OpeningFlags.East, north.Rotate(1), "North + 1 turn = East");
            Assert.AreEqual(OpeningFlags.South, north.Rotate(2), "North + 2 turns = South");
            Assert.AreEqual(OpeningFlags.West, north.Rotate(3), "North + 3 turns = West");
            Assert.AreEqual(OpeningFlags.North, north.Rotate(4), "North + 4 turns = North");

            var corner = OpeningFlags.North | OpeningFlags.East;
            Assert.AreEqual(OpeningFlags.East | OpeningFlags.South, corner.Rotate(1));
            Assert.AreEqual(OpeningFlags.South | OpeningFlags.West, corner.Rotate(2));
            Assert.AreEqual(OpeningFlags.West | OpeningFlags.North, corner.Rotate(3));
            Assert.AreEqual(corner, corner.Rotate(4));
        }

        [Test]
        public void DungeonPieceDescriptor_BuildableMaskRotation_MapsSquareGridClockwise()
        {
            int size = 10;
            var origin = new GridCoord(0, 0);

            var rot1 = DungeonPieceDescriptor.RotateLocalCoord(origin, 1, size);
            Assert.AreEqual(new GridCoord(0, 9), rot1, "(0,0) + 1 turn clockwise = (0, 9)");

            var rot2 = DungeonPieceDescriptor.RotateLocalCoord(origin, 2, size);
            Assert.AreEqual(new GridCoord(9, 9), rot2, "(0,0) + 2 turns = (9, 9)");

            var rot3 = DungeonPieceDescriptor.RotateLocalCoord(origin, 3, size);
            Assert.AreEqual(new GridCoord(9, 0), rot3, "(0,0) + 3 turns = (9, 0)");

            var rot4 = DungeonPieceDescriptor.RotateLocalCoord(origin, 4, size);
            Assert.AreEqual(origin, rot4, "(0,0) + 4 turns = (0, 0)");
        }

        [Test]
        public void PieceValidator_ValidationBadges_FlagsErrorsAndWarnings()
        {
            int cellW = 10;
            int cellH = 10;

            // 1. Error: No openings
            var noOpenings = new DungeonPieceDescriptor("p1", DungeonRoomType.Normal, OpeningFlags.None);
            var badges1 = PieceValidator.ValidatePiece(noOpenings, cellW, cellH);
            Assert.IsTrue(PieceValidator.HasErrors(badges1));

            // 2. Error: Footprint > 1x1
            var multiFootprint = new DungeonPieceDescriptor("p2", DungeonRoomType.Normal, OpeningFlags.North)
            {
                Footprint = new GridCoord(2, 1)
            };
            var badges2 = PieceValidator.ValidatePiece(multiFootprint, cellW, cellH);
            Assert.IsTrue(PieceValidator.HasErrors(badges2));

            // 3. Error: Rotatable on non-square cell
            var rotatableNonSquare = new DungeonPieceDescriptor("p3", DungeonRoomType.Normal, OpeningFlags.North, isRotatable: true);
            var badges3 = PieceValidator.ValidatePiece(rotatableNonSquare, 10, 8);
            Assert.IsTrue(PieceValidator.HasErrors(badges3));

            // 4. Error: Buildable mask out of bounds
            var oobMask = new DungeonPieceDescriptor("p4", DungeonRoomType.Normal, OpeningFlags.North);
            oobMask.BuildableMask.Add(new GridCoord(10, 5)); // max valid index is 9
            var badges4 = PieceValidator.ValidatePiece(oobMask, cellW, cellH);
            Assert.IsTrue(PieceValidator.HasErrors(badges4));

            // 5. Warning: Rotatable with directional art
            var directionalRotatable = new DungeonPieceDescriptor("p5", DungeonRoomType.Normal, OpeningFlags.North, isRotatable: true)
            {
                HasDirectionalArt = true
            };
            var badges5 = PieceValidator.ValidatePiece(directionalRotatable, cellW, cellH);
            Assert.IsFalse(PieceValidator.HasErrors(badges5), "Warning should not be treated as a blocking error.");
            Assert.AreEqual(1, badges5.Count);
            Assert.IsTrue(badges5[0].IsWarning);

            // 6. Error: Duplicate piece ID in set
            var pieceA = new DungeonPieceDescriptor("dup", DungeonRoomType.Normal, OpeningFlags.North);
            var pieceB = new DungeonPieceDescriptor("dup", DungeonRoomType.Normal, OpeningFlags.South);
            var setBadges = PieceValidator.ValidatePieceSet(new[] { pieceA, pieceB }, cellW, cellH);
            Assert.IsTrue(PieceValidator.HasErrors(setBadges));
        }

        [Test]
        public void DungeonPieceSelector_PrefersExactMatchOverSuperset()
        {
            var rng = new MockRng();
            var exactPiece = new DungeonPieceDescriptor("exact_deadend", DungeonRoomType.Normal, OpeningFlags.North);
            var fourWayPiece = new DungeonPieceDescriptor("four_way", DungeonRoomType.Normal, OpeningFlags.All);

            var candidates = new List<DungeonPieceDescriptor> { fourWayPiece, exactPiece };
            var usage = new Dictionary<string, int>();

            var result = DungeonPieceSelector.SelectPiece(
                DungeonRoomType.Normal,
                OpeningFlags.North,
                candidates,
                usage,
                rng
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsExactMatch);
            Assert.AreEqual("exact_deadend", result.SelectedPiece.PieceId);
            Assert.AreEqual(OpeningFlags.None, result.CappedSides);
        }

        [Test]
        public void DungeonPieceSelector_FallsBackToSupersetWithOpeningCaps()
        {
            var rng = new MockRng();
            // Only a four-way piece is available
            var fourWayPiece = new DungeonPieceDescriptor("four_way", DungeonRoomType.Normal, OpeningFlags.All);
            var candidates = new List<DungeonPieceDescriptor> { fourWayPiece };
            var usage = new Dictionary<string, int>();

            // Request a dead end (North only)
            var result = DungeonPieceSelector.SelectPiece(
                DungeonRoomType.Normal,
                OpeningFlags.North,
                candidates,
                usage,
                rng
            );

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.IsExactMatch, "Must be flagged as superset, not exact.");
            Assert.AreEqual("four_way", result.SelectedPiece.PieceId);
            Assert.AreEqual(OpeningFlags.East | OpeningFlags.South | OpeningFlags.West, result.CappedSides, "Unused East, South, West must be capped.");
        }

        [Test]
        public void DungeonPieceSelector_RotatablePieceMatchesViaRotation()
        {
            var rng = new MockRng();
            // Corner piece with North and East openings, marked rotatable
            var cornerPiece = new DungeonPieceDescriptor("corner_ne", DungeonRoomType.Normal, OpeningFlags.North | OpeningFlags.East, isRotatable: true);
            var candidates = new List<DungeonPieceDescriptor> { cornerPiece };
            var usage = new Dictionary<string, int>();

            // Request South and West doorway
            var result = DungeonPieceSelector.SelectPiece(
                DungeonRoomType.Normal,
                OpeningFlags.South | OpeningFlags.West,
                candidates,
                usage,
                rng
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsExactMatch);
            Assert.AreEqual(2, result.QuarterTurns, "North+East rotated 2 turns (180 deg) = South+West");
        }

        [Test]
        public void DungeonPieceSelector_HonorsMaxCountPerDungeon()
        {
            var rng = new MockRng();
            var piece = new DungeonPieceDescriptor("limited_piece", DungeonRoomType.Normal, OpeningFlags.North, maxCount: 1);
            var candidates = new List<DungeonPieceDescriptor> { piece };
            var usage = new Dictionary<string, int> { { "limited_piece", 1 } };

            var result = DungeonPieceSelector.SelectPiece(
                DungeonRoomType.Normal,
                OpeningFlags.North,
                candidates,
                usage,
                rng
            );

            Assert.IsFalse(result.Success, "Selection must fail when max count is reached.");
        }

        [Test]
        public void PieceSetCoverageCalculator_EvaluatesMatrixAccurately()
        {
            // A piece set with:
            // - Normal: 4-way rotatable piece
            // - Entrance: North dead end
            // - SafeRoom: None
            // - Boss: South dead end
            var pNormal = new DungeonPieceDescriptor("norm_4way", DungeonRoomType.Normal, OpeningFlags.All, isRotatable: true);
            var pEntrance = new DungeonPieceDescriptor("ent_n", DungeonRoomType.Entrance, OpeningFlags.North);
            var pBoss = new DungeonPieceDescriptor("boss_s", DungeonRoomType.Boss, OpeningFlags.South);

            var report = PieceSetCoverageCalculator.CalculateCoverage(
                new[] { pNormal, pEntrance, pBoss },
                cellWidth: 10,
                cellHeight: 10
            );

            Assert.AreEqual(60, report.Entries.Count, "Must evaluate 4 room types x 15 door subsets = 60 combinations.");
            Assert.IsTrue(report.HasErrors, "Should have errors because SafeRoom has no pieces.");
            Assert.IsTrue(report.HasWarnings, "Should have warnings because Normal 4-way requires caps for 1-way, 2-way, 3-way.");

            // Normal 4-way must be ExactMatch for All (15)
            var normalAll = report.Entries.Find(e => e.RoomType == DungeonRoomType.Normal && e.RequiredDoors == OpeningFlags.All);
            Assert.AreEqual(PieceCoverageStatus.ExactMatch, normalAll.Status);

            // Normal 1-way (North) must be SupersetWithCaps
            var normalNorth = report.Entries.Find(e => e.RoomType == DungeonRoomType.Normal && e.RequiredDoors == OpeningFlags.North);
            Assert.AreEqual(PieceCoverageStatus.SupersetWithCaps, normalNorth.Status);
            Assert.AreEqual(OpeningFlags.East | OpeningFlags.South | OpeningFlags.West, normalNorth.CappedSides);

            // SafeRoom must be None across all 15 door sets
            var safeRoomEntries = report.Entries.FindAll(e => e.RoomType == DungeonRoomType.SafeRoom);
            Assert.AreEqual(15, safeRoomEntries.Count);
            foreach (var entry in safeRoomEntries)
            {
                Assert.AreEqual(PieceCoverageStatus.None, entry.Status);
            }
        }

        [Test]
        public void DungeonBatchSimulator_20Seeds_CalculatesMetricsAndDetectsCoverageGaps()
        {
            var pEntrance = new DungeonPieceDescriptor("piece_entrance", DungeonRoomType.Entrance, OpeningFlags.All, isRotatable: false);
            var pNormal = new DungeonPieceDescriptor("piece_normal_4way", DungeonRoomType.Normal, OpeningFlags.All, isRotatable: true);
            var pSafe = new DungeonPieceDescriptor("piece_safe_4way", DungeonRoomType.SafeRoom, OpeningFlags.All, isRotatable: true);
            var pBoss = new DungeonPieceDescriptor("piece_boss_4way", DungeonRoomType.Boss, OpeningFlags.All, isRotatable: true);
            var pUnused = new DungeonPieceDescriptor("piece_unused_restricted", DungeonRoomType.Normal, OpeningFlags.None, isRotatable: false);

            var pieces = new List<DungeonPieceDescriptor> { pEntrance, pNormal, pSafe, pBoss, pUnused };

            var config = new DungeonBatchConfig(
                seedCount: 20,
                startSeed: 2000,
                maxRetriesPerSeed: 5
            );

            var report = DungeonBatchSimulator.RunSimulation(config, pieces);

            Assert.AreEqual(20, report.TotalSeeds);
            Assert.AreEqual(20, report.SuccessfulSeeds, "All 20 seeds should succeed because 4-way pieces cover all shapes.");
            Assert.AreEqual(100f, report.SuccessRate);
            Assert.GreaterOrEqual(report.AverageAttempts, 1.0f);
            Assert.AreEqual(20, report.SeedResults.Count);

            // Verify piece usage counts
            Assert.Greater(report.PieceSelectionCounts["piece_entrance"], 0);
            Assert.Greater(report.PieceSelectionCounts["piece_normal_4way"], 0);
            Assert.Greater(report.PieceSelectionCounts["piece_safe_4way"], 0);
            Assert.Greater(report.PieceSelectionCounts["piece_boss_4way"], 0);

            // Verify unused piece detection (coverage gap)
            Assert.Contains("piece_unused_restricted", report.NeverSelectedPieceIds, "Unused piece with 0 openings must be detected as a coverage gap.");
        }
    }
}
