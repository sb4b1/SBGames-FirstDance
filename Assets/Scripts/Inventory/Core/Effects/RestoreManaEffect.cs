using UnityEngine;

namespace SBGames.Inventory.Core.Effects
{
    /// <summary>
    /// Restores mana to a character. Validates that the character is not already at full mana.
    /// </summary>
    [CreateAssetMenu(fileName = "RestoreManaEffect", menuName = "SBGames/Inventory/Effects/Restore Mana")]
    public class RestoreManaEffect : ConsumableEffect
    {
        [Tooltip("Amount of mana to restore.")]
        [Min(1f)] public float ManaAmount = 20f;

        public override bool CanApply(GameObject target, CharacterStatsManager stats, out string failureReason)
        {
            if (stats == null)
            {
                failureReason = "Target character does not have a CharacterStatsManager.";
                return false;
            }

            if (stats.Mana >= stats.MaxMana.Value)
            {
                failureReason = "Character is already at full mana.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public override bool Apply(GameObject target, CharacterStatsManager stats)
        {
            if (!CanApply(target, stats, out _))
            {
                return false;
            }

            stats.RestoreMana(ManaAmount);
            return true;
        }
    }
}
