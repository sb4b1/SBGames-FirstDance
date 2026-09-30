namespace SBGames.Placement
{
    /// <summary>
    /// Distinguishes the primary entity classification for placeable objects.
    /// </summary>
    public enum EntityCategory
    {
        Monster = 0,
        Trap = 1,
        Hazard = 2,
        Obstacle = 3
    }

    /// <summary>
    /// Classification of trap functionality and destructibility.
    /// </summary>
    public enum TrapCategoryType
    {
        /// <summary>Stationary hazard trigger with no HP/pathfinding (Spikes, Poison Gas, Pitfall).</summary>
        MechanicalHazard = 0,

        /// <summary>Immobile destructible unit implementing IDamageable with HP (Mimics, Totems, Barricades).</summary>
        LivingUnit = 1
    }

    /// <summary>
    /// Trigger mechanics and detonation mode for a trap.
    /// </summary>
    public enum TrapTriggerType
    {
        /// <summary>Instant payload applied immediately upon collider entry.</summary>
        InstantBurst = 0,

        /// <summary>Periodic damage tick applied to entities while they remain inside the trigger zone.</summary>
        PeriodicTick = 1,

        /// <summary>Delayed explosion/triggering with a windup timer after initial contact.</summary>
        DelayedDetonation = 2
    }

    /// <summary>
    /// Entity target filtering for trap triggers.
    /// </summary>
    public enum TrapTargetFilter
    {
        /// <summary>Only triggers and deals damage to invading Adventurers/Heroes.</summary>
        AdventurersOnly = 0,

        /// <summary>Triggers on both monsters and adventurers (indiscriminate hazard).</summary>
        AllEntities = 1,

        /// <summary>Only triggers on dungeon monsters.</summary>
        MonstersOnly = 2
    }
}
