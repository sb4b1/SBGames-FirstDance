# SBGames Engineering & Technical Architecture Guide

This guide is the primary technical manual for software engineers and AI coding agents working on **SBGames**. It combines an exhaustive file-by-file codebase inventory, core architectural patterns, runtime execution flows, coding invariants, and step-by-step playbooks for designing and extending features.

---

## 1. Architectural Patterns & Core Paradigms

Before modifying or adding code, understand these four foundational architectural patterns used throughout the project:

### Pattern A: Pure C# Core + Thin Unity Wrapper
Several subsystems (`Placement`, `Shop`, `Inventory`) are strictly separated into two layers:
- **`Core/` (Pure C#)**: Contains business logic, state containers, calculators, and validation with **zero Unity dependencies** (no `MonoBehaviour`, `GameObject`, or `UnityEngine`). This layer is fast, testable in standard CLI runners, and uses interfaces for dependency injection (e.g., `IRandomProvider`, `ICapacityPool`, `IResourceWallet`).
- **`Unity/` (MonoBehaviour Wrapper)**: Bridges the pure-C# core to the scene hierarchy, input events, particle effects, UI Toolkit, and inspectors.

### Pattern B: Two-Tier Decoupled Character Hierarchy
Every combat entity separates physical state/actuation from AI planning:
- **Root GameObject**: Carries identity, physical colliders, and combat controllers (`CharacterBrain`, `CharacterStatsManager`, `CharacterCombatController`, `AnimationObjectController`, `FactionMember`).
- **`AI_Augment` Child**: Tagged `AI_Augment`. Carries perception, pathfinding, and GOAP/Utility AI (`BaseAI`, `GoapActionProvider`, `AgentMoveBehaviour`, `VisionController`, `CharacterSkillController`, `NavMeshAgent`).

### Pattern C: Reflection-Based Service Locator (`GameServices.cs`)
- `GameServices` provides static access to core singletons (`CharacterManager`, `CharacterRuntimeDatabase`, `UtilitySkillRuntimeDatabase`, `MasterGameDatabase`).
- **Gotcha**: `GameServices.Register<T>()` sets properties **by reflection using the type name**. The registered type's name must match the target property name exactly, or registration fails silently (logs a warning only).

### Pattern D: GOAP Goal & Utility Goal Pairing
AI goals operate in pairs:
- **GOAP Goal** (`Assets/Scripts/GOAP/Goals/`): A lightweight (2–5 lines) marker class used by CrashKonijn GOAP for condition/effect graph planning.
- **Utility Goal** (`Assets/Scripts/GOAP/UtilityAI/Goal/`): The evaluation script calculating real-time urgency (0.0 to 1.0) using `AnimationCurve`s. When extending goals, both must be created and registered together.

---

## 2. Complete Codebase & Subsystem Inventory

All paths are relative to `Assets/Scripts/`.

```
Assets/Scripts/
├── Base/            - Core identifiers (ObjectIdenifier.cs)
├── Characters/      - Actor state, combat controllers, perception, trap controllers
├── Database/        - ScriptableObject catalogs, runtime registry, CSV pipeline
├── Environment/     - Destructible props and environment objects
├── Events/          - Dungeon events (AltarEventSO, DarkBoonEventSO, trigger contexts)
├── Factions/        - Faction member interfaces and identifiers (Monster vs Adventurer)
├── Gizmo/           - Debug visualization helpers
├── GOAP/            - CrashKonijn GOAP v3 (Actions, Goals, Sensors, Keys, Behaviours)
├── Hierarchy/       - Editor hierarchy dividers and labels
├── Input/           - InputManager, UI toggle interfaces
├── Interface/       - Core contracts (IDamageable, ISkillMovement, ISkillDetonation)
├── Inventory/       - Items, stat modifiers, runes, inventory UI, equipment binding
├── Loop/            - CoreGameLoop, DungeonMaster, Progression, Fame, Talents
├── Manager/         - CharacterManager, ObjectPoolManager, DevTool
├── Personality/     - Traits, race instincts, personality databases, altar events
├── Placement/       - Grid placement system (Pure C# Core + Unity wrapper), BenchManager
├── SB4b1/           - Low-level utilities (GameRng, CombatMath, Loggers, ComponentUtil)
├── Shop/            - Monster shop system (Pure C# Core + Unity placement coordinator)
├── Skills/          - Skill data, projectile movement, detonation, casting controllers
├── Squads/          - PartyLeader, SquadBlackboard, tactical evaluator, recon markers
├── Stat/            - CharacterStatsManager, Stat, StatModifier, DamageData, BaseStatsSO
├── StatusEffect/    - Status effect definitions, runtime instances, manager
└── Testing/         - Battle simulation harness, test bootstrap, HUD
```

### Subsystem Details

#### 2.1 Characters, Combat & Vitality
- **`Characters/CharacterBrain.cs`**: The central actor state machine. Tracks `CanMove`, `IsAttacking`, `IsCastingSkill`, `IsRecovering`, `AgentStatus`, and implements `IPoolable`. Controls AI state via `AIActivation(bool)`.
- **`Characters/Controller/`**:
  - `CharacterCombatController`: Executes basic weapon attacks, hitbox triggers, and attack recovery buffers.
  - `CharacterSkillController` (~500 lines): Manages equipped skills, cooldowns, mana validation, and utility-based skill selection.
  - `VisionController`: Throttles SensorToolkit queries (0.2s interval) and updates `BaseAI` target lists.
  - `AnimationObjectController`: Bridges state changes to Animator triggers and hit reactions.
  - `CharacterMovementController` & `CharacterFlipRotator`: Movement handling and sensor rotation on flip.
- **`Characters/AI/CharacterAIMemory.cs`**: Pre-allocated tactical memory (damage records, grudges, last-known targets) and spatial hazard memory.
- **`Characters/AI/DoorwayStagingController.cs`**: Adds hesitation and pacing when navigating doorways/corners via 2D raycasts.
- **`Characters/Traps/TrapController.cs`**: Trap logic, trigger interactions, damage delivery. Implements `IDamageable`.
- **`Stat/`**:
  - `CharacterStatsManager`: Implements `IDamageable`. Calculates derived stats from `CharacterBaseStatSO` + item modifiers + status effects.
  - `DamageData`: Struct containing `Amount`, `Type` (`Physical`/`Magical`/`True`), `Source`, and `IsCritical`.
  - `Stat`, `StatModifier`, `SkillStatsManager`.
- **`CombatResolver.cs`** (root): Static damage mitigation and critical hit resolution using `CombatMath` and `GameRng`.
- **`StatusEffect/`**: `StatusEffectData` (SO), `StatusEffect` (runtime tick container), `StatusEffectManager`.
- **`Factions/`**: `FactionId` (`Monster`, `Adventurer`, `Neutral`, plus placeholder expansion slots), `FactionMember`.

#### 2.2 Artificial Intelligence & Squads (`GOAP/`, `Squads/`, `Personality/`)
- **`GOAP/AI/BaseAI.cs`** (~800 lines): Blackboard and utility arbiter. Holds `EnemyTargets`, `AllyTargets`, `CurrentTarget`, emotion stats (`ThreatLevel`, `FearLevel`), and vitals. Automatically attaches `AIDecisionThrottler` and `CharacterAIMemory`.
- **`GOAP/UtilityAI/`**: Scored via `AnimationCurve`. Goals include `KillEnemy`, `Survive`, `FallBack`, `FocusFire`, `FollowLeader`, `AlertAllies`, `ScoutRecon`, `Debrief`, `Wander`.
- **`GOAP/AgentTypes/` & `GOAP/Capabilities/`**: Factory infrastructure binding goals, actions, and keys:
  - Agent Types: `NormalAgentTypeFactory`, `MageAgentTypeFactory`, `MinionAgentTypeFactory`.
  - Capabilities: `AttackCapabilityFactory`, `BasicCapabilityFactory`, `SkillCapabilityFactory`, `SurvivalCapabilityFactory`, `SquadCapabilityFactory`, `WanderCapabilityFactory`, `MinionBasicCapabilityFactory`.
- **`Squads/`**:
  - `PartyLeader`: Leader brain for adventurer squads. Owns a shared `SquadBlackboard`.
  - `SquadTacticalEvaluator`: Evaluates battlefield conditions and issues `SquadOrderType` directives (`FollowLeader`, `FocusFire`, `FallBack`, `Standby`, `ScoutAhead`).
  - `ReconWorldMarkerManager`: Manages pooled visual recon markers.
- **`Personality/`**: `CharacterPersonality`, `CharacterTraitSO`, `RaceInstinctSO`, and `AltarEventManager` (handling mid-run moral/stat choices).

#### 2.3 Meta Loop, Progression & The Dungeon Master (`Loop/`)
- **`CoreGameLoop.cs`**: Top-level phase machine managing phases (`NotStarted`, `Planning`, `Battle`, `GameOver`).
- **`DungeonMaster.cs`** (~1040 lines): Dungeon Master state container. Manages Energy, essence, equipped `Artifact`s, and defeat conditions.
- **`CombatResultHandler.cs`** (~910 lines): Records casualties, DPS, and survivor counts; communicates outcomes to progression and UI.
- **`DifficultyScaler.cs`**: Compares dungeon defense power against incoming adventurer encounter rating to yield a `DifficultyTier`.
- **`DungeonFameSystem.cs`**: Manages win streaks, fame thresholds, tier promotions, and rare artifact drops.
- **`DungeonProgression.cs`**: Tracks Dungeon Rank (E through S), experience, and stat unlocks.
- **`Loop/Talents/`**: `DMTalentTreeManager`, `TalentNodeSO`, and `TalentTreeConfigSO` managing the DM's persistent talent tree.

#### 2.4 Dungeon Placement & Shop (`Placement/`, `Shop/`)
- **`Placement/Core/` (Pure C#)**: `DungeonPlacementSystem` coordinates grid cell occupancy (`IGridOccupancyTracker`), placement costs (`IResourceWallet`), and capacity power multipliers (`ICapacityMultiplierStrategy`).
- **`Placement/Unity/`**: `DungeonPlacementManager`, `PlacementCursorController` (~880 lines), `DungeonGridVisualizer`.
- **`Placement/Bench/`**: `BenchManager` and `BenchSlot` managing the staging area for purchased monsters.
- **`Shop/Core/` (Pure C#)**: `MonsterShopSystem` handles random card generation, tier weighting, slot locking, and rerolls using `IRandomProvider`.
- **`Shop/Unity/`**: `ShopPlacementCoordinator` transfers purchased shop units onto the bench or grid.

#### 2.5 Inventory, Items & Runes (`Inventory/`)
- **`Inventory/Core/` (Pure C#)**: `Inventory`, `InventorySlot`, and `InventoryItemInstance`.
- **`InventoryManager.cs`** (~970 lines): MonoBehaviour coordinator. Equipment consists of **4 Rune slots + 1 Artifact slot** (`EquipmentSlotType`). Enforces phase locks during the Battle phase.
- **`CharacterEquipmentStatBinder.cs`**: Converts equipped item modifiers into runtime `StatModifier` instances on `CharacterStatsManager`.
- **`InventoryUIController.cs`** (~1280 lines): UI Toolkit presentation layer for bag, runes, and tooltips.

#### 2.6 Data Architecture, Events & Managers (`Database/`, `Events/`, `Manager/`)
- **`Database/`**: Catalogs (`CharacterDatabaseSO`, `SkillDatabaseSO`, `ItemDatabaseSO`) indexed into `MasterGameDatabaseSO`. `DatabaseCsvPipeline` enables bidirectional CSV synchronization with `Assets/DataTables/`.
- **`Events/`**: `DungeonEventManager` dispatches `DungeonEventSO.Execute(context, onComplete)` during run checkpoints.
- **`Manager/`**:
  - `CharacterManager`: Runtime registration and querying of living units.
  - `ObjectPoolManager`: High-performance object pool with `IPoolable` lifecycle hooks.
  - `DevTool` (~2000 lines): In-editor HUD, profiling, gizmo rendering, and debug control panel.

---

## 3. Runtime Execution & Lifecycles

```mermaid
sequenceDiagram
    autonumber
    participant Loop as CoreGameLoop
    participant Bench as BenchManager
    participant Grid as DungeonPlacementSystem
    participant CB as CharacterBrain
    participant AI as AI_Augment (BaseAI)
    participant VC as VisionController
    participant GOAP as CrashKonijn GOAP
    participant CC as CharacterCombatController
    participant Target as Target IDamageable

    Note over Loop: PLANNING PHASE
    Loop->>Bench: Purchase & Stage Monsters
    Loop->>Grid: Deploy Monsters to Grid Positions
    Grid->>CB: Instantiate Prefabs (AI Disabled)

    Note over Loop: TRANSITION TO BATTLE PHASE
    Loop->>CB: AIActivation(true)
    
    loop 0.2s Throttled Perception
        VC->>VC: Pulse SensorToolkit Sensors
        VC->>AI: Write to EnemyTargets & AllyTargets
    end

    loop GOAP Decision Cycle
        AI->>AI: Evaluate UtilityGoals (SelectBestGoal)
        AI->>GOAP: RequestGoal<TGoal>()
        GOAP->>GOAP: Resolve Action Graph Plan
        GOAP->>AI: AgentMoveBehaviour drives NavMesh destination
    end

    Note over CB,CC: When Target within Attack Range
    GOAP->>CC: Perform Action -> Attack()
    CC->>CB: Set IsAttacking = true
    CC->>Target: TakeDamage(in DamageData)
    Target->>Target: Apply Resistances & Deduct HP
    CC->>CB: Post-Attack Recovery Delay

    Note over Target: On Lethal Damage
    Target-->>CB: TakeDamage returns true (Dead)
    CB->>AI: AIActivation(false)
    CB->>Loop: Register Casualty to CombatResultHandler
```

### Complete Cross-System Connections
1. **Character Stack**: `CharacterBrain` holds `CharacterStatsManager`, `AnimationObjectController`, `CharacterCombatController`, and `CharacterSkillController`. `BaseAI` senses via `VisionController`, picks a `UtilityGoal`, requests a GOAP plan, and executes actions calling back into the brain and combat controllers.
2. **Skill Path**: `CharacterSkillController` casts via `SkillCaster`, instantiating a pooled `SkillInstance`. Movement (`ISkillMovement`) and detonation (`ISkillDetonation`) guide the projectile. Collision triggers `SkillHitResolver`, `CombatResolver` calculates final damage, and `StatusEffectManager` applies buffs/debuffs.
3. **Squad Path**: `PartyLeader` reads the `SquadBlackboard` and writes orders via `SquadTacticalEvaluator`. Members read orders via `SquadOrderSensor`, unlocking squad GOAP goals (`FollowLeader`, `FocusFire`, `FallBack`).
4. **Game Flow**: `CoreGameLoop` manages phase transitions. Planning allows shop purchases, bench staging, and grid deployment. The Battle phase locks placement/inventory and runs combat simulation. `CombatResultHandler` evaluates the outcome and updates `DungeonFameSystem`, `DungeonProgression`, and `DifficultyScaler`.
5. **Equipment to Power**: Equipping runes/artifacts generates `StatModifier`s via `CharacterEquipmentStatBinder`. `CharacterCombatPower` recalculates unit combat power, feeding both grid capacity limits and battle difficulty scaling.

---

## 4. Strict Coding Invariants

All agents writing code in this repository must obey these four hard rules:

### Rule 1: ScriptableObjects are Strictly Immutable Runtime Templates
- `CharacterBaseStatSO`, `SkillData`, and `ItemDataSO` are asset definitions.
- **Never mutate fields on ScriptableObjects at runtime**. Doing so permanently alters asset files in the project and leaks state between instances.
- Always store mutable runtime state on component managers (`CharacterStatsManager`, `SkillInstance`, `InventoryItemInstance`).

### Rule 2: Damage Exclusively Through `IDamageable.TakeDamage`
- Never deduct health directly from external scripts.
- Always pass a `DamageData` struct using the `in` parameter keyword:
  ```csharp
  // CORRECT:
  if (target.TryGetComponent<IDamageable>(out var damageable))
  {
      damageable.TakeDamage(new DamageData(calculatedDamage, DamageType.Physical, source: this));
  }

  // STRICTLY FORBIDDEN:
  targetStatsManager.CurrentHealth -= 25f; // VIOLATION
  ```

### Rule 3: Zero-Allocation Hot Paths (Target 60 FPS)
All code executing in `Update()`, `FixedUpdate()`, GOAP `Sense()`, `Perform()`, or Utility `Evaluate()` must not allocate managed memory on the heap:
- **No LINQ**: Avoid `.Where()`, `.OrderBy()`, `.FirstOrDefault()`, `.ToList()`.
- **No Lambdas / Closures**: Do not allocate delegates inside tick loops.
- **Distance Checks**: Use `(posA - posB).sqrMagnitude < range * range` instead of `Vector2.Distance()`.
- **Physics**: Pass preallocated arrays to `Physics2D.OverlapCircleNonAlloc` and preallocated lists to SensorToolkit queries.

### Rule 4: Decoupled GOAP Actuation
- **Actions only act; they never drive navigation**: Movement is handled exclusively by `AgentMoveBehaviour` and `NavMeshAgent`.
- **Controller Injection**: Actions inject parent controllers using `[GetComponentInParent]` rather than direct hard references.

---

## 5. Feature Extension Playbooks

### Playbook 1: Adding a New Character Archetype

1. **Create Base Stats Asset**:
   - Unity Menu: `Assets > Create > SBGames > Stats > Character`.
   - Set attributes (`MaxLife`, `Damage`, `AttackRange`, `Movespeed`, `AttackTimeInterval`).
   - Sync values with `Assets/DataTables/CharacterStats.csv` via `StatBalanceManagerWindow`.
2. **Construct Prefab**:
   - Root GameObject: Add `CharacterBrain`, `CharacterStatsManager`, `CharacterCombatController`, `AnimationObjectController`, `FactionMember`, and `CapsuleCollider2D`.
   - Set Layer to `Character` (6). Set Tag to `Monster` or `Adventurer`.
3. **Attach `AI_Augment` Child**:
   - Create child named `AI_Augment` with Tag `AI_Augment`.
   - Attach `BaseAI`, `GoapActionProvider`, `AgentBehaviour`, `AgentMoveBehaviour`, `VisionController`, and `NavMeshAgent`.
   - Create child `Sensors` containing `RangeSensor2D` and `LOSSensor2D`, and wire them into `VisionController`.
4. **Configure Agent Type**:
   - Set `AgentType` on `GoapActionProvider` to `NormalAgent`, `MageAgent`, or a new custom factory registered in `Assets/Scripts/GOAP/AgentTypes/`.
5. **Verify**:
   - Test in `Assets/Scenes/Combat_Sandbox.unity` using `BattleSimulationTestRunner`.

---

### Playbook 2: Creating a New Skill

1. **Create `SkillData` Asset**:
   - Unity Menu: `Assets > Create > SBGames > Skills > Skill Data`.
   - Define `skillID`, `baseDamage`, `damageType`, `castRange`, `baseManaCost`, and `targetFilter` (`EnemiesOnly`, `AlliesOnly`, `Everyone`).
2. **Build Projectile / Effect Prefab**:
   - Attach movement component (`LinearProjectileMovement` or `BeamMovement`).
   - Attach detonation component (`AOEDetonation` or `SkillHitResolver`).
   - Attach `SkillCollision` and a 2D Trigger Collider.
3. **Register with Object Pool**:
   - Add the prefab to `ObjectPoolManager.Instance` to prevent runtime instantiation spikes.
4. **Equip Skill**:
   - Add the `SkillData` asset to the unit's `CharacterSkillController.equippedSkills` list.

---

### Playbook 3: Adding a New GOAP Goal, Utility Scorer & Action

1. **Create Goal Marker and Key**:
   ```csharp
   // Assets/Scripts/GOAP/TargetKeys/Target/HealTarget.cs
   public class HealTarget : TargetKeyBase {}

   // Assets/Scripts/GOAP/Goals/HealAllyGoal.cs
   public class HealAllyGoal : GoalBase {}
   ```
2. **Create Utility Goal Scorer**:
   ```csharp
   // Assets/Scripts/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs
   public class UtilityHealAllyGoal : UtilityGoal
   {
       public override float Evaluate(BaseAI ai)
       {
           // Return urgency (0.0 to 1.0) based on wounded ally health
           return ai.HasWoundedAlly ? urgencyCurve.Evaluate(ai.LowestAllyHealthPercent) : 0f;
       }
   }
   ```
3. **Implement GOAP Action**:
   ```csharp
   // Assets/Scripts/GOAP/Actions/HealAllyAction.cs
   public class HealAllyAction : GoapActionBase<HealAllyAction.Data>
   {
       public class Data : IActionData
       {
           public ITarget Target { get; set; }
           [GetComponentInParent] public CharacterSkillController SkillController { get; set; }
       }

       public override ActionRunState Perform(IMonoAgent agent, Data data, ActionContext context)
       {
           if (data.Target == null || data.SkillController == null)
               return ActionRunState.Stop;

           bool castSuccess = data.SkillController.TryCastSkill(skillIndex: 1, data.Target.Position);
           return castSuccess ? ActionRunState.Completed : ActionRunState.Stop;
       }
   }
   ```
4. **Bind in Capability Factory**:
   Create or edit a factory under `Assets/Scripts/GOAP/Capabilities/`:
   ```csharp
   public class SupportCapabilityFactory : CapabilityFactoryBase
   {
       public override ICapabilityConfig Create()
       {
           var builder = new CapabilityBuilder("SupportCapability");
           builder.AddGoal<HealAllyGoal>().AddCondition<AllyHealthLow>(Comparison.SmallerThanOrEqual, 0).SetBaseCost(2);
           builder.AddAction<HealAllyAction>().SetTarget<HealTarget>().AddEffect<AllyHealthLow>(EffectType.Decrease).SetBaseCost(1);
           return builder.Build();
       }
   }
   ```
5. **Register Capability in Agent Type**:
   In `NormalAgentTypeFactory.cs`: `builder.AddCapability<SupportCapabilityFactory>();`.

---

### Playbook 4: Creating a New Item & Stat Modifier

1. **Create `ItemDataSO` Asset**:
   - Unity Menu: `Assets > Create > SBGames > Inventory > Item Data SO`.
   - Configure identity (`itemID`, `displayName`, `itemType`, `equippableSlotType`).
2. **Configure Modifiers**:
   - In `statModifiers`, add bonuses (`Damage`, `MaxLife`, `Movespeed`) with modifier types (`Flat`, `PercentAdd`, or `PercentMult`).
3. **Equip Flow**:
   - Equipping triggers `CharacterEquipmentStatBinder.OnEquipmentChanged`, which instantiates `StatModifier`s and applies them to `CharacterStatsManager`.

---

### Playbook 5: Adding a New Dungeon Event

1. **Inherit from `DungeonEventSO`**:
   ```csharp
   // Assets/Scripts/Events/CustomBoonEventSO.cs
   [CreateAssetMenu(menuName = "SBGames/Events/Custom Boon")]
   public class CustomBoonEventSO : DungeonEventSO
   {
       public override void Execute(EventTriggerContext ctx, Action onComplete)
       {
           // Apply run modifiers or trigger UI choice modal
           ctx.DungeonMaster.AddEnergy(50);
           onComplete?.Invoke();
       }
   }
   ```
2. **Register in `EventDatabaseSO`**:
   Add the event asset to `EventDatabaseSO` so `DungeonEventManager` can select and dispatch it during run progression.

---

## 6. Known Codebase Quirks & Gotchas

Agents working on the codebase should be aware of these existing code traps and inconsistencies:

1. **Two Overlapping Phase Enums**:
   - `GamePhase` in `Loop/CoreGameLoop.cs` (`NotStarted`, `Planning`, `Battle`, `GameOver`).
   - `DungeonPhase` in `Placement/Core/DungeonPhaseState.cs` (`PlanningPhase = 0`, `BuildPhase = 0`, `CombatPhase = 1`).
   - Notice that `PlanningPhase` and `BuildPhase` share enum value `0`. When modifying game phase transitions, ensure both systems remain in sync.
2. **Reflection Registration in `GameServices.Register<T>()`**:
   - Service property names must match type names verbatim. If a type name differs from the property name, registration fails silently (logs a warning only).
3. **Known Typographical Errors in Source Code**:
   - `ObjectIdenifier.cs` (missing 't' in Identifier).
   - `UtillityNoAvaiableSkill.cs` (misspelled Utility and Available).
   - `BasicCapabiliityFactory.cs` (extra 'i').
   - `CharacterRuntimeDataBase.cs` (file name has capital B, but class is `CharacterRuntimeDatabase`).
   *Rule: Do not rename these classes or files without updating all code references, as doing so breaks existing call sites.*
4. **Large Source Files (Inspect in Ranges)**:
   Avoid viewing entire files when working on these modules; read in slices:
   - `DevTool.cs` (~2,000 lines)
   - `InventoryUIController.cs` (~1,280 lines)
   - `DungeonMaster.cs` (~1,040 lines)
   - `CombatResultHandler.cs` (~910 lines)
   - `PlacementCursorController.cs` (~880 lines)
   - `BaseAI.cs` (~800 lines)
5. **No TODO/FIXME Comments**:
   The codebase contains no inline TODO tags; refer to GitHub Issues and `docs/agents/issue-tracker.md` for task specifications.
6. **Placeholder Faction Slots**:
   `FactionId` includes `Faction_01` and `Faction_02` reserved for future multi-team combat expansions.

---

## 7. Testing & Verification

1. **Combat Sandbox Simulation**:
   - Open `Assets/Scenes/Combat_Sandbox.unity`.
   - Run isolated simulations via `BattleSimulationTestRunner` at $1\times$, $2\times$, or $5\times$ speed.
   - Verifies unit stats, GOAP decision loops, and combat resolution without needing to boot the meta loop.
2. **Unity CLI Compilation Check**:
   - Always run the Unity CLI to confirm assembly compilation passes with zero errors:
     ```powershell
     unity -quit -batchmode -projectPath "d:\UnityProject\SBGames" -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation
     ```
