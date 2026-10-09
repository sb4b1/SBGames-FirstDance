using System;
using System.Collections.Generic;
using SBGames.Loop;
using SBGames.Personality.Altar;
using SBGames.Placement;
using SBGames.Placement.Bench;
using UnityEngine;

namespace SBGames.Events
{
    /// <summary>
    /// Central manager orchestrating start-of-round Dungeon Events and Encounters.
    /// Listens to CoreGameLoop.OnRoundStarted, tracks round pacing/pity guarantees,
    /// rolls eligible DungeonEventSO assets, and manages active/minimized event lifecycles.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonEventManager : MonoBehaviour
    {
        public static DungeonEventManager Instance { get; private set; }

        [Header("Event Configuration")]
        [SerializeField] private EventDatabaseSO eventDatabase;
        [SerializeField, Range(0f, 1f)] private float baseTriggerChance = 0.40f;
        [SerializeField, Min(1)] private int minRoundToStart = 2;
        [SerializeField, Min(0)] private int cooldownRoundsBetweenEvents = 1;
        [SerializeField, Min(1)] private int pityRoundsThreshold = 3;

        [Header("Scene Dependencies (Auto-found if null)")]
        [SerializeField] private CoreGameLoop coreGameLoop;
        [SerializeField] private DungeonMaster dungeonMaster;
        [SerializeField] private DungeonPlacementManager placementManager;
        [SerializeField] private BenchManager benchManager;

        // Runtime State
        private int _roundsSinceLastEvent = 0;
        private DungeonEventSO _activeEvent;
        private bool _isEventMinimized = false;
        private EventTriggerContext _activeContext;
        private readonly Dictionary<string, int> _occurrenceCounts = new(StringComparer.OrdinalIgnoreCase);

        public DungeonEventSO ActiveEvent => _activeEvent;
        public bool HasActiveEvent => _activeEvent != null;
        public bool IsEventMinimized => _isEventMinimized;
        public int RoundsSinceLastEvent => _roundsSinceLastEvent;
        public EventDatabaseSO EventDatabase { get => eventDatabase; set => eventDatabase = value; }

        // Events
        public event Action<DungeonEventSO> OnEventTriggered;
        public event Action<DungeonEventSO> OnEventResolved;
        public event Action<bool> OnEventMinimizedChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ResolveDependencies();
        }

        private void Start()
        {
            ResolveDependencies();
            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted += HandleRoundStarted;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (coreGameLoop != null)
            {
                coreGameLoop.OnRoundStarted -= HandleRoundStarted;
            }
        }

        private void ResolveDependencies()
        {
            if (coreGameLoop == null) coreGameLoop = FindAnyObjectByType<CoreGameLoop>();
            if (dungeonMaster == null) dungeonMaster = FindAnyObjectByType<DungeonMaster>();
            if (placementManager == null) placementManager = DungeonPlacementManager.Instance ?? FindAnyObjectByType<DungeonPlacementManager>();
            if (benchManager == null) benchManager = BenchManager.Instance ?? FindAnyObjectByType<BenchManager>();
        }

        private void HandleRoundStarted(int round)
        {
            // Close any unresolved previous event safely
            if (_activeEvent != null)
            {
                ResolveActiveEvent();
            }

            _roundsSinceLastEvent++;

            if (round < minRoundToStart) return;
            if (_roundsSinceLastEvent <= cooldownRoundsBetweenEvents) return;

            bool shouldTrigger = false;
            // Pity check: if no event occurred for pityRoundsThreshold rounds, guarantee trigger
            if (_roundsSinceLastEvent >= pityRoundsThreshold)
            {
                shouldTrigger = true;
            }
            else
            {
                float roll = UnityEngine.Random.value;
                shouldTrigger = roll <= baseTriggerChance;
            }

            if (!shouldTrigger) return;

            TriggerRandomEvent(round);
        }

        /// <summary>
        /// Rolls an eligible event from the database and executes it.
        /// </summary>
        public bool TriggerRandomEvent(int round)
        {
            if (eventDatabase == null) return false;

            ResolveDependencies();
            var ctx = new EventTriggerContext(round, dungeonMaster, placementManager, benchManager, coreGameLoop, this);
            var chosen = eventDatabase.RollEligibleEvent(ctx, _occurrenceCounts);

            if (chosen == null) return false;

            ExecuteEvent(chosen, ctx);
            return true;
        }

        /// <summary>
        /// Manually triggers a specific event (or rolls one if null). Ideal for testing and dev tools.
        /// </summary>
        public void ForceTriggerEvent(DungeonEventSO specificEvent = null)
        {
            ResolveDependencies();
            int round = coreGameLoop != null ? coreGameLoop.CurrentRound : 1;
            var ctx = new EventTriggerContext(round, dungeonMaster, placementManager, benchManager, coreGameLoop, this);

            var evt = specificEvent ?? (eventDatabase != null ? eventDatabase.RollEligibleEvent(ctx, _occurrenceCounts) : null);
            if (evt != null)
            {
                ExecuteEvent(evt, ctx);
            }
        }

        private void ExecuteEvent(DungeonEventSO evt, EventTriggerContext ctx)
        {
            _activeEvent = evt;
            _activeContext = ctx;
            _isEventMinimized = false;
            _roundsSinceLastEvent = 0;

            if (!_occurrenceCounts.ContainsKey(evt.EventId))
                _occurrenceCounts[evt.EventId] = 0;
            _occurrenceCounts[evt.EventId]++;

            OnEventTriggered?.Invoke(evt);

            evt.Execute(ctx, ResolveActiveEvent);
        }

        /// <summary>
        /// Minimizes the active event modal so the player can inspect the grid/shop,
        /// leaving an active event pill on screen.
        /// </summary>
        public void MinimizeActiveEvent()
        {
            if (_activeEvent == null) return;
            _isEventMinimized = true;
            OnEventMinimizedChanged?.Invoke(true);
        }

        /// <summary>
        /// Reopens a minimized active event modal.
        /// </summary>
        public void ReopenActiveEvent()
        {
            if (_activeEvent == null) return;
            _isEventMinimized = false;
            OnEventMinimizedChanged?.Invoke(false);

            // Re-trigger view execution
            _activeEvent.Execute(_activeContext, ResolveActiveEvent);
        }

        /// <summary>
        /// Concludes and dismisses the currently active event.
        /// </summary>
        public void ResolveActiveEvent()
        {
            if (_activeEvent == null) return;

            // Ensure any active sub-event UI (such as Altar modal or reopen pill) is closed cleanly
            if (AltarEventManager.Instance != null)
            {
                AltarEventManager.Instance.CloseAltarForcefully();
            }

            var resolvedEvt = _activeEvent;
            _activeEvent = null;
            _isEventMinimized = false;

            OnEventMinimizedChanged?.Invoke(false);
            OnEventResolved?.Invoke(resolvedEvt);
        }
    }
}
