# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-16 | Latest Commit: 311ca88 feat: implement core dungeon piece system and 2D camera controller*

## 1. Recently Completed Milestones & Features
- **Dungeon Piece System & Dynamic Seam Layout Architecture ([ADR 0003](file:///d:/UnityProject/SBGames/docs/adr/0003-dungeon-piece-system-and-selection.md)):**
  - Modular piece-based generation system (`DungeonPiece`, `PieceSetSO`, `DungeonPieceDescriptor`, `DungeonPieceSelector`) replacing pure procedural boxes with authored, themed map chunks.
  - Dynamic piece dimensions ($W \times H \ge 4 \times 4$) and distinct piece structural roles (`PieceRole.Room` vs `PieceRole.Pathway`).
  - Freeform 2-tile doorway openings (`PieceOpening`) declaring anchor tile and outward cardinal normal direction, supporting non-square quarter-turn rotation cycles.
  - Collision-free seam snapping layout solver (`DungeonSeamLayoutSolver`) computing exact world origins for sockets (`OriginB = OriginA + OpeningA.AnchorTile + Normal - OpeningB.AnchorTile`), with direct Room-to-Room and Pathway insertion (`Room -> Pathway -> Room`), validating against tile footprint overlaps (`OccupiedDungeonTiles`).
  - Automatic doorway sealing for unused openings using `Opening Cap` prefabs.
  - Dynamic room capacity calculation derived from the piece's piece-local `BuildableMask`: Soft Capacity = $\lfloor \text{maskCount} / \text{cellsPerSoftSlot} \times \text{mult}_{\text{soft}} \rfloor$, Max Capacity = $\lceil \text{Soft} \times \text{mult}_{\text{max}} \rceil$.
  - Pure C# batch simulation suite (`DungeonBatchSimulation`) verifying ground tile seam continuity across generated seeds and 2D NavMesh path reachability from Entrance to Boss.
- **2D Orthographic Camera Controller ([`CameraController2D`](file:///d:/UnityProject/SBGames/Assets/Scripts/Camera/CameraController2D.cs)):**
  - Right-click drag-to-pan with 1:1 world unit grab-and-drag motion (`worldUnitsPerPixel = (orthographicSize * 2) / Screen.height`).
  - Mouse wheel zoom smoothly centered on the mouse cursor world position with configurable min/max zoom limits (10 to 60).
  - Smart RMB discrimination (`dragThresholdPixels = 5f`, `HasDraggedThisPress`, `IsDragging`) distinguishing pan drags from stationary clicks to prevent accidental placement cancellations/sales.
  - UI input blocking (`EventSystem.IsPointerOverGameObject()`).
  - Dynamic arena bounds clamping (`GetCurrentBounds()`), extracting composite bounds from `DungeonRoomRegistry`, active `DungeonPiece` components, `DungeonPlacementManager`, or fallback bounds, with configurable edge padding.
- **Room Capacity & Crowding Penalty System:**
  - Decoupled `IRoomCapacityTracker` interface with `DictionaryRoomCapacityTracker` for unit tests/simulations and `DungeonRoomCapacityTracker` component for Unity runtime.
  - Room-based crowding penalties (`IRoomCrowdingPenaltyStrategy`) applying diminishing returns or gold surcharges when placing units in rooms exceeding soft capacity, and hard rejection when exceeding max capacity.
  - Integrated into `DungeonPlacementSystem` and `DungeonPlacementManager`.
- **Character & AI System Reorganization:**
  - Normalized character paths: moved from flat `Assets/Scripts/Characters/` and `Assets/Scripts/GOAP/` into structured `Assets/Scripts/Character/` and `Assets/Scripts/Character/AI/GOAP/`.
  - Comprehensive character controllers: `CharacterBrain`, `CharacterMovementController`, `CharacterCombatController`, `CharacterSkillController`, `AnimationController`, `AnimationObjectController`, `VisionController`, `CharacterFlipRotator`.
  - Adventurer AI profiles: `AdventurerAIProfile`, `AdventurerItemKitController`, `AdventurerEventHub`, `RoomLoopTuningSO`, `ItemUseQualityTuningSO`, `LeaderCheckpointTuningSO`.
- **Newly Created ScriptableObjects, Prefabs & Editor Tools:**
  - **Assets:** `StarterPieceSet.asset`, `TestPieceSet.asset`, `DefaultDungeonGeneratorConfig.asset`, 20 starter piece prefabs (`boss_*`, `entrance_*`, `normal_*`, `saferoom_*` across corner, cross, deadend, straight, and T layouts) in `Assets/DungeonPiece/StarterSet/Pieces/`, plus `opening_cap.prefab`.
  - **Editor Windows & Tools:** `DungeonPieceAuthoringWindow` (coverage matrix, footprint presets, interactive opening toggles, buildable mask painting, validation badges), `DungeonPieceEditor` (scene GUI bounds recalibration and visual opening gizmos), `DungeonRuntimeBuilderEditor` (direct inspector generation, clear, and simulation runner), `DungeonSheetImporter`, `StarterPieceSetBuilder`, `ScenePaintingPieceConverter`, `TilePixelSizeValidator`.
- **Automated Test Coverage:**
  - 25 passing test suites in `Assets/Tests/` covering GOAP, combat, progression, room capacity (`RoomCapacityTests.cs`), dungeon pieces (`DungeonPieceTests.cs`), and 2D camera (`CameraController2DTests.cs`).
  - Clean Unity Editor status verified via Unity CLI (`unity cmd console`): 0 compilation errors, 0 console errors.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
Current C# file counts across `Assets/Scripts/` (331 total scripts), `Assets/Editor/` (29 total scripts), and `Assets/Tests/` (25 total scripts):

| Folder | C# files | Role / Key Modules |
| --- | ---: | --- |
| `Assets/Scripts/Character` | 124 | Character identity, controllers, traps, AI profiles/memory, and GOAP/Utility AI system. |
| `Assets/Scripts/Dungeon` | 26 | Room/door markers, registry, procedural layout graph/generator, dynamic piece system, seam layout solver, and runtime builder. |
| `Assets/Scripts/Placement` | 26 | Dungeon placement system, grid visualizer, bench, buildable mask validator, and room capacity tracking. |
| `Assets/Scripts/Loop` | 26 | Core game loop, combat results handler, DM abilities/talents, post-battle reports, and progression/fame. |
| `Assets/Scripts/Inventory` | 26 | Inventory, equipment, items, consumable effects (including Warp escape), and save data. |
| `Assets/Scripts/Database` | 13 | Catalog ScriptableObjects, CSV parsers, character/item databases, and editor sync pipeline. |
| `Assets/Scripts/Skills` | 13 | Skill casting, targeting, projectile pooling, and skill database. |
| `Assets/Scripts/Shop` | 12 | Shop manager, merchant inventories, pricing, and economy loop. |
| `Assets/Scripts/Squads` | 11 | Party leader, squad blackboard, squad member coordination, tactical evaluators, and scout intel HUD. |
| `Assets/Scripts/Stat` | 9 | Character base stats, stat managers, and stat modifier pipelines. |
| `Assets/Scripts/Personality` | 7 | Character personality traits, personality profiles, and behavioral biases. |
| `Assets/Scripts/Events` | 6 | Dungeon event definitions, event manager, contexts, and altar/dark boon events. |
| `Assets/Scripts/Input` | 4 | Input management and keybinding configurations. |
| `Assets/Scripts/Testing` | 4 | Combat test environment bootstrap, combat test HUD, and simulation runners. |
| `Assets/Scripts/Interface` | 4 | Core entity interfaces (`IDamageable`, `IPoolable`, `ITargetable`, `IInteractable`). |
| `Assets/Scripts/StatusEffect` | 3 | Status effect manager, controllers, and status effect instances. |
| `Assets/Scripts/Manager` | 3 | High-level game and audio managers. |
| `Assets/Scripts/Factions` | 3 | Faction identifiers, alignments, and relations. |
| `Assets/Scripts/Gizmo` | 2 | Editor visualization gizmo helpers. |
| `Assets/Scripts/SB4b1` | 2 | Shared mathematical helpers and utility extensions. |
| `Assets/Scripts/Camera` | 1 | `CameraController2D` (pan, zoom, mouse-drag filtering, arena clamping). |
| `Assets/Scripts/Hierarchy` | 1 | Hierarchy header visual organization tool. |
| `Assets/Scripts/Environment` | 1 | Environmental obstacles and interactive props. |
| `Assets/Scripts/Editor` | 1 | Runtime editor helper hooks. |
| `Assets/Scripts/Base` | 1 | Base identifier types. |
| `Assets/Scripts/Examples` | 0 | Example templates directory. |

### Key Modules Added or Refactored
- [`CameraController2D`](file:///d:/UnityProject/SBGames/Assets/Scripts/Camera/CameraController2D.cs):
  - **Responsibilities:** Orthographic 2D camera control supporting right-click dragging, mouse-wheel zooming centered on the mouse pointer, UI interaction blocking, and dynamic dungeon arena boundary clamping.
  - **Key API:** `IsDragging`, `HasDraggedThisPress`, `BoundsPadding`, `GetCurrentBounds()`.
- [`DungeonPiece`](file:///d:/UnityProject/SBGames/Assets/Scripts/Dungeon/Unity/Pieces/DungeonPiece.cs) & [`PieceSetSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Dungeon/Unity/Pieces/PieceSetSO.cs):
  - **Responsibilities:** Authored chunk component holding dimensions ($W \times H$), piece role (`Room` vs `Pathway`), authored freeform 2-tile openings (`PieceOpening`), piece-local buildable mask, and tile references; `PieceSetSO` organizes themed collections, tile pixel sizes, and opening cap prefabs.
  - **Key API:** `CellSize`, `Role`, `Openings`, `BuildableMask`, `TilePixelSize`, `OpeningCapPrefab`.
- [`DungeonSeamLayoutSolver`](file:///d:/UnityProject/SBGames/Assets/Scripts/Dungeon/Core/Pieces/DungeonSeamLayoutSolver.cs):
  - **Responsibilities:** Pure C# layout solver that snaps pieces together at matching socket openings without tile overlaps, inserting pathway pieces when necessary.
  - **Key API:** `SolveLayout(DungeonLayoutGraph, PieceSetSO, IDungeonRng)`.
- [`DungeonBatchSimulation`](file:///d:/UnityProject/SBGames/Assets/Scripts/Dungeon/Core/Pieces/DungeonBatchSimulation.cs):
  - **Responsibilities:** Headless seed simulation running seam integrity checks and 2D NavMesh path connectivity from Entrance to Boss room across arbitrary seed counts.
  - **Key API:** `RunSimulation(int seedCount, PieceSetSO, DungeonGeneratorConfig)`.
- [`IRoomCapacityTracker`](file:///d:/UnityProject/SBGames/Assets/Scripts/Placement/Core/IRoomCapacityTracker.cs) & [`DungeonRoomCapacityTracker`](file:///d:/UnityProject/SBGames/Assets/Scripts/Placement/Unity/DungeonRoomCapacityTracker.cs):
  - **Responsibilities:** Decouples placement logic from scene objects; tracks soft/max room capacities, unit occupancy, and crowding status.
  - **Key API:** `HasRoomAt(gridPos)`, `GetRoomId(gridPos)`, `GetRoomCapacityUsed(id)`, `GetRoomSoftCapacity(id)`, `GetRoomMaxCapacity(id)`, `CanAccommodate(id, cost)`.
- [`AdventurerEventHub`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/AdventurerEventHub.cs):
  - **Responsibilities:** Centralized event bus for party and adventurer lifecycle events, room transitions, potion/warp consumption, seal interruptions, and boss encounters.
  - **Key API:** `PublishLeaderDied()`, `PublishCheckpointReached()`, `PublishRoomEntered()`, `PublishRoomCleared()`, `PublishPotionUsed()`, `PublishWarpUsed()`, `PublishBossFightStarted()`.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI Catalog:**
  - **13 Goals (`GoalBase`):** `AlertAlliesGoal`, `DebriefLeaderGoal`, `FallBackGoal`, `FocusFireGoal`, `FollowLeaderGoal`, `IdleGoal`, `KeepDistanceGoal`, `KillEnemyGoal`, `RecoverGoal`, `ScoutReconGoal`, `StandbyGoal`, `SurviveGoal`, `WarpGoal`.
  - **13 Actions (`GoapActionBase`):** `AlertAlliesAction`, `AttackAction`, `CastSkillAction`, `DebriefLeaderAction`, `FallBackAction`, `FleeAction`, `FocusFireAction`, `FollowLeaderAction`, `IdleAction`, `RecoverAction`, `ScoutReconAction`, `WanderAction`, `WarpAction`.
  - **12 Utility Goals (`UtilityGoal`):** `UtilityAlertAlliesGoal`, `UtilityDebriefGoal`, `UtilityFallBackGoal`, `UtilityFocusFireGoal`, `UtilityFollowLeaderGoal`, `UtilityKillEnemyGoal`, `UtilityRecoverGoal`, `UtilityScoutReconGoal`, `UtilityStandbyGoal`, `UtilitySurviveGoal`, `UtilityWanderGoal`, `UtilityWarpGoal`.
  - **Utility Skills:** `UtilityBaseSkill`, `UtilityFireSpell`, `UtilityNoAvailableSkill`.
  - **14 World Keys (`WorldKeyBase`):** `AgentLife`, `AgentMana`, `CanWarp`, `EnemyDistance`, `FearLevel`, `HasAlertedAllies`, `HasDebriefedLeader`, `HasScoutedArea`, `HasSquadOrder`, `IsIdle`, `IsSkillReady`, `NeedsRecovery`, `SquadOrderTypeKey`, `ThreatLevel`.
  - **9 Target Keys (`TargetKeyBase`):** `EnemyTarget`, `FocusFireTarget`, `KeepDistanceTarget`, `LeaderTarget`, `RecoverTarget`, `ScoutReconTarget`, `SelfTarget`, `WanderTarget`, `WarpTarget`.
  - **9 Capability Factories:** `AttackCapabilityFactory`, `BasicCapabilityFactory`, `MinionBasicCapabilityFactory`, `RecoveryCapabilityFactory`, `SkillCapabilityFactory`, `SquadCapabilityFactory`, `SurvivalCapabilityFactory`, `WanderCapabilityFactory`, `WarpCapabilityFactory`.
  - **Agent Type Capabilities Mapping:**
    - `NormalAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Warp.
    - `MageAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Warp.
    - `MinionAgent`: Wander, Attack, MinionBasic, Squad, Survival *(Recovery and Warp intentionally omitted)*.
- **Core Systems & Services Wiring:**
  - `BaseAI`: Bridges Utility AI goal evaluations to CrashKonijn GOAP planning. Caches perception (`VisionController`), character stats (`CharacterStatsManager`), squad membership (`SquadMember`), personality traits (`CharacterPersonality`), memory (`CharacterAIMemory`), item kit usage (`AdventurerItemKitController`), and doorway staging (`DoorwayStagingController`). Implements goal hysteresis (`goalHysteresisBonus = 0.08f`) to eliminate flip-flopping.
  - `PartyLeader` & `SquadBlackboard`: Maintains squad operational orders (`Engage`, `FallBack`, `ScoutRecon`, `Standby`), room clearance progress, hazard memory, focus-fire targets, and leader succession when the primary leader dies.
  - `DungeonRoomRegistry`: Tracks all active `RoomVolume` instances and placed `DungeonPiece` components in scene for camera bounds clamping, DM placement validation, room lookups, and AI room event triggers.
  - `DungeonPlacementManager` & `DungeonPlacementSystem`: Manages grid coordinate validation, occupancy tracking, resource wallet spending, buildable mask checking, and room capacity enforcement (`IRoomCapacityTracker`).
  - `GameServices`: Central service registry resolving singleton dependencies (`DungeonPlacementManager`, `CharacterManager`, `ObjectPoolManager`, etc.) by exact type name.
  - `CoreGameLoop`: Oversees high-level game states, phase transitions (`PlanningPhase`, `ActionPhase`), round timers, and victory/defeat evaluation.
- **Cross-System Event Hooks:**
  - `AdventurerEventHub`: Publishes leader death/succession, checkpoint updates, room entry/clearing, potion/warp item usage, retreat blockades, DM seal triggers, and escape completion.
  - `CombatResultHandler`: Dispatches `OnCombatEnded`, `OnSquadWiped`, `OnAdventurersEscaped` to evaluate battle metrics and trigger `PostBattleReport`.
  - `CoreGameLoop`: Emits `OnPhaseChanged`, `OnRoundStarted`, `OnGameOver`.
  - `DMSealAbilityController` & `DungeonMaster`: Listens to seal triggers, energy accumulation, and talent purchases.
  - `DungeonProgression` & `DungeonFameSystem`: Emits fame tier shifts and danger multiplier updates.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:**
  - Verify that `CameraController2D` is attached to the Main Camera in `Combat_Sandbox.unity` and gameplay scenes, with `clampToArenaBounds` enabled.
  - Ensure `DungeonRuntimeBuilder` in the active scene references `StarterPieceSet.asset` and the desired `DungeonGeneratorConfigSO`.
  - Confirm that unit prefabs assigned to `[TEST_HARNESS]` in `Combat_Sandbox.unity` have valid `AdventurerAIProfile` and `CharacterBaseStats` ScriptableObjects assigned.
  - Check that agent prefabs with `SkillCaster` components have corresponding `utilityData` ScriptableObjects assigned or configured in `UtilitySkillRuntimeDatabase`.
- **Known Gotchas / Architectural Constraints:**
  - **Macro Footprints:** The current dungeon piece layout solver handles single-piece footprints with dynamic dimensions ($W \times H$); multi-cell macro pieces (spanning multiple grid nodes) remain deferred to future iterations.
  - **Phase Enum Overlap:** `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` define overlapping concepts where `PlanningPhase` and `BuildPhase` both map to integer value 0.
  - **Service Registration Convention:** `GameServices.Register<T>()` relies on exact type-name reflection; service property names on `GameServices` must match the registered type name precisely.
  - **Zero-Corridor Abutting Requirement:** When using seamless adjacent piece layouts (`corridorLength = 0`), ensure opening parity matches (`cellSize - openingWidth` is even) so doorways align symmetrically on cell boundaries.
