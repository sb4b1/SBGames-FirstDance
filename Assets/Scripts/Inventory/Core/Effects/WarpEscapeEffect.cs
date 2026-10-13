using SBGames.Characters.AI;
using UnityEngine;

namespace SBGames.Inventory.Core.Effects
{
    /// <summary>
    /// Consumable effect strategy triggered upon completing the cast of a Warp Escape item.
    /// Does not restore life or mana; teleports the single carrier out of the dungeon, marking them Escaped.
    /// BYPASSES the DM Seal (RetreatBlocked is intentionally not checked).
    /// </summary>
    [CreateAssetMenu(fileName = "WarpEscapeEffect", menuName = "SBGames/Inventory/Effects/Warp Escape")]
    public class WarpEscapeEffect : ConsumableEffect
    {
        public override bool CanApply(GameObject target, CharacterStatsManager stats, out string failureReason)
        {
            if (target == null)
            {
                failureReason = "Target is null.";
                return false;
            }

            if (stats != null && stats.Life <= 0f)
            {
                failureReason = "Target is dead.";
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

            string unitId = target.name;
            if (target.TryGetComponent<CharacterIdentifier>(out var identifier) && !string.IsNullOrWhiteSpace(identifier.UniqueId))
            {
                unitId = identifier.UniqueId;
            }

            Vector3 escapePos = target.transform.position;

            // 1. If carrier is the squad leader, trigger emergent succession as a leader loss
            if (target.TryGetComponent<PartyLeader>(out var leader))
            {
                leader.HandleLeaderDeparted("Warped");
            }
            if (target.TryGetComponent<SquadMember>(out var member))
            {
                member.IsEscaped = true;
            }

            // 2. Raise escape event to notify AdventurerEventHub and CombatResultHandler
            AdventurerEventHub.RaiseUnitEscaped(unitId, target.name, escapePos);

            // 3. Remove carrier from dungeon (deactivate carrier GameObject)
            target.SetActive(false);

            return true;
        }
    }
}
