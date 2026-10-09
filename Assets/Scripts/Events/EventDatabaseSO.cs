using System.Collections.Generic;
using SBGames.Shop.Core;
using UnityEngine;

namespace SBGames.Events
{
    /// <summary>
    /// Central catalog holding registered DungeonEventSO assets.
    /// Filters eligible events by round constraints and handles weighted selection.
    /// </summary>
    [CreateAssetMenu(fileName = "EventDatabase", menuName = "SBGames/Events/Event Database")]
    public class EventDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<DungeonEventSO> events = new();

        public IReadOnlyList<DungeonEventSO> Events => events;

        /// <summary>
        /// Selects an eligible event using weighted random selection based on current context.
        /// </summary>
        public DungeonEventSO RollEligibleEvent(
            EventTriggerContext ctx,
            Dictionary<string, int> eventOccurrenceCounts,
            IRandomProvider rng = null)
        {
            if (events == null || events.Count == 0) return null;

            var candidates = new List<DungeonEventSO>();
            float totalWeight = 0f;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null) continue;

                // Check max occurrences
                if (evt.MaxOccurrencesPerRun > 0 && eventOccurrenceCounts != null)
                {
                    if (eventOccurrenceCounts.TryGetValue(evt.EventId, out int count) && count >= evt.MaxOccurrencesPerRun)
                        continue;
                }

                // Check custom trigger condition
                if (!evt.CanTrigger(ctx)) continue;

                candidates.Add(evt);
                totalWeight += Mathf.Max(0.01f, evt.Weight);
            }

            if (candidates.Count == 0) return null;

            float roll = rng != null ? rng.NextFloat() * totalWeight : UnityEngine.Random.Range(0f, totalWeight);
            float accum = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                accum += Mathf.Max(0.01f, candidates[i].Weight);
                if (roll <= accum)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }
    }
}
