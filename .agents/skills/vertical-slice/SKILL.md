---
name: vertical-slice
description: Evaluates feature complexity against SBGames architecture and breaks multi-layer tasks into testable, end-to-end Vertical Slices (Pure C# Core -> Unity Wrapper -> GOAP/Loop Integration -> Editor/UI) with CLI compile and EditMode test gates after each slice.
---

# Vertical Slice Planner & Executor (`/vertical-slice`)

When the user invokes `/vertical-slice <feature or task description>` (or asks to slice a feature), follow this workflow strictly.

## Step 1: Pre-Flight Architecture & Diagnostic Check
1. Inspect the relevant modules and read `docs/engineering_guide.md` and `GLOSSARY.md` if applicable.
2. Score the task against the **4 Vertical Slice Diagnostic Criteria**:
   - **Crosses Layers?** (Touches Pure C# `Core/`, `Unity/` MonoBehaviours, SOs, and Editor/UI Toolkit)
   - **Touches 3+ Subsystems?** (e.g., GOAP/Utility AI, `CharacterStatsManager`, `DungeonPlacementSystem`, `CoreGameLoop`, `SquadBlackboard`)
   - **High Integration / Timing Risk?** (Involves frame timings, NavMeshPlus, SensorToolkit, or state transitions)
   - **Playability / Simulation Dependent?** (Requires `BattleSimulationTestRunner` or EditMode verification to prove it works)

- **If 0–1 are YES:** Tell the user: *"This is a surgical/isolated task (~5–15 mins) and does not require vertical slicing."* Then offer to execute it directly in one pass.
- **If 2+ are YES:** Proceed to Step 2 and generate a **Vertical Slice Execution Plan**.

---

## Step 2: Design the Vertical Slices (3 to 4 Stages Max)
Break the feature down into small, playable/testable vertical milestones tailored to **SBGames** patterns (`docs/engineering_guide.md` §3–§5):

### Slice 1: The End-to-End Skeleton (Pure C# Core + Minimal Contract)
- **Goal:** Establish the narrowest working data path from input to output.
- **Scope:**
  - Follow **Pattern A**: Build the pure C# data structs, interfaces, and core logic (`*/Core/`) with **zero `UnityEngine` references** and deterministic `GameRng`.
  - Write fast **EditMode unit tests** in `Assets/Tests/` to prove the core math and state transitions work in isolation.
- **Verification Gate:** `unity recompile` + `unity test --mode EditMode --filter <NewTestName>`.

### Slice 2: Thin Unity Wrapper & Single-Entity Loop
- **Goal:** Connect the Pure C# Core to a single scene object, prefab, or AI agent without full meta-loop complexity.
- **Scope:**
  - Create immutable `ScriptableObject` configs/templates and thin `MonoBehaviour` wrappers (`*/Unity/`).
  - If Character/AI related (Pattern B & D): Wire a single `GOAP Goal` + `UtilityGoal` + `GoapActionBase` or `CharacterBrain` hook following `docs/goap_utility_context.md`.
- **Verification Gate:** `unity recompile` + EditMode component/integration test (or `Combat_Sandbox` verification).

### Slice 3: Cross-System Wiring & Edge Cases
- **Goal:** Connect the working slice to macro systems (`CoreGameLoop`, `DungeonRoomRegistry`, `SquadBlackboard`, `DungeonPlacementSystem`, `ObjectPoolManager`) and handle lifecycle edge cases (unit death mid-action, pool reset via `IPoolable`, unwalkable NavMesh, null targets).
- **Verification Gate:** `unity recompile` + run regression tests via `unity test --mode EditMode` to ensure zero broken contracts.

### Slice 4: Authoring Tools, UI Toolkit & Polish (If Needed)
- **Goal:** Expose inspector helpers, Editor windows (`#if UNITY_EDITOR`), gizmos, or UI Toolkit (`UXML`/`USS`) bindings.
- **Verification Gate:** `unity recompile` + zero console errors (`unity cmd console`).

---

## Step 3: Execution Protocol
1. **Present the Slice Plan First:** Output a concise table showing **Slice 1 to Slice N**, the exact files to create/modify in each slice, and the verification gate for each slice.
2. **Execute One Slice at a Time:**
   - Complete the current slice (code + unit tests).
   - Check `unity status` and run the verification gate (`unity recompile` and `unity test --mode EditMode --filter <TestName>`, falling back to batchmode only if no Editor is connected).
   - Report the status of the slice (Done, test results, and any manual Inspector/Prefab steps) before moving to the next slice.
