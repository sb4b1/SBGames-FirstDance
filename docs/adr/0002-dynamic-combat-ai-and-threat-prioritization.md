# 2. Dynamic Combat AI & Threat Prioritization (Beast vs Boss Archetypes)

Date: 2026-10-09

## Context
Combat AI in SBGames previously evaluated targets primarily through basic Euclidean distance proximity and scalar personality traits (e.g. `Bloodthirsty` execution priority). Boss encounters lacked multi-phase transitions, enrage states, and high-priority interrupt mechanisms, while pack-based units (such as Beasts and Goblins) lacked swarm coordination, flocking courage, and casualty-driven morale shock. 

To create distinct tactical behavior between solitary apex monsters (Bosses) and coordinated swarmers (Beast packs), we require a structured design unifying GOAP planning, squad coordination, and data-driven threat weighting.

## Decision

We establish the following architectural guidelines for Beast vs. Boss Combat AI:

1. **Pure GOAP Boss Phase Integration & Hard Interrupts**:
   - Boss phase transitions (e.g., Phase 2 Enrage, Add Summoning, Shielding) are modeled as high-urgency **Utility Goals** (`BossPhaseGoal`) with matching GOAP actions (`BossPhaseTransitionAction`).
   - When a Boss crosses a critical health threshold (e.g., ≤ 50% HP), the phase goal evaluates to `1.0`. A hard interrupt cancels any ongoing lower-priority action, triggers an un-interruptible roar/transition animation, and immediately begins Phase 2 mechanics.

2. **Hybrid Beast Pack Coordination & Morale Shock**:
   - Beast packs use the existing `PartyLeader` / `SquadMember` blackboard structure. An Alpha leader issues tactical `FocusFire` orders targeting wounded adventurers.
   - Individual pack members evaluate local peer density via `AllyTargetSensor`. Adjacent living pack members provide **Swarm Courage** (lowering fear intake and increasing threat multiplier).
   - If the Alpha leader is slain, or if >50% of the pack is eliminated within a short window, a **Morale Shock** event sets fear to maximum (100) for a brief duration, routing the survivors into `FleeAction`.

3. **Data-Driven Threat Prioritization (`ArchetypeThreatPolicySO`)**:
   - Replace hardcoded scoring routines with a modular `ArchetypeThreatPolicySO` containing normalized evaluation weights:
     - `ProximityWeight`
     - `LowestHpPercentWeight`
     - `RecentDamageDoneWeight`
     - `ClusterDensityWeight`
   - Evaluation is throttled to the 0.2s `VisionController` update and cached into preallocated buffers to ensure zero-allocation runtime performance at 60 FPS.

## Consequences

- **Pros**:
  - Unifies all behaviors within the CrashKonijn GOAP graph without introducing conflicting external state machines.
  - Enables designers to author unique threat behaviors (Sniper, Assassin, Swarmer, Apex Boss) via ScriptableObjects without modifying C#.
  - Emergent tactical counterplay (e.g. killing the Alpha beast scatters the pack).
- **Cons**:
  - Requires authoring new GOAP goals and actions for Boss phases.
  - Designers must balance threat policy weights to avoid target fluttering.
