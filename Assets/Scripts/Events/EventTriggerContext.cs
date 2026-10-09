using SBGames.Loop;
using SBGames.Placement;
using SBGames.Placement.Bench;

namespace SBGames.Events
{
    /// <summary>
    /// Context payload provided to DungeonEventSO instances when evaluating trigger eligibility and executing logic.
    /// </summary>
    public readonly struct EventTriggerContext
    {
        public readonly int Round;
        public readonly DungeonMaster DM;
        public readonly DungeonPlacementManager PlacementManager;
        public readonly BenchManager BenchManager;
        public readonly CoreGameLoop CoreLoop;
        public readonly DungeonEventManager EventManager;

        public EventTriggerContext(
            int round,
            DungeonMaster dm,
            DungeonPlacementManager placementManager,
            BenchManager benchManager,
            CoreGameLoop coreLoop,
            DungeonEventManager eventManager)
        {
            Round = round;
            DM = dm;
            PlacementManager = placementManager;
            BenchManager = benchManager;
            CoreLoop = coreLoop;
            EventManager = eventManager;
        }
    }
}
