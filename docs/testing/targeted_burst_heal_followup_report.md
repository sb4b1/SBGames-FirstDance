# Targeted Burst Heal — Follow-up Fixes and Verification

Date: 2026-10-24 (Asia/Bangkok). Project: `D:/UnityProject/SBGames`, Unity `6000.6.4f1`, HEAD `3422f99`, existing working tree. This report supplements [the original implementation report](targeted_burst_heal_skill_report.md); its earlier verification figures are historical. No commit was created. Unrelated pre-existing worktree edits and deletions were preserved.

## Final verification

| Check | Result | Evidence |
|---|---|---|
| Unity CLI compilation | **0 errors**, 0 reported compilation warnings | [compilation.json](targeted_burst_heal_followup_evidence/compilation.json) |
| Entire discovered EditMode suite | **78 passed / 78**, 0 failed, skipped, or inconclusive | [editmode_tests.json](targeted_burst_heal_followup_evidence/editmode_tests.json) |
| AISetupValidatorCore.RunAudit | **0 errors / 0 warnings**, no findings | [ai_setup_audit.json](targeted_burst_heal_followup_evidence/ai_setup_audit.json) |
| Live caster-death / target-reuse probe | Heal lands after caster destruction; reused target receives no heal; **0 console errors** | [probe results](targeted_burst_heal_followup_evidence/live_probe_result.json), [probe console](targeted_burst_heal_followup_evidence/live_probe_console.json) |
| Managed allocations | **0 bytes** across 1,000 warmed Recover + Survive evaluations | `RecoveryAndSurviveEvaluate_AllocateZeroManagedBytesAfterWarmup` in the test results |

All Editor automation used the Unity CLI; Unity MCP was not used. `unity status` confirmed the connected project before pipeline work. The final Editor session is in Edit Mode. The standalone `unity test` command refused to run against an already-open project, so tests used the live command:

```text
unity cmd run_tests --mode EditMode --async_tests false --timeout 120
unity recompile
unity cmd eval "...AISetupValidatorCore.RunAudit()..."
```

The live console error count was zero. Console warnings from test/probe setup are separate from the AI validator's zero-warning result; this report does not claim a warning-free Editor console.

## 1. Utility normalization and safe recovery

`BaseAI.SelectBestGoal` now normalizes every evaluated UtilityGoal score centrally to `[0, 1]`, including future goals. NaN becomes zero; infinities clamp to an endpoint. Debug/stored scores contain this normalized score only. The default `goalHysteresisBonus = 0.08` is added exclusively to a temporary comparison value. Equal positive normalized scores choose **Survive**, even when Recover was active and receives hysteresis, and regardless of goal-list order. All-zero candidates still yield None.

`UtilityRecoverGoal` no longer has `surviveOverrideFloor = 1.02` or a 1.05 casting score. Health, mana, status-cure, item-cast commitment, and self-only healing urgency are multiplied by a bounded recovery safety factor. Self-only healing works without an item kit. `RecoverSensor` recognizes self-only healing, and `RecoverAction` now actually starts the self-cast on the character root. Recovery navigation remains owned by `AgentMoveBehaviour`, using the sensor-provided target; the action no longer sets a destination.

Safety is configured by the immutable `RecoverySafetyTuningSO` and its default asset `Assets/Resources/AI/RecoverySafety_Default.asset`, loaded once by BaseAI. Default tuning:

| Signal | Default normalization / shape |
|---|---|
| Cached ThreatLevel | 0 to 80; linear safety 1 to 0 |
| CharacterAIMemory recent damage | 2-second window; 0 to 25% of MaxLife; linear safety 1 to 0 |
| Cached enemy proximity | Squared distance, inner 1.5 units / safe 5 units; linear safety 0 to 1 in that normalized domain |
| Combination | Product of the three independently clamped safety values |

No physics or NavMesh query occurs in these evaluations. Enemy scanning uses the existing cached transforms and `sqrMagnitude`. Existing cornered/outlook navigation probing was moved behind BaseAI's throttled cached outlook update so utility evaluation reads the cached result.

Survive uses pressure as urgency when survival is relevant, including a threatened unit that could otherwise recover. Healthy units do not automatically flee merely because an enemy exists. Under heavy pressure recovery falls to zero, allowing retreat while a potion remains. Safe, wounded units can choose Recover. The surviving goal's archetype/personality gates remain in force.

