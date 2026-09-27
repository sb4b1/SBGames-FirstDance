using System;

namespace SBGames.Placement
{
    /// <summary>
    /// Contract for managing player resources/currency required for unit placement.
    /// </summary>
    public interface IResourceWallet
    {
        /// <summary>Current available resources.</summary>
        int CurrentResources { get; }

        /// <summary>Checks whether the wallet has at least <paramref name="cost"/> resources.</summary>
        bool CanAfford(int cost);

        /// <summary>Deducts <paramref name="cost"/> if affordable. Returns true if deducted, false otherwise.</summary>
        bool TryDeduct(int cost);

        /// <summary>Adds resources (e.g. income, rewards, refunds).</summary>
        void Add(int amount);

        /// <summary>Fired when resource balance changes: (currentBalance, delta).</summary>
        event Action<int, int> OnResourcesChanged;
    }

    /// <summary>
    /// Default standard thread-safe implementation of IResourceWallet.
    /// </summary>
    public class ResourceWallet : IResourceWallet
    {
        private int currentResources;

        public int CurrentResources => currentResources;

        public event Action<int, int> OnResourcesChanged;

        public ResourceWallet(int initialResources = 0)
        {
            currentResources = Math.Max(0, initialResources);
        }

        public bool CanAfford(int cost)
        {
            if (cost < 0) return false;
            return currentResources >= cost;
        }

        public bool TryDeduct(int cost)
        {
            if (cost < 0) return false;
            if (currentResources < cost) return false;

            currentResources -= cost;
            OnResourcesChanged?.Invoke(currentResources, -cost);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;

            currentResources += amount;
            OnResourcesChanged?.Invoke(currentResources, amount);
        }
    }
}
