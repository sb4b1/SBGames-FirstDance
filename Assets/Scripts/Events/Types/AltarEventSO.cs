using System;
using SBGames.Personality.Altar;
using SBGames.Personality.Data;
using UnityEngine;

namespace SBGames.Events.Types
{
    public enum AltarCurrencyType
    {
        Blood = 0,   // Costs DM Essence (Blood Currency)
        Arcane = 1   // Costs Energy
    }

    /// <summary>
    /// Concrete DungeonEventSO representing the Altar of Transmutation ritual.
    /// Rolls either a Blood Altar (costs DM Essence) or Arcane Altar (costs Energy),
    /// pulls owned units from Grid & Bench, and displays a 1-of-3 trait draft comparison UI.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_AltarOfTransmutation", menuName = "SBGames/Events/Altar Event")]
    public class AltarEventSO : DungeonEventSO
    {
        [Header("Altar Costs")]
        [Tooltip("Essence cost if rolled as a Blood Altar.")]
        [SerializeField, Min(1)] private int bloodCost = 10;

        [Tooltip("Energy cost if rolled as an Arcane Altar.")]
        [SerializeField, Min(1)] private int energyCost = 25;

        [Tooltip("Probability that this altar rolls as a Blood Altar (0.5 = 50% Blood, 50% Arcane).")]
        [SerializeField, Range(0f, 1f)] private float bloodAltarChance = 0.50f;

        [Header("Draft Configuration")]
        [SerializeField] private PersonalityDatabaseSO personalityDatabase;
        [SerializeField, Range(2, 5)] private int draftOptionCount = 3;

        public int BloodCost => bloodCost;
        public int EnergyCost => energyCost;
        public float BloodAltarChance => bloodAltarChance;
        public PersonalityDatabaseSO PersonalityDatabase => personalityDatabase;
        public int DraftOptionCount => draftOptionCount;

        public override bool CanTrigger(EventTriggerContext ctx)
        {
            if (!base.CanTrigger(ctx)) return false;

            // Only trigger if player has at least one unit owned on grid or bench
            int gridUnits = ctx.PlacementManager != null && ctx.PlacementManager.OccupancyTracker != null
                ? ctx.PlacementManager.OccupancyTracker.GetAllPlacedUnits().Count
                : 0;

            int benchUnits = ctx.BenchManager != null ? ctx.BenchManager.OccupiedCount : 0;

            return (gridUnits + benchUnits) > 0;
        }

        public override void Execute(EventTriggerContext ctx, Action onComplete)
        {
            if (AltarEventManager.Instance != null)
            {
                AltarEventManager.Instance.OpenAltar(this, ctx, onComplete);
            }
            else
            {
                Debug.LogWarning("[AltarEventSO] AltarEventManager.Instance is missing in scene!");
                onComplete?.Invoke();
            }
        }
    }
}
