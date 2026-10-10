using System.Collections.Generic;
using CrashKonijn.Goap.Runtime;
using NUnit.Framework;
using SBGames.Personality;
using SBGames.Personality.Data;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class MinionMonsterAITests
    {
        private GameObject rootGo;
        private GameObject augmentGo;
        private BaseAI baseAI;
        private CharacterBrain brain;
        private CharacterStatsManager stats;
        private CharacterCombatController combatController;
        private SquadMember squadMember;
        private CharacterPersonality personality;
        private AIDecisionThrottler throttler;

        [SetUp]
        public void SetUp()
        {
            rootGo = new GameObject("Test_MinionRoot");
            stats = rootGo.AddComponent<CharacterStatsManager>();
            brain = rootGo.GetComponent<CharacterBrain>() ?? rootGo.AddComponent<CharacterBrain>();
            combatController = rootGo.AddComponent<CharacterCombatController>();
            squadMember = rootGo.AddComponent<SquadMember>();
            personality = rootGo.AddComponent<CharacterPersonality>();

            // Wire stats into brain & combat controller
            var brainStatsField = typeof(CharacterBrain).GetField("statsManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            brainStatsField?.SetValue(brain, stats);
            var combatStatsField = typeof(CharacterCombatController).GetField("stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            combatStatsField?.SetValue(combatController, stats);
            var combatBrainField = typeof(CharacterCombatController).GetField("brain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            combatBrainField?.SetValue(combatController, brain);

            augmentGo = new GameObject("AI_Augment");
            augmentGo.transform.SetParent(rootGo.transform, false);
            var provider = augmentGo.AddComponent<GoapActionProvider>();
            baseAI = augmentGo.AddComponent<BaseAI>();
            throttler = augmentGo.AddComponent<AIDecisionThrottler>();

            var providerField = typeof(BaseAI).GetField("provider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            providerField?.SetValue(baseAI, provider);
        }

        [TearDown]
        public void TearDown()
        {
            if (rootGo != null)
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void MinionBasicCapabilityFactory_RegistersExpectedSensors_ExcludesCombatStatusSensor()
        {
            var factory = new MinionBasicCapabilityFactory();
            var config = factory.Create();

            Assert.IsNotNull(config, "MinionBasicCapability config must not be null.");
            Assert.AreEqual(0, config.Goals.Count, "MinionBasicCapability should not register goals.");
            Assert.AreEqual(0, config.Actions.Count, "MinionBasicCapability should not register actions.");

            // Verify Target Sensors: EnemyTargetSensor targeting EnemyTarget
            Assert.AreEqual(1, config.TargetSensors.Count, "Should register EnemyTargetSensor.");
            StringAssert.Contains("EnemyTargetSensor", config.TargetSensors[0].ClassType);

            // Verify Multi Sensors: AwarenessSensor and EnemyDistanceSensor (CombatStatusSensor EXCLUDED)
            Assert.AreEqual(2, config.MultiSensors.Count, "Should register only AwarenessSensor and EnemyDistanceSensor.");
            var sensorNames = new HashSet<string>();
            foreach (var s in config.MultiSensors)
            {
                sensorNames.Add(s.ClassType);
            }

            Assert.IsTrue(sensorNames.Contains(typeof(AwarenessSensor).AssemblyQualifiedName) || sensorNames.Contains(typeof(AwarenessSensor).FullName) || sensorNames.Contains(nameof(AwarenessSensor)), "Must contain AwarenessSensor.");
            Assert.IsTrue(sensorNames.Contains(typeof(EnemyDistanceSensor).AssemblyQualifiedName) || sensorNames.Contains(typeof(EnemyDistanceSensor).FullName) || sensorNames.Contains(nameof(EnemyDistanceSensor)), "Must contain EnemyDistanceSensor.");
            Assert.IsFalse(sensorNames.Contains(typeof(CombatStatusSensor).AssemblyQualifiedName) || sensorNames.Contains(typeof(CombatStatusSensor).FullName) || sensorNames.Contains(nameof(CombatStatusSensor)), "Must EXCLUDE CombatStatusSensor.");
        }

        [Test]
        public void MinionAgentTypeFactory_BuildsSuccessfully_ExcludesSkillCapability()
        {
            var factory = new MinionAgentTypeFactory();
            var config = factory.Create();

            Assert.IsNotNull(config, "MinionAgent config must not be null.");

            // Verify capabilities by inspecting registered goals and actions
            var goalNames = new HashSet<string>();
            foreach (var g in config.Goals)
            {
                goalNames.Add(g.ClassType);
            }

            Assert.IsTrue(goalNames.Contains(typeof(KillEnemyGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(KillEnemyGoal).FullName) || goalNames.Contains(nameof(KillEnemyGoal)), "Must include KillEnemyGoal.");
            Assert.IsTrue(goalNames.Contains(typeof(IdleGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(IdleGoal).FullName) || goalNames.Contains(nameof(IdleGoal)), "Must include IdleGoal.");
            Assert.IsTrue(goalNames.Contains(typeof(FollowLeaderGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(FollowLeaderGoal).FullName) || goalNames.Contains(nameof(FollowLeaderGoal)), "Must include FollowLeaderGoal.");
            Assert.IsTrue(goalNames.Contains(typeof(FocusFireGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(FocusFireGoal).FullName) || goalNames.Contains(nameof(FocusFireGoal)), "Must include FocusFireGoal.");
            Assert.IsTrue(goalNames.Contains(typeof(FallBackGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(FallBackGoal).FullName) || goalNames.Contains(nameof(FallBackGoal)), "Must include FallBackGoal.");
            Assert.IsTrue(goalNames.Contains(typeof(SurviveGoal).AssemblyQualifiedName) || goalNames.Contains(typeof(SurviveGoal).FullName) || goalNames.Contains(nameof(SurviveGoal)), "Must include SurviveGoal.");

            var actionNames = new HashSet<string>();
            foreach (var a in config.Actions)
            {
                actionNames.Add(a.ClassType);
            }

            Assert.IsTrue(actionNames.Contains(typeof(AttackAction).AssemblyQualifiedName) || actionNames.Contains(typeof(AttackAction).FullName) || actionNames.Contains(nameof(AttackAction)), "Must include AttackAction.");
            Assert.IsTrue(actionNames.Contains(typeof(WanderAction).AssemblyQualifiedName) || actionNames.Contains(typeof(WanderAction).FullName) || actionNames.Contains(nameof(WanderAction)), "Must include WanderAction.");
            Assert.IsTrue(actionNames.Contains(typeof(FollowLeaderAction).AssemblyQualifiedName) || actionNames.Contains(typeof(FollowLeaderAction).FullName) || actionNames.Contains(nameof(FollowLeaderAction)), "Must include FollowLeaderAction.");
            Assert.IsTrue(actionNames.Contains(typeof(FocusFireAction).AssemblyQualifiedName) || actionNames.Contains(typeof(FocusFireAction).FullName) || actionNames.Contains(nameof(FocusFireAction)), "Must include FocusFireAction.");
            Assert.IsTrue(actionNames.Contains(typeof(FallBackAction).AssemblyQualifiedName) || actionNames.Contains(typeof(FallBackAction).FullName) || actionNames.Contains(nameof(FallBackAction)), "Must include FallBackAction.");
            Assert.IsTrue(actionNames.Contains(typeof(FleeAction).AssemblyQualifiedName) || actionNames.Contains(typeof(FleeAction).FullName) || actionNames.Contains(nameof(FleeAction)), "Must include FleeAction.");
            Assert.IsFalse(actionNames.Contains(typeof(CastSkillAction).AssemblyQualifiedName) || actionNames.Contains(typeof(CastSkillAction).FullName) || actionNames.Contains(nameof(CastSkillAction)), "Must EXCLUDE CastSkillAction.");
        }

        [Test]
        public void MinionAI_WithoutSkills_SelectBestSkillReturnsNull_BasicAttackOperates()
        {
            // Verify skills list is empty
            Assert.AreEqual(0, baseAI.Skills.Count, "Minion should have empty skills list.");
            Assert.IsNull(baseAI.SelectBestSkill(), "SelectBestSkill must return null when skills list is empty.");

            // Verify basic attack initiates via combat controller and brain
            // By default brain can start attack if not on cooldown
            bool canAttack = combatController.Attack();
            // In unit test without animator/sprite, brain.TryStartAttack returns true if alive and not busy
            Assert.IsTrue(canAttack, "CharacterCombatController.Attack() should succeed for basic attack.");
        }

        [Test]
        public void MinionAI_SquadOrders_ObeyedWithCorrectPrecedence()
        {
            var leaderGo = new GameObject("Test_Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();

            squadMember.AssignLeader(leader);
            Assert.IsTrue(baseAI.HasSquadLeader, "Minion must recognize squad leader.");

            var fallbackGoal = ScriptableObject.CreateInstance<UtilityFallBackGoal>();
            var focusfireGoal = ScriptableObject.CreateInstance<UtilityFocusFireGoal>();
            var followleaderGoal = ScriptableObject.CreateInstance<UtilityFollowLeaderGoal>();
            var killenemyGoal = ScriptableObject.CreateInstance<UtilityKillEnemyGoal>();

            // 1. Order: FallBack -> Score should be 1.0 (Highest precedence)
            leader.OrderFallBack();
            Assert.AreEqual(SquadOrderType.FallBack, baseAI.SquadOrder);
            Assert.AreEqual(1.0f, fallbackGoal.Evaluate(baseAI), 0.001f, "FallBack must score 1.0f when commanded.");

            // 2. Order: FocusFire -> Score should be ~0.95
            var focusTargetGo = new GameObject("Test_FocusTarget");
            leader.OrderFocusFire(focusTargetGo.transform);
            Assert.AreEqual(SquadOrderType.FocusFire, baseAI.SquadOrder);
            Assert.AreEqual(0.95f, focusfireGoal.Evaluate(baseAI), 0.01f, "FocusFire must score ~0.95f when commanded.");

            // 3. Order: FollowLeader -> Score should be 0.85
            leader.OrderFollowLeader();
            Assert.AreEqual(SquadOrderType.FollowLeader, baseAI.SquadOrder);
            Assert.AreEqual(0.85f, followleaderGoal.Evaluate(baseAI), 0.01f, "FollowLeader must score 0.85f when commanded.");

            Object.DestroyImmediate(focusTargetGo);
            Object.DestroyImmediate(leaderGo);
            Object.DestroyImmediate(fallbackGoal);
            Object.DestroyImmediate(focusfireGoal);
            Object.DestroyImmediate(followleaderGoal);
            Object.DestroyImmediate(killenemyGoal);
        }

        [Test]
        public void MinionAI_FightToTheDeathByDefault_DoesNotFleeOnLowHP_UnlessCowardly()
        {
            var surviveGoal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();

            // Configure surviveGoal for minion default archetype (ignoreSurvivalGoal = true, survivalHpThreshold = 0f)
            var ignoreField = typeof(UtilitySurviveGoal).GetField("ignoreSurvivalGoal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ignoreField.SetValue(surviveGoal, true);
            var thresholdField = typeof(UtilitySurviveGoal).GetField("survivalHpThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            thresholdField.SetValue(surviveGoal, 0f);

            // Simulate critically wounded minion (10% HP)
            var lifeField = typeof(BaseAI).GetField("agentLife", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lifeField.SetValue(baseAI, 10f);
            baseAI.SenseEnemy(); // Generate fear/threat
            baseAI.SenseEnemy();

            // Default minion without cowardly trait -> Should NOT flee (score == 0)
            float defaultScore = surviveGoal.Evaluate(baseAI);
            Assert.AreEqual(0f, defaultScore, "Minions should fight to the death and score 0 on survival by default.");

            // Assign Cowardly trait to minion
            var cowardlyTrait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var traitThresholdField = typeof(CharacterTraitSO).GetField("survivalHpThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            traitThresholdField.SetValue(cowardlyTrait, 0.50f);
            var neverFleeField = typeof(CharacterTraitSO).GetField("neverFlee", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            neverFleeField.SetValue(cowardlyTrait, false);

            personality.SetTrait(cowardlyTrait);

            // With Cowardly trait, minion on low HP now scores survival > 0
            float cowardlyScore = surviveGoal.Evaluate(baseAI);
            Assert.Greater(cowardlyScore, 0f, "Cowardly minion on low HP must evaluate survival goal > 0.");

            Object.DestroyImmediate(surviveGoal);
            Object.DestroyImmediate(cowardlyTrait);
        }

        [Test]
        public void AIDecisionThrottler_And_BaseAI_ZeroManagedAllocationsInDecisionLoop()
        {
            // Setup goals on BaseAI
            var goals = new List<UtilityGoal>
            {
                ScriptableObject.CreateInstance<UtilityWanderGoal>(),
                ScriptableObject.CreateInstance<UtilityFallBackGoal>(),
                ScriptableObject.CreateInstance<UtilityFocusFireGoal>()
            };

            var goalsField = typeof(BaseAI).GetField("goals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            goalsField.SetValue(baseAI, goals);

            var selectBestGoalMethod = typeof(BaseAI).GetMethod("SelectBestGoal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Warm up
            for (int i = 0; i < 5; i++)
            {
                selectBestGoalMethod.Invoke(baseAI, null);
            }

            // Measure GC allocations over 50 goal evaluation ticks
            long allocBefore = System.GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 50; i++)
            {
                selectBestGoalMethod.Invoke(baseAI, null);
            }

            long allocAfter = System.GC.GetAllocatedBytesForCurrentThread();
            long totalAlloc = allocAfter - allocBefore;

            Assert.AreEqual(0, totalAlloc, $"Goal selection hot path must not allocate managed memory. Allocated: {totalAlloc} bytes.");

            foreach (var g in goals) Object.DestroyImmediate(g);
        }
    }
}
