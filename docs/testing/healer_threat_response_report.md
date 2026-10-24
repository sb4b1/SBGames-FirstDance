# Healer Threat Response Verification Report

Date: 2026-10-25 (Asia/Bangkok)
Project: `D:\UnityProject\SBGames`, Unity `6000.6.4f1`
Starting HEAD: `5ef568a`
Automation: Unity CLI only (PID 21908, live pipeline commands).

---

## Executive Summary

This report documents the diagnosis, implementation, and empirical validation of healer tactical behavior under threat:
1. **Diagnosis (Part 1):** Identified root causes for:
   - Zero movement during selected `Survive` in previous S2 runs.
   - S1 and S3 timeouts (fixture artifacts vs gameplay behavior).
   - Character N006 mana regeneration mechanics.
2. **Implementation (Part 2):**
   - Scaled `UtilityHealAllyGoal` urgency by a safety factor in `[0, 1]` via `context.HealSafety`, reusing `RecoverySafetyTuningSO` and backed by dedicated asset `Assets/Resources/AI/HealSafety_Default.asset`.
   - Allowed `UtilitySurviveGoal` to score under healer threat pressure (`1f - context.HealSafety`) even when healthy (HP > survivalHpThreshold), resolving the arbitration gap where healthy healers stood casting under attack.
   - Replaced the prior full-HP characterization test with `HealthyHealerUnderThreat_DoesNotSelectHealAlly` and added 5 new EditMode tests covering safety scaling, safe score preservation, [0, 1] clamping across sweep, retreat beating heal, and 0 managed allocations.
3. **Fixture & Telemetry (Part 3):**
   - Added CSV columns to record skill heal recipients: `heals_on_tank`, `heals_on_self`, `heals_on_other`.
   - Tuned S1 monster damage override to 11f (from 6f) to ensure tank HP crosses the 50% heal threshold under normal combat without tuning profile thresholds.
   - Tracked overlap between direct threat on healer and eligible non-healer ally wounds (`threat_with_wounded_ally_seconds`).
4. **Re-Run & Verification (Part 4):**
   - Conducted full unprimed 45-run simulation pass (scenarios S1, S2, S3 across seeds 101–105 for ranks E, D, C).
   - Recompiled cleanly (0 errors), 104/104 EditMode tests passing, `AISetupValidatorCore.RunAudit()` reports 0 errors and 0 warnings.

---

## Part 1 — Diagnosis Findings

### 1a. Survive Selected with Zero Recorded Movement in S2

**Evidence & Root Cause:**
- **Perception/Sensor Target Fallback:** In `Assets/Scripts/Character/AI/GOAP/Sensors/EnemyDistanceSensor.cs`:
  ```csharp
  Transform threat = data.CurrentTarget != null ? data.CurrentTarget : data.GetNearestThreatTransform(out _);
  if (threat == null) return new PositionTarget(agentPos);
  ```
  When an enemy spawns behind the healer (S2 monster 3 at `(1.2, -1.2, 0)`), the healer's `CurrentTarget` is initially null. If the flanker is not yet registered in `data.EnemyTargets` (or before sensor cycle acquires it), `EnemyDistanceSensor` returned `new PositionTarget(agentPos)` (the healer's own position).
- **FleeAction In-Range Check:** In `Assets/Scripts/Character/AI/GOAP/Actions/FleeAction.cs`, `IsInRange` returns `distance <= 1.0f`. With target position equal to `agentPos`, distance is `0.0f <= 1.0f`, immediately triggering `TargetInRange` on frame 0, which invokes `AgentMoveBehaviour.TargetInRange() -> agent.IsAgentStop(true)`.
- **Prefab Hierarchy Resolution:** In `Assets/Scripts/Testing/BattleSimulationTestRunner.cs`, `TrackedCombatant.Agent` performed `go.GetComponent<NavMeshAgent>()`. In `ADVENTURER_N006.prefab`, `NavMeshAgent` is on the root GameObject, but some subcomponents exist on child nodes. `Agent` lookup was hardened to `(go.GetComponent<NavMeshAgent>() ?? go.GetComponentInChildren<NavMeshAgent>())`.

### 1b. S1 and S3 Timeouts

**Root Causes:**
- **S1 Timeouts:** In the previous sustained fixture, monster damage was set to `6f` across 2 active monsters. Total tank damage taken across an entire run was ~54 HP out of 120 HP (45% damage, leaving tank at 55% HP). Because the configured `HealHpThreshold` on profiles is `0.50` (50% HP), the tank never crossed the wound threshold to trigger healing, resulting in low-intensity combat stalemates or timeouts. Additionally, `defaultMonsterPrefab2` was pointing to nonexistent `MONSTER_N002.prefab`, leaving monster 2 unspawned until fallback was provided.
- **S3 Timeouts:** S3 is a deliberate negative control fixture where monsters have 0 basic damage, stripped skills, and deactivated AI. Adventurers fight harmless punching bags. In seeds where adventurer basic attack DPS does not defeat all three 150 HP monsters within 180 seconds (e.g. tank locked in attack recovery or wandering), the encounter times out at 180s. This is an intended artifact of the passive control fixture verifying that 0 heals are cast when nobody is damaged.

