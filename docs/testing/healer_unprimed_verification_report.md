# Unprimed healer verification

Date: 2026-10-24 (Asia/Bangkok). Project: `D:/UnityProject/SBGames`, Unity `6000.6.4f1`, starting HEAD `5ef568a`. All Editor automation uses the Unity CLI. No commit was created. The pre-existing `docs/project_status_snapshot.md` edit is outside this change.

## Goal audit and corrections

`Assets/ScriptableObjects/UtilityGoal/Default/DefaultHealAllyGoal.asset` already exists and uses `UtilityHealAllyGoal`. Its original, unchanged urgency curve has keys `(0, 0.55)` and `(1, 0.90)`, linear interior tangents approximately `0.35`, zero exterior tangents, and base weight `1`. Its curve was not changed. A serialized fallbackProfile reference was added to the existing D profile, preserving the previous no-profile threshold `0.5` and delay `0.25` without literal fallback settings in the scorer. No profile, skill, character-stat, or balance number was tuned. See [fallback configuration](healer_unprimed_evidence/goal_fallback_configuration.json).

The scorer now checks each cooldown-ready, affordable, positive-healing ally skill independently, skips `SelfTarget.Only`, and finds the lowest eligible wounded HP using cached BaseAI perception/squad lists. `Skill.IsReady` only checks cooldown: the previous extra five-mana constant did not establish affordability. The scorer now compares actual `SkillStatsManager.ManaCost` against cached character mana. An Excluded skill cannot create urgency for self alone; an Allowed skill can. No physics query was added.

The existing wounded-ally query gains an optional urgency-only scan that ignores tank preference; its normal target-selection callers still use the existing profile priority. This separates lowest-HP urgency from the profile's preference for which ally to heal. Self filtering now compares against the character root owning CharacterStatsManager, rather than the AI_Augment child GameObject. This matters for real prefab hierarchies.

Scores remain clamped to `[0, 1]`; the inspector weight range is also bounded. The scorer's separate hardcoded 50%-own-HP attenuation was removed to satisfy the no-hardcoded-threshold requirement. Its existing profile survival threshold gate remains. Reaction delay and wounded-HP thresholds come from the existing adventurer profile mechanism (or the configured fallback profile when no agent profile exists); no new rank branching was added. ScriptableObjects retain immutable runtime configuration; per-agent reaction time remains on BaseAI.

`ValidationService.ValidateHealerGoal` reports an Error for a positive-healing equipped skill with no UtilityHealAllyGoal on BaseAI. The shared audit calls it for prefab-equipped skills and catalog defaultSkillIds, so AI Setup Validator and Database/Stats use the same finding. The requested structural rule includes self-only healers, although their casts continue through Recover. N006 is the only heal-equipped character prefab found in the current character-prefab scan and has the default asset assigned. See [prefab audit](healer_unprimed_evidence/healer_prefabs.json) and [catalog/profile audit](healer_unprimed_evidence/catalog_and_profiles.json). The live E/D/C assets all currently resolve HealHpThreshold to `0.5`; reaction delays are `0.8 / 0.25 / 0.05` seconds, and C resolves TankFirstThenLowestHp. This records their actual configuration rather than assuming the rank-switch fallback thresholds apply.

## Targeted test coverage

[Goal test evidence](healer_unprimed_evidence/goal_tests.json) contains 14 passing cases:

| Test surface | Evidence |
|---|---|
| No wounded ally | Zero score |
| Missing agent profile | Uses configured existing profile tuning; missing both profiles yields zero |
| Cooldown and insufficient mana | Two independent zero-score cases |
| Excluded self / Only skill / Allowed skill | Correct goal eligibility; Only bypasses HealAlly |
| AI_Augment child hierarchy | Excluded recognizes the actual owning character root |
| Multiple ready skills | A later Allowed skill can qualify even when an earlier Excluded skill cannot |
| Health sweep | Monotonic normalized urgency through 99 living-health states; dead ally scores zero |
| Tank preference versus urgency | Profile still selects the tank while urgency follows the lower-HP eligible ally |
| Allocations | Zero managed bytes across 1,000 warmed positive Evaluate calls |
| Goal removal | Shared validator emits a character Error after removing the goal |
| Destroyed combatant before heal recipient | Event attribution skips destroyed GameObjects and records actual/overheal correctly |
| Heavy threat, low versus full own HP | Critical-HP Survive wins; the full-HP safety gap is characterized separately below |

Tests use owned ScriptableObject clones for controlled conditions; no asset templates are mutated in play. A no-delay profile fixture isolates scoring from the clock; real rank delays are exercised by the multi-seed battles.

## Runner and fixture