The project-wide ceiling also required normalizing Warp's old 1.10 commitment and 1.15 maximum to 1. Its score no longer bypasses the arbiter's Survive tie rule. The validator now checks serialized utility curves, including tangent extrema between valid-looking keys and weighted curves. Twelve existing curves were corrected: KillEnemy fear overshoot, Warp's health curve, and ten skill cluster curves. UtilitySkill's default cluster curve was also corrected so new assets start valid.

An additional item-kit bug surfaced when the legacy regression became discoverable: resolved category slots incremented the initial count but left the remaining count at zero. `AdventurerItemKitController` now increments both. The potion-vs-retreat regression therefore tests a genuinely usable item.

### Tests added and existing expectations changed

New tests cover the authored-goal score sweep over 135 health/mana/threat/distance combinations; future goal outputs including negatives, values above one, NaN and infinities; safety's individual cached signals; safe self-only recovery versus heavy-threat survival; both tie orders with active Recover hysteresis; stored scores without hysteresis; zero managed allocation; curve key/tangent violations; and healer prefab/animation/import wiring.

The existing tests changed were:

- `AdventurerAIProfileTests.Phase5_RecoverAndSurviveCompetition_Hysteresis_And_RankQualityPositioning`: a melee-threatened unit with a usable potion now expects **Survive**, replacing the potion-first Recover expectation. Rank-specific recovery positions and room-quota assertions remain. Subsequent manual item consumption tests kit mechanics independently of AI selection.
- `AdventurerAIProfileTests.Phase8_UtilityWarpGoal_EvaluatesHysteresisDuringCast`: active Warp commitment now expects **1.0**, replacing **1.10**.

`LegacyRecoveryRegressionTests` exposes those two existing methods to the EditMode runner. The original fixture compiles into Assembly-CSharp and is not otherwise discovered as an Editor test fixture. Both bridged regressions pass. Other undiscovered legacy fixtures were not bulk-migrated into a new assembly.

## 2. Single life entry-point audit

The search covered project C# under Assets, including potions/item kits, inventory effects, regeneration, skills, DM/loop abilities, status effects, traps, props, shop instances, Editor tools, and tests. It searched life/health/vitality/HP assignments, restoration calls, damage calls, and reflective life writes. No external direct write to character Life remains; its setter is private.

| Found surface | Routing and follow-up |
|---|---|
| `CharacterStatsManager.InitializeStats` / `SetBaseStats` | Initial/spawn setup assigns full life. Retained as initialization, not an in-combat heal. |
| `CharacterStatsManager.ApplyHeal` | Canonical positive healing. Actual healing and overheal remain separate. `NotifyLifeChanged` is now inside the `actualHealed > 0` block. A full-health target returns the entire requested amount as overheal without emitting OnLifeChanged or restoration events. |
| `RestoreLife` callers | Existing compatibility wrapper already delegates to ApplyHeal. No second mutation path was introduced. |
| `TickRegen` | `RestoreLife -> ApplyHeal`; the existing regen-specific event remains attributed to regeneration. |
| `RestoreHealthEffect` | Now calls ApplyHeal directly and returns its success. Dead targets are rejected instead of allowing an item to be consumed for a failed heal. |
| `AdventurerItemKitController` | `CompleteCast -> ConsumableEffect.Apply -> RestoreHealthEffect -> ApplyHeal`; no life assignment. Remaining-count bookkeeping fixed as described above. |
| `SkillHitResolver` | Already calls ApplyHeal, then emits both skill events with actual healing. Preserved. |
| Sandbox stat setup and competing-heal fixture | `RestoreLife -> ApplyHeal` and direct ApplyHeal respectively; simulated injury uses TakeDamage with True DamageData. |
| `CharacterStatsManager.TakeDamage` / ForceDeath | Existing centralized damage/death paths own life reductions and death assignment. Retained. |
| Trap / destructible-prop health | Their own IDamageable implementations own damage/death writes. Spawn and planning-round reset assignments initialize/reset their own health. They are not character healing bypasses. |
| `CombatResultHandler.BossCombatRecord.CurrentHealth` | Reporting snapshot updated from damage/death/health reports, not an authoritative character vitality field. No heal bypass found. |
| `MonsterRuntimeInstance.currentHealth` | Shop initialization snapshot, not an in-combat character-life mutation. |
| DM abilities and status effects | No additional character-life write or independent healing mutation found. |

`ApplyHeal_FullHealth_ReturnsOverhealWithoutLifeNotification` verifies the zero-actual-heal notification rule.

## 3. Overheal events and complete battles

