# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-14 02:27 ICT | Latest Commit: ec5738d feat: add dungeon room registry, warp GOAP mechanics, and AI profiles*

## 1. Recently Completed Milestones & Features
- **Latest commit (`ec5738d`)** adds dungeon room and door markers with a static registry; Warp consumable escape behavior integrated with GOAP; per-rank room-loop tuning; adventurer progression/event reporting; DM Seal control; and post-battle report handling.
- New ScriptableObject assets in that commit include `RoomLoopTuning_Rank_C/D/E`, `ITEM_CONSUMABLE_WARP`, `WarpEscapeEffect_Default`, and the `DM_CONTROL_SEAL` talent. Rank tuning/profile data and the item database were updated. `AdventurerAIProfileTests.cs` was expanded with 691 lines of tests (not run for this snapshot).
- Earlier recent milestones: `c89a19e` added recovery GOAP and character AI profiles plus squad tactical utilities; `3dc96a2` introduced the GOAP AI system, squad formations, combat sandbox, and character profiles; `ed17ac8` added scout reconnaissance and intel HUD; `74e8dbe` added AI decision throttling, trap control, and character memory.
- `96f9a37` added the project overview and engineering guide. The ten most recent commits span from `ec5738d` through `9e508b7`; see `git log -n 10 --oneline --stat` for full file-level history.
- At the initial Git status capture, the new `.agents/skills/project-snapshot/` and `.claude/` skill directories were untracked. This snapshot file was created after that capture, so its own status is not included there.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
The current `Assets/Scripts/` inventory has the following C# file counts by top-level folder:

| Folder | C# files | Role / key modules |
| --- | ---: | --- |
| `Base` | 1 | Core object identifiers. |
| `Characters` | 29 | Actor state, combat controllers, AI profiles and memory, item kits, vision and doorway staging. Recent additions include `AdventurerEventHub`, `RoomLoopState`, and `RoomLoopTuningSO`. |
| `Database` | 13 | ScriptableObject catalogs, CSV parsing and synchronization. |
| `Dungeon` | 5 | New `Markers/` module: `RoomVolume` defines room bounds and exits; `DoorMarker` tracks connectivity and open/closed/explored state; `DungeonRoomRegistry` registers and queries rooms and doors; `BossDoorMarker` and `SafeRoomMarker` identify special locations. |
| `Editor` | 1 | Editor tooling and validators. |
| `Environment` | 1 | Environment objects. |
| `Events` | 6 | Dungeon event definitions and trigger contexts. |
| `Examples` | 0 | No C# files currently. |
| `Factions` | 3 | Faction membership and identity. |
| `Gizmo` | 2 | Debug visualization. |
| `GOAP` | 95 | GOAP actions, goals, sensors, keys, utility scorers, agent types, and capabilities. New Warp action/sensor/goal/key/capability files support escape behavior. |
| `Hierarchy` | 1 | Editor hierarchy helpers. |
| `Input` | 4 | Input and UI toggles. |
| `Interface` | 4 | Cross-system interfaces. |
| `Inventory` | 26 | Inventory and equipment; `WarpEscapeEffect` consumes/executes the new escape item effect. |
| `Loop` | 26 | Game phase/progression, battle resolution and reporting, Dungeon Master. Recent additions include `DMSealAbilityController` and `PostBattleReport`; `CombatResultHandler` was expanded. |
| `Manager` | 3 | Character/object pool and development managers. |
| `Personality` | 7 | Traits, instincts, personality data. |
| `Placement` | 22 | Grid placement and bench systems. |
| `SB4b1` | 2 | Shared low-level utilities. |
| `Shop` | 12 | Shop/economy. |
| `Skills` | 13 | Skill definitions, casting and projectiles. |
| `Squads` | 11 | `PartyLeader`, `SquadBlackboard`, member/tactical coordination. Recent changes add room-loop state, checkpoint/leader events, succession and shared room progress. |
| `Stat` | 9 | Character stats, modifiers and damage data. |
| `StatusEffect` | 3 | Status effect data and runtime manager. |
| `Testing` | 4 | Combat sandbox bootstrap, runner and HUD. |