The pass extends `BattleSimulationTestRunner`; it does not introduce a separate combat harness. Entry: `StartUnprimedHealerPass()`. `StartUnprimedHealerPass(false, true)` resumes completed CSV scenario/seed/rank keys without repeating them. The Editor was observed back in Edit Mode after 41 completed rows; the four missing control keys were resumed rather than discarding the completed rows. It runs E/D/C profiles with seeds 101, 102, 103, 104, 105 for each of S1/S2/S3, at 4x simulation speed. No cast is manually started, no competing heal is injected, no initial wound is injected, and encounters never stop at their first heal. Item kits are cleared after normal spawn initialization, matching the prior healer fixture, to isolate skill healing.

Each battle uses the existing tank N001 and healer N006, removing the DPS slot from ConfigureScenario. The existing sustained-monster fixture uses three resolved monster prefabs with runtime overrides: 150 HP, 6 damage, 2.5 movement, 0.8 attack range, and 1.2-second attack interval. The scene's serialized monster references, rather than assumptions from their field names, determine which prefab is instantiated.

- S1: original front-monster formation; the tank takes normal combat damage.
- S2: same composition, with the third monster moved to `(1.2, -1.2, 0)` near the healer at `(2.2, -1.2, 0)`. Damage and threat overlap must be checked in the telemetry; a nearby spawn alone is not proof of engagement.
- S3: same battle with passive monsters: basic damage explicitly zeroed on runtime stat managers, offensive skills removed, pending CharacterBrain spawn callbacks cancelled, and enemy AI disabled using the existing control API. This is a deliberately harmless control fixture, not an assertion that normal monsters cannot hurt the party. No health restoration or invulnerability is used to erase damage.

The stock override helper ignores zero damage, combat substitutes 10 damage when the stat is zero, and CharacterBrain schedules a delayed spawn-time AI reactivation. Initial control attempts were therefore invalid and discarded. The final control explicitly cancels that callback and makes enemies passive after initialization; the tank and healer keep their normal AI, with normal victory detection. No combat damage semantics were changed to make the control pass. A 180-second simulation safety limit records Timeout as the runner's terminal result. A Timeout is a stalemate/incomplete decisive battle, not a win or evidence of eventual survival.

A preliminary batch exposed a telemetry MissingReferenceException when an already-destroyed combatant preceded a living heal recipient. The handler now skips Unity-null combatant GameObjects before accessing transforms. A regression test reproduces that event-order case. The preliminary batch was discarded and its exception retained as [before-fix evidence](healer_unprimed_evidence/harness_bug_before.json). Final telemetry comes only from the restarted pass.

## Survival arbitration counterexample

**FAIL at full HP; PASS at critical HP.** The isolated, no-reaction-delay profile fixture with a wounded ally and ten SenseEnemy signals (maximum threat) gives the healthy healer Survive `0` versus HealAlly approximately `0.83`, and normal arbitration selects HealAlly even with a full threat safety penalty. At critical own HP, normal arbitration selects Survive. The existing Survive above-health-threshold gate admits panic only for burst damage, very high fear, or an available self-recovery route; high threat plus ally healing alone does not pass that gate. See [original failing assertion](healer_unprimed_evidence/heavy_threat_counterexample.json). The final tests characterize the healthy-healer counterexample explicitly; a green suite does **not** certify this safety requirement. The normal bounded-score arbitration and Survive asset were not tuned or changed.

## Results

**45/45 distinct keys recorded**, with 21 AdventurersWin, 8 MonstersWin, and **16 Timeouts**. Thus 29 battles reached a decisive faction outcome; eventual decisive outcomes for the 16 stalemates remain unverified. All rows ran to the runner's terminal result, with no first-heal early exit. See [CSV](healer_unprimed_telemetry.csv), [completion](healer_unprimed_evidence/completion.json), and [validated summary](healer_unprimed_evidence/telemetry_summary.json).

| Scenario | Rank | Runs casting / 5 | Casts / applied | Actual healing | Overheal | Healer alive / 5 | Adv win / Mon win / timeout |
|---|---|---:|---:|---:|---:|---:|---:|
| S1 | E | 0 | 0 / 0 | 0 | 0 | 1 | 4 / 0 / 1 |
| S1 | D | 5 | 5 / 5 | 125 | 0 | 4 | 4 / 0 / 1 |
| S1 | C | 4 | 4 / 4 | 100 | 0 | 2 | 3 / 0 / 2 |
| S2 | E | 0 | 0 / 0 | 0 | 0 | 3 | 3 / 2 / 0 |
| S2 | D | 3 | 18 / 18 | 450 | 0 | 1 | 1 / 4 / 0 |
| S2 | C | 2 | 12 / 12 | 300 | 0 | 3 | 3 / 2 / 0 |
| S3 | E | 0 | 0 / 0 | 0 | 0 | 5 | 2 / 0 / 3 |
| S3 | D | 0 | 0 / 0 | 0 | 0 | 5 | 0 / 0 / 5 |
| S3 | C | 0 | 0 / 0 | 0 | 0 | 5 | 1 / 0 / 4 |