The existing event signature remains `OnSkillHealApplied(SkillInstance, GameObject, float)`; its first argument remains the skill instance, from which a live caster may be obtained. It fires with **actual healing**, alongside `OnSkillHealWithOverhealApplied(SkillInstance, GameObject, float, float)`. No caller API changed. A caster may be Unity-null after destruction; the burst's target validation uses captured launch alignment rather than dereferencing that caster.

The battle harness now records every applied skill heal and its overheal. It reclassifies the exact synchronous OnLifeRestored amount into skill healing, replacing the previous timestamp heuristic and first-heal-only fallback. Cast count and applied-heal count remain separate, so fizzles can be distinguished.

`StartHealerOverhealBattleComparison(101)` runs three complete Combat_Sandbox encounters with E/D/C profiles. The fixture uses three monsters with 150 HP, 6 damage, 2.5 move speed, 0.8 range, and a 1.2-second attack interval. It waits for spawn initialization, clears potion kits, and injures the tank by 60% of MaxLife. One regular SkillCaster cast is primed per rank to guarantee the accounting case. A competing heal during wind-up leaves the target missing 5 HP. The burst requests 25 HP, yielding 5 actual / 20 overheal. The remainder of each encounter runs under AI control until an outcome, with no first-heal early exit.

| Rank | Outcome | Full battle duration | Casts | Applied heals | Total actual skill healing | Total overheal |
|---|---|---:|---:|---:|---:|---:|
| E | AdventurersWin | 14.961 s | 1 | 1 | 5 HP | 20 HP |
| D | AdventurersWin | 16.802 s | 1 | 1 | 5 HP | 20 HP |
| C | MonstersWin | 37.159 s | 1 | 1 | 5 HP | 20 HP |

Evidence: [full_battles.json](targeted_burst_heal_followup_evidence/full_battles.json). Each row confirms the near-full-target injection. These totals cover the entire battle, including the primed cast. No additional autonomous heal cast occurred in these runs. They verify end-to-end accounting and are **not** evidence of rank reaction-time ordering, general autonomous healing frequency, or rank balance. Earlier unprimed attempts produced no heal and were not treated as successful overheal verification.

## 4. Supplied art, animation, and foreground visibility

The imported PNG is byte-for-byte identical to the supplied `output/healing_effect/heal_sheet.png`, paired with `heal_preview.gif` in the chat workspace. Both PNG SHA-256 hashes are `962407b990adc2da42bd2055f0a98dc6112673a2921f9b3311605835de1892ec`. The GIF contains twelve 256x256 preview frames; the sheet contains twelve 64x64 frames. Comparing all 2,574 visible source-frame pixels against the GIF at its 4x preview scale found **zero RGB mismatches**. The PNG contains 46,578 fully transparent pixels. See [art_provenance.json](targeted_burst_heal_followup_evidence/art_provenance.json).

The old prefab had a built-in placeholder as its static SpriteRenderer sprite, a six-frame animation, compressed import settings, and the Default sorting layer. The follow-up:

- Assigns the first supplied sprite directly on `SKILLFX_SKILL_HEAL_001.prefab`.
- Animates all twelve supplied frames during the 0.5-second wind-up, preserving the existing controller location/structure. Animation restarts on each detonation, including pool reuse.
- Uses **VisualAnchor.Feet**, with zero feet offset. It follows the target's world position rather than inheriting its activation hierarchy.
- Sets Point filtering, Compression None/Uncompressed, transparent alpha, Full Rect mesh, and retains the existing 100 PPU. ADR 0004 does not specify a replacement PPU.
- Sets sorting layer **SkillFX**, order **5**. SkillFX sorts after Character. N006's character SortingGroup is on Character, order 0.

The heal is visibly rendered in front of the target, verified in the live game view:

![Supplied heal art rendered in front of N006](targeted_burst_heal_followup_evidence/heal_visual.png)

The screenshot is a paused animation sample captured for visual verification; live event/lifecycle checks resumed afterwards.

N006's serialized BaseAI goal list also lacked DefaultHealAllyGoal despite containing the heal skill. That goal is now assigned, and a prefab regression test checks the goal, the twelve distinct frame sprites, the real static sprite, and the import/sorting settings.

## 5. Caster death and target pooling during wind-up

A launched TargetedBurst captures its caster's faction/tag alignment. Caster death, deactivation, destruction, or reuse with a different faction does not invalidate that launched heal. Target faction, target life, and target active lifetime remain subject to revalidation at the burst moment.

