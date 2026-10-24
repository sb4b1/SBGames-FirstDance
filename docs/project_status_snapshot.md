# SBGames — Project Progress & Architecture Snapshot
*Last Updated: 2026-10-24 | Latest Commit: 5ef568a feat: add character N006, targeted burst heal skills, AI recovery systems, and validation tools*

## 1. Recently Completed Milestones & Features
- **Targeted Burst Heal Skill System & Adventurer N006 ([`TargetedBurstDetonation`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/TargetedBurstDetonation.cs), [`TargetedBurstTargetLifetime`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/TargetedBurstTargetLifetime.cs), [`VisualAnchor`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/Enums/VisualAnchor.cs), [Report](file:///d:/UnityProject/SBGames/docs/testing/targeted_burst_heal_followup_report.md)):**
  - Added `SkillType.TargetedBurst` with wind-up detonation (`SKILL_HEAL_001`: 0.5s wind-up, 25 BaseHeal, 10 mana, 6u range, 1s cooldown), tracking targets in `LateUpdate` via `VisualAnchor.Feet` without reparenting into target hierarchies.
  - Captures launch faction/tag alignment so caster death, deactivation, or destruction during wind-up does not cancel an in-flight heal; tracks target pool generation via `TargetedBurstTargetLifetime` so targets deactivated and immediately recycled do not receive stale heals.
  - Centralized all positive character healing through `CharacterStatsManager.ApplyHeal(amount, out actualHealed, out overheal)`, suppressing `OnLifeChanged` when `actualHealed == 0`, rejecting dead targets in `RestoreHealthEffect`, and emitting `SkillHitResolver.OnSkillHealWithOverhealApplied(SkillInstance, GameObject, float, float)` alongside `OnSkillHealApplied`.
  - Added healer adventurer `ADVENTURER_N006` (`ADVENTURER_N006.prefab`, `ADVENTURER_N006.asset`, animator controller/clips) and 12-frame `SKILLFX_SKILL_HEAL_001.prefab` (`Anim_S_H001`) on sorting layer `SkillFX` (order 5). Verified by [`HealSkillTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/HealSkillTests.cs) and [`TargetedBurstVisualFitTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/TargetedBurstVisualFitTests.cs).
- **Utility Score Normalization & Recovery Safety System ([`RecoverySafetyTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/RecoverySafetyTuningSO.cs), [`UtilityRecoverGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityRecoverGoal.cs), [`UtilitySurviveGoal`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilitySurviveGoal.cs)):**
  - Centralized `[0, 1]` utility score normalization in `BaseAI.SelectBestGoal` (sanitizing NaN to 0 and clamping infinities), applying `goalHysteresisBonus = 0.08f` strictly to temporary comparison scores and resolving ties in favor of `Survive`.
  - Replaced `UtilityRecoverGoal`'s `surviveOverrideFloor = 1.02f` with a zero-allocation multiplicative safety factor configured by [`RecoverySafetyTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/RecoverySafetyTuningSO.cs) (`Assets/Resources/AI/RecoverySafety_Default.asset`) combining cached `ThreatLevel`, 2-second recent damage from `CharacterAIMemory`, and cached enemy squared distance.
  - Enabled self-only skill healing in `UtilityRecoverGoal`, `RecoverSensor`, and `RecoverAction` without requiring an item kit, and fixed `AdventurerItemKitController` category slot remaining-count initialization. Verified by [`RecoverySafetyTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/RecoverySafetyTests.cs) and [`LegacyRecoveryRegressionTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/LegacyRecoveryRegressionTests.cs).
- **Unified Editor Validation Service ([`ValidationService`](file:///d:/UnityProject/SBGames/Assets/Editor/Validation/ValidationService.cs), [`ValidationFinding`](file:///d:/UnityProject/SBGames/Assets/Editor/Validation/ValidationFinding.cs)):**
  - Consolidated project-wide validation across [`AISetupValidatorWindow`](file:///d:/UnityProject/SBGames/Assets/Editor/Validation/AISetupValidatorWindow.cs), [`SBGamesCreatorWindow`](file:///d:/UnityProject/SBGames/Assets/Editor/Creator/SBGamesCreatorWindow.cs), and [`StatBalanceManagerWindow`](file:///d:/UnityProject/SBGames/Assets/Editor/Stats/StatBalanceManagerWindow.cs).
  - Added automated inspection of serialized `AnimationCurve` keys and Bezier tangent extrema across UtilityAI goals and skills, correcting 12 out-of-range curves across `KillEnemy`, `Warp`, and skill cluster curves. Verified by [`ValidationServiceTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/ValidationServiceTests.cs).
- **Combat Sandbox Pass Executor & Roster Audit ([`CombatSandboxPassExecutor`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatSandboxPassExecutor.cs), [Report](file:///d:/UnityProject/SBGames/docs/testing/combat_sandbox_pass_report.md)):**
  - Added batch scenario execution and telemetry export (`docs/testing/combat_sandbox_pass_telemetry.csv`) alongside [`PartyBattleAnalyticsHarness`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/PartyBattleAnalyticsHarness.cs) and [`CombatSandboxRosterAuditTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/CombatSandboxRosterAuditTests.cs).
  - Standardized skill animator folders (`Anim_S_F001`, `Anim_S_H001`, `Anim_S_L001`, `Anim_S_WA001`, `Anim_S_WA002`, `Anim_S_WI001`) and added [`SpriteAnimationBuilderSkillTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/SpriteAnimationBuilderSkillTests.cs).
- **Tactical Positioning, Kiting & Fight Outlook ([`KeepDistanceResolver`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/KeepDistanceResolver.cs), [`FightOutlookCalculator`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/FightOutlookCalculator.cs)):**
  - Dynamic ranged kiting (`UtilityKeepDistanceGoal`, `KeepDistanceTuningSO`, `KeepDistanceMode`) and zero-allocation TTK vs TTD fight outlook calculation (`FightOutlookTuningSO`).
- **Unity Editor & Test Suite Status:**
  - 40 test suites across `Assets/Tests/` (28 runtime suites) and `Assets/Tests/Editor/` (12 editor suites) with all 78 discovered EditMode tests passing and 0 AI setup validator findings.
  - Live Unity Editor verified via Unity CLI (`unity status`, `unity cmd console`): PID 42080 running on port 7800, 0 compilation errors, 0 active console errors.

## 2. Current Codebase & Script Inventory (Delta / Key Modules)
Current C# file counts across `Assets/Scripts/` (353 total scripts), `Assets/Editor/` (37 total scripts), and `Assets/Tests/` (40 total scripts):

| Folder | C# files | Role / Key Modules |
| --- | ---: | --- |
| `Assets/Scripts/Character` | 139 | Character identity, controllers, traps, AI profiles/memory, adaptive perception, fight outlook, keep distance, recovery safety, and GOAP/Utility AI (+1 script). |
| `Assets/Scripts/Dungeon` | 26 | Room/door markers, registry, procedural layout graph/generator, dynamic piece system, seam layout solver, and runtime builder. |
| `Assets/Scripts/Placement` | 26 | Dungeon placement system, grid visualizer, bench, buildable mask validator, and room capacity tracking. |
| `Assets/Scripts/Loop` | 26 | Core game loop, combat results handler, DM abilities/talents, post-battle reports, and progression/fame. |
| `Assets/Scripts/Inventory` | 26 | Inventory, equipment, items, consumable effects (including `RestoreHealthEffect` and Warp escape), and save data. |
| `Assets/Scripts/Skills` | 16 | Skill casting, targeting, projectile pooling, targeted burst detonation/lifetime, visual anchors, and hit resolution (+3 scripts). |
| `Assets/Scripts/Database` | 13 | Catalog ScriptableObjects, CSV parsers, character/item databases, and editor sync pipeline. |
| `Assets/Scripts/Shop` | 12 | Shop manager, merchant inventories, pricing, and economy loop. |
| `Assets/Scripts/Stat` | 11 | Character base stats, stat managers (`ApplyHeal`, `TakeDamage`, `TickRegen`), life/mana regeneration pipeline, and basic attack modes. |
| `Assets/Scripts/Squads` | 11 | Party leader, squad blackboard, squad member coordination, tactical evaluators, and scout intel HUD. |
| `Assets/Scripts/Personality` | 7 | Character personality traits, personality profiles, and behavioral biases. |
| `Assets/Scripts/Testing` | 6 | Combat test bootstrap, HUD, battle simulation runner, party battle analytics harness, and sandbox pass executor (+1 script). |
| `Assets/Scripts/Events` | 6 | Dungeon event definitions, event manager, contexts, and altar/dark boon events. |
| `Assets/Scripts/Input` | 4 | Input management and keybinding configurations. |
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
- [`TargetedBurstDetonation`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/TargetedBurstDetonation.cs) & [`TargetedBurstTargetLifetime`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/TargetedBurstTargetLifetime.cs):
  - **Responsibilities:** Manages delayed wind-up burst effects (`SkillType.TargetedBurst`) following target world positions (`VisualAnchor.Feet`), preserving launch caster alignment if the caster dies mid-wind-up, and invalidating bursts if the target is deactivated/recycled.
  - **Key API:** `Arm(...)`, `Generation`, `VisualAnchor`.
- [`RecoverySafetyTuningSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/RecoverySafetyTuningSO.cs):
  - **Responsibilities:** Immutable tuning asset computing a `[0, 1]` multiplicative safety factor for `UtilityRecoverGoal` from cached `ThreatLevel`, recent damage ratio in `CharacterAIMemory`, and squared enemy distance.
  - **Key API:** `EvaluateSafety(threatLevel, recentDamageRatio, nearestEnemyDistSqr)`.
- [`ValidationService`](file:///d:/UnityProject/SBGames/Assets/Editor/Validation/ValidationService.cs) & [`ValidationFinding`](file:///d:/UnityProject/SBGames/Assets/Editor/Validation/ValidationFinding.cs):
  - **Responsibilities:** Shared editor validation engine auditing character prefabs, skill assets, GOAP capabilities, and serialized `AnimationCurve` value/tangent ranges across editor windows.
  - **Key API:** `ValidateAll()`, `ValidateCurveInUnitRange(AnimationCurve, ...)`.
- [`CombatSandboxPassExecutor`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatSandboxPassExecutor.cs):
  - **Responsibilities:** Executes multi-scenario sandbox passes and exports structured battle telemetry and markdown summaries.

## 3. Architecture, GOAP & Event Wiring
- **GOAP & Utility AI Catalog:**
  - **14 Goals (`GoalBase`):** `AlertAlliesGoal`, `DebriefLeaderGoal`, `FallBackGoal`, `FocusFireGoal`, `FollowLeaderGoal`, `HealAllyGoal`, `IdleGoal`, `KeepDistanceGoal`, `KillEnemyGoal`, `RecoverGoal`, `ScoutReconGoal`, `StandbyGoal`, `SurviveGoal`, `WarpGoal`.
  - **14 Actions (`GoapActionBase`):** `AlertAlliesAction`, `AttackAction`, `CastSkillAction`, `DebriefLeaderAction`, `FallBackAction`, `FleeAction`, `FocusFireAction`, `FollowLeaderAction`, `HealAllyAction`, `IdleAction`, `RecoverAction`, `ScoutReconAction`, `WanderAction`, `WarpAction`.
  - **14 Utility Goals (`UtilityGoal`):** `UtilityAlertAlliesGoal`, `UtilityDebriefGoal`, `UtilityFallBackGoal`, `UtilityFocusFireGoal`, `UtilityFollowLeaderGoal`, `UtilityHealAllyGoal`, `UtilityKeepDistanceGoal`, `UtilityKillEnemyGoal`, `UtilityRecoverGoal`, `UtilityScoutReconGoal`, `UtilityStandbyGoal`, `UtilitySurviveGoal`, `UtilityWanderGoal`, `UtilityWarpGoal`.
  - **Utility Skills:** `UtilityBaseSkill`, `UtilityFireSpell`, `UtilityNoAvailableSkill`, plus database-registered assets (`Utility_SKILL_001`, `Utility_SKILL_HEAL_001`, `Utility_SKILL_F001`, `Utility_SKILL_F002`, `Utility_SKILL_L001`, `Utility_SKILL_L002`, `Utility_SKILL_WA001`, `Utility_SKILL_WA002`, `Utility_SKILL_WI001`, `Utility_SKILL_WI002`).
  - **15 World Keys (`WorldKeyBase`):** `AgentLife`, `AgentMana`, `AlliesNeedHealing`, `CanWarp`, `EnemyDistance`, `FearLevel`, `HasAlertedAllies`, `HasDebriefedLeader`, `HasScoutedArea`, `HasSquadOrder`, `IsIdle`, `IsSkillReady`, `NeedsRecovery`, `SquadOrderTypeKey`, `ThreatLevel`.
  - **10 Target Keys (`TargetKeyBase`):** `EnemyTarget`, `FocusFireTarget`, `KeepDistanceTarget`, `LeaderTarget`, `RecoverTarget`, `ScoutReconTarget`, `SelfTarget`, `WanderTarget`, `WarpTarget`, `WoundedAllyTarget`.
  - **10 Capability Factories:** `AttackCapabilityFactory`, `BasicCapabilityFactory`, `HealCapabilityFactory`, `MinionBasicCapabilityFactory`, `RecoveryCapabilityFactory`, `SkillCapabilityFactory`, `SquadCapabilityFactory`, `SurvivalCapabilityFactory`, `WanderCapabilityFactory`, `WarpCapabilityFactory`.
  - **Agent Type Capabilities Mapping:**
    - `NormalAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Warp, Heal.
    - `MageAgent`: Wander, Attack, Skill, Basic, Squad, Survival, Recovery, Heal, Warp.
    - `MinionAgent`: Wander, Attack, MinionBasic, Squad, Survival *(Recovery, Warp, and Heal omitted)*.
- **Core Systems & Services Wiring:**
  - `BaseAI`: Centrally normalizes all `UtilityGoal` evaluations to `[0, 1]`, applies `goalHysteresisBonus = 0.08f` only to temporary comparison values, resolves equal-score ties in favor of `Survive`, and loads `RecoverySafety_Default` for safe recovery scoring.
  - `CharacterStatsManager`: Single authoritative entry point for character vitality changes: damage via `IDamageable.TakeDamage(in DamageData)` and healing via `ApplyHeal(float, out float, out float)`.
  - `PartyLeader` & `SquadBlackboard`: Coordinates squad orders (`Engage`, `FallBack`, `ScoutRecon`, `Standby`), room clearance progress, hazard memory, focus-fire targets, and leader succession.
  - `DungeonRoomRegistry`, `DungeonPlacementManager` & `GameServices`: Manages room volumes, piece placement, capacity tracking (`IRoomCapacityTracker`), and singleton service resolution.
- **Cross-System Event Hooks:**
  - `CharacterStatsManager`: `OnLifeChanged(current, max)`, `OnLifeRestored(amount)`, `OnCharacterDeath(manager)`.
  - `SkillHitResolver`: `OnSkillHealApplied(SkillInstance, GameObject, float)` (actual heal) and `OnSkillHealWithOverhealApplied(SkillInstance, GameObject, float, float)` (actual heal + overheal).
  - `AdventurerEventHub`: Publishes leader death/succession, checkpoint updates, room entry/clearing, potion/warp usage, retreat blockades, DM seal triggers, and escape completion.
  - `CombatResultHandler` & `CoreGameLoop`: Emits `OnCombatEnded`, `OnSquadWiped`, `OnAdventurersEscaped`, `OnPhaseChanged`, `OnRoundStarted`, and `OnGameOver`.

## 4. Pending Manual Steps, Known Issues & Next Steps
- **Unity Editor / Inspector Tasks:**
  - Run `Tools/SBGames/AI Setup Validator` (or `ValidationService`) after authoring new skills, utility curves, or character prefabs to maintain 0 validation findings.
  - When authoring new `UtilityGoal` or `UtilitySkill` curves, ensure curve keys and Bezier tangents stay strictly within `[0, 1]` so `ValidationService` passes without curve overshoot warnings.
  - Keep `Resources/AI/RecoverySafety_Default.asset` present or assign a custom `RecoverySafetyTuningSO` on `BaseAI` when tuning recovery-vs-retreat thresholds.
- **Known Gotchas / Architectural Constraints:**
  - **Inactive GameObject Coroutines on Action Stop:** Deactivating an agent during death/despawn while a GOAP action stops (`OnDisable` $\to$ `StopAction`) can invoke recovery coroutines on an inactive GameObject unless guarded by `gameObject.activeInHierarchy`.
  - **Legacy Test Discovery Boundary:** Tests in `Assets/Tests/*.cs` compile into `Assembly-CSharp` rather than an Editor test assembly unless bridged via fixtures in `Assets/Tests/Editor/` (such as `LegacyRecoveryRegressionTests.cs`).
  - **Macro Footprints & Phase Enums:** Dungeon piece generation supports single-piece dynamic dimensions ($W \times H \ge 4 \times 4$) while multi-node macro pieces remain deferred; `CoreGameLoop.GamePhase` and `DungeonPhaseState.DungeonPhase` remain separate enums sharing ordinal 0 for planning/build.
