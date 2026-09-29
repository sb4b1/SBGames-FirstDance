using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DifficultyScalerTests
    {
        private GameObject _holderGo;
        private DifficultyScaler _scaler;
        private DungeonMaster _dungeonMaster;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_DifficultyScaler]");
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 50);

            _scaler = _holderGo.AddComponent<DifficultyScaler>();
            _scaler.Initialize(dm: _dungeonMaster);
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
        public void MonsterCombatPower_CalculatesCorrectly_MatchingFormula()
        {
            // Level 1 monster: Base 100 + (1 * 25) = 125
            // Stats: HP 100 * 0.5 = 50, DMG 10 * 3.0 = 30, ATK_INT 1.0 -> 1 atk/sec * 10 = 10, DEF 0, SPD 3.5 * 2 = 7
            // Total Stat points = 50 + 30 + 10 + 7 = 97
            // Total CP = 125 + 97 = 222
            var monster = new MonsterStats(
                monsterId: "test_m1",
                monsterName: "Test Monster",
                level: 1,
                maxHealth: 100f,
                damage: 10f,
                attackInterval: 1.0f,
                moveSpeed: 3.5f,
                defense: 0f,
                activeModifierCount: 0,
                equippedArtifactPower: 0
            );

            int cp = DifficultyScaler.CalculateMonsterCombatPower(monster);
            Assert.AreEqual(222, cp);
        }

        [Test]
        public void MonsterCombatPower_WithModifiersAndArtifacts_ScalesAccurately()
        {
            // Level 5 monster: Base 100 + (5 * 25) = 225
            // Stats: HP 200 * 0.5 = 100, DMG 20 * 3.0 = 60, ATK_INT 0.5 -> 2 atk/sec * 10 = 20, DEF 10 * 2.5 = 25, SPD 4 * 2 = 8
            // Total Stat points = 100 + 60 + 20 + 25 + 8 = 213
            // Modifiers: 2 * 15 = 30
            // Artifact Power: 50 * 1.0 = 50
            // Total CP = 225 + 213 + 30 + 50 = 518
            var monster = new MonsterStats(
                monsterId: "test_elite",
                monsterName: "Elite Monster",
                level: 5,
                maxHealth: 200f,
                damage: 20f,
                attackInterval: 0.5f,
                moveSpeed: 4.0f,
                defense: 10f,
                activeModifierCount: 2,
                equippedArtifactPower: 50
            );

            int cp = DifficultyScaler.CalculateMonsterCombatPower(monster);
            Assert.AreEqual(518, cp);
        }

        [Test]
        public void CalculateDifficulty_AggregatesMultipleMonsters()
        {
            var m1 = MonsterStats.CreateForRank(DungeonRank.E);
            var m2 = MonsterStats.CreateForRank(DungeonRank.E);

            int cp1 = DifficultyScaler.CalculateMonsterCombatPower(m1);
            int cp2 = DifficultyScaler.CalculateMonsterCombatPower(m2);

            var eval = DifficultyScaler.CalculateDifficulty(
                rank: DungeonRank.E,
                monsters: new[] { m1, m2 },
                playerModifier: 1.0f
            );

            Assert.AreEqual(2, eval.MonsterCount);
            Assert.AreEqual(cp1 + cp2, eval.RawMonsterCombatPower);
            Assert.GreaterOrEqual(eval.TotalDungeonDefensePower, eval.RawMonsterCombatPower);
        }

        [Test]
        public void CalculateDifficulty_RankMultipliers_ScaleHigherTiers()
        {
            var monster = MonsterStats.CreateForRank(DungeonRank.E);
            int rawCp = DifficultyScaler.CalculateMonsterCombatPower(monster);

            var evalE = DifficultyScaler.CalculateDifficulty(DungeonRank.E, new[] { monster });
            var evalD = DifficultyScaler.CalculateDifficulty(DungeonRank.D, new[] { monster });
            var evalS = DifficultyScaler.CalculateDifficulty(DungeonRank.S, new[] { monster });

            Assert.AreEqual(rawCp, evalE.TotalDungeonDefensePower);
            Assert.AreEqual(Mathf.RoundToInt(rawCp * 1.4f), evalD.TotalDungeonDefensePower);
            Assert.AreEqual(Mathf.RoundToInt(rawCp * 7.0f), evalS.TotalDungeonDefensePower);
        }

        [Test]
        public void CalculateDifficulty_WithDungeonMasterArtifacts_GrantsSynergyBonus()
        {
            var monster = MonsterStats.CreateForRank(DungeonRank.E);

            // DM initially has Master's Heart (PowerLevel 1)
            var evalWithDM = DifficultyScaler.CalculateDifficulty(
                rank: DungeonRank.E,
                monsters: new[] { monster },
                playerModifier: 1.0f,
                dm: _dungeonMaster
            );

            var evalWithoutDM = DifficultyScaler.CalculateDifficulty(
                rank: DungeonRank.E,
                monsters: new[] { monster },
                playerModifier: 1.0f,
                dm: null
            );

            Assert.Greater(evalWithDM.DmSynergyPowerBonus, 0);
            Assert.AreEqual(0, evalWithoutDM.DmSynergyPowerBonus);
            Assert.Greater(evalWithDM.TotalDungeonDefensePower, evalWithoutDM.TotalDungeonDefensePower);
        }

        [Test]
        public void CalculateDifficulty_NullAndEmptyMonsters_HandlesGracefully()
        {
            // Null collection
            var evalNull = DifficultyScaler.CalculateDifficulty(DungeonRank.E, (IEnumerable<IMonsterStats>)null);
            Assert.AreEqual(0, evalNull.MonsterCount);
            Assert.AreEqual(0, evalNull.RawMonsterCombatPower);
            Assert.AreEqual(0, evalNull.TotalDungeonDefensePower);
            Assert.AreEqual(DifficultyTier.Lethal, evalNull.ChallengeTier);

            // Empty collection
            var evalEmpty = DifficultyScaler.CalculateDifficulty(DungeonRank.E, new List<IMonsterStats>());
            Assert.AreEqual(0, evalEmpty.MonsterCount);
            Assert.AreEqual(0, evalEmpty.RawMonsterCombatPower);
            Assert.AreEqual(0, evalEmpty.TotalDungeonDefensePower);
        }

        [Test]
        public void CalculateDifficulty_PlayerModifier_ScalesDefensePower()
        {
            var monster = MonsterStats.CreateForRank(DungeonRank.E);

            var evalNormal = DifficultyScaler.CalculateDifficulty(DungeonRank.E, new[] { monster }, playerModifier: 1.0f);
            var evalBuffed = DifficultyScaler.CalculateDifficulty(DungeonRank.E, new[] { monster }, playerModifier: 1.5f);

            Assert.Greater(evalBuffed.TotalDungeonDefensePower, evalNormal.TotalDungeonDefensePower);
            Assert.AreEqual(1.5f, evalBuffed.PlayerModifier);
        }

        [Test]
        public void DifficultyTier_Evaluation_CategorizesCorrectly()
        {
            // Massive defense setup (Trivial)
            var army = new List<IMonsterStats>();
            for (int i = 0; i < 10; i++)
            {
                army.Add(MonsterStats.CreateForRank(DungeonRank.S));
            }

            var evalTrivial = DifficultyScaler.CalculateDifficulty(DungeonRank.E, army);
            Assert.AreEqual(DifficultyTier.Trivial, evalTrivial.ChallengeTier);
            Assert.GreaterOrEqual(evalTrivial.DifficultyBalanceRatio, 1.6f);
        }
    }
}