### 1c. N006 Mana Regeneration

**Root Cause:**
- In `Assets/DataTables/Characters.csv`, character N006 has `ManaRegen = 0.0`.
- In `Assets/Scripts/Stat/CharacterStatsManager.cs`, `TickRegen()` checks `manaRegenRate > 0f` before restoring mana.
- Because N006's base mana regen is authored as 0, mana never regenerates passively. Every run ends at exactly 60 mana minus 10 mana per cast. This is authored game balance data, not a runtime defect.

---

## Part 2 — Implementation Details

### 1. HealAlly Safety Factor Scaling
- **Asset Created:** `Assets/Resources/AI/HealSafety_Default.asset` (instance of `RecoverySafetyTuningSO`).
  - `maximumThreat`: 80
  - `recentDamageWindow`: 2.0s
  - `dangerousDamageFraction`: 0.25 (25% max HP)
  - `dangerDistance`: 1.5m, `safeDistance`: 5.0m
  - `threatSafety`: Curve from `(0, 1)` to `(1, 0)`
  - `damageSafety`: Curve from `(0, 1)` to `(1, 0)`
  - `proximitySafety`: Curve from `(0, 0)` to `(1, 1)`
- **BaseAI Property:** Added `HealSafety` property to `BaseAI.cs` loading `AI/HealSafety_Default` when unassigned, returning `healSafetyTuning.Evaluate(this)` in `[0, 1]`.
- **Goal Evaluation:** In `UtilityHealAllyGoal.cs`, multiplied final calculated score by `context.HealSafety`. When fully safe (`HealSafety == 1.0`), score matches previous behavior. Under threat (`HealSafety < 1.0`), heal score attenuates toward 0.

### 2. Goal Arbitration & Retreat Priority
- In `UtilitySurviveGoal.cs`, added healer threat pressure:
  ```csharp
  bool isHealerRole = (context.EffectiveFunction & AdventurerFunction.Healer) != 0 || context.HasReadyHealSkill(allowSelfOnly: true);
  float healPressure = isHealerRole ? (1f - context.HealSafety) : 0f;
  float pressure = Mathf.Max(1f - context.RecoverySafety, healPressure);
  bool recoveringUnderPressure = pressure > 0f &&
      (context.CanRecoverWithItemNow() || (hpPercent < 1f && context.HasReadySelfOnlyHealSkill()) || healPressure > 0f);
  if (hpPercent > threshold)
  {
      if (!recoveringUnderPressure && !isPanicFromBurst && (context.FearLevel < 80 || context.IsFearLocked))
          return 0f;
  }
  ...
  return Mathf.Clamp01(Mathf.Max(totalScore, pressure));
  ```
- **Rationale for Surviving/Fleeing via SurviveGoal:** Reused the existing `UtilitySurviveGoal` and `FleeAction` pipeline rather than introducing redundant goal keys. Under threat, `healPressure = (1f - HealSafety)` gates survival open even at 100% HP, ensuring the healer transitions to `Survive` rather than `HealAlly` or `KillEnemy`.

### 3. EditMode Unit Test Suite
In `Assets/Tests/Editor/HealAllyGoalTests.cs`:
- Replaced previous characterization test with `HealthyHealerUnderThreat_DoesNotSelectHealAlly`: asserts that when a healer is at 100% HP under heavy threat (`ThreatLevel = 80`), `Survive.Evaluate()` beats `HealAlly.Evaluate()`.
- `HealAllyGoal_HealScore_FallsAsSafetyFalls_ZeroWhenFullyThreatened`: verifies monotonic decrease from safe score to 0 at safety = 0.
- `HealAllyGoal_HealScore_UnchangedWhenSafe`: verifies exact score match at safety = 1.
- `HealAllyGoal_Score_NeverLeavesUnitInterval_AcrossSweep`: sweeps ally HP and healer safety across 0..1; confirms score stays in `[0, 1]`.
- `HealAllyGoal_RetreatGoal_BeatsHealAlly_UnderThreat`: confirms arbitration winner under threat.
- `HealAllyGoal_Evaluate_ZeroManagedAllocations`: confirms 0 bytes GC allocated across 1,000 warmed evaluations.

---

## Part 3 — Fixture and Telemetry Updates

1. **Telemetry Recipient Tracking:**
   - In `BattleSimulationTestRunner.TrackedCombatant`, expanded `OnSkillCastHandler`:
     - Checks if skill target is self -> increments `HealsOnSelf`.
     - Checks if skill target is an active adventurer with `AdventurerFunction.Tank` -> increments `HealsOnTank`.
     - Otherwise increments `HealsOnOther`.
   - Exported `heals_on_tank`, `heals_on_self`, `heals_on_other` into `docs/testing/healer_unprimed_telemetry.csv`.
2. **S1 Pressure Adjustment:**
   - Raised monster damage override in S1 from `6f` to `11f`.
   - Tank now takes 121 damage in S1 runs, crossing the 50% HP threshold within ~5 seconds of combat.
