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

            // 3. Non-square rotatable pieces are now valid (dimensions swap on odd turns)
            var rotatableNonSquare = new DungeonPieceDescriptor("p3", DungeonRoomType.Normal, OpeningFlags.North, isRotatable: true);
            var badges3 = PieceValidator.ValidatePiece(rotatableNonSquare, 10, 8);
            Assert.IsFalse(PieceValidator.HasErrors(badges3), "Non-square rotatable pieces are valid under dynamic piece sizing.");

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

        [Test]
        public void OpeningGeometry_CenteredCoordinates_Matches16x16EdgeTiles()
        {
            int cellW = 16;
            int cellH = 16;
            int openingW = 2;

            // North: tiles 7-8 along y=15
            var north = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.North);
            Assert.AreEqual(2, north.Count);
            Assert.AreEqual(new GridCoord(7, 15), north[0]);
            Assert.AreEqual(new GridCoord(8, 15), north[1]);

            // South: tiles 7-8 along y=0
            var south = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.South);
            Assert.AreEqual(2, south.Count);
            Assert.AreEqual(new GridCoord(7, 0), south[0]);
            Assert.AreEqual(new GridCoord(8, 0), south[1]);

            // West: tiles 7-8 along x=0
            var west = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.West);
            Assert.AreEqual(2, west.Count);
            Assert.AreEqual(new GridCoord(0, 7), west[0]);
            Assert.AreEqual(new GridCoord(0, 8), west[1]);

            // East: tiles 7-8 along x=15
            var east = OpeningGeometry.GetOpeningEdgeCoords(cellW, cellH, openingW, CardinalDirection.East);
            Assert.AreEqual(2, east.Count);
            Assert.AreEqual(new GridCoord(15, 7), east[0]);
            Assert.AreEqual(new GridCoord(15, 8), east[1]);
        }

        [Test]
        public void OpeningGeometry_Rotation_IsConsistentWithOpeningFlags()
        {
            int size = 16;
            int openingW = 2;
            var northCoords = OpeningGeometry.GetOpeningEdgeCoords(size, size, openingW, CardinalDirection.North);

            // 1 turn clockwise: North -> East
            var rotated1 = OpeningGeometry.RotateCoords(northCoords, 1, size);
            var eastCoords = OpeningGeometry.GetOpeningEdgeCoords(size, size, openingW, CardinalDirection.East);
            CollectionAssert.AreEquivalent(eastCoords, rotated1);

            // 2 turns clockwise: North -> South
            var rotated2 = OpeningGeometry.RotateCoords(northCoords, 2, size);
            var southCoords = OpeningGeometry.GetOpeningEdgeCoords(size, size, openingW, CardinalDirection.South);
            CollectionAssert.AreEquivalent(southCoords, rotated2);

            // 3 turns clockwise: North -> West
            var rotated3 = OpeningGeometry.RotateCoords(northCoords, 3, size);
            var westCoords = OpeningGeometry.GetOpeningEdgeCoords(size, size, openingW, CardinalDirection.West);
            CollectionAssert.AreEquivalent(westCoords, rotated3);

            // 4 turns: back to North
            var rotated4 = OpeningGeometry.RotateCoords(northCoords, 4, size);
            CollectionAssert.AreEquivalent(northCoords, rotated4);

            // Direction rotation helper
            Assert.AreEqual(CardinalDirection.East, OpeningGeometry.RotateDirection(CardinalDirection.North, 1));
            Assert.AreEqual(CardinalDirection.South, OpeningGeometry.RotateDirection(CardinalDirection.North, 2));
            Assert.AreEqual(CardinalDirection.West, OpeningGeometry.RotateDirection(CardinalDirection.North, 3));
        }

        [Test]
        public void PieceValidator_OpeningParityAndGroundPresence_BadgesAccurately()
        {
            int cellW = 16;
            int cellH = 16;
            var pNormal = new DungeonPieceDescriptor("p_test", DungeonRoomType.Normal, OpeningFlags.North);

            // 1. Error: Parity mismatch (openingWidth 3 on 16x16 cell)
            var parityBadges = PieceValidator.ValidatePiece(pNormal, cellW, cellH, openingWidth: 3);
            Assert.IsTrue(PieceValidator.HasErrors(parityBadges));
            Assert.IsTrue(parityBadges.Exists(b => b.Message.Contains("parity")));

            // 2. Error: Declared North opening missing Ground
            var emptyGround = new HashSet<GridCoord>();
            var missingGroundBadges = PieceValidator.ValidatePiece(pNormal, cellW, cellH, openingWidth: 2, emptyGround);
            Assert.IsTrue(PieceValidator.HasErrors(missingGroundBadges));
            Assert.IsTrue(missingGroundBadges.Exists(b => b.Message.Contains("Declared North Opening has no Ground")));

            // 3. Valid: North opening has Ground
            var validGround = new HashSet<GridCoord>
            {
                new GridCoord(7, 15),
                new GridCoord(8, 15)
            };
            var validBadges = PieceValidator.ValidatePiece(pNormal, cellW, cellH, openingWidth: 2, validGround);
            Assert.IsFalse(PieceValidator.HasErrors(validBadges));

            // 4. Warning: Ground reaches undeclared East edge tiles
            validGround.Add(new GridCoord(15, 7)); // Undeclared East edge tile
            var undeclaredBadges = PieceValidator.ValidatePiece(pNormal, cellW, cellH, openingWidth: 2, validGround);
            Assert.IsFalse(PieceValidator.HasErrors(undeclaredBadges));
            Assert.IsTrue(undeclaredBadges.Exists(b => b.IsWarning && b.Message.Contains("Ground reaches undeclared East")));

            // 5. Error: Entrance piece missing entry anchor
            var pEntrance = new DungeonPieceDescriptor("p_ent", DungeonRoomType.Entrance, OpeningFlags.North)
            {
                EntryAnchorCount = 0
            };
            var entranceBadges = PieceValidator.ValidatePiece(pEntrance, cellW, cellH);
            Assert.IsTrue(PieceValidator.HasErrors(entranceBadges));
            Assert.IsTrue(entranceBadges.Exists(b => b.Message.Contains("entry anchor")));

            pEntrance.EntryAnchorCount = 1;
            var entranceValidBadges = PieceValidator.ValidatePiece(pEntrance, cellW, cellH);
            Assert.IsFalse(PieceValidator.HasErrors(entranceValidBadges));
        }

        [Test]
        public void DungeonPieceDescriptor_GetRotatedGroundCells_TransformsCoordinatesClockwise()
        {
            var desc = new DungeonPieceDescriptor("p1", DungeonRoomType.Normal, OpeningFlags.North, isRotatable: true)
            {
                GroundCells = new HashSet<GridCoord>
                {
                    new GridCoord(7, 15),
                    new GridCoord(8, 15)
                }
            };

            // 1 turn clockwise: North edge (y=15) rotates to East edge (x=15)
            // (7, 15) -> (y, 15-x) = (15, 8)
            // (8, 15) -> (y, 15-x) = (15, 7)
            var rot1 = desc.GetRotatedGroundCells(1, 16);
            Assert.IsTrue(rot1.Contains(new GridCoord(15, 7)));
            Assert.IsTrue(rot1.Contains(new GridCoord(15, 8)));
        }

        [Test]
        public void DungeonBatchSimulator_SeamCheck_PassesWhenEdgeTilesHaveGround()
        {
            // Build a piece set with all required topologies and ground filled
            var pieces = new List<DungeonPieceDescriptor>();
            var allGround = new HashSet<GridCoord>();
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    allGround.Add(new GridCoord(x, y));
                }
            }

            // Normal piece with all 4 openings
            pieces.Add(new DungeonPieceDescriptor("cross", DungeonRoomType.Normal, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = allGround
            });
            // Entrance with all 4 openings
            pieces.Add(new DungeonPieceDescriptor("ent", DungeonRoomType.Entrance, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = allGround,
                EntryAnchorCount = 1
            });
            // Boss with all 4 openings
            pieces.Add(new DungeonPieceDescriptor("boss", DungeonRoomType.Boss, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = allGround
            });
            // SafeRoom with all 4 openings
            pieces.Add(new DungeonPieceDescriptor("safe", DungeonRoomType.SafeRoom, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = allGround
            });

            var genConfig = new DungeonGeneratorConfig(
                minNormalRooms: 5,
                maxNormalRooms: 6,
                branchProbability: 0.35f,
                loopProbability: 0f,
                safeRoomCount: 1,
                roomWidth: 16,
                roomHeight: 16,
                corridorLength: 0,
                corridorWidth: 2
            );

            var batchConfig = new DungeonBatchConfig(
                seedCount: 5,
                startSeed: 2000,
                maxRetriesPerSeed: 3,
                openingWidth: 2,
                generatorConfig: genConfig
            );

            var report = DungeonBatchSimulator.RunSimulation(batchConfig, pieces);
            Assert.Greater(report.SuccessfulSeeds, 0);
            Assert.AreEqual(0, report.SeamCheckFailedSeeds, "All successful seeds should pass seam check when pieces have ground.");
            Assert.AreEqual(report.SuccessfulSeeds, report.SeamCheckPassedSeeds);
        }

        [Test]
        public void DungeonBatchSimulator_SeamCheck_FailsWhenPieceMissingGroundAtSeam()
        {
            // Create pieces where Normal piece has NO ground at its edges
            var pieces = new List<DungeonPieceDescriptor>();
            var emptyGround = new HashSet<GridCoord>(); // no ground!

            pieces.Add(new DungeonPieceDescriptor("cross_noground", DungeonRoomType.Normal, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = emptyGround
            });
            // But let's create a piece with partial ground that doesn't include the opening tiles
            var interiorOnlyGround = new HashSet<GridCoord> { new GridCoord(5, 5) };
            pieces[0].GroundCells = interiorOnlyGround;

            pieces.Add(new DungeonPieceDescriptor("ent", DungeonRoomType.Entrance, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = interiorOnlyGround,
                EntryAnchorCount = 1
            });
            pieces.Add(new DungeonPieceDescriptor("boss", DungeonRoomType.Boss, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = interiorOnlyGround
            });
            pieces.Add(new DungeonPieceDescriptor("safe", DungeonRoomType.SafeRoom, OpeningFlags.All, isRotatable: true)
            {
                GroundCells = interiorOnlyGround
            });

            var genConfig = new DungeonGeneratorConfig(
                minNormalRooms: 5,
                maxNormalRooms: 6,
                roomWidth: 16,
                roomHeight: 16
            );

            var batchConfig = new DungeonBatchConfig(
                seedCount: 2,
                startSeed: 2000,
                maxRetriesPerSeed: 2,
                openingWidth: 2,
                generatorConfig: genConfig
            );

            var report = DungeonBatchSimulator.RunSimulation(batchConfig, pieces);
            if (report.SuccessfulSeeds > 0)
            {
                Assert.Greater(report.SeamCheckFailedSeeds, 0, "Pieces lacking ground at edge tiles must fail seam check.");
                Assert.Greater(report.AllSeamFailures.Count, 0);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void DungeonPiece_RealPieceSet_SeamCheckAcrossAdjacentPlacedRooms_Passes()
        {
            var pieceSet = UnityEditor.AssetDatabase.LoadAssetAtPath<SBGames.Dungeon.Unity.Pieces.PieceSetSO>(
                "Assets/DungeonPiece/StarterSet/StarterPieceSet.asset"
            );
            Assert.IsNotNull(pieceSet, "StarterPieceSet.asset should exist.");
            Assert.AreEqual(20, pieceSet.PiecePrefabs.Count);

            var descriptors = pieceSet.GetDescriptors();
            Assert.AreEqual(20, descriptors.Count);

            // Verify each piece has ground populated
            foreach (var desc in descriptors)
            {
                Assert.IsNotNull(desc.GroundCells);
                Assert.Greater(desc.GroundCells.Count, 0, $"Piece {desc.PieceId} must have GroundCells populated.");
            }

            var genConfig = new DungeonGeneratorConfig(
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

            var batchConfig = new DungeonBatchConfig(
                seedCount: 5,
                startSeed: 3000,
                maxRetriesPerSeed: 5,
                openingWidth: pieceSet.OpeningWidth,
                generatorConfig: genConfig
            );

            var report = DungeonBatchSimulator.RunSimulation(batchConfig, descriptors);
            Assert.AreEqual(5, report.SuccessfulSeeds, "All 5 seeds should successfully find matching pieces.");
            Assert.AreEqual(0, report.SeamCheckFailedSeeds, $"Seam check failures detected: {string.Join("; ", report.AllSeamFailures)}");
            Assert.AreEqual(5, report.SeamCheckPassedSeeds);
        }
#endif

        [Test]
        public void PieceOpening_EdgeTiles_AndOutsideTiles_CalculatedCorrectly()
        {
            // North opening at (4, 9), width 2
            var northOp = new PieceOpening(4, 9, CardinalDirection.North, 2);
            var edgeN = northOp.GetEdgeTiles();
            Assert.AreEqual(2, edgeN.Count);
            Assert.AreEqual(new GridCoord(4, 9), edgeN[0]);
            Assert.AreEqual(new GridCoord(5, 9), edgeN[1]);

            var outsideN = northOp.GetOutsideConnectionTiles();
            Assert.AreEqual(2, outsideN.Count);
            Assert.AreEqual(new GridCoord(4, 10), outsideN[0]);
            Assert.AreEqual(new GridCoord(5, 10), outsideN[1]);

            // East opening at (9, 3), width 2
            var eastOp = new PieceOpening(9, 3, CardinalDirection.East, 2);
            var edgeE = eastOp.GetEdgeTiles();
            Assert.AreEqual(new GridCoord(9, 3), edgeE[0]);
            Assert.AreEqual(new GridCoord(9, 4), edgeE[1]);

            var outsideE = eastOp.GetOutsideConnectionTiles();
            Assert.AreEqual(new GridCoord(10, 3), outsideE[0]);
            Assert.AreEqual(new GridCoord(10, 4), outsideE[1]);
        }

        [Test]
        public void PieceOpening_NonSquareRotation_CyclePreservesDimensionsAndOpenings()
        {
            int cellW = 20;
            int cellH = 12;

            // North opening at anchor (6, 11), width 2
            var op = new PieceOpening(6, 11, CardinalDirection.North, 2);

            // 1 turn: (20x12) -> (12x20), North -> East
            // newAnchor = (curH - 1, curW - width - anchor.X) = (11, 20 - 2 - 6) = (11, 12)
            var op1 = op.RotateClockwise(cellW, cellH, 1);
            Assert.AreEqual(CardinalDirection.East, op1.Direction);
            Assert.AreEqual(new GridCoord(11, 12), op1.AnchorTile);

            // 2 turns: (12x20) -> (20x12), East -> South
            // newAnchor = (anchor.Y, 0) = (12, 0)
            var op2 = op.RotateClockwise(cellW, cellH, 2);
            Assert.AreEqual(CardinalDirection.South, op2.Direction);
            Assert.AreEqual(new GridCoord(12, 0), op2.AnchorTile);

            // 3 turns: (20x12) -> (12x20), South -> West
            // newAnchor = (0, curW - width - anchor.X) = (0, 20 - 2 - 12) = (0, 6)
            var op3 = op.RotateClockwise(cellW, cellH, 3);
            Assert.AreEqual(CardinalDirection.West, op3.Direction);
            Assert.AreEqual(new GridCoord(0, 6), op3.AnchorTile);

            // 4 turns: full cycle back to (6, 11) North
            var op4 = op.RotateClockwise(cellW, cellH, 4);
            Assert.AreEqual(op, op4);
        }

        [Test]
        public void OpeningGeometry_CalculateSnappedWorldOrigin_SnapsOppositeOpeningsCorrectly()
        {
            // Room A at (10, 20) with East opening at local (15, 4)
            var originA = new GridCoord(10, 20);
            var openingA = new PieceOpening(15, 4, CardinalDirection.East, 2);

            // Room B with West opening at local (0, 2)
            var openingB = new PieceOpening(0, 2, CardinalDirection.West, 2);

            // OriginB = OriginA + OpeningA.AnchorTile + Normal(OpeningA.Direction) - OpeningB.AnchorTile
            // OriginB = (10, 20) + (15, 4) + (1, 0) - (0, 2) = (26, 22)
            var originB = OpeningGeometry.CalculateSnappedWorldOrigin(originA, openingA, openingB);
            Assert.AreEqual(new GridCoord(26, 22), originB);

            // Verify seam alignment:
            // Outside connection of A in world: (10+15+1, 20+4) = (26, 24) and (26, 25)
            // Edge tiles of B in world: (26+0, 22+2) = (26, 24) and (26+0, 22+3) = (26, 25)
            var outsideA = openingA.GetOutsideConnectionTiles();
            var edgeB = openingB.GetEdgeTiles();
            for (int i = 0; i < 2; i++)
            {
                var worldOutsideA = originA + outsideA[i];
                var worldEdgeB = originB + edgeB[i];
                Assert.AreEqual(worldOutsideA, worldEdgeB, $"Seam tile {i} must align exactly.");
            }
        }

        [Test]
        public void PieceValidator_FreeformOpenings_BadgesCorrect()
        {
            int w = 14;
            int h = 10;

            // 1. Overlapping openings error
            var opA = new PieceOpening(4, 9, CardinalDirection.North, 2); // tiles (4,9), (5,9)
            var opB = new PieceOpening(5, 9, CardinalDirection.North, 2); // tiles (5,9), (6,9) - overlaps at (5,9)
            var pieceOverlap = new DungeonPieceDescriptor("overlap", PieceRole.Room, DungeonRoomType.Normal, w, h, new[] { opA, opB });
            var badges1 = PieceValidator.ValidatePiece(pieceOverlap, w, h);
            Assert.IsTrue(PieceValidator.HasErrors(badges1));
            Assert.IsTrue(badges1.Exists(b => b.Message.Contains("overlap")));

            // 2. Missing ground at edge tiles error
            var opValid = new PieceOpening(4, 9, CardinalDirection.North, 2);
            var pieceMissingGround = new DungeonPieceDescriptor("missingGround", PieceRole.Room, DungeonRoomType.Normal, w, h, new[] { opValid });
            var groundPartial = new HashSet<GridCoord> { new GridCoord(4, 9) }; // missing (5, 9)
            var badges2 = PieceValidator.ValidatePiece(pieceMissingGround, w, h, groundCells: groundPartial);
            Assert.IsTrue(PieceValidator.HasErrors(badges2));
            Assert.IsTrue(badges2.Exists(b => b.Message.Contains("does not have painted Ground")));

            // 3. Ground reaches outer border where no opening declared -> Warning
            var fullGround = new HashSet<GridCoord>
            {
                new GridCoord(4, 9), new GridCoord(5, 9), // Opening tiles
                new GridCoord(0, 4) // Border tile with no opening
            };
            var pieceUndeclaredGround = new DungeonPieceDescriptor("undeclaredBorder", PieceRole.Room, DungeonRoomType.Normal, w, h, new[] { opValid });
            var badges3 = PieceValidator.ValidatePiece(pieceUndeclaredGround, w, h, groundCells: fullGround);
            Assert.IsFalse(PieceValidator.HasErrors(badges3));
            Assert.IsTrue(badges3.Exists(b => b.IsWarning && b.Message.Contains("outer border")));
        }

        [Test]
        public void DungeonSeamLayoutSolver_DirectRoomToRoom_SnapsSeamsWithZeroOverlap()
        {
            var rng = new MockRng();

            // Entrance piece (12x10) with East opening at (11, 4)
            var entOp = new PieceOpening(11, 4, CardinalDirection.East, 2);
            var entDesc = new DungeonPieceDescriptor("ent_12x10", PieceRole.Room, DungeonRoomType.Entrance, 12, 10, new[] { entOp })
            {
                EntryAnchorCount = 1
            };

            // Normal piece (14x12) with West opening at (0, 3)
            var normOp = new PieceOpening(0, 3, CardinalDirection.West, 2);
            var normDesc = new DungeonPieceDescriptor("norm_14x12", PieceRole.Room, DungeonRoomType.Normal, 14, 12, new[] { normOp });

            // Graph: Room 0 (Entrance) -> East -> Room 1 (Normal)
            var graph = new DungeonLayoutGraph();
            var nodeEnt = new DungeonLayoutNode(0, DungeonRoomType.Entrance, new GridCoord(0, 0));
            var nodeNorm = new DungeonLayoutNode(1, DungeonRoomType.Normal, new GridCoord(1, 0));
            graph.AddRoom(nodeEnt);
            graph.AddRoom(nodeNorm);
            graph.AddDoor(new DungeonDoorConnection(0, 0, 1, CardinalDirection.East, false));

            var result = DungeonSeamLayoutSolver.Solve(
                graph,
                new[] { entDesc, normDesc },
                pathwayProbability: 0f,
                openingWidth: 2,
                rng: rng
            );

            Assert.IsTrue(result.Success, $"Solve failed: {result.FailureReason}");
            Assert.AreEqual(2, result.PlacedRooms.Count);
            Assert.AreEqual(0, result.PlacedPathways.Count);
            Assert.AreEqual(1, result.Seams.Count);

            // World origin of entrance is (0, 0)
            var placedEnt = result.PlacedRooms[0];
            var placedNorm = result.PlacedRooms[1];
            Assert.AreEqual(new GridCoord(0, 0), placedEnt.WorldOrigin);

            // Snapped origin of normal room:
            // (0, 0) + (11, 4) + (1, 0) - (0, 3) = (12, 1)
            Assert.AreEqual(new GridCoord(12, 1), placedNorm.WorldOrigin);

            // Verify seam alignment
            var seam = result.Seams[0];
            var outEnt0 = placedEnt.WorldOrigin + seam.OpeningA.GetOutsideConnectionTiles()[0];
            var edgeNorm0 = placedNorm.WorldOrigin + seam.OpeningB.GetEdgeTiles()[0];
            Assert.AreEqual(outEnt0, edgeNorm0);

            // Verify tile count matches sum with zero overlap
            int totalExpectedTiles = (12 * 10) + (14 * 12);
            Assert.AreEqual(totalExpectedTiles, result.OccupiedDungeonTiles.Count);
        }

        [Test]
        public void DungeonSeamLayoutSolver_RoomToPathwayToRoom_BridgesWhenPathwayForcedOrNeeded()
        {
            var rng = new MockRng();

            // Entrance piece (10x10) with South opening at (4, 0)
            var entOp = new PieceOpening(4, 0, CardinalDirection.South, 2);
            var entDesc = new DungeonPieceDescriptor("ent_10x10", PieceRole.Room, DungeonRoomType.Entrance, 10, 10, new[] { entOp })
            {
                EntryAnchorCount = 1
            };

            // Straight pathway (6x8) with North opening at (2, 7) and South opening at (2, 0)
            var pathNorthOp = new PieceOpening(2, 7, CardinalDirection.North, 2);
            var pathSouthOp = new PieceOpening(2, 0, CardinalDirection.South, 2);
            var pathDesc = new DungeonPieceDescriptor("path_straight_6x8", PieceRole.Pathway, DungeonRoomType.Normal, 6, 8, new[] { pathNorthOp, pathSouthOp });

            // Normal room (12x12) with North opening at (5, 11)
            var normOp = new PieceOpening(5, 11, CardinalDirection.North, 2);
            var normDesc = new DungeonPieceDescriptor("norm_12x12", PieceRole.Room, DungeonRoomType.Normal, 12, 12, new[] { normOp });

            // Graph: Room 0 (Entrance) -> South -> Room 1 (Normal)
            var graph = new DungeonLayoutGraph();
            var nodeEnt = new DungeonLayoutNode(0, DungeonRoomType.Entrance, new GridCoord(0, 0));
            var nodeNorm = new DungeonLayoutNode(1, DungeonRoomType.Normal, new GridCoord(0, -1));
            graph.AddRoom(nodeEnt);
            graph.AddRoom(nodeNorm);
            graph.AddDoor(new DungeonDoorConnection(0, 0, 1, CardinalDirection.South, false));

            var result = DungeonSeamLayoutSolver.Solve(
                graph,
                new[] { entDesc, normDesc, pathDesc },
                pathwayProbability: 1.0f, // Force pathway insertion
                openingWidth: 2,
                rng: rng
            );

            Assert.IsTrue(result.Success, $"Solve failed: {result.FailureReason}");
            Assert.AreEqual(2, result.PlacedRooms.Count, "Must have placed 2 rooms.");
            Assert.AreEqual(1, result.PlacedPathways.Count, "Must have placed 1 pathway connector.");
            Assert.AreEqual(2, result.Seams.Count, "Must have 2 seams (Room->Path and Path->Room).");

            var placedPath = result.PlacedPathways[0];
            Assert.AreEqual(PieceRole.Pathway, placedPath.Descriptor.Role);

            // Zero collision/overlap in occupied tiles
            int expectedTiles = (10 * 10) + (6 * 8) + (12 * 12);
            Assert.AreEqual(expectedTiles, result.OccupiedDungeonTiles.Count);
        }

        [Test]
        public void DungeonSeamLayoutSolver_UnusedSockets_CollectedForCapping()
        {
            var rng = new MockRng();

            // Entrance (10x10) with East opening at (9, 4)
            var entOp = new PieceOpening(9, 4, CardinalDirection.East, 2);
            var entDesc = new DungeonPieceDescriptor("ent", PieceRole.Room, DungeonRoomType.Entrance, 10, 10, new[] { entOp })
            {
                EntryAnchorCount = 1
            };

            // Normal room (10x10) with 3 openings (West at (0, 4), North at (4, 9), East at (9, 4))
            var normWest = new PieceOpening(0, 4, CardinalDirection.West, 2);
            var normNorth = new PieceOpening(4, 9, CardinalDirection.North, 2);
            var normEast = new PieceOpening(9, 4, CardinalDirection.East, 2);
            var normDesc = new DungeonPieceDescriptor("norm_3way", PieceRole.Room, DungeonRoomType.Normal, 10, 10, new[] { normWest, normNorth, normEast });

            var graph = new DungeonLayoutGraph();
            var nodeEnt = new DungeonLayoutNode(0, DungeonRoomType.Entrance, new GridCoord(0, 0));
            var nodeNorm = new DungeonLayoutNode(1, DungeonRoomType.Normal, new GridCoord(1, 0));
            graph.AddRoom(nodeEnt);
            graph.AddRoom(nodeNorm);
            graph.AddDoor(new DungeonDoorConnection(0, 0, 1, CardinalDirection.East, false));

            var result = DungeonSeamLayoutSolver.Solve(
                graph,
                new[] { entDesc, normDesc },
                pathwayProbability: 0f,
                openingWidth: 2,
                rng: rng
            );

            Assert.IsTrue(result.Success);
            // normNorth and normEast are unused sockets that need capping
            Assert.AreEqual(2, result.UnusedSockets.Count);
            Assert.IsTrue(result.UnusedSockets.Exists(s => s.Opening.Direction == CardinalDirection.North));
            Assert.IsTrue(result.UnusedSockets.Exists(s => s.Opening.Direction == CardinalDirection.East));
        }
    }
}
