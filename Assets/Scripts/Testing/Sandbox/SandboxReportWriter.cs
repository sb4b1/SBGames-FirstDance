using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SBGames.Testing.Sandbox
{
    public static class SandboxReportWriter
    {
        public static string GenerateMarkdown(SandboxPassReport report)
        {
            if (report == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"# {report.title ?? "Sandbox Pass Report"}");
            sb.AppendLine();
            sb.AppendLine($"- **Sandbox ID:** `{report.sandboxId}`");
            sb.AppendLine($"- **Timestamp:** {report.timestampIso}");
            sb.AppendLine($"- **Total Runs:** {report.TotalRuns}");
            sb.AppendLine($"- **Passed:** {report.PassedRuns}");
            sb.AppendLine($"- **Failed:** {report.FailedRuns}");
            sb.AppendLine($"- **Total Duration:** {report.totalDurationSeconds:F2}s");
            sb.AppendLine();

            sb.AppendLine("## Scenario Results");
            sb.AppendLine();
            sb.AppendLine("| Scenario ID | Name | Outcome | Duration (s) | Checklist Covered | Anomalies / Reason |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

            // Deterministic stable sorting by scenarioId
            var sortedScenarios = new List<ScenarioRunResult>(report.scenarioResults);
            sortedScenarios.Sort((a, b) => string.CompareOrdinal(a.scenarioId, b.scenarioId));

            foreach (var run in sortedScenarios)
            {
                string checklistSummary = FormatChecklistSummary(run.checklistResults);
                string reason = string.IsNullOrEmpty(run.failureReason) 
                    ? (run.anomalies.Count > 0 ? string.Join("; ", run.anomalies) : "None")
                    : run.failureReason;

                sb.AppendLine($"| `{run.scenarioId}` | {EscapePipes(run.scenarioName)} | **{run.outcome}** | {run.durationSeconds:F2}s | {checklistSummary} | {EscapePipes(reason)} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Checklist Items Matrix");
            sb.AppendLine();
            sb.AppendLine("| Checklist Item ID | Status | Checked In Scenarios | Notes |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");

            var checklistMap = AggregateChecklistItems(sortedScenarios);
            var sortedKeys = new List<string>(checklistMap.Keys);
            sortedKeys.Sort(StringComparer.Ordinal);

            foreach (var key in sortedKeys)
            {
                var entry = checklistMap[key];
                string scens = string.Join(", ", entry.scenarios);
                sb.AppendLine($"| `{key}` | **{entry.status}** | {scens} | {EscapePipes(entry.notes)} |");
            }

            return sb.ToString();
        }

        public static string GenerateCsv(SandboxPassReport report)
        {
            if (report == null) return string.Empty;

            var sb = new StringBuilder();

            if (report.telemetryHeaders != null && report.telemetryHeaders.Count > 0)
            {
                sb.AppendLine(string.Join(",", report.telemetryHeaders));
            }

            var sortedScenarios = new List<ScenarioRunResult>(report.scenarioResults);
            sortedScenarios.Sort((a, b) => string.CompareOrdinal(a.scenarioId, b.scenarioId));

            foreach (var run in sortedScenarios)
            {
                if (run.telemetryRows == null) continue;
                foreach (var row in run.telemetryRows)
                {
                    if (row.values == null) continue;
                    for (int i = 0; i < row.values.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(FormatCsvCell(row.values[i]));
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public static void WriteReportFiles(SandboxPassReport report, string basePath = "docs/testing")
        {
            if (report == null) return;
            string prefix = string.IsNullOrEmpty(report.sandboxId) ? "sandbox" : report.sandboxId.ToLowerInvariant();

            string mdContent = GenerateMarkdown(report);
            string csvContent = GenerateCsv(report);

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string mdPath = Path.Combine(basePath, $"{prefix}_pass_report.md");
            string csvPath = Path.Combine(basePath, $"{prefix}_pass_telemetry.csv");

            File.WriteAllText(mdPath, mdContent, Encoding.UTF8);
            if (!string.IsNullOrEmpty(csvContent))
            {
                File.WriteAllText(csvPath, csvContent, Encoding.UTF8);
            }
        }

        private static string FormatCsvCell(string cell)
        {
            if (cell == null) return string.Empty;
            if (cell.Contains(",") || cell.Contains("\"") || cell.Contains("\n") || cell.Contains("\r"))
            {
                return $"\"{cell.Replace("\"", "\"\"")}\"";
            }
            return cell;
        }

        private static string EscapePipes(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatChecklistSummary(List<ChecklistItemResult> list)
        {
            if (list == null || list.Count == 0) return "-";
            var parts = new List<string>(list.Count);
            foreach (var item in list)
            {
                parts.Add($"{item.itemId}:{item.status}");
            }
            return string.Join("; ", parts);
        }

        private class AggregatedItem
        {
            public ChecklistItemStatus status = ChecklistItemStatus.NotChecked;
            public List<string> scenarios = new();
            public string notes = string.Empty;
        }

        private static Dictionary<string, AggregatedItem> AggregateChecklistItems(List<ScenarioRunResult> runs)
        {
            var map = new Dictionary<string, AggregatedItem>(StringComparer.Ordinal);
            foreach (var run in runs)
            {
                if (run.checklistResults == null) continue;
                foreach (var item in run.checklistResults)
                {
                    if (string.IsNullOrEmpty(item.itemId)) continue;
                    if (!map.TryGetValue(item.itemId, out var agg))
                    {
                        agg = new AggregatedItem();
                        map[item.itemId] = agg;
                    }
                    if (!agg.scenarios.Contains(run.scenarioId))
                    {
                        agg.scenarios.Add(run.scenarioId);
                    }
                    if (item.status == ChecklistItemStatus.Failed)
                    {
                        agg.status = ChecklistItemStatus.Failed;
                        if (!string.IsNullOrEmpty(item.message))
                        {
                            agg.notes = string.IsNullOrEmpty(agg.notes) ? item.message : $"{agg.notes}; {item.message}";
                        }
                    }
                    else if (item.status == ChecklistItemStatus.Passed && agg.status != ChecklistItemStatus.Failed)
                    {
                        agg.status = ChecklistItemStatus.Passed;
                    }
                }
            }
            return map;
        }
    }
}
