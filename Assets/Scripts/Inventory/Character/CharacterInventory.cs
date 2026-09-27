using System;
using System.Collections.Generic;
using SBGames.Inventory.Core;

namespace SBGames.Inventory.Character
{
    /// <summary>
    /// Pure C# model managing a character's equipped items.
    /// Configured with typed slots (default: 4 Rune slots, 1 Artifact slot).
    /// Enforces equipment rules:
    /// - Artifacts: Auto-swapped back to User Inventory.
    /// - Runes: Inscribed/overwritten, destroying the displaced rune upon user confirmation.
    /// Decoupled from UI and Unity components; fires events when equipment changes.
    /// </summary>
    public class CharacterInventory
    {
        private readonly List<EquipmentSlot> _slots;

        public IReadOnlyList<EquipmentSlot> Slots => _slots;
        public int SlotCount => _slots.Count;

        /// <summary>
        /// Fired when an item is equipped: (slotIndex, slot, equippedItem).
        /// </summary>
        public event Action<int, EquipmentSlot, ItemDefinition> OnItemEquipped;

        /// <summary>
        /// Fired when an item is unequipped: (slotIndex, slot, unequippedItem).
        /// </summary>
        public event Action<int, EquipmentSlot, ItemDefinition> OnItemUnequipped;

        /// <summary>
        /// Fired when any slot's equipped state changes: (slotIndex, slot).
        /// </summary>
        public event Action<int, EquipmentSlot> OnSlotChanged;

        /// <summary>
        /// Fired whenever any equipment changes.
        /// </summary>
        public event Action OnEquipmentChanged;

        /// <summary>
        /// Constructs a character inventory with the standard 4 Rune + 1 Artifact layout.
        /// </summary>
        public CharacterInventory() : this(CreateDefaultSlots()) { }

        /// <summary>
        /// Constructs a character inventory with custom slot definitions.
        /// </summary>
        public CharacterInventory(IEnumerable<EquipmentSlot> customSlots)
        {
            _slots = new List<EquipmentSlot>(customSlots ?? CreateDefaultSlots());
            if (_slots.Count == 0)
            {
                _slots.AddRange(CreateDefaultSlots());
            }
        }

        public static List<EquipmentSlot> CreateDefaultSlots()
        {
            return new List<EquipmentSlot>
            {
                new EquipmentSlot(EquipmentSlotType.Rune, "Rune Slot 1"),
                new EquipmentSlot(EquipmentSlotType.Rune, "Rune Slot 2"),
                new EquipmentSlot(EquipmentSlotType.Rune, "Rune Slot 3"),
                new EquipmentSlot(EquipmentSlotType.Rune, "Rune Slot 4"),
                new EquipmentSlot(EquipmentSlotType.Artifact, "Artifact Slot 1")
            };
        }

        public EquipmentSlot GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
            return _slots[slotIndex];
        }

        /// <summary>
        /// Checks if an item can be equipped into the specified slot index.
        /// </summary>
        public bool CanEquip(int slotIndex, ItemDefinition item, out string failureReason)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null)
            {
                failureReason = $"Slot index {slotIndex} is invalid.";
                return false;
            }

            return slot.CanEquip(item, out failureReason);
        }

        /// <summary>
        /// Finds the first empty slot suitable for the specified item, or -1 if none found.
        /// </summary>
        public int FindFirstAvailableSlot(ItemDefinition item)
        {
            if (item == null || !item.IsEquippable) return -1;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsOccupied && _slots[i].SlotType == item.EquippableSlotType)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Equips an item into the designated slot.
        /// - For Artifacts: The previous artifact is returned in displacedItem for return to inventory.
        /// - For Runes: The previous rune is destroyed (displacedItem is null, destroyedRune is populated).
        /// </summary>
        public bool Equip(
            int slotIndex,
            ItemDefinition item,
            out ItemDefinition displacedItem,
            out ItemDefinition destroyedRune,
            bool confirmDestroyRune = true)
        {
            displacedItem = null;
            destroyedRune = null;

            var slot = GetSlot(slotIndex);
            if (slot == null) return false;

            if (!slot.CanEquip(item, out _)) return false;

            // Handle replacement rules if currently occupied
            if (slot.IsOccupied)
            {
                if (slot.SlotType == EquipmentSlotType.Rune)
                {
                    if (!confirmDestroyRune)
                    {
                        // User confirmation required to destroy existing rune
                        return false;
                    }

                    // Inscribing rune destroys previous rune
                    destroyedRune = slot.EquippedItem;
                }
                else if (slot.SlotType == EquipmentSlotType.Artifact)
                {
                    // Artifact is auto-swapped back to User Inventory
                    displacedItem = slot.EquippedItem;
                }
            }

            // Unequip current if present
            if (slot.IsOccupied)
            {
                var prev = slot.EquippedItem;
                slot.Unequip(out _);
                OnItemUnequipped?.Invoke(slotIndex, slot, prev);
            }

            // Equip new item
            slot.Equip(item, out _);
            OnItemEquipped?.Invoke(slotIndex, slot, item);
            OnSlotChanged?.Invoke(slotIndex, slot);
            OnEquipmentChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Unequips the item in the specified slot, returning it in unequippedItem.
        /// </summary>
        public bool Unequip(int slotIndex, out ItemDefinition unequippedItem)
        {
            unequippedItem = null;
            var slot = GetSlot(slotIndex);
            if (slot == null || !slot.IsOccupied) return false;

            if (slot.Unequip(out unequippedItem))
            {
                OnItemUnequipped?.Invoke(slotIndex, slot, unequippedItem);
                OnSlotChanged?.Invoke(slotIndex, slot);
                OnEquipmentChanged?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Unequips all items from all slots, returning a list of all unequipped items.
        /// </summary>
        public List<ItemDefinition> UnequipAll()
        {
            var unequipped = new List<ItemDefinition>();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied && Unequip(i, out var item))
                {
                    unequipped.Add(item);
                }
            }
            return unequipped;
        }
    }
}