The 39 casts produced 39 applications, 975 actual HP restored, and zero observed overheal. There were no manually primed casts. Control rows have zero tank damage, zero healer damage, no first-damage event, zero casts, zero applied heals, and end mana 60. Mana for every row matches the initial 60 minus 10 per recorded cast.

### Required behavior checks

| Check | Verdict | Evidence / qualification |
|---|---|---|
| Autonomous casting in S1, across all ranks/seeds | **FAIL overall; PASS demonstrated for D and four C seeds** | E: 0/5, D: 5/5, C: 4/5 cast autonomously. C seed 105 did not cast. This is casting autonomy, not proof of tank-directed healing in every run. |
| Zero waste in S3 | **PASS** | All 15 valid passive-enemy controls: no damage, 0 casts / 0 applications / 0 actual healing / 0 overheal; full-health state was not restored artificially. |
| S2 retreats/survives under direct threat | **FAIL where direct damage occurred; INCONCLUSIVE in the other seven seeds** | All eight directly damaged healers died; every row records 0 Survive movement time and 0 Survive path distance. Selecting Survive did not translate into recorded retreat. |
| Average first-heal ordering E >= D >= C | **INCONCLUSIVE** | E never cast, so its delay cannot be averaged or treated as infinity. D >= C holds for the observed S1 and S2 caster subsets. |
| Survive beats HealAlly under heavy threat | **FAIL at full HP; PASS at critical HP** | Counterexample and bounded normal-arbitration test described above. No score was raised over 1. |

S2 direct-damage seeds: E **102, 103**; D **101, 102, 104, 105**; C **103, 104**. The remaining seeds had zero healer damage and are inconclusive for direct-threat survival. All eight directly damaged runs have nonzero threat/wound overlap telemetry; however that cache query may include a wounded self under Allowed. It is not independent proof that an eligible non-healer tank wound and direct threat overlapped for every instant. The near-healer spawn did not guarantee direct engagement across all seeds.

S1 did wound the tank through normal combat. In 14/15 S1 runs, total tank damage was only 54 of its 120 maximum HP, insufficient on its own to cross the configured 50% heal threshold. No thresholds were changed to manufacture additional opportunities. Because Allowed permits self-healing and this CSV aggregates recipient events, these results do not independently establish tank-targeted healing in those runs. The absence of such evidence is not presented as a sensor bug.

A stationary-run probe captured a surviving monster in Wander/Idle and the tank in Attack with movement locked, supporting a real combat stalemate rather than an executor early exit. Its root cause was not established; no speculative movement, perception, animation, or balance change was made. See [probe](healer_unprimed_evidence/stationary_probe.json).

### First-heal delay

Delay = first actual heal-cast event minus first tank-damage event, in simulation seconds. Averages include only runs with both observed events; missing heals are censored observations, not zero delay. S3 has no damage event and contributes no delay samples.

| Rank | S1 mean delay (observations) | S2 mean delay (observations) | S1+S2 pooled mean (observations) |
|---|---:|---:|---:|
| E | N/A (0/5) | N/A (0/5) | N/A (0/10) |
| D | 10.689 s (5/5) | 21.265 s (3/5) | 14.655 s (8/10) |
| C | 10.565 s (4/5) | 17.703 s (2/5) | 12.944 s (6/10) |

This small, conditional sample does not establish rank performance ordering or balance superiority.

### Selected-goal time

Mean simulation seconds per run, while the healer is alive. The per-run values are in the CSV.

| Scenario | Rank | HealAlly | Survive | Recover | KillEnemy | Other |
|---|---|---:|---:|---:|---:|---:|
| S1 | E | 0.000 | 35.575 | 0.000 | 0.000 | 15.274 |
| S1 | D | 1.100 | 0.369 | 0.000 | 0.000 | 21.515 |
| S1 | C | 0.936 | 0.556 | 0.000 | 0.000 | 20.778 |
| S2 | E | 0.000 | 1.579 | 0.000 | 0.000 | 16.869 |
| S2 | D | 1.420 | 6.071 | 0.000 | 0.000 | 35.217 |
| S2 | C | 1.840 | 2.429 | 0.000 | 0.000 | 19.441 |
| S3 | E | 0.000 | 0.000 | 0.000 | 0.000 | 117.059 |
| S3 | D | 0.000 | 0.000 | 0.000 | 0.000 | 180.022 |
| S3 | C | 0.000 | 0.000 | 0.000 | 0.000 | 148.907 |