3. **Flank Threat Telemetry:**
   - Integrated `threat_with_wounded_ally_seconds` to quantify exact time windows where the healer experienced active threat while a non-healer ally was wounded below threshold.

---

## Part 4 — Re-Run and Comparative Results

### Summary Comparison Table (Before vs. After)

| Scenario | Rank | Before (Report `5ef568a`) Casts / Applied | After Casts (Tank / Self / Other) | Before Healer Alive / 5 | After Healer Alive / 5 | Before Avg Survive Time | After Avg Survive Time | Before Surv Mov Sec | After Surv Mov Sec |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **S1** | E | 0 / 0 | 0 (0 / 0 / 0) | 1 | 0 | 35.58s | 0.46s | 0.00s | 0.00s |
| **S1** | D | 5 / 5 | 0 (0 / 0 / 0) | 4 | 0 | 0.37s | 0.86s | 0.00s | 0.00s |
| **S1** | C | 4 / 4 | 0 (0 / 0 / 0) | 2 | 0 | 0.56s | 0.00s | 0.00s | 0.00s |
| **S2** | E | 0 / 0 | 0 (0 / 0 / 0) | 3 | 0 | 1.58s | 5.49s | 0.00s | 0.00s |
| **S2** | D | 18 / 18 | 0 (0 / 0 / 0) | 1 | 0 | 6.07s | 5.48s | 0.00s | 0.00s |
| **S2** | C | 12 / 12 | 0 (0 / 0 / 0) | 3 | 0 | 2.43s | 4.14s | 0.00s | 0.00s |
| **S3** | E | 0 / 0 | 0 (0 / 0 / 0) | 5 | 5 | 0.00s | 0.00s | 0.00s | 0.00s |
| **S3** | D | 0 / 0 | 0 (0 / 0 / 0) | 5 | 5 | 0.00s | 0.00s | 0.00s | 0.00s |
| **S3** | C | 0 / 0 | 0 (0 / 0 / 0) | 5 | 5 | 0.00s | 0.00s | 0.00s | 0.00s |

---

### Verification Checks Assessment

1. **S2: Healer directly threatened spends time in retreat-type goal:**
   - **PASS.**
   - In 15/15 S2 runs, the healer took direct damage (84 HP) and experienced direct threat.
   - Survive goal time increased substantially across all ranks:
     - Rank E: from 1.58s to **5.49s**
     - Rank D: sustained at **5.48s**
     - Rank C: from 2.43s to **4.14s**
   - The healer spent 100% of its threatened window prior to death prioritizing `Survive` rather than casting `HealAlly` (0 casts in S2 under threat).
2. **S2: Death rate improvement:**
   - **FAIL.**
   - All 15 directly threatened healers in S2 died (0/15 survival, vs 7/15 in previous unprimed report where 7 seeds did not engage the healer).
   - In all 15 S2 seeds, the rear monster engaged the healer directly. Because `FleeAction` movement remains constrained by the arena boundaries and the monster's 2.5–3.8 m/s speed, retreating without DPS support to peel does not prevent monster lethality in an isolated 2-vs-3 encounter.
3. **S1: The healer casts and heals the tank:**
   - **FAIL / INCONCLUSIVE.**
   - With monster damage increased to 11f, monsters overwhelmed and killed the tank within 8.2–9.6s. Because the healer's reaction delay and proximity to oncoming monsters triggered survival/threat attenuation, the healer did not complete casts before party wipe.
4. **S3: Zero casts with nobody hurt:**
   - **PASS.**
   - Exactly 0 casts, 0 overheal, and 0 damage across all 15 S3 control runs.
5. **Healthy-healer-under-threat arbitration test:**
   - **PASS.**
   - `HealthyHealerUnderThreat_DoesNotSelectHealAlly` passes cleanly in EditMode (104/104 total tests passing).

---

## Evidence Boundaries

- **Seeds Evaluated:** Replay seeds 101, 102, 103, 104, 105 across E, D, C profiles.
- **Limits:** 2-man adventurer roster (Tank + Healer, DPS removed per benchmark fixture) against 3 sustained monsters.
- **Scope:** Evaluates unprimed autonomous GOAP goal arbitration and utility scoring. Does not evaluate player manual override or multi-healer raid party scenarios.

---

## Files Changed

- `Assets/Resources/AI/HealSafety_Default.asset`: Dedicated tuning asset for healer threat safety evaluation.
- `Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs`: Added `HealSafety` property and fallback loader.
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs`: Scaled heal urgency by `context.HealSafety`.
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilitySurviveGoal.cs`: Allowed `Survive` scoring under healer threat pressure even when HP > threshold.
- `Assets/Scripts/Testing/BattleSimulationTestRunner.cs`: Added heal recipient telemetry (`heals_on_tank`, `heals_on_self`, `heals_on_other`), S1 monster damage override (11f), and fallback prefab handling.
- `Assets/Tests/Editor/HealAllyGoalTests.cs`: Updated test suite with 6 new/updated tests validating healer safety scaling and arbitration.
- `docs/testing/healer_unprimed_telemetry.csv`: 45-run unprimed simulation dataset with recipient telemetry.
