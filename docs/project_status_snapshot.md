# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-15 | Latest Commit: 94882b1 feat: add character system including controllers, AI behaviors, and combat mechanics*

## 1. Recently Completed Milestones & Features
- The latest committed work reorganizes the character source tree and adds character controllers, combat mechanics, and adventurer AI. Earlier commits establish squad tactics, recovery and Warp GOAP behaviors, room registration, and the combat sandbox.
- The current uncommitted work adds a procedural dungeon layout model and runtime builder under `Assets/Scripts/Dungeon/{Core,Unity}/`, and expands placement with core interfaces and validation. Other in-progress edits touch CSV import, character combat and item kits, squad behavior, character assets, tile assets, and sandbox/creative scenes.
- Utility goal assets now include default Recover and Warp assets, alongside Wander, Survive, combat, and squad goals. New uncommitted character assets include adventurer N001 and N099; dungeon configuration and tests are also present.
- The working tree is heavily modified, with asset additions, edits, and deletions as well as source changes. Treat these as in-progress workspace state; Git history does not yet record them as completed milestones.
- Unity CLI is connected to the project (Unity 6000.6.4f1). Its live state reports compilation complete and no current console errors. The returned console history contains repeated warnings that UtilitySkill data is missing on some SkillCasters. No tests or builds were run for this snapshot.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
Current C# file counts by top-level folder under `Assets/Scripts/`:

| Folder | C# files | Role / key modules |
| --- | ---: | --- |
| `Base` | 1 | Shared identifiers. |
| `Character` | 124 | Character identity, controllers, traps, AI profiles/memory, and GOAP/Utility AI. |
| `Database` | 13 | Catalog ScriptableObjects, CSV parser, and editor pipeline. |
| `Dungeon` | 14 | Room/door markers and registry, procedural layout graph/generator, configuration, and runtime builder. |
| `Editor` | 1 | Editor utilities. |
| `Environment` | 1 | Environment behavior. |
| `Events` | 6 | Dungeon event definitions, manager, and contexts. |
| `Factions` | 3 | Faction identity and membership. |
| `Inventory` | 26 | Inventory, equipment, effects, and save data. |
| `Loop` | 26 | Core game loop, combat results, progression, and Dungeon Master systems. |
| `Placement` | 23 | Dungeon placement, grid, bench, and placement contracts. |
| `Shop` | 12 | Shop and economy. |
| `Skills` | 13 | Skill casting, targeting, and projectiles. |
| `Squads` | 11 | Party leader, squad blackboard, members, tactics, and recon HUD. |
| Other folders | 39 | `Gizmo` (2), `Hierarchy` (1), `Input` (4), `Interface` (4), `Manager` (3), `Personality` (7), `SB4b1` (2), `Stat` (9), `StatusEffect` (3), `Testing` (4); `Examples` currently has no C# files. |

Key modules and responsibilities:
- `Character/AI/GOAP/BaseAI` bridges utility goal selection to CrashKonijn GOAP planning and exposes cached character, perception, resource, and squad state. `VisionController` feeds cached sensor data; capability factories register actions, sensors, and goals for agent types.
- `PartyLeader` and `SquadBlackboard` coordinate squad orders, enemy/hazard reports, recon, succession, room progress, and checkpoints.
- `Dungeon/Core` defines the layout graph, nodes, room types, door connections, generator configuration, and RNG contract. `Dungeon/Unity/DungeonRuntimeBuilder` adapts generated layouts to Unity objects.
- `Placement/Core` contains placement requests/results, unit definitions, occupancy/resource/capacity interfaces, and the new buildable-cell validation contract; Unity-facing placement is handled by `DungeonPlacementManager`.
- `Database/CSV` handles CSV parsing and editor synchronization with ScriptableObject catalogs. Other major gameplay modules remain split across `Inventory`, `Shop`, `Loop`, `Skills`, and `Stat`.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI:** `BaseAI` requests Idle, KillEnemy, FollowLeader, FocusFire, FallBack, Survive, KeepDistance, Standby, AlertAllies, ScoutRecon, DebriefLeader, Recover, and Warp goals. Utility scorers also include Wander and skill selection.
- **Capabilities and agent registration:** Normal and Mage register Wander, Attack, Skill, Basic, Squad, Survival, Recovery, and Warp capabilities. Minion registers Wander, Attack, MinionBasic, Squad, and Survival. The current code therefore does not register Warp or Recovery for Minion.
- **World/target keys:** World keys represent life, mana, Warp eligibility, enemy distance, fear, alert/debrief/scout state, squad orders, idle/skill readiness, recovery, and threat. Target keys cover enemy, focus fire, keep distance, leader, recovery, recon, self, wander, and Warp.
- **Core systems:** `CoreGameLoop` exposes phase, round, completion, and game-over events. `GameServices` resolves service properties by exact type name. `PartyLeader` and `SquadBlackboard` coordinate squad and room state; `CombatResultHandler` aggregates combat outcomes.
- **Event hooks:** `AdventurerEventHub` publishes leader death/change, checkpoint, room entry/clear, potion, boss fight, retreat blocked, seal, escape, and Warp lifecycle events. `CombatResultHandler`, `CoreGameLoop`, `DungeonEventManager`, `DungeonMaster`, `DungeonProgression`, and `SquadBlackboard` expose local events for results, phases, dungeon events, resources, progression, and reports.
- **Binding rules:** Follow `docs/goap_utility_context.md`: perception belongs in cached sensors/wrappers, actions use cached state and controllers, and hot paths avoid allocations. Damage must go through `IDamageable.TakeDamage(in DamageData)`.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:** Unity Editor is connected, but this scan did not inspect each scene or prefab’s serialized assignments. Confirm the new dungeon generator config and builder setup in the intended scene, placement validator wiring, and utility goal lists on agent prefabs. The Default Warp and Recover goal assets now exist; their presence alone does not confirm prefab assignment. The combat sandbox guide documents assigning unit prefabs to `[TEST_HARNESS]`.
- **Known issues / constraints:** The connected Editor’s returned log history includes repeated “No UtilitySkill data found” warnings for SkillCasters; check their `utilityData` assignments or matching `UtilitySkillRuntimeDatabase` entries. `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` overlap, and `PlanningPhase`/`BuildPhase` both use value 0. `GameServices.Register<T>()` expects a writable property whose name exactly matches the type name.
- The working tree includes extensive asset changes and deletions as well as source changes. Review those changes before committing; this snapshot does not attribute them to the latest commit.
- No GitHub Issues query or independent full TODO/FIXME scan was performed. Use `docs/agents/issue-tracker.md` for issue tracking conventions.
