using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Database;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using SBGames.Inventory.Save;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class RuntimeInventorySystemTests
    {
        private GameObject _managerGo;
        private InventoryManager _inventoryManager;
        private ItemDatabaseSO _itemDatabase;
        private SimpleDungeonPhaseManager _phaseManager;

        private GameObject _heroGo;
        private CharacterInventoryManager _heroManager;

        private ItemDataSO _healingPotion;
        private ItemDataSO _fireRune;
        private ItemDataSO _iceRune;
        private ItemDataSO _ironSword;

        [SetUp]
        public void Setup()
        {
            // 1. Create test items
            _healingPotion = ItemDataSO.CreateRuntimeInstance(
                id: "item_potion_hp",
                itemName: "Healing Potion",
                itemType: ItemType.Consumable,
                maxStack: 10,
                baseValue: 15
            );

            _fireRune = ItemDataSO.CreateRuntimeInstance(
                id: "rune_fire_burst",
                itemName: "Fire Burst Rune",
                itemType: ItemType.Rune,
                maxStack: 1,
                baseValue: 50,
                slotType: EquipmentSlotType.Rune
            );

            _iceRune = ItemDataSO.CreateRuntimeInstance(
                id: "rune_frost_bite",
                itemName: "Frost Bite Rune",
                itemType: ItemType.Rune,
                maxStack: 1,
                baseValue: 60,
                slotType: EquipmentSlotType.Rune
            );

            _ironSword = ItemDataSO.CreateRuntimeInstance(
                id: "item_iron_sword",
                itemName: "Iron Sword",
                itemType: ItemType.Artifact,
                maxStack: 1,
                baseValue: 100,
                slotType: EquipmentSlotType.Artifact
            );

            // 2. Setup Item Database
            _itemDatabase = ScriptableObject.CreateInstance<ItemDatabaseSO>();
            _itemDatabase.SetItemsEditor(new List<ItemDataSO> { _healingPotion, _fireRune, _iceRune, _ironSword });

            // 3. Setup Phase Manager
            _phaseManager = new SimpleDungeonPhaseManager(DungeonPhase.PlanningPhase);

            // 4. Setup InventoryManager GameObject
            _managerGo = new GameObject("[Test_InventoryManager]");
            _inventoryManager = _managerGo.AddComponent<InventoryManager>();
            _inventoryManager.SetItemDatabase(_itemDatabase);
            _inventoryManager.SetPhaseManager(_phaseManager);
            _inventoryManager.InitializeInventory(10);

            // 5. Setup Character
            _heroGo = new GameObject("[Test_Hero]");
            _heroManager = _heroGo.AddComponent<CharacterInventoryManager>();
            _heroManager.InitializeInventory();
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            if (_heroGo != null) Object.DestroyImmediate(_heroGo);
            if (_itemDatabase != null) Object.DestroyImmediate(_itemDatabase);

            if (_healingPotion != null) Object.DestroyImmediate(_healingPotion);
            if (_fireRune != null) Object.DestroyImmediate(_fireRune);
            if (_iceRune != null) Object.DestroyImmediate(_iceRune);
            if (_ironSword != null) Object.DestroyImmediate(_ironSword);
        }

        [Test]
        public void AddItem_StackableConsumables_FillsExistingStackAndRespectsMaxStack()
        {
            var res1 = _inventoryManager.AddItem(_healingPotion, 6);
            Assert.IsTrue(res1.IsSuccess);
            Assert.AreEqual(6, _inventoryManager.GetTotalQuantity("item_potion_hp"));
            Assert.AreEqual(6, _inventoryManager.GetSlot(0).Quantity);

            var res2 = _inventoryManager.AddItem(_healingPotion, 7);
            Assert.IsTrue(res2.IsSuccess);
            Assert.AreEqual(13, _inventoryManager.GetTotalQuantity("item_potion_hp"));
            Assert.AreEqual(10, _inventoryManager.GetSlot(0).Quantity);
            Assert.AreEqual(3, _inventoryManager.GetSlot(1).Quantity);
        }

        [Test]
        public void HasItem_O1Lookup_ReturnsAccurateResultsWithoutAllocating()
        {
            _inventoryManager.AddItem(_healingPotion, 5);

            Assert.IsTrue(_inventoryManager.HasItem("item_potion_hp", 5));
            Assert.IsTrue(_inventoryManager.HasItem("item_potion_hp", 3));
            Assert.IsFalse(_inventoryManager.HasItem("item_potion_hp", 6));
            Assert.IsFalse(_inventoryManager.HasItem("non_existing_item", 1));
        }

        [Test]
        public void RemoveItem_AcrossMultipleSlots_UpdatesQuantitiesAndLookupMaps()
        {
            _inventoryManager.AddItem(_healingPotion, 15);

            var res = _inventoryManager.RemoveItem("item_potion_hp", 8);
            Assert.IsTrue(res.IsSuccess);
            Assert.AreEqual(7, _inventoryManager.GetTotalQuantity("item_potion_hp"));
            Assert.AreEqual(7, _inventoryManager.GetSlot(0).Quantity);
            Assert.IsTrue(_inventoryManager.GetSlot(1).IsEmpty);
        }

        [Test]
        public void SwapSlots_And_SplitSlots_BehaveCorrectly()
        {
            _inventoryManager.AddItem(_healingPotion, 10);

            var splitRes = _inventoryManager.SplitSlot(0, 2, 4);
            Assert.IsTrue(splitRes.IsSuccess);
            Assert.AreEqual(6, _inventoryManager.GetSlot(0).Quantity);
            Assert.AreEqual(4, _inventoryManager.GetSlot(2).Quantity);

            var swapRes = _inventoryManager.SwapSlots(2, 5);
            Assert.IsTrue(swapRes.IsSuccess);
            Assert.IsTrue(_inventoryManager.GetSlot(2).IsEmpty);
            Assert.AreEqual(4, _inventoryManager.GetSlot(5).Quantity);
        }

        [Test]
        public void PhaseGuard_BlocksMutationsInCombatPhase_UnlessForced()
        {
            _phaseManager.SetPhase(DungeonPhase.CombatPhase);
            Assert.IsTrue(_inventoryManager.IsCombatLocked);

            var failRes = _inventoryManager.AddItem(_healingPotion, 1);
            Assert.IsFalse(failRes.IsSuccess);
            Assert.AreEqual(InventoryOperationStatus.LockedInCombat, failRes.Status);
            Assert.IsFalse(_inventoryManager.HasItem("item_potion_hp", 1));

            var forcedRes = _inventoryManager.AddItem(_healingPotion, 1, force: true);
            Assert.IsTrue(forcedRes.IsSuccess);
            Assert.IsTrue(_inventoryManager.HasItem("item_potion_hp", 1));

            _phaseManager.SetPhase(DungeonPhase.PlanningPhase);
            Assert.IsFalse(_inventoryManager.IsCombatLocked);

            var normalRes = _inventoryManager.AddItem(_healingPotion, 2);
            Assert.IsTrue(normalRes.IsSuccess);
            Assert.AreEqual(3, _inventoryManager.GetTotalQuantity("item_potion_hp"));
        }

        [Test]
        public void InscribeRune_DirectlyToCharacter_OverwritingDestroysPreviousRune()
        {
            _inventoryManager.AddItem(_fireRune, 1);
            _inventoryManager.AddItem(_iceRune, 1);

            int fireSlotIdx = _inventoryManager.Slots[0].ItemID == "rune_fire_burst" ? 0 : 1;
            int iceSlotIdx = fireSlotIdx == 0 ? 1 : 0;

            // Inscribe Fire Rune to hero Slot 0 (Rune Slot 1)
            bool inscribeFire = _inventoryManager.EquipToCharacter(fireSlotIdx, _heroManager, targetSlotIndex: 0);
            Assert.IsTrue(inscribeFire);
            Assert.AreEqual(_fireRune, _heroManager.GetEquippedItem(0));
            Assert.IsFalse(_inventoryManager.HasItem("rune_fire_burst", 1));

            // Inscribe Ice Rune over Slot 0 -> replaces and destroys Fire Rune
            bool inscribeIce = _inventoryManager.EquipToCharacter(iceSlotIdx, _heroManager, targetSlotIndex: 0, confirmDestroyRune: true);
            Assert.IsTrue(inscribeIce);
            Assert.AreEqual(_iceRune, _heroManager.GetEquippedItem(0));
            Assert.IsFalse(_inventoryManager.HasItem("rune_frost_bite", 1));
            // Fire rune is destroyed forever, not in inventory
            Assert.IsFalse(_inventoryManager.HasItem("rune_fire_burst", 1));
        }

        [Test]
        public void Artifact_EquipAndUnequip_TransfersBackToInventory()
        {
            _inventoryManager.AddItem(_ironSword, 1);
            Assert.IsTrue(_inventoryManager.HasItem("item_iron_sword", 1));

            // Equip sword to hero (Artifact slot index 4)
            bool equipRes = _inventoryManager.EquipToCharacter(0, _heroManager, targetSlotIndex: 4);
            Assert.IsTrue(equipRes);
            Assert.AreEqual(_ironSword, _heroManager.GetEquippedItem(4));
            Assert.IsFalse(_inventoryManager.HasItem("item_iron_sword", 1));

            // Unequip sword back to inventory
            bool unequipRes = _inventoryManager.UnequipFromCharacter(_heroManager, characterSlotIndex: 4, out _);
            Assert.IsTrue(unequipRes);
            Assert.IsNull(_heroManager.GetEquippedItem(4));
            Assert.IsTrue(_inventoryManager.HasItem("item_iron_sword", 1));
        }

        [Test]
        public void SaveAndLoad_SerializesAndRestoresInventoryAndCharacterEquipment()
        {
            _inventoryManager.AddItem(_healingPotion, 8);
            _inventoryManager.AddItem(_fireRune, 1);
            _inventoryManager.AddItem(_ironSword, 1);

            // Inscribe fire rune and equip artifact to hero
            _inventoryManager.EquipToCharacter(1, _heroManager, targetSlotIndex: 0);
            _inventoryManager.EquipToCharacter(2, _heroManager, targetSlotIndex: 4);

            var heroes = new List<CharacterInventoryManager> { _heroManager };

            // Save to JSON
            string json = _inventoryManager.ToJson(heroes, prettyPrint: true);
            Assert.IsNotEmpty(json);

            // Clear inventory and hero equipment
            _inventoryManager.Clear();
            _heroManager.Inventory.GetSlot(0).Clear(out _);
            _heroManager.Inventory.GetSlot(4).Clear(out _);

            Assert.AreEqual(0, _inventoryManager.GetTotalQuantity("item_potion_hp"));
            Assert.IsNull(_heroManager.GetEquippedItem(0));
            Assert.IsNull(_heroManager.GetEquippedItem(4));

            // Restore from JSON
            var loadRes = _inventoryManager.LoadFromJson(json, heroes);
            Assert.IsTrue(loadRes.IsSuccess);

            // Verify inventory restored
            Assert.AreEqual(8, _inventoryManager.GetTotalQuantity("item_potion_hp"));

            // Verify character equipment restored
            Assert.AreEqual(_fireRune, _heroManager.GetEquippedItem(0));
            Assert.AreEqual(_ironSword, _heroManager.GetEquippedItem(4));
        }
    }
}
