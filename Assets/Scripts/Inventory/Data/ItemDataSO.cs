using System;
using System.Collections.Generic;
using SBGames.Inventory.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace SBGames.Inventory.Data
{
    /// <summary>
    /// Base ScriptableObject defining immutable static data for all inventory items, runes, and consumables.
    /// Acts as the single source of truth and MUST NEVER be mutated at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemData", menuName = "SBGames/Inventory/Item Data SO")]
    public class ItemDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier used for lookups, databases, and serialization.")]
        [FormerlySerializedAs("id")]
        [SerializeField] protected string itemID;

        [Tooltip("Localized or display name for UI.")]
        [FormerlySerializedAs("itemName")]
        [SerializeField] protected string displayName;

        [Tooltip("Sprite icon used in UI Toolkit grids and inspect cards.")]
        [SerializeField] protected Sprite icon;

        [TextArea(2, 5)]
        [Tooltip("Descriptive text displayed in item tooltips.")]
        [SerializeField] protected string description;

        [Header("Classification & Economy")]
        [Tooltip("Category of the item (Consumable, Rune, Artifact, Material, Misc).")]
        [SerializeField] protected ItemType itemType = ItemType.Misc;

        [Tooltip("Equipment slot compatibility if this item can be equipped.")]
        [SerializeField] protected EquipmentSlotType equippableSlotType = EquipmentSlotType.None;

        [Tooltip("Base gold or resource value for shop buying/selling.")]
        [SerializeField, Min(0)] protected int baseValue = 10;

        [Header("Stacking Rules")]
        [Tooltip("Whether multiple units of this item can be stacked in a single slot.")]
        [SerializeField] protected bool isStackable = false;

        [Tooltip("Maximum units permitted in a single slot.")]
        [SerializeField, Min(1)] protected int maxStack = 1;

        [Header("Sockets & Upgrades")]
        [Tooltip("Maximum number of rune sockets this item can hold (e.g. for weapons/artifacts).")]
        [SerializeField, Min(0)] protected int maxSockets = 0;

        [Header("Static Stat Modifiers (Runes / Equipment)")]
        [SerializeField] protected List<ItemStatModifierData> statModifiers = new();

        [Header("Consumable Effect")]
        [SerializeField] protected ConsumableEffect consumableEffect;

        // Public Read-Only Properties
        public string ItemID => itemID;
        public string Id => itemID;
        public string DisplayName => displayName;
        public string ItemName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public ItemType ItemType => itemType;
        public EquipmentSlotType EquippableSlotType => equippableSlotType;
        public int BaseValue => baseValue;
        public bool IsStackable => isStackable && maxStack > 1;
        public int MaxStack => isStackable ? Math.Max(1, maxStack) : 1;
        public int MaxSockets => maxSockets;
        public IReadOnlyList<ItemStatModifierData> StatModifiers => statModifiers;
        public ConsumableEffect ConsumableEffect => consumableEffect;

        public bool IsEquippable => itemType == ItemType.Rune || itemType == ItemType.Artifact;
        public bool IsConsumable => itemType == ItemType.Consumable;

        /// <summary>
        /// Validates if this item can be equipped into a specific equipment slot.
        /// </summary>
        public bool CanEquipInSlot(EquipmentSlotType slotType)
        {
            if (!IsEquippable) return false;
            return equippableSlotType == slotType;
        }

        [Obsolete("ItemDataSO is immutable static data at runtime and must not be mutated. Configure names in definitions or runtime instance models.", false)]
        public void Rename(string newName)
        {
            displayName = newName;
        }

        public void AddModifier(ItemStatModifierData modifier)
        {
            statModifiers.Add(modifier);
        }

        public void ClearModifiers()
        {
            statModifiers.Clear();
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemID))
            {
                itemID = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }

            if (!isStackable)
            {
                maxStack = 1;
            }
            else if (maxStack < 1)
            {
                maxStack = 1;
            }

            if (IsEquippable)
            {
                isStackable = false;
                maxStack = 1;
                if (equippableSlotType == EquipmentSlotType.None)
                {
                    equippableSlotType = itemType == ItemType.Rune ? EquipmentSlotType.Rune : EquipmentSlotType.Artifact;
                }
            }
        }

        public void InitializeRuntimeData(
            string id,
            string name,
            ItemType type,
            int maxStack = 1,
            int baseValue = 10,
            EquipmentSlotType slotType = EquipmentSlotType.None,
            IEnumerable<ItemStatModifierData> modifiers = null,
            ConsumableEffect effect = null,
            int maxSockets = 0)
        {
            this.itemID = id;
            this.displayName = name;
            this.itemType = type;
            this.isStackable = maxStack > 1 && type != ItemType.Rune && type != ItemType.Artifact;
            this.maxStack = this.isStackable ? maxStack : 1;
            this.baseValue = baseValue;
            this.equippableSlotType = slotType;
            this.consumableEffect = effect;
            this.maxSockets = maxSockets;

            if (modifiers != null)
            {
                this.statModifiers.Clear();
                this.statModifiers.AddRange(modifiers);
            }
        }

        /// <summary>
        /// Primary factory helper for creating runtime test assets or dynamic definitions without touching assets on disk.
        /// </summary>
        public static ItemDataSO CreateRuntimeInstance(
            string id,
            string itemName,
            ItemType itemType,
            EquipmentSlotType slotType = EquipmentSlotType.None,
            int maxStack = 1,
            int baseValue = 10,
            IEnumerable<ItemStatModifierData> modifiers = null,
            ConsumableEffect effect = null,
            int maxSockets = 0)
        {
            var data = CreateInstance<ItemDataSO>();
            data.InitializeRuntimeData(id, itemName, itemType, maxStack, baseValue, slotType, modifiers, effect, maxSockets);
            return data;
        }

        /// <summary>
        /// Convenience overload for creating consumable items with an effect directly.
        /// </summary>
        public static ItemDataSO CreateRuntimeInstance(
            string id,
            string itemName,
            ItemType itemType,
            ConsumableEffect effect)
        {
            var data = CreateInstance<ItemDataSO>();
            data.InitializeRuntimeData(id, itemName, itemType, itemType == ItemType.Consumable ? 99 : 1, 10, EquipmentSlotType.None, null, effect, 0);
            return data;
        }
    }
}
