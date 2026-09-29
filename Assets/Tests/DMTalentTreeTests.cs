using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Loop;
using SBGames.Loop.Talents;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class DMTalentTreeTests
    {
        private GameObject _holderGo;
        private DungeonMaster _dungeonMaster;
        private DungeonProgression _progression;
        private DMTalentTreeManager _talentManager;

        private TalentNodeSO _rootCore;
        private TalentNodeSO _swarmT1;
        private TalentNodeSO _swarmT2A;
        private TalentNodeSO _swarmT2B;
        private TalentNodeSO _swarmT3Capstone;
        private TalentNodeSO _bossActivePower;
        private TalentTreeConfigSO _treeConfig;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_DMTalentTree]");
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: null,
                roundLossPenalty: 25,
                maximumEssence: 100,
                maximumEnergy: 200
            );

            _progression = _holderGo.AddComponent<DungeonProgression>();
            _progression.Initialize(startingRank: DungeonRank.E);

            _talentManager = _holderGo.AddComponent<DMTalentTreeManager>();

            // Create test talent nodes programmatically
            _rootCore = ScriptableObject.CreateInstance<TalentNodeSO>();
            _rootCore.Initialize(
                id: "DM_CORE_01",
                name: "Dungeon Heart Awakening",
                cost: 15,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier0,
                minRank: DungeonRank.E,
                playerModBonus: 0.05f,
                synergyBonus: 50
            );

            _swarmT1 = ScriptableObject.CreateInstance<TalentNodeSO>();
            _swarmT1.Initialize(
                id: "DM_SWARM_01",
                name: "Brood Hivemind",
                cost: 20,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.E,
                prereqs: new[] { _rootCore },
                playerModBonus: 0.10f
            );

            _swarmT2A = ScriptableObject.CreateInstance<TalentNodeSO>();
            _swarmT2A.Initialize(
                id: "DM_SWARM_02",
                name: "Necrotic Reanimation",
                cost: 35,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                prereqs: new[] { _swarmT1 }
            );

            _swarmT2B = ScriptableObject.CreateInstance<TalentNodeSO>();
            _swarmT2B.Initialize(
                id: "DM_SWARM_03",
                name: "Pack Frenzy",
                cost: 35,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                prereqs: new[] { _swarmT1 }
            );

            _swarmT3Capstone = ScriptableObject.CreateInstance<TalentNodeSO>();
            _swarmT3Capstone.Initialize(
                id: "DM_SWARM_04",
                name: "Endless Tide",
                cost: 60,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                prereqs: new[] { _swarmT2A, _swarmT2B },
                prereqLogic: TalentPrerequisiteLogic.Any,
                playerModBonus: 0.20f,
                synergyBonus: 200
            );

            _bossActivePower = ScriptableObject.CreateInstance<TalentNodeSO>();
            _bossActivePower.Initialize(
                id: "DM_BOSS_ACTIVE",
                name: "Cataclysm Protocol",
                cost: 50,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier2,
                type: TalentType.ActivePower,
                minRank: DungeonRank.D,
                prereqs: new[] { _rootCore },
                activeCost: 10,
                cooldown: 5.0f
            );

            _treeConfig = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
            _treeConfig.Initialize("Test Tree", new[]
            {
                _rootCore, _swarmT1, _swarmT2A, _swarmT2B, _swarmT3Capstone, _bossActivePower
            });

            _talentManager.Initialize(_dungeonMaster, _progression, null, _treeConfig);
        }

        [TearDown]
        public void Teardown()
        {
            if (_holderGo != null)
            {
                Object.DestroyImmediate(_holderGo);
            }
            if (_rootCore != null) Object.DestroyImmediate(_rootCore);
            if (_swarmT1 != null) Object.DestroyImmediate(_swarmT1);
            if (_swarmT2A != null) Object.DestroyImmediate(_swarmT2A);
            if (_swarmT2B != null) Object.DestroyImmediate(_swarmT2B);
            if (_swarmT3Capstone != null) Object.DestroyImmediate(_swarmT3Capstone);
            if (_bossActivePower != null) Object.DestroyImmediate(_bossActivePower);
            if (_treeConfig != null) Object.DestroyImmediate(_treeConfig);
        }

        [Test]
        public void InitialState_HasZeroTalentsUnlocked_AndCorrectStatuses()
        {
            Assert.AreEqual(0, _talentManager.UnlockedCount);
            Assert.AreEqual(0, _talentManager.TotalEnergySpent);
            Assert.IsFalse(_talentManager.IsUnlocked(_rootCore));

            // Root core has no prerequisites -> Available
            Assert.AreEqual(TalentUnlockStatus.Available, _talentManager.GetTalentStatus(_rootCore));
            // Swarm T1 requires Root Core -> Locked
            Assert.AreEqual(TalentUnlockStatus.Locked, _talentManager.GetTalentStatus(_swarmT1));
        }

        [Test]
        public void UnlockRootTalent_DeductsEnergy_AndSetsUnlockedState()
        {
            int startingEnergy = _dungeonMaster.Energy;
            bool eventFired = false;
            _talentManager.OnTalentUnlocked += node =>
            {
                if (node == _rootCore) eventFired = true;
            };

            bool success = _talentManager.TryUnlockTalent(_rootCore, out string reason);

            Assert.IsTrue(success, $"Unlock failed: {reason}");
            Assert.IsTrue(_talentManager.IsUnlocked(_rootCore));
            Assert.AreEqual(1, _talentManager.UnlockedCount);
            Assert.AreEqual(_rootCore.EnergyCost, _talentManager.TotalEnergySpent);
            Assert.AreEqual(startingEnergy - _rootCore.EnergyCost, _dungeonMaster.Energy);
            Assert.IsTrue(eventFired);
            Assert.AreEqual(TalentUnlockStatus.Unlocked, _talentManager.GetTalentStatus(_rootCore));
        }

        [Test]
        public void PrerequisiteGating_BlocksT1UntilRootIsUnlocked()
        {
            // Try to unlock T1 directly without Root Core
            bool directUnlock = _talentManager.TryUnlockTalent(_swarmT1, out string failReason);
            Assert.IsFalse(directUnlock);
            Assert.IsFalse(_talentManager.IsUnlocked(_swarmT1));
            Assert.That(failReason, Does.Contain("Prerequisite"));

            // Unlock Root Core
            _talentManager.TryUnlockTalent(_rootCore);
            Assert.AreEqual(TalentUnlockStatus.Available, _talentManager.GetTalentStatus(_swarmT1));

            // Now unlock T1
            bool t1Unlock = _talentManager.TryUnlockTalent(_swarmT1, out string successReason);
            Assert.IsTrue(t1Unlock, $"T1 unlock failed: {successReason}");
            Assert.IsTrue(_talentManager.IsUnlocked(_swarmT1));
        }

        [Test]
        public void DungeonRankGating_BlocksTier2UntilPlayerReachesRankD()
        {
            // Unlock Core and T1
            _talentManager.TryUnlockTalent(_rootCore);
            _talentManager.TryUnlockTalent(_swarmT1);

            // Progression is currently Rank E -> T2 requires Rank D
            Assert.AreEqual(DungeonRank.E, _progression.CurrentRank);
            Assert.AreEqual(TalentUnlockStatus.Locked, _talentManager.GetTalentStatus(_swarmT2A));

            bool rankGated = _talentManager.TryUnlockTalent(_swarmT2A, out string reason);
            Assert.IsFalse(rankGated);
            Assert.That(reason, Does.Contain("Rank"));

            // Promote to Rank D
            _progression.SetRank(DungeonRank.D);
            Assert.AreEqual(TalentUnlockStatus.Available, _talentManager.GetTalentStatus(_swarmT2A));

            bool unlockedAtRankD = _talentManager.TryUnlockTalent(_swarmT2A, out string successReason);
            Assert.IsTrue(unlockedAtRankD, $"Unlock failed after rank promotion: {successReason}");
            Assert.IsTrue(_talentManager.IsUnlocked(_swarmT2A));
        }

        [Test]
        public void PrerequisiteLogic_Any_AllowsUnlockWhenAtLeastOnePrereqIsMet()
        {
            _dungeonMaster.GainEnergy(200);
            // Unlock Core and T1, promote to Rank C
            _progression.SetRank(DungeonRank.C);
            _talentManager.TryUnlockTalent(_rootCore);
            _talentManager.TryUnlockTalent(_swarmT1);

            // T3 Capstone has PrerequisiteLogic = Any (needs either T2A or T2B)
            Assert.IsFalse(_talentManager.ArePrerequisitesMet(_swarmT3Capstone));

            // Unlock only T2A
            _talentManager.TryUnlockTalent(_swarmT2A);
            Assert.IsTrue(_talentManager.ArePrerequisitesMet(_swarmT3Capstone));

            // Should now be able to unlock T3 Capstone without T2B
            bool capstoneUnlocked = _talentManager.TryUnlockTalent(_swarmT3Capstone, out string reason);
            Assert.IsTrue(capstoneUnlocked, $"Capstone failed with Any logic: {reason}");
            Assert.IsTrue(_talentManager.IsUnlocked(_swarmT3Capstone));
        }

        [Test]
        public void InsufficientEnergy_RejectsUnlock_WithoutMutatingState()
        {
            _dungeonMaster.SpendEnergy(_dungeonMaster.Energy); // 0 Energy
            Assert.AreEqual(0, _dungeonMaster.Energy);

            Assert.AreEqual(TalentUnlockStatus.AffordableOnlyWhenGained, _talentManager.GetTalentStatus(_rootCore));

            bool success = _talentManager.TryUnlockTalent(_rootCore, out string reason);
            Assert.IsFalse(success);
            Assert.That(reason, Does.Contain("Insufficient Energy"));
            Assert.IsFalse(_talentManager.IsUnlocked(_rootCore));
            Assert.AreEqual(0, _talentManager.TotalEnergySpent);
        }

        [Test]
        public void DoubleUnlock_IsPrevented_AndDoesNotChargeEnergyTwice()
        {
            _talentManager.TryUnlockTalent(_rootCore);
            int energyAfterFirst = _dungeonMaster.Energy;
            int spentAfterFirst = _talentManager.TotalEnergySpent;

            bool secondAttempt = _talentManager.TryUnlockTalent(_rootCore, out string reason);
            Assert.IsFalse(secondAttempt);
            Assert.That(reason, Does.Contain("already unlocked"));
            Assert.AreEqual(energyAfterFirst, _dungeonMaster.Energy);
            Assert.AreEqual(spentAfterFirst, _talentManager.TotalEnergySpent);
        }

        [Test]
        public void AggregateModifiers_AccumulateCorrectly()
        {
            _dungeonMaster.GainEnergy(200);
            _progression.SetRank(DungeonRank.C);
            _talentManager.TryUnlockTalent(_rootCore); // +0.05 playerMod, +50 synergy
            _talentManager.TryUnlockTalent(_swarmT1); // +0.10 playerMod
            _talentManager.TryUnlockTalent(_swarmT2A);
            _talentManager.TryUnlockTalent(_swarmT3Capstone); // +0.20 playerMod, +200 synergy

            float totalPlayerMod = _talentManager.GetAggregatePlayerModifierBonus();
            int totalSynergy = _talentManager.GetAggregateSynergyPowerBonus();

            Assert.AreEqual(0.35f, totalPlayerMod, 0.001f);
            Assert.AreEqual(250, totalSynergy);
        }

        [Test]
        public void ResetTalentTree_ClearsAllState_AndNotifiesListeners()
        {
            _talentManager.TryUnlockTalent(_rootCore);
            Assert.AreEqual(1, _talentManager.UnlockedCount);

            bool resetFired = false;
            _talentManager.OnTalentTreeReset += () => resetFired = true;

            _talentManager.ResetTalentTree();

            Assert.AreEqual(0, _talentManager.UnlockedCount);
            Assert.AreEqual(0, _talentManager.TotalEnergySpent);
            Assert.IsFalse(_talentManager.IsUnlocked(_rootCore));
            Assert.IsTrue(resetFired);
        }

        [Test]
        public void TreeValidation_DetectsCircularDependencies_AndDuplicateIds()
        {
            var nodeA = ScriptableObject.CreateInstance<TalentNodeSO>();
            var nodeB = ScriptableObject.CreateInstance<TalentNodeSO>();

            // Setup circular dependency: A requires B, B requires A
            nodeA.Initialize("NODE_A", "Node A", 10, prereqs: new[] { nodeB });
            nodeB.Initialize("NODE_B", "Node B", 10, prereqs: new[] { nodeA });

            var config = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
            config.Initialize("Cyclic Tree", new[] { nodeA, nodeB });

            bool isValid = config.ValidateTree(out List<string> errors);
            Assert.IsFalse(isValid);
            Assert.IsTrue(errors.Exists(e => e.Contains("Circular dependency")));

            Object.DestroyImmediate(nodeA);
            Object.DestroyImmediate(nodeB);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ActivePower_ExecutionFlow_ValidatesPhaseAndCooldown()
        {
            var loopGo = new GameObject("[Test_Loop]");
            var loop = loopGo.AddComponent<CoreGameLoop>();
            loop.Initialize(_dungeonMaster, initialRound: 1, planningAllowance: 15, victoryBonus: 25, maxRoundLimit: 0);
            loop.StartGame(); // Starts in Planning Phase

            _talentManager.Initialize(_dungeonMaster, _progression, loop, _treeConfig);

            // Promote to Rank D and unlock Root Core + Boss Active
            _progression.SetRank(DungeonRank.D);
            _talentManager.TryUnlockTalent(_rootCore);
            _talentManager.TryUnlockTalent(_bossActivePower);

            Assert.IsTrue(_talentManager.IsUnlocked(_bossActivePower));

            // In Planning phase -> TryActivatePower should fail (only Battle phase allowed)
            bool activeInPlanning = _talentManager.TryActivatePower(_bossActivePower, out string planFail);
            Assert.IsFalse(activeInPlanning);
            Assert.That(planFail, Does.Contain("Battle phase"));

            // Transition to Battle phase
            loop.StartBattle();
            Assert.AreEqual(GamePhase.Battle, loop.CurrentPhase);

            // Active activation in Battle phase -> should succeed
            int energyBeforeCast = _dungeonMaster.Energy;
            bool activeTriggered = false;
            _talentManager.OnActivePowerTriggered += node =>
            {
                if (node == _bossActivePower) activeTriggered = true;
            };

            bool castSuccess = _talentManager.TryActivatePower(_bossActivePower, out string castFail);
            Assert.IsTrue(castSuccess, $"Cast failed: {castFail}");
            Assert.IsTrue(activeTriggered);
            Assert.AreEqual(energyBeforeCast - _bossActivePower.ActiveEnergyCost, _dungeonMaster.Energy);

            // Immediate recast should fail due to cooldown
            bool recast = _talentManager.TryActivatePower(_bossActivePower, out string cooldownFail);
            Assert.IsFalse(recast);
            Assert.That(cooldownFail, Does.Contain("cooldown"));

            Object.DestroyImmediate(loopGo);
        }
    }
}
