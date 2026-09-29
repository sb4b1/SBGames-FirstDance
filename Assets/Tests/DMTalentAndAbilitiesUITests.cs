using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Input;
using SBGames.Loop;
using SBGames.Loop.Talents;
using SBGames.Loop.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace SBGames.Tests
{
    [TestFixture]
    public class DMTalentAndAbilitiesUITests
    {
        private GameObject _holderGo;
        private DungeonMaster _dungeonMaster;
        private DungeonProgression _progression;
        private DMTalentTreeManager _talentManager;
        private DMTalentTreeUIController _talentUI;
        private DMAbilitiesUIController _abilitiesUI;
        private UIDocument _talentUIDoc;
        private UIDocument _abilitiesUIDoc;

        [SetUp]
        public void Setup()
        {
            _holderGo = new GameObject("[Test_UIHolder]");
            
            _dungeonMaster = _holderGo.AddComponent<DungeonMaster>();
            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: new[] { new Artifact("Test Anchor", 1, 0) }
            );

            _progression = _holderGo.AddComponent<DungeonProgression>();
            _progression.Initialize(startingRank: DungeonRank.E);

            _talentManager = _holderGo.AddComponent<DMTalentTreeManager>();
            _talentManager.Initialize(_dungeonMaster, _progression, null, null);

            var talentUIGo = new GameObject("[Test_TalentUI]");
            _talentUIDoc = talentUIGo.AddComponent<UIDocument>();
            _talentUI = talentUIGo.AddComponent<DMTalentTreeUIController>();

            var abilitiesUIGo = new GameObject("[Test_AbilitiesUI]");
            _abilitiesUIDoc = abilitiesUIGo.AddComponent<UIDocument>();
            _abilitiesUI = abilitiesUIGo.AddComponent<DMAbilitiesUIController>();
        }

        [TearDown]
        public void Teardown()
        {
            Time.timeScale = 1f;
            if (_holderGo != null) Object.DestroyImmediate(_holderGo);
            if (_talentUI != null) Object.DestroyImmediate(_talentUI.gameObject);
            if (_abilitiesUI != null) Object.DestroyImmediate(_abilitiesUI.gameObject);
        }

        [Test]
        public void TalentTreeUI_Toggle_OpensAndCloses_AndPausesTime()
        {
            Assert.IsFalse(_talentUI.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);

            // Open via Toggle
            _talentUI.Toggle();
            Assert.IsTrue(_talentUI.IsOpen);
            Assert.AreEqual(0f, Time.timeScale, "Time.timeScale should be 0 when Talent Tree is open");

            // Close via Toggle
            _talentUI.Toggle();
            Assert.IsFalse(_talentUI.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "Time.timeScale should restore to 1 when Talent Tree is closed");
        }

        [Test]
        public void DMAbilitiesUI_Toggle_OpensAndCloses_AndPausesTime()
        {
            Assert.IsFalse(_abilitiesUI.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);

            // Open via Toggle
            _abilitiesUI.Toggle();
            Assert.IsTrue(_abilitiesUI.IsOpen);
            Assert.AreEqual(0f, Time.timeScale, "Time.timeScale should be 0 when DM Abilities UI is open");

            // Close via Toggle
            _abilitiesUI.Toggle();
            Assert.IsFalse(_abilitiesUI.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "Time.timeScale should restore to 1 when DM Abilities UI is closed");
        }

        [Test]
        public void InputManager_UIButtonEnum_ContainsPandH()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIButton), "P"), "UIButton enum must contain 'P'");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIButton), "H"), "UIButton enum must contain 'H'");
        }

        [Test]
        public void InputManager_RegistrationAndMutualExclusion_WorksCorrectly()
        {
            var imGo = new GameObject("[Test_InputManager]");
            var inputManager = imGo.AddComponent<InputManager>();

            // Register both UIs
            inputManager.RegisterUISystem(UIButton.P, _talentUI);
            inputManager.RegisterUISystem(UIButton.H, _abilitiesUI);

            Assert.IsTrue(inputManager.IsUISystemRegistered(UIButton.P));
            Assert.IsTrue(inputManager.IsUISystemRegistered(UIButton.H));

            // Open Talent UI directly
            _talentUI.Open();
            Assert.IsTrue(_talentUI.IsOpen);

            // Open Abilities UI directly
            _abilitiesUI.Open();
            Assert.IsTrue(_abilitiesUI.IsOpen);

            inputManager.UnregisterUISystem(_talentUI);
            inputManager.UnregisterUISystem(_abilitiesUI);
            Object.DestroyImmediate(imGo);
        }

        [Test]
        public void DMAbilities_CoreOperations_ExecuteCorrectly()
        {
            // Test Recover Essence
            _dungeonMaster.LoseEssence(30);
            Assert.AreEqual(70, _dungeonMaster.Essence);
            int energyBefore = _dungeonMaster.Energy;

            bool recovered = _dungeonMaster.RecoverEssence(essenceAmount: 15, energyCost: 20);
            Assert.IsTrue(recovered);
            Assert.AreEqual(85, _dungeonMaster.Essence);
            Assert.AreEqual(energyBefore - 20, _dungeonMaster.Energy);

            // Test Forge Artifact
            int essenceBeforeForge = _dungeonMaster.Essence;
            var forged = _dungeonMaster.ForgeArtifact("New Relic", essenceCost: 25, startingPower: 1);
            Assert.IsNotNull(forged);
            Assert.AreEqual(essenceBeforeForge - 25, _dungeonMaster.Essence);
            Assert.AreEqual(2, _dungeonMaster.ArtifactCount);

            // Test Empower Artifact
            int energyBeforeEmpower = _dungeonMaster.Energy;
            bool empowered = _dungeonMaster.EmpowerArtifact(forged, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(empowered);
            Assert.AreEqual(2, forged.PowerLevel);
            Assert.AreEqual(energyBeforeEmpower - 15, _dungeonMaster.Energy);

            // Test Sacrifice Artifact
            int energyBeforeSacrifice = _dungeonMaster.Energy;
            bool sacrificed = _dungeonMaster.SacrificeArtifact(forged, energyReward: 25);
            Assert.IsTrue(sacrificed);
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            Assert.AreEqual(energyBeforeSacrifice + 25, _dungeonMaster.Energy);
        }
    }
}
