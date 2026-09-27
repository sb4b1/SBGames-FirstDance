using System;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Represents a single slot in an inventory, holding an ItemDefinition and quantity.
    /// Pure C# class, completely independent of Unity UI or GameObjects.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemDefinition Item { get; private set; }
        public int Amount { get; private set; }

        public bool IsEmpty => Item == null || Amount <= 0;
        public int RemainingCapacity => IsEmpty ? 0 : Math.Max(0, Item.MaxStack - Amount);

        public InventorySlot()
        {
            Clear();
        }

        public InventorySlot(ItemDefinition item, int amount)
        {
            Set(item, amount);
        }

        /// <summary>
        /// Checks whether the given item can be stacked into this slot.
        /// </summary>
        public bool CanStackWith(ItemDefinition item)
        {
            if (IsEmpty || item == null) return false;
            return Item == item && Amount < Item.MaxStack;
        }

        /// <summary>
        /// Populates this slot with an item and quantity.
        /// </summary>
        public void Set(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0)
            {
                Clear();
                return;
            }

            Item = item;
            Amount = Math.Clamp(amount, 1, item.MaxStack);
        }

        /// <summary>
        /// Adds a quantity to this slot. Returns any overflow amount that could not fit.
        /// </summary>
        public int AddAmount(int quantity)
        {
            if (IsEmpty || quantity <= 0) return quantity;

            int space = Item.MaxStack - Amount;
            int toAdd = Math.Min(space, quantity);
            Amount += toAdd;

            return quantity - toAdd;
        }

        /// <summary>
        /// Removes a quantity from this slot. If amount reaches 0, the slot is cleared.
        /// Returns the actual amount removed.
        /// </summary>
        public int RemoveAmount(int quantity)
        {
            if (IsEmpty || quantity <= 0) return 0;

            int toRemove = Math.Min(Amount, quantity);
            Amount -= toRemove;

            if (Amount <= 0)
            {
                Clear();
            }

            return toRemove;
        }

        /// <summary>
        /// Clears the slot to an empty state.
        /// </summary>
        public void Clear()
        {
            Item = null;
            Amount = 0;
        }
    }
}
