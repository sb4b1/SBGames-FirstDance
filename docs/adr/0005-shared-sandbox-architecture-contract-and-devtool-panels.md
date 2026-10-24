# ADR 0005: Shared Sandbox Architecture Contract and DevTool Modular Panels

## Status
Accepted

## Context
Before assembling full game prototypes in SBGames (a 2D dungeon-defense auto-battler), four complex subsystems require extensive isolated balancing and iteration: Combat, Dungeon Generation, Planning/Meta GUI, and Adventurer Runs.

Previously, the Combat sandbox (`Combat_Sandbox.unity`) used a bespoke test harness (`BattleSimulationTestRunner.cs`, `CombatSandboxPassExecutor.cs`, and `CombatTestHUD.cs`). Re-implementing ad-hoc harnesses for the other sandboxes would duplicate bootstrap logic, reporting code, timeout safeguards, and GUI controls. Additionally, `DevTool.cs` (~2,000 lines) was monolithic, preventing easy reuse of runner and report inspection panels across sandbox scenes and the upcoming prototype.

## Decisions

1. **Shared Sandbox Architecture Contract**:
   All sandboxes follow a standardized contract (`docs/testing/sandbox_contract.md`) with a unified hierarchy (`[TEST_HARNESS]`), early-execution bootstrap base (`SandboxBootstrapBase` at `[DefaultExecutionOrder(-200)]`), runner base (`SandboxRunnerBase` / `ISandboxRunner`), pure C# result model (`SandboxResultModel.cs`), deterministic report writer (`SandboxReportWriter.cs`), and headless entry points (`SandboxHeadlessRunner.cs`).

2. **Scenario as ScriptableObject (`SandboxScenarioSO`)**:
   Scenarios are ScriptableObject assets containing an ID, display name, description, sandbox ID, and covered checklist item IDs. Specialized scenarios (e.g. `CombatScenarioSO`) inherit from `SandboxScenarioSO` to specify domain parameters (seeds, unit slots, layouts).

3. **Dual Execution Modes from the Same Scenarios**:
   The exact same Scenario assets are executed both interactively (via DevTool's Scenario Runner panel in PlayMode) and headlessly (via Unity CLI batch mode). Headless execution monitors a safety timeout (`SandboxSafetyTimeoutWatcher`) to flag hung simulations as failures.

4. **Modular DevTool Panels (`IDevToolPanel`)**:
   DevTool is refactored into a panel-based host without breaking existing features.
   - Panel 1: `ScenarioRunnerPanel` (Scenario picker, run, reset, 1x/2x/5x speed controls, pass/fail status).
   - Panel 4: `ReportViewerPanel` (Last run breakdown and Validation Badges / `ValidationService` findings).
   Panels 2, 3, and 5 are reserved for subsequent sandbox tasks.

5. **Formal Sign-off Workflow**:
   Sign-off sheets (`docs/testing/<sandbox>_signoff.md`) track mechanical criteria (ticked automatically by the runner report) and judged criteria (ticked manually by designers). Only issues blocking prototype integration are filed on the issue tracker.

## Consequences
- Every new sandbox can be stood up by subclassing `SandboxBootstrapBase`, `SandboxRunnerBase`, and `SandboxScenarioSO`.
- DevTool panels provide uniform UI in both sandbox scenes and the prototype.
- Combat sandbox is fully adapted with zero breaking changes to existing simulation math or results.
- Reports and telemetry CSVs in `docs/testing/` remain deterministic and clean in version control.