`TargetedBurstTargetLifetime` increments a generation on target deactivation. The effect tracks that generation, so a target deactivated and reactivated in the same frame cannot receive an old heal. The effect follows the target independently in LateUpdate. This avoids Unity's prohibition against reparenting children while a parent hierarchy is being deactivated. Both cold-created and pool-managed skill effects now despawn safely: an ObjectPoolManager is used only when a PooledObject exists.

New HealSkillTests cover four caster cases (dead, inactive, pooled/reassigned faction, destroyed), both event amounts, target deactivation, and deactivation followed by immediate reuse. EditMode tests drive the burst directly and explicitly invoke target lifetime callbacks because non-ExecuteAlways callbacks are not dispatched for these EditMode fixtures.

The separate live Play Mode probe used the real effect prefab and let its normal 0.5-second wind-up elapse. The caster was actually destroyed; the heal restored the recipient from 95 to 100 HP. The legacy event recorded 5 actual HP with a Unity-null caster, and the overheal event recorded 20. A second target was deactivated/reactivated during wind-up and remained at 50 HP. No console errors occurred. Temporary probe objects, subscriptions, camera changes, and pause state were discarded on leaving Play Mode.

## 6. Final configuration, assumptions, and limits

| SKILL_HEAL_001 setting | Verified value |
|---|---|
| Cooldown | **1 second**, from the caster prefab's SkillStatsManager |
| Mana cost | **10** |
| Cast range | **6 world units** |
| Wind-up | **0.5 seconds** |
| BaseHeal | **25 HP** |
| SelfTarget | Allowed; self-only behavior is separately covered by tests |

| ADVENTURER_N006 Character Base Stats / data | Verified value |
|---|---|
| Life | **80** |
| Mana | **60** |
| Damage | **8** |
| Move speed | **4.5** |
| Default skills | **SKILL_HEAL_001** |
| Rank / function / class | **E / Healer / Cleric** |

Evidence: [configuration.json](targeted_burst_heal_followup_evidence/configuration.json), CharacterDatabase's defaultSkillIds, and the character CSV. The initial on-disk N006 asset read 70 life / 55 mana / 4 damage (1 attack range, 0.8 attack interval), while Characters.csv already specified 80 / 60 / 8 (6 range, 1 interval). After the Editor/import reload, the asset matched the CSV. The final report and battle runs use the verified synchronized values; no new balance numbers were selected for N006.

Assumptions and evidence boundaries:

- “Cast started” for the death guarantee means the targeted burst has been launched and entered its visual wind-up, analogous to a projectile in flight. A queued CharacterSkillController cast that has not yet emitted its skill instance is still subject to the existing animation/cast interruption rules.
- Initial/spawn setup, planning-round reset, and reporting snapshots are distinct from in-combat healing; their assignments were retained as described in the life audit.
- All **78 tests discovered by the Editor's EditMode runner** passed. Two relevant otherwise-undiscovered legacy methods were bridged and included. This does not claim that every NUnit method in unmarked Assembly-CSharp fixtures was independently discovered or executed.
- No standalone player build or separate complete PlayMode test suite was run. Runtime lifecycle behavior was verified through the live sandbox probe; an attempted cross-mode Editor test stalled, the Editor was recovered through a graceful Unity CLI restart, and the temporary cross-mode test was removed.
- The warmed allocation measurement covers the changed Recover/Survive Evaluate paths, not every unrelated system in the game.
- The full-battle fixture guarantees one primed heal and records all later activity. Broader autonomous healer behavior and multi-seed balance remain outside this accounting verification.
- GitNexus impact checks identified BaseAI as HIGH risk, CharacterStatsManager and the kit controller as CRITICAL. Graph results have interface-dispatch and flow-enumeration limits; targeted source checks and live tests supplied the runtime evidence. No graph result was treated as a proof that an unresolved caller does not exist.

## 7. Changed-file manifest

The following is the follow-up's explicit file manifest, including generated evidence and Unity metadata. It excludes unrelated changes already present in the working tree. N006's observed stat synchronization is included. A machine-readable copy is [file_manifest.json](targeted_burst_heal_followup_evidence/file_manifest.json).

