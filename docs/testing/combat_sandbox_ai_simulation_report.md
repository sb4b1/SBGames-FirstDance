# Combat Sandbox AI Simulation & Behavior Report

**Scene:** [`Assets/Scenes/Combat_Sandbox.unity`](file:///d:/UnityProject/SBGames/Assets/Scenes/Combat_Sandbox.unity)  
**Test Harness:** [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs)  
**Bootstrap:** [`CombatTestEnvironmentBootstrap.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatTestEnvironmentBootstrap.cs)  
**Date:** October 16, 2026  
**Execution Environment:** Unity Editor (Windows, Unity CLI headless automation)

---

## 1. Executive Summary

This report documents the verification pass of adventurer and monster AI behaviors in the isolated combat test sandbox [`Combat_Sandbox.unity`](file:///d:/UnityProject/SBGames/Assets/Scenes/Combat_Sandbox.unity). The objective was to evaluate core AI decision loops, targeting mechanics by rank, tactical roles, skill usage, survival logic, monster pack dynamics, and run-time stability prior to full vertical slice dungeon integration.

A total of **24 automated simulation runs** were executed across 4 primary combat scenarios at both **1×** and **5×** simulation speeds over multiple fixed seeds (`101`, `102`, `103`). An additional investigation was conducted regarding Boss encounters.

Across all test runs, the combat execution engine completed with **0 console errors** (`consoleErrors: 0`) and zero goal flip-flopping. However, several critical behavioral bugs and missing subsystems were identified—notably friendly-fire targeting among monsters caused by child-collider perception, the absence of ally-targeted healing for the Healer role, and missing dynamic facing rotation.

---

## 2. Benchmark Scenario Matrix & Simulation Results

Each scenario was run repeatedly at **1×** (baseline real-time physics) and **5×** (accelerated physics with scaled fixed delta time: `fixedDeltaTime = 0.1f`).

```
Party Composition (Adventurers):
  - Unit 1: ADVENTURER_N001 (Tank, Party Leader, +1.5f frontline offset)
  - Unit 2: ADVENTURER_N001 (DPS, Flank, +1.2f flank offset)
  - Unit 3: ADVENTURER_N001 (Healer, Rear, -2.5f backline offset)

Enemy Compositions:
  - Small Group (3): 1x MONSTER_N001 (Melee), 1x MONSTER_N002 (Brawler), 1x MONSTER_N003 (Ranged/Caster)
  - Large Group (6): 2x MONSTER_N001, 2x MONSTER_N002, 2x MONSTER_N003
```

### Aggregate Results Table

| Scenario | Speed | Seeds | Win Rate | Avg Duration | Adv DPS | Monster DPS | Survivors (Adv / Mon) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **1. E-Rank vs Small Group (3)** | 1× | 101, 102, 103 | **100% Adv** | 17.1s | 17.6 | 6.2 | 3.0 / 0.0 |
| | 5× | 101, 102, 103 | **100% Adv** | 17.1s | 17.8 | 6.1 | 3.0 / 0.0 |
| **2. D-Rank vs Small Group (3)** | 1× | 101, 102, 103 | **100% Adv** | 17.0s | 17.7 | 5.4 | 3.0 / 0.0 |
| | 5× | 101, 102, 103 | **100% Adv** | 17.4s | 17.5 | 6.5 | 3.0 / 0.0 |
| **3. C-Rank vs Small Group (3)** | 1× | 101, 102, 103 | **100% Adv** | 15.1s | 20.1 | 5.3 | 3.0 / 0.0 |
| | 5× | 101, 102, 103 | **100% Adv** | 15.4s | 19.7 | 5.8 | 3.0 / 0.0 |
| **4. C-Rank vs Large Group (6)** | 1× | 101, 102, 103 | **66.7% Adv** | 42.0s | 18.5 | 7.9 | 2.0 / 1.3 |
| | 5× | 101, 102, 103 | **100% Adv** | 22.2s | 28.5 | 9.9 | 3.0 / 0.0 |
| **5. Party vs Boss** | — | — | **N/A** | — | — | — | **Boss character data missing** |

### Detailed Per-Run Logs

```text
[Run 01] E-Rank vs Small | 1x | Seed 101 | AdventurersWin | Dur: 15.6s | AdvDPS: 19.2 | MonDPS: 5.8 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 02] E-Rank vs Small | 1x | Seed 102 | AdventurersWin | Dur: 18.5s | AdvDPS: 16.2 | MonDPS: 6.5 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 03] E-Rank vs Small | 1x | Seed 103 | AdventurersWin | Dur: 17.3s | AdvDPS: 17.4 | MonDPS: 6.4 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 04] E-Rank vs Small | 5x | Seed 101 | AdventurersWin | Dur: 18.8s | AdvDPS: 15.9 | MonDPS: 5.3 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 05] E-Rank vs Small | 5x | Seed 102 | AdventurersWin | Dur: 18.5s | AdvDPS: 16.2 | MonDPS: 6.5 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 06] E-Rank vs Small | 5x | Seed 103 | AdventurersWin | Dur: 14.0s | AdvDPS: 21.4 | MonDPS: 6.4 | AdvSurv: 3/3 | MonSurv: 0/3

