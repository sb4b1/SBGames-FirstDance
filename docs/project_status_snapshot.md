# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-19 | Latest Commit: f05136c feat: implement core character stats, AI GOAP behaviors, and combat systems with associated prefabs and assets*

## 1. Recently Completed Milestones & Features
- **Character Stat & Regeneration Pipeline ([`RegenTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/RegenTuningSO.cs), [`CharacterStatsManager`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs)):**
  - Implemented continuous life and mana regeneration (`LifeRegen`, `ManaRegen`) in [`CharacterStatsManager.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs) via per-frame `TickRegen(Time.deltaTime)`.
  - Added out-of-combat regeneration acceleration governed by [`RegenTuningSO.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/RegenTuningSO.cs): tracks elapsed time since last damage dealt or taken (`timeSinceLastDamageTakenOrDealt`), boosting regeneration rates by a configurable multiplier (default 3.0× after 5.0s out of combat).
  - Authored default tuning asset [`RegenTuning_Default.asset`](file:///d:/UnityProject/SBGames/Assets/ScriptableObjects/RegenTuning_Default.asset) and accompanying test suite [`CharacterRegenTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/CharacterRegenTests.cs).
- **Basic Attack Modes & Projectile Integration ([`BasicAttackMode`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/BasicAttackMode.cs), [`CharacterBaseStats`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterBaseStats.cs)):**
  - Added [`BasicAttackMode`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/BasicAttackMode.cs) (`Auto = 0`, `None = 1`) to [`CharacterBaseStatSO.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterBaseStats.cs), enabling archetypes to either perform automated standard attacks or rely purely on active GOAP skills (e.g. specialized mages or pacifist supports).
  - Updated [`CharacterCombatController.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/CharacterCombatController.cs) and [`AttackAction.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/AttackAction.cs) to check `BasicAttackMode`, preventing unintended basic attack loops on non-basic-attacking archetypes.
  - Added arrow projectile sprites (`arrow_1.png` – `arrow_4.png`, `Just_arrow.png`) and verified via [`BasicAttackModeTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/BasicAttackModeTests.cs).
- **Character & Monster Asset Restructuring:**
  - Standardized adventurer animator controllers and motion clips under `Assets/Animators/Characters/Adventurers/Normal/N002` through `N005`.
  - Updated monster animator controllers (`NC001_Animator`, `NC002`, `MONSTER_N003`) and consolidated monster prefabs.
  - Synchronized character database definitions and CSV tables (`Characters.csv`, `DatabaseCsvPipeline.cs`, `MasterGameDatabase.asset`).
- **AI Healing Behavior & Healer Combat Loop ([Report](file:///d:/UnityProject/SBGames/docs/testing/heal_proof_and_edc_comparison_report.md)):**
  - Full GOAP and Utility AI support for ally restoration: [`HealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Goals/HealAllyGoal.cs), [`HealAllyAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/HealAllyAction.cs), [`UtilityHealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs), [`HealCapabilityFactory`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Capabilities/HealCapabilityFactory.cs), [`WoundedAllyTargetSensor`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Sensors/WoundedAllyTargetSensor.cs), [`WoundedAllyTarget`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/TargetKeys/Target/WoundedAllyTarget.cs), and [`AlliesNeedHealing`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/WorldKeys/AlliesNeedHealing.cs) world key.
  - Resolved 0 HP heal bug by enforcing target health-deficit checks, registering the GOAP world-key decrease effect, fixing caster root resolution under scene grouping containers (`SpawnedCombatUnits`), and aligning projectile trigger layer masks.
  - Authored heal assets: `SKILL_HEAL_001.prefab`, `SKILL_HEAL_001.asset`, `DefaultHealAllyGoal.asset`, `Utility_SKILL_HEAL_001.asset`.
- **Adaptive Sensor Pulse Intervals in VisionController ([Report](file:///d:/UnityProject/SBGames/docs/testing/adaptive_sensor_pulse_intervals_report.md)):**
  - Dynamic state-driven perception tiers configured via [`PerceptionTuningSO.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/PerceptionTuningSO.cs): Calm (0.40s / 2.5 Hz), Default (0.20s / 5.0 Hz), Engaged (0.12s / 8.33 Hz), Critical (0.05s / 20.0 Hz).
  - Asymmetric dwell hysteresis ($0.50\text{s}$ before demotion), deterministic desynchronization phase offsets, and rate-limited reactive force pulses (`RequestImmediatePulse()`). Verified via [`VisionControllerAdaptivePulseTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/VisionControllerAdaptivePulseTests.cs).
- **Dungeon Piece System & 2D Camera Controller ([ADR 0003](file:///d:/UnityProject/SBGames/docs/adr/0003-dungeon-piece-system-and-selection.md)):**
  - Authored piece generation (`DungeonPiece`, `PieceSetSO`, `DungeonPieceDescriptor`, `DungeonPieceSelector`) with dynamic dimensions ($W \times H \ge 4 \times 4$), structural roles (`Room` vs `Pathway`), freeform 2-tile openings, non-square quarter-turn rotation, and collision-free seam layout solver (`DungeonSeamLayoutSolver`).
  - Dynamic room capacity calculation derived from piece-local `BuildableMask` and placement crowding penalties (`IRoomCapacityTracker`, `DungeonRoomCapacityTracker`).
  - 2D camera controller ([`CameraController2D`](file:///d:/UnityProject/SBGames/Assets/Scripts/Camera/CameraController2D.cs)) with 1:1 right-click drag pan, cursor-centered zoom, smart drag discrimination, and dynamic arena bounds clamping.
- **Unity Editor & Test Suite Status:**
  - 28 test suites in `Assets/Tests/` with all tests passing (+2 new suites: [`BasicAttackModeTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/BasicAttackModeTests.cs), [`CharacterRegenTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/CharacterRegenTests.cs)).
  - Live Unity Editor verified via Unity CLI (`unity status`, `unity cmd console`): PID 3276 running on port 7800, 0 compilation errors, 0 active console errors.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
Current C# file counts across `Assets/Scripts/` (341 total scripts), `Assets/Editor/` (29 total scripts), and `Assets/Tests/` (28 total scripts):

| Folder | C# files | Role / Key Modules |
| --- | ---: | --- |
| `Assets/Scripts/Character` | 132 | Character identity, controllers, traps, AI profiles/memory, adaptive perception, and GOAP/Utility AI system. |
| `Assets/Scripts/Dungeon` | 26 | Room/door markers, registry, procedural layout graph/generator, dynamic piece system, seam layout solver, and runtime builder. |
| `Assets/Scripts/Placement` | 26 | Dungeon placement system, grid visualizer, bench, buildable mask validator, and room capacity tracking. |
| `Assets/Scripts/Loop` | 26 | Core game loop, combat results handler, DM abilities/talents, post-battle reports, and progression/fame. |
| `Assets/Scripts/Inventory` | 26 | Inventory, equipment, items, consumable effects (including Warp escape), and save data. |
| `Assets/Scripts/Database` | 13 | Catalog ScriptableObjects, CSV parsers, character/item databases, and editor sync pipeline. |
| `Assets/Scripts/Skills` | 13 | Skill casting, targeting, projectile pooling, ally/enemy hit resolution, and skill database. |
| `Assets/Scripts/Shop` | 12 | Shop manager, merchant inventories, pricing, and economy loop. |
| `Assets/Scripts/Stat` | 11 | Character base stats, stat managers, life/mana regeneration pipeline, and basic attack modes (+2 scripts). |
| `Assets/Scripts/Squads` | 11 | Party leader, squad blackboard, squad member coordination, tactical evaluators, and scout intel HUD. |
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
- [`RegenTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/RegenTuningSO.cs):
  - **Responsibilities:** Immutable configuration asset defining out-of-combat delay threshold and out-of-combat regeneration multipliers for life and mana.
  - **Key API:** `OutOfCombatDelay`, `OutOfCombatRegenMultiplier`, `CreateRuntimeInstance(delay, mult)`.
- [`BasicAttackMode`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/BasicAttackMode.cs):
  - **Responsibilities:** Archetype configuration enum determining whether units perform basic attacks (`Auto = 0`) or rely entirely on skill casting (`None = 1`).
- [`CharacterStatsManager`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs) *(Refactored)*:
  - **Responsibilities:** Tracks unit health and mana, executes continuous regeneration ticks via `TickRegen()`, manages `timeSinceLastDamageTakenOrDealt`, and scales regen when out of combat.
  - **Key API:** `LifeRegen`, `ManaRegen`, `TickRegen(float)`, `RestoreLife(float)`, `RestoreMana(float)`, `TryConsumeMana(float)`.
- [`HealAllyGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Goals/HealAllyGoal.cs) & [`HealAllyAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/HealAllyAction.cs):
  - **Responsibilities:** GOAP goal and action commanding healers to target and cast restoration spells on injured allies, checking target validity and range.
  - **Key API:** `Perform(IMonoAgent, ActionData)`, `HealHpThreshold = 0.50f`.
- [`PerceptionTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/PerceptionTuningSO.cs):
  - **Responsibilities:** Configuration asset for adaptive perception intervals (Calm, Default, Engaged, Critical), dwell timers, and force-pulse rate limits.
  - **Key API:** `CalmInterval`, `DefaultInterval`, `EngagedInterval`, `CriticalInterval`, `MinTierDwellDuration`.
- [`VisionController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/VisionController.cs) *(Refactored)*:
  - **Responsibilities:** Dynamic perception polling using state-driven tiers with dwell hysteresis, phase desynchronization, and reactive force pulse support.
  - **Key API:** `CurrentTier`, `EffectivePulseInterval`, `RequestImmediatePulse()`, `SetTuning(PerceptionTuningSO)`.

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
  - `CharacterStatsManager`: Dispatches `OnLifeChanged(current, max)` and `OnCharacterDeath(manager)`.
  - `SkillHitResolver`: Dispatches `OnSkillHealApplied(caster, target, amount)` for test monitoring and UI floaters.
  - `AdventurerEventHub`: Publishes leader death/succession, checkpoint updates, room entry/clearing, potion/warp item usage, retreat blockades, DM seal triggers, and escape completion.
  - `CombatResultHandler`: Dispatches `OnCombatEnded`, `OnSquadWiped`, `OnAdventurersEscaped` to evaluate battle metrics and trigger `PostBattleReport`.
  - `CoreGameLoop`: Emits `OnPhaseChanged`, `OnRoundStarted`, `OnGameOver`.
  - `DMSealAbilityController` & `DungeonMaster`: Listens to seal triggers, energy accumulation, and talent purchases.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:**
  - Ensure healer prefabs (e.g. `ADVENTURER_N001`) have `DefaultHealAllyGoal` assigned in `BaseAI.goals` and `SKILL_HEAL_001` configured in `CharacterSkillController.skills`.
  - Assign `RegenTuning_Default.asset` to `CharacterStatsManager` instances or prefabs where out-of-combat regeneration tuning is required.
  - Assign `PerceptionTuning_Default.asset` to `VisionController` instances to activate adaptive perception tiers.
  - Confirm that `CameraController2D` is attached to the Main Camera with `clampToArenaBounds = true` across gameplay scenes.
  - In `Combat_Sandbox.unity`, ensure `BattleSimulationTestRunner` harness is populated with valid adventurer and monster prefabs.
- **Known Gotchas / Architectural Constraints:**
  - **Inactive GameObject Coroutines on Death/Cleanup:** When an agent is deactivated during death or despawn while an action stops (`OnDisable` $\to$ `StopAction` $\to$ `CharacterBrain.EndCastSkill` / `EndAttack`), calling `StartCoroutine` on an inactive GameObject logs a Unity warning/error. Handlers should guard with `gameObject.activeInHierarchy` before starting recovery coroutines.
  - **MageAgent Healing:** `MageAgentTypeFactory` does not register `HealCapabilityFactory`. If a Mage or Cleric subclass is expected to cast ally heals via GOAP, `HealCapabilityFactory` must be added to its factory.
  - **Recover vs Survive Priority:** When recovery items remain in a unit's item kit, `UtilityRecoverGoal` overrides `UtilitySurviveGoal` (`surviveOverrideFloor = 1.02f` vs clamped `1.0f`). Frontline units under extreme burst damage may exhaust items or fall before transitioning to `Survive`.
  - **Macro Footprints:** Dungeon piece generation currently supports single-piece footprints with dynamic interior dimensions ($W \times H \ge 4 \times 4$); multi-cell macro pieces spanning multiple layout graph nodes remain deferred.
  - **Phase Enum Overlap:** `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` overlap conceptually, with both defining phase 0 as planning/build.
