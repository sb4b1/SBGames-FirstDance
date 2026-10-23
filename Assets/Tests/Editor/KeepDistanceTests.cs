using SBGames.Database;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Character;
using SBGames.Character.AI;
using SBGames.Database.Editor.CSV;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class KeepDistanceTests
    {
        private GameObject _go;
        private BaseAI _ai;
        private CharacterStatsManager _stats;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Test_KeepDistanceUnit");
            _stats = _go.AddComponent<CharacterStatsManager>();
            _ai = _go.AddComponent<BaseAI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void AutoOnAndOff_ResolutionFromRange()
        {
            // Range 1.0m (melee) -> Auto resolves to false
            Assert.IsFalse(KeepDistanceResolver.ResolveIsEnabled(KeepDistanceMode.Auto, 1.0f, 3.0f));

            // Range 5.0m (ranged) -> Auto resolves to true
            Assert.IsTrue(KeepDistanceResolver.ResolveIsEnabled(KeepDistanceMode.Auto, 5.0f, 3.0f));

            // On mode -> always true even for 1.0m
            Assert.IsTrue(KeepDistanceResolver.ResolveIsEnabled(KeepDistanceMode.On, 1.0f, 3.0f));

            // Off mode -> always false even for 10.0m
            Assert.IsFalse(KeepDistanceResolver.ResolveIsEnabled(KeepDistanceMode.Off, 10.0f, 3.0f));
        }

        [Test]
        public void ReferenceRange_DerivationForCasterWithoutBasicAttack()
        {
            // BasicAttackMode None with skill range 7.5m derives refRange 7.5m
            float refRange = KeepDistanceResolver.ResolveReferenceRange(1.0f, BasicAttackMode.None, 7.5f);
            Assert.AreEqual(7.5f, refRange, 0.001f);

            // BasicAttackMode Auto with attack range 5.0m uses attack range 5.0m
            float normalRefRange = KeepDistanceResolver.ResolveReferenceRange(5.0f, BasicAttackMode.Auto, 2.0f);
            Assert.AreEqual(5.0f, normalRefRange, 0.001f);
        }

        [Test]
        public void HysteresisDistances_StartAndStopBoundaries()
        {
            var tuning = KeepDistanceTuningSO.CreateRuntimeInstance(3.0f, 0.6f, 0.9f);
            float refRange = 5.0f;

            KeepDistanceResolver.CalculateHysteresisDistances(refRange, 0f, tuning, out float startDist, out float stopDist);

            // 0.6 * 5.0 = 3.0m
            Assert.AreEqual(3.0f, startDist, 0.01f);
            // 0.9 * 5.0 = 4.5m
            Assert.AreEqual(4.5f, stopDist, 0.01f);

            // Comfort distance override takes precedence as startDist
            KeepDistanceResolver.CalculateHysteresisDistances(refRange, 4.0f, tuning, out float customStart, out float customStop);
            Assert.AreEqual(4.0f, customStart, 0.01f);
            Assert.AreEqual(6.0f, customStop, 0.01f); // 4.0 * (0.9 / 0.6) = 6.0m
        }

        [Test]
        public void CharactersCsv_KeepDistanceFieldsRoundTrip()
        {
            var rowDict = new Dictionary<string, string>
            {
                ["Id"] = "TEST_ARCHER",
                ["KeepDistanceMode"] = "On",
                ["ComfortDistanceOverride"] = "4.2",
                ["BasicAttackMode"] = "Auto"
            };

            var parsedMode = DatabaseCsvPipeline.ParseKeepDistanceMode(rowDict);
            var parsedOverride = DatabaseCsvPipeline.ParseComfortDistanceOverride(rowDict);

            Assert.AreEqual(KeepDistanceMode.On, parsedMode);
            Assert.AreEqual(4.2f, parsedOverride, 0.01f);

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            DatabaseCsvPipeline.ApplyCharacterCsvRowToStats(rowDict, baseStat);

            Assert.AreEqual(KeepDistanceMode.On, baseStat.keepDistanceMode);
            Assert.AreEqual(4.2f, baseStat.comfortDistanceOverride, 0.01f);

            var characterEntry = new CharacterEntry
            {
                id = "TEST_ARCHER",
                baseStats = baseStat
            };

            var exportedRow = DatabaseCsvPipeline.BuildCharacterCsvRow(characterEntry);
            Assert.AreEqual("On", exportedRow["KeepDistanceMode"]);
            Assert.AreEqual("4.2", exportedRow["ComfortDistanceOverride"]);
        }
    }
}
