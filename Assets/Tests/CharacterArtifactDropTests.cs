using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using UnityEngine;


namespace SBGames.Tests
{
    [TestFixture]
    public class CharacterArtifactDropTests
    {
        private GameObject _characterGo;
        private CharacterStatsManager _statsManager;
        private CharacterInventoryManager _inventoryManager;
        private ItemDefinition _artifactItem;

        [SetUp]
        public void Setup()
        {
            _characterGo = new GameObject("[Test_Character]");
            _characterGo.AddComponent<AnimationController>();
            _characterGo.AddComponent<CharacterBrain>();
            _statsManager = _characterGo.AddComponent<CharacterStatsManager>();
            _inventoryManager = _characterGo.AddComponent<CharacterInventoryManager>();
            _inventoryManager.InitializeInventory();

            _artifactItem = ItemDefinition.CreateRuntimeInstance(
                "test_artifact",
                "Sunfire Ring",
                ItemType.Artifact,
                EquipmentSlotType.Artifact
            );
        }

        [TearDown]
        public void Teardown()
        {
            if (_characterGo != null)
            {
                Object.DestroyImmediate(_characterGo);
            }

            if (_artifactItem != null)
            {
                Object.DestroyImmediate(_artifactItem);
            }
        }

        [Test]
        public void TryDropEquippedArtifacts_WhenChanceIsOne_UnequipsAndFiresEvents()
        {

            // Equip artifact in slot 4 (Artifact slot)
            bool equipped = _inventoryManager.Equip(4, _artifactItem, out _, out _);
            Assert.IsTrue(equipped);
            Assert.AreEqual(_artifactItem, _inventoryManager.GetEquippedItem(4));

            bool instanceEventFired = false;
            ItemDefinition instanceDroppedItem = null;
            Vector3 instanceDropPosition = Vector3.zero;

            bool staticEventFired = false;
            ItemDefinition staticDroppedItem = null;

            _inventoryManager.OnArtifactDropped += (manager, item, pos) =>
            {
                instanceEventFired = true;
                instanceDroppedItem = item;
                instanceDropPosition = pos;
            };

            CharacterInventoryManager.OnAnyArtifactDropped += (manager, item, pos) =>
            {
                staticEventFired = true;
                staticDroppedItem = item;
            };

            List<ItemDefinition> dropped = _inventoryManager.TryDropEquippedArtifacts(dropChance: 1.0f);

            Assert.AreEqual(1, dropped.Count);
            Assert.AreEqual(_artifactItem, dropped[0]);
            Assert.IsNull(_inventoryManager.GetEquippedItem(4), "Artifact slot should be empty after dropping");
            Assert.IsTrue(instanceEventFired);
            Assert.AreEqual(_artifactItem, instanceDroppedItem);
            Assert.IsTrue(staticEventFired);
            Assert.AreEqual(_artifactItem, staticDroppedItem);
        }

        [Test]
        public void TryDropEquippedArtifacts_WhenChanceIsZero_KeepsArtifactEquipped()
        {

            bool equipped = _inventoryManager.Equip(4, _artifactItem, out _, out _);
            Assert.IsTrue(equipped);

            bool instanceEventFired = false;
            _inventoryManager.OnArtifactDropped += (manager, item, pos) => instanceEventFired = true;

            List<ItemDefinition> dropped = _inventoryManager.TryDropEquippedArtifacts(dropChance: 0.0f);

            Assert.AreEqual(0, dropped.Count);
            Assert.IsFalse(instanceEventFired);
            Assert.AreEqual(_artifactItem, _inventoryManager.GetEquippedItem(4), "Artifact should remain equipped when drop roll fails");
        }

        [Test]
        public void OnCharacterDeath_WhenAutoDropEnabled_DropsEquippedArtifact()
        {
            _inventoryManager.ArtifactDropChance = 1.0f;
            _inventoryManager.AutoDropOnDeath = true;

            bool equipped = _inventoryManager.Equip(4, _artifactItem, out _, out _);
            Assert.IsTrue(equipped, "Equip should succeed");
            Assert.AreEqual(_artifactItem, _inventoryManager.GetEquippedItem(4));

            bool deathFired = false;
            _statsManager.OnCharacterDeath += s => deathFired = true;

            bool dropped = false;
            _inventoryManager.OnArtifactDropped += (mgr, item, pos) => dropped = true;

            // Trigger death
            _statsManager.ForceDeath();

            Assert.IsTrue(deathFired, "OnCharacterDeath should have fired");
            Assert.IsTrue(dropped, "Artifact should have dropped when character died");
        }
    }
}
