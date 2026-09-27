using System;
using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Serializable data definition for a stat modification attached to an equippable item (Rune/Artifact).
    /// Converts into a runtime StatModifier instance referencing the item as the source.
    /// </summary>
    [Serializable]
    public struct ItemStatModifierData
    {
        [Tooltip("Target stat that will receive this modifier.")]
        public StatType StatType;

        [Tooltip("Numerical value of the modifier (flat value, or decimal percentage e.g. 0.15 for +15%).")]
        public float Value;

        [Tooltip("Type of modification: Flat (100), PercentAdd (200), or PercentMult (300).")]
        public StatModType ModType;

        [Tooltip("Optional order override. Leave at -1 to use the default enum order based on ModType.")]
        public int OrderOverride;

        public ItemStatModifierData(StatType statType, float value, StatModType modType, int orderOverride = -1)
        {
            StatType = statType;
            Value = value;
            ModType = modType;
            OrderOverride = orderOverride;
        }

        /// <summary>
        /// Creates a runtime StatModifier with the specified source object.
        /// </summary>
        public StatModifier ToStatModifier(object source)
        {
            int order = OrderOverride >= 0 ? OrderOverride : (int)ModType;
            return new StatModifier(Value, ModType, order, source);
        }
    }
}
