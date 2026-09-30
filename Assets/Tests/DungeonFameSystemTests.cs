using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DungeonFameSystemTests
    {
        private GameObject _holderGo;
        private DungeonFameSystem _fameSystem;
        private DungeonProgression _progression;
        private CoreGameLoop _coreGameLoop;
        private DungeonMaster _dungeonMaster;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_DungeonFameSystem]");
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 50, roundLossPenalty: 20);

            _fameSystem = _holderGo.AddComponent<DungeonFameSystem>();
            _progression = _holderGo.AddComponent<DungeonProgression>();
            _coreGameLoop = _holderGo.AddComponent<CoreGameLoop>();

            _fameSystem.Initialize(config: null, loop: _coreGameLoop, progression: _progression, dm: _dungeonMaster, startingStreak: 0);
            _progression.Initialize(startingRank: DungeonRank.E, config: null, loop: _coreGameLoop, fame: _fameSystem);
            _coreGameLoop.Initialize(dm: _dungeonMaster, initialRound: 1, planningAllowance: 15, victoryBonus: 25, fame: _fameSystem);
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
        public void DungeonFameTier_EnumValuesAndThresholds_AreValid()
        {
            Assert.AreEqual(0, (int)DungeonFameTier.Unknown);
            Assert.AreEqual(1, (int)DungeonFameTier.Rumored);
            Assert.AreEqual(2, (int)DungeonFameTier.Feared);
            Assert.AreEqual(3, (int)DungeonFameTier.Infamous);
            Assert.AreEqual(4, (int)DungeonFameTier.Legendary);
            Assert.AreEqual(5, (int)DungeonFameTier.MythicCalamity);
            Assert.AreEqual(6, DungeonFameTierExtensions.TierCount);

            Assert.AreEqual(DungeonFameTier.Unknown, DungeonFameTierExtensions.FromWinStreak(0));
            Assert.AreEqual(DungeonFameTier.Unknown, DungeonFameTierExtensions.FromWinStreak(1));
            Assert.AreEqual(DungeonFameTier.Rumored, DungeonFameTierExtensions.FromWinStreak(2));
            Assert.AreEqual(DungeonFameTier.Feared, DungeonFameTierExtensions.FromWinStreak(4));
            Assert.AreEqual(DungeonFameTier.Infamous, DungeonFameTierExtensions.FromWinStreak(7));
            Assert.AreEqual(DungeonFameTier.Legendary, DungeonFameTierExtensions.FromWinStreak(10));
            Assert.AreEqual(DungeonFameTier.MythicCalamity, DungeonFameTierExtensions.FromWinStreak(15));
        }

        [Test]
        public void DungeonFameSystem_WinStreakAndTierEscalation_WorkCorrectly()
        {
            Assert.AreEqual(0, _fameSystem.CurrentWinStreak);
            Assert.AreEqual(DungeonFameTier.Unknown, _fameSystem.CurrentTier);

            // Win 2 rounds -> Rumored (Tier 1)
            _fameSystem.RecordBattleOutcome(BattleOutcome.Victory, DungeonRank.E, DungeonRank.E);
            _fameSystem.RecordBattleOutcome(BattleOutcome.Victory, DungeonRank.E, DungeonRank.E);

            Assert.AreEqual(2, _fameSystem.CurrentWinStreak);
            Assert.AreEqual(DungeonFameTier.Rumored, _fameSystem.CurrentTier);

            // Win 2 more rounds -> Feared (Tier 2 @ streak 4)
            _fameSystem.RecordBattleOutcome(BattleOutcome.Victory, DungeonRank.E, DungeonRank.E);
            _fameSystem.RecordBattleOutcome(BattleOutcome.Victory, DungeonRank.E, DungeonRank.E);

            Assert.AreEqual(4, _fameSystem.CurrentWinStreak);
            Assert.AreEqual(DungeonFameTier.Feared, _fameSystem.CurrentTier);
            Assert.AreEqual(DungeonFameTier.Feared, _fameSystem.HighestTierReached);
        }

        [Test]
        public void DungeonFameSystem_DefeatResetsStreakAndTier_ToUnknown()
        {
            // Build up to Feared (streak 4)
            for (int i = 0; i < 4; i++)
            {
                _fameSystem.RecordBattleOutcome(BattleOutcome.Victory, DungeonRank.E, DungeonRank.E);
            }
            Assert.AreEqual(DungeonFameTier.Feared, _fameSystem.CurrentTier);
            Assert.AreEqual(4, _fameSystem.CurrentWinStreak);

            // Single defeat
            _fameSystem.RecordBattleOutcome(BattleOutcome.Defeat, DungeonRank.E, DungeonRank.E);

            Assert.AreEqual(0, _fameSystem.CurrentWinStreak);
            Assert.AreEqual(DungeonFameTier.Unknown, _fameSystem.CurrentTier);
            Assert.AreEqual(4, _fameSystem.HighestWinStreak); // Highest streak preserved
            Assert.AreEqual(DungeonFameTier.Feared, _fameSystem.HighestTierReached); // Highest tier preserved
        }

        [Test]
        public void DungeonProgression_WithFameSystem_IncreasesHigherRankProbabilities()
        {
            // Base probabilities without fame at Rank E
            _progression.Initialize(startingRank: DungeonRank.E, fame: null);
            float[] baseProbs = _progression.GetNormalizedProbabilities(DungeonRank.E);

            // Probabilities with high fame (Streak 10 -> Legendary)
            _fameSystem.Initialize(config: null, loop: _coreGameLoop, progression: _progression, dm: _dungeonMaster, startingStreak: 10);
            _progression.SetFameSystem(_fameSystem);
            float[] fameProbs = _progression.GetNormalizedProbabilities(DungeonRank.E);

            // Probability of +1 Rank (Rank D) and +2 Rank (Rank C) should be significantly higher with Legendary fame
            float baseHigherProb = baseProbs[(int)DungeonRank.D] + baseProbs[(int)DungeonRank.C];
            float fameHigherProb = fameProbs[(int)DungeonRank.D] + fameProbs[(int)DungeonRank.C];

            Assert.Greater(fameHigherProb, baseHigherProb, "Fame should increase the likelihood of higher-rank adventurers challenging the dungeon.");
            
            // Total probabilities must still normalize cleanly to 1.0
            float sum = 0f;
            for (int i = 0; i < fameProbs.Length; i++) sum += fameProbs[i];
            Assert.AreEqual(1.0f, sum, 0.0001f);
        }

        [Test]
        public void DungeonFameSystem_RewardScaling_MultipliesEnergyAndXp()
        {
            // Baseline at Tier 0 (Unknown)
            _fameSystem.Initialize(startingStreak: 0);
            int baseEnergy = 25;
            int scaledEnergyT0 = _fameSystem.CalculateScaledEnergyReward(baseEnergy, DungeonRank.E, DungeonRank.E);
            Assert.AreEqual(25, scaledEnergyT0);

            // Legendary Tier (Streak 10)
            _fameSystem.Initialize(startingStreak: 10);
            Assert.AreEqual(DungeonFameTier.Legendary, _fameSystem.CurrentTier);

            int scaledEnergyT4 = _fameSystem.CalculateScaledEnergyReward(baseEnergy, DungeonRank.E, DungeonRank.E);
            Assert.Greater(scaledEnergyT4, baseEnergy);
            Assert.AreEqual(50, scaledEnergyT4); // 2.0x multiplier

            // Upset bonus: Defeating +1 rank adventurer grants additional upset multiplier
            int upsetEnergy = _fameSystem.CalculateScaledEnergyReward(baseEnergy, DungeonRank.E, DungeonRank.D);
            Assert.Greater(upsetEnergy, scaledEnergyT4);
        }

        [Test]
        public void DungeonFameSystem_BreachPenalty_ScalesWithHigherRankDelta()
        {
            int basePenalty = 20;

            // Defeat against same rank -> base penalty
            int sameRankPenalty = _fameSystem.CalculateScaledBreachPenalty(basePenalty, DungeonRank.E, DungeonRank.E);
            Assert.AreEqual(basePenalty, sameRankPenalty);

            // Defeat against +2 rank higher adventurer (Rank C vs Rank E dungeon) -> +50% penalty (1.5x)
            int highRankPenalty = _fameSystem.CalculateScaledBreachPenalty(basePenalty, DungeonRank.E, DungeonRank.C);
            Assert.Greater(highRankPenalty, basePenalty);
            Assert.AreEqual(30, highRankPenalty);
        }

        [Test]
        public void DungeonFameSystem_RareArtifactDrop_RollsAndAddsToDungeonMaster()
        {
            // At Tier Feared (streak 4), defeat a higher rank adventurer with deterministic 100% RNG roll (roll = 0.0)
            _fameSystem.Initialize(dm: _dungeonMaster, startingStreak: 4);
            int initialArtifactCount = _dungeonMaster.ArtifactCount;

            // Defeat higher-rank adventurer with rollOverride 0.05f (< 0.24f drop chance)
            var dropped = _fameSystem.RollRareArtifactDrop(DungeonRank.E, DungeonRank.D, rollOverride: 0.05f);

            Assert.IsNotNull(dropped, "Defeating higher-rank challenger under high fame should roll artifact drop.");
            Assert.AreEqual(initialArtifactCount + 1, _dungeonMaster.ArtifactCount);
            Assert.IsTrue(System.Linq.Enumerable.Contains(_dungeonMaster.Artifacts, dropped));
        }

        [Test]
        public void CoreGameLoop_Integration_FameScalesVictoryRewards()
        {
            _coreGameLoop.StartGame();
            _coreGameLoop.StartBattle();

            // Set Legendary Fame
            _fameSystem.Initialize(dm: _dungeonMaster, startingStreak: 10);
            int initialEnergy = _dungeonMaster.Energy;

            // Resolve battle win
            var res = _coreGameLoop.ResolveBattle(BattleOutcome.Victory);

            // Standard victory is 25, with 2.0x Fame multiplier -> 50 + planning allowance 15 = +65 energy (capped at maxEnergy 100)
            Assert.Greater(res.EnergyAwarded, 25);
            Assert.AreEqual(50, res.EnergyAwarded);
        }
    }
}
