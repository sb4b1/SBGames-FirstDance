using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// ScriptableObject defining static properties of an item.
    /// Acts as the single source of truth for item identifiers, display attributes, stacking, and stat modifiers.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemDefinition", menuName = "SBGames/Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id;
        [SerializeField] private string itemName;
        [SerializeField] private Sprite icon;
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Classification")]
        [SerializeField] private ItemType itemType = ItemType.Misc;
        [SerializeField] private EquipmentSlotType equippableSlotType = EquipmentSlotType.None;
        [SerializeField, Min(1)] private int maxStack = 1;

        [Header("Equipment Stats (Runes / Artifacts)")]
        [SerializeField] private List<ItemStatModifierData> statModifiers = new();

        [Header("Consumable Logic")]
        [SerializeField] private ConsumableEffect consumableEffect;

        public string Id => id;
        public string ItemName => itemName;
        public Sprite Icon => icon;
        public string Description => description;
        public ItemType ItemType => itemType;
        public EquipmentSlotType EquippableSlotType => equippableSlotType;
        public int MaxStack => maxStack;
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

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = name;
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = name;
            }

            // Equippables cannot stack
            if (IsEquippable)
            {
                maxStack = 1;
                if (equippableSlotType == EquipmentSlotType.None)
                {
                    equippableSlotType = itemType == ItemType.Rune ? EquipmentSlotType.Rune : EquipmentSlotType.Artifact;
                }
            }
            else
            {
                equippableSlotType = EquipmentSlotType.None;
            }
        }

        /// <summary>
        /// Runtime factory method to dynamically create an ItemDefinition for testing or procedural items.
        /// </summary>
        public static ItemDefinition CreateRuntimeInstance(
            string id,
            string itemName,
            ItemType itemType,
            EquipmentSlotType slotType = EquipmentSlotType.None,
            int maxStack = 1,
            IEnumerable<ItemStatModifierData> modifiers = null,
            ConsumableEffect effect = null)
        {
            if (itemType == ItemType.Consumable && maxStack <= 1)
            {
                maxStack = 99;
            }

            var def = CreateInstance<ItemDefinition>();
            def.id = id;
            def.itemName = itemName;
            def.itemType = itemType;
            def.equippableSlotType = slotType;
            def.maxStack = maxStack;
            def.consumableEffect = effect;

            if (modifiers != null)
            {
                def.statModifiers.AddRange(modifiers);
            }

            return def;
        }
    }
}
