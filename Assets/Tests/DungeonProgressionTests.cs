using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DungeonProgressionTests
    {
        private GameObject _holderGo;
        private DungeonProgression _progression;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_DungeonProgression]");
            _progression = _holderGo.AddComponent<DungeonProgression>();
            _progression.Initialize(startingRank: DungeonRank.E);
        }

        [TearDown]
        public void Teardown()
        {
            if (_holderGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_holderGo);
            }
        }

        [Test]
        public void DungeonRank_EnumOrderAndCount_AreValid()
        {
            Assert.AreEqual(0, (int)DungeonRank.E);
            Assert.AreEqual(1, (int)DungeonRank.D);
            Assert.AreEqual(2, (int)DungeonRank.C);
            Assert.AreEqual(3, (int)DungeonRank.B);
            Assert.AreEqual(4, (int)DungeonRank.A);
            Assert.AreEqual(5, (int)DungeonRank.S);
            Assert.AreEqual(6, DungeonRankExtensions.RankCount);
        }

        [Test]
        public void DungeonRank_Extensions_ClampingWorksCorrectly()
        {
            Assert.AreEqual(DungeonRank.D, DungeonRank.E.GetNextRank());
            Assert.AreEqual(DungeonRank.S, DungeonRank.S.GetNextRank()); // Max clamp
            Assert.AreEqual(DungeonRank.E, DungeonRank.E.GetPreviousRank()); // Min clamp
            Assert.AreEqual(DungeonRank.A, DungeonRank.S.GetPreviousRank());

            Assert.AreEqual(1, DungeonRank.E.RankDifference(DungeonRank.D));
            Assert.AreEqual(-1, DungeonRank.D.RankDifference(DungeonRank.E));
        }

        [Test]
        public void Probabilities_SumToOne_AcrossAllRanks()
        {
            for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
            {
                var rank = (DungeonRank)r;
                float[] probs = _progression.GetNormalizedProbabilities(rank);

                Assert.AreEqual(6, probs.Length);

                float sum = 0f;
                for (int i = 0; i < probs.Length; i++)
                {
                    Assert.GreaterOrEqual(probs[i], 0f, $"Probability at index {i} for rank {rank} is negative");
                    sum += probs[i];
                }

                Assert.AreEqual(1.0f, sum, 0.0001f, $"Sum of probabilities for {rank} does not equal 1.0");
            }
        }

        [Test]
        public void Probabilities_MatchingRankHasHighestWeight()
        {
            for (int r = 0; r < DungeonRankExtensions.RankCount; r++)
            {
                var rank = (DungeonRank)r;
                float[] probs = _progression.GetNormalizedProbabilities(rank);

                float matchingProb = probs[(int)rank];
                for (int other = 0; other < probs.Length; other++)
                {
                    if (other != (int)rank)
                    {
                        Assert.Greater(matchingProb, probs[other],
                            $"Matching rank {rank} ({matchingProb:P1}) should have higher probability than rank {(DungeonRank)other} ({probs[other]:P1})");
                    }
                }
            }
        }

        [Test]
        public void Probabilities_HigherRankDownwardAttempt_IsMoreLikelyThanLowerRankUpwardAttempt()
        {
            // For Rank C:
            // Delta = +1 (Rank D dungeon tested against Rank C attempt is downward invasion by D or C against D)
            // Let's test Rank C dungeon:
            // Rank D attempting Rank C (Lower trying Higher, Delta = -1) -> ~7%
            // Rank B attempting Rank C (Higher trying Lower, Delta = +1) -> ~15%
            float[] probs = _progression.GetNormalizedProbabilities(DungeonRank.C);

            float lowerRankTryingHigher = probs[(int)DungeonRank.D]; // Rank D trying Rank C dungeon
            float higherRankTryingLower = probs[(int)DungeonRank.B]; // Rank B trying Rank C dungeon

            Assert.Greater(higherRankTryingLower, lowerRankTryingHigher,
                $"Higher-ranked adventurer attempting lower dungeon ({higherRankTryingLower:P1}) should have higher chance than lower adventurer ({lowerRankTryingHigher:P1})");
        }

        [Test]
        public void DeterministicRoll_SelectsExpectedRank()
        {
            _progression.Initialize(startingRank: DungeonRank.C);
            float[] probs = _progression.GetNormalizedProbabilities(DungeonRank.C);

            // Roll 0.0 should fall into the first non-zero probability bucket
            var firstBucketRank = _progression.DetermineAttemptRank(0.0f);
            Assert.IsTrue((int)firstBucketRank >= 0);

            // A roll within the matching rank range should return Rank C
            float matchingStart = probs[0] + probs[1];
            float midMatching = matchingStart + (probs[2] * 0.5f);
            var result = _progression.DetermineAttemptRank(midMatching);
            Assert.AreEqual(DungeonRank.C, result);
        }

        [Test]
        public void RecordAttemptResult_TracksStatsAndStreaksCorrectly()
        {
            _progression.Initialize(startingRank: DungeonRank.E);

            // 1. Victory
            int xp1 = _progression.RecordAttemptResult(DungeonRank.E, BattleOutcome.Victory);
            Assert.Greater(xp1, 0);
            Assert.AreEqual(1, _progression.TotalAttempts);
            Assert.AreEqual(1, _progression.TotalVictories);
            Assert.AreEqual(0, _progression.TotalDefeats);
            Assert.AreEqual(1, _progression.CurrentWinStreak);
            Assert.AreEqual(1, _progression.HighestWinStreak);

            // 2. Another Victory
            _progression.RecordAttemptResult(DungeonRank.E, BattleOutcome.Victory);
            Assert.AreEqual(2, _progression.TotalAttempts);
            Assert.AreEqual(2, _progression.TotalVictories);
            Assert.AreEqual(2, _progression.CurrentWinStreak);

            // 3. Defeat resets current streak but preserves highest streak
            _progression.RecordAttemptResult(DungeonRank.E, BattleOutcome.Defeat);
            Assert.AreEqual(3, _progression.TotalAttempts);
            Assert.AreEqual(2, _progression.TotalVictories);
            Assert.AreEqual(1, _progression.TotalDefeats);
            Assert.AreEqual(0, _progression.CurrentWinStreak);
            Assert.AreEqual(2, _progression.HighestWinStreak);
        }

        [Test]
        public void ProgressionPoints_AccumulatesAndTriggersPromotion()
        {
            _progression.Initialize(startingRank: DungeonRank.E);
            Assert.AreEqual(DungeonRank.E, _progression.CurrentRank);

            int requiredPoints = _progression.RequiredPointsForNextRank;
            Assert.Greater(requiredPoints, 0);

            // Add points just below threshold
            _progression.AddProgressionPoints(requiredPoints - 10);
            Assert.AreEqual(DungeonRank.E, _progression.CurrentRank);
            Assert.AreEqual(requiredPoints - 10, _progression.CurrentProgressionPoints);
            Assert.IsFalse(_progression.CanAdvanceRank());

            // Add remaining points + 15 overflow
            _progression.AddProgressionPoints(25);
            Assert.AreEqual(DungeonRank.D, _progression.CurrentRank);
            Assert.AreEqual(15, _progression.CurrentProgressionPoints); // Carryover verified
        }

        [Test]
        public void ManualRankControl_AndReset_WorkAsExpected()
        {
            _progression.SetRank(DungeonRank.A);
            Assert.AreEqual(DungeonRank.A, _progression.CurrentRank);

            _progression.ResetProgression(DungeonRank.E);
            Assert.AreEqual(DungeonRank.E, _progression.CurrentRank);
            Assert.AreEqual(0, _progression.TotalAttempts);
            Assert.AreEqual(0, _progression.CurrentProgressionPoints);
        }
    }
}
