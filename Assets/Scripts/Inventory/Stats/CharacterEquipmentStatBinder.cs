using System;
using System.Collections.Generic;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using UnityEngine;

namespace SBGames.Inventory.Stats
{
    /// <summary>
    /// Mediates the bidirectional relationship between a CharacterInventory and a CharacterStatsManager.
    /// Observes item equip/unequip events on CharacterInventory and automatically creates, applies,
    /// or removes StatModifiers on the character's Stats.
    ///
    /// Design guarantees:
    /// 1. Modifiers use the item as their 'Source' object, enabling targeted removal without affecting buffs/debuffs.
    /// 2. External modifications to CharacterStatsManager (level-ups, combat buffs) never leak into or alter inventory state.
    /// 3. Emits OnStatsChanged so observers (UI, gameplay systems) can react immediately to stat changes.
    /// </summary>
    public class CharacterEquipmentStatBinder : IDisposable
    {
        private readonly CharacterInventory _characterInventory;
        private readonly CharacterStatsManager _statsManager;

        public event Action OnStatsChanged;

        public CharacterEquipmentStatBinder(CharacterInventory characterInventory, CharacterStatsManager statsManager)
        {
            _characterInventory = characterInventory ?? throw new ArgumentNullException(nameof(characterInventory));
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));

            _characterInventory.OnItemEquipped += HandleItemEquipped;
            _characterInventory.OnItemUnequipped += HandleItemUnequipped;

            // Apply any already equipped items (e.g. if loaded from save data or inspector defaults)
            ApplyAllCurrentEquipment();
        }

        /// <summary>
        /// Applies stat modifiers for an equipped item.
        /// </summary>
        private void HandleItemEquipped(int slotIndex, EquipmentSlot slot, ItemDefinition item)
        {
            if (item == null || _statsManager == null) return;

            ApplyModifiersFromItem(item);
            OnStatsChanged?.Invoke();
        }

        /// <summary>
        /// Removes all stat modifiers originating from the unequipped item.
        /// </summary>
        private void HandleItemUnequipped(int slotIndex, EquipmentSlot slot, ItemDefinition item)
        {
            if (item == null || _statsManager == null) return;

            RemoveModifiersFromItem(item);
            OnStatsChanged?.Invoke();
        }

        private void ApplyModifiersFromItem(ItemDefinition item)
        {
            IReadOnlyList<ItemStatModifierData> modifiers = item.StatModifiers;
            if (modifiers == null || modifiers.Count == 0) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modData = modifiers[i];
                var stat = _statsManager.GetStat(modData.StatType);
                if (stat != null)
                {
                    var modifier = modData.ToStatModifier(item);
                    stat.AddModifier(modifier);
                }
            }
        }

        private void RemoveModifiersFromItem(ItemDefinition item)
        {
            IReadOnlyList<ItemStatModifierData> modifiers = item.StatModifiers;
            if (modifiers == null || modifiers.Count == 0) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modData = modifiers[i];
                var stat = _statsManager.GetStat(modData.StatType);
                if (stat != null)
                {
                    stat.RemoveAllModifiersFromSource(item);
                }
            }
        }

        private void ApplyAllCurrentEquipment()
        {
            for (int i = 0; i < _characterInventory.SlotCount; i++)
            {
                var slot = _characterInventory.GetSlot(i);
                if (slot != null && slot.IsOccupied)
                {
                    ApplyModifiersFromItem(slot.EquippedItem);
                }
            }
        }

        /// <summary>
        /// Detaches all event listeners and removes all active equipment modifiers from the character's stats.
        /// </summary>
        public void Dispose()
        {
            if (_characterInventory != null)
            {
                _characterInventory.OnItemEquipped -= HandleItemEquipped;
                _characterInventory.OnItemUnequipped -= HandleItemUnequipped;

                for (int i = 0; i < _characterInventory.SlotCount; i++)
                {
                    var slot = _characterInventory.GetSlot(i);
                    if (slot != null && slot.IsOccupied)
                    {
                        RemoveModifiersFromItem(slot.EquippedItem);
                    }
                }
            }

            OnStatsChanged = null;
        }
    }
}
