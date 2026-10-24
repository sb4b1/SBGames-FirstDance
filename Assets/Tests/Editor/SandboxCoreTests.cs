using System;
using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Testing.Sandbox;

namespace SBGames.Tests
{
    [TestFixture]
    public class SandboxCoreTests
    {
        [Test]
        public void ScenarioRunResult_PassAndFailCalculation_IsAccurate()
        {
            var resPass = new ScenarioRunResult
            {
                scenarioId = "S1",
                outcome = ScenarioOutcome.Passed,
                durationSeconds = 5.2f
            };
            Assert.IsTrue(resPass.IsPass);

            var resFail = new ScenarioRunResult
            {
                scenarioId = "S2",
                outcome = ScenarioOutcome.Failed,
                durationSeconds = 12.0f
            };
            Assert.IsFalse(resFail.IsPass);

            var report = new SandboxPassReport
            {
                sandboxId = "TestSandbox",
                title = "Unit Test Report",
                scenarioResults = new List<ScenarioRunResult> { resPass, resFail }
            };

            Assert.AreEqual(2, report.TotalRuns);
            Assert.AreEqual(1, report.PassedRuns);
            Assert.AreEqual(1, report.FailedRuns);
        }

        [Test]
        public void SandboxReportWriter_GeneratesDeterministicMarkdownAndCsv()
        {
            var runB = new ScenarioRunResult
            {
                scenarioId = "B1",
                scenarioName = "Scenario B",
                outcome = ScenarioOutcome.Passed,
                durationSeconds = 3.5f,
                checklistResults = new List<ChecklistItemResult>
                {
                    new ChecklistItemResult("CHK-2", ChecklistItemStatus.Passed),
                    new ChecklistItemResult("CHK-1", ChecklistItemStatus.Passed)
                },
                telemetryRows = new List<TelemetryRow>
                {
                    new TelemetryRow(new[] { "B", "Scenario B", "100", "Passed" })
                }
            };

            var runA = new ScenarioRunResult
            {
                scenarioId = "A1",
                scenarioName = "Scenario A",
                outcome = ScenarioOutcome.Failed,
                durationSeconds = 4.2f,
                failureReason = "Test failure",
                checklistResults = new List<ChecklistItemResult>
                {
                    new ChecklistItemResult("CHK-1", ChecklistItemStatus.Failed, "Expected win")
                },
                telemetryRows = new List<TelemetryRow>
                {
                    new TelemetryRow(new[] { "A", "Scenario A", "200", "Failed" })
                }
            };

            // Insert in reverse order (B then A)
            var report = new SandboxPassReport
            {
                sandboxId = "Combat",
                title = "Deterministic Test",
                timestampIso = "2026-10-25T00:00:00Z",
                totalDurationSeconds = 7.7f,
                telemetryHeaders = new List<string> { "Code,Name,Seed,Outcome" },
                scenarioResults = new List<ScenarioRunResult> { runB, runA }
            };

            string md = SandboxReportWriter.GenerateMarkdown(report);
            string csv = SandboxReportWriter.GenerateCsv(report);

            // In deterministic sorting, A1 must appear before B1 in the markdown table
            int idxA = md.IndexOf("`A1`", StringComparison.Ordinal);
            int idxB = md.IndexOf("`B1`", StringComparison.Ordinal);
            Assert.IsTrue(idxA >= 0 && idxB >= 0, "Both scenario IDs must be present in markdown");
            Assert.IsTrue(idxA < idxB, "Scenarios must be deterministically sorted by scenarioId ascending");

            // CSV also sorts deterministically: row for A must appear before row for B
            int idxCsvA = csv.IndexOf("Scenario A", StringComparison.Ordinal);
            int idxCsvB = csv.IndexOf("Scenario B", StringComparison.Ordinal);
            Assert.IsTrue(idxCsvA >= 0 && idxCsvB >= 0, "Both rows must be present in CSV");
            Assert.IsTrue(idxCsvA < idxCsvB, "Telemetry rows must be deterministically sorted by scenarioId");

            // Verify checklist aggregation reflects failure
            Assert.IsTrue(md.Contains("| `CHK-1` | **Failed** |"), "Aggregated status for CHK-1 must reflect failure");
            Assert.IsTrue(md.Contains("| `CHK-2` | **Passed** |"), "Aggregated status for CHK-2 must reflect passed");
        }

        [Test]
        public void SafetyTimeoutWatcher_ReportsHungRunAsFailure()
        {
            var watcher = new SandboxSafetyTimeoutWatcher(5.0f);
            Assert.IsFalse(watcher.IsTimedOut);

            // Tick 3 seconds
            bool timedOut1 = watcher.Tick(3.0f);
            Assert.IsFalse(timedOut1);
            Assert.IsFalse(watcher.IsTimedOut);
            Assert.AreEqual(3.0f, watcher.ElapsedSeconds, 0.001f);

            // Tick another 2.5 seconds (total 5.5s >= 5.0s)
            bool timedOut2 = watcher.Tick(2.5f);
            Assert.IsTrue(timedOut2);
            Assert.IsTrue(watcher.IsTimedOut);

            var runResult = new ScenarioRunResult
            {
                scenarioId = "HungScenario",
                scenarioName = "Infinite Loop Test",
                outcome = ScenarioOutcome.Unset
            };

            watcher.ApplyTimeoutToResult(runResult);

            Assert.AreEqual(ScenarioOutcome.Timeout, runResult.outcome);
            Assert.IsFalse(runResult.IsPass);
            Assert.IsTrue(runResult.failureReason.Contains("Safety timeout reached"));
        }

        [Test]
        public void SandboxHeadlessRunner_SetsExitCodeProperly()
        {
            SandboxHeadlessRunner.ExitBatch(0);
            Assert.AreEqual(0, SandboxHeadlessRunner.LastExitCode);

            SandboxHeadlessRunner.ExitBatch(3);
            Assert.AreEqual(3, SandboxHeadlessRunner.LastExitCode);
        }
    }
}
