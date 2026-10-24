# Sandbox Sign-off: [Sandbox Name]

**Date:** YYYY-MM-DD  
**Scene:** `Assets/Scenes/[SandboxName]_Sandbox.unity`  
**Runner:** `[SandboxRunnerClass]`  
**Status:** [Pending / In Progress / Signed Off]  

---

## 1. Feature Checklist

Mechanical items are automatically validated and ticked from the runner's pass report (`docs/testing/[sandbox]_pass_report.md`). Judged items represent qualitative evaluation, feel, and pacing, and must be reviewed and ticked manually by the developer/designer.

| Item ID | Description | Type | Status | Verified In / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **`CHK-[FEATURE]-001`** | Core mechanic executes without exceptions | Mechanical | Pending | Validated by runner |
| **`CHK-[FEATURE]-002`** | Reset cleanly restores initial scene state | Mechanical | Pending | Zero memory leak / entity purge |
| **`CHK-[FEATURE]-003`** | Visual feedback and animation timing feels responsive | Judged | Pending | Evaluated via interactive HUD |
| **`CHK-[FEATURE]-004`** | Decision pacing and speed controls (1x/2x/5x) behave smoothly | Judged | Pending | Evaluated via interactive HUD |

---

## 2. Sign-off Rules

1. **Mechanical Gate**: All mechanical checklist items must show **Passed** in the latest runner report. Zero runtime exceptions permitted.
2. **Judged Gate**: All judged items must be inspected interactively at $1\times$ and $2\times$ speed and marked **Passed** by the reviewer.
3. **Known Gaps Rule**:
   - Only gaps that **block the prototype assembly or break core game loops** become GitHub Issues on the issue tracker.
   - Non-blocking tuning remarks, visual polish items, and deferred enhancements must remain documented under [Known Gaps](#3-known-gaps) below.

---

## 3. Known Gaps

| Gap ID | Description | Severity | Prototype Blocker? | Tracker Issue # / Disposition |
| :--- | :--- | :--- | :--- | :--- |
| `GAP-01` | *Example: Minor sprite jitter when turning* | Minor | No (Deferred) | Deferred to visual polish pass |
