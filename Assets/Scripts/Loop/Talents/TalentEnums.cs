using System;

namespace SBGames.Loop.Talents
{
    /// <summary>
    /// Categorical pillar of the DM Talent Tree.
    /// </summary>
    public enum TalentPillar
    {
        Core = 0,            // Foundational / Neutral DM abilities
        MonsterSwarm = 1,    // Minion quantity, swarm speed, death effects
        TrapControl = 2,     // Environmental hazards, area denial, energy harvesting
        ApexBoss = 3,        // Single-entity titanic units, auras, enrage mechanics
        Hybrid = 4,          // Cross-pillar fusions (e.g. Swarm+Trap, Trap+Boss, Swarm+Boss)
        Transcendent = 5     // High-rank endgame capstones (Rank A/S)
    }

    /// <summary>
    /// Progression tier of a talent node within the tree.
    /// </summary>
    public enum TalentTier
    {
        Tier0 = 0, // Root foundation
        Tier1 = 1, // Branch starters
        Tier2 = 2, // Tactical enhancers
        Tier3 = 3, // Specialization masteries
        Tier4 = 4  // Apex capstones
    }

    /// <summary>
    /// Behavioral type of the talent effect.
    /// </summary>
    public enum TalentType
    {
        Passive = 0,     // Autonomous permanent stat/mechanic modifier
        ActivePower = 1, // Manually or phase-triggered active power
        Aura = 2,        // Continuous spatial zone effect
        Triggered = 3    // Reactive on combat event (e.g. on unit death, on trap sprung)
    }

    /// <summary>
    /// Current availability / lock state of a talent for a player.
    /// </summary>
    public enum TalentUnlockStatus
    {
        Locked = 0,     // Prerequisites or rank requirement not met
        Available = 1,  // Prerequisites met and sufficient energy/rank to purchase
        AffordableOnlyWhenGained = 2, // Prerequisites met, but insufficient energy
        Unlocked = 3    // Already purchased and active
    }

    /// <summary>
    /// Boolean logic evaluation for nodes with multiple prerequisites.
    /// </summary>
    public enum TalentPrerequisiteLogic
    {
        All = 0, // Must have unlocked ALL listed prerequisites
        Any = 1  // Must have unlocked at least ONE of the listed prerequisites
    }
}
