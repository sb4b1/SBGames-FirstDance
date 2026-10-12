using UnityEngine;

namespace SBGames.Inventory.Core.Effects
{
    /// <summary>
    /// Removes active harmful status effects from a character via <see cref="StatusEffectManager"/>.
    /// Validates that the character currently has at least one harmful status effect.
    /// </summary>
    [CreateAssetMenu(fileName = "CureStatusEffect", menuName = "SBGames/Inventory/Effects/Cure Status")]
    public class CureStatusEffect : ConsumableEffect
    {
        public override bool CanApply(GameObject target, CharacterStatsManager stats, out string failureReason)
        {
            var statusManager = ResolveStatusManager(target, stats);
            if (statusManager == null)
            {
                failureReason = "Target character does not have a StatusEffectManager.";
                return false;
            }

            if (!statusManager.HasHarmfulEffect())
            {
                failureReason = "Character has no harmful status effects to cure.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public override bool Apply(GameObject target, CharacterStatsManager stats)
        {
            var statusManager = ResolveStatusManager(target, stats);
            if (statusManager == null || !statusManager.HasHarmfulEffect())
            {
                return false;
            }

            return statusManager.RemoveHarmfulEffects() > 0;
        }

        private static StatusEffectManager ResolveStatusManager(GameObject target, CharacterStatsManager stats)
        {
            if (target != null)
            {
                var mgr = target.GetComponent<StatusEffectManager>()
                    ?? target.GetComponentInParent<StatusEffectManager>()
                    ?? target.GetComponentInChildren<StatusEffectManager>();
                if (mgr != null) return mgr;
            }

            if (stats != null)
            {
                return stats.GetComponent<StatusEffectManager>()
                    ?? stats.GetComponentInParent<StatusEffectManager>()
                    ?? stats.GetComponentInChildren<StatusEffectManager>();
            }

            return null;
        }
    }
}
