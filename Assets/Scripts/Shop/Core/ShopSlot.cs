using System;
using SBGames.Shop.Data;
using UnityEngine;

namespace SBGames.Shop.Core
{
    /// <summary>
    /// Represents an individual slot in the monster shop inventory.
    /// Tracks the currently displayed monster offer, purchase status, and lock state across refreshes.
    /// </summary>
    [Serializable]
    public class ShopSlot
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private MonsterShopItem currentItem;
        [SerializeField] private bool isLocked;
        [SerializeField] private bool isPurchased;

        public int SlotIndex => slotIndex;
        public MonsterShopItem CurrentItem => currentItem;
        public bool IsLocked => isLocked;
        public bool IsPurchased => isPurchased;

        /// <summary>
        /// True if the slot currently contains no offer or if the offer has already been purchased.
        /// </summary>
        public bool IsEmpty => currentItem == null || isPurchased;

        /// <summary>
        /// True if this slot contains an active, unbought monster offer.
        /// </summary>
        public bool HasAvailableItem => currentItem != null && !isPurchased;

        public ShopSlot(int slotIndex)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), "Slot index cannot be negative.");

            this.slotIndex = slotIndex;
            this.currentItem = null;
            this.isLocked = false;
            this.isPurchased = false;
        }

        /// <summary>
        /// Populates the slot with a new monster offer and clears purchase status.
        /// </summary>
        public void Populate(MonsterShopItem item)
        {
            currentItem = item ?? throw new ArgumentNullException(nameof(item));
            isPurchased = false;
        }

        /// <summary>
        /// Marks the current item in this slot as purchased.
        /// Unlocks the slot automatically upon purchase so future refreshes can re-populate it.
        /// </summary>
        public void MarkPurchased()
        {
            if (IsEmpty)
                throw new InvalidOperationException($"Cannot purchase an empty or already purchased slot at index {slotIndex}.");

            isPurchased = true;
            isLocked = false;
        }

        /// <summary>
        /// Sets or toggles the lock state of this slot.
        /// </summary>
        public void SetLocked(bool locked)
        {
            if (isPurchased && locked)
            {
                // Cannot lock a purchased/empty slot
                isLocked = false;
                return;
            }
            isLocked = locked;
        }

        /// <summary>
        /// Clears the slot completely.
        /// </summary>
        public void Clear()
        {
            currentItem = null;
            isPurchased = false;
            isLocked = false;
        }

        public override string ToString()
        {
            if (isPurchased)
                return $"[Slot {slotIndex}] (Purchased / Empty)";
            if (currentItem == null)
                return $"[Slot {slotIndex}] (Empty)";

            string lockTag = isLocked ? " [LOCKED]" : "";
            return $"[Slot {slotIndex}]{lockTag} {currentItem}";
        }
    }
}
