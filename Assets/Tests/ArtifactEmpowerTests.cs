using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Stats;
using SBGames.Loop;
using SBGames.Loop.Talents;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class ArtifactEmpowerTests
    {
        private GameObject _holderGo;
        private DungeonMaster _dungeonMaster;
        private DMTalentTreeManager _talentManager;
        private DungeonProgression _progression;
        private TalentNodeSO _core01;
        private TalentNodeSO _util01;
        private TalentNodeSO _overchargeTalent;
        private TalentTreeConfigSO _treeConfig;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_ArtifactEmpower]");
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _progression = _holderGo.AddComponent<DungeonProgression>();
            _progression.Initialize(startingRank: DungeonRank.C);

            _talentManager = _holderGo.AddComponent<DMTalentTreeManager>();

            // Setup talents
            _core01 = ScriptableObject.CreateInstance<TalentNodeSO>();
            _core01.Initialize(
                id: "DM_CORE_01",
                name: "Dungeon Heart Awakening",
                cost: 15,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier0,
                minRank: DungeonRank.E
            );

            _util01 = ScriptableObject.CreateInstance<TalentNodeSO>();
            _util01.Initialize(
                id: "DM_UTILITY_01",
                name: "Relic Transmutation",
                cost: 20,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.D,
                prereqs: new[] { _core01 }
            );

            _overchargeTalent = ScriptableObject.CreateInstance<TalentNodeSO>();
            _overchargeTalent.Initialize(
                id: "DM_ARTIFACT_OVERCHARGE",
                name: "Relic Overcharge",
                cost: 30,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.C,
                prereqs: new[] { _util01 }
            );

            _treeConfig = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
            _treeConfig.Initialize("Test Tree", new[] { _core01, _util01, _overchargeTalent });

            _talentManager.Initialize(
                dm: _dungeonMaster,
                progression: _progression,
                loop: null,
                config: _treeConfig
            );

            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 200,
                startingArtifacts: null,
                roundLossPenalty: 25,
                maximumEssence: 100,
                maximumEnergy: 200,
                talentMgr: _talentManager
            );
        }

        [TearDown]
        public void Teardown()
        {
            if (_holderGo != null)
            {
                Object.DestroyImmediate(_holderGo);
            }
            if (_core01 != null) Object.DestroyImmediate(_core01);
            if (_util01 != null) Object.DestroyImmediate(_util01);
            if (_overchargeTalent != null) Object.DestroyImmediate(_overchargeTalent);
            if (_treeConfig != null) Object.DestroyImmediate(_treeConfig);
        }

        [Test]
        public void FirstEmpower_SetsIsEmpowered_AddsPrefix_AndIncreasesPower()
        {
            var artifact = _dungeonMaster.Artifacts[0];
            Assert.IsFalse(artifact.IsEmpowered);
            Assert.AreEqual("Master's Heart", artifact.ArtifactName);
            Assert.AreEqual(1, artifact.PowerLevel);

            int startEnergy = _dungeonMaster.Energy;
            bool success = _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);

            Assert.IsTrue(success);
            Assert.IsTrue(artifact.IsEmpowered);
            Assert.AreEqual("Empowered Master's Heart", artifact.ArtifactName);
            Assert.AreEqual(2, artifact.PowerLevel);
            Assert.AreEqual(startEnergy - 15, _dungeonMaster.Energy);
        }

        [Test]
        public void SecondEmpower_WithoutTalent_Fails()
        {
            var artifact = _dungeonMaster.Artifacts[0];
            _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(artifact.IsEmpowered);

            int energyBeforeSecondEmpower = _dungeonMaster.Energy;
            bool secondSuccess = _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);

            Assert.IsFalse(secondSuccess, "Should fail to empower multiple times without Relic Overcharge talent.");
            Assert.AreEqual(2, artifact.PowerLevel);
            Assert.AreEqual(energyBeforeSecondEmpower, _dungeonMaster.Energy);
        }

        [Test]
        public void SecondEmpower_WithTalent_Succeeds_AndDoesNotDuplicatePrefix()
        {
            var artifact = _dungeonMaster.Artifacts[0];
            _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);
            Assert.AreEqual("Empowered Master's Heart", artifact.ArtifactName);
            Assert.AreEqual(2, artifact.PowerLevel);

            // Unlock prerequisites and Overcharge talent
            _talentManager.TryUnlockTalent(_core01, out _);
            _talentManager.TryUnlockTalent(_util01, out _);
            bool unlockedOvercharge = _talentManager.TryUnlockTalent(_overchargeTalent, out _);
            Assert.IsTrue(unlockedOvercharge);
            Assert.IsTrue(_dungeonMaster.CanMultiEmpower());

            int energyBefore = _dungeonMaster.Energy;
            bool secondSuccess = _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);

            Assert.IsTrue(secondSuccess, "Should succeed with Relic Overcharge talent unlocked.");
            Assert.AreEqual(3, artifact.PowerLevel);
            Assert.AreEqual("Empowered Master's Heart", artifact.ArtifactName, "Prefix should not be duplicated.");
            Assert.AreEqual(energyBefore - 15, _dungeonMaster.Energy);
        }

        [Test]
        public void GlobalArtifactModifiers_OnlyIncludeEmpoweredArtifacts()
        {
            var mod1 = new ItemStatModifierData(StatType.Attack, 10f, StatModType.Flat);
            var mod2 = new ItemStatModifierData(StatType.Defense, 5f, StatModType.Flat);

            var normalArtifact = new Artifact("Shadow Ring", 1, 0, null, false, new[] { mod1 });
            var empoweredArtifact = new Artifact("Flame Brand", 1, 0, null, false, new[] { mod2 });

            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: new[] { normalArtifact, empoweredArtifact },
                talentMgr: _talentManager
            );

            // Initially neither is empowered
            var globalMods = _dungeonMaster.GetGlobalArtifactStatModifiers();
            Assert.AreEqual(0, globalMods.Count);
            Assert.AreEqual(0, _dungeonMaster.GetTotalEmpoweredArtifactPower());

            // Empower one artifact
            _dungeonMaster.EmpowerArtifact(empoweredArtifact, energyCost: 15);

            globalMods = _dungeonMaster.GetGlobalArtifactStatModifiers();
            Assert.AreEqual(1, globalMods.Count);
            Assert.AreEqual(StatType.Defense, globalMods[0].StatType);
            Assert.AreEqual(5f, globalMods[0].Value);
            Assert.AreEqual(2, _dungeonMaster.GetTotalEmpoweredArtifactPower());
        }

        [Test]
        public void EmpowerArtifact_AutoUnequipsFromCharacterInventory()
        {
            var charGo = new GameObject("[Test_Hero]");
            charGo.AddComponent<AnimationController>();
            var statsManager = charGo.AddComponent<CharacterStatsManager>();
            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            var artifactItem = ItemDefinition.CreateRuntimeInstance(
                "art_001",
                "Blade of Wrath",
                ItemType.Artifact,
                EquipmentSlotType.Artifact
            );

            // Equip into character artifact slot (slot 4)
            bool equipped = charInvMgr.Equip(4, artifactItem, out _, out _);
            Assert.IsTrue(equipped);
            Assert.AreEqual(artifactItem, charInvMgr.GetEquippedItem(4));

            // Create matching artifact in DM
            var dmArtifact = new Artifact("Blade of Wrath", 1, 0, "art_001");
            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: new[] { dmArtifact },
                talentMgr: _talentManager
            );

            // Empowering DM artifact should auto-unequip it from character
            bool empowerSuccess = _dungeonMaster.EmpowerArtifact(dmArtifact, energyCost: 15);
            Assert.IsTrue(empowerSuccess);
            Assert.IsNull(charInvMgr.GetEquippedItem(4), "Artifact slot should be cleared after empowerment!");

            Object.DestroyImmediate(charGo);
            Object.DestroyImmediate(artifactItem);
        }
    }
}
