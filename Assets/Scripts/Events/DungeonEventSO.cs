using System;
using UnityEngine;

namespace SBGames.Events
{
    /// <summary>
    /// Abstract base ScriptableObject for all random round events and dungeon encounters.
    /// Encapsulates trigger criteria, occurrence weights, lore presentation, and execution hooks.
    /// </summary>
    public abstract class DungeonEventSO : ScriptableObject
    {
        [Header("Event Identity")]
        [SerializeField] private string eventId = "Event_Default";
        [SerializeField] private string eventTitle = "Dungeon Encounter";
        [SerializeField] private string eventSubtitle = "Mysterious Occurrence";
        [SerializeField, TextArea(2, 4)] private string flavorDescription = "A sudden change echoes through the dark corridors of the dungeon.";
        [SerializeField] private Sprite eventIcon;

        [Header("Pacing & Occurrence Rules")]
        [Tooltip("Minimum round index required before this event can trigger.")]
        [SerializeField, Min(1)] private int minRound = 2;

        [Tooltip("Maximum times this event can trigger per run (0 = unlimited).")]
        [SerializeField, Min(0)] private int maxOccurrencesPerRun = 0;

        [Tooltip("Relative selection weight when rolled against other eligible events.")]
        [SerializeField, Range(0.1f, 10f)] private float weight = 1.0f;

        [Tooltip("Whether the player can minimize this event modal and reopen it before Planning concludes.")]
        [SerializeField] private bool allowMinimizing = true;

        public string EventId => eventId;
        public string EventTitle => eventTitle;
        public string EventSubtitle => eventSubtitle;
        public string FlavorDescription => flavorDescription;
        public Sprite EventIcon => eventIcon;
        public int MinRound => minRound;
        public int MaxOccurrencesPerRun => maxOccurrencesPerRun;
        public float Weight => weight;
        public bool AllowMinimizing => allowMinimizing;

        /// <summary>
        /// Evaluates if all prerequisites for this event are met (e.g. required units, resources, round).
        /// </summary>
        public virtual bool CanTrigger(EventTriggerContext ctx)
        {
            if (ctx.Round < minRound) return false;
            return true;
        }

        /// <summary>
        /// Executes the event logic (e.g. opening an interactive UI, applying buffs, or giving rewards).
        /// Calls <paramref name="onComplete"/> once the event is resolved or dismissed.
        /// </summary>
        public abstract void Execute(EventTriggerContext ctx, Action onComplete);
    }
}
