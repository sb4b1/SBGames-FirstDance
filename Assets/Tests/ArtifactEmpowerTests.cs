using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
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
                startingArtifacts: new[] { new Artifact("Relic of Power", 1, 0) },
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
            Assert.AreEqual("Relic of Power", artifact.ArtifactName);
            Assert.AreEqual(1, artifact.PowerLevel);

            int startEnergy = _dungeonMaster.Energy;
            bool success = _dungeonMaster.EmpowerArtifact(artifact, energyCost: 15, powerIncrease: 1);

            Assert.IsTrue(success);
            Assert.IsTrue(artifact.IsEmpowered);
            Assert.AreEqual("Empowered Relic of Power", artifact.ArtifactName);
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
            Assert.AreEqual("Empowered Relic of Power", artifact.ArtifactName);
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
            Assert.AreEqual("Empowered Relic of Power", artifact.ArtifactName, "Prefix should not be duplicated.");
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
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            var charGo = new GameObject("[Test_Hero]");
            charGo.AddComponent<AnimationController>();
            var statsManager = charGo.AddComponent<CharacterStatsManager>();
            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            var artifactItem = ItemDataSO.CreateRuntimeInstance(
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

            // Empowering DM artifact should auto-unequip it from character and deposit into user inventory
            bool empowerSuccess = _dungeonMaster.EmpowerArtifact(dmArtifact, energyCost: 15);
            Assert.IsTrue(empowerSuccess);
            Assert.IsNull(charInvMgr.GetEquippedItem(4), "Artifact slot should be cleared after empowerment!");
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount, "Artifact must NOT be deleted when empowered while equipped!");
            Assert.IsTrue(_dungeonMaster.Artifacts[0].IsEmpowered, "Artifact must be empowered after auto-unequip!");
            Assert.AreEqual("Empowered Blade of Wrath", _dungeonMaster.Artifacts[0].ArtifactName);
            Assert.AreEqual(_dungeonMaster.Artifacts[0], _dungeonMaster.GetActiveEmpoweredArtifact());
            Assert.IsFalse(userInv.GetSlot(0).IsEmpty, "Unequipped artifact must be placed into user inventory!");

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(charGo);
            Object.DestroyImmediate(artifactItem);
        }

        [Test]
        public void UserInventory_AddingArtifact_AutomaticallySyncsToDungeonMaster()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);
            Assert.AreEqual(0, _dungeonMaster.ArtifactCount);

            var boots = ItemDataSO.CreateRuntimeInstance("art_boots", "Hermes Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            userInv.AddItem(boots, 1);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            Assert.AreEqual("Hermes Boots", _dungeonMaster.Artifacts[0].ArtifactName);

            // Removing from user inventory syncs removal
            userInv.RemoveItem(boots, 1);
            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(0, _dungeonMaster.ArtifactCount);

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(boots);
        }

        [Test]
        public void DungeonMaster_ForgeArtifact_SpawnsItemInUserInventory()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var forged = _dungeonMaster.ForgeArtifact("Forged Shield", essenceCost: 25, startingPower: 1);
            Assert.IsNotNull(forged);
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);

            // Verify item was created in User Inventory
            var slot = userInv.GetSlot(0);
            Assert.IsFalse(slot.IsEmpty);
            Assert.AreEqual("Forged Shield", slot.Item.ItemName);
            Assert.AreEqual(ItemType.Artifact, slot.Item.ItemType);

            // Sacrificing removes from User Inventory
            _dungeonMaster.SacrificeArtifact(forged, energyReward: 25);
            Assert.AreEqual(0, _dungeonMaster.ArtifactCount);
            Assert.IsTrue(userInv.GetSlot(0).IsEmpty);

            Object.DestroyImmediate(userInvGo);
        }

        [Test]
        public void DungeonMaster_SpawnStartingArtifactIfNeeded_SpawnsIntoInventory()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);
            Assert.AreEqual(0, _dungeonMaster.ArtifactCount);

            _dungeonMaster.SpawnStartingArtifactIfNeeded();

            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            Assert.IsFalse(userInv.GetSlot(0).IsEmpty);
            Assert.AreEqual(ItemType.Artifact, userInv.GetSlot(0).Item.ItemType);

            Object.DestroyImmediate(userInvGo);
        }

        [Test]
        public void MultipleArtifactsOfSameId_InInventory_AllTrackedAndDisplayedIndividually()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);
            Assert.AreEqual(0, _dungeonMaster.ArtifactCount);

            var bootsA = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            var bootsB = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);

            userInv.AddItem(bootsA, 1);
            userInv.AddItem(bootsB, 1);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(2, _dungeonMaster.ArtifactCount, "Both duplicate artifacts of the same ID must be tracked individually");
            Assert.AreEqual("[Inventory]", _dungeonMaster.Artifacts[0].LocationBadge);
            Assert.AreEqual("[Inventory]", _dungeonMaster.Artifacts[1].LocationBadge);

            // Sacrificing one duplicate removes only one from DM and inventory
            var firstArt = _dungeonMaster.Artifacts[0];
            bool sacrificed = _dungeonMaster.SacrificeArtifact(firstArt, energyReward: 25);
            Assert.IsTrue(sacrificed);
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(bootsA);
            Object.DestroyImmediate(bootsB);
        }

        [Test]
        public void MultipleArtifactsOfSameId_EquippedAndInventory_ShowsDistinctLocationBadges()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            var charGo = new GameObject("[Test_Hero]");
            charGo.AddComponent<AnimationController>();
            charGo.AddComponent<CharacterStatsManager>();
            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var bootsInBag = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            var bootsEquipped = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);

            userInv.AddItem(bootsInBag, 1);
            charInvMgr.Equip(4, bootsEquipped, out _, out _);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(2, _dungeonMaster.ArtifactCount);

            var invArt = System.Linq.Enumerable.FirstOrDefault(_dungeonMaster.Artifacts, a => a.LocationBadge == "[Inventory]");
            var eqArt = System.Linq.Enumerable.FirstOrDefault(_dungeonMaster.Artifacts, a => a.LocationBadge.Contains("Equipped"));

            Assert.IsNotNull(invArt, "Should find inventory artifact");
            Assert.IsNotNull(eqArt, "Should find equipped artifact");

            // Empowering the equipped artifact auto-unequips it
            bool empowered = _dungeonMaster.EmpowerArtifact(eqArt, energyCost: 15);
            Assert.IsTrue(empowered);
            Assert.IsTrue(eqArt.IsEmpowered);
            Assert.IsFalse(invArt.IsEmpowered);
            Assert.IsNull(charInvMgr.GetEquippedItem(4), "Equipped artifact should be auto-unequipped after empower");

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(charGo);
            Object.DestroyImmediate(bootsInBag);
            Object.DestroyImmediate(bootsEquipped);
        }

        [Test]
        public void EmpowerArtifact_DoesNotMutateInventoryItemDataSO()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var artifactSO = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            userInv.AddItem(artifactSO, 1);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            var dmArtifact = _dungeonMaster.Artifacts[0];

            // Empower artifact in DM
            bool empowered = _dungeonMaster.EmpowerArtifact(dmArtifact, energyCost: 15);
            Assert.IsTrue(empowered);
            Assert.AreEqual("Empowered Brown Boots", dmArtifact.ArtifactName);

            // ItemDataSO in Inventory must remain completely unchanged (immutable)
            var slot = userInv.GetSlot(0);
            Assert.IsFalse(slot.IsEmpty);
            Assert.AreEqual("Brown Boots", slot.Item.DisplayName, "ItemDataSO DisplayName must not be mutated");
            Assert.AreEqual("Brown Boots", artifactSO.DisplayName, "Original ScriptableObject DisplayName must not be mutated");

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(artifactSO);
        }

        [Test]
        public void EmpowerArtifact_WhenAnotherArtifactAlreadyEmpowered_UnempowersOldAndEmpowersNew()
        {
            var mod1 = new ItemStatModifierData(StatType.Attack, 10f, StatModType.Flat);
            var mod2 = new ItemStatModifierData(StatType.Defense, 5f, StatModType.Flat);

            var art1 = new Artifact("Sun Blade", 1, 0, null, false, new[] { mod1 });
            var art2 = new Artifact("Moon Shield", 1, 0, null, false, new[] { mod2 });

            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: new[] { art1, art2 },
                talentMgr: _talentManager
            );

            // 1. Empower first artifact
            bool emp1 = _dungeonMaster.EmpowerArtifact(art1, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(emp1);
            Assert.IsTrue(art1.IsEmpowered);
            Assert.AreEqual("Empowered Sun Blade", art1.ArtifactName);
            Assert.AreEqual(2, art1.PowerLevel);
            Assert.IsFalse(art2.IsEmpowered);
            Assert.AreEqual(art1, _dungeonMaster.GetActiveEmpoweredArtifact());

            // Global stats should be only art1 (Attack +10)
            var mods = _dungeonMaster.GetGlobalArtifactStatModifiers();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.Attack, mods[0].StatType);

            // 2. Empower second artifact
            bool emp2 = _dungeonMaster.EmpowerArtifact(art2, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(emp2);

            // art2 is now empowered
            Assert.IsTrue(art2.IsEmpowered);
            Assert.AreEqual("Empowered Moon Shield", art2.ArtifactName);
            Assert.AreEqual(2, art2.PowerLevel);
            Assert.AreEqual(art2, _dungeonMaster.GetActiveEmpoweredArtifact());

            // art1 should have lost empowerment, reverted prefix, but preserved accumulated power level
            Assert.IsFalse(art1.IsEmpowered);
            Assert.AreEqual("Sun Blade", art1.ArtifactName);
            Assert.AreEqual(2, art1.PowerLevel, "Power level should be preserved upon unempowerment");

            // Global stats should now only be art2 (Defense +5)
            mods = _dungeonMaster.GetGlobalArtifactStatModifiers();
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(StatType.Defense, mods[0].StatType);
        }

        [Test]
        public void EmpowerArtifact_FiresOnArtifactUnempoweredEventForPreviousArtifact()
        {
            var art1 = new Artifact("Sun Blade", 1, 0);
            var art2 = new Artifact("Moon Shield", 1, 0);

            _dungeonMaster.Initialize(
                startingEssence: 100,
                startingEnergy: 100,
                startingArtifacts: new[] { art1, art2 },
                talentMgr: _talentManager
            );

            var unempoweredList = new List<Artifact>();
            _dungeonMaster.OnArtifactUnempowered += a => unempoweredList.Add(a);

            _dungeonMaster.EmpowerArtifact(art1, energyCost: 15);
            Assert.AreEqual(0, unempoweredList.Count);

            _dungeonMaster.EmpowerArtifact(art2, energyCost: 15);
            Assert.AreEqual(1, unempoweredList.Count);
            Assert.AreEqual(art1, unempoweredList[0]);
        }

        [Test]
        public void EmpowerArtifact_SameActiveArtifact_IncreasesPowerWithoutUnempowering()
        {
            var art = _dungeonMaster.Artifacts[0];

            // Unlock prerequisites and Overcharge talent
            _talentManager.TryUnlockTalent(_core01, out _);
            _talentManager.TryUnlockTalent(_util01, out _);
            _talentManager.TryUnlockTalent(_overchargeTalent, out _);

            var unempoweredList = new List<Artifact>();
            _dungeonMaster.OnArtifactUnempowered += a => unempoweredList.Add(a);

            _dungeonMaster.EmpowerArtifact(art, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(art.IsEmpowered);
            Assert.AreEqual(2, art.PowerLevel);
            Assert.AreEqual(0, unempoweredList.Count);

            // Re-empowering same active artifact
            _dungeonMaster.EmpowerArtifact(art, energyCost: 15, powerIncrease: 1);
            Assert.IsTrue(art.IsEmpowered);
            Assert.AreEqual(3, art.PowerLevel);
            Assert.AreEqual(0, unempoweredList.Count, "Should not unempower when upgrading the active empowered artifact");
        }

        [Test]
        public void MultipleArtifactsOfSameId_OnlyEmpoweredOneIsMarkedEmpowered()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var bootsData = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            userInv.AddItem(bootsData, 1);
            userInv.AddItem(bootsData, 1);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(2, _dungeonMaster.ArtifactCount);

            var art1 = _dungeonMaster.Artifacts[0];
            var art2 = _dungeonMaster.Artifacts[1];

            // Empower only the first artifact
            bool emp = _dungeonMaster.EmpowerArtifact(art1, energyCost: 15);
            Assert.IsTrue(emp);
            Assert.IsTrue(art1.IsEmpowered);
            Assert.AreEqual("Empowered Brown Boots", art1.ArtifactName);

            // Second artifact of same ItemDataSO ID must NOT be empowered
            Assert.IsFalse(art2.IsEmpowered);
            Assert.AreEqual("Brown Boots", art2.ArtifactName);

            // User inventory slots must still have clean base ItemDataSO
            Assert.AreEqual("Brown Boots", userInv.GetSlot(0).Item.DisplayName);
            Assert.AreEqual("Brown Boots", userInv.GetSlot(1).Item.DisplayName);

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(bootsData);
        }

        [Test]
        public void EmpoweredArtifact_EquipAndUnequip_PreservesEmpowermentState()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            var charGo = new GameObject("[Test_Hero]");
            charGo.AddComponent<AnimationController>();
            charGo.AddComponent<CharacterStatsManager>();
            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var bootsData = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_001", "Brown Boots", ItemType.Artifact, EquipmentSlotType.Artifact);
            userInv.AddItem(bootsData, 1);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            var dmArtifact = _dungeonMaster.Artifacts[0];

            // 1. Empower in bag
            bool emp = _dungeonMaster.EmpowerArtifact(dmArtifact, energyCost: 15);
            Assert.IsTrue(emp);
            Assert.IsTrue(dmArtifact.IsEmpowered);
            Assert.AreEqual("Empowered Brown Boots", dmArtifact.ArtifactName);

            // 2. Equip to character
            bool equipped = userInv.EquipToCharacter(0, charInvMgr, 4);
            Assert.IsTrue(equipped);
            _dungeonMaster.SyncWithInventory();

            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            Assert.IsTrue(dmArtifact.IsEmpowered, "Artifact must remain empowered while equipped");
            Assert.AreEqual("Empowered Brown Boots", dmArtifact.ArtifactName);
            Assert.IsTrue(dmArtifact.LocationBadge.Contains("Equipped"), "Location badge should reflect equipped status");

            // 3. Unequip back to inventory
            bool unequipped = userInv.UnequipFromCharacter(charInvMgr, 4, out _);
            Assert.IsTrue(unequipped);
            _dungeonMaster.SyncWithInventory();

            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            Assert.IsTrue(dmArtifact.IsEmpowered, "Artifact must remain empowered after being unequipped back to inventory");
            Assert.AreEqual("Empowered Brown Boots", dmArtifact.ArtifactName);
            Assert.AreEqual("[Inventory]", dmArtifact.LocationBadge);

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(charGo);
            Object.DestroyImmediate(bootsData);
        }

        [Test]
        public void EmpowerEquippedArtifact_ViaLiveSync_UnequipsAndEmpowers()
        {
            var userInvGo = new GameObject("[Test_UserInv]");
            var userInv = userInvGo.AddComponent<InventoryManager>();
            userInv.InitializeInventory(10);

            var charGo = new GameObject("[Test_Hero]");
            charGo.AddComponent<AnimationController>();
            charGo.AddComponent<CharacterStatsManager>();
            var charInvMgr = charGo.AddComponent<CharacterInventoryManager>();
            charInvMgr.InitializeInventory();

            _dungeonMaster.Initialize(startingEssence: 100, startingEnergy: 100, startingArtifacts: new Artifact[0]);

            var swordData = ItemDataSO.CreateRuntimeInstance("ITEM_ARTIFACT_SWORD", "Shadow Blade", ItemType.Artifact, EquipmentSlotType.Artifact);
            charInvMgr.Equip(4, swordData, out _, out _);

            _dungeonMaster.SyncWithInventory();
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            var equippedArtifact = _dungeonMaster.Artifacts[0];
            Assert.IsFalse(equippedArtifact.IsEmpowered);
            Assert.IsTrue(equippedArtifact.LocationBadge.Contains("Equipped"));

            // Empower the equipped unempowered artifact
            bool empowered = _dungeonMaster.EmpowerArtifact(equippedArtifact, energyCost: 15);
            Assert.IsTrue(empowered);

            // Verify it was unequipped from character and added to user bag
            Assert.IsNull(charInvMgr.GetEquippedItem(4), "Slot 4 on character should be empty");
            Assert.AreEqual(1, userInv.GetTotalQuantity(swordData.ItemID), "Item should now be in user inventory bag");

            // Verify DM artifact state is empowered, not reset or removed
            Assert.AreEqual(1, _dungeonMaster.ArtifactCount);
            var resultArtifact = _dungeonMaster.Artifacts[0];
            Assert.IsTrue(resultArtifact.IsEmpowered, "Artifact must be empowered!");
            Assert.AreEqual("Empowered Shadow Blade", resultArtifact.ArtifactName);
            Assert.AreEqual("[Inventory]", resultArtifact.LocationBadge);
            Assert.AreEqual(resultArtifact, _dungeonMaster.GetActiveEmpoweredArtifact());

            Object.DestroyImmediate(userInvGo);
            Object.DestroyImmediate(charGo);
            Object.DestroyImmediate(swordData);
        }
    }
}
