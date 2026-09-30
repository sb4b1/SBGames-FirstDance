using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CombatResultHandlerTests
    {
        private GameObject _holderGo;
        private DungeonMaster _dungeonMaster;
        private CoreGameLoop _gameLoop;
        private CombatResultHandler _handler;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_CombatResultHandler]");
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 50, roundLossPenalty: 25);

            _gameLoop = _holderGo.AddComponent<CoreGameLoop>();
            _gameLoop.Initialize(_dungeonMaster, initialRound: 1, planningAllowance: 15, victoryBonus: 25);

            _handler = _holderGo.AddComponent<CombatResultHandler>();
            _handler.Initialize(loop: _gameLoop, dm: _dungeonMaster, autoTrack: false, autoDiscover: false, defaultBossHp: 500f);
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
        public void AllAdventurersKilled_TriggersPlayerVictory()
        {
            CombatResultData? receivedResult = null;
            _handler.OnShowResultsScreen += data => receivedResult = data;

            _handler.BeginCombat(roundNumber: 1);
            _handler.RegisterAdventurer("adv1", "Knight", 100f);
            _handler.RegisterAdventurer("adv2", "Mage", 80f);

            Assert.AreEqual(2, _handler.ActiveAdventurersCount);
            Assert.AreEqual(CombatState.CombatActive, _handler.CurrentCombatState);

            // Kill first adventurer -> combat should still be active
            _handler.ReportAdventurerDeath("adv1");
            Assert.AreEqual(1, _handler.ActiveAdventurersCount);
            Assert.AreEqual(1, _handler.DefeatedAdventurersCount);
            Assert.IsNull(receivedResult);

            // Kill second adventurer -> combat should resolve as Victory
            _handler.ReportAdventurerDeath("adv2");
            Assert.IsNotNull(receivedResult);
            Assert.AreEqual(BattleOutcome.Victory, receivedResult.Value.Outcome);
            Assert.AreEqual(2, receivedResult.Value.TotalAdventurers);
            Assert.AreEqual(2, receivedResult.Value.DefeatedAdventurers);
            Assert.AreEqual(0, receivedResult.Value.RetreatedAdventurers);
            Assert.AreEqual(CombatState.AwaitingAcknowledgment, _handler.CurrentCombatState);
        }

        [Test]
        public void AllAdventurersRetreated_TriggersPlayerVictory()
        {
            CombatResultData? receivedResult = null;
            _handler.OnShowResultsScreen += data => receivedResult = data;

            _handler.BeginCombat(roundNumber: 2);
            _handler.RegisterAdventurer("adv1", "Thief", 70f);
            _handler.RegisterAdventurer("adv2", "Ranger", 90f);

            _handler.ReportAdventurerRetreat("adv1");
            Assert.IsNull(receivedResult);

            _handler.ReportAdventurerRetreat("adv2");
            Assert.IsNotNull(receivedResult);
            Assert.AreEqual(BattleOutcome.Victory, receivedResult.Value.Outcome);
            Assert.AreEqual(0, receivedResult.Value.DefeatedAdventurers);
            Assert.AreEqual(2, receivedResult.Value.RetreatedAdventurers);
        }

        [Test]
        public void MixedAdventurersKilledAndRetreated_TriggersPlayerVictory()
        {
            CombatResultData? receivedResult = null;
            _handler.OnShowResultsScreen += data => receivedResult = data;

            _handler.BeginCombat(roundNumber: 3);
            _handler.RegisterAdventurer("adv1", "Paladin", 150f);
            _handler.RegisterAdventurer("adv2", "Rogue", 80f);
            _handler.RegisterAdventurer("adv3", "Cleric", 90f);

            _handler.ReportAdventurerDeath("adv1");
            _handler.ReportAdventurerRetreat("adv2");
            Assert.IsNull(receivedResult);

            _handler.ReportAdventurerDeath("adv3");
            Assert.IsNotNull(receivedResult);
            Assert.AreEqual(BattleOutcome.Victory, receivedResult.Value.Outcome);
            Assert.AreEqual(3, receivedResult.Value.TotalAdventurers);
            Assert.AreEqual(2, receivedResult.Value.DefeatedAdventurers);
            Assert.AreEqual(1, receivedResult.Value.RetreatedAdventurers);
        }

        [Test]
        public void BossDeath_TriggersPlayerDefeat()
        {
            CombatResultData? receivedResult = null;
            _handler.OnShowResultsScreen += data => receivedResult = data;

            _handler.BeginCombat(roundNumber: 1);
            _handler.RegisterAdventurer("adv1", "Hero", 200f);

            Assert.IsTrue(_handler.IsBossAlive);

            _handler.ReportBossDamage(100f, 400f);
            Assert.AreEqual(400f, _handler.BossRecord.CurrentHealth);
            Assert.IsNull(receivedResult);

            // Kill boss
            _handler.ReportBossDeath();
            Assert.IsFalse(_handler.IsBossAlive);
            Assert.IsNotNull(receivedResult);
            Assert.AreEqual(BattleOutcome.Defeat, receivedResult.Value.Outcome);
            Assert.AreEqual(CombatState.AwaitingAcknowledgment, _handler.CurrentCombatState);
        }

        [Test]
        public void SimultaneousBossDeathAndAdventurerWipe_DefeatTakesPrecedence()
        {
            CombatResultData? receivedResult = null;
            _handler.OnShowResultsScreen += data => receivedResult = data;

            _handler.BeginCombat(roundNumber: 1);
            _handler.RegisterAdventurer("adv1", "Hero", 100f);

            // Simulating simultaneous wipe on the exact same frame / call
            _handler.ReportBossDeath();
            _handler.ReportAdventurerDeath("adv1");

            Assert.IsNotNull(receivedResult);
            Assert.AreEqual(BattleOutcome.Defeat, receivedResult.Value.Outcome);
        }

        [Test]
        public void AcknowledgeResults_ResolvesCoreGameLoopAndResetsState()
        {
            _gameLoop.StartGame();
            _gameLoop.StartBattle();
            Assert.AreEqual(GamePhase.Battle, _gameLoop.CurrentPhase);

            _handler.BeginCombat(roundNumber: 1);
            _handler.RegisterAdventurer("adv1", "Scout", 50f);
            _handler.ReportAdventurerDeath("adv1");

            Assert.AreEqual(CombatState.AwaitingAcknowledgment, _handler.CurrentCombatState);
            int prevEnergy = _dungeonMaster.Energy;

            // Player acknowledges results screen
            var resolution = _handler.AcknowledgeResultsAndProceed();

            Assert.AreEqual(BattleOutcome.Victory, resolution.Outcome);
            Assert.AreEqual(GamePhase.Planning, _gameLoop.CurrentPhase);
            Assert.AreEqual(2, _gameLoop.CurrentRound);
            Assert.AreEqual(CombatState.Idle, _handler.CurrentCombatState);
            Assert.AreEqual(0, _handler.TotalAdventurersCount);
            // Energy awarded: victory energy bonus (25) + next round planning allowance (15) = 40
            Assert.AreEqual(prevEnergy + 25 + 15, _dungeonMaster.Energy);
        }

        [Test]
        public void Idempotency_MultipleDeathAndRetreatCallsDoNotCorruptCounts()
        {
            _handler.BeginCombat(roundNumber: 1);
            _handler.RegisterAdventurer("adv1", "Warrior", 100f);

            _handler.ReportAdventurerDeath("adv1");
            Assert.AreEqual(1, _handler.DefeatedAdventurersCount);
            Assert.AreEqual(0, _handler.ActiveAdventurersCount);

            // Repeat death call
            _handler.ReportAdventurerDeath("adv1");
            Assert.AreEqual(1, _handler.DefeatedAdventurersCount);
            Assert.AreEqual(0, _handler.ActiveAdventurersCount);

            // Attempt retreat after death
            _handler.ReportAdventurerRetreat("adv1");
            Assert.AreEqual(1, _handler.DefeatedAdventurersCount);
            Assert.AreEqual(0, _handler.RetreatedAdventurersCount);
        }

        [Test]
        public void DebugSimulations_TriggerAppropriateOutcomes()
        {
            CombatResultData? victoryResult = null;
            _handler.OnShowResultsScreen += data => victoryResult = data;

            // Test debug victory
            _handler.DebugSimulateVictory(kills: 2, retreats: 1);
            Assert.IsNotNull(victoryResult);
            Assert.AreEqual(BattleOutcome.Victory, victoryResult.Value.Outcome);
            Assert.AreEqual(3, victoryResult.Value.TotalAdventurers);
            Assert.AreEqual(2, victoryResult.Value.DefeatedAdventurers);
            Assert.AreEqual(1, victoryResult.Value.RetreatedAdventurers);

            // Reset
            _handler.DebugResetCombat();
            Assert.AreEqual(CombatState.Idle, _handler.CurrentCombatState);

            CombatResultData? defeatResult = null;
            _handler.OnShowResultsScreen += data => defeatResult = data;

            // Test debug defeat
            _handler.DebugSimulateDefeat();
            Assert.IsNotNull(defeatResult);
            Assert.AreEqual(BattleOutcome.Defeat, defeatResult.Value.Outcome);
        }

        [Test]
        public void NullAndInvalidInputHandling_DoesNotThrowOrCorruptState()
        {
            Assert.DoesNotThrow(() =>
            {
                _handler.RegisterAdventurer(null, null, -50f);
                _handler.RegisterBoss(null, null, -100f);
                _handler.ReportAdventurerDeath(null);
                _handler.ReportAdventurerRetreat("non_existent_id");
                _handler.ReportAdventurerDamage("non_existent_id", -10f, -5f);
                _handler.UnregisterAdventurer(null);
                _handler.UnregisterAdventurer("non_existent_id");
                _handler.AcknowledgeResultsAndProceed();
                _handler.ResetCombatState();
            });
        }
    }
}
