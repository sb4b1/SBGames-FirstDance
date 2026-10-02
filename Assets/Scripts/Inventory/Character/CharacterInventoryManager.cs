using System;
using System.Collections.Generic;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
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

        [Header("Drop Settings")]
        [SerializeField, Range(0f, 1f)] private float artifactDropChance = 0.5f;
        [SerializeField] private bool autoDropOnDeath = true;

        [Header("Dependencies")]
        [SerializeField] private CharacterStatsManager statsManager;

        private CharacterInventory _inventory;
        private CharacterEquipmentStatBinder _statBinder;

        public string CharacterDisplayName => string.IsNullOrEmpty(characterDisplayName) ? gameObject.name : characterDisplayName;
        public CharacterStatsManager StatsManager => statsManager != null ? statsManager : (statsManager = GetComponent<CharacterStatsManager>());
        public CharacterInventory Inventory => _inventory;
        public CharacterEquipmentStatBinder StatBinder => _statBinder;

        public float ArtifactDropChance
        {
            get => artifactDropChance;
            set => artifactDropChance = Mathf.Clamp01(value);
        }

        public bool AutoDropOnDeath
        {
            get => autoDropOnDeath;
            set => autoDropOnDeath = value;
        }

        public event Action OnEquipmentOrStatsChanged;
        public static event Action<CharacterInventoryManager> OnAnyEquipmentChanged;
        public event Action<CharacterInventoryManager, ItemDataSO, Vector3> OnArtifactDropped;
        public static event Action<CharacterInventoryManager, ItemDataSO, Vector3> OnAnyArtifactDropped;

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
            if (statsManager != null)
            {
                statsManager.OnCharacterDeath -= HandleCharacterDeath;
            }
            _statBinder?.Dispose();
        }

        private void OnEnable()
        {
            if (StatsManager != null)
            {
                StatsManager.OnCharacterDeath -= HandleCharacterDeath;
                StatsManager.OnCharacterDeath += HandleCharacterDeath;
            }
        }

        private void OnDisable()
        {
            if (statsManager != null)
            {
                statsManager.OnCharacterDeath -= HandleCharacterDeath;
            }
        }

        private void HandleCharacterDeath(CharacterStatsManager stats)
        {
            if (autoDropOnDeath)
            {
                TryDropEquippedArtifacts();
            }
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
                statsManager.OnCharacterDeath -= HandleCharacterDeath;
                statsManager.OnCharacterDeath += HandleCharacterDeath;

                _statBinder = new CharacterEquipmentStatBinder(_inventory, statsManager);
                _statBinder.OnStatsChanged += () => OnEquipmentOrStatsChanged?.Invoke();
            }

            _inventory.OnEquipmentChanged += () =>
            {
                OnEquipmentOrStatsChanged?.Invoke();
                OnAnyEquipmentChanged?.Invoke(this);
            };
        }

        /// <summary>
        /// Equips an item into the specified equipment slot.
        /// </summary>
        public bool Equip(
            int slotIndex,
            ItemDataSO item,
            out ItemDataSO displacedItem,
            out ItemDataSO destroyedRune,
            bool confirmDestroyRune = true)
        {
            displacedItem = null;
            destroyedRune = null;

            if (_inventory == null) return false;

            return _inventory.Equip(slotIndex, item, out displacedItem, out destroyedRune, confirmDestroyRune);
        }

        /// <summary>
        /// Unequips the item from the specified equipment slot.
        /// Fails if the item is a Rune (runes cannot be unequipped).
        /// </summary>
        public bool Unequip(int slotIndex, out ItemDataSO unequippedItem)
        {
            unequippedItem = null;
            if (_inventory == null) return false;

            return _inventory.Unequip(slotIndex, out unequippedItem);
        }

        /// <summary>
        /// Deletes/destroys the rune in the specified slot.
        /// Runes cannot be unequipped back to inventory; they can only be deleted or replaced.
        /// </summary>
        public bool DestroyRune(int slotIndex, out ItemDataSO destroyedRune)
        {
            destroyedRune = null;
            if (_inventory == null) return false;

            return _inventory.DestroyRune(slotIndex, out destroyedRune);
        }

        /// <summary>
        /// Gets the item currently equipped in the specified slot.
        /// </summary>
        public ItemDataSO GetEquippedItem(int slotIndex)
        {
            return _inventory?.GetSlot(slotIndex)?.EquippedItem;
        }

        /// <summary>
        /// Evaluates and drops equipped artifacts based on drop chance.
        /// If dropChance is negative (default), uses the configured artifactDropChance field.
        /// Successfully dropped artifacts are unequipped from the character and broadcast via drop events.
        /// </summary>
        /// <param name="dropChance">Override drop chance between 0 and 1. If negative, uses artifactDropChance.</param>
        /// <returns>List of unequipped artifacts that dropped.</returns>
        public List<ItemDataSO> TryDropEquippedArtifacts(float dropChance = -1f)
        {
            var droppedArtifacts = new List<ItemDataSO>();
            if (_inventory == null) return droppedArtifacts;

            float effectiveChance = dropChance >= 0f ? Mathf.Clamp01(dropChance) : artifactDropChance;
            Vector3 dropPos = transform.position;

            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                var slot = _inventory.GetSlot(i);
                if (slot == null || !slot.IsOccupied || slot.SlotType != EquipmentSlotType.Artifact)
                {
                    continue;
                }

                if (UnityEngine.Random.value <= effectiveChance)
                {
                    if (_inventory.Unequip(i, out var unequippedItem) && unequippedItem != null)
                    {
                        droppedArtifacts.Add(unequippedItem);
                        OnArtifactDropped?.Invoke(this, unequippedItem, dropPos);
                        OnAnyArtifactDropped?.Invoke(this, unequippedItem, dropPos);
                    }
                }
            }

            return droppedArtifacts;
        }
    }
}