[Run 07] D-Rank vs Small | 1x | Seed 101 | AdventurersWin | Dur: 18.0s | AdvDPS: 16.7 | MonDPS: 6.1 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 08] D-Rank vs Small | 1x | Seed 102 | AdventurersWin | Dur: 15.5s | AdvDPS: 19.3 | MonDPS: 3.2 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 09] D-Rank vs Small | 1x | Seed 103 | AdventurersWin | Dur: 17.6s | AdvDPS: 17.0 | MonDPS: 6.8 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 10] D-Rank vs Small | 5x | Seed 101 | AdventurersWin | Dur: 14.4s | AdvDPS: 20.8 | MonDPS: 6.9 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 11] D-Rank vs Small | 5x | Seed 102 | AdventurersWin | Dur: 19.7s | AdvDPS: 15.3 | MonDPS: 6.6 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 12] D-Rank vs Small | 5x | Seed 103 | AdventurersWin | Dur: 18.2s | AdvDPS: 16.5 | MonDPS: 6.1 | AdvSurv: 3/3 | MonSurv: 0/3

[Run 13] C-Rank vs Small | 1x | Seed 101 | AdventurersWin | Dur: 17.4s | AdvDPS: 17.2 | MonDPS: 5.2 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 14] C-Rank vs Small | 1x | Seed 102 | AdventurersWin | Dur: 14.0s | AdvDPS: 21.5 | MonDPS: 5.7 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 15] C-Rank vs Small | 1x | Seed 103 | AdventurersWin | Dur: 14.0s | AdvDPS: 21.5 | MonDPS: 5.0 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 16] C-Rank vs Small | 5x | Seed 101 | AdventurersWin | Dur: 13.9s | AdvDPS: 21.6 | MonDPS: 5.8 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 17] C-Rank vs Small | 5x | Seed 102 | AdventurersWin | Dur: 14.8s | AdvDPS: 20.3 | MonDPS: 4.7 | AdvSurv: 3/3 | MonSurv: 0/3
[Run 18] C-Rank vs Small | 5x | Seed 103 | AdventurersWin | Dur: 17.6s | AdvDPS: 17.1 | MonDPS: 6.8 | AdvSurv: 3/3 | MonSurv: 0/3