- `Assets/Animators/Skills/Heal/Anim_S_H001/Anim_S_H001_Hit.anim`
- `Assets/Editor/Validation/AISetupValidatorWindow.cs`
- `Assets/Editor/Validation/ValidationService.cs`
- `Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N006.prefab`
- `Assets/Prefabs/SkillEffect/Default/SKILLFX_SKILL_HEAL_001.prefab`
- `Assets/Resources/AI.meta`
- `Assets/Resources/AI/RecoverySafety_Default.asset`
- `Assets/Resources/AI/RecoverySafety_Default.asset.meta`
- `Assets/Resources/Skills/Heal/H001/Sprite_S_H001.png.meta`
- `Assets/ScriptableObjects/Character/Adventurer/Normal/ADVENTURER_N006.asset`
- `Assets/ScriptableObjects/UtilityGoal/Default/DefaultKillEnemyGoal.asset`
- `Assets/ScriptableObjects/UtilityGoal/Default/DefaultRecoverGoal.asset`
- `Assets/ScriptableObjects/UtilityGoal/Default/DefaultWarpGoal.asset`
- `Assets/ScriptableObjects/UtilitySkill/Default/Utility_SKILL_001.asset`
- `Assets/ScriptableObjects/UtilitySkill/Default/Utility_SKILL_HEAL_001.asset`
- `Assets/ScriptableObjects/UtilitySkill/Fire/Utility_SKILL_F001.asset`
- `Assets/ScriptableObjects/UtilitySkill/Fire/Utility_SKILL_F002.asset`
- `Assets/ScriptableObjects/UtilitySkill/Lightning/Utility_SKILL_L001.asset`
- `Assets/ScriptableObjects/UtilitySkill/Lightning/Utility_SKILL_L002.asset`
- `Assets/ScriptableObjects/UtilitySkill/NoAvailableSkill.asset`
- `Assets/ScriptableObjects/UtilitySkill/Water/Utility_SKILL_WA001.asset`
- `Assets/ScriptableObjects/UtilitySkill/Water/Utility_SKILL_WA002.asset`
- `Assets/ScriptableObjects/UtilitySkill/Wind/Utility_SKILL_WI002.asset`
- `Assets/Scripts/Character/AI/AdventurerItemKitController.cs`
- `Assets/Scripts/Character/AI/GOAP/AI/BaseAI.cs`
- `Assets/Scripts/Character/AI/GOAP/Actions/RecoverAction.cs`
- `Assets/Scripts/Character/AI/GOAP/Sensors/RecoverSensor.cs`
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityRecoverGoal.cs`
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilitySurviveGoal.cs`
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/Goal/UtilityWarpGoal.cs`
- `Assets/Scripts/Character/AI/GOAP/UtilityAI/UtilitySkill.cs`
- `Assets/Scripts/Character/AI/RecoverySafetyTuningSO.cs`
- `Assets/Scripts/Character/AI/RecoverySafetyTuningSO.cs.meta`
- `Assets/Scripts/Inventory/Core/Effects/RestoreHealthEffect.cs`
- `Assets/Scripts/Skills/SkillInstance.cs`
- `Assets/Scripts/Skills/TargetedBurstDetonation.cs`
- `Assets/Scripts/Skills/TargetedBurstTargetLifetime.cs`
- `Assets/Scripts/Skills/TargetedBurstTargetLifetime.cs.meta`
- `Assets/Scripts/Stat/CharacterStatsManager.cs`
- `Assets/Scripts/Testing/BattleSimulationTestRunner.cs`
- `Assets/Tests/AdventurerAIProfileTests.cs`
- `Assets/Tests/Editor/HealSkillTests.cs`
- `Assets/Tests/Editor/LegacyRecoveryRegressionTests.cs`
- `Assets/Tests/Editor/LegacyRecoveryRegressionTests.cs.meta`
- `Assets/Tests/Editor/RecoverySafetyTests.cs`
- `Assets/Tests/Editor/RecoverySafetyTests.cs.meta`
- `docs/testing/targeted_burst_heal_followup_evidence/ai_setup_audit.json`
- `docs/testing/targeted_burst_heal_followup_evidence/art_provenance.json`
- `docs/testing/targeted_burst_heal_followup_evidence/compilation.json`
- `docs/testing/targeted_burst_heal_followup_evidence/configuration.json`
- `docs/testing/targeted_burst_heal_followup_evidence/editmode_tests.json`
- `docs/testing/targeted_burst_heal_followup_evidence/file_manifest.json`
- `docs/testing/targeted_burst_heal_followup_evidence/final_console.json`
- `docs/testing/targeted_burst_heal_followup_evidence/full_battles.json`
- `docs/testing/targeted_burst_heal_followup_evidence/heal_visual.png`
- `docs/testing/targeted_burst_heal_followup_evidence/live_probe_console.json`
- `docs/testing/targeted_burst_heal_followup_evidence/live_probe_result.json`
- `docs/testing/targeted_burst_heal_followup_evidence/live_probe_start.json`
- `docs/testing/targeted_burst_heal_followup_report.md`
