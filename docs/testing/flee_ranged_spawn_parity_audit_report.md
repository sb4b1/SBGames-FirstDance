# Combat Sandbox AI Audit: Flee Check, Ranged Units, Spawn Parity & Full Regression

**Document:** `docs/testing/flee_ranged_spawn_parity_audit_report.md`  
**Test Harness:** [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs)  
**Scene:** [`Assets/Scenes/Combat_Sandbox.unity`](file:///d:/UnityProject/SBGames/Assets/Scenes/Combat_Sandbox.unity)  
**Date:** October 17, 2026  
**Execution Environment:** Unity 6 (`6000.6.4f1`), Unity CLI Automation, Play Mode & Live Evaluation  

---

## 1. Executive Summary

This report documents the audit and verification results covering four critical combat behavior areas:
1. **Flee Behavior & Survival Metric:** Root cause analysis for 0 Tank flee events across earlier runs, metric validation, and verification of rank-based individual flee thresholds (E 40%, D 35%, C 30%) with personality modifiers.
2. **Ranged & Caster Units After Range-Check Cap:** Validation of melee reach clamping in `AttackAction` vs. ranged abilities in `CastSkillAction`, with specific analysis of `ADVENTURER_N099` (Mage) and `MONSTER_N003`.
3. **Spawn Parity:** Detailed step-by-step diff between test runner harness unit spawning and production dungeon placement / party spawning pipelines.
4. **Full Regression Matrix:** Full execution pass across all 9 benchmark scenarios on seeds `101` and `102` with 60-second timeouts, reporting win rates, durations, DPS, swing accuracy, damage taken by role, and error counts.

Across all 18 regression runs, **0 timeouts** occurred and the Unity console recorded **0 runtime errors/exceptions** (`consoleErrors: 0`).

---

## 2. Step 1: Flee Behavior & Threshold Verification

### 2.1 Investigation: Why Tank Flee Events Were 0
In all previous runs, `tankFleeEvents` was reported as `0`, even in heavy pressure scenarios where the Tank took over 120–160 damage (exceeding its 100 base max HP).

#### Root Cause Analysis
1. **Goal Competition from `UtilityRecoverGoal`:**
   * In [`UtilityRecoverGoal.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityRecoverGoal.cs#L55-L60), whenever health drops at or below the flee threshold (`hpFrac <= context.SurvivalHpThreshold`), the score enforces `surviveOverrideFloor = 1.02f` as long as usable items remain in the unit's kit.
   * In [`UtilitySurviveGoal.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilitySurviveGoal.cs#L81), the survival score is clamped via `Mathf.Clamp01(totalScore)` (maximum `1.0f`).
   * Because `1.02 > 1.0`, **`Recover` strictly beats `Survive`** while health potions or recovery items remain available.
2. **Action Locking & Burst Damage Timing:**
   * In [`AIDecisionThrottler.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/AIDecisionThrottler.cs#L97-L101), strategic re-evaluation is deferred when `IsActionLocked` is true (unit is in active attack swing or recovery backswing).
   * Once potions were depleted, incoming monster focus fire (30–45 monster DPS) killed the frontline Tank in under 1.5 seconds, before an uninhibited decision cycle could switch the active goal to `Survive`.
3. **Metric Accuracy:**
   * The metric in [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs#L920-L924) checks:
     ```csharp
     EnumUtilGoal currentGoal = adv.AI.CurrentGoalKey;
     if (currentGoal == EnumUtilGoal.Survive && adv.LastGoalKey != EnumUtilGoal.Survive)
     {
         adv.IncrementFleeEvents();
     }
     ```
   * The metric is functioning as designed; the Tank genuinely never transitioned to `Survive` during combat because recovery goal pre-emption and fast lethal focus fire prevented the goal switch.

### 2.2 Rank Flee Threshold Verification
The rank thresholds defined in `AdventurerAIProfile` were verified via runtime inspection:
```
- Rank E (Profile_Rank_E.asset): Base = 0.40 (40% HP)
    Personality adjustment range [0.85x, 1.15x] -> [34.0% HP, 46.0% HP]
- Rank D (Profile_Rank_D.asset): Base = 0.35 (35% HP)
    Personality adjustment range [0.85x, 1.15x] -> [29.75% HP, 40.25% HP]
- Rank C (Profile_Rank_C.asset): Base = 0.30 (30% HP)
    Personality adjustment range [0.85x, 1.15x] -> [25.50% HP, 34.50% HP]
```

When tested in a scenario where potions are depleted or removed and the unit takes non-lethal sustained damage dropping below its threshold, `UtilitySurviveGoal` successfully triggers and transitions the unit into [`FleeAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/FleeAction.cs).

---

## 3. Step 2: Ranged Units After Range-Check Change

### 3.1 Recent Range-Check Cap Context
In [`AttackAction.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/AttackAction.cs#L84-L95) and [`FocusFireAction.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/FocusFireAction.cs#L99-L109), `IsInRange` was capped to melee physical reach:
```csharp
float maxReachWithTarget = data.Controller.MaxEffectiveReach + targetExtent;
float effectiveAttackRange = Mathf.Min(standingDistance, maxReachWithTarget);
return distance <= effectiveAttackRange;
```
This was added to prevent melee units with inflated `AttackRange` stats from swinging in place outside the physical overlap radius of their `CharacterCombatController`.

### 3.2 Evaluation of Ranged & Caster Units

#### 1. Mage-type Adventurers (`ADVENTURER_N099`)
* **Finding:** Mage spells do **not** use `AttackAction` or `FocusFireAction`.
* Spells are executed through [`CastSkillAction.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/CastSkillAction.cs#L99-L113), which determines range via:
  ```csharp
  return distance <= skillController.GetBestReadySkillRange();
  ```
* For `ADVENTURER_N099`, [`SKILL_WI001`](file:///d:/UnityProject/SBGames/Assets/ScriptableObjects/Skill/Wind/SKILL_WI001.asset) has an effective casting range of **10.0m**.
* **Result:** Mage units retain full distance (10.0m) and cast from range without being restricted by the melee reach check.

#### 2. Monster `MONSTER_N003` (Ranged / Caster Profile)
* **Finding:** In `Assets/Prefabs/Character/Monster/Normal/MONSTER_N003.prefab`, `MONSTER_N003` has:
  * `characterClass = CharacterClass.None`
  * `skills.Count = 0` (no ranged skills equipped in prefab)
  * `stats.AttackRange = 1.0m`, `MaxEffectiveReach = 0.83m`
* Because `MONSTER_N003` does not currently have a ranged projectile skill attached, its only offensive action is basic attack via `AttackAction`, which requires moving into melee reach (`~0.83m + 0.25m = 1.08m`).
* **Recommendation:** When configuring `MONSTER_N003` as an active caster/ranged attacker in game balance passes, equip a projectile skill (such as `SKILL_001` or `SKILL_F001`) on its `CharacterSkillController`. `CastSkillAction` will then automatically maintain its intended standoff distance.

---

## 4. Step 3: Spawn Parity Audit

Comparison between test harness spawning ([`BattleSimulationTestRunner.SpawnUnit`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs#L730-L866)) and runtime placement ([`DungeonPlacementManager.TryPlaceUnit`](file:///d:/UnityProject/SBGames/Assets/Scripts/Placement/Unity/DungeonPlacementManager.cs#L330-L350)):

| Step / Setting | Test Runner (`BattleSimulationTestRunner`) | Runtime Placement / Dungeon Spawner | Classification & Action |
| :--- | :--- | :--- | :--- |
| **Prefab Instantiation** | `Instantiate(slot.unitPrefab, spawnPos, spawnRot, unitsContainer)` | `Instantiate(unitDef.Prefab, worldPos, Quaternion.identity, parent)` | Parity achieved |
| **NavMeshAgent Flags** | Forces `updateRotation = false`, `updateUpAxis = false` | Prefabs default to `true`/`true`, but [`BaseAI.Awake`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs#L247) overrides both to `false` | Parity achieved |
| **FactionMember Component** | Adds/ensures `FactionMember`, sets `FactionId.Monster` or `Adventurer` | `FactionMember` exists on prefabs but defaults to `Neutral` | **Small Gap:** Placement spawner must set `factionMember.Faction = FactionId.Monster` upon placement |
| **Tag Assignment** | Sets `unitGo.tag = "Monster"` or `"Adventurer"` | Relies on prefab tag setting | Parity dependent on asset serialization |
| **CharacterIdentifier** | Ensures component exists and calls `GenerateID()` if empty | Component serialized on prefab; `GenerateID()` only runs if triggered | Minor gap |
| **Facing / Rotation** | Runs `CharacterFlipRotator.ApplyFlip()` facing toward center | Spawns with fixed `Quaternion.identity` without orienting toward target/door | Minor gap |
| **Healer Skill Injection** | Dynamically instantiates `SKILL_HEAL_001.prefab` into Healer's `SkillList` | No dynamic skill injection; requires skills to be authored on prefab or equipped | Expected test-harness shim |
| **Squad / Party Registration** | Instantiates `PartyLeader`, attaches `SquadMember`, calls `RegisterMember()` | No runtime adventurer party spawner implemented in `DungeonPlacementManager` | **Large Architectural Gap:** A dedicated Adventurer Party Spawner must be implemented for dungeon ingress |

---

## 5. Step 4: Full Regression Simulation Table

All 9 scenarios were executed across fixed seeds **101** and **102** with the standard 60-second timeout limit in Unity Play Mode.

```
Total Runs: 18 | Timeouts: 0 | Console Errors: 0
```

| # | Scenario | Seed | Outcome | Dur | Adv DPS | Mon DPS | Swings (Hit/Att) | Accuracy | Dmg (T/D/H) | Skill Heal | Survivors (Adv / Mon) |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| 01 | **E-rank vs Small Group** | 101 | **AdventurersWin** | 9.2s | 32.4 | 18.5 | 28 / 29 | 96.6% | 20 / 100 / 50 | 35 HP | 3/3 Adv / 0 Mon |
| 02 | **E-rank vs Small Group** | 102 | **AdventurersWin** | 8.4s | 35.2 | 19.0 | 28 / 28 | 100.0% | 40 / 50 / 70 | 35 HP | 3/3 Adv / 0 Mon |
| 03 | **D-rank vs Small Group** | 101 | **AdventurersWin** | 8.4s | 35.1 | 10.7 | 24 / 25 | 96.0% | 50 / 20 / 20 | 45 HP | 3/3 Adv / 0 Mon |
| 04 | **D-rank vs Small Group** | 102 | **AdventurersWin** | 8.0s | 36.8 | 13.7 | 24 / 25 | 96.0% | 30 / 40 / 40 | 30 HP | 3/3 Adv / 0 Mon |
| 05 | **C-rank vs Small Group** | 101 | **AdventurersWin** | 8.1s | 36.5 | 14.8 | 25 / 26 | 96.2% | 40 / 40 / 40 | 45 HP | 3/3 Adv / 0 Mon |
| 06 | **C-rank vs Small Group** | 102 | **AdventurersWin** | 10.6s | 28.0 | 16.1 | 31 / 31 | 100.0% | 80 / 60 / 30 | 55 HP | 3/3 Adv / 0 Mon |
| 07 | **C-rank vs Large Group** | 101 | **MonstersWin** | 21.8s | 20.8 | 19.7 | 51 / 88 | 58.0% | 150 / 130 / 150 | 60 HP | 0/3 Adv / 4 Mon |
| 08 | **C-rank vs Large Group** | 102 | **AdventurersWin** | 15.0s | 39.5 | 21.3 | 50 / 54 | 92.6% | 110 / 150 / 60 | 95 HP | 2/3 Adv / 0 Mon |
| 09 | **Party vs Boss** | 101 | **AdventurersWin** | 11.9s | 33.5 | 16.8 | 33 / 33 | 100.0% | 60 / 70 / 70 | 60 HP | 3/3 Adv / 0 Mon |
| 10 | **Party vs Boss** | 102 | **AdventurersWin** | 11.9s | 33.6 | 16.8 | 33 / 33 | 100.0% | 60 / 100 / 40 | 0 HP | 3/3 Adv / 0 Mon |
| 11 | **E-rank vs Pressure Group** | 101 | **MonstersWin** | 13.3s | 43.9 | 33.9 | 50 / 51 | 98.0% | 130 / 122 / 198 | 100 HP | 0/3 Adv / 1 Mon |
| 12 | **E-rank vs Pressure Group** | 102 | **MonstersWin** | 13.8s | 34.9 | 32.9 | 56 / 56 | 100.0% | 122 / 134 / 198 | 93 HP | 0/3 Adv / 3 Mon |
| 13 | **D-rank vs Pressure Group** | 101 | **MonstersWin** | 12.0s | 35.4 | 37.9 | 39 / 39 | 100.0% | 142 / 126 / 186 | 93 HP | 0/3 Adv / 2 Mon |
| 14 | **D-rank vs Pressure Group** | 102 | **MonstersWin** | 14.8s | 46.6 | 30.2 | 54 / 54 | 100.0% | 126 / 122 / 198 | 93 HP | 0/3 Adv / 1 Mon |
| 15 | **C-rank vs Pressure Group** | 101 | **AdventurersWin** | 17.0s | 47.5 | 21.8 | 48 / 48 | 100.0% | 116 / 124 / 132 | 75 HP | 1/3 Adv / 0 Mon |
| 16 | **C-rank vs Pressure Group** | 102 | **MonstersWin** | 16.8s | 42.4 | 28.7 | 58 / 58 | 100.0% | 162 / 122 / 198 | 93 HP | 0/3 Adv / 1 Mon |
| 17 | **C-rank vs Healer-Danger** | 101 | **AdventurersWin** | 14.0s | 54.4 | 24.1 | 37 / 42 | 88.1% | 136 / 144 / 56 | 81 HP | 2/3 Adv / 0 Mon |
| 18 | **C-rank vs Healer-Danger** | 102 | **MonstersWin** | 10.5s | 42.9 | 43.7 | 31 / 32 | 96.9% | 138 / 138 / 182 | 68 HP | 0/3 Adv / 3 Mon |

---

## 6. Key Takeaways & Recommendations

1. **Flee vs. Recover Hierarchy:**
   * The `surviveOverrideFloor = 1.02f` in `UtilityRecoverGoal` functions as designed by preventing premature fleeing while potions remain in stock. If design requires panic-fleeing under lethal burst even with potions in inventory, `UtilitySurviveGoal`'s burst-damage urgency formula can be elevated or granted higher hysteresis.
2. **Ranged Capabilities:**
   * `CastSkillAction` is properly decoupled from melee reach limits and respects full skill ranges. Ranged monsters like `MONSTER_N003` only require a projectile skill asset equipped in their prefab to leverage their intended engagement distance.
3. **Spawn Pipeline Integration:**
   * Before integrating full dungeon combat runs, ensure the monster placement manager explicitly assigns `FactionId.Monster` to placed units and author the incoming Adventurer Party Spawner with `PartyLeader` / `SquadMember` bindings.
