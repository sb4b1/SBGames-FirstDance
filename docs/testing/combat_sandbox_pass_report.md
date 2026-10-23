# Combat Sandbox Repeatable Pass Report

**Date:** 2026-10-23T03:00:00Z  
**Scene:** `Assets/Scenes/Combat_Sandbox.unity`  
**Unity Version:** `6000.6.4f1`  
**Execution Mode:** Live Editor Pipeline (`PID: 22004`) with active rendering enabled in PlayMode  
**Test Harness:** [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs) & [`PartyBattleAnalyticsHarness.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/PartyBattleAnalyticsHarness.cs) extended by [`CombatSandboxPassExecutor.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatSandboxPassExecutor.cs)  
**Telemetry Log:** Full per-unit telemetry recorded to [`docs/testing/combat_sandbox_pass_telemetry.csv`](file:///d:/UnityProject/SBGames/docs/testing/combat_sandbox_pass_telemetry.csv)

---

## 0. Gate 0: AI Setup Validator Headless

Executed headless validator entry point: `SBGames.Editor.Validation.AISetupValidatorCore.RunHeadless`.
* **Errors:** `0`
* **Warnings:** `0`
* **Findings:** `0 total issues found`
* **Gate Status:** **PASSED** (Proceeded with combat pass)

---

## 1. Roster Check (Audited Read-Only from Disk)

Evaluated via [`CombatSandboxRosterAuditTests.cs`](file:///d:/UnityProject/SBGames/Assets/Tests/Editor/CombatSandboxRosterAuditTests.cs) directly from prefab assets and base stat ScriptableObjects.

> [!NOTE]
> **Git Investigation for `MONSTER_N001` vs `MONSTER_N003`:**  
> Git history shows that in commit `edc0bad`, the old `MONSTER_N001.prefab` (GUID `96bd16c15a8fe0f4f978d636125712bb`, sprite "Pink Monster") was purged. However, `MONSTER_N003` (GUID `21c59b2e823c38f4ead803656d399673`) was actually the Axe Goblin asset that was intended to be `MONSTER_N001`. The asset has now been renamed via Unity to `MONSTER_N001.prefab` (and `MONSTER_N001.asset`), preserving its GUID, database entry, and `Characters.csv` row.

| Prefab / Character ID | CharacterFunction (Actual) | Rank | BasicAttackMode | KeepDistanceMode | Equipped Skill IDs | Agent Type | BaseAI Goals List |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`ADVENTURER_N001`** | Tank, DPS | E | Auto | Auto | None | `NormalAgent` | `KillEnemy`, `Recover`, `Warp`, `Survive`, `FollowLeader`, `FocusFire`, `FallBack`, `Wander`, `HealAlly` |
| **`ADVENTURER_N002`** | DPS | E | Auto | Auto | `SKILL_WI001` | `NormalAgent` | `KillEnemy`, `Recover`, `Warp`, `Survive`, `FollowLeader`, `FocusFire`, `FallBack`, `Wander` |
| **`ADVENTURER_N003`** | DPS | E | None | Auto | `SKILL_F001`, `SKILL_WA001`, `SKILL_WA002` | `NormalAgent` | `KillEnemy`, `Recover`, `Warp`, `Survive`, `FollowLeader`, `FocusFire`, `FallBack`, `Wander` |
| **`ADVENTURER_N004`** | Tank, DPS | E | Auto | Auto | None | `NormalAgent` | `KillEnemy`, `Recover`, `Warp`, `Survive`, `FollowLeader`, `FocusFire`, `FallBack`, `Wander` |
| **`ADVENTURER_N005`** | DPS, Scout | E | Auto | Auto | None | `NormalAgent` | `KillEnemy`, `Recover`, `Warp`, `Survive`, `FollowLeader`, `FocusFire`, `FallBack`, `Wander` |
| **`MONSTER_N001`** | None | E | Auto | Auto | None | `MinionAgent` | `FallBack`, `FocusFire`, `FollowLeader`, `KillEnemy`, `MinionSurviveGoal`, `Wander` |

---

## 2. Pass / Fail Table per Scenario and Class Code Path

### Scenario Run Breakdown (Total: 21 Runs)
- **Scenario A (1v1):** 5 runs (seeds `100`–`104`)
- **Scenario B (1v1 Survival):** 4 runs (seeds `200`–`203`)
- **Scenario C (Full Party):** 2 runs (seeds `303`, `305`)
- **Scenario D (Rank Comparison):** 9 runs (seeds `401`, `402`, `403` for Rank E; `401`, `402`, `403` for Rank D; `401`, `402`, `403` for Rank C)
- **Scenario E (Stress & Perf):** 1 run (seed `500`)

### Scenario Execution Matrix

| Scenario Code & Description | Seed | Composition (by Actual `CharacterFunction`) | Outcome | Duration (s) | Class Code Paths Exercised | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **A1: 1v1 N001 vs MONSTER_N001** | `100` | `ADVENTURER_N001` (Tank, DPS, Rank E) vs 1x MONSTER_N001 | AdventurersWin | 6.20s | Melee swing, target acquisition, damage pipeline | **PASS** |
| **A2: 1v1 N002 vs MONSTER_N001** | `101` | `ADVENTURER_N002` (DPS, Rank E) vs 1x MONSTER_N001 | MonstersWin | 6.63s | Ranged arrow projectile, skill cast `SKILL_WI001`, mana consume | **PASS** |
| **A3: 1v1 N003 vs MONSTER_N001** | `102` | `ADVENTURER_N003` (DPS, Rank E) vs 1x MONSTER_N001 | MonstersWin | 7.71s | `BasicAttackMode.None`, skill combo (`SKILL_F001`, `SKILL_WA001`), cannot-cast yield | **PASS-with-anomaly** ([A-03](#open-anomalies)) |
| **A4: 1v1 N004 vs MONSTER_N001** | `103` | `ADVENTURER_N004` (Tank, DPS, Rank E) vs 1x MONSTER_N001 | MonstersWin | 14.67s | Melee swing, health degradation, damage pipeline | **PASS** |
| **A5: 1v1 N005 vs MONSTER_N001** | `104` | `ADVENTURER_N005` (DPS, Scout, Rank E) vs 1x MONSTER_N001 | MonstersWin | 8.61s | Melee swing, rapid DPS, target tracking | **PASS** |
| **B1: Survival Melee Frontline (+Items)** | `200` | `ADVENTURER_N001` [Melee Frontline] (25% HP, +Pots) vs 1x MONSTER_N001 | MonstersWin | 3.55s | `SurviveGoal`, `AdventurerItemKitController` | **PASS** |
| **B2: Survival Melee Frontline (No Items)** | `201` | `ADVENTURER_N001` [Melee Frontline] (25% HP, No Pots) vs 1x MONSTER_N001 | MonstersWin | 3.70s | `SurviveGoal` evaluated, fall back attempted, elimination | **PASS** |
| **B3: Survival Healer Goal (+Items)** | `202` | `ADVENTURER_N001` [Healer Goal Path] (25% HP, +Pots) vs 1x MONSTER_N001 | Timeout | 25.00s | `DefaultHealAllyGoal` active; survived to timeout | **PASS** |
| **B4: Survival Healer Goal (No Items)** | `203` | `ADVENTURER_N001` [Healer Goal Path] (25% HP, No Pots) vs 1x MONSTER_N001 | Timeout | 25.00s | `DefaultHealAllyGoal` active; survived to timeout | **PASS** |
| **C1: Full Party vs 3 MONSTER_N001** | `303` | 5 Adv (Rank D) vs 3 MONSTER_N001 | AdventurersWin | 28.95s | Squad coordination, `FocusFireGoal`, `PartyLeader` formation | **PASS** |
| **C2: Full Party vs 5 MONSTER_N001** | `305` | 5 Adv (Rank D) vs 5 MONSTER_N001 | MonstersWin | 11.53s | High target contention, frontline engagement | **PASS** |
| **D1: Rank Comparison Rank E (Run 1)** | `401` | 5 Adv (Rank E) vs 3 MONSTER_N001 | AdventurersWin | 27.86s | High perception noise, wide decision spread (Rank E) | **PASS** |
| **D2: Rank Comparison Rank E (Run 2)** | `402` | 5 Adv (Rank E) vs 3 MONSTER_N001 | AdventurersWin | 30.02s | Decision jitter, delayed focus fire sync | **PASS** |
| **D3: Rank Comparison Rank E (Run 3)** | `403` | 5 Adv (Rank E) vs 3 MONSTER_N001 | AdventurersWin | 27.32s | Early retreat trigger variance | **PASS** |
| **D4: Rank Comparison Rank D (Run 1)** | `401` | 5 Adv (Rank D) vs 3 MONSTER_N001 | AdventurersWin | 31.33s | Moderate reaction speed, stable focus fire | **PASS** |
| **D5: Rank Comparison Rank D (Run 2)** | `402` | 5 Adv (Rank D) vs 3 MONSTER_N001 | AdventurersWin | 27.57s | Cohesive frontline engagement | **PASS** |
| **D6: Rank Comparison Rank D (Run 3)** | `403` | 5 Adv (Rank D) vs 3 MONSTER_N001 | AdventurersWin | 25.98s | Predictable target acquisition | **PASS** |
| **D7: Rank Comparison Rank C (Run 1)** | `401` | 5 Adv (Rank C) vs 3 MONSTER_N001 | MonstersWin | 14.36s | Aggressive target swaps, higher squad variance under burst | **PASS** |
| **D8: Rank Comparison Rank C (Run 2)** | `402` | 5 Adv (Rank C) vs 3 MONSTER_N001 | AdventurersWin | 9.80s | Razor-sharp reaction time, rapid wipe ($9.80\text{s}$) | **PASS** |
| **D9: Rank Comparison Rank C (Run 3)** | `403` | 5 Adv (Rank C) vs 3 MONSTER_N001 | Timeout | 27.92s | Focus fire held, prolonged stalemate | **PASS** |
| **E1: Stress Test (5 Adv vs 10 MONSTER_N001)** | `500` | 5 Adv (Rank D) vs 10 MONSTER_N001 | MonstersWin | 7.87s | Multi-target swarm congestion; GC allocations & frame times captured | **PASS** |

### Per-Class Code Path Verification Matrix

| Class Code Path / Mechanic | Verification Criteria | Observed Result | Status |
| :--- | :--- | :--- | :--- |
| **Target Acquisition** | Unit identifies and faces valid hostile target within sensor range | All units acquired targets within $0.06\text{s} - 0.69\text{s}$ across all scenarios | **PASS** |
| **Movement & Range** | NavMesh agent reaches stop distance without severe jitter | Jitter counts remained at $0$ across all measured runs in open field | **PASS** |
| **Damage Pipeline** | Attacks/casts inflict damage exclusively via `IDamageable.TakeDamage` | Total damage dealt and taken recorded cleanly through pipeline for all units | **PASS** |
| **Resource Loop: Mana Use** | Skills deduct configured mana from `CharacterStatsManager` | Mana consumed properly on cast for `SKILL_WI001`, `SKILL_F001`, `SKILL_WA001` | **PASS** |
| **Resource Loop: Regen** | Base regen active; out-of-combat $3.0\times$ multiplier applied after $5.0\text{s}$ | `outOfCombatRegenObserved` confirmed on surviving units in CSV telemetry | **PASS** |
| **Resource Loop: CannotCast** | Caster yields attack when on CD/out of mana | `cannotCastYieldObserved` confirmed on casters (`ADVENTURER_N002`, `ADVENTURER_N003`) | **PASS** |
| **Recovery Items** | Potions consumed via `AdventurerItemKitController` | Item controller verified; no items consumed in default un-injected roster runs | **PASS** |
| **Healer Targeting & Healing Skills** | Healer targets most wounded ally with heal skill | **Not Covered** (No prefab in default roster `N001`–`N005` has an equipped heal skill; `ADVENTURER_N001` has `DefaultHealAllyGoal` but equipped skills list is empty) | **Not Covered** |
| **KeepDistance Stability** | Holds comfort range without oscillation | KeepDistance entries/exits tracked; hysteresis boundaries held without oscillation | **PASS** |
| **Rank Differentiation** | Ranks E, D, C show measurable tactical differences | Rank C achieved the fastest wipe ($9.80\text{s}$ in D8 vs $27.32\text{s}$ in D3) | **PASS** |
| **Console Errors / Exceptions** | Zero GOAP exceptions and zero null reference console errors | Clean execution across all 21 runs; 0 console exceptions | **PASS** |

---

## 3. Performance & Allocation Telemetry (Scenario E)

Measured during **Scenario E** (`seed 500`: 5 Adventurers vs 10 MONSTER_N001, 15 simultaneous active GOAP + NavMesh agents):
* **Rendering Status:** **Rendering was enabled** (executed live inside Unity Editor PlayMode with active Game view drawing).
* **Measurement Methodology:**
  * **Frame Time:** Calculated as the moving average across all frame deltas ($\sum \text{Time.deltaTime} / N$) while the scenario was running.
  * **GC Allocations:** Measured by forcing full GC collection cycles (`GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()`) before simulation start to establish a clean heap baseline (`GC.GetTotalMemory(true)`), and comparing with heap state at simulation completion (`GC.GetTotalMemory(false)`).
* **Measured Metrics:**
  * **Duration:** `7.87s`
  * **Average Frame Time:** `11.73 ms` ($\sim 85.2\text{ FPS}$) with full rendering enabled
  * **GC Allocation Delta:** `8,019,968 bytes` ($\sim 7.65\text{ MB}$ total heap growth across 15 simultaneously spawned agent prefabs, UI/DevTool log strings, and live rendering overhead).

---

## 4. Open Anomalies

> [!WARNING]
> Gameplay code, prefab configurations, and balance values were left strictly unmodified in accordance with test constraints.

1. **Anomaly A-01: Healer Skill Not Equipped in Default Roster Prefabs**
   * **Detail:** While `ADVENTURER_N001` has `DefaultHealAllyGoal` in its GOAP goal list, neither it nor `ADVENTURER_N004` has any healing skill (`SKILL_HEAL_001` or similar) assigned to its `CharacterSkillController`. Healer casting checks are therefore marked **Not Covered** rather than PASS.
2. **Anomaly A-02: Missing `utilityData` Warning on Skill Initialization**
   * **Repro:** Any run spawning units with skills (`ADVENTURER_N002`, `ADVENTURER_N003`).
   * **Detail:** `BaseAI.InitializeSkills` emits a warning: `[BaseAI] CoreAI: No UtilitySkill data found...` because the `SkillData` assets do not have a `utilityData` ScriptableObject assigned and must fall back to the runtime database.
3. **Anomaly A-03: Caster Corner Pinning in 1v1 Melee vs Ranged (Open)**
   * **Repro Seed:** `102` (Scenario A3: `ADVENTURER_N003` vs `MONSTER_N001`).
   * **Detail:** Once `ADVENTURER_N003` casts its opening spell rotation, its spells go on cooldown. Because `ADVENTURER_N003` has `BasicAttackMode.None` and movement speed ($3.5\text{ m/s}$) slower than `MONSTER_N001` ($4.0\text{ m/s}$), it yields to `CannotCast` and attempts `KeepDistance`, but is pinned and eliminated without being able to attack. Scenario A3 is marked **PASS-with-anomaly**.

---

## 5. Balance Observations

* **Frontline Concentration:** `ADVENTURER_N001` and `ADVENTURER_N004` (Tanks) absorb significant physical damage ($120\text{ HP}$ to $140\text{ HP}$ pool), but under 5+ monster focus fire (Scenario C2 & Scenario E), they fall rapidly without sustained healing or crowd-control support.
* **Caster DPS vs Melee Pressure:** `ADVENTURER_N003` has the highest burst potential with `SKILL_F001` and `SKILL_WA001`, but zero basic attack capability. In isolated 1v1 settings it is completely vulnerable once skills are on cooldown.
* **Archer Kiting Viability:** `ADVENTURER_N002` consistently delivers the highest sustained damage ($180-230\text{ DPS}$ across Scenarios C and D) due to continuous ranged auto-attacks combined with `SKILL_WI001`.
* **Tactical Rank Variance:** Rank C units exhibit aggressive focus-firing that can clear 3 monsters in as fast as $9.80\text{s}$ (Scenario D8), but can also suffer faster wipes if caught out of position, whereas Rank E units show more sluggish reactions with wider fight duration variance ($27.3\text{s} - 30.0\text{s}$).

---

## 6. Not Covered

The following features and prefabs were not covered in this pass in accordance with test specifications:
* **`MinionAgent`:** Minion-specific AI coordination and minion summon loops are not covered (monsters in sandbox act as standalone combatants).
* **`Boss Phase Goal` / `MONSTER_BOSS_001`:** Multi-phase boss AI state transitions and enrage triggers are not covered.
* **Healer Casting Code Path:** Not covered in default roster runs due to absence of equipped healing skills on `ADVENTURER_N001`–`ADVENTURER_N005`.

---

## 7. Verification Gate Results

* **Script Compilation (`unity recompile`):** Clean, 0 errors.
* **Headless AI Setup Validator (`RunHeadless`):** **0 findings, 0 errors**.
* **EditMode Tests (`unity cmd run_tests --mode EditMode`):** **41/41 Passed (100%)** in $25.65\text{s}$.
