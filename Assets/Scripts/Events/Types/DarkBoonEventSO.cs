using System;
using UnityEngine;

namespace SBGames.Events.Types
{
    /// <summary>
    /// Sample secondary event demonstrating the multi-event pool.
    /// The Dungeon Master can choose to sacrifice Vital Essence to gain a surge of Dark Energy.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_DarkBoon", menuName = "SBGames/Events/Dark Boon Event")]
    public class DarkBoonEventSO : DungeonEventSO
    {
        [Header("Boon Trade")]
        [SerializeField] private int essenceSacrifice = 15;
        [SerializeField] private int energyReward = 40;

        public int EssenceSacrifice => essenceSacrifice;
        public int EnergyReward => energyReward;

        public override bool CanTrigger(EventTriggerContext ctx)
        {
            if (!base.CanTrigger(ctx)) return false;
            // Only trigger if DM has enough essence to sacrifice without dying
            return ctx.DM != null && ctx.DM.Essence > essenceSacrifice;
        }

        public override void Execute(EventTriggerContext ctx, Action onComplete)
        {
            // Simple automated or console execution / demonstration fallback
            if (ctx.DM != null && ctx.DM.Essence > essenceSacrifice)
            {
                Debug.Log($"[DarkBoonEvent] Player offered dark boon: -{essenceSacrifice} Essence for +{energyReward} Energy.");
                // Player accepts boon if essence is healthy (> 50), otherwise declines
                if (ctx.DM.Essence >= 50)
                {
                    ctx.DM.LoseEssence(essenceSacrifice);
                    ctx.DM.GainEnergy(energyReward);
                    Debug.Log("[DarkBoonEvent] Boon accepted!");
                }
            }

            onComplete?.Invoke();
        }
    }
}
