using UnityEngine;

namespace SBGames.Inventory.Core.Effects
{
    /// <summary>
    /// Restores life to a character. Validates that the character is not already at full life.
    /// </summary>
    [CreateAssetMenu(fileName = "RestoreHealthEffect", menuName = "SBGames/Inventory/Effects/Restore Health")]
    public class RestoreHealthEffect : ConsumableEffect
    {
        [Tooltip("Amount of life to heal.")]
        [Min(1f)] public float HealAmount = 25f;

        public override bool CanApply(GameObject target, CharacterStatsManager stats, out string failureReason)
        {
            if (stats == null)
            {
                failureReason = "Target character does not have a CharacterStatsManager.";
                return false;
            }

            if (stats.Life >= stats.MaxLife.Value)
            {
                failureReason = "Character is already at full life.";
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

            stats.RestoreLife(HealAmount);
            return true;
        }
    }
}
