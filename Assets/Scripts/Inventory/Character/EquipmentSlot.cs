using System;
using SBGames.Inventory.Core;

namespace SBGames.Inventory.Character
{
    /// <summary>
    /// Represents a single character equipment slot constrained to a specific EquipmentSlotType.
    /// Pure C# model with strict type validation.
    /// </summary>
    [Serializable]
    public class EquipmentSlot
    {
        public string SlotName { get; }
        public EquipmentSlotType SlotType { get; }
        public ItemDefinition EquippedItem { get; private set; }

        public bool IsOccupied => EquippedItem != null;
        public bool IsEmpty => !IsOccupied;

        public EquipmentSlot(EquipmentSlotType slotType, string slotName = null)
        {
            SlotType = slotType;
            SlotName = string.IsNullOrEmpty(slotName) ? slotType.ToString() : slotName;
            EquippedItem = null;
        }

        /// <summary>
        /// Validates whether an item can be equipped into this slot.
        /// </summary>
        public bool CanEquip(ItemDefinition item, out string failureReason)
        {
            if (item == null)
            {
                failureReason = "Item cannot be null.";
                return false;
            }

            if (!item.IsEquippable)
            {
                failureReason = $"{item.ItemName} is not an equippable item.";
                return false;
            }

            if (item.EquippableSlotType != SlotType)
            {
                failureReason = $"Cannot equip {item.EquippableSlotType} into a {SlotType} slot.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        /// <summary>
        /// Equips the item into this slot, outputting any previously equipped item.
        /// </summary>
        public bool Equip(ItemDefinition item, out ItemDefinition previousItem)
        {
            if (!CanEquip(item, out _))
            {
                previousItem = null;
                return false;
            }

            previousItem = EquippedItem;
            EquippedItem = item;
            return true;
        }

        /// <summary>
        /// Unequips the currently equipped item, returning it and leaving the slot empty.
        /// </summary>
        public bool Unequip(out ItemDefinition unequippedItem)
        {
            if (!IsOccupied)
            {
                unequippedItem = null;
                return false;
            }

            unequippedItem = EquippedItem;
            EquippedItem = null;
            return true;
        }
    }
}
