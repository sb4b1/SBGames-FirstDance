using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SBGames.Tests
{
    [TestFixture]
    public class ScoutReconnaissanceSystemTests
    {
        private GameObject leaderGo;
        private GameObject scoutGo;
        private GameObject warriorGo;
        private GameObject enemyGo;

        private PartyLeader leader;
        private SquadMember scoutMember;
        private SquadMember warriorMember;
        private BaseAI scoutAI;
        private BaseAI warriorAI;
        private BaseAI leaderAI;
        private CharacterAIMemory scoutMemory;

        [SetUp]
        public void SetUp()
        {
            leaderGo = new GameObject("Leader");
            scoutGo = new GameObject("Scout");
            warriorGo = new GameObject("Warrior");
            enemyGo = new GameObject("Enemy");

            leader = leaderGo.AddComponent<PartyLeader>();
            leaderAI = leaderGo.AddComponent<BaseAI>();
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();

            scoutMember = scoutGo.AddComponent<SquadMember>();
            scoutAI = scoutGo.AddComponent<BaseAI>();
            scoutMemory = scoutGo.AddComponent<CharacterAIMemory>();
            var scoutStats = scoutGo.AddComponent<CharacterStatsManager>();

            warriorMember = warriorGo.AddComponent<SquadMember>();
            warriorAI = warriorGo.AddComponent<BaseAI>();
            var warriorStats = warriorGo.AddComponent<CharacterStatsManager>();

            // Setup CharacterBaseStats SO
            var leaderBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBaseStat.characterClass = CharacterClass.Warrior;
            leaderStats.SetBaseStats(leaderBaseStat);

            var warriorBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            warriorBaseStat.characterClass = CharacterClass.Warrior;
            warriorStats.SetBaseStats(warriorBaseStat);

            var scoutBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            scoutBaseStat.characterClass = CharacterClass.Scout;
            scoutStats.SetBaseStats(scoutBaseStat);

            leader.RegisterMember(scoutMember);
            leader.RegisterMember(warriorMember);
        }

        [TearDown]
        public void TearDown()
        {
            if (leaderGo != null) Object.DestroyImmediate(leaderGo);
            if (scoutGo != null) Object.DestroyImmediate(scoutGo);
            if (warriorGo != null) Object.DestroyImmediate(warriorGo);
            if (enemyGo != null) Object.DestroyImmediate(enemyGo);
        }

        [Test]
        public void CharacterClass_IdentifiesScoutAutomatically()
        {
            Assert.AreEqual(CharacterClass.Scout, scoutMember.StatsManager.CharacterClass);
            Assert.IsTrue(scoutMember.IsScout, "SquadMember should identify as Scout based on CharacterClass.");
            Assert.IsFalse(warriorMember.IsScout, "Warrior should not identify as Scout.");
        }

        [Test]
        public void PartyLeader_DispatchScout_PutsSquadInStandbyAndScoutInScoutAhead()
        {
            bool dispatched = leader.DispatchScout();
            Assert.IsTrue(dispatched, "Leader should successfully dispatch living scout.");
            Assert.AreEqual(scoutMember, leader.ActiveScout);
            Assert.IsTrue(scoutMember.IsScouting);

            // Fellow members should be on Standby
            Assert.AreEqual(SquadOrderType.Standby, leader.Blackboard.CurrentOrder);
            Assert.AreEqual(SquadOrderType.Standby, warriorMember.CurrentOrder);

            // Scout order should be ScoutAhead
            Assert.AreEqual(SquadOrderType.ScoutAhead, scoutMember.CurrentOrder);
        }

        [Test]
        public void Scout_DuringRecon_CachesHazardsAndEnemiesLocallyWithoutSquadSync()
        {
            leader.DispatchScout();

            Vector2Int trapGrid = new Vector2Int(5, 5);
            Vector3 trapWorld = new Vector3(5f, 5f, 0f);

            // Scout detects hazard during recon
            scoutMemory.RecordHazard(trapGrid, trapWorld, dangerLevel: 50f, duration: 30f);

            // Scout detects enemy during recon
            scoutAI.ReceiveAlert(enemyGo.transform, enemyGo.transform.position, 6f);

            // 1. Scout has local memory
            Assert.AreEqual(1, scoutMemory.HazardCount);
            Assert.IsTrue(scoutMemory.IsPositionHazardous(trapWorld, 1.2f));
            Assert.AreEqual(1, scoutAI.LocalKnownEnemies.Count);

            // 2. Leader and Squad Blackboard have NOT received intelligence yet (Fog of War)
            Assert.AreEqual(0, leader.Blackboard.SharedHazards.Count, "Squad blackboard must not receive hazards while scout is in infiltration mode.");
            Assert.AreEqual(0, leader.Blackboard.KnownEnemies.Count, "Squad blackboard must not receive enemies while scout is in infiltration mode.");
            Assert.IsFalse(leader.Blackboard.IsPositionHazardous(trapWorld, 1.2f));
        }

        [Test]
        public void Scout_DebriefLeader_TransfersLocalRecordsToSquadBlackboardAndResumesMarch()
        {
            leader.DispatchScout();

            Vector2Int trapGrid = new Vector2Int(7, 7);
            Vector3 trapWorld = new Vector3(7f, 7f, 0f);

            scoutMemory.RecordHazard(trapGrid, trapWorld, dangerLevel: 80f, duration: 30f);
            scoutAI.ReceiveAlert(enemyGo.transform, enemyGo.transform.position, 6f);

            // Scout arrives near leader and debriefs
            leader.SynchronizeScoutIntel(scoutMember);

            // Intelligence synchronized to SquadBlackboard
            Assert.AreEqual(1, leader.Blackboard.SharedHazards.Count, "Squad blackboard should receive synchronized hazards.");
            Assert.AreEqual(1, leader.Blackboard.KnownEnemies.Count, "Squad blackboard should receive synchronized enemies.");
            Assert.IsTrue(leader.Blackboard.IsPositionHazardous(trapWorld, 1.2f));

            // Scout is no longer scouting and squad resumes march (FollowLeader)
            Assert.IsFalse(scoutMember.IsScouting);
            Assert.IsNull(leader.ActiveScout);
            Assert.AreEqual(SquadOrderType.FollowLeader, leader.Blackboard.CurrentOrder);
            Assert.AreEqual(SquadOrderType.FollowLeader, warriorMember.CurrentOrder);
        }

        [Test]
        public void ScoutEliminated_DestroysIntel_CausesFearSurgeAndResumesBlindMarch()
        {
            leader.DispatchScout();

            Vector2Int trapGrid = new Vector2Int(10, 10);
            Vector3 trapWorld = new Vector3(10f, 10f, 0f);

            scoutMemory.RecordHazard(trapGrid, trapWorld, dangerLevel: 90f, duration: 30f);
            scoutAI.ReceiveAlert(enemyGo.transform, enemyGo.transform.position, 6f);

            int warriorInitialFear = warriorAI.FearLevel;

            // Scout eliminated before reaching leader
            scoutMember.StatsManager.ForceDeath();

            // Intelligence is permanently destroyed with the unit
            Assert.AreEqual(0, leader.Blackboard.SharedHazards.Count, "Squad must receive NO hazard intelligence upon scout death.");
            Assert.AreEqual(0, leader.Blackboard.KnownEnemies.Count, "Squad must receive NO enemy intelligence upon scout death.");

            // Fear surge applied (+35 fear)
            Assert.AreEqual(warriorInitialFear + 35, warriorAI.FearLevel, "Squad members should experience a fear surge.");

            // During panic window, squad is still in Standby
            Assert.AreEqual(SquadOrderType.Standby, leader.Blackboard.CurrentOrder);

            // Trigger panic completion routine directly
            leader.CompleteScoutPanicForTesting();

            // After panic window, squad resumes march blindly
            Assert.AreEqual(SquadOrderType.FollowLeader, leader.Blackboard.CurrentOrder, "Squad should resume march blindly into the fog of war.");
        }

        [Test]
        public void UtilityGoals_ScoutReconAndDebrief_ScoreCorrectlyBasedOnState()
        {
            var reconGoal = ScriptableObject.CreateInstance<UtilityScoutReconGoal>();
            var debriefGoal = ScriptableObject.CreateInstance<UtilityDebriefGoal>();

            // Case 1: Normal state without scout order -> both score 0
            leader.ClearOrder();
            Assert.AreEqual(0f, reconGoal.Evaluate(scoutAI));
            Assert.AreEqual(0f, debriefGoal.Evaluate(scoutAI));

            // Case 2: Scout dispatched, no intel found yet -> ReconGoal scores > 0
            leader.DispatchScout();
            Assert.Greater(reconGoal.Evaluate(scoutAI), 0f, "ScoutReconGoal should evaluate > 0 when dispatched.");
            Assert.AreEqual(0f, debriefGoal.Evaluate(scoutAI), "DebriefGoal should be 0 when nothing has been detected yet.");

            // Case 3: Intel found -> DebriefGoal scores high, ReconGoal scores 0
            scoutMemory.RecordHazard(new Vector2Int(1, 1), Vector3.one, 50f, 30f);
            Assert.AreEqual(0f, reconGoal.Evaluate(scoutAI), "ReconGoal should score 0 once intel is discovered.");
            Assert.Greater(debriefGoal.Evaluate(scoutAI), 0.9f, "DebriefGoal should score high priority when intel is ready to debrief.");

            Object.DestroyImmediate(reconGoal);
            Object.DestroyImmediate(debriefGoal);
        }

        [Test]
        public void DoorwayStagingController_TriggersSlowdownAndStagingState()
        {
            var stagingController = scoutGo.AddComponent<SBGames.Characters.AI.DoorwayStagingController>();
            Assert.IsFalse(stagingController.IsStaging);

            stagingController.StartStaging();
            Assert.IsTrue(stagingController.IsStaging, "Controller should be staging upon trigger.");

            stagingController.EndStaging();
            Assert.IsFalse(stagingController.IsStaging, "Controller should exit staging after completion.");
        }

        [Test]
        public void HumanLikeExplorationSensor_RejectsHazardCoordinatesAndAppliesFogBonus()
        {
            var sensor = new SBGames.GOAP.Sensors.HumanLikeExplorationSensor();

            // Candidate position
            Vector3 testFrontier = new Vector3(8f, 0f, 0f);

            // 1. Initial state: unexplored factor is 1.0
            float unexploredFactor = scoutMemory.GetUnexploredFactor(testFrontier);
            Assert.AreEqual(1.0f, unexploredFactor, 0.001f);

            // 2. Mark visited
            scoutMemory.RecordVisitedPosition(testFrontier);
            float visitedFactor = scoutMemory.GetUnexploredFactor(testFrontier);
            Assert.AreEqual(0.1f, visitedFactor, 0.001f, "Visited positions should receive diminished unexplored factor.");

            // 3. Mark hazard
            scoutMemory.RecordHazard(new Vector2Int(8, 0), testFrontier, 80f, 30f);
            Assert.IsTrue(scoutMemory.IsPositionHazardous(testFrontier, 1.2f));
        }
    }
}
