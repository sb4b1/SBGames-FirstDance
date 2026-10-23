using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SB4b1.GameMath;
using SBGames.Character;
using SBGames.Characters.AI;
using SBGames.Loop;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SBGames.Testing
{
    /// <summary>
    /// Executes the full repeatable open-field combat test pass across scenarios A, B, C, D, E.
    /// Captures per-unit telemetry, resource loop validation, and anomaly tracking.
    /// Writes detailed CSV logs to docs/testing/combat_sandbox_pass_telemetry.csv.
    /// </summary>
    public static class CombatSandboxPassExecutor
    {
        [Serializable]
        public class ClassTelemetryData
        {
            public string characterId;
            public string actualFunction;
            public float damageDealt;
            public float damageTaken;
            public int castsCount;
            public int healsDoneCount;
            public float healingDoneAmount;
            public float healingReceivedAmount;
            public int itemUsesCount;
            public float timeToFirstTarget = -1f;
            public float timeToFirstAttack = -1f;
            public int goalFlipFlopCount;
            public int keepDistanceEntries;
            public int keepDistanceExits;
            public float stuckOrIdleDuration;
            public int jitterCount;
            public bool outOfCombatRegenObserved;
            public bool cannotCastYieldObserved;
            public bool isAlive;
        }

        [Serializable]
        public class ScenarioRunData
        {
            public string scenarioCode; // A, B, C, D, E
            public string scenarioName;
            public int seed;
            public string rankLabel;
            public string composition;
            public BattleSimulationTestRunner.BattleOutcome outcome;
            public float duration;
            public List<ClassTelemetryData> units = new();
            public List<string> anomalies = new();
            public long gcAllocBytes = -1; // -1 if not measured
            public float avgFrameTimeMs = -1f; // -1 if not measured
            public bool perfMeasured = false;
        }

        public static List<ScenarioRunData> AllPassResults { get; } = new();
        public static bool IsExecuting { get; private set; }

        public static void RunAllScenariosHeadless()
        {
            var harnessGo = GameObject.Find("TEST_HARNESS");
            if (harnessGo == null)
            {
                Debug.LogError("[CombatPassExecutor] TEST_HARNESS not found in scene!");
                return;
            }

            var runner = harnessGo.GetComponent<BattleSimulationTestRunner>();
            var analytics = harnessGo.GetComponent<PartyBattleAnalyticsHarness>() ?? harnessGo.AddComponent<PartyBattleAnalyticsHarness>();

            runner.StartCoroutine(ExecuteAllRoutine(runner, analytics));
        }

        public static IEnumerator ExecuteAllRoutine(BattleSimulationTestRunner runner, PartyBattleAnalyticsHarness analytics)
        {
            IsExecuting = true;
            AllPassResults.Clear();

            // Register GOAP factories
            var goap = UnityEngine.Object.FindAnyObjectByType<CrashKonijn.Goap.Runtime.GoapBehaviour>();
            if (goap != null)
            {
                CombatTestEnvironmentBootstrap.EnsureAgentFactoriesOnExistingGoap(goap);
            }

            // Load assets
            analytics.LoadAssets();
#if UNITY_EDITOR
            var adv1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N001.prefab");
            var adv2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N002.prefab");
            var adv3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N003.prefab");
            var adv4 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N004.prefab");
            var adv5 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N005.prefab");
            var mon1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");

            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
#else
            GameObject adv1 = null, adv2 = null, adv3 = null, adv4 = null, adv5 = null, mon1 = null;
            AdventurerAIProfile profileE = null, profileD = null, profileC = null;
#endif

            // ==========================================
            // SCENARIO A: 1v1 Each Adventurer vs 1 MONSTER_N001 (5 Runs)
            // ==========================================
            var advPrefabs = new (string id, GameObject prefab)[]
            {
                ("ADVENTURER_N001", adv1),
                ("ADVENTURER_N002", adv2),
                ("ADVENTURER_N003", adv3),
                ("ADVENTURER_N004", adv4),
                ("ADVENTURER_N005", adv5)
            };

            for (int i = 0; i < advPrefabs.Length; i++)
            {
                var adv = advPrefabs[i];
                var run = new ScenarioRunData
                {
                    scenarioCode = "A",
                    scenarioName = $"1v1: {adv.id} vs MONSTER_N001",
                    seed = 100 + i,
                    rankLabel = "E",
                    composition = $"{adv.id} (Rank E) vs 1x MONSTER_N001"
                };

                runner.Roster.Clear();
                runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                {
                    slotLabel = adv.id,
                    unitPrefab = adv.prefab,
                    customSpawnPosition = new Vector3(2.0f, 0f, 0f),
                    faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                    profileOverride = profileE,
                    isPartyLeader = true
                });
                runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                {
                    slotLabel = "MONSTER_N001",
                    unitPrefab = mon1,
                    customSpawnPosition = new Vector3(-2.0f, 0f, 0f),
                    faction = BattleSimulationTestRunner.BattleFaction.Monster
                });

                yield return ExecuteScenarioRunRoutine(runner, run, timeScale: 2f, timeout: 25f);
                AllPassResults.Add(run);
            }

            // ==========================================
            // SCENARIO B: 1v1 Survival (Melee Frontline N001 & Healer N001 at Low HP, with and without items) (4 Runs)
            // ==========================================
            // Note: ADVENTURER_N001 is the only unit with HealAllyGoal in prefab
            var survivalConfigs = new (string label, GameObject prefab, bool withItems)[]
            {
                ("ADVENTURER_N001 [Melee Frontline]", adv1, true),
                ("ADVENTURER_N001 [Melee Frontline]", adv1, false),
                ("ADVENTURER_N001 [Healer Goal Path]", adv1, true),
                ("ADVENTURER_N001 [Healer Goal Path]", adv1, false)
            };

            for (int i = 0; i < survivalConfigs.Length; i++)
            {
                var sc = survivalConfigs[i];
                var run = new ScenarioRunData
                {
                    scenarioCode = "B",
                    scenarioName = $"1v1 Survival: {sc.label} [{(sc.withItems ? "With Items" : "No Items")}]",
                    seed = 200 + i,
                    rankLabel = "E",
                    composition = $"{sc.label} (25% HP, {(sc.withItems ? "+Pots" : "No Pots")}) vs 1x MONSTER_N001"
                };

                runner.Roster.Clear();
                runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                {
                    slotLabel = sc.label,
                    unitPrefab = sc.prefab,
                    customSpawnPosition = new Vector3(2.0f, 0f, 0f),
                    faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                    profileOverride = profileE,
                    isPartyLeader = true
                });
                runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                {
                    slotLabel = "MONSTER_N001",
                    unitPrefab = mon1,
                    customSpawnPosition = new Vector3(-2.5f, 0f, 0f),
                    faction = BattleSimulationTestRunner.BattleFaction.Monster
                });

                yield return ExecuteScenarioRunRoutine(runner, run, timeScale: 2f, timeout: 25f, onSpawnSetup: () =>
                {
                    for (int a = 0; a < runner.Adventurers.Count; a++)
                    {
                        var adv = runner.Adventurers[a];
                        if (adv.Stats != null)
                        {
                            // Reduce to 25% HP
                            float dmg = adv.Stats.Life - (adv.MaxLife * 0.25f);
                            if (dmg > 0f) adv.Stats.TakeDamage(new DamageData(dmg, DamageType.True));
                        }

                        var kit = adv.GameObject.GetComponent<AdventurerItemKitController>();
                        if (kit != null)
                        {
                            if (!sc.withItems)
                            {
                                kit.ClearKit();
                            }
                            else
                            {
                                kit.OnSpawn();
                            }
                        }
                    }
                });
                AllPassResults.Add(run);
            }

            // ==========================================
            // SCENARIO C: Full Party 5 Adventurers vs 3 and vs 5 Monsters (2 Runs)
            // ==========================================
            int[] monCounts = new int[] { 3, 5 };
            for (int cIdx = 0; cIdx < monCounts.Length; cIdx++)
            {
                int count = monCounts[cIdx];
                var run = new ScenarioRunData
                {
                    scenarioCode = "C",
                    scenarioName = $"Full Party: 5 Adv vs {count} MONSTER_N001",
                    seed = 300 + count,
                    rankLabel = "D",
                    composition = $"5 Adventurers (Rank D) vs {count} MONSTER_N001"
                };

                runner.Roster.Clear();
                AddStandardFiveParty(runner, adv1, adv2, adv3, adv4, adv5, profileD);

                for (int m = 0; m < count; m++)
                {
                    float y = (m - (count - 1) * 0.5f) * 1.5f;
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = $"MONSTER_N001_{m + 1}",
                        unitPrefab = mon1,
                        customSpawnPosition = new Vector3(-3.0f, y, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                }

                yield return ExecuteScenarioRunRoutine(runner, run, timeScale: 2f, timeout: 35f);
                AllPassResults.Add(run);
            }

            // ==========================================
            // SCENARIO D: Rank Comparison (Full Party at Ranks E, D, C vs 3 Monsters, 3 seeded runs each) (9 Runs)
            // ==========================================
            var rankProfiles = new (string rank, AdventurerAIProfile prof)[]
            {
                ("E", profileE),
                ("D", profileD),
                ("C", profileC)
            };
            int[] dSeeds = new int[] { 401, 402, 403 };

            for (int r = 0; r < rankProfiles.Length; r++)
            {
                var rp = rankProfiles[r];
                for (int s = 0; s < dSeeds.Length; s++)
                {
                    int seed = dSeeds[s];
                    var run = new ScenarioRunData
                    {
                        scenarioCode = "D",
                        scenarioName = $"Rank Comparison: Rank {rp.rank} (Run #{s + 1})",
                        seed = seed,
                        rankLabel = rp.rank,
                        composition = $"5 Adventurers (Rank {rp.rank}) vs 3 MONSTER_N001"
                    };

                    runner.Roster.Clear();
                    AddStandardFiveParty(runner, adv1, adv2, adv3, adv4, adv5, rp.prof);

                    for (int m = 0; m < 3; m++)
                    {
                        float y = (m - 1f) * 1.5f;
                        runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                        {
                            slotLabel = $"MONSTER_N001_{m + 1}",
                            unitPrefab = mon1,
                            customSpawnPosition = new Vector3(-3.0f, y, 0f),
                            faction = BattleSimulationTestRunner.BattleFaction.Monster
                        });
                    }

                    yield return ExecuteScenarioRunRoutine(runner, run, timeScale: 2f, timeout: 35f);
                    AllPassResults.Add(run);
                }
            }

            // ==========================================
            // SCENARIO E: 5 Adventurers vs 10 Monsters with Frame Time and GC Allocation Capture (1 Run)
            // ==========================================
            {
                var run = new ScenarioRunData
                {
                    scenarioCode = "E",
                    scenarioName = "Stress Test: 5 Adv vs 10 MONSTER_N001 (Perf & Alloc)",
                    seed = 500,
                    rankLabel = "D",
                    composition = "5 Adventurers (Rank D) vs 10 MONSTER_N001"
                };

                runner.Roster.Clear();
                AddStandardFiveParty(runner, adv1, adv2, adv3, adv4, adv5, profileD);

                for (int m = 0; m < 10; m++)
                {
                    float y = (m - 4.5f) * 0.8f;
                    float x = -3.0f - (m % 2 == 0 ? 0f : 1.2f);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = $"MONSTER_N001_{m + 1}",
                        unitPrefab = mon1,
                        customSpawnPosition = new Vector3(x, y, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                }

                yield return ExecuteScenarioRunRoutine(runner, run, timeScale: 2f, timeout: 45f, capturePerf: true);
                AllPassResults.Add(run);
            }

            // Write results to CSV
            WriteTelemetryToCsv("docs/testing/combat_sandbox_pass_telemetry.csv");

            IsExecuting = false;
            Time.timeScale = 1f;
            Debug.Log($"<color=green><b>[CombatSandboxPassExecutor] ALL SCENARIOS COMPLETED ({AllPassResults.Count} runs)! Telemetry written to docs/testing/combat_sandbox_pass_telemetry.csv</b></color>");
        }

        private static void AddStandardFiveParty(
            BattleSimulationTestRunner runner,
            GameObject adv1, GameObject adv2, GameObject adv3, GameObject adv4, GameObject adv5,
            AdventurerAIProfile profile)
        {
            // 1. ADVENTURER_N001 (Tank, DPS)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "ADVENTURER_N001",
                unitPrefab = adv1,
                customSpawnPosition = new Vector3(1.5f, 0f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                profileOverride = profile,
                isPartyLeader = true
            });

            // 2. ADVENTURER_N002 (DPS)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "ADVENTURER_N002",
                unitPrefab = adv2,
                customSpawnPosition = new Vector3(3.0f, 1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                profileOverride = profile
            });

            // 3. ADVENTURER_N003 (DPS)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "ADVENTURER_N003",
                unitPrefab = adv3,
                customSpawnPosition = new Vector3(3.5f, 0f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                profileOverride = profile
            });

            // 4. ADVENTURER_N004 (Tank, DPS)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "ADVENTURER_N004",
                unitPrefab = adv4,
                customSpawnPosition = new Vector3(3.0f, -1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                profileOverride = profile
            });

            // 5. ADVENTURER_N005 (DPS, Scout)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "ADVENTURER_N005",
                unitPrefab = adv5,
                customSpawnPosition = new Vector3(2.0f, -1.0f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                profileOverride = profile
            });
        }

        private static IEnumerator ExecuteScenarioRunRoutine(
            BattleSimulationTestRunner runner,
            ScenarioRunData run,
            float timeScale,
            float timeout,
            Action onSpawnSetup = null,
            bool capturePerf = false)
        {
            UnityEngine.Random.InitState(run.seed);
            runner.SetTimeScale(timeScale);

            long gcBefore = 0;
            run.perfMeasured = capturePerf;
            if (capturePerf)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                gcBefore = GC.GetTotalMemory(true);
            }

            runner.StartSimulation();
            onSpawnSetup?.Invoke();

            var adventurers = runner.Adventurers;
            int unitCount = adventurers.Count;
            var telemetry = new ClassTelemetryData[unitCount];
            var lastGoals = new EnumUtilGoal[unitCount];
            var lastGoalTimes = new float[unitCount];
            var lastPositions = new Vector3[unitCount];
            var samePosTimes = new float[unitCount];
            var wasInKeepDistance = new bool[unitCount];

            for (int i = 0; i < unitCount; i++)
            {
                var adv = adventurers[i];
                string actualFunc = adv.Stats != null ? adv.Stats.CharacterFunction.ToString() : "None";
                telemetry[i] = new ClassTelemetryData
                {
                    characterId = adv.SourceSlot != null ? adv.SourceSlot.slotLabel : adv.GameObject.name,
                    actualFunction = actualFunc,
                    isAlive = true
                };
                lastGoals[i] = EnumUtilGoal.None;
                lastGoalTimes[i] = 0f;
                lastPositions[i] = adv.GameObject.transform.position;
                samePosTimes[i] = 0f;
                wasInKeepDistance[i] = false;

                // Subscribe potion events
                var kit = adv.GameObject.GetComponent<AdventurerItemKitController>();
                if (kit != null)
                {
                    int capturedIdx = i;
                    kit.OnPotionConsumed += (k, item) =>
                    {
                        telemetry[capturedIdx].itemUsesCount++;
                    };
                }

                // Subscribe skill caster events
                var casters = adv.GameObject.GetComponentsInChildren<SkillCaster>(true);
                foreach (var caster in casters)
                {
                    int capturedIdx = i;
                    caster.OnSkillCast += (sData, target) =>
                    {
                        telemetry[capturedIdx].castsCount++;
                        if (sData != null && (sData.targetFilter == SkillData.SkillTargetFilter.AlliesOnly || sData.targetFilter == SkillData.SkillTargetFilter.SelfOnly))
                        {
                            telemetry[capturedIdx].healsDoneCount++;
                        }
                    };
                }
            }

            float frameTimeSum = 0f;
            int frameCount = 0;

            while (runner.State == BattleSimulationTestRunner.SimulationState.Running && runner.ElapsedTime < timeout)
            {
                float t = runner.ElapsedTime;
                float dt = Time.deltaTime;

                if (capturePerf)
                {
                    frameTimeSum += dt;
                    frameCount++;
                }

                for (int i = 0; i < unitCount; i++)
                {
                    var adv = adventurers[i];
                    if (adv == null || !adv.IsAlive || adv.GameObject == null)
                    {
                        telemetry[i].isAlive = false;
                        continue;
                    }

                    Vector3 pos = adv.GameObject.transform.position;

                    // First target acquired check
                    if (telemetry[i].timeToFirstTarget < 0f && adv.AI != null && adv.AI.CurrentTarget != null)
                    {
                        telemetry[i].timeToFirstTarget = t;
                    }

                    // First attack initiated check
                    if (telemetry[i].timeToFirstAttack < 0f && adv.Brain != null && (adv.Brain.IsAttacking || adv.Brain.IsCastingSkill))
                    {
                        telemetry[i].timeToFirstAttack = t;
                    }

                    // Goal Flip-Flop tracking
                    if (adv.AI != null)
                    {
                        EnumUtilGoal g = adv.AI.CurrentGoalKey;
                        if (g != lastGoals[i])
                        {
                            if (lastGoals[i] != EnumUtilGoal.None)
                            {
                                float dwell = t - lastGoalTimes[i];
                                if (dwell < 0.4f)
                                {
                                    telemetry[i].goalFlipFlopCount++;
                                }
                            }
                            lastGoals[i] = g;
                            lastGoalTimes[i] = t;
                        }

                        // KeepDistance entries & exits
                        bool inKd = (g == EnumUtilGoal.KeepDistance);
                        if (inKd && !wasInKeepDistance[i])
                        {
                            telemetry[i].keepDistanceEntries++;
                            wasInKeepDistance[i] = true;
                        }
                        else if (!inKd && wasInKeepDistance[i])
                        {
                            telemetry[i].keepDistanceExits++;
                            wasInKeepDistance[i] = false;
                        }

                        // CannotCast yield
                        if (adv.AI.CannotCast)
                        {
                            telemetry[i].cannotCastYieldObserved = true;
                        }
                    }

                    // Out of combat regen
                    if (adv.Stats != null && adv.Stats.IsOutOfCombat)
                    {
                        telemetry[i].outOfCombatRegenObserved = true;
                    }

                    // Movement jitter & stuck check
                    if (adv.Agent != null && adv.Agent.isActiveAndEnabled && adv.Agent.isOnNavMesh)
                    {
                        float delta = (pos - lastPositions[i]).magnitude;
                        if (adv.Agent.hasPath && delta < 0.01f)
                        {
                            samePosTimes[i] += dt;
                            if (samePosTimes[i] > 1.0f)
                            {
                                telemetry[i].stuckOrIdleDuration += dt;
                            }
                        }
                        else
                        {
                            samePosTimes[i] = 0f;
                        }

                        if (adv.Agent.velocity.sqrMagnitude > 1.0f && delta < 0.005f)
                        {
                            telemetry[i].jitterCount++;
                        }
                    }
                    lastPositions[i] = pos;
                }

                yield return null;
            }

            if (runner.State == BattleSimulationTestRunner.SimulationState.Running && runner.ElapsedTime >= timeout)
            {
                runner.EndSimulation(BattleSimulationTestRunner.BattleOutcome.Timeout);
            }

            // Sync final damage & stats
            for (int i = 0; i < unitCount; i++)
            {
                var adv = adventurers[i];
                if (adv != null)
                {
                    telemetry[i].damageDealt = adv.DamageDealt;
                    telemetry[i].damageTaken = adv.DamageTaken;
                    telemetry[i].healingReceivedAmount = adv.HealingReceived;
                    telemetry[i].healingDoneAmount = adv.SkillHealingReceived;
                    telemetry[i].isAlive = adv.IsAlive;
                }

                if (telemetry[i].goalFlipFlopCount > 8)
                {
                    run.anomalies.Add($"Excessive flip-flop ({telemetry[i].goalFlipFlopCount} times) on {telemetry[i].characterId}");
                }
                if (telemetry[i].jitterCount > 20)
                {
                    run.anomalies.Add($"High jitter count ({telemetry[i].jitterCount}) on {telemetry[i].characterId}");
                }
                if (telemetry[i].stuckOrIdleDuration > 4.0f)
                {
                    run.anomalies.Add($"Stuck or idle for {telemetry[i].stuckOrIdleDuration:F1}s on {telemetry[i].characterId}");
                }

                run.units.Add(telemetry[i]);
            }

            run.duration = runner.ElapsedTime;
            run.outcome = runner.CurrentOutcome;

            if (capturePerf)
            {
                long gcAfter = GC.GetTotalMemory(false);
                run.gcAllocBytes = Math.Max(0, gcAfter - gcBefore);
                run.avgFrameTimeMs = frameCount > 0 ? (frameTimeSum / frameCount) * 1000f : 0f;
            }

            runner.ResetSimulation(resetTimeScale: false);
            yield return null;
        }

        public static void SaveCsvDefault()
        {
            WriteTelemetryToCsv("docs/testing/combat_sandbox_pass_telemetry.csv");
        }

        public static void WriteTelemetryToCsv(string relativePath)
        {
            var sb = new StringBuilder();

            foreach (var run in AllPassResults)
            {
                foreach (var u in run.units)
                {
                    sb.AppendLine(string.Format("{0},\"{1}\",{2},{3},{4},{5:F2},{6},\"{7}\",{8},{9:F1},{10:F1},{11},{12},{13:F1},{14:F1},{15},{16:F2},{17:F2},{18},{19},{20},{21:F2},{22},{23},{24}",
                        run.scenarioCode,
                        run.scenarioName,
                        run.seed,
                        run.rankLabel,
                        run.outcome,
                        run.duration,
                        u.characterId,
                        u.actualFunction,
                        u.isAlive,
                        u.damageDealt,
                        u.damageTaken,
                        u.castsCount,
                        u.healsDoneCount,
                        u.healingDoneAmount,
                        u.healingReceivedAmount,
                        u.itemUsesCount,
                        u.timeToFirstTarget,
                        u.timeToFirstAttack,
                        u.goalFlipFlopCount,
                        u.keepDistanceEntries,
                        u.keepDistanceExits,
                        u.stuckOrIdleDuration,
                        u.jitterCount,
                        u.outOfCombatRegenObserved,
                        u.cannotCastYieldObserved));
                }
            }

            string fullPath = Path.Combine(Application.dataPath, "..", relativePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[CombatSandboxPassExecutor] Saved telemetry CSV to {fullPath}");
        }
    }
}
