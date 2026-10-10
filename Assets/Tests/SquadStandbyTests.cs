using NUnit.Framework;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class SquadStandbyTests
    {
        private GameObject _holderGo;
        private GameObject _leaderGo;

        [SetUp]
        public void SetUp()
        {
            _holderGo = new GameObject("Test_SquadMember");
            _leaderGo = new GameObject("Test_PartyLeader");
        }

        [TearDown]
        public void TearDown()
        {
            if (_holderGo != null) Object.DestroyImmediate(_holderGo);
            if (_leaderGo != null) Object.DestroyImmediate(_leaderGo);
        }

        [Test]
        public void PartyLeader_OrderStandby_SetsBlackboardOrderToStandby()
        {
            var leader = _leaderGo.AddComponent<PartyLeader>();
            var member = _holderGo.AddComponent<SquadMember>();
            var ai = _holderGo.AddComponent<BaseAI>();

            leader.RegisterMember(member);
            leader.OrderStandby();

            Assert.AreEqual(SquadOrderType.Standby, ai.SquadOrder, "BaseAI should reflect SquadOrderType.Standby when leader orders Standby.");
            Assert.AreEqual(SquadOrderType.Standby, leader.Blackboard.CurrentOrder);
        }

        [Test]
        public void SquadCapabilityFactory_RegistersStandbyGoalAndIdleAction()
        {
            var factory = new SquadCapabilityFactory();
            var config = factory.Create();

            Assert.IsNotNull(config, "Squad capability config should not be null.");

            Assert.AreEqual(4, config.Goals.Count, "Should register 4 goals (FollowLeaderGoal, FocusFireGoal, FallBackGoal, StandbyGoal).");
            Assert.AreEqual(4, config.Actions.Count, "Should register 4 actions (FollowLeaderAction, FocusFireAction, FallBackAction, IdleAction).");
        }

        [Test]
        public void UtilityStandbyGoal_EvaluatesHighPriorityOnStandbyOrder_AndZeroOtherwise()
        {
            var goal = ScriptableObject.CreateInstance<UtilityStandbyGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var member = _holderGo.AddComponent<SquadMember>();
            var leader = _leaderGo.AddComponent<PartyLeader>();

            leader.RegisterMember(member);

            // Case 1: No order (None) -> 0
            leader.ClearOrder();
            Assert.AreEqual(0f, goal.Evaluate(ai), "Standby goal should score 0 when no standby order is active.");

            // Case 2: Standby order -> 0.95f
            leader.OrderStandby();
            float standbyScore = goal.Evaluate(ai);
            Assert.AreEqual(0.95f, standbyScore, 0.001f, "Standby goal should evaluate to 0.95f on Standby order.");

            // Case 3: Other orders (e.g. FollowLeader) -> 0
            leader.OrderFollowLeader();
            Assert.AreEqual(0f, goal.Evaluate(ai), "Standby goal should score 0 on FollowLeader order.");

            // Case 4: No leader -> 0
            leader.UnregisterMember(member);
            Assert.AreEqual(0f, goal.Evaluate(ai), "Standby goal should score 0 when unit has no leader.");

            Object.DestroyImmediate(goal);
        }

        [Test]
        public void StandbyOrder_SuppressesKillEnemyAndWanderGoals()
        {
            var killGoal = ScriptableObject.CreateInstance<UtilityKillEnemyGoal>();
            var wanderGoal = ScriptableObject.CreateInstance<UtilityWanderGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var member = _holderGo.AddComponent<SquadMember>();
            var leader = _leaderGo.AddComponent<PartyLeader>();

            leader.RegisterMember(member);
            ai.SenseEnemy(); // Threat > 0

            // Normal without standby order: kill goal is > 0
            leader.ClearOrder();
            Assert.Greater(killGoal.Evaluate(ai), 0f, "KillEnemy should score > 0 under threat when not in Standby.");

            // Standby active: both kill and wander must be 0
            leader.OrderStandby();
            Assert.AreEqual(0f, killGoal.Evaluate(ai), "KillEnemy goal must be suppressed to 0 during Standby.");
            Assert.AreEqual(0f, wanderGoal.Evaluate(ai), "Wander goal must be suppressed to 0 during Standby.");

            Object.DestroyImmediate(killGoal);
            Object.DestroyImmediate(wanderGoal);
        }

        [Test]
        public void UtilitySurviveGoal_CriticalHealth_ExceedsStandbyPriority()
        {
            var surviveGoal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();
            var standbyGoal = ScriptableObject.CreateInstance<UtilityStandbyGoal>();
            var ai = _holderGo.AddComponent<BaseAI>();
            var member = _holderGo.AddComponent<SquadMember>();
            var leader = _leaderGo.AddComponent<PartyLeader>();

            leader.RegisterMember(member);
            leader.OrderStandby();

            float standbyScore = standbyGoal.Evaluate(ai);
            Assert.AreEqual(0.95f, standbyScore, 0.001f);

            // Critical health: 1 HP (with default threshold 0.25 on 100 max HP)
            ai.SetAgentLife(1f);
            float survivalScore = surviveGoal.Evaluate(ai);

            Assert.Greater(survivalScore, standbyScore, "Critical low health survival score must exceed Standby priority (0.95f) for emergency escape.");

            Object.DestroyImmediate(surviveGoal);
            Object.DestroyImmediate(standbyGoal);
        }
    }
}
