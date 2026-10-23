using NUnit.Framework;
using SB4b1.GameMath;
using SBGames.Character.AI;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class FightOutlookTests
    {
        [Test]
        public void OutlookCalculation_WinningWhenTtkLowerThanTtd()
        {
            var input = new FightOutlookCalculator.CalculationInput
            {
                SelfLife = 100f,
                SelfMaxLife = 100f,
                SelfDps = 25f,
                TargetLife = 50f, // TTK = 50 / 25 = 2.0s
                TargetMaxLife = 50f,
                NearbyAlliesDps = 0f,
                IncomingEnemiesDps = 10f, // TTD = 100 / 10 = 10.0s
                CourageBias = 0f,
                FunctionTolerance = 1.0f,
                RankTtkNoiseFactor = 0f,
                RankTtdNoiseFactor = 0f,
                IsHardSafetyTrigger = false
            };

            var result = FightOutlookCalculator.Calculate(input);

            Assert.IsTrue(result.IsWinningOrEven);
            Assert.IsFalse(result.IsLosing);
            Assert.Greater(result.NormalizedOutlook, 0.5f);
        }

        [Test]
        public void OutlookCalculation_LosingWhenTtdLowerThanTtk()
        {
            var input = new FightOutlookCalculator.CalculationInput
            {
                SelfLife = 30f,
                SelfMaxLife = 100f,
                SelfDps = 10f,
                TargetLife = 100f, // TTK = 100 / 10 = 10.0s
                TargetMaxLife = 100f,
                NearbyAlliesDps = 0f,
                IncomingEnemiesDps = 30f, // TTD = 30 / 30 = 1.0s
                CourageBias = 0f,
                FunctionTolerance = 1.0f,
                RankTtkNoiseFactor = 0f,
                RankTtdNoiseFactor = 0f,
                IsHardSafetyTrigger = false
            };

            var result = FightOutlookCalculator.Calculate(input);

            Assert.IsTrue(result.IsLosing);
            Assert.IsFalse(result.IsWinningOrEven);
            Assert.Less(result.NormalizedOutlook, -0.2f);
        }

        [Test]
        public void SeededReproducibility_RankENoise()
        {
            // Two GameRng instances seeded identically produce identical sequence
            uint seed = 987654321u;
            var rng1 = new GameRng(seed);
            var rng2 = new GameRng(seed);

            float noise1A = rng1.Range(-0.4f, 0.4f);
            float noise1B = rng1.Range(-0.4f, 0.4f);

            float noise2A = rng2.Range(-0.4f, 0.4f);
            float noise2B = rng2.Range(-0.4f, 0.4f);

            Assert.AreEqual(noise1A, noise2A, 0.00001f);
            Assert.AreEqual(noise1B, noise2B, 0.00001f);
        }

        [Test]
        public void HardSafetyTrigger_SuppressesRankNoise()
        {
            var inputWithNoise = new FightOutlookCalculator.CalculationInput
            {
                SelfLife = 15f, // Critical HP
                SelfMaxLife = 100f,
                SelfDps = 20f,
                TargetLife = 80f,
                TargetMaxLife = 100f,
                NearbyAlliesDps = 0f,
                IncomingEnemiesDps = 15f,
                CourageBias = 0f,
                FunctionTolerance = 1.0f,
                RankTtkNoiseFactor = 0.40f, // Rank E high misjudgment noise
                RankTtdNoiseFactor = -0.40f,
                IsHardSafetyTrigger = true // Hard safety active!
            };

            var result = FightOutlookCalculator.Calculate(inputWithNoise);

            // Estimated TTK and TTD should equal the raw un-noised values (80/20 = 4s, 15/15 = 1s)
            Assert.AreEqual(4.0f, result.EstimatedTtk, 0.01f);
            Assert.AreEqual(1.0f, result.EstimatedTtd, 0.01f);
        }

        [Test]
        public void TankTolerance_ToleratesWorseOutlookThanHealer()
        {
            var tuning = FightOutlookTuningSO.CreateRuntimeInstance();

            var tankInput = new FightOutlookCalculator.CalculationInput
            {
                SelfLife = 60f,
                SelfMaxLife = 150f,
                SelfDps = 15f,
                TargetLife = 80f,
                TargetMaxLife = 80f,
                NearbyAlliesDps = 0f,
                IncomingEnemiesDps = 20f,
                CourageBias = 0f,
                FunctionTolerance = tuning.TankOutlookTolerance, // 1.5x
                RankTtkNoiseFactor = 0f,
                RankTtdNoiseFactor = 0f,
                IsHardSafetyTrigger = false
            };

            var healerInput = new FightOutlookCalculator.CalculationInput
            {
                SelfLife = 60f,
                SelfMaxLife = 150f,
                SelfDps = 15f,
                TargetLife = 80f,
                TargetMaxLife = 80f,
                NearbyAlliesDps = 0f,
                IncomingEnemiesDps = 20f,
                CourageBias = 0f,
                FunctionTolerance = tuning.HealerOutlookTolerance, // 0.65x
                RankTtkNoiseFactor = 0f,
                RankTtdNoiseFactor = 0f,
                IsHardSafetyTrigger = false
            };

            var tankResult = FightOutlookCalculator.Calculate(tankInput, tuning);
            var healerResult = FightOutlookCalculator.Calculate(healerInput, tuning);

            Assert.Greater(tankResult.NormalizedOutlook, healerResult.NormalizedOutlook);
        }
    }
}
