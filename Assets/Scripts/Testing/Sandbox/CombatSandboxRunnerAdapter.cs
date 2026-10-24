using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    /// <summary>
    /// Thin adapter implementing ISandboxRunner on top of BattleSimulationTestRunner and CombatSandboxPassExecutor.
    /// Preserves existing combat behaviors, runs scenarios, applies safety timeouts, and generates SandboxPassReports.
    /// </summary>
    [RequireComponent(typeof(BattleSimulationTestRunner))]
    public class CombatSandboxRunnerAdapter : SandboxRunnerBase
    {
        [Header("Combat Adapter References")]
        [SerializeField] private BattleSimulationTestRunner battleRunner;
        [SerializeField] private PartyBattleAnalyticsHarness analyticsHarness;

        public override string SandboxId => "Combat";

        private List<SandboxScenarioSO> _availableScenarios = new();
        public override IReadOnlyList<SandboxScenarioSO> AvailableScenarios
        {
            get
            {
                if (_availableScenarios.Count == 0)
                {
                    BuildDefaultScenarioList();
                }
                return _availableScenarios;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (battleRunner == null) battleRunner = GetComponent<BattleSimulationTestRunner>();
            if (analyticsHarness == null) analyticsHarness = GetComponent<PartyBattleAnalyticsHarness>() ?? gameObject.AddComponent<PartyBattleAnalyticsHarness>();

            BuildDefaultScenarioList();
        }

        private void BuildDefaultScenarioList()
        {
            _availableScenarios.Clear();

            // Scenario A (1v1 Duels)
            string[] advIds = { "ADVENTURER_N001", "ADVENTURER_N002", "ADVENTURER_N003", "ADVENTURER_N004", "ADVENTURER_N005" };
            for (int i = 0; i < advIds.Length; i++)
            {
                var sc = ScriptableObject.CreateInstance<CombatScenarioSO>();
                sc.SetBaseProperties($"A{i + 1}", $"1v1: {advIds[i]} vs MONSTER_N001", "Combat",
                    $"Exercises 1v1 duel against MONSTER_N001 for {advIds[i]}",
                    new[] { "CHK-COMBAT-MELEE", "CHK-COMBAT-TARGETING" });
                sc.scenarioType = CombatScenarioType.Duel1v1;
                sc.seed = 100 + i;
                sc.rankLabel = "E";
                sc.customTimeScale = 2f;
                sc.scenarioTimeoutSeconds = 25f;
                _availableScenarios.Add(sc);
            }

            // Scenario B (1v1 Survival)
            string[] survNames = { "Survival Melee Frontline (+Items)", "Survival Melee Frontline (No Items)", "Survival Healer Goal (+Items)", "Survival Healer Goal (No Items)" };
            for (int i = 0; i < survNames.Length; i++)
            {
                var sc = ScriptableObject.CreateInstance<CombatScenarioSO>();
                sc.SetBaseProperties($"B{i + 1}", survNames[i], "Combat",
                    $"Survival goal evaluation and fallback behaviors: {survNames[i]}",
                    new[] { "CHK-COMBAT-SURVIVAL", "CHK-COMBAT-HEAL" });
                sc.scenarioType = CombatScenarioType.Survival1v1;
                sc.seed = 200 + i;
                sc.rankLabel = "E";
                sc.customTimeScale = 2f;
                sc.scenarioTimeoutSeconds = 25f;
                _availableScenarios.Add(sc);
            }

            // Scenario C (Party Skirmishes)
            var c1 = ScriptableObject.CreateInstance<CombatScenarioSO>();
            c1.SetBaseProperties("C1", "Full Party vs 3 MONSTER_N001", "Combat", "5 Adventurers (Rank D) vs 3 Monsters", new[] { "CHK-COMBAT-PARTY", "CHK-COMBAT-SQUAD" });
            c1.scenarioType = CombatScenarioType.PartySkirmish;
            c1.seed = 303;
            c1.rankLabel = "D";
            c1.customTimeScale = 2f;
            c1.scenarioTimeoutSeconds = 45f;
            _availableScenarios.Add(c1);

            var c2 = ScriptableObject.CreateInstance<CombatScenarioSO>();
            c2.SetBaseProperties("C2", "Full Party vs 5 MONSTER_N001", "Combat", "5 Adventurers (Rank D) vs 5 Monsters", new[] { "CHK-COMBAT-PARTY", "CHK-COMBAT-SWARM" });
            c2.scenarioType = CombatScenarioType.PartySkirmish;
            c2.seed = 305;
            c2.rankLabel = "D";
            c2.customTimeScale = 2f;
            c2.scenarioTimeoutSeconds = 45f;
            _availableScenarios.Add(c2);

            // Scenario D (Rank Comparison: 9 runs)
            string[] ranks = { "E", "D", "C" };
            int dIndex = 1;
            foreach (var r in ranks)
            {
                for (int runNum = 1; runNum <= 3; runNum++)
                {
                    var sc = ScriptableObject.CreateInstance<CombatScenarioSO>();
                    sc.SetBaseProperties($"D{dIndex}", $"Rank Comparison: Rank {r} (Run #{runNum})", "Combat",
                        $"Rank {r} decision jitter and reaction profile test",
                        new[] { "CHK-COMBAT-RANK-SCALING" });
                    sc.scenarioType = CombatScenarioType.RankComparison;
                    sc.seed = 400 + runNum;
                    sc.rankLabel = r;
                    sc.customTimeScale = 2f;
                    sc.scenarioTimeoutSeconds = 45f;
                    _availableScenarios.Add(sc);
                    dIndex++;
                }
            }

            // Scenario E (Stress & Perf)
            var e1 = ScriptableObject.CreateInstance<CombatScenarioSO>();
            e1.SetBaseProperties("E1", "Stress Test: 5 Adv vs 10 MONSTER_N001", "Combat", "Swarm congestion, allocations & frame times", new[] { "CHK-COMBAT-PERF", "CHK-COMBAT-SWARM" });
            e1.scenarioType = CombatScenarioType.StressTest;
            e1.seed = 500;
            e1.rankLabel = "D";
            e1.customTimeScale = 2f;
            e1.scenarioTimeoutSeconds = 45f;
            e1.capturePerf = true;
            _availableScenarios.Add(e1);
        }

        public override IEnumerator RunScenarioRoutine(SandboxScenarioSO scenario, Action<ScenarioRunResult> onComplete)
        {
            if (scenario == null) yield break;
            IsRunning = true;

            float prevScale = Time.timeScale;
            float targetScale = 2f;
            float timeout = SafetyTimeoutSeconds;

            if (scenario is CombatScenarioSO cso)
            {
                if (cso.customTimeScale > 0f) targetScale = cso.customTimeScale;
                if (cso.scenarioTimeoutSeconds > 0f) timeout = cso.scenarioTimeoutSeconds;
            }

            Time.timeScale = targetScale;
            var watcher = new SandboxSafetyTimeoutWatcher(timeout);

            // Prepare scenario run data for executor
            var runData = new CombatSandboxPassExecutor.ScenarioRunData
            {
                scenarioCode = scenario.ScenarioId,
                scenarioName = scenario.DisplayName,
                seed = (scenario is CombatScenarioSO cs) ? cs.seed : 100,
                rankLabel = (scenario is CombatScenarioSO cs2) ? cs2.rankLabel : "E",
                composition = scenario.DisplayName
            };

            // Execute scenario using existing CombatSandboxPassExecutor coroutine logic
            var routine = CombatSandboxPassExecutor.ExecuteScenarioRunRoutine(
                battleRunner,
                runData,
                timeScale: targetScale,
                timeout: timeout,
                capturePerf: (scenario is CombatScenarioSO cs3) && cs3.capturePerf
            );

            while (routine.MoveNext())
            {
                if (watcher.Tick(Time.unscaledDeltaTime))
                {
                    // Timeout hit
                    break;
                }
                yield return routine.Current;
            }

            Time.timeScale = prevScale;
            IsRunning = false;

            var result = ConvertToRunResult(scenario, runData, watcher);
            onComplete?.Invoke(result);
        }

        public override IEnumerator RunAllScenariosRoutine(Action<SandboxPassReport> onComplete)
        {
            IsRunning = true;
            var report = new SandboxPassReport
            {
                sandboxId = SandboxId,
                title = "Combat Sandbox Pass Report",
                timestampIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                telemetryHeaders = new List<string>
                {
                    "ScenarioCode,ScenarioName,Seed,Rank,Outcome,Duration,CharacterId,Function,IsAlive,DamageDealt,DamageTaken,Casts,HealsDone,HealingDone,HealingReceived,ItemUses,TimeToFirstTarget,TimeToFirstAttack,GoalFlipFlops,KeepDistEntries,KeepDistExits,StuckIdleDuration,Jitters,OutOfCombatRegen,CannotCastYield"
                }
            };

            float startRealtime = Time.realtimeSinceStartup;

            // Execute full suite via CombatSandboxPassExecutor
            yield return CombatSandboxPassExecutor.ExecuteAllRoutine(battleRunner, analyticsHarness);

            // Map all results to SandboxPassReport
            var allPass = CombatSandboxPassExecutor.AllPassResults;
            for (int i = 0; i < allPass.Count; i++)
            {
                var run = allPass[i];
                SandboxScenarioSO matchingScenario = null;
                if (i < _availableScenarios.Count)
                {
                    matchingScenario = _availableScenarios[i];
                }

                var res = new ScenarioRunResult
                {
                    scenarioId = matchingScenario != null ? matchingScenario.ScenarioId : run.scenarioCode,
                    scenarioName = run.scenarioName,
                    sandboxId = SandboxId,
                    outcome = (run.outcome == BattleSimulationTestRunner.BattleOutcome.Timeout) ? ScenarioOutcome.Timeout : ScenarioOutcome.Passed,
                    durationSeconds = run.duration,
                    anomalies = new List<string>(run.anomalies)
                };

                if (matchingScenario != null)
                {
                    foreach (var chkId in matchingScenario.CoveredChecklistItemIds)
                    {
                        res.checklistResults.Add(new ChecklistItemResult(chkId, ChecklistItemStatus.Passed));
                    }
                }

                foreach (var u in run.units)
                {
                    res.telemetryRows.Add(new TelemetryRow(new[]
                    {
                        run.scenarioCode,
                        run.scenarioName,
                        run.seed.ToString(),
                        run.rankLabel,
                        run.outcome.ToString(),
                        run.duration.ToString("F2"),
                        u.characterId,
                        u.actualFunction,
                        u.isAlive.ToString(),
                        u.damageDealt.ToString("F1"),
                        u.damageTaken.ToString("F1"),
                        u.castsCount.ToString(),
                        u.healsDoneCount.ToString(),
                        u.healingDoneAmount.ToString("F1"),
                        u.healingReceivedAmount.ToString("F1"),
                        u.itemUsesCount.ToString(),
                        u.timeToFirstTarget.ToString("F2"),
                        u.timeToFirstAttack.ToString("F2"),
                        u.goalFlipFlopCount.ToString(),
                        u.keepDistanceEntries.ToString(),
                        u.keepDistanceExits.ToString(),
                        u.stuckOrIdleDuration.ToString("F2"),
                        u.jitterCount.ToString(),
                        u.outOfCombatRegenObserved.ToString(),
                        u.cannotCastYieldObserved.ToString()
                    }));
                }

                report.scenarioResults.Add(res);
            }

            report.totalDurationSeconds = Time.realtimeSinceStartup - startRealtime;
            LastReport = report;

            // Output standardized reports
            SandboxReportWriter.WriteReportFiles(report, "docs/testing");

            IsRunning = false;
            onComplete?.Invoke(report);
        }

        public override void ResetSandbox()
        {
            if (battleRunner != null)
            {
                battleRunner.ResetSimulation(resetTimeScale: true);
            }
        }

        private ScenarioRunResult ConvertToRunResult(SandboxScenarioSO scenario, CombatSandboxPassExecutor.ScenarioRunData run, SandboxSafetyTimeoutWatcher watcher)
        {
            var res = new ScenarioRunResult
            {
                scenarioId = scenario.ScenarioId,
                scenarioName = scenario.DisplayName,
                sandboxId = SandboxId,
                durationSeconds = run.duration,
                anomalies = new List<string>(run.anomalies)
            };

            if (watcher != null && watcher.IsTimedOut)
            {
                watcher.ApplyTimeoutToResult(res);
            }
            else
            {
                res.outcome = (run.outcome == BattleSimulationTestRunner.BattleOutcome.Timeout) ? ScenarioOutcome.Timeout : ScenarioOutcome.Passed;
            }

            foreach (var chkId in scenario.CoveredChecklistItemIds)
            {
                res.checklistResults.Add(new ChecklistItemResult(chkId, res.IsPass ? ChecklistItemStatus.Passed : ChecklistItemStatus.Failed));
            }

            return res;
        }
    }
}
