# Battle Simulation Sandbox & Test Harness Guide

This guide describes how to configure and execute isolated, high-speed combat balance simulations in Unity 6 (`6000.6.2f1`) for **SBGames**, completely bypassing meta systems (Shop, Inventory, Bench, Grid Placement, DungeonMaster HP/Essence, and CoreGameLoop round phases).

---

## 1. Architecture & Design Principles

The Battle Simulation Sandbox isolates pure combat logic:
- **Character Brain & Actions**: [`CharacterBrain`](file:///d:/UnityProject/SBGames/Assets/Scripts/Characters/CharacterBrain.cs), [`CharacterCombatController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Characters/Controller/CharacterCombatController.cs), [`CharacterSkillController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/CharacterSkillController.cs).
- **Damage & Vitality**: [`CharacterStatsManager`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs), [`IDamageable`](file:///d:/UnityProject/SBGames/Assets/Scripts/Interface/IDamageable.cs), [`DamageData`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/DamageData.cs).
- **GOAP & Utility AI**: [`BaseAI`](file:///d:/UnityProject/SBGames/Assets/Scripts/GOAP/AI/BaseAI.cs), `GoapActionProvider`, `AgentBehaviour`, `AIDecisionThrottler`.
- **Perception & Factions**: [`VisionController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Characters/Controller/VisionController.cs) (SensorToolkit wrappers) and [`FactionMember`](file:///d:/UnityProject/SBGames/Assets/Scripts/Factions/FactionMember.cs) (`FactionId.Monster` vs `FactionId.Adventurer`).
- **2D Navigation**: `NavMeshPlus` (`NavMeshSurface` + `CollectSources2d`).

### Meta System Isolation
Unit scripts never throw `NullReferenceException` when running outside the core loop:
- [`CombatTestEnvironmentBootstrap`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatTestEnvironmentBootstrap.cs) executes before other components (`[DefaultExecutionOrder(-200)]`) to automatically supply or validate essential singletons:
  - `ObjectPoolManager.Instance` (required by skill casters and projectiles).
  - `GameServices.Instance` & `CharacterManager.Instance` (used for unit registration).
  - `GoapBehaviour` with `ReactiveControllerBehaviour` and archetype factories (`NormalAgentTypeFactory`, `MageAgentTypeFactory`, `MinionAgentTypeFactory`).
  - `NavMeshSurface` bake validation.
- All round-based events (`OnPhaseChanged`, `OnRoundStarted`) and meta singletons (`BenchManager`, `DungeonProgression`, `DungeonPlacementManager`) are bypassed.

---

## 2. Test Scene Setup (`Combat_Sandbox.unity`)

### Hierarchy Structure
Create a new scene or open `Assets/Scenes/Combat_Sandbox.unity` with the following GameObject hierarchy:

```
Combat_Sandbox
├── Main Camera (2D Orthographic, Size: 8, Position: 0, 0, -10)
├── Directional Light 2D / Global Light 2D
├── [TEST_HARNESS]
│   ├── CombatTestEnvironmentBootstrap
│   ├── BattleSimulationTestRunner
│   └── CombatTestHUD
├── [ARENA]
│   ├── NavMesh_Surface_2D (NavMeshSurface + CollectSources2d)
│   ├── ArenaBoundaries (Optional static BoxCollider2D walls)
│   └── Ground_Visual (SpriteRenderer or Tilemap)
└── SpawnedCombatUnits (Container managed by BattleSimulationTestRunner)
```

---

## 3. Layer, Tag & NavMesh Setup

### Tags & Layers
1. **Tags**:
   - `Monster`: Assigned to monster combatants.
   - `Adventurer`: Assigned to adventurer combatants.
   - `AI_Augment`: Tagged on the child GameObject containing `BaseAI`, `Sensor`, and `GoapActionProvider`.
2. **Layers**:
   - `Default` (0): Ground and environment.
   - `Character` (6): Combatants and hitboxes.
   - Static obstacles/walls can reside on `Default` or an obstacle layer.

### 2D NavMesh (NavMeshPlus)
1. Add a GameObject named `NavMesh_Surface_2D`.
2. Attach:
   - `NavMeshPlus.Components.NavMeshSurface`
   - `NavMeshPlus.Extensions.CollectSources2d`
3. In `NavMeshSurface`:
   - Set **Agent Type**: `Humanoid` (or default agent index `0`).
   - Set **Collect Objects**: `All`.
   - Set **Layer Mask**: `Everything` (or layers containing walkable areas & obstacles).
4. Click **Bake** in the inspector or use the ContextMenu on [`CombatTestEnvironmentBootstrap`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatTestEnvironmentBootstrap.cs) -> **"Bake 2D NavMesh"**.

---

## 4. Configuring the Roster

Select the `[TEST_HARNESS]` GameObject in the Inspector:

1. In [`BattleSimulationTestRunner`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs):
   - You can click any **Quick Roster Preset** button:
     - **1v1 Duel**: 1 Monster vs 1 Adventurer.
     - **3v3 Skirmish**: 3 Monsters vs 3 Adventurers.
     - **5v5 Brawl**: 5 Monsters vs 5 Adventurers.
   - Or configure individual `UnitSpawnSlot` entries in the **Roster** list:
     - **Unit Prefab**: Assign e.g. `MONSTER_N001.prefab`, `ADVENTURER_N002.prefab`.
     - **Spawn Anchor** / **Custom Spawn Position**: Where the unit will materialize.
     - **Faction**: Select `Monster` or `Adventurer`.
     - **Initial Level**: Level integer (1–100).
     - **Override Stats** (Optional): Enable to customize Max Life, Damage, Movespeed, Attack Range, or Attack Interval for balance experiments.

---

## 5. Running & Diagnosing Simulations

### Controls
- **Inspector / Context Menu**:
  - `Start Simulation`: Spawns units, assigns AI control, begins stopwatch.
  - `Restart / Rerun`: Instantly wipes the board and respawns the roster cleanly.
  - `Clear Board / Reset`: Cleans all active units, stops AI plans, and resets timers.
  - `Toggle / Cycle Speed`: Cycles between 1x, 2x, 5x, 10x.
- **In-Game Diagnostic HUD ([`CombatTestHUD`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatTestHUD.cs))**:
  - Live faction casualty scorecard.
  - Stopwatch and individual unit health bars.
  - Winner announcement banner with casualties breakdown.
  - On-screen speed and restart buttons.
- **Keyboard Shortcuts**:
  - `R`: Instant restart / rerun.
  - `Space`: Pause / resume simulation.
  - `1`, `2`, `5`, `0`: Set time scale to 1x, 2x, 5x, 10x.
  - `H`: Toggle HUD visibility.

---

## 6. Zero Memory Leak & Re-Run Guarantee

Consecutive simulation runs maintain zero memory leaks:
1. `ResetSimulation()` calls [`CharacterBrain.InterruptAllActions()`](file:///d:/UnityProject/SBGames/Assets/Scripts/Characters/CharacterBrain.cs#L381-L417), resetting casting and attack coroutines.
2. `BaseAI.InterruptCurrentPlan()` stops active GOAP actions and stops the NavMesh agent cleanly.
3. Tracked units and SensorToolkit buffers are cleared before game objects are destroyed.
4. Despawn automatically unregisters units from `CharacterManager`.
