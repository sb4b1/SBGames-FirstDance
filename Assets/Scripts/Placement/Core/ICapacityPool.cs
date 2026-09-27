using System;

namespace SBGames.Placement
{
    /// <summary>
    /// Contract for managing dungeon unit capacity allocation.
    /// </summary>
    public interface ICapacityPool
    {
        /// <summary>Maximum capacity allowed in the dungeon.</summary>
        int MaxCapacity { get; }

        /// <summary>Total capacity currently occupied by units.</summary>
        int CurrentCapacityUsed { get; }

        /// <summary>Remaining free capacity.</summary>
        int AvailableCapacity { get; }

        /// <summary>Checks whether adding <paramref name="capacityRequired"/> would exceed MaxCapacity.</summary>
        bool CanAccommodate(int capacityRequired);

        /// <summary>Attempts to allocate capacity. Returns true if successful.</summary>
        bool TryAllocate(int capacityRequired);

        /// <summary>Releases previously allocated capacity (e.g. on unit death or removal).</summary>
        void Release(int capacityToRelease);

        /// <summary>Updates max capacity limit (e.g. dungeon upgrade, relics).</summary>
        void SetMaxCapacity(int newMaxCapacity);

        /// <summary>Fired when capacity usage or maximum changes: (usedCapacity, maxCapacity).</summary>
        event Action<int, int> OnCapacityChanged;
    }

    /// <summary>
    /// Default implementation of ICapacityPool.
    /// </summary>
    public class CapacityPool : ICapacityPool
    {
        private int maxCapacity;
        private int currentCapacityUsed;

        public int MaxCapacity => maxCapacity;
        public int CurrentCapacityUsed => currentCapacityUsed;
        public int AvailableCapacity => Math.Max(0, maxCapacity - currentCapacityUsed);

        public event Action<int, int> OnCapacityChanged;

        public CapacityPool(int maxCapacity, int initialUsedCapacity = 0)
        {
            this.maxCapacity = Math.Max(0, maxCapacity);
            this.currentCapacityUsed = Math.Clamp(initialUsedCapacity, 0, this.maxCapacity);
        }

        public bool CanAccommodate(int capacityRequired)
        {
            if (capacityRequired < 0) return false;
            return currentCapacityUsed + capacityRequired <= maxCapacity;
        }

        public bool TryAllocate(int capacityRequired)
        {
            if (capacityRequired < 0) return false;
            if (currentCapacityUsed + capacityRequired > maxCapacity) return false;

            currentCapacityUsed += capacityRequired;
            OnCapacityChanged?.Invoke(currentCapacityUsed, maxCapacity);
            return true;
        }

        public void Release(int capacityToRelease)
        {
            if (capacityToRelease <= 0) return;

            currentCapacityUsed = Math.Max(0, currentCapacityUsed - capacityToRelease);
            OnCapacityChanged?.Invoke(currentCapacityUsed, maxCapacity);
        }

        public void SetMaxCapacity(int newMaxCapacity)
        {
            maxCapacity = Math.Max(0, newMaxCapacity);
            // If current used exceeds new max, we clamp or keep tracking over-capacity gracefully
            OnCapacityChanged?.Invoke(currentCapacityUsed, maxCapacity);
        }
    }
}
