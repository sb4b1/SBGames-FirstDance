using System;
using UnityEngine;

namespace SBGames.Placement.Bench
{
    /// <summary>
    /// Represents a single storage slot on the player's unit / trap bench.
    /// </summary>
    [Serializable]
    public class BenchSlot
    {
        [SerializeField] private int slotIndex;
        private PlacedUnitInstance unitInstance;

        public int SlotIndex => slotIndex;
        public PlacedUnitInstance UnitInstance => unitInstance;
        public bool IsEmpty => unitInstance == null;
        public IUnitPlacementDefinition Definition => unitInstance?.Definition;
        public GameObject GameObjectInstance => unitInstance?.GameObjectInstance;

        public BenchSlot(int slotIndex)
        {
            this.slotIndex = slotIndex;
            this.unitInstance = null;
        }

        public void Assign(PlacedUnitInstance unit)
        {
            unitInstance = unit;
        }

        public PlacedUnitInstance Clear()
        {
            var prev = unitInstance;
            unitInstance = null;
            return prev;
        }
    }
}
