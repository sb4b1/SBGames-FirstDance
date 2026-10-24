using System;
using System.Collections.Generic;

namespace SBGames.Testing.Sandbox
{
    public enum ChecklistItemStatus
    {
        NotChecked = 0,
        Passed = 1,
        Failed = 2
    }

    public enum ScenarioOutcome
    {
        Unset = 0,
        Passed = 1,
        Failed = 2,
        Timeout = 3,
        Error = 4
    }

    [Serializable]
    public class ChecklistItemResult
    {
        public string itemId;
        public ChecklistItemStatus status;
        public string message;

        public ChecklistItemResult() { }

        public ChecklistItemResult(string id, ChecklistItemStatus status, string message = null)
        {
            this.itemId = id;
            this.status = status;
            this.message = message;
        }
    }

    [Serializable]
    public class TelemetryRow
    {
        public List<string> values = new();

        public TelemetryRow() { }

        public TelemetryRow(IEnumerable<string> cols)
        {
            if (cols != null) values.AddRange(cols);
        }
    }

    [Serializable]
    public class ScenarioRunResult
    {
        public string scenarioId;
        public string scenarioName;
        public string sandboxId;
        public ScenarioOutcome outcome = ScenarioOutcome.Unset;
        public float durationSeconds;
        public string failureReason;
        public List<ChecklistItemResult> checklistResults = new();
        public List<TelemetryRow> telemetryRows = new();
        public List<string> anomalies = new();

        public bool IsPass => outcome == ScenarioOutcome.Passed;
    }

    [Serializable]
    public class SandboxPassReport
    {
        public string sandboxId;
        public string title;
        public string timestampIso;
        public float totalDurationSeconds;
        public List<string> telemetryHeaders = new();
        public List<ScenarioRunResult> scenarioResults = new();

        public int TotalRuns => scenarioResults.Count;
        public int PassedRuns
        {
            get
            {
                int count = 0;
                for (int i = 0; i < scenarioResults.Count; i++)
                {
                    if (scenarioResults[i].IsPass) count++;
                }
                return count;
            }
        }
        public int FailedRuns => TotalRuns - PassedRuns;
    }
}
