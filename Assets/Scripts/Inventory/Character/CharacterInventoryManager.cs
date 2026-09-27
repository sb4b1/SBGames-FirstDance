using System;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Stats;
using UnityEngine;

namespace SBGames.Inventory.Character
{
    /// <summary>
    /// MonoBehaviour component attached to a character GameObject.
    /// Manages the runtime CharacterInventory and its binding to CharacterStatsManager.
    /// Configured with 4 Rune slots and 1 Artifact slot by default.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsManager))]
    [DisallowMultipleComponent]
    public class CharacterInventoryManager : MonoBehaviour
    {
        [Header("Character Identity")]
        [SerializeField] private string characterDisplayName = "Hero";

        [Header("Dependencies")]
        [SerializeField] private CharacterStatsManager statsManager;

        private CharacterInventory _inventory;
        private CharacterEquipmentStatBinder _statBinder;

        public string CharacterDisplayName => string.IsNullOrEmpty(characterDisplayName) ? gameObject.name : characterDisplayName;
        public CharacterStatsManager StatsManager => statsManager != null ? statsManager : (statsManager = GetComponent<CharacterStatsManager>());
        public CharacterInventory Inventory => _inventory;
        public CharacterEquipmentStatBinder StatBinder => _statBinder;

        public event Action OnEquipmentOrStatsChanged;

        private void Awake()
        {
            if (statsManager == null)
            {
                statsManager = GetComponent<CharacterStatsManager>();
            }

            InitializeInventory();
        }

        private void OnDestroy()
        {
            _statBinder?.Dispose();
        }

        public void InitializeInventory()
        {
            if (_inventory != null) return;

            if (statsManager == null)
            {
                statsManager = GetComponent<CharacterStatsManager>();
            }

            // Setup default 4 Rune slots + 1 Artifact slot
            _inventory = new CharacterInventory();

            if (statsManager != null)
            {
                _statBinder = new CharacterEquipmentStatBinder(_inventory, statsManager);
                _statBinder.OnStatsChanged += () => OnEquipmentOrStatsChanged?.Invoke();
            }

            _inventory.OnEquipmentChanged += () => OnEquipmentOrStatsChanged?.Invoke();
        }

        /// <summary>
        /// Equips an item into the specified equipment slot.
        /// </summary>
        public bool Equip(
            int slotIndex,
            ItemDefinition item,
            out ItemDefinition displacedItem,
            out ItemDefinition destroyedRune,
            bool confirmDestroyRune = true)
        {
            displacedItem = null;
            destroyedRune = null;

            if (_inventory == null) return false;

            return _inventory.Equip(slotIndex, item, out displacedItem, out destroyedRune, confirmDestroyRune);
        }

        /// <summary>
        /// Unequips the item from the specified equipment slot.
        /// </summary>
        public bool Unequip(int slotIndex, out ItemDefinition unequippedItem)
        {
            unequippedItem = null;
            if (_inventory == null) return false;

            return _inventory.Unequip(slotIndex, out unequippedItem);
        }

        /// <summary>
        /// Gets the item currently equipped in the specified slot.
        /// </summary>
        public ItemDefinition GetEquippedItem(int slotIndex)
        {
            return _inventory?.GetSlot(slotIndex)?.EquippedItem;
        }
    }
}
