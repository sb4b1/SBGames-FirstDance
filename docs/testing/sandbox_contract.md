# Sandbox Contract & Shared Testing Architecture

This document defines the architectural contract, standard layouts, and execution rules shared across all feature sandboxes in **SBGames**.

---

## 1. Architecture & Design Principles

Before assembling full gameplay prototypes, each game subsystem operates in an isolated, high-speed sandbox scene.

The shared sandbox framework ensures:
1. **Scene-Local Isolation**: Every sandbox boots only the singletons and mock environments it requires. Meta game singletons (e.g. `CoreGameLoop`, `DungeonMaster`, `EscapeMenu`, `BenchManager`) are automatically bypassed or purged.
2. **Dual-Mode Execution**: Interactive runs in the Unity Editor and headless batch execution via the Unity CLI run the **exact same `SandboxScenarioSO` assets** through the same runner.
3. **Pure C# Core**: Data models, scenario execution contracts, result reporting, and safety watchers remain in pure C# where possible.
4. **Deterministic Reports**: Pass reports (Markdown) and telemetry logs (CSV) are ordered deterministically by `scenarioId` and written to `docs/testing/` to diff cleanly in version control.
5. **Decoupled Tooling**: Diagnostics and scenario controls are hosted as modular `IDevToolPanel` components inside `DevTool`.

---

## 2. Standard Sandbox Scene Layout

Every sandbox scene (e.g. `Assets/Scenes/Combat_Sandbox.unity`, `Dungeon_Sandbox.unity`, `MetaGUI_Sandbox.unity`) follows a standardized GameObject hierarchy:

```
<Feature>_Sandbox.unity
├── Main Camera (2D Orthographic / Scene view)
├── Lighting / Environment
├── [TEST_HARNESS]
│   ├── <Feature>TestEnvironmentBootstrap (Derives from SandboxBootstrapBase)
│   ├── <Feature>SandboxRunner (Implements ISandboxRunner / SandboxRunnerBase)
│   ├── DevTool (Host for OnGUI HUD and runtime debugging)
│   ├── ScenarioRunnerPanel (DevTool Panel 1: Select, Run, Reset, 1x/2x/5x)
│   └── ReportViewerPanel (DevTool Panel 4: Pass/Fail results & Validation Badges)
├── [ARENA] / [SIMULATION_CONTAINER]
│   └── Domain-specific components (e.g. 2D NavMeshSurface, Grid Canvas, Layout containers)
└── SpawnedUnits / TemporaryEntities (Managed dynamically by the runner)
```

---

## 3. Bootstrap Rules

Each sandbox provides a dedicated bootstrap component derived from [`SandboxBootstrapBase`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxBootstrapBase.cs):
- **Early Execution Order**: Runs at `[DefaultExecutionOrder(-200)]` to initialize essential singletons and mock bindings before any other components run `Awake()`.
- **Meta Suppression**: Automatically scans for and destroys auto-instantiated meta loop or persistent game managers (`[CoreGameLoop]`, `[DungeonMaster]`, `[CombatResultsUI]`, etc.).
- **Minimal System Registration**: Registers only required subsystems into [`GameServices`](file:///d:/UnityProject/SBGames/Assets/Scripts/GameServices.cs) and scene managers.

---

## 4. Scenario Assets (`SandboxScenarioSO`)

Scenarios are ScriptableObject assets representing single or grouped test configurations:
- **`scenarioId`**: Unique programmatic identifier (e.g. `A1`, `B1`, `C1`, `ROOM_3X3`).
- **`displayName`**: Human-readable name shown in DevTool panels and reports.
- **`sandboxId`**: Programmatic ID of the owning sandbox (`Combat`, `DungeonGen`, `MetaGUI`, `AdventurerRun`).
- **`description`**: Summary of mechanics and edge cases verified.
- **`coveredChecklistItemIds`**: List of IDs from the sandbox sign-off checklist (e.g. `CHK-COMBAT-MELEE`, `CHK-COMBAT-TARGETING`).

Specialized sandboxes inherit from [`SandboxScenarioSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxScenarioSO.cs) (e.g. [`CombatScenarioSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/CombatScenarioSO.cs)) to define domain-specific parameters (seeds, unit slots, map templates).

---

## 5. Runner & Result Model

### `ISandboxRunner` Interface
Runners inherit from [`SandboxRunnerBase`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxRunnerBase.cs) and provide:
- `RunScenarioRoutine(scenario, onComplete)`: Coroutine executing a single scenario.
- `RunAllScenariosRoutine(onComplete)`: Coroutine executing the complete test suite.
- `ResetSandbox()`: Clears active entities, cancels AI coroutines, resets time scale.
- `TimeScale`: Dynamic control ($1\times$, $2\times$, $5\times$).
- `SafetyTimeoutSeconds`: Safety timeout to guard against infinite loops or hung simulations.

