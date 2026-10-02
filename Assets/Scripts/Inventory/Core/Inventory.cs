using System;
using System.Collections.Generic;
using SBGames.Inventory.Data;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Pure C# inventory domain model managing a fixed capacity collection of InventorySlots.
    /// Handles item stacking, insertion, removal, moving, and swapping.
    /// Emits observer events whenever slot contents change, keeping logic decoupled from UI.
    /// </summary>
    public class Inventory
    {
        private readonly InventorySlot[] _slots;

        public int Capacity => _slots.Length;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        /// <summary>
        /// Fired when an individual slot changes: (slotIndex, slot).
        /// </summary>
        public event Action<int, InventorySlot> OnSlotChanged;

        /// <summary>
        /// Fired whenever any change occurs in the inventory.
        /// </summary>
        public event Action OnInventoryChanged;

        public Inventory(int capacity = 20)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Inventory capacity must be greater than zero.", nameof(capacity));
            }

            _slots = new InventorySlot[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _slots[i] = new InventorySlot();
            }
        }

        /// <summary>
        /// Retrieves the slot at the specified index. Returns null if index is out of bounds.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length) return null;
            return _slots[index];
        }

        /// <summary>
        /// Determines if the inventory has sufficient space to accept the given item and quantity.
        /// </summary>
        public bool CanAddItem(ItemDataSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;

            int remaining = amount;

            // 1. Check existing partial stacks (if stackable)
            if (item.MaxStack > 1)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].CanStackWith(item))
                    {
                        remaining -= _slots[i].RemainingCapacity;
                        if (remaining <= 0) return true;
                    }
                }
            }

            // 2. Check empty slots
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    remaining -= item.MaxStack;
                    if (remaining <= 0) return true;
                }
            }

            return remaining <= 0;
        }

        /// <summary>
        /// Adds an item and quantity to the inventory.
        /// Fills existing non-full stacks first, then populates empty slots.
        /// Returns any leftover amount that could not fit (0 if all added).
        /// </summary>
        public int AddItem(ItemDataSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return amount;

            int remaining = amount;
            bool changed = false;

            // Step 1: Fill existing non-full stacks
            if (item.MaxStack > 1)
            {
                for (int i = 0; i < _slots.Length && remaining > 0; i++)
                {
                    if (_slots[i].CanStackWith(item))
                    {
                        int leftover = _slots[i].AddAmount(remaining);
                        if (leftover != remaining)
                        {
                            changed = true;
                            remaining = leftover;
                            NotifySlotChanged(i);
                        }
                    }
                }
            }

            // Step 2: Fill empty slots
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int toAdd = Math.Min(remaining, item.MaxStack);
                    _slots[i].Set(item, toAdd);
                    remaining -= toAdd;
                    changed = true;
                    NotifySlotChanged(i);
                }
            }

            if (changed)
            {
                NotifyInventoryChanged();
            }

            return remaining;
        }

        /// <summary>
        /// Removes a specified quantity of an item across all slots containing it.
        /// Returns the actual total quantity removed.
        /// </summary>
        public int RemoveItem(ItemDataSO item, int amount = 1)
        {
            if (item == null || amount <= 0) return 0;

            int needed = amount;
            bool changed = false;

            for (int i = _slots.Length - 1; i >= 0 && needed > 0; i--)
            {
                if (_slots[i].Item == item)
                {
                    int removed = _slots[i].RemoveAmount(needed);
                    if (removed > 0)
                    {
                        needed -= removed;
                        changed = true;
                        NotifySlotChanged(i);
                    }
                }
            }

            if (changed)
            {
                NotifyInventoryChanged();
            }

            return amount - needed;
        }

        /// <summary>
        /// Removes a quantity from a specific slot index.
        /// </summary>
        public int RemoveItemAt(int slotIndex, int amount = 1)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length || amount <= 0) return 0;

            int removed = _slots[slotIndex].RemoveAmount(amount);
            if (removed > 0)
            {
                NotifySlotChanged(slotIndex);
                NotifyInventoryChanged();
            }

            return removed;
        }

        /// <summary>
        /// Moves, stacks, or swaps items between fromIndex and toIndex.
        /// </summary>
        public bool MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return false;
            if (fromIndex < 0 || fromIndex >= _slots.Length) return false;
            if (toIndex < 0 || toIndex >= _slots.Length) return false;

            var fromSlot = _slots[fromIndex];
            var toSlot = _slots[toIndex];

            if (fromSlot.IsEmpty) return false;

            // Case 1: Target is empty -> move
            if (toSlot.IsEmpty)
            {
                toSlot.Set(fromSlot.ItemInstance, fromSlot.Amount);
                fromSlot.Clear();
                NotifySlotChanged(fromIndex);
                NotifySlotChanged(toIndex);
                NotifyInventoryChanged();
                return true;
            }

            // Case 2: Target is the same item and stackable -> merge
            if (toSlot.CanStackWith(fromSlot.ItemInstance))
            {
                int leftover = toSlot.AddAmount(fromSlot.Amount);
                if (leftover == 0)
                {
                    fromSlot.Clear();
                }
                else
                {
                    fromSlot.Set(fromSlot.ItemInstance, leftover);
                }

                NotifySlotChanged(fromIndex);
                NotifySlotChanged(toIndex);
                NotifyInventoryChanged();
                return true;
            }

            // Case 3: Different items or target full -> swap
            SwapSlots(fromIndex, toIndex);
            return true;
        }

        /// <summary>
        /// Swaps the contents of two slots.
        /// </summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return;
            if (indexA < 0 || indexA >= _slots.Length) return;
            if (indexB < 0 || indexB >= _slots.Length) return;

            var slotA = _slots[indexA];
            var slotB = _slots[indexB];

            var tempInst = slotA.ItemInstance;
            int tempAmount = slotA.Amount;

            slotA.Set(slotB.ItemInstance, slotB.Amount);
            slotB.Set(tempInst, tempAmount);

            NotifySlotChanged(indexA);
            NotifySlotChanged(indexB);
            NotifyInventoryChanged();
        }

        /// <summary>
        /// Counts the total quantity of an item present in the inventory.
        /// </summary>
        public int CountItem(ItemDataSO item)
        {
            if (item == null) return 0;
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Item == item)
                {
                    count += _slots[i].Amount;
                }
            }
            return count;
        }

        /// <summary>
        /// Checks if the inventory contains at least the specified amount of an item.
        /// </summary>
        public bool HasItem(ItemDataSO item, int amount = 1)
        {
            return CountItem(item) >= amount;
        }

        /// <summary>
        /// Finds the first slot index containing the specified item, or -1 if not found.
        /// </summary>
        public int FindFirstSlotIndex(ItemDataSO item)
        {
            if (item == null) return -1;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Item == item) return i;
            }
            return -1;
        }

        /// <summary>
        /// Clears all slots in the inventory.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
                NotifySlotChanged(i);
            }
            NotifyInventoryChanged();
        }

        private void NotifySlotChanged(int index) => OnSlotChanged?.Invoke(index, _slots[index]);
        private void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();
    }
}
