using System;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using UnityEngine;

namespace SBGames.Inventory.User
{
    /// <summary>
    /// MonoBehaviour managing the player's general inventory.
    /// Handles user inventory state, item consumption, and transfer transactions between
    /// User Inventory and Character Equipment slots.
    /// Fully decoupled from UI Toolkit or any presentation views.
    /// </summary>
    [DisallowMultipleComponent]
    public class UserInventoryManager : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField, Min(1)] private int defaultCapacity = 24;

        private Core.Inventory _inventory;

        public Core.Inventory Inventory => _inventory;
        public int Capacity => _inventory?.Capacity ?? defaultCapacity;

        public event Action<int, InventorySlot> OnSlotChanged;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            InitializeInventory();
        }

        public void InitializeInventory()
        {
            if (_inventory != null) return;

            _inventory = new Core.Inventory(defaultCapacity);
            _inventory.OnSlotChanged += (idx, slot) => OnSlotChanged?.Invoke(idx, slot);
            _inventory.OnInventoryChanged += () => OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Adds an item and quantity to the user inventory. Returns leftover amount not added.
        /// </summary>
        public int AddItem(ItemDefinition item, int amount = 1)
        {
            if (_inventory == null) InitializeInventory();
            return _inventory.AddItem(item, amount);
        }

        /// <summary>
        /// Removes an item quantity from the user inventory.
        /// </summary>
        public int RemoveItem(ItemDefinition item, int amount = 1)
        {
            if (_inventory == null) return 0;
            return _inventory.RemoveItem(item, amount);
        }

        /// <summary>
        /// Removes an item quantity from a specific slot.
        /// </summary>
        public int RemoveItemAt(int slotIndex, int amount = 1)
        {
            if (_inventory == null) return 0;
            return _inventory.RemoveItemAt(slotIndex, amount);
        }

        /// <summary>
        /// Uses a consumable item from a slot on a target character.
        /// Validates that the item is consumable and that the character can benefit from it.
        /// </summary>
        public bool ConsumeItem(int slotIndex, CharacterInventoryManager targetCharacter, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (_inventory == null || targetCharacter == null || targetCharacter.StatsManager == null)
            {
                resultMessage = "Invalid inventory or target character.";
                return false;
            }

            var slot = _inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                resultMessage = "Selected slot is empty.";
                return false;
            }

            var item = slot.Item;
            if (!item.IsConsumable || item.ConsumableEffect == null)
            {
                resultMessage = $"{item.ItemName} cannot be consumed.";
                return false;
            }

            var stats = targetCharacter.StatsManager;
            if (!item.ConsumableEffect.CanApply(targetCharacter.gameObject, stats, out string failureReason))
            {
                resultMessage = $"Cannot consume {item.ItemName}: {failureReason}";
                return false;
            }

            if (item.ConsumableEffect.Apply(targetCharacter.gameObject, stats))
            {
                _inventory.RemoveItemAt(slotIndex, 1);
                resultMessage = $"Consumed {item.ItemName} on {targetCharacter.CharacterDisplayName}.";
                return true;
            }

            resultMessage = $"Failed to apply effect of {item.ItemName}.";
            return false;
        }

        /// <summary>
        /// Equips an item from the user inventory to the target character.
        /// If targetSlotIndex is -1, selects the first available matching slot.
        /// Handles auto-swap for artifacts and destruction for runes.
        /// </summary>
        public bool EquipToCharacter(
            int userSlotIndex,
            CharacterInventoryManager targetCharacter,
            int targetSlotIndex = -1,
            bool confirmDestroyRune = true,
            string failureMessage = null)
        {
            if (_inventory == null || targetCharacter == null || targetCharacter.Inventory == null)
            {
                return false;
            }

            var userSlot = _inventory.GetSlot(userSlotIndex);
            if (userSlot == null || userSlot.IsEmpty)
            {
                return false;
            }

            var itemToEquip = userSlot.Item;
            if (!itemToEquip.IsEquippable)
            {
                return false;
            }

            var charInv = targetCharacter.Inventory;

            // Resolve target slot index
            if (targetSlotIndex < 0)
            {
                targetSlotIndex = charInv.FindFirstAvailableSlot(itemToEquip);
                if (targetSlotIndex < 0)
                {
                    // If no empty slot, target the first slot matching this equipment type
                    for (int i = 0; i < charInv.SlotCount; i++)
                    {
                        if (charInv.GetSlot(i).SlotType == itemToEquip.EquippableSlotType)
                        {
                            targetSlotIndex = i;
                            break;
                        }
                    }
                }
            }

            if (targetSlotIndex < 0)
            {
                return false;
            }

            var targetSlot = charInv.GetSlot(targetSlotIndex);
            if (targetSlot == null || !targetSlot.CanEquip(itemToEquip, out _))
            {
                return false;
            }

            // Remove 1 item from user inventory slot
            _inventory.RemoveItemAt(userSlotIndex, 1);

            // Attempt equip to character
            if (charInv.Equip(targetSlotIndex, itemToEquip, out var displacedItem, out _, confirmDestroyRune))
            {
                // If an artifact was displaced, add it back to User Inventory
                if (displacedItem != null)
                {
                    _inventory.AddItem(displacedItem, 1);
                }

                return true;
            }

            // Rollback if equip failed
            _inventory.AddItem(itemToEquip, 1);
            return false;
        }

        /// <summary>
        /// Unequips an item from a character and transfers it back into the User Inventory.
        /// Fails if the user inventory has no available space.
        /// </summary>
        public bool UnequipFromCharacter(
            CharacterInventoryManager targetCharacter,
            int characterSlotIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (_inventory == null || targetCharacter == null || targetCharacter.Inventory == null)
            {
                resultMessage = "Invalid inventory or character reference.";
                return false;
            }

            var charSlot = targetCharacter.Inventory.GetSlot(characterSlotIndex);
            if (charSlot == null || !charSlot.IsOccupied)
            {
                resultMessage = "Selected equipment slot is empty.";
                return false;
            }

            var itemToUnequip = charSlot.EquippedItem;

            // Check if user inventory can accept the unequipped item
            if (!_inventory.CanAddItem(itemToUnequip, 1))
            {
                resultMessage = "User inventory is full! Cannot unequip item.";
                return false;
            }

            if (targetCharacter.Inventory.Unequip(characterSlotIndex, out var unequippedItem))
            {
                _inventory.AddItem(unequippedItem, 1);
                resultMessage = $"Unequipped {unequippedItem.ItemName} to inventory.";
                return true;
            }

            resultMessage = "Failed to unequip item.";
            return false;
        }
    }
}