## Final gates

| Gate | Result | Evidence |
|---|---|---|
| Unity CLI compilation | **0 errors / 0 reported compilation warnings** | [compilation](healer_unprimed_evidence/compilation.json) |
| Entire discovered EditMode suite | **100/100 passed**, 0 failed/skipped/inconclusive | [full results](healer_unprimed_evidence/editmode_tests.json) |
| AISetupValidatorCore.RunAudit | **0 errors / 0 warnings**, no findings | [audit](healer_unprimed_evidence/ai_setup_audit.json) |
| Default goal curve/tangents | **Normalized**, curve/weight unchanged | [final goal audit](healer_unprimed_evidence/final_goal_audit.json) |
| CSV structure | **45 unique expected keys**, all 15 controls harmless/no-cast; mana accounting consistent | [summary](healer_unprimed_evidence/telemetry_summary.json) |

The final resumed-runtime console capture reports zero errors; console warnings are separate from validator warnings. See [runtime console](healer_unprimed_evidence/runtime_errors.json). The earlier, discarded-batch exception is preserved separately. The Editor is left in Edit Mode. A passing characterization test for the healthy-healer counterexample does not change that behavior check's FAIL verdict.

## Telemetry definitions and limits

[Telemetry CSV](healer_unprimed_telemetry.csv) contains one row per run. Times are simulation seconds relative to that run. A first-event value of `-1` means not observed. First ally damage refers to the non-healer tank, not damage to the healer. First heal cast is the real SkillCaster.OnSkillCast event, after the queued animation/cast reaches execution; it is not goal selection or burst application. Heal casts and applied heals are distinct. Actual skill healing and overheal are attributed from SkillHitResolver events and include self-heals permitted by Allowed. Aggregate applied-heal totals alone do not prove that the tank, rather than the healer, received every heal.

Goal columns integrate the healer's selected Utility goal while alive, sampled once per simulation monitor frame. `other` includes FocusFire, FollowLeader, Wander, None, and any other key. They measure selection time, not action completion; selected Survive does not prove retreat. The sum can be below total duration when the healer dies. `survive_moving_seconds` and `survive_distance` integrate NavMeshAgent velocity during Survive; distance is path length, not signed separation from an enemy. The threat/wound column measures overlap of reduced RecoverySafety and an eligible profile-threshold wound; it can include a wounded self for Allowed. End mana is the last observed live stat-manager value, retained if its GameObject is destroyed.

S1/S2 and the first eleven valid controls used explicitly assigned E/D/C profiles before the final no-profile fallback refactor; that refactor does not change their profile-backed scoring path. The remaining four controls used the final code. All final code is checked by the final EditMode suite.

The five Unity random seeds are replay inputs, not proof of bitwise determinism: frame timing, GOAP jobs, and scene/system state can vary. This pass does not cover all ranks, compositions, enemy types, maps, levels, player builds, long-term balance, or every possible SelfTarget/skill combination. EditMode fixtures cover specific targeting and scoring edge cases independently of the battles. No balance thresholds or curves were adjusted to force a PASS.

## Files changed

- `Assets/ScriptableObjects/UtilityGoal/Default/DefaultHealAllyGoal.asset`: reference to existing fallback tuning, with curve and weight unchanged.
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityHealAllyGoal.cs`: bounded scorer, actual affordability, per-skill eligibility, lowest-HP urgency, removal of the inline own-HP attenuation threshold.
- `Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs`: character-root self filtering and optional urgency scan preserving normal profile target preference.
- `Assets/Editor/Validation/ValidationService.cs`: shared missing-heal-goal rule for equipped and catalog-configured heal skills.
- `Assets/Scripts/Testing/BattleSimulationTestRunner.cs`: unprimed seeded scenarios, per-run CSV, first-damage and goal-time telemetry, passive control setup, destroyed-combatant event guard.
- `Assets/Tests/Editor/HealAllyGoalTests.cs` and `.meta`: scoring/readiness/targeting/allocation, validator removal, telemetry lifetime regression, and survival counterexample characterization.
- This report, `healer_unprimed_telemetry.csv`, and the linked `healer_unprimed_evidence/` JSON artifacts.

The existing DefaultHealAllyGoal asset gained only its fallback-profile reference; its curve/weight and the N006 prefab needed no value changes. No rank/profile/balance asset was changed. The pre-existing project snapshot edit is preserved.
