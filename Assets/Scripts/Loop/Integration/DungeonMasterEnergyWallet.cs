using System;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Loop.Integration
{
    /// <summary>
    /// Adapter that bridges the DungeonPlacementSystem's IResourceWallet contract
    /// directly to DungeonMaster's Energy reserve.
    /// This causes building and unit placement on the grid to spend Energy instead of gold,
    /// and ensures unit sell/removal refunds restore Energy to the Dungeon Master.
    /// </summary>
    public class DungeonMasterEnergyWallet : IResourceWallet, IDisposable
    {
        private readonly DungeonMaster dungeonMaster;
        private int lastKnownEnergy;

        public int CurrentResources => dungeonMaster != null ? dungeonMaster.Energy : 0;

        public event Action<int, int> OnResourcesChanged;

        public DungeonMasterEnergyWallet(DungeonMaster dm)
        {
            dungeonMaster = dm ?? throw new ArgumentNullException(nameof(dm));
            lastKnownEnergy = dungeonMaster.Energy;
            dungeonMaster.OnEnergyChanged += HandleDungeonMasterEnergyChanged;
        }

        public bool CanAfford(int cost)
        {
            if (cost < 0) return false;
            if (dungeonMaster == null) return false;
            return dungeonMaster.Energy >= cost;
        }

        public bool TryDeduct(int cost)
        {
            if (cost < 0) return false;
            if (dungeonMaster == null) return false;

            return dungeonMaster.SpendEnergy(cost);
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            if (dungeonMaster == null) return;

            dungeonMaster.GainEnergy(amount);
        }

        private void HandleDungeonMasterEnergyChanged(int current, int max)
        {
            int delta = current - lastKnownEnergy;
            lastKnownEnergy = current;
            OnResourcesChanged?.Invoke(current, delta);
        }

        public void Dispose()
        {
            if (dungeonMaster != null)
            {
                dungeonMaster.OnEnergyChanged -= HandleDungeonMasterEnergyChanged;
            }
        }
    }
}
