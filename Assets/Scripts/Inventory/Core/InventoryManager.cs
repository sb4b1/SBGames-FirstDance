using System;
using System.Collections.Generic;
using SBGames.Database;
using SBGames.Inventory.Character;
using SBGames.Inventory.Data;
using SBGames.Inventory.Save;
using SBGames.Loop;
using SBGames.Placement;
using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Unified main controller and domain manager for the runtime inventory system.
    /// Manages player bag slots, fast O(1) item lookups, phase-aware mutation locks,
    /// character equipment/rune inscriptions, consumables, and JSON persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField, Min(1)] private int defaultCapacity = 24;
        [SerializeField] private ItemDatabaseSO itemDatabase;
        [SerializeField] private bool persistAcrossScenes = false;

        [Header("Phase Integration (Optional)")]
        [Tooltip("When enabled, mutating inventory operations are blocked during Combat Phase unless forced.")]
        [SerializeField] private bool enforceCombatPhaseLock = true;

        // Slot collection
        private readonly List<InventorySlot> _slots = new();

        // Fast O(1) cached lookup maps: itemID -> List of slot indices, and itemID -> Total Quantity
        private readonly Dictionary<string, List<int>> _itemToSlotsMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _itemQuantityMap = new(StringComparer.OrdinalIgnoreCase);

        // Phase provider reference
        private IDungeonPhaseManager _phaseManager;

        // Public read-only accessors
        public int Capacity => _slots.Count;
        public IReadOnlyList<InventorySlot> Slots => _slots;
        public ItemDatabaseSO Database => itemDatabase;
        public bool IsCombatLocked => enforceCombatPhaseLock && _phaseManager != null && _phaseManager.CurrentPhase == DungeonPhase.CombatPhase;

        // Events
        /// <summary>Fired whenever any inventory state changes.</summary>
        public event Action OnInventoryChanged;

        /// <summary>Fired when a specific slot is updated: (slotIndex, slot).</summary>
        public event Action<int, InventorySlot> OnSlotUpdated;

        /// <summary>Alias for OnSlotUpdated to support standard event subscribers.</summary>
        public event Action<int, InventorySlot> OnSlotChanged;

        /// <summary>Fired when an item instance is added: (instance, quantityAdded).</summary>
        public event Action<InventoryItemInstance, int> OnItemAdded;

        /// <summary>Fired when an item instance is removed: (instance, quantityRemoved).</summary>
        public event Action<InventoryItemInstance, int> OnItemRemoved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeInventory(defaultCapacity);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Injects or links the dungeon phase manager for phase-aware locking.
        /// </summary>
        public void SetPhaseManager(IDungeonPhaseManager phaseManager)
        {
            _phaseManager = phaseManager;
        }

        /// <summary>
        /// Injects an ItemDatabaseSO at runtime.
        /// </summary>
        public void SetItemDatabase(ItemDatabaseSO database)
        {
            itemDatabase = database;
        }

        /// <summary>
        /// Initializes the inventory with a given capacity.
        /// </summary>
        public void InitializeInventory(int capacity)
        {
            _slots.Clear();
            _itemToSlotsMap.Clear();
            _itemQuantityMap.Clear();

            int targetCap = Math.Max(1, capacity);
            for (int i = 0; i < targetCap; i++)
            {
                _slots.Add(new InventorySlot());
            }

            NotifyInventoryChanged();
        }

        #region O(1) Fast Read Operations (Zero Allocations)

        /// <summary>
        /// Retrieves the slot at the specified index, or null if out of range.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Count) return null;
            return _slots[index];
        }

        /// <summary>
        /// Checks if the inventory contains at least the required quantity of an item in O(1) time without allocating.
        /// </summary>
        public bool HasItem(string itemID, int requiredQuantity = 1)
        {
            if (string.IsNullOrEmpty(itemID) || requiredQuantity <= 0) return false;
            if (_itemQuantityMap.TryGetValue(itemID, out int currentQuantity))
            {
                return currentQuantity >= requiredQuantity;
            }
            return false;
        }

        /// <summary>
        /// Checks if the inventory contains at least the required quantity of an ItemDataSO in O(1) time.
        /// </summary>
        public bool HasItem(ItemDataSO itemData, int requiredQuantity = 1)
        {
            if (itemData == null) return false;
            return HasItem(itemData.ItemID, requiredQuantity);
        }

        /// <summary>
        /// Returns the total quantity of the specified item across all slots in O(1) time.
        /// </summary>
        public int GetTotalQuantity(string itemID)
        {
            if (string.IsNullOrEmpty(itemID)) return 0;
            return _itemQuantityMap.TryGetValue(itemID, out int count) ? count : 0;
        }

        /// <summary>
        /// Returns the total quantity of the specified ItemDataSO in O(1) time.
        /// </summary>
        public int CountItem(ItemDataSO item)
        {
            if (item == null) return 0;
            return GetTotalQuantity(item.ItemID);
        }

        /// <summary>
        /// Evaluates whether the inventory can fit the given item and quantity without modifying slots.
        /// </summary>
        public bool CanAcceptItem(ItemDataSO data, int quantity = 1)
        {
            if (data == null || quantity <= 0) return false;

            int remaining = quantity;

            // 1. Partial stack space
            if (data.IsStackable && _itemToSlotsMap.TryGetValue(data.ItemID, out var slotIndices))
            {
                for (int i = 0; i < slotIndices.Count; i++)
                {
                    var slot = _slots[slotIndices[i]];
                    if (slot.CanStackWith(data))
                    {
                        remaining -= slot.RemainingCapacity;
                        if (remaining <= 0) return true;
                    }
                }
            }

            // 2. Empty slots
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    remaining -= data.MaxStack;
                    if (remaining <= 0) return true;
                }
            }

            return remaining <= 0;
        }

        public bool CanAddItem(ItemDataSO item, int amount = 1) => CanAcceptItem(item, amount);

        #endregion

        #region Mutation Operations (Phase-Aware & Encapsulated)

        /// <summary>
        /// Adds an item definition and quantity into the inventory.
        /// Fills existing partial stacks first, then populates empty slots.
        /// </summary>
        public InventoryOperationResult AddItem(ItemDataSO data, int quantity = 1, int upgradeTier = 0, bool force = false)
        {
            if (data == null)
                return InventoryOperationResult.Failure(InventoryOperationStatus.IncompatibleItem, "Item data cannot be null.");
            if (quantity <= 0)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Quantity must be greater than zero.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot modify inventory during Combat Phase.");

            if (!CanAcceptItem(data, quantity))
                return InventoryOperationResult.Failure(InventoryOperationStatus.InventoryFull, "Not enough inventory space.");

            int remaining = quantity;
            int totalAdded = 0;
            bool isStackable = data.IsStackable && upgradeTier == 0;

            // Step 1: Fill partial stacks if stackable
            if (isStackable && _itemToSlotsMap.TryGetValue(data.ItemID, out var slotIndices))
            {
                var cachedIndices = slotIndices.ToArray();
                for (int i = 0; i < cachedIndices.Length && remaining > 0; i++)
                {
                    int slotIdx = cachedIndices[i];
                    var slot = _slots[slotIdx];
                    if (slot.CanStackWith(data))
                    {
                        int leftover = slot.AddQuantity(remaining);
                        int addedToSlot = remaining - leftover;
                        remaining = leftover;
                        totalAdded += addedToSlot;

                        if (addedToSlot > 0)
                        {
                            UpdateSlotLookup(slotIdx);
                            NotifySlotUpdated(slotIdx);
                            OnItemAdded?.Invoke(slot.ItemInstance, addedToSlot);
                        }
                    }
                }
            }

            // Step 2: Fill empty slots
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int toAdd = isStackable ? Math.Min(remaining, data.MaxStack) : 1;
                    var instance = new InventoryItemInstance(data, upgradeTier);
                    _slots[i].Set(instance, toAdd);
                    remaining -= toAdd;
                    totalAdded += toAdd;

                    UpdateSlotLookup(i);
                    NotifySlotUpdated(i);
                    OnItemAdded?.Invoke(instance, toAdd);
                }
            }

            NotifyInventoryChanged();
            return InventoryOperationResult.Success($"Added {totalAdded}x {data.DisplayName}", totalAdded);
        }

        /// <summary>
        /// Adds a pre-existing runtime item instance.
        /// </summary>
        public InventoryOperationResult AddItemInstance(InventoryItemInstance instance, int quantity = 1, bool force = false)
        {
            if (instance == null || instance.ItemData == null)
                return InventoryOperationResult.Failure(InventoryOperationStatus.IncompatibleItem, "Item instance is invalid.");
            if (quantity <= 0)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Quantity must be greater than zero.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot modify inventory during Combat Phase.");

            // If stackable, try stacking first
            if (instance.ItemData.IsStackable && !instance.IsUpgraded)
            {
                return AddItem(instance.ItemData, quantity, instance.UpgradeTier, force);
            }

            // Otherwise, place into first empty slot
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].Set(instance, 1);
                    UpdateSlotLookup(i);
                    NotifySlotUpdated(i);
                    OnItemAdded?.Invoke(instance, 1);
                    NotifyInventoryChanged();
                    return InventoryOperationResult.Success($"Added {instance.ItemData.DisplayName}", 1);
                }
            }

            return InventoryOperationResult.Failure(InventoryOperationStatus.InventoryFull, "Inventory is full.");
        }

        /// <summary>
        /// Removes a quantity of an item by itemID across matching slots.
        /// </summary>
        public InventoryOperationResult RemoveItem(string itemID, int quantity = 1, bool force = false)
        {
            if (string.IsNullOrEmpty(itemID) || quantity <= 0)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Invalid removal parameters.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot modify inventory during Combat Phase.");

            if (!HasItem(itemID, quantity))
                return InventoryOperationResult.Failure(InventoryOperationStatus.InsufficientQuantity, $"Insufficient quantity of item '{itemID}'.");

            int needed = quantity;
            int totalRemoved = 0;

            if (_itemToSlotsMap.TryGetValue(itemID, out var slotIndices))
            {
                var indices = slotIndices.ToArray();
                for (int i = indices.Length - 1; i >= 0 && needed > 0; i--)
                {
                    int slotIdx = indices[i];
                    var slot = _slots[slotIdx];
                    var instance = slot.ItemInstance;
                    int removed = slot.RemoveQuantity(needed);

                    if (removed > 0)
                    {
                        needed -= removed;
                        totalRemoved += removed;
                        UpdateSlotLookup(slotIdx);
                        NotifySlotUpdated(slotIdx);
                        OnItemRemoved?.Invoke(instance, removed);
                    }
                }
            }

            NotifyInventoryChanged();
            return InventoryOperationResult.Success($"Removed {totalRemoved}x {itemID}", totalRemoved);
        }

        public InventoryOperationResult RemoveItem(ItemDataSO item, int quantity = 1, bool force = false)
        {
            if (item == null)
                return InventoryOperationResult.Failure(InventoryOperationStatus.IncompatibleItem, "Item data is null.");
            return RemoveItem(item.ItemID, quantity, force);
        }

        /// <summary>
        /// Removes a quantity from a specific slot index.
        /// </summary>
        public InventoryOperationResult RemoveSlot(int slotIndex, int quantity = 1, bool force = false)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidSlot, "Slot index out of range.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot modify inventory during Combat Phase.");

            var slot = _slots[slotIndex];
            if (slot.IsEmpty)
                return InventoryOperationResult.Failure(InventoryOperationStatus.ItemNotFound, "Slot is empty.");

            var instance = slot.ItemInstance;
            int removed = slot.RemoveQuantity(quantity);
            if (removed > 0)
            {
                UpdateSlotLookup(slotIndex);
                NotifySlotUpdated(slotIndex);
                OnItemRemoved?.Invoke(instance, removed);
                NotifyInventoryChanged();
                return InventoryOperationResult.Success($"Removed {removed} items from slot {slotIndex}", removed);
            }

            return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Could not remove items.");
        }

        public int RemoveItemAt(int slotIndex, int amount = 1)
        {
            var res = RemoveSlot(slotIndex, amount, force: false);
            return res.IsSuccess ? res.QuantityAffected : 0;
        }

        /// <summary>
        /// Swaps or merges items between two slots.
        /// </summary>
        public InventoryOperationResult SwapSlots(int fromIndex, int toIndex, bool force = false)
        {
            if (fromIndex == toIndex) return InventoryOperationResult.Success();
            if (fromIndex < 0 || fromIndex >= _slots.Count || toIndex < 0 || toIndex >= _slots.Count)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidSlot, "Invalid slot indices.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot rearrange inventory during Combat Phase.");

            var slotFrom = _slots[fromIndex];
            var slotTo = _slots[toIndex];

            if (slotFrom.IsEmpty && slotTo.IsEmpty) return InventoryOperationResult.Success();

            // Case 1: Merge stack into target
            if (!slotFrom.IsEmpty && !slotTo.IsEmpty && slotTo.CanStackWith(slotFrom.ItemInstance))
            {
                int leftover = slotTo.AddQuantity(slotFrom.Quantity);
                if (leftover == 0)
                {
                    slotFrom.Clear();
                }
                else
                {
                    slotFrom.Set(slotFrom.ItemInstance, leftover);
                }

                UpdateSlotLookup(fromIndex);
                UpdateSlotLookup(toIndex);
                NotifySlotUpdated(fromIndex);
                NotifySlotUpdated(toIndex);
                NotifyInventoryChanged();
                return InventoryOperationResult.Success("Merged stacks.");
            }

            // Case 2: Standard Swap
            var tempInstance = slotFrom.ItemInstance;
            int tempQty = slotFrom.Quantity;

            slotFrom.Set(slotTo.ItemInstance, slotTo.Quantity);
            slotTo.Set(tempInstance, tempQty);

            UpdateSlotLookup(fromIndex);
            UpdateSlotLookup(toIndex);
            NotifySlotUpdated(fromIndex);
            NotifySlotUpdated(toIndex);
            NotifyInventoryChanged();

            return InventoryOperationResult.Success("Swapped slots.");
        }

        /// <summary>
        /// Splits a stack from fromIndex into an empty toIndex slot.
        /// </summary>
        public InventoryOperationResult SplitSlot(int fromIndex, int toIndex, int splitAmount, bool force = false)
        {
            if (fromIndex < 0 || fromIndex >= _slots.Count || toIndex < 0 || toIndex >= _slots.Count)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidSlot, "Invalid slot indices.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot split items during Combat Phase.");

            var fromSlot = _slots[fromIndex];
            var toSlot = _slots[toIndex];

            if (fromSlot.IsEmpty || splitAmount <= 0 || splitAmount >= fromSlot.Quantity)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Invalid split amount.");

            if (!toSlot.IsEmpty)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidSlot, "Target slot must be empty for split.");

            var newInstance = fromSlot.ItemInstance.Clone();
            fromSlot.RemoveQuantity(splitAmount);
            toSlot.Set(newInstance, splitAmount);

            UpdateSlotLookup(fromIndex);
            UpdateSlotLookup(toIndex);
            NotifySlotUpdated(fromIndex);
            NotifySlotUpdated(toIndex);
            NotifyInventoryChanged();

            return InventoryOperationResult.Success($"Split {splitAmount} items.", splitAmount);
        }

        /// <summary>
        /// Upgrades an item in a slot to the next tier.
        /// </summary>
        public InventoryOperationResult UpgradeItem(int slotIndex, int levels = 1, bool force = false)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidSlot, "Invalid slot index.");

            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot upgrade items during Combat Phase.");

            var slot = _slots[slotIndex];
            if (slot.IsEmpty || slot.ItemInstance == null)
                return InventoryOperationResult.Failure(InventoryOperationStatus.ItemNotFound, "Slot is empty.");

            slot.ItemInstance.IncrementTier(levels);
            UpdateSlotLookup(slotIndex);
            NotifySlotUpdated(slotIndex);
            NotifyInventoryChanged();

            return InventoryOperationResult.Success($"Upgraded item to Tier +{slot.ItemInstance.UpgradeTier}");
        }

        /// <summary>
        /// Clears all slots.
        /// </summary>
        public InventoryOperationResult Clear(bool force = false)
        {
            if (!force && IsCombatLocked)
                return InventoryOperationResult.Failure(InventoryOperationStatus.LockedInCombat, "Cannot clear inventory during Combat Phase.");

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Clear();
                NotifySlotUpdated(i);
            }

            _itemToSlotsMap.Clear();
            _itemQuantityMap.Clear();
            NotifyInventoryChanged();

            return InventoryOperationResult.Success("Cleared inventory.");
        }

        #endregion

        #region Character & Equipment Interactions

        /// <summary>
        /// Uses a consumable item from a slot on a target character.
        /// </summary>
        public bool ConsumeItem(int slotIndex, CharacterInventoryManager targetCharacter, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (targetCharacter == null || targetCharacter.StatsManager == null)
            {
                resultMessage = "Invalid target character.";
                return false;
            }

            if (IsCombatLocked)
            {
                resultMessage = "Cannot consume items during Combat Phase.";
                return false;
            }

            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                resultMessage = "Selected slot is empty.";
                return false;
            }

            var item = slot.Item;
            if (!item.IsConsumable || item.ConsumableEffect == null)
            {
                resultMessage = $"{item.DisplayName} cannot be consumed.";
                return false;
            }

            var stats = targetCharacter.StatsManager;
            if (!item.ConsumableEffect.CanApply(targetCharacter.gameObject, stats, out string failureReason))
            {
                resultMessage = $"Cannot consume {item.DisplayName}: {failureReason}";
                return false;
            }

            if (item.ConsumableEffect.Apply(targetCharacter.gameObject, stats))
            {
                RemoveSlot(slotIndex, 1, force: true);
                resultMessage = $"Consumed {item.DisplayName} on {targetCharacter.CharacterDisplayName}.";
                return true;
            }

            resultMessage = $"Failed to apply effect of {item.DisplayName}.";
            return false;
        }

        /// <summary>
        /// Equips an item (e.g. Artifact or Rune) from inventory to the target character.
        /// Auto-swaps artifacts back to inventory. Inscribing runes permanently overwrites/destroys previous runes.
        /// </summary>
        public bool EquipToCharacter(
            int userSlotIndex,
            CharacterInventoryManager targetCharacter,
            int targetSlotIndex = -1,
            bool confirmDestroyRune = true)
        {
            if (targetCharacter == null || targetCharacter.Inventory == null) return false;
            if (IsCombatLocked) return false;

            var userSlot = GetSlot(userSlotIndex);
            if (userSlot == null || userSlot.IsEmpty) return false;

            var itemToEquip = userSlot.Item;
            if (!itemToEquip.IsEquippable) return false;

            var charInv = targetCharacter.Inventory;

            // Resolve target slot index
            if (targetSlotIndex < 0)
            {
                targetSlotIndex = charInv.FindFirstAvailableSlot(itemToEquip);
                if (targetSlotIndex < 0)
                {
                    for (int i = 0; i < charInv.SlotCount; i++)
                    {
                        if (charInv.GetSlot(i).SlotType == itemToEquip.EquippableSlotType)
                        {
                            targetSlotIndex = i;
                            break;
                        }
                    }
                }
            }

            if (targetSlotIndex < 0) return false;

            var targetSlot = charInv.GetSlot(targetSlotIndex);
            if (targetSlot == null || !targetSlot.CanEquip(itemToEquip, out _)) return false;

            using (DungeonMaster.Instance?.SuppressSync())
            {
                // Remove 1 item from inventory slot
                RemoveSlot(userSlotIndex, 1, force: true);

                // Attempt equip onto character
                if (charInv.Equip(targetSlotIndex, itemToEquip, out var displacedItem, out _, confirmDestroyRune))
                {
                    if (displacedItem != null)
                    {
                        AddItem(displacedItem, 1, force: true);
                    }
                    return true;
                }

                // Rollback on failure
                AddItem(itemToEquip, 1, force: true);
                return false;
            }
        }

        /// <summary>
        /// Unequips an artifact from a character and transfers it back into player inventory.
        /// Runes cannot be unequipped (they must be overwritten or deleted).
        /// </summary>
        public bool UnequipFromCharacter(
            CharacterInventoryManager targetCharacter,
            int characterSlotIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (targetCharacter == null || targetCharacter.Inventory == null)
            {
                resultMessage = "Invalid character reference.";
                return false;
            }

            if (IsCombatLocked)
            {
                resultMessage = "Cannot change equipment during Combat Phase.";
                return false;
            }

            var charSlot = targetCharacter.Inventory.GetSlot(characterSlotIndex);
            if (charSlot == null || !charSlot.IsOccupied)
            {
                resultMessage = "Selected equipment slot is empty.";
                return false;
            }

            if (!charSlot.CanUnequip)
            {
                resultMessage = "Runes cannot be unequipped. They can only be replaced by inscribing a new rune or deleted.";
                return false;
            }

            var itemToUnequip = charSlot.EquippedItem;
            if (!CanAcceptItem(itemToUnequip, 1))
            {
                resultMessage = "Inventory is full! Cannot unequip item.";
                return false;
            }

            using (DungeonMaster.Instance?.SuppressSync())
            {
                if (targetCharacter.Inventory.Unequip(characterSlotIndex, out var unequippedItem))
                {
                    AddItem(unequippedItem, 1, force: true);
                    resultMessage = $"Unequipped {unequippedItem.DisplayName} to inventory.";
                    return true;
                }
            }

            resultMessage = "Failed to unequip item.";
            return false;
        }

        /// <summary>
        /// Permanently deletes an equipped rune from a character.
        /// </summary>
        public bool DestroyRuneFromCharacter(
            CharacterInventoryManager targetCharacter,
            int characterSlotIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (targetCharacter == null || targetCharacter.Inventory == null)
            {
                resultMessage = "Invalid character reference.";
                return false;
            }

            if (IsCombatLocked)
            {
                resultMessage = "Cannot modify runes during Combat Phase.";
                return false;
            }

            var charSlot = targetCharacter.Inventory.GetSlot(characterSlotIndex);
            if (charSlot == null || !charSlot.IsOccupied)
            {
                resultMessage = "Selected equipment slot is empty.";
                return false;
            }

            if (charSlot.SlotType != EquipmentSlotType.Rune && (charSlot.EquippedItem == null || charSlot.EquippedItem.ItemType != ItemType.Rune))
            {
                resultMessage = "Only runes can be deleted. Use unequip for artifacts.";
                return false;
            }

            if (targetCharacter.DestroyRune(characterSlotIndex, out var destroyedRune))
            {
                resultMessage = $"Deleted rune {destroyedRune.DisplayName}.";
                return true;
            }

            resultMessage = "Failed to delete rune.";
            return false;
        }

        #endregion

        #region Internal Lookup Maintenance

        private void UpdateSlotLookup(int slotIndex)
        {
            RebuildLookupMaps();
        }

        private void RebuildLookupMaps()
        {
            _itemToSlotsMap.Clear();
            _itemQuantityMap.Clear();

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.ItemData == null) continue;

                string id = slot.ItemData.ItemID;

                if (!_itemToSlotsMap.TryGetValue(id, out var indices))
                {
                    indices = new List<int>();
                    _itemToSlotsMap[id] = indices;
                }
                indices.Add(i);

                if (!_itemQuantityMap.TryGetValue(id, out int currentQty))
                {
                    _itemQuantityMap[id] = slot.Quantity;
                }
                else
                {
                    _itemQuantityMap[id] = currentQty + slot.Quantity;
                }
            }
        }

        private void NotifySlotUpdated(int index)
        {
            var slot = _slots[index];
            OnSlotUpdated?.Invoke(index, slot);
            OnSlotChanged?.Invoke(index, slot);
        }

        private void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

        #endregion

        #region Persistence & Serialization

        /// <summary>
        /// Exports the inventory state and character equipment into an InventorySaveData DTO.
        /// </summary>
        public InventorySaveData SaveToDTO(IEnumerable<CharacterInventoryManager> characters = null)
        {
            var dto = new InventorySaveData(Capacity);

            // 1. General inventory slots
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.ItemInstance == null || slot.ItemData == null) continue;

                var slotDTO = new InventorySlotSaveDTO(
                    slotIndex: i,
                    instanceID: slot.ItemInstance.InstanceID,
                    itemID: slot.ItemData.ItemID,
                    quantity: slot.Quantity,
                    upgradeTier: slot.ItemInstance.UpgradeTier
                );
                dto.slots.Add(slotDTO);
            }

            // 2. Character equipment (Runes & Artifacts)
            if (characters != null)
            {
                foreach (var character in characters)
                {
                    if (character == null || character.Inventory == null) continue;

                    string artifactId = null;
                    var runeIds = new List<string>();

                    for (int i = 0; i < character.Inventory.SlotCount; i++)
                    {
                        var slot = character.Inventory.GetSlot(i);
                        if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                        {
                            if (slot.SlotType == EquipmentSlotType.Artifact)
                            {
                                artifactId = slot.EquippedItem.ItemID;
                            }
                            else if (slot.SlotType == EquipmentSlotType.Rune)
                            {
                                runeIds.Add(slot.EquippedItem.ItemID);
                            }
                        }
                    }

                    dto.characterEquipments.Add(new CharacterEquipmentSaveDTO(
                        characterID: character.CharacterDisplayName,
                        artifactItemID: artifactId,
                        runeIDs: runeIds
                    ));
                }
            }

            return dto;
        }

        /// <summary>
        /// Restores inventory and character equipment state from an InventorySaveData DTO.
        /// </summary>
        public InventoryOperationResult LoadFromDTO(
            InventorySaveData saveData,
            IEnumerable<CharacterInventoryManager> characters = null,
            ItemDatabaseSO databaseOverride = null)
        {
            if (saveData == null)
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "Save data is null.");

            var db = databaseOverride != null ? databaseOverride : itemDatabase;
            if (db == null)
            {
                Debug.LogError("[InventoryManager] Cannot load save data: No ItemDatabaseSO assigned!", this);
                return InventoryOperationResult.Failure(InventoryOperationStatus.InvalidOperation, "No ItemDatabaseSO available to resolve item IDs.");
            }

            InitializeInventory(saveData.capacity);

            // 1. Restore inventory slots
            for (int i = 0; i < saveData.slots.Count; i++)
            {
                var slotDTO = saveData.slots[i];
                if (slotDTO.slotIndex < 0 || slotDTO.slotIndex >= _slots.Count) continue;

                var itemData = db.GetItem(slotDTO.itemID);
                if (itemData == null)
                {
                    Debug.LogWarning($"[InventoryManager] Item '{slotDTO.itemID}' not found in ItemDatabase during load.", this);
                    continue;
                }

                var instance = new InventoryItemInstance(itemData, slotDTO.upgradeTier, slotDTO.instanceID);
                _slots[slotDTO.slotIndex].Set(instance, slotDTO.quantity);
                NotifySlotUpdated(slotDTO.slotIndex);
            }

            // 2. Restore character equipment
            if (characters != null && saveData.characterEquipments != null)
            {
                var charDict = new Dictionary<string, CharacterInventoryManager>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in characters)
                {
                    if (c != null) charDict[c.CharacterDisplayName] = c;
                }

                foreach (var charDTO in saveData.characterEquipments)
                {
                    if (charDict.TryGetValue(charDTO.characterID, out var targetChar))
                    {
                        targetChar.InitializeInventory();
                        var charInv = targetChar.Inventory;

                        // Equip artifact
                        if (!string.IsNullOrEmpty(charDTO.artifactItemID))
                        {
                            var artItem = db.GetItem(charDTO.artifactItemID);
                            if (artItem != null)
                            {
                                int artSlot = charInv.FindFirstAvailableSlot(artItem);
                                if (artSlot >= 0) charInv.Equip(artSlot, artItem, out _, out _);
                            }
                        }

                        // Equip runes into rune slots
                        if (charDTO.inscribedRuneIDs != null)
                        {
                            int runeIndex = 0;
                            for (int s = 0; s < charInv.SlotCount && runeIndex < charDTO.inscribedRuneIDs.Count; s++)
                            {
                                var slot = charInv.GetSlot(s);
                                if (slot.SlotType == EquipmentSlotType.Rune)
                                {
                                    var runeItem = db.GetItem(charDTO.inscribedRuneIDs[runeIndex]);
                                    if (runeItem != null)
                                    {
                                        charInv.Equip(s, runeItem, out _, out _, confirmDestroyRune: true);
                                    }
                                    runeIndex++;
                                }
                            }
                        }
                    }
                }
            }

            RebuildLookupMaps();
            NotifyInventoryChanged();

            return InventoryOperationResult.Success("Successfully loaded inventory and character equipment.");
        }

        public string ToJson(IEnumerable<CharacterInventoryManager> characters = null, bool prettyPrint = false)
        {
            return SaveToDTO(characters).ToJson(prettyPrint);
        }

        public InventoryOperationResult LoadFromJson(
            string json,
            IEnumerable<CharacterInventoryManager> characters = null,
            ItemDatabaseSO databaseOverride = null)
        {
            var dto = InventorySaveData.FromJson(json);
            return LoadFromDTO(dto, characters, databaseOverride);
        }

        #endregion
    }
}