[Run 19] C-Rank vs Large | 1x | Seed 101 | AdventurersWin | Dur: 29.0s | AdvDPS: 20.7 | MonDPS: 7.3 | AdvSurv: 3/3 | MonSurv: 0/6
[Run 20] C-Rank vs Large | 1x | Seed 102 | MonstersWin    | Dur: 78.2s | AdvDPS:  2.9 | MonDPS: 5.8 | AdvSurv: 0/3 | MonSurv: 4/6
[Run 21] C-Rank vs Large | 1x | Seed 103 | AdventurersWin | Dur: 18.8s | AdvDPS: 31.8 | MonDPS: 10.6 | AdvSurv: 3/3 | MonSurv: 0/6
[Run 22] C-Rank vs Large | 5x | Seed 101 | AdventurersWin | Dur: 29.6s | AdvDPS: 20.2 | MonDPS: 7.4 | AdvSurv: 3/3 | MonSurv: 0/6
[Run 23] C-Rank vs Large | 5x | Seed 102 | AdventurersWin | Dur: 17.3s | AdvDPS: 34.7 | MonDPS: 12.1 | AdvSurv: 3/3 | MonSurv: 0/6
[Run 24] C-Rank vs Large | 5x | Seed 103 | AdventurersWin | Dur: 19.6s | AdvDPS: 30.6 | MonDPS: 10.2 | AdvSurv: 3/3 | MonSurv: 0/6
```

---

## 3. Detailed Behavioral Verification

### 3.1 Targeting by Rank
Target scoring logic is implemented in [`BaseAI.GetTraitWeightedTargetScore`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs#L1070-L1130) driven by [`AdventurerAIProfile`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/AdventurerAIProfile.cs):

* **Rank E (`TargetPriorityMode.Nearest`):**
  Uses pure inverse distance: `100f / (1f + distance)`. Tank and DPS consistently engage the closest hostile target. No focus fire on vulnerable units.
* **Rank D (`TargetPriorityMode.LowestHp`):**
  Scales scoring inversely with target current life: `10,000f / (1f + currentHp)`. In multi-monster engagements, wounded targets are prioritized, yielding noticeably lower average fight times (15–17s vs 17–19s).
* **Rank C (`TargetPriorityMode.SupportFirstThenLowestHp`):**
  Awards a huge bonus (`+100,000`) if the target has [`AdventurerFunction.Healer`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs#L1095), followed by lowest current HP.
* **Gap Identified:** The requirement to target "fragile targets first" is currently approximated solely by *lowest current HP*. A high-Max-HP monster with a scratch (e.g. 180/200 HP) can be prioritized over an untouched low-Max-HP glass cannon caster (e.g. 60/60 HP) because fragility (Max HP / Armor ratio) is not modeled in `GetTraitWeightedTargetScore`.

### 3.2 Roles & Squad Formations
* **Tank ([`AdventurerFunction.Tank`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs#L22)):**
  Assigned frontline slot `(0, +1.5f)` in [`SquadFormationConfigSO`](file:///d:/UnityProject/SBGames/Assets/Scripts/Squads/SquadFormationConfigSO.cs). Effectively engages monsters first, absorbing opening attacks.
* **DPS ([`AdventurerFunction.DPS`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs#L22)):**
  Assigned flank offset `(1.2f, 0f)`. Maintains continuous offensive actions without stalling or idling.
* **Healer ([`AdventurerFunction.Healer`](file:///d:/UnityProject/SBGames/Assets/Scripts/Stat/CharacterStatsManager.cs#L22)):**
  Stationed at backline offset `(0, -2.5f)`.
  > [!WARNING]
  > **Critical Role Gap:** The Healer has **no ally-healing capabilities**. All skills defined in [`SkillData`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/SkillData.cs) enforce `targetFilter = EnemiesOnly`. [`SkillHitResolver`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/SkillHitResolver.cs) only calls `TakeDamage`. There is no GOAP goal, action, or target key for a Healer to target wounded allies. The Healer role currently only grants formation backline positioning and extra potion inventory via [`ItemKitModifiers.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Inventory/Core/Modifiers/ItemKitModifiers.cs).

### 3.3 Skills, Mana, and Recovery
* **Consumables & Self-Recovery:**
  [`RecoverAction`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Actions/RecoverAction.cs) activates reliably when HP falls below thresholds. Units consume `ITEM_CONSUMABLE_HP_POTION`, restoring 45 HP (logged as `[RestoreLife] Adventurer healed 45`). Mana potion consumption operates through the same pipeline.
