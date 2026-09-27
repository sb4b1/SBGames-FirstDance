using UnityEngine;

namespace SBGames.Inventory.Core
{
    /// <summary>
    /// Base ScriptableObject defining an extensible effect triggered when a consumable item is used.
    /// Uses the Strategy Pattern so new item behaviors can be added without modifying the inventory core.
    /// </summary>
    public abstract class ConsumableEffect : ScriptableObject
    {
        /// <summary>
        /// Validates whether this effect can currently be applied to the target character.
        /// </summary>
        public abstract bool CanApply(GameObject target, CharacterStatsManager stats, out string failureReason);

        /// <summary>
        /// Applies the effect to the target character.
        /// Returns true if successfully consumed and applied.
        /// </summary>
        public abstract bool Apply(GameObject target, CharacterStatsManager stats);
    }
}