Key changed or extended types include:
- `Characters/AI/AdventurerEventHub`: typed static events for leader/checkpoint/room progression, potion use, boss fights, DM Seals, escapes and Warp lifecycle.
- `Characters/AI/RoomLoopTuningSO` and `RoomLoopState`: data-driven tuning and state for room progression.
- `Dungeon/Markers/*`: room/door metadata and registry lookups (`RegisterRoom`, `RegisterDoor`, `GetRoomAt`, `GetClosestDoor`, `Clear`).
- `GOAP/Actions/WarpAction`, `WarpSensor`, `UtilityWarpGoal`, `WarpGoal`, `WarpTarget`, `CanWarp`, `WarpCapabilityFactory`: Warp item eligibility, selection, execution and planning integration.
- `Loop/DMSealAbilityController`: reacts to boss-fight/leader and combat state to control a sealed door; `PostBattleReport` aggregates adventurer events; `CombatResultHandler` tracks combat/escape/Warp states and report presentation.
- `Squads/PartyLeader` exposes scout, leader succession, checkpoint, room-entered/cleared and room-loop events; `SquadBlackboard` exposes enemy and hazard reports.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI:** Current marker goals are `Idle`, `KillEnemy`, `Survive`, `KeepDistance`, `FollowLeader`, `FocusFire`, `FallBack`, `Standby`, `AlertAllies`, `ScoutRecon`, `DebriefLeader`, `Recover`, and `Warp`. Utility scorer classes exist for Wander, KillEnemy, Survive, FollowLeader, FocusFire, FallBack, Standby, AlertAllies, ScoutRecon, Debrief, Recover and Warp; `BaseAI` maps these `EnumUtilGoal` keys to GOAP goals.
- Capability registrations: Normal and Mage agent types register Wander, Attack, Skill, Basic, Squad, Survival, Recovery and Warp capabilities. Minion registers Wander, Attack, MinionBasic, Squad and Survival. `WarpCapabilityFactory` adds `WarpGoal`, `WarpAction`, `WarpTarget`, `CanWarp` and `WarpSensor`.
- World keys currently include `AgentLife`, `AgentMana`, `CanWarp`, `EnemyDistance`, `FearLevel`, `HasAlertedAllies`, `HasDebriefedLeader`, `HasScoutedArea`, `HasSquadOrder`, `IsIdle`, `IsSkillReady`, `NeedsRecovery`, `SquadOrderTypeKey` and `ThreatLevel`. Target keys include enemy, focus-fire, keep-distance, leader, recover, scout-recon, self, wander and Warp targets.
- **Core Systems & Services:** `BaseAI` now exposes Warp gating/scoring inputs alongside existing perception, life/mana, fear, squad and profile data. `PartyLeader` now coordinates room traversal, checkpoint decisions and Warp/escape outcomes in addition to squad tactics and succession. `SquadBlackboard` remains the shared enemy/hazard memory. `CombatResultHandler` gained event-based tracking for room, checkpoint, leader, escape and Warp results. The latest commit does not change `CoreGameLoop` or `GameServices`.
- **Event Hooks:** `AdventurerEventHub` publishes leader died/changed, checkpoint decision, room entered/cleared, potion consumed, boss fight started, retreat blocked, seal activated/released, unit escaped, Warp started/completed/interrupted and Warp item forfeited. `PartyLeader` has local scout/leader/checkpoint/room-loop events; `SquadBlackboard` publishes enemy/hazard reports. `CombatResultHandler` exposes results-screen, combat/adventurer state, boss-health and reset events; `CoreGameLoop` exposes phase, round-started/completed and game-over events.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:** `BaseAI.goals` is a serialized list of `UtilityGoal` assets. `UtilityWarpGoal` exists as code, and Warp capability is registered for Normal/Mage, but no `UtilityWarpGoal` asset or prefab/scene reference to it was found under `Assets`; create/configure the utility goal asset and assign it to the intended Normal/Mage `BaseAI` prefabs. The three room-loop tuning assets exist, but scene/prefab assignments were not confirmed from repository references. The new room/door marker components likewise have no scene/prefab references in the scanned asset text; place and configure them in dungeon scenes that use room progression and boss/safe-room behavior.
- The combat sandbox guide documents manual harness setup: assign unit prefabs on `[TEST_HARNESS]` before running a simulation. This is a standing setup requirement, not confirmed as currently pending.
- **Known Gotchas / Constraints:** GOAP sensors should own perception queries and actions should consume cached state and call controllers; preserve the documented zero-allocation hot-path rule. Utility Goal assets are assigned through `BaseAI.goals`. `GameServices.Register<T>()` resolves properties by exact type name via reflection and can fail with a warning if names do not match. `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` overlap, with `PlanningPhase` and `BuildPhase` both represented by value `0`. The engineering guide also warns that several existing identifiers are misspelled; update all references before any rename.
- No inline TODO/FIXME tags were found in the source/docs scan. Track deferred work and active requests through GitHub Issues per `docs/agents/issue-tracker.md`; the issue list was not queried for this snapshot.
- No Unity Editor inspection or tests were run while generating this snapshot; scene/prefab wiring above is based on tracked text assets and documentation only.

