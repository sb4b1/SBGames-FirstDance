using NUnit.Framework;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class GOAPAndUtilityAITests
    {
        private GameObject _holderGo;

        [SetUp]
        public void SetUp()
        {
            _holderGo = new GameObject("Test_GOAPHolder");
        }

        [TearDown]
        public void TearDown()
        {
            if (_holderGo != null)
            {
                Object.DestroyImmediate(_holderGo);
            }
        }

        [Test]
        public void SurvivalCapabilityFactory_CreatesValidConfig()
        {
            var factory = new SurvivalCapabilityFactory();
            var config = factory.Create();

            Assert.IsNotNull(config, "Survival capability config should not be null.");
            Assert.AreEqual(2, config.Goals.Count, "Should register SurviveGoal and KeepDistanceGoal.");
            Assert.AreEqual(1, config.Actions.Count, "Should register FleeAction.");
        }

        [Test]
        public void BasicCapabilityFactory_DoesNotContainDuplicateKillEnemyGoal()
        {
            var factory = new BasicCapabilityFactory();
            var config = factory.Create();

            Assert.IsNotNull(config);
            Assert.AreEqual(0, config.Goals.Count, "BasicCapability should only contain shared perception sensors, no duplicate goals.");
        }

        [Test]
        public void BaseAI_SetTarget_UpdatesTargetDistanceAccurately()
        {
            var ai = _holderGo.AddComponent<BaseAI>();
            var targetGo = new GameObject("Test_Target");
            targetGo.transform.position = new Vector3(3f, 4f, 0f); // 3-4-5 triangle -> distance 5

            ai.SetTarget(targetGo.transform);

            Assert.AreEqual(targetGo.transform, ai.CurrentTarget);
            Assert.AreEqual(5f, ai.TargetDistance, 0.001f);

            ai.SetTarget(null);
            Assert.IsNull(ai.CurrentTarget);
            Assert.AreEqual(0f, ai.TargetDistance, 0.001f);

            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void BaseAI_UpdateTargets_CleansUpGhostThreatWhenNoEnemiesPresent()
        {
            var ai = _holderGo.AddComponent<BaseAI>();

            // Simulate enemy detection
            ai.SenseEnemy();
            Assert.Greater(ai.ThreatLevel, 0);

            // Update with no targets in perception (enemies despawned or destroyed)
            ai.UpdateTargets();

            Assert.AreEqual(0, ai.ThreatLevel, "Threat level should reset to 0 when no enemies are in perception.");
        }
    }
}
