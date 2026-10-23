using CrashKonijn.Goap.Runtime;
using SB4b1.GameMath;
using SBGames.Character;
using SBGames.Character.AI;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Characters.AI;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CombatSandboxRegressionTests
    {
        private GameObject leaderGo;
        private GameObject warriorGo;
        private GameObject rogueGo;
        private GameObject mageGo;
        private GameObject clericGo;
        private GameObject monsterGo;

        private PartyLeader leader;
        private SquadTacticalEvaluator evaluator;
        private BaseAI leaderAI;
        private BaseAI warriorAI;
        private BaseAI rogueAI;
        private BaseAI mageAI;
        private BaseAI clericAI;
        private BaseAI monsterAI;

        private CharacterStatsManager leaderStats;
        private CharacterStatsManager warriorStats;
        private CharacterStatsManager rogueStats;
        private CharacterStatsManager mageStats;
        private CharacterStatsManager clericStats;
        private CharacterStatsManager monsterStats;

        [SetUp]
        public void SetUp()
        {
            leaderGo = new GameObject("Leader_Warrior");
            warriorGo = new GameObject("Squad_Warrior");
            rogueGo = new GameObject("Squad_Rogue");
            mageGo = new GameObject("Squad_Mage");
            clericGo = new GameObject("Squad_Cleric");
            monsterGo = new GameObject("Monster_Goblin");

            leader = leaderGo.AddComponent<PartyLeader>();
            evaluator = leaderGo.AddComponent<SquadTacticalEvaluator>();
            leaderAI = leaderGo.AddComponent<BaseAI>();
            leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBrain = leaderGo.AddComponent<CharacterBrain>();
            var leaderCombat = leaderGo.AddComponent<CharacterCombatController>();
            var leaderFaction = leaderGo.AddComponent<FactionMember>();
            leaderFaction.Faction = FactionId.Adventurer;

            warriorAI = warriorGo.AddComponent<BaseAI>();
            warriorStats = warriorGo.AddComponent<CharacterStatsManager>();
            var warriorMember = warriorGo.AddComponent<SquadMember>();
            var warriorBrain = warriorGo.AddComponent<CharacterBrain>();
            var warriorCombat = warriorGo.AddComponent<CharacterCombatController>();
            var warriorFaction = warriorGo.AddComponent<FactionMember>();
            warriorFaction.Faction = FactionId.Adventurer;
            leader.RegisterMember(warriorMember);

            rogueAI = rogueGo.AddComponent<BaseAI>();
            rogueStats = rogueGo.AddComponent<CharacterStatsManager>();
            var rogueMember = rogueGo.AddComponent<SquadMember>();
            var rogueBrain = rogueGo.AddComponent<CharacterBrain>();
            var rogueCombat = rogueGo.AddComponent<CharacterCombatController>();
            var rogueFaction = rogueGo.AddComponent<FactionMember>();
            rogueFaction.Faction = FactionId.Adventurer;
            leader.RegisterMember(rogueMember);

            mageAI = mageGo.AddComponent<BaseAI>();
            mageStats = mageGo.AddComponent<CharacterStatsManager>();
            var mageMember = mageGo.AddComponent<SquadMember>();
            var mageBrain = mageGo.AddComponent<CharacterBrain>();
            var mageCombat = mageGo.AddComponent<CharacterCombatController>();
            var mageFaction = mageGo.AddComponent<FactionMember>();
            mageFaction.Faction = FactionId.Adventurer;
            leader.RegisterMember(mageMember);

            clericAI = clericGo.AddComponent<BaseAI>();
            clericStats = clericGo.AddComponent<CharacterStatsManager>();
            var clericMember = clericGo.AddComponent<SquadMember>();
            var clericBrain = clericGo.AddComponent<CharacterBrain>();
            var clericCombat = clericGo.AddComponent<CharacterCombatController>();
            var clericFaction = clericGo.AddComponent<FactionMember>();
            clericFaction.Faction = FactionId.Adventurer;
            leader.RegisterMember(clericMember);

            monsterAI = monsterGo.AddComponent<BaseAI>();
            monsterStats = monsterGo.AddComponent<CharacterStatsManager>();
            var monsterBrain = monsterGo.AddComponent<CharacterBrain>();
            var monsterCombat = monsterGo.AddComponent<CharacterCombatController>();
            var monsterFaction = monsterGo.AddComponent<FactionMember>();
            monsterFaction.Faction = FactionId.Monster;

            var warriorBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            warriorBaseStat.characterClass = CharacterClass.Warrior;
            warriorBaseStat.MaxLife = 100f;

            var rogueBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            rogueBaseStat.characterClass = CharacterClass.Rogue;
            rogueBaseStat.MaxLife = 80f;

            var mageBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            mageBaseStat.characterClass = CharacterClass.Mage;
            mageBaseStat.MaxLife = 60f;

            var clericBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            clericBaseStat.characterClass = CharacterClass.Cleric;
            clericBaseStat.MaxLife = 70f;

            var monsterBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            monsterBaseStat.characterClass = CharacterClass.Warrior;
            monsterBaseStat.MaxLife = 100f;

            leaderStats.SetBaseStats(warriorBaseStat);
            warriorStats.SetBaseStats(warriorBaseStat);
            rogueStats.SetBaseStats(rogueBaseStat);
            mageStats.SetBaseStats(mageBaseStat);
            clericStats.SetBaseStats(clericBaseStat);
            monsterStats.SetBaseStats(monsterBaseStat);

            // Configure positions: Adventurers grouped at origin, monster at (2, 0, 0)
            leaderGo.transform.position = Vector3.zero;
            warriorGo.transform.position = new Vector3(-0.5f, 0f, 0f);
            rogueGo.transform.position = new Vector3(-0.5f, 0.5f, 0f);
            mageGo.transform.position = new Vector3(-1f, 0f, 0f);
            clericGo.transform.position = new Vector3(-1f, -0.5f, 0f);
            monsterGo.transform.position = new Vector3(2f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (leaderGo != null) Object.DestroyImmediate(leaderGo);
            if (warriorGo != null) Object.DestroyImmediate(warriorGo);
            if (rogueGo != null) Object.DestroyImmediate(rogueGo);
            if (mageGo != null) Object.DestroyImmediate(mageGo);
            if (clericGo != null) Object.DestroyImmediate(clericGo);
            if (monsterGo != null) Object.DestroyImmediate(monsterGo);
        }

        [Test]
        public void PartyLeader_SpottedEnemy_PopulatesSquadBlackboard_AndSynchronizesToMembers()
        {
            // Leader spots monster directly
            leaderAI.SenseEnemy();
            leaderAI.EnemyTargets = new List<Transform> { monsterGo.transform };
            leaderAI.UpdateTargets();

            // When leader detects an enemy, it must be reported to the squad blackboard
            Assert.IsTrue(leader.Blackboard.HasKnownEnemies, "Leader detection must report enemy to squad blackboard.");
            Assert.GreaterOrEqual(leader.Blackboard.KnownEnemies.Count, 1, "Squad blackboard should have at least 1 known enemy.");

            // Squad members accessing KnownEnemies must see the blackboard record without needing their own direct vision
            Assert.IsTrue(warriorAI.HasKnownEnemies, "Warrior squad member must see blackboard known enemies.");
            Assert.IsTrue(rogueAI.HasKnownEnemies, "Rogue squad member must see blackboard known enemies.");
            Assert.IsTrue(mageAI.HasKnownEnemies, "Mage squad member must see blackboard known enemies.");
            Assert.IsTrue(clericAI.HasKnownEnemies, "Cleric squad member must see blackboard known enemies.");
        }

        [Test]
        public void PartyLeader_PerformAlertShout_AlertsAllSquadMembers_ElevatingThreat()
        {
            // Initial threat on members should be 0
            Assert.AreEqual(0, warriorAI.ThreatLevel);
            Assert.AreEqual(0, rogueAI.ThreatLevel);
            Assert.AreEqual(0, mageAI.ThreatLevel);

            // Leader alerts squad of target
            leaderAI.SetTarget(monsterGo.transform);
            leaderAI.PerformAlertShout();

            // All squad members should have received alert and elevated their threat readiness stance
            Assert.Greater(warriorAI.ThreatLevel, 0, "Warrior threat level should elevate after leader alert shout.");
            Assert.Greater(rogueAI.ThreatLevel, 0, "Rogue threat level should elevate after leader alert shout.");
            Assert.Greater(mageAI.ThreatLevel, 0, "Mage threat level should elevate after leader alert shout.");
            Assert.Greater(clericAI.ThreatLevel, 0, "Cleric threat level should elevate after leader alert shout.");
        }

        [Test]
        public void SquadTacticalEvaluator_WithKnownEnemy_IssuesFocusFireOrder_ToEntireSquad()
        {
            // Report enemy into leader blackboard
            leader.Blackboard.ReportEnemy(monsterGo.transform, monsterGo.transform.position, Time.time);

            // Evaluate tactics
            evaluator.EvaluateTactics(0.5f);

            // Blackboard order should now be FocusFire targeting monster
            Assert.AreEqual(SquadOrderType.FocusFire, leader.Blackboard.CurrentOrder, "Tactical evaluator must issue FocusFire order.");
            Assert.AreEqual(monsterGo.transform, leader.Blackboard.FocusFireTarget, "FocusFire target must match known enemy.");

            // All squad members must reflect FocusFire order and focus target
            Assert.AreEqual(SquadOrderType.FocusFire, warriorAI.SquadOrder);
            Assert.AreEqual(monsterGo.transform, warriorAI.FocusFireTarget);

            Assert.AreEqual(SquadOrderType.FocusFire, rogueAI.SquadOrder);
            Assert.AreEqual(monsterGo.transform, rogueAI.FocusFireTarget);

            Assert.AreEqual(SquadOrderType.FocusFire, mageAI.SquadOrder);
            Assert.AreEqual(monsterGo.transform, mageAI.FocusFireTarget);

            Assert.AreEqual(SquadOrderType.FocusFire, clericAI.SquadOrder);
            Assert.AreEqual(monsterGo.transform, clericAI.FocusFireTarget);
        }

        [Test]
        public void UtilityFocusFireGoal_ScoresHighForSquadMembers_WhenFocusFireOrderActive()
        {
            var goal = ScriptableObject.CreateInstance<UtilityFocusFireGoal>();

            // Before order: 0 score
            Assert.AreEqual(0f, goal.Evaluate(warriorAI));
            Assert.AreEqual(0f, goal.Evaluate(rogueAI));
            Assert.AreEqual(0f, goal.Evaluate(mageAI));

            // Leader orders FocusFire on monster
            leader.OrderFocusFire(monsterGo.transform);

            // Squad members evaluate FocusFire goal
            float warriorScore = goal.Evaluate(warriorAI);
            float rogueScore = goal.Evaluate(rogueAI);
            float mageScore = goal.Evaluate(mageAI);

            Assert.Greater(warriorScore, 0.8f, "Warrior should score high on FocusFire.");
            Assert.Greater(rogueScore, 0.8f, "Rogue should score high on FocusFire.");
            Assert.Greater(mageScore, 0.8f, "Mage should score high on FocusFire.");

            Object.DestroyImmediate(goal);
        }

        [Test]
        public void CombatSandbox_AllOffensiveUnits_DealDamageWithinAllocatedWindow()
        {
            // Simulate 5 combat turns where each offensive adventurer executes an attack
            var units = new[] { leaderGo, warriorGo, rogueGo, mageGo };
            var damageDealtMap = new Dictionary<GameObject, float>();
            foreach (var u in units) damageDealtMap[u] = 0f;

            for (int i = 0; i < units.Length; i++)
            {
                var attackerGo = units[i];
                var stats = attackerGo.GetComponent<CharacterStatsManager>();
                float atkDmg = 15f;

                // Attacker deals damage to monster
                var dmgData = new DamageData(atkDmg, DamageType.Physical, stats);
                monsterStats.TakeDamage(dmgData);
                damageDealtMap[attackerGo] += atkDmg;
            }

            // Every unit must have dealt > 0 damage
            foreach (var kvp in damageDealtMap)
            {
                Assert.Greater(kvp.Value, 0f, $"{kvp.Key.name} failed to deal damage within the combat window.");
            }
        }
        [Test]
        public void CombatSandbox_ArcherResumesFiring_AfterReachingStopDistance()
        {
            var archerGo = new GameObject("Squad_Archer");
            var archerAI = archerGo.AddComponent<BaseAI>();
            var archerStats = archerGo.AddComponent<CharacterStatsManager>();
            var archerBrain = archerGo.AddComponent<CharacterBrain>();
            var archerCombat = archerGo.AddComponent<CharacterCombatController>();
            var archerMember = archerGo.AddComponent<SquadMember>();
            leader.RegisterMember(archerMember);

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.basicAttackMode = BasicAttackMode.Auto;
            baseStat.AttackRange = 6.0f; // Ranged archer
            baseStat.keepDistanceMode = KeepDistanceMode.Auto;
            baseStat.MaxLife = 100f;
            archerStats.SetBaseStats(baseStat);

            var tuning = KeepDistanceTuningSO.CreateRuntimeInstance(3.0f, 0.6f, 0.9f);
            archerStats.SetKeepDistanceTuning(tuning);

            var keepGoal = ScriptableObject.CreateInstance<UtilityKeepDistanceGoal>();
            var killGoal = ScriptableObject.CreateInstance<UtilityKillEnemyGoal>();
            archerAI.Goals = new List<UtilityGoal> { keepGoal, killGoal };

            // Step 1: Monster gets very close inside start fraction (6.0 * 0.6 = 3.6m; monster is at 2.0m)
            archerGo.transform.position = Vector3.zero;
            monsterGo.transform.position = new Vector3(2.0f, 0f, 0f);
            archerAI.SenseEnemy();
            archerAI.SetTarget(monsterGo.transform);

            float keepScoreClose = keepGoal.Evaluate(archerAI);
            float killScoreClose = killGoal.Evaluate(archerAI);
            Assert.Greater(keepScoreClose, killScoreClose, "Archer should prioritize KeepDistance when enemy is inside comfort boundary.");

            // Set active goal to KeepDistance (simulate kiting in progress)
            archerAI.SetActiveGoalForTesting(EnumUtilGoal.KeepDistance);

            // Step 2: Archer retreats to stop distance (6.0 * 0.9 = 5.4m; enemy now at 5.5m)
            monsterGo.transform.position = new Vector3(5.5f, 0f, 0f);
            float keepScoreFar = keepGoal.Evaluate(archerAI);
            float killScoreFar = killGoal.Evaluate(archerAI);

            Assert.AreEqual(0f, keepScoreFar, "KeepDistance should evaluate to 0 once enemy is past stop distance.");
            Assert.Greater(killScoreFar, 0f, "Archer should resume firing/KillEnemy once stop distance is reached.");

            Object.DestroyImmediate(archerGo);
            Object.DestroyImmediate(keepGoal);
            Object.DestroyImmediate(killGoal);
        }

        [Test]
        public void CombatSandbox_HealerKiting_PreservesSquadCohesion()
        {
            // Position leader/tank at origin
            leaderGo.transform.position = Vector3.zero;
            clericGo.transform.position = new Vector3(-2.0f, 0f, 0f);

            var clericMember = clericGo.AddComponent<SquadMember>();
            leader.RegisterMember(clericMember);

            var clericBaseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            clericBaseStat.basicAttackMode = BasicAttackMode.None;
            clericBaseStat.characterFunction = AdventurerFunction.Healer;
            clericStats.SetBaseStats(clericBaseStat);

            // Monster approaches cleric from the left (-4.0, 0)
            monsterGo.transform.position = new Vector3(-4.0f, 0f, 0f);
            clericAI.SenseEnemy();
            clericAI.SetTarget(monsterGo.transform);

            // Sensor evaluation for cleric keep distance retreat target
            var sensor = new EnemyDistanceSensor();
            var target = new PositionTarget(clericGo.transform.position);

            // The retreat direction should bias towards squad/tank (leader at (0,0)) instead of fleeing indefinitely left
            Vector3 retreatVector = leaderGo.transform.position - clericGo.transform.position;
            Assert.Greater(retreatVector.x, 0f, "Leader is to the right of cleric.");

            // Verify cleric DesiredRecoveryPosition seeks squad anchor
            Vector3 desiredPos = clericAI.GetDesiredRecoveryPosition();
            Assert.GreaterOrEqual(desiredPos.x, clericGo.transform.position.x, "Healer retreat position should not isolate healer further from squad.");
        }

        [Test]
        public void CombatSandbox_RankENoise_ShowsEarlyRetreatOrOverstay_WhileRankCStable()
        {
            var rankETuning = FightOutlookTuningSO.CreateRuntimeInstance();
            var rankCTuning = FightOutlookTuningSO.CreateRuntimeInstance();

            // Set up test inputs across 10 randomized decision ticks
            float rankENoiseSpread = 0f;
            float rankCNoiseSpread = 0f;

            var rngE = new GameRng(42u);
            var rngC = new GameRng(42u);

            float minOutlookE = float.MaxValue, maxOutlookE = float.MinValue;
            float minOutlookC = float.MaxValue, maxOutlookC = float.MinValue;

            for (int i = 0; i < 10; i++)
            {
                float noiseE_Ttk = rngE.Range(-rankETuning.RankEErrorFactor, rankETuning.RankEErrorFactor);
                float noiseE_Ttd = rngE.Range(-rankETuning.RankEErrorFactor, rankETuning.RankEErrorFactor);

                var inputE = new FightOutlookCalculator.CalculationInput
                {
                    SelfLife = 50f,
                    SelfMaxLife = 100f,
                    SelfDps = 15f,
                    TargetLife = 50f,
                    TargetMaxLife = 100f,
                    NearbyAlliesDps = 0f,
                    IncomingEnemiesDps = 15f,
                    CourageBias = 0f,
                    FunctionTolerance = 1.0f,
                    RankTtkNoiseFactor = noiseE_Ttk,
                    RankTtdNoiseFactor = noiseE_Ttd,
                    IsHardSafetyTrigger = false
                };

                var resE = FightOutlookCalculator.Calculate(inputE, rankETuning);
                if (resE.NormalizedOutlook < minOutlookE) minOutlookE = resE.NormalizedOutlook;
                if (resE.NormalizedOutlook > maxOutlookE) maxOutlookE = resE.NormalizedOutlook;

                float noiseC_Ttk = rngC.Range(-rankCTuning.RankCErrorFactor, rankCTuning.RankCErrorFactor);
                float noiseC_Ttd = rngC.Range(-rankCTuning.RankCErrorFactor, rankCTuning.RankCErrorFactor);

                var inputC = new FightOutlookCalculator.CalculationInput
                {
                    SelfLife = 50f,
                    SelfMaxLife = 100f,
                    SelfDps = 15f,
                    TargetLife = 50f,
                    TargetMaxLife = 100f,
                    NearbyAlliesDps = 0f,
                    IncomingEnemiesDps = 15f,
                    CourageBias = 0f,
                    FunctionTolerance = 1.0f,
                    RankTtkNoiseFactor = noiseC_Ttk,
                    RankTtdNoiseFactor = noiseC_Ttd,
                    IsHardSafetyTrigger = false
                };

                var resC = FightOutlookCalculator.Calculate(inputC, rankCTuning);
                if (resC.NormalizedOutlook < minOutlookC) minOutlookC = resC.NormalizedOutlook;
                if (resC.NormalizedOutlook > maxOutlookC) maxOutlookC = resC.NormalizedOutlook;
            }

            rankENoiseSpread = maxOutlookE - minOutlookE;
            rankCNoiseSpread = maxOutlookC - minOutlookC;

            // Rank E should show significant variance (misjudgments leading to mixed early-retreat or overstay)
            Assert.Greater(rankENoiseSpread, 0.25f, "Rank E must demonstrate wide perception variance.");
            // Rank C should remain razor sharp and stable
            Assert.Less(rankCNoiseSpread, 0.08f, "Rank C must remain stable with near-zero error.");
        }
    }
}
