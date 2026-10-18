# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-18 | Latest Commit: 8779333 feat: add character assets, prefabs, animators, and AI GOAP capabilities for adventurers and monsters*

## 1. Recently Completed Milestones & Features
- **AI Healing Behavior & Healer Combat Loop ([Report](file:///d:/UnityProject/SBGames/docs/testing/heal_proof_and_edc_comparison_report.md)):**
  - Implemented full GOAP and Utility AI support for ally healing: [`HealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Goals/HealAllyGoal.cs), [`HealAllyAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/HealAllyAction.cs), [`UtilityHealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs), [`HealCapabilityFactory`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Capabilities/HealCapabilityFactory.cs), [`WoundedAllyTargetSensor`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Sensors/WoundedAllyTargetSensor.cs), [`WoundedAllyTarget`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/TargetKeys/Target/WoundedAllyTarget.cs), and [`AlliesNeedHealing`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/WorldKeys/AlliesNeedHealing.cs) world key.
  - Resolved historic "70 casts, 0 HP healed" bug across four root causes: added target alive/deficit validation to prevent phantom casts, established the `AlliesNeedHealing` world key decrease effect in GOAP planning, corrected scene container desync where `SpawnedCombatUnits` was passed instead of character root to `SkillCaster`, and resolved child hitbox collider mismatches in `SkillHitResolver`.
  - Authored heal skill assets: `SKILL_HEAL_001.prefab`, `SKILL_HEAL_001.asset`, `DefaultHealAllyGoal.asset`, and `Utility_SKILL_HEAL_001.asset` (+25 HP restored, 10 mana consumed, `AlliesOnly` target filtering).
  - Split healing telemetry in [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs) into distinct `skillHealingDone` vs `potionHealingDone` metrics and verified Rank E/D/C reaction latencies.
- **Adaptive Sensor Pulse Intervals in VisionController ([Report](file:///d:/UnityProject/SBGames/docs/testing/adaptive_sensor_pulse_intervals_report.md)):**
  - Replaced uniform $0.20\text{s}$ perception polling in [`VisionController.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/VisionController.cs) with dynamic state-driven tiers via immutable [`PerceptionTuningSO.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/PerceptionTuningSO.cs):
    - **Calm ($0.40\text{s}$ / $2.5\text{ pulses/s}$):** $50\%$ CPU reduction when units are idle, wandering, or scouting without known threats.
    - **Default ($0.20\text{s}$ / $5.0\text{ pulses/s}$):** Standard operational baseline.
    - **Engaged ($0.12\text{s}$ / $8.33\text{ pulses/s}$):** High-responsiveness acquisition during active combat.
    - **Critical ($0.05\text{s}$ / $20.0\text{ pulses/s}$):** Emergency burst acuity on low HP, morale shock, or fallback commands.
  - Implemented asymmetric tier dwell hysteresis ($0.50\text{s}$ dwell before demotion, instant escalation), deterministic desynchronization offsets, and rate-limited reactive force pulses (`RequestImmediatePulse()`).
  - Added unit test suite [`VisionControllerAdaptivePulseTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/VisionControllerAdaptivePulseTests.cs) (5 passing tests).
- **Combat Sandbox AI Audit & Regression Matrix ([Report](file:///d:/UnityProject/SBGames/docs/testing/flee_ranged_spawn_parity_audit_report.md)):**
  - Audited Tank flee mechanics: confirmed `UtilityRecoverGoal` intentionally overrides `UtilitySurviveGoal` while recovery items are available (`surviveOverrideFloor = 1.02f` vs clamped `1.0f`).
  - Audited melee attack range clamping vs ranged skill targeting (`CastSkillAction`), validating behaviors for `ADVENTURER_N099` (Mage) and `MONSTER_N003`.
  - Documented spawn parity between test harness spawning and production dungeon placement.
  - Executed 18-run regression matrix across all 9 benchmark scenarios across multiple seeds: 0 timeouts, 0 console errors.
- **Character Roster Assets & Boss Integration:**
  - Added adventurer ScriptableObjects and assets for `ADVENTURER_N002`, `ADVENTURER_N003`, `ADVENTURER_N004`, `ADVENTURER_N005`.
  - Added boss monster prefab `MONSTER_BOSS_001.prefab` and ScriptableObject `MONSTER_BOSS_001.asset`.
  - Cleaned up root archive `Assets/Scripts.zip`.
- **Dungeon Piece System & 2D Camera Controller ([ADR 0003](file:///d:/UnityProject/SBGames/docs/adr/0003-dungeon-piece-system-and-selection.md)):**
  - Modular piece generation (`DungeonPiece`, `PieceSetSO`, `DungeonPieceDescriptor`, `DungeonPieceSelector`) with dynamic dimensions ($W \times H \ge 4 \times 4$), structural roles (`Room` vs `Pathway`), freeform 2-tile openings, and non-square quarter-turn rotation cycles.
  - Collision-free seam snapping solver (`DungeonSeamLayoutSolver`) and automatic opening capping.
  - Dynamic room capacity calculation derived from piece-local `BuildableMask` and placement crowding penalties (`IRoomCapacityTracker`, `DungeonRoomCapacityTracker`).
  - 2D camera controller ([`CameraController2D`](file:///d:/UnityProject/SBGames/Assets/Scripts/Camera/CameraController2D.cs)) with 1:1 right-click drag pan, cursor-centered zoom, smart drag discrimination, and dynamic arena bounds clamping.
- **Unity Editor & Test Suite Status:**
  - 26 test suites in `Assets/Tests/` with all tests passing.
  - Live Unity Editor verified via Unity CLI (`unity status`, `unity cmd console`): PID 13632 running on port 7800, 0 compilation errors, 0 active console errors.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
Current C# file counts across `Assets/Scripts/` (339 total scripts), `Assets/Editor/` (29 total scripts), and `Assets/Tests/` (26 total scripts):

| Folder | C# files | Role / Key Modules |
| --- | ---: | --- |
| `Assets/Scripts/Character` | 132 | Character identity, controllers, traps, AI profiles/memory, adaptive perception, and GOAP/Utility AI system (+8 scripts). |
| `Assets/Scripts/Dungeon` | 26 | Room/door markers, registry, procedural layout graph/generator, dynamic piece system, seam layout solver, and runtime builder. |
| `Assets/Scripts/Placement` | 26 | Dungeon placement system, grid visualizer, bench, buildable mask validator, and room capacity tracking. |
| `Assets/Scripts/Loop` | 26 | Core game loop, combat results handler, DM abilities/talents, post-battle reports, and progression/fame. |
| `Assets/Scripts/Inventory` | 26 | Inventory, equipment, items, consumable effects (including Warp escape), and save data. |
| `Assets/Scripts/Database` | 13 | Catalog ScriptableObjects, CSV parsers, character/item databases, and editor sync pipeline. |
| `Assets/Scripts/Skills` | 13 | Skill casting, targeting, projectile pooling, ally/enemy hit resolution, and skill database. |
| `Assets/Scripts/Shop` | 12 | Shop manager, merchant inventories, pricing, and economy loop. |
| `Assets/Scripts/Squads` | 11 | Party leader, squad blackboard, squad member coordination, tactical evaluators, and scout intel HUD. |
| `Assets/Scripts/Stat` | 9 | Character base stats, stat managers, damage pipeline, and healing restoration (`RestoreLife`). |
| `Assets/Scripts/Personality` | 7 | Character personality traits, personality profiles, and behavioral biases. |
| `Assets/Scripts/Events` | 6 | Dungeon event definitions, event manager, contexts, and altar/dark boon events. |
| `Assets/Scripts/Input` | 4 | Input management and keybinding configurations. |
| `Assets/Scripts/Testing` | 4 | Combat test environment bootstrap, combat test HUD, and simulation runners (`BattleSimulationTestRunner`). |
| `Assets/Scripts/Interface` | 4 | Core entity interfaces (`IDamageable`, `IPoolable`, `ITargetable`, `IInteractable`). |
| `Assets/Scripts/StatusEffect` | 3 | Status effect manager, controllers, and status effect instances. |
| `Assets/Scripts/Manager` | 3 | High-level game and audio managers. |
| `Assets/Scripts/Factions` | 3 | Faction identifiers, alignments, and ally verification (`IFactionMember.IsAllyOf`). |
| `Assets/Scripts/Gizmo` | 2 | Editor visualization gizmo helpers. |
| `Assets/Scripts/SB4b1` | 2 | Shared mathematical helpers and utility extensions. |
| `Assets/Scripts/Camera` | 1 | `CameraController2D` (pan, zoom, mouse-drag filtering, arena clamping). |
| `Assets/Scripts/Hierarchy` | 1 | Hierarchy header visual organization tool. |
| `Assets/Scripts/Environment` | 1 | Environmental obstacles and interactive props. |
| `Assets/Scripts/Editor` | 1 | Runtime editor helper hooks. |
| `Assets/Scripts/Base` | 1 | Base identifier types. |
| `Assets/Scripts/Examples` | 0 | Example templates directory. |

### Key Modules Added or Refactored
- [`HealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Goals/HealAllyGoal.cs) & [`HealAllyAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/HealAllyAction.cs):
  - **Responsibilities:** GOAP goal and action pair for commanding healer units to cast targeted restoration spells. Action validates target vitality and distance, aborting if the target is already full HP or deceased.
  - **Key API:** `Perform(IMonoAgent, ActionData)`, `HealHpThreshold = 0.50f`.
- [`UtilityHealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs):
  - **Responsibilities:** Utility AI goal scoring heal urgency between 0.55 and 0.90 based on ally HP deficit, factoring in healer role, class affinity, and self-preservation constraints.
  - **Key API:** `Evaluate(BaseAI)`, `GetBestWoundedAllyTarget(float)`.
- [`PerceptionTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/PerceptionTuningSO.cs):
  - **Responsibilities:** Immutable configuration asset defining perception poll intervals across Calm, Default, Engaged, and Critical tiers, dwell hysteresis, and force-pulse rate limits.
  - **Key API:** `CalmInterval`, `DefaultInterval`, `EngagedInterval`, `CriticalInterval`, `MinTierDwellDuration`, `ForcePulseCooldown`.
- [`VisionController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/VisionController.cs) *(Refactored)*:
  - **Responsibilities:** Executes perception scanning on dynamic intervals based on current tier, manages asymmetric dwell timers to prevent tier thrashing, applies randomized start phase offsets, and exposes reactive pulse requests.
  - **Key API:** `CurrentTier`, `EffectivePulseInterval`, `RequestImmediatePulse()`, `SetTuning(PerceptionTuningSO)`.
- [`BattleSimulationTestRunner`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs) *(Refactored)*:
  - **Responsibilities:** Automated harness for sandbox combat scenarios; records DPS, accuracy, swing counts, role damage taken, skill vs potion healing metrics, and reaction latencies.
  - **Key API:** `StartSimulation()`, `EndSimulation()`, `SkillHealingDone`, `PotionHealingDone`.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI Catalog:**
  - **14 Goals (`GoalBase`):** `AlertAlliesGoal`, `DebriefLeaderGoal`, `FallBackGoal`, `FocusFireGoal`, `FollowLeaderGoal`, `HealAllyGoal`, `IdleGoal`, `KeepDistanceGoal`, `KillEnemyGoal`, `RecoverGoal`, `ScoutReconGoal`, `StandbyGoal`, `SurviveGoal`, `WarpGoal`.
  - **14 Actions (`GoapActionBase`):** `AlertAlliesAction`, `AttackAction`, `CastSkillAction`, `DebriefLeaderAction`, `FallBackAction`, `FleeAction`, `FocusFireAction`, `FollowLeaderAction`, `HealAllyAction`, `IdleAction`, `RecoverAction`, `ScoutReconAction`, `WanderAction`, `WarpAction`.
  - **13 Utility Goals (`UtilityGoal`):** `UtilityAlertAlliesGoal`, `UtilityDebriefGoal`, `UtilityFallBackGoal`, `UtilityFocusFireGoal`, `UtilityFollowLeaderGoal`, `UtilityHealAllyGoal`, `UtilityKillEnemyGoal`, `UtilityRecoverGoal`, `UtilityScoutReconGoal`, `UtilityStandbyGoal`, `UtilitySurviveGoal`, `UtilityWanderGoal`, `UtilityWarpGoal`.
  - **Utility Skills:** `UtilityBaseSkill`, `UtilityFireSpell`, `UtilityNoAvailableSkill`.
  - **15 World Keys (`WorldKeyBase`):** `AgentLife`, `AgentMana`, `AlliesNeedHealing`, `CanWarp`, `EnemyDistance`, `FearLevel`, `HasAlertedAllies`, `HasDebriefedLeader`, `HasScoutedArea`, `HasSquadOrder`, `IsIdle`, `IsSkillReady`, `NeedsRecovery`, `SquadOrderTypeKey`, `ThreatLevel`.
  - **10 Target Keys (`TargetKeyBase`):** `EnemyTarget`, `FocusFireTarget`, `KeepDistanceTarget`, `LeaderTarget`, `RecoverTarget`, `ScoutReconTarget`, `SelfTarget`, `WanderTarget`, `WarpTarget`, `WoundedAllyTarget`.
  - **10 Capability Factories:** `AttackCapabilityFactory`, `BasicCapabilityFactory`, `HealCapabilityFactory`, `MinionBasicCapabilityFactory`, `RecoveryCapabilityFactory`, `SkillCapabilityFactory`, `SquadCapabilityFactory`, `SurvivalCapabilityFactory`, `WanderCapabilityFactory`, `WarpCapabilityFactory`.
  - **Agent Type Capabilities Mapping:**
    - `NormalAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Warp, Heal.
    - `MageAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Warp.
    - `MinionAgent`: Wander, Attack, MinionBasic, Squad, Survival *(Recovery, Warp, and Heal omitted)*.
- **Core Systems & Services Wiring:**
  - `BaseAI`: Bridges Utility AI goal evaluations to CrashKonijn GOAP planning. Caches perception (`VisionController`), character stats (`CharacterStatsManager`), squad membership (`SquadMember`), personality traits (`CharacterPersonality`), memory (`CharacterAIMemory`), item kit usage (`AdventurerItemKitController`), and doorway staging (`DoorwayStagingController`). Implements goal hysteresis (`goalHysteresisBonus = 0.08f`) and provides `GetBestWoundedAllyTarget` for ally scanning.
  - `PartyLeader` & `SquadBlackboard`: Maintains squad operational orders (`Engage`, `FallBack`, `ScoutRecon`, `Standby`), room clearance progress, hazard memory, focus-fire targets, and leader succession.
  - `DungeonRoomRegistry`: Tracks all active `RoomVolume` instances and placed `DungeonPiece` components in scene for camera bounds clamping, DM placement validation, room lookups, and AI room event triggers.
  - `DungeonPlacementManager` & `DungeonPlacementSystem`: Manages grid coordinate validation, occupancy tracking, resource wallet spending, buildable mask checking, and room capacity enforcement (`IRoomCapacityTracker`).
  - `GameServices`: Central service registry resolving singleton dependencies (`DungeonPlacementManager`, `CharacterManager`, `ObjectPoolManager`, etc.) by exact type name.
  - `CoreGameLoop`: Oversees high-level game states, phase transitions (`PlanningPhase`, `ActionPhase`), round timers, and victory/defeat evaluation.
- **Cross-System Event Hooks:**
  - `SkillHitResolver`: Dispatches `OnSkillHealApplied(caster, target, amount)` for test monitoring and UI floaters.
  - `AdventurerEventHub`: Publishes leader death/succession, checkpoint updates, room entry/clearing, potion/warp item usage, retreat blockades, DM seal triggers, and escape completion.
  - `CombatResultHandler`: Dispatches `OnCombatEnded`, `OnSquadWiped`, `OnAdventurersEscaped` to evaluate battle metrics and trigger `PostBattleReport`.
  - `CoreGameLoop`: Emits `OnPhaseChanged`, `OnRoundStarted`, `OnGameOver`.
  - `DMSealAbilityController` & `DungeonMaster`: Listens to seal triggers, energy accumulation, and talent purchases.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:**
  - Ensure healer prefabs (e.g., `ADVENTURER_N001` or support archetypes) have `DefaultHealAllyGoal` in their `BaseAI.goals` list and `SKILL_HEAL_001` in their `CharacterSkillController.skills`.
  - Assign `PerceptionTuning_Default.asset` to the `VisionController` serialized field on character prefabs to benefit from the adaptive perception polling tiers.
  - In `Combat_Sandbox.unity`, ensure `BattleSimulationTestRunner` harness is configured with the desired adventurer and monster prefabs.
  - Confirm that `CameraController2D` is attached to the Main Camera with `clampToArenaBounds = true` across gameplay scenes.
- **Known Gotchas / Architectural Constraints:**
  - **MageAgent Healing:** `MageAgentTypeFactory` does not currently include `HealCapabilityFactory`. If a Mage or Cleric subclass is expected to cast ally heals via GOAP, `HealCapabilityFactory` must be registered in its factory or a specialized `SupportAgentTypeFactory`.
  - **Recover vs Survive Priority:** When a unit has recovery potions in its kit, `UtilityRecoverGoal` overrides `UtilitySurviveGoal` (`surviveOverrideFloor = 1.02f` vs clamped `1.0f`). Frontline units under extreme burst damage may exhaust potions or die before transitioning to `Survive`.
  - **Macro Footprints:** Dungeon piece generation currently operates on 1x1 macro-cell footprints with dynamic interior dimensions ($W \times H \ge 4 \times 4$); multi-cell macro pieces spanning multiple layout graph nodes are deferred.
  - **Phase Enum Overlap:** `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` overlap in concept with both defining phase 0 as planning/build.