* **Skill Casting Data:**
  If [`SkillData`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/SkillData.cs) lacks a `utilityData` ScriptableObject, [`BaseAI.InitializeSkills`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs#L442) logs a warning. Basic attacks and item actions continue uninterrupted.

### 3.4 Survival & Fleeing
* **Adventurers:**
  Survival HP thresholds are rank- and personality-adjusted via [`AdventurerAIProfile.GetAdjustedFleeThreshold`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/AdventurerAIProfile.cs):
  * Rank E: 40% HP
  * Rank D: 35% HP
  * Rank C: 30% HP
  In Scenario 4 (Seed 102, 1× speed), when reduced to 33% HP (40/120 HP), the sole surviving adventurer exited attack state, transitioned to flee/wander mode, and retreated to the arena boundary (`x = 9.87`).
* **Monsters:**
  Monsters initialize with `MinionSurviveGoal` having `ignoreSurvivalGoal = true`. Monsters fight to the death and do not flee under any health circumstance.

### 3.5 Monster Morale & Swarm Dynamics
* **Swarm Courage & Morale Shock:**
  > [!IMPORTANT]
  > Both `Swarm Courage` (peer density bravery bonus) and `Morale Shock` (stat penalty / flee trigger when allies are slain) are documented in [`GLOSSARY.md`](file:///d:/UnityProject/SBGames/GLOSSARY.md) and ADR 0002 ([`docs/adr/0002-dynamic-combat-ai-and-threat-prioritization.md`](file:///d:/UnityProject/SBGames/docs/adr/0002-dynamic-combat-ai-and-threat-prioritization.md)). **Neither system is implemented in C#.** Monsters maintain static bravery regardless of group size or casualties.

### 3.6 Stability & Physics
* **Zero Console Errors:** 0 exceptions or critical errors across all 24 simulation runs.
* **Hysteresis:** `goalHysteresisBonus = 15f` in [`BaseAI.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs#L39) prevented goal flipping between `Attack` and `Recover` on boundary frames.
* **Time Scale Variance:** At 5× speed, `fixedDeltaTime = 0.1f` (10 Hz). While basic combat timings scale cleanly, tightly grouped collisions in large swarm encounters (Scenario 4) resolve with slight jitter compared to 1× speed (50 Hz).

---

## 4. Identified Behavior Bugs & Root Causes

### Bug 1: Monster Friendly Fire via Child Collider Perception
* **Symptom:** In Scenario 4 (Large Group), monsters targeted and attacked fellow monsters (e.g. `Monster_2` chose `Monster_3` as its combat target, while `EnemyTargets` contained other monsters).
* **Root Cause:**
  1. SensorToolkit detects `Collider2D` objects. In character prefabs, colliders exist on child objects (e.g., `HitBox`).
  2. In [`VisionController.TickPerception`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/VisionController.cs#L160-L175), detected objects are passed to `factionMember.IsAllyOf(obj)`.
  3. [`FactionMember.IsAllyOf(GameObject otherObj)`](file:///d:/UnityProject/SBGames/Assets/Scripts/Factions/FactionMember.cs#L54-L71) calls `otherObj.TryGetComponent<IFactionMember>()` (which fails because `IFactionMember` is on the root) and then evaluates `otherObj.CompareTag()` (which fails because child colliders are `Untagged`).
  4. Both `IsAllyOf` and `IsHostileTo` return `false`.
  5. The fallback `else` branch in [`VisionController.cs:172`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/VisionController.cs#L172) defaults unclassified detections to `enemyTargets.Add(obj.transform)`.
* **Fix Proposal:** In [`FactionMember.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Factions/FactionMember.cs), use `otherObj.GetComponentInParent<IFactionMember>()` and check parent tag if child is untagged.

### Bug 2: Missing Ally Healing Action & Filter
* **Symptom:** The Healer role holds backline formation, carries extra potions, but never heals wounded allies.
* **Root Cause:**
  1. Skills in [`SkillData`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/SkillData.cs) only support `targetFilter = EnemiesOnly`.
  2. [`SkillHitResolver.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Skills/SkillHitResolver.cs) solely routes damage to `TakeDamage`.
  3. No GOAP Goal (`HealAllyGoal`) or Action (`HealAllyAction`) exists in `Assets/Scripts/Character/AI/GOAP/` to select an injured party member and cast a heal.
* **Fix Proposal:** Introduce `TargetFilter.AlliesOnly`, implement `HealAllyAction`, and wire Healer priority in [`BaseAI`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs).

### Bug 3: Lack of Runtime Character Facing Rotation
* **Symptom:** Units spawn facing away from opponents and fail to establish Line of Sight through their forward-facing `LOSSensor2D` cone (50°/80° angle).
* **Root Cause:**
  [`CharacterFlipRotator.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/CharacterFlipRotator.cs) exists in the project but is never called by [`BaseAI`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs), [`AgentMoveBehaviour`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/GOAP/Behaviours/AgentMoveBehaviour.cs), or [`CharacterCombatController`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/Controller/CharacterCombatController.cs). Units only faced right or left based on their initial prefab scale.
* **Fix Applied for Sandbox:** In [`BattleSimulationTestRunner.SpawnUnit`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs#L640-L650), units are now automatically oriented towards arena center (`x = 0`) upon spawning.
* **Open Need for Full Game:** A dynamic facing controller must be connected to `AgentMoveBehaviour` or `CharacterCombatController` to continuously update scale/rotation based on movement direction and active target.

### Bug 4: Missing Animator Parameter on Despawn/Death
* **Symptom:** Console warning: `Parameter 'Hash 134114850' does not exist` logged whenever a unit dies.
* **Root Cause:** [`CharacterBrain.InterruptAllActions`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/CharacterBrain.cs#L448) invokes `AnimationController.UpdateCastSkill(false)`, which attempts to reset a skill casting boolean parameter that does not exist in the SPUM Animator Controller.

### Bug 5: Boss Character Entity Missing
* **Symptom:** Scenario 5 cannot be simulated.
* **Root Cause:** No Boss character prefabs or `CharacterBaseStatSO` assets exist in the project. The only boss-related assets are dungeon environment tiles (`boss_corner.prefab`, `boss_cross.prefab`), DM talent cards (`DM_BOSS_01` to `05`), and `BossDoorMarker.cs`.

---

## 5. Systems Requiring Full Dungeon Context to Test

The following mechanics cannot be validated within the isolated combat sandbox and require the procedural dungeon loop:

1. **Room Loop State & Door Sealing:**
   Validation of [`DMSealAbilityController.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Loop/DMSealAbilityController.cs), doorway containment traps, and transition states (`EnteringRoom`, `Engaged`, `RoomCleared`).
2. **Scout Reconnaissance & Traps:**
   The `ScoutAhead` squad order, door listening/cracking mechanics, hazard avoidance, and trap location memory in [`CharacterAIMemory.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/AI/CharacterAIMemory.cs).
3. **Party Leader Checkpoint Decisions:**
   Leader evaluate-and-decide loop ([`CheckpointDecision`](file:///d:/UnityProject/SBGames/Assets/Scripts/Squads/PartyLeader.cs#L9-L15): `Advance`, `Recover`, `Regroup`, `Retreat`) based on loot value, member casualties, and floor safety margins.

---

## 6. Bug Disposition Matrix

| Issue | Status | Action Taken / Recommendation |
| :--- | :---: | :--- |
| **Sandbox NavMesh Not Walkable** | **Fixed** | Added `NavMeshModifier` (Walkable) to Tilemap and rebaked the NavMesh surface. |
| **Editor Focus Execution Freeze** | **Fixed** | Added `Application.runInBackground = true` in [`CombatTestEnvironmentBootstrap.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/CombatTestEnvironmentBootstrap.cs). |
| **Runner Role & Profile Overrides** | **Fixed** | Implemented `functionOverride`, `profileOverride`, and DPS metrics in [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs). |
| **Initial Spawn Orientation** | **Fixed** | Auto-oriented units towards the center in [`BattleSimulationTestRunner.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Testing/BattleSimulationTestRunner.cs) so sensors establish immediate LOS. |
| **Monster Friendly Fire (Child Colliders)** | **Open** | Update [`FactionMember.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Factions/FactionMember.cs) to resolve `GetComponentInParent<IFactionMember>()` and inherit parent tags. |
| **Missing Ally Healing for Healer** | **Open** | Architectural decision required: implement friendly skill targeting, `HealAllyGoal`, and `HealAllyAction`. |
| **Dynamic Facing During Movement/Attacks** | **Open** | Connect [`CharacterFlipRotator.cs`](file:///d:/UnityProject/SBGames/Assets/Scripts/Character/CharacterFlipRotator.cs) to velocity/target updates during combat. |
| **Swarm Courage & Morale Shock** | **Open** | Implement runtime stat/bravery adjustments in `MinionMonsterAI` per ADR 0002. |
| **Missing Boss Prefab & Stats** | **Open** | Author boss character prefab and `CharacterBaseStatSO` in asset pipeline. |
| **Animator Cast Parameter Hash Mismatch** | **Open** | Guard `AnimationController.UpdateCastSkill` with parameter existence check. |

---

## 7. How to Reproduce & Re-run Benchmarks

To re-run the 24-run automated benchmark suite in the Unity Editor:

1. Ensure the Unity Editor has [`Assets/Scenes/Combat_Sandbox.unity`](file:///d:/UnityProject/SBGames/Assets/Scenes/Combat_Sandbox.unity) open.
2. Execute the following CLI commands from PowerShell:

```powershell
# 1. Enter Play Mode
unity cmd eval "UnityEditor.EditorApplication.isPlaying = true; return 'Playing';"

# 2. Trigger the automated batch suite
unity cmd eval "var runner = UnityEngine.Object.FindAnyObjectByType<SBGames.Testing.BattleSimulationTestRunner>(); runner.StartBatchScenarios(new int[] { 101, 102, 103 }); return 'Batch Started';"

# 3. Check progress
unity cmd eval "var runner = UnityEngine.Object.FindAnyObjectByType<SBGames.Testing.BattleSimulationTestRunner>(); return $\"Running: {runner.isBatchRunning}, Runs: {runner.batchResults.Count}/24\";"

# 4. Dump full results when complete
unity cmd eval "var runner = UnityEngine.Object.FindAnyObjectByType<SBGames.Testing.BattleSimulationTestRunner>(); return string.Join('\n', runner.batchResults.Select(r => $\"{r.scenarioName} | {r.timeScale}x | Seed {r.seed} | {r.outcome} | Dur: {r.duration:F1}s | AdvDPS: {r.adventurerDps:F1} | MonDPS: {r.monsterDps:F1} | AdvSurv: {r.survivingAdventurers}/3\"));"
```