### Result Model & Safety Watcher
- [`ScenarioRunResult`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxResultModel.cs): Stores outcome (`Passed`, `Failed`, `Timeout`), duration, checklist statuses (`ChecklistItemStatus`), anomalies, and raw telemetry rows.
- [`SandboxSafetyTimeoutWatcher`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxSafetyTimeoutWatcher.cs): Monitors elapsed simulation/real time. If a run exceeds the timeout, the watcher halts execution and marks the run as `Timeout` (failed). **This is an execution safety net, not a game balance timer.**

---

## 6. Report Format & Locations

Reports are deterministically written to `docs/testing/`:
- **Markdown Report**: `docs/testing/<sandbox_id>_pass_report.md`
  - High-level execution metrics (total runs, pass/fail counts, total duration).
  - Deterministically sorted Scenario Results table.
  - Aggregated Checklist Items matrix with pass/fail status and notes.
- **Telemetry CSV**: `docs/testing/<sandbox_id>_pass_telemetry.csv`
  - Full per-unit or per-step numerical data sorted stably by scenario ID.

---

## 7. Headless Execution Command

Headless batch runs are executed via the Unity CLI static entry point:

```powershell
unity -quit -batchmode -projectPath "d:\UnityProject\SBGames" -executeMethod SBGames.Testing.Sandbox.SandboxHeadlessRunner.RunCombatSandboxHeadless
```

To run an arbitrary sandbox scene headlessly from script or CLI:
```csharp
SBGames.Testing.Sandbox.SandboxHeadlessRunner.RunSandboxHeadless("<SandboxId>", "Assets/Scenes/<SceneName>.unity");
```

The process exits with code `0` on complete success, or non-zero (`1`, `2`, `3`) upon scene load failure, missing runner, or scenario failures.

---

## 8. DevTool Panels

Sandbox diagnostics are integrated into [`DevTool`](file:///d:/UnityProject/SBGames/Assets/Scripts/Manager/DevTool.cs) through the [`IDevToolPanel`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/IDevToolPanel.cs) interface:
- **Panel 1: Scenario Runner (`ScenarioRunnerPanel`)**:
  - Displays sandbox status (`RUNNING` / `IDLE`).
  - Speed controls ($1\times$, $2\times$, $5\times$).
  - Scenario dropdown selection list.
  - "Run Selected", "Run All Scenarios", and "Reset" triggers.
  - Live summary of scenario outcomes.
- **Panel 4: Report Viewer (`ReportViewerPanel`)**:
  - Tab 1: Formatted breakdown of the most recent `SandboxPassReport` with per-scenario details and checklist tags.
  - Tab 2: Validation Badges and findings from `ValidationService` with one-click full project audit trigger.

---

## 9. How to Add a New Sandbox (Step-by-Step)

1. **Create Scene**: Create `Assets/Scenes/<Feature>_Sandbox.unity`.
2. **Define Scenario SO**:
   - Create `<Feature>ScenarioSO` inheriting from [`SandboxScenarioSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxScenarioSO.cs).
   - Add feature-specific configurations (rosters, piece sets, balance dials).
3. **Implement Bootstrap**:
   - Create `<Feature>TestEnvironmentBootstrap` inheriting from [`SandboxBootstrapBase`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxBootstrapBase.cs).
   - Register only required mock services into `GameServices`.
4. **Implement Runner**:
   - Create `<Feature>SandboxRunner` inheriting from [`SandboxRunnerBase`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxRunnerBase.cs).
   - Implement `RunScenarioRoutine`, `RunAllScenariosRoutine`, and `ResetSandbox`.
5. **Assemble `[TEST_HARNESS]`**:
   - Add `[TEST_HARNESS]` GameObject in the scene.
   - Attach `<Feature>TestEnvironmentBootstrap`, `<Feature>SandboxRunner`, `DevTool`, `ScenarioRunnerPanel`, and `ReportViewerPanel`.
6. **Create Sign-off Document**:
   - Copy `docs/testing/sandbox_signoff_template.md` to `docs/testing/<feature>_signoff.md`.
   - Fill in mechanical and judged checklist items.
7. **Add Headless Entry Point**:
   - Add a static entry method to [`SandboxHeadlessRunner`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/Sandbox/SandboxHeadlessRunner.cs) pointing to the scene path.
8. **Verify Verification Gates**:
   - Confirm EditMode tests pass.
   - Run headless command and verify report and telemetry files are written to `docs/testing/`.
