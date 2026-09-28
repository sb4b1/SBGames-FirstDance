using System;

namespace SBGames.Placement
{
    /// <summary>
    /// Represents the high-level gameplay phases in a roguelike dungeon wave cycle.
    /// </summary>
    public enum DungeonPhase
    {
        /// <summary>Preparation time: players can place, reposition, and sell units.</summary>
        PlanningPhase = 0,
        BuildPhase = 0,

        /// <summary>Active combat / wave defense: unit placement and modifications are locked.</summary>
        CombatPhase = 1
    }

    /// <summary>
    /// Contract for querying and updating the current dungeon phase.
    /// </summary>
    public interface IDungeonPhaseManager
    {
        DungeonPhase CurrentPhase { get; }
        bool CanPlaceOrModifyUnits { get; }
        void SetPhase(DungeonPhase phase);
        event Action<DungeonPhase> OnPhaseChanged;
    }

    /// <summary>
    /// Standard implementation of IDungeonPhaseManager.
    /// </summary>
    public class SimpleDungeonPhaseManager : IDungeonPhaseManager
    {
        private DungeonPhase currentPhase;

        public DungeonPhase CurrentPhase => currentPhase;
        public bool CanPlaceOrModifyUnits => currentPhase == DungeonPhase.PlanningPhase;

        public event Action<DungeonPhase> OnPhaseChanged;

        public SimpleDungeonPhaseManager(DungeonPhase initialPhase = DungeonPhase.PlanningPhase)
        {
            currentPhase = initialPhase;
        }

        public void SetPhase(DungeonPhase phase)
        {
            if (currentPhase == phase) return;
            currentPhase = phase;
            OnPhaseChanged?.Invoke(currentPhase);
        }
    }
}
