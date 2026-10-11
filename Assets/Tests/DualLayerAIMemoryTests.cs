using System;
using NUnit.Framework;
using SBGames.Characters.Traps;
using SBGames.Loop;
using SBGames.Personality;
using SBGames.Personality.Data;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DualLayerAIMemoryTests
    {
        private GameObject _agentGo;
        private BaseAI _baseAI;
        private CharacterAIMemory _memory;
        private CharacterStatsManager _stats;
        private CharacterPersonality _personality;

        [SetUp]
        public void SetUp()
        {
            _agentGo = new GameObject("Test_Agent");
            _stats = _agentGo.AddComponent<CharacterStatsManager>();
            _personality = _agentGo.AddComponent<CharacterPersonality>();
            _baseAI = _agentGo.AddComponent<BaseAI>();
            _memory = _agentGo.AddComponent<CharacterAIMemory>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_agentGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_agentGo);
            }
        }

        [Test]
        public void DamageIntake_RecordsInRingBuffer_CalculatesBurstDamage()
        {
            var attacker = new GameObject("Test_Attacker");

            var dmg1 = new DamageData(20f, DamageType.Physical, attacker);
            var dmg2 = new DamageData(30f, DamageType.Physical, attacker);

            _memory.RecordDamageIntake(in dmg1, attacker);
            _memory.RecordDamageIntake(in dmg2, attacker);

            Assert.AreEqual(2, _memory.DamageRecordCount);
            Assert.AreEqual(50f, _memory.GetRecentDamage(5.0f), 0.001f);
            Assert.IsTrue(_memory.HasRecentAttacker);
            Assert.AreEqual(attacker.GetEntityId().GetHashCode(), _memory.LastAttackerId);
            Assert.AreEqual(attacker.transform, _memory.LastAttackerTransform);

            UnityEngine.Object.DestroyImmediate(attacker);
        }

        [Test]
        public void Grudge_AccumulatesAndHeavilyWeightsTargetScore()
        {
            var attacker = new GameObject("Test_Attacker");
            var neutralEnemy = new GameObject("Test_Neutral");

            float neutralScoreBefore = _baseAI.GetTraitWeightedTargetScore(neutralEnemy.transform, 5f);
            float attackerScoreBefore = _baseAI.GetTraitWeightedTargetScore(attacker.transform, 5f);
            Assert.AreEqual(neutralScoreBefore, attackerScoreBefore, 0.001f);

            // Attacker deals 40 damage
            var dmg = new DamageData(40f, DamageType.Physical, attacker);
            _memory.RecordDamageIntake(in dmg, attacker);

            float grudgeScore = _memory.GetGrudgeScore(attacker.GetEntityId().GetHashCode());
            Assert.Greater(grudgeScore, 0f, "Grudge score should accumulate after taking damage.");

            float attackerScoreAfter = _baseAI.GetTraitWeightedTargetScore(attacker.transform, 5f);
            float neutralScoreAfter = _baseAI.GetTraitWeightedTargetScore(neutralEnemy.transform, 5f);

            Assert.Greater(attackerScoreAfter, neutralScoreAfter,
                "Attacker with grudge and retaliation bonus must score substantially higher than neutral enemy.");

            UnityEngine.Object.DestroyImmediate(attacker);
            UnityEngine.Object.DestroyImmediate(neutralEnemy);
        }

        [Test]
        public void RetaliationWindow_ExpiresOnDecay()
        {
            var attacker = new GameObject("Test_Attacker");
            var dmg = new DamageData(25f, DamageType.Physical, attacker);

            _memory.RecordDamageIntake(in dmg, attacker);
            Assert.IsTrue(_memory.HasRecentAttacker);

            // Tick decay past the retaliation duration (4 seconds)
            _memory.TickMemoryDecay(5.0f);

            Assert.IsFalse(_memory.HasRecentAttacker, "Retaliation window must expire after decay duration.");
            Assert.AreEqual(0, _memory.LastAttackerId);
            Assert.IsNull(_memory.LastAttackerTransform);

            UnityEngine.Object.DestroyImmediate(attacker);
        }

        [Test]
        public void Grudge_DecaysOverTime_PrunesZeroScores()
        {
            var attacker = new GameObject("Test_Attacker");
            var dmg = new DamageData(10f, DamageType.Physical, attacker);

            _memory.RecordDamageIntake(in dmg, attacker);
            int attackerId = attacker.GetEntityId().GetHashCode();

            float initialGrudge = _memory.GetGrudgeScore(attackerId);
            Assert.Greater(initialGrudge, 0f);

            // Tick decay partially
            _memory.TickMemoryDecay(1.0f);
            float decayedGrudge = _memory.GetGrudgeScore(attackerId);
            Assert.Less(decayedGrudge, initialGrudge);

            // Tick decay past total expiration
            _memory.TickMemoryDecay(20.0f);
            Assert.AreEqual(0f, _memory.GetGrudgeScore(attackerId), "Grudge should reach 0 and prune.");
            Assert.AreEqual(0, _memory.GrudgeCount);

            UnityEngine.Object.DestroyImmediate(attacker);
        }

        [Test]
        public void HazardMemory_LocalAndSquadSync_EnablesSharedAvoidance()
        {
            // Setup Squad Leader and Member
            var leaderGo = new GameObject("Test_Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();

            var scoutGo = new GameObject("Test_Scout");
            var scoutMember = scoutGo.AddComponent<SquadMember>();
            var scoutAI = scoutGo.AddComponent<BaseAI>();
            var scoutMemory = scoutGo.AddComponent<CharacterAIMemory>();
            leader.RegisterMember(scoutMember);

            var followerGo = new GameObject("Test_Follower");
            var followerMember = followerGo.AddComponent<SquadMember>();
            var followerAI = followerGo.AddComponent<BaseAI>();
            var followerMemory = followerGo.AddComponent<CharacterAIMemory>();
            leader.RegisterMember(followerMember);

            Vector2Int trapGrid = new Vector2Int(5, 7);
            Vector3 trapWorld = new Vector3(5f, 7f, 0f);

            // Scout discovers/steps on trap
            scoutMemory.RecordHazard(trapGrid, trapWorld, dangerLevel: 50f, duration: 30f);

            // 1. Scout has local hazard knowledge
            Assert.IsTrue(scoutMemory.IsPositionHazardous(trapWorld, safetyRadius: 1.2f));

            // 2. Leader's Blackboard inherited the shared hazard
            Assert.IsTrue(leader.Blackboard.HasSharedHazards);
            Assert.IsTrue(leader.Blackboard.IsPositionHazardous(trapWorld, safetyRadius: 1.2f));

            // 3. Follower inherits hazard avoidance without having stepped on it
            Assert.IsTrue(followerMemory.IsPositionHazardous(trapWorld, safetyRadius: 1.2f));

            // Position outside safety radius is not hazardous
            Assert.IsFalse(followerMemory.IsPositionHazardous(new Vector3(10f, 10f, 0f), safetyRadius: 1.2f));

            UnityEngine.Object.DestroyImmediate(leaderGo);
            UnityEngine.Object.DestroyImmediate(scoutGo);
            UnityEngine.Object.DestroyImmediate(followerGo);
        }

        [Test]
        public void PersonalityBiases_TunnelVisionAndGrudgeMultiplier()
        {
            var traitReckless = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var so = new UnityEditor.SerializedObject(traitReckless);
            so.FindProperty("traitId").stringValue = "Reckless";
            so.FindProperty("neverFlee").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            _personality.Initialize(null, traitReckless);

            // Verify Reckless snapshot properties
            Assert.IsTrue(_personality.Snapshot.HasTunnelVision, "Reckless must have Tunnel Vision.");
            Assert.Greater(_personality.Snapshot.GrudgeWeightMultiplier, 1.5f, "Reckless must have extreme grudge weight.");

            // Record a hazard
            Vector3 trapPos = new Vector3(3f, 3f, 0f);
            _memory.RecordHazard(new Vector2Int(3, 3), trapPos, 50f);

            // Out of combat: recognizes hazard
            Assert.IsTrue(_memory.IsPositionHazardous(trapPos, 1.2f));

            // In combat (threatLevel > 0): Tunnel Vision causes unit to disregard hazard
            _baseAI.SenseEnemy();
            Assert.Greater(_baseAI.ThreatLevel, 0);
            Assert.IsFalse(_memory.IsPositionHazardous(trapPos, 1.2f),
                "Reckless unit in active combat must ignore hazard due to Tunnel Vision.");

            UnityEngine.Object.DestroyImmediate(traitReckless);
        }

        [Test]
        public void UtilitySurviveGoal_TriggersEmergencyPanicOnBurstDamage()
        {
            var surviveGoal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();

            // Character is at full HP (100 HP), threshold is default (25% = 25 HP)
            _baseAI.SetAgentLife(100f);

            // Under normal circumstances with full HP and 0 fear, survive score is 0
            float normalScore = surviveGoal.Evaluate(_baseAI);
            Assert.AreEqual(0f, normalScore, "Full HP unit should not trigger survive goal under calm conditions.");

            // Unit takes sudden 35 burst damage (35% of max HP in 2s)
            var attacker = new GameObject("Test_Attacker");
            var dmg = new DamageData(35f, DamageType.Physical, attacker);
            _memory.RecordDamageIntake(in dmg, attacker);

            // Evaluate survive goal now: burst damage velocity exceeds 25% max HP in 2s
            float panicScore = surviveGoal.Evaluate(_baseAI);
            Assert.Greater(panicScore, 0f, "Sudden burst damage velocity must trigger emergency survival panic.");

            UnityEngine.Object.DestroyImmediate(attacker);
            UnityEngine.Object.DestroyImmediate(surviveGoal);
        }

        [Test]
        public void CoreGameLoop_ResetCombatMemory_PersistsHazardMemory()
        {
            var attacker = new GameObject("Test_Attacker");
            var dmg = new DamageData(30f, DamageType.Physical, attacker);
            _memory.RecordDamageIntake(in dmg, attacker);

            Vector3 trapPos = new Vector3(4f, 4f, 0f);
            _memory.RecordHazard(new Vector2Int(4, 4), trapPos, 50f);

            Assert.Greater(_memory.GrudgeCount, 0);
            Assert.IsTrue(_memory.HasRecentAttacker);
            Assert.Greater(_memory.HazardCount, 0);

            // Simulate transition to Planning phase
            _memory.ResetCombatMemory();

            // Tactical memory is cleared
            Assert.AreEqual(0, _memory.GrudgeCount, "Grudges must be cleared on combat reset.");
            Assert.IsFalse(_memory.HasRecentAttacker, "Last attacker must be cleared on combat reset.");
            Assert.AreEqual(0, _memory.DamageRecordCount, "Damage history must be cleared on combat reset.");

            // Spatial hazard memory persists
            Assert.Greater(_memory.HazardCount, 0, "Hazard memory must persist across combat resets.");
            Assert.IsTrue(_memory.IsPositionHazardous(trapPos, 1.2f));

            UnityEngine.Object.DestroyImmediate(attacker);
        }

        [Test]
        public void ZeroGCAlloc_HotPathEvaluation()
        {
            var attacker = new GameObject("Test_Attacker");
            var dmg = new DamageData(15f, DamageType.Physical, attacker);
            _memory.RecordDamageIntake(in dmg, attacker);
            _memory.RecordHazard(new Vector2Int(2, 2), new Vector3(2f, 2f, 0f), 50f);

            int attackerId = attacker.GetEntityId().GetHashCode();
            Vector3 testPos = new Vector3(2f, 2f, 0f);

            // Warm up JIT and cache
            for (int i = 0; i < 50; i++)
            {
                _memory.TickMemoryDecay(0.01f);
                _memory.GetGrudgeScore(attackerId);
                _memory.GetRecentDamage(2f);
                _memory.IsPositionHazardous(testPos, 1.2f);
                _baseAI.GetTraitWeightedTargetScore(attacker.transform, 3f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetTotalMemory(true);

            // Execute hot paths 500 times
            for (int i = 0; i < 500; i++)
            {
                _memory.TickMemoryDecay(0.001f);
                _memory.GetGrudgeScore(attackerId);
                _memory.GetRecentDamage(2f);
                _memory.IsPositionHazardous(testPos, 1.2f);
                _baseAI.GetTraitWeightedTargetScore(attacker.transform, 3f);
            }

            long allocAfter = GC.GetTotalMemory(false);
            long totalAlloc = allocAfter - allocBefore;

            Assert.LessOrEqual(totalAlloc, 0,
                $"Expected 0 B GC Alloc during hot path memory evaluation, but got {totalAlloc} B.");

            UnityEngine.Object.DestroyImmediate(attacker);
        }
    }
}
