using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Personality;
using SBGames.Personality.Data;
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

        #region Utility Goal Boundary & Fuzzing Tests

        [Test]
        public void UtilityKillEnemyGoal_BoundaryEvaluations_NeverProduceInvalidScores()
        {
            var goal = ScriptableObject.CreateInstance<UtilityKillEnemyGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();

            // Boundary 1: Zero threat -> exactly 0
            Assert.AreEqual(0f, goal.Evaluate(ai));

            // Boundary 2: Max threat, 0 fear -> high score clamped <= 1.0
            ai.SenseEnemy();
            for (int i = 0; i < 20; i++) ai.SenseEnemy(); // Push threat high
            float highThreatScore = goal.Evaluate(ai);
            Assert.Greater(highThreatScore, 0f);
            Assert.LessOrEqual(highThreatScore, 1f);

            // Fuzzing boundary: threat level extremes
            for (int threat = -50; threat <= 200; threat += 25)
            {
                var so = new UnityEditor.SerializedObject(ai);
                so.FindProperty("threatLevel").intValue = threat;
                so.ApplyModifiedPropertiesWithoutUndo();

                float score = goal.Evaluate(ai);
                Assert.IsTrue(score >= 0f && score <= 1f, $"Score {score} at threat {threat} must stay strictly within [0, 1].");
            }

            Object.DestroyImmediate(goal);
        }

        [Test]
        public void UtilitySurviveGoal_HealthAndFearMatrix_EvaluatesDeterministically()
        {
            var goal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();

            // Scenario 1: Full health, no fear -> score 0
            ai.SetAgentLife(100f);
            Assert.AreEqual(0f, goal.Evaluate(ai), "Healthy agent with low fear should score 0 on survival.");

            // Scenario 2: Critical health (5 HP out of 100 with default 25% threshold) -> high urgency score
            ai.SetAgentLife(5f);
            float lowHpScore = goal.Evaluate(ai);
            Assert.Greater(lowHpScore, 0.4f, "Low HP below threshold should score significant survival urgency.");
            Assert.LessOrEqual(lowHpScore, 1f);

            // Scenario 3: Fuzzing Life values from -10 to 150
            for (float hp = -10f; hp <= 150f; hp += 15f)
            {
                ai.SetAgentLife(hp);
                float score = goal.Evaluate(ai);
                Assert.IsTrue(score >= 0f && score <= 1f, $"Score {score} at HP {hp} must be clamped to [0, 1].");
            }

            Object.DestroyImmediate(goal);
        }

        [Test]
        public void UtilityWanderGoal_EvaluatesOnlyWhenCompletelySafe()
        {
            var goal = ScriptableObject.CreateInstance<UtilityWanderGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();

            // When no threat or fear -> positive wander score
            Assert.Greater(goal.Evaluate(ai), 0f, "Should wander when idle and safe.");

            // Under threat -> strictly 0
            ai.SenseEnemy();
            Assert.AreEqual(0f, goal.Evaluate(ai), "Must not wander while threatened.");

            Object.DestroyImmediate(goal);
        }

        [Test]
        public void UtilityFollowLeader_SquadOrderDominatesLeashDistance()
        {
            var goal = ScriptableObject.CreateInstance<UtilityFollowLeaderGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var squadMember = _holderGo.AddComponent<SquadMember>();

            var leaderGo = new GameObject("Test_Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();

            leader.RegisterMember(squadMember);

            // Explicit order: FollowLeader -> returns base explicit weight (~0.85f)
            leader.OrderFollowLeader();
            float orderScore = goal.Evaluate(ai);
            Assert.AreEqual(0.85f, orderScore, 0.001f);

            // When order is None and within leash distance -> 0
            leader.ClearOrder();
            ai.transform.position = Vector3.zero;
            leaderGo.transform.position = new Vector3(1f, 0f, 0f); // 1m away, well within leash (6m)
            Assert.AreEqual(0f, goal.Evaluate(ai), "Should not pull to follow leader when within leash distance.");

            // When drifting far (20m away) -> tension curve score activates
            leaderGo.transform.position = new Vector3(20f, 0f, 0f);
            float driftScore = goal.Evaluate(ai);
            Assert.Greater(driftScore, 0.5f, "Leash tension should score high when drifting 20m from leader.");
            Assert.LessOrEqual(driftScore, 1f);

            Object.DestroyImmediate(leaderGo);
            Object.DestroyImmediate(goal);
        }

        [Test]
        public void UtilityFocusFire_EvaluatesOnlyWhenTargetAndOrderAreValid()
        {
            var goal = ScriptableObject.CreateInstance<UtilityFocusFireGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var squadMember = _holderGo.AddComponent<SquadMember>();

            var leaderGo = new GameObject("Test_Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();
            var targetGo = new GameObject("Test_FocusTarget");

            leader.RegisterMember(squadMember);

            // Without order -> 0
            Assert.AreEqual(0f, goal.Evaluate(ai));

            // With FocusFire order but no target -> 0
            leader.OrderFocusFire(null);
            Assert.AreEqual(0f, goal.Evaluate(ai));

            // With FocusFire order and valid target -> high score
            leader.OrderFocusFire(targetGo.transform);
            float score = goal.Evaluate(ai);
            Assert.Greater(score, 0.8f, "Focus fire with valid target should score high.");
            Assert.LessOrEqual(score, 1f);

            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(leaderGo);
            Object.DestroyImmediate(goal);
        }

        [Test]
        public void UtilityFallBack_EvaluatesFullScoreOnCommand()
        {
            var goal = ScriptableObject.CreateInstance<UtilityFallBackGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var squadMember = _holderGo.AddComponent<SquadMember>();

            var leaderGo = new GameObject("Test_Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();
            leader.RegisterMember(squadMember);

            Assert.AreEqual(0f, goal.Evaluate(ai));

            leader.OrderFallBack();
            Assert.AreEqual(1.0f, goal.Evaluate(ai), 0.001f);

            Object.DestroyImmediate(leaderGo);
            Object.DestroyImmediate(goal);
        }

        #endregion

        #region EnemyTargetSensor Edge-Case Stress Tests

        [Test]
        public void EnemyTargetSensor_SelectsClosestValidTarget_IgnoringDestroyedTransforms()
        {
            var sensor = new EnemyTargetSensor();
            var ai = _holderGo.AddComponent<BaseAI>();

            var t1 = new GameObject("Enemy_1").transform;
            var t2 = new GameObject("Enemy_2").transform;
            var t3 = new GameObject("Enemy_3").transform;

            t1.position = new Vector3(10f, 0f, 0f); // 10 units away
            t2.position = new Vector3(2f, 0f, 0f);  // 2 units away (closest)
            t3.position = new Vector3(5f, 0f, 0f);  // 5 units away

            ai.EnemyTargets = new System.Collections.Generic.List<Transform> { t1, t2, t3 };

            // Mock agent action receiver
            var agentGo = new GameObject("Test_Agent");
            var receiver = agentGo.AddComponent<CrashKonijn.Agent.Runtime.AgentBehaviour>();

            // Destroy t2 (simulate enemy killed this tick)
            Object.DestroyImmediate(t2.gameObject);

            // Sense should safely skip destroyed t2 and pick t3 (5 units away, closest surviving)
            var target = sensor.Sense(receiver, null, null);

            Assert.IsNotNull(target);
            Assert.AreEqual(t3, ai.CurrentTarget, "Sensor must pick t3 as the closest non-destroyed target.");
            Assert.AreEqual(5f, ai.TargetDistance, 0.001f);

            Object.DestroyImmediate(t1.gameObject);
            Object.DestroyImmediate(t3.gameObject);
            Object.DestroyImmediate(agentGo);
        }

        #endregion

        #region Trait Execution Bias Stress Test

        [Test]
        public void BaseAI_GetTraitWeightedTargetScore_PrioritizesWoundedUnitsWithBloodthirsty()
        {
            var ai = _holderGo.AddComponent<BaseAI>();
            var personality = _holderGo.AddComponent<CharacterPersonality>();

            // Setup healthy enemy (100/100 HP) at distance 5
            var healthyGo = new GameObject("Enemy_Healthy");
            var healthyStats = healthyGo.AddComponent<CharacterStatsManager>();
            healthyGo.transform.position = new Vector3(5f, 0f, 0f);

            // Setup wounded enemy (20/100 HP, <=30% execution threshold) at distance 5
            var woundedGo = new GameObject("Enemy_Wounded");
            var woundedStats = woundedGo.AddComponent<CharacterStatsManager>();
            woundedStats.TakeDamage(new DamageData(80f, DamageType.True)); // 100 - 80 = 20 HP
            woundedGo.transform.position = new Vector3(5f, 0f, 0f);

            // Default personality: equal scores for equal distance
            float defaultScoreHealthy = ai.GetTraitWeightedTargetScore(healthyGo.transform, 5f);
            float defaultScoreWounded = ai.GetTraitWeightedTargetScore(woundedGo.transform, 5f);
            Assert.AreEqual(defaultScoreHealthy, defaultScoreWounded, 0.001f);

            // Trait setup: Bloodthirsty / Low Health priority
            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var so = new UnityEditor.SerializedObject(trait);
            so.FindProperty("lowHealthTargetPriority").floatValue = 2.5f;
            so.FindProperty("executionHealthThreshold").floatValue = 0.30f;
            so.ApplyModifiedPropertiesWithoutUndo();

            personality.Initialize(null, trait);

            float traitScoreHealthy = ai.GetTraitWeightedTargetScore(healthyGo.transform, 5f);
            float traitScoreWounded = ai.GetTraitWeightedTargetScore(woundedGo.transform, 5f);

            Assert.Greater(traitScoreWounded, traitScoreHealthy * 2.0f, "Bloodthirsty trait must significantly boost score for low-health target.");

            Object.DestroyImmediate(healthyGo);
            Object.DestroyImmediate(woundedGo);
            Object.DestroyImmediate(trait);
        }

        #endregion
    }
}
