using System;
using SBGames.Inventory.Data;
using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Pure C# class representing a single inventory slot in a grid/list collection.
    /// Manages quantity and an InventoryItemInstance reference.
    /// Zero MonoBehaviour/GameObject dependencies.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private InventoryItemInstance itemInstance;
        [SerializeField] private int quantity;

        public InventoryItemInstance ItemInstance => itemInstance;
        public int Quantity => quantity;

        // Backward compatibility & convenience properties
        public ItemDataSO Item => itemInstance?.ItemData;
        public ItemDataSO ItemData => itemInstance?.ItemData;
        public int Amount => quantity;
        public string ItemID => itemInstance?.ItemData?.ItemID;

        public bool IsEmpty => itemInstance == null || itemInstance.ItemData == null || quantity <= 0;
        public int MaxStack => itemInstance?.ItemData != null ? itemInstance.ItemData.MaxStack : 1;
        public int RemainingCapacity => IsEmpty ? 0 : Math.Max(0, MaxStack - quantity);

        public InventorySlot()
        {
            Clear();
        }

        public InventorySlot(InventoryItemInstance instance, int amount)
        {
            Set(instance, amount);
        }

        public InventorySlot(ItemDataSO data, int amount, int tier = 0)
        {
            if (data != null && amount > 0)
            {
                Set(new InventoryItemInstance(data, tier), amount);
            }
            else
            {
                Clear();
            }
        }

        /// <summary>
        /// Validates if an item instance can be stacked into this slot.
        /// </summary>
        public bool CanStackWith(InventoryItemInstance instance)
        {
            if (IsEmpty || instance == null) return false;
            return itemInstance.CanStackWith(instance) && quantity < MaxStack;
        }

        /// <summary>
        /// Validates if static ItemDataSO can be stacked into this slot.
        /// </summary>
        public bool CanStackWith(ItemDataSO data)
        {
            if (IsEmpty || data == null) return false;
            return itemInstance.CanStackWith(data) && quantity < MaxStack;
        }

        /// <summary>
        /// Sets the slot contents from runtime item instance.
        /// </summary>
        public void Set(InventoryItemInstance instance, int amount)
        {
            if (instance == null || instance.ItemData == null || amount <= 0)
            {
                Clear();
                return;
            }

            itemInstance = instance;
            int max = instance.ItemData.MaxStack;
            quantity = Math.Clamp(amount, 1, max);
        }

        /// <summary>
        /// Sets slot contents directly from ItemDataSO and quantity.
        /// </summary>
        public void Set(ItemDataSO data, int amount, int tier = 0)
        {
            if (data == null || amount <= 0)
            {
                Clear();
                return;
            }

            Set(new InventoryItemInstance(data, tier), amount);
        }

        /// <summary>
        /// Adds quantity to this slot. Returns any overflow amount that could not fit.
        /// </summary>
        public int AddQuantity(int amount)
        {
            if (IsEmpty || amount <= 0) return amount;

            int space = MaxStack - quantity;
            int toAdd = Math.Min(space, amount);
            quantity += toAdd;

            return amount - toAdd;
        }

        /// <summary>
        /// Removes quantity from this slot. If quantity reaches 0, clears the slot.
        /// Returns the actual amount removed.
        /// </summary>
        public int RemoveQuantity(int amount)
        {
            if (IsEmpty || amount <= 0) return 0;

            int toRemove = Math.Min(quantity, amount);
            quantity -= toRemove;

            if (quantity <= 0)
            {
                Clear();
            }

            return toRemove;
        }

        public int AddAmount(int amount) => AddQuantity(amount);
        public int RemoveAmount(int amount) => RemoveQuantity(amount);

        /// <summary>
        /// Clears the slot to an empty state.
        /// </summary>
        public void Clear()
        {
            itemInstance = null;
            quantity = 0;
        }
    }
}
