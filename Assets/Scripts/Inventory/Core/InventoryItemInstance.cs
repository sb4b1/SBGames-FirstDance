using System;
using System.Collections.Generic;
using SBGames.Inventory.Data;
using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Plain C# data model representing a mutable runtime item instance.
    /// Tracks unique identity (GUID), upgrade levels, socketed runes, and custom stats.
    /// Pure data structure without MonoBehaviour or GameObject dependencies.
    /// </summary>
    [Serializable]
    public class InventoryItemInstance
    {
        [SerializeField] private string instanceID;
        [SerializeField] private ItemDataSO itemData;
        [SerializeField] private int upgradeTier;

        public string InstanceID => instanceID;
        public ItemDataSO ItemData => itemData;
        public int UpgradeTier => upgradeTier;

        public bool IsUpgraded => upgradeTier > 0;

        /// <summary>
        /// Creates a fresh runtime instance from static ItemDataSO.
        /// </summary>
        public InventoryItemInstance(ItemDataSO data, int tier = 0, string customInstanceID = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            instanceID = string.IsNullOrEmpty(customInstanceID) ? Guid.NewGuid().ToString("N") : customInstanceID;
            itemData = data;
            upgradeTier = Math.Max(0, tier);
        }

        /// <summary>
        /// Creates a deep copy of an item instance.
        /// </summary>
        public InventoryItemInstance Clone(bool generateNewId = true)
        {
            return new InventoryItemInstance(itemData, upgradeTier, generateNewId ? Guid.NewGuid().ToString("N") : instanceID);
        }

        /// <summary>
        /// Determines if this runtime instance can stack with another instance.
        /// Stackable items must have identical ItemData, be marked isStackable, and tier 0.
        /// </summary>
        public bool CanStackWith(InventoryItemInstance other)
        {
            if (other == null || itemData == null || other.itemData == null) return false;
            if (itemData.ItemID != other.itemData.ItemID) return false;
            if (!itemData.IsStackable) return false;

            // Upgraded items become unique and cannot stack
            if (IsUpgraded || other.IsUpgraded) return false;

            return true;
        }

        /// <summary>
        /// Determines if this runtime instance can stack with a static ItemDataSO definition.
        /// </summary>
        public bool CanStackWith(ItemDataSO data)
        {
            if (data == null || itemData == null) return false;
            if (itemData.ItemID != data.ItemID) return false;
            if (!itemData.IsStackable) return false;
            if (IsUpgraded) return false;

            return true;
        }

        /// <summary>
        /// Increments the upgrade tier.
        /// </summary>
        public void IncrementTier(int levels = 1)
        {
            upgradeTier = Math.Max(0, upgradeTier + levels);
        }
    }
}
