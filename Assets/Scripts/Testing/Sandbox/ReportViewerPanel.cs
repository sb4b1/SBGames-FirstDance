using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SBGames.Testing.Sandbox
{
    /// <summary>
    /// DevTool Panel 4: Report Viewer.
    /// Displays the last run's results, checklist coverage, and Validation Badges / findings.
    /// </summary>
    public class ReportViewerPanel : MonoBehaviour, IDevToolPanel
    {
        [SerializeField] private DevTool targetDevTool;
        [SerializeField] private SandboxRunnerBase runner;

        public string PanelId => "ReportViewer";
        public string PanelTitle => "Report Viewer";
        public int Order => 40;

        private Vector2 _reportScrollPos;
        private Vector2 _validationScrollPos;
        private int _subTab = 0;
        private readonly string[] _subTabs = { "Sandbox Report", "Validation Findings" };

        private void OnEnable()
        {
            if (targetDevTool == null)
            {
                targetDevTool = DevTool.Instance ?? FindAnyObjectByType<DevTool>();
            }
            if (targetDevTool != null)
            {
                targetDevTool.RegisterPanel(this);
            }
        }

        private void OnDisable()
        {
            if (targetDevTool != null)
            {
                targetDevTool.UnregisterPanel(this);
            }
        }

        public void OnPanelRegistered(DevTool devTool)
        {
            targetDevTool = devTool;
            if (runner == null)
            {
                runner = FindAnyObjectByType<SandboxRunnerBase>();
            }
        }

        public void OnPanelUpdate()
        {
            if (runner == null)
            {
                runner = FindAnyObjectByType<SandboxRunnerBase>();
            }
        }

        public void DrawPanelGUI()
        {
            GUILayout.BeginVertical();

            _subTab = GUILayout.Toolbar(_subTab, _subTabs);
            GUILayout.Space(6f);

            if (_subTab == 0)
            {
                DrawReportView();
            }
            else
            {
                DrawValidationView();
            }

            GUILayout.EndVertical();
        }

        private void DrawReportView()
        {
            if (runner == null)
            {
                runner = FindAnyObjectByType<SandboxRunnerBase>();
            }

            var report = runner != null ? runner.LastReport : null;
            if (report == null)
            {
                GUILayout.Label("<i>No report available yet. Run a scenario or test pass.</i>");
                return;
            }

            GUILayout.Label($"<b>Report:</b> {report.title} ({report.timestampIso})");
            GUILayout.Label($"<b>Total Runs:</b> {report.TotalRuns} | <b>Passed:</b> <color=green>{report.PassedRuns}</color> | <b>Failed:</b> <color=red>{report.FailedRuns}</color> | <b>Duration:</b> {report.totalDurationSeconds:F2}s");

            _reportScrollPos = GUILayout.BeginScrollView(_reportScrollPos, GUILayout.Height(180f));
            foreach (var run in report.scenarioResults)
            {
                string color = run.IsPass ? "green" : "red";
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<color={color}><b>[{run.outcome}]</b></color> <b>{run.scenarioId}: {run.scenarioName}</b> ({run.durationSeconds:F2}s)");
                if (!string.IsNullOrEmpty(run.failureReason))
                {
                    GUILayout.Label($"<color=red>Reason: {run.failureReason}</color>");
                }
                if (run.checklistResults != null && run.checklistResults.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Checklist: ", GUILayout.Width(65f));
                    foreach (var c in run.checklistResults)
                    {
                        string cColor = c.status == ChecklistItemStatus.Passed ? "green" : (c.status == ChecklistItemStatus.Failed ? "red" : "grey");
                        GUILayout.Label($"<color={cColor}>[{c.itemId}]</color>", GUILayout.Width(60f));
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawValidationView()
        {
#if UNITY_EDITOR
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>AI Setup & Data Validation</b>");
            if (GUILayout.Button("Run Audit", GUILayout.Width(90f)))
            {
                RunValidationViaReflection();
            }
            GUILayout.EndHorizontal();

            var findings = GetValidationFindingsViaReflection();
            if (findings == null || findings.Count == 0)
            {
                GUILayout.Label("<color=green><b>0 findings recorded. All systems valid!</b></color>");
            }
            else
            {
                GUILayout.Label($"<b>Total Findings:</b> {findings.Count}");
                _validationScrollPos = GUILayout.BeginScrollView(_validationScrollPos, GUILayout.Height(180f));
                foreach (var item in findings)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(item);
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
            }
#else
            GUILayout.Label("Validation Service findings are only accessible inside the Unity Editor.");
#endif
        }

#if UNITY_EDITOR
        private static void RunValidationViaReflection()
        {
            var editorAssembly = System.Reflection.Assembly.Load("Assembly-CSharp-Editor");
            if (editorAssembly == null) return;
            var type = editorAssembly.GetType("SBGames.Editor.Validation.ValidationService");
            var method = type?.GetMethod("RunFullValidation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { null });
        }

        private static List<string> GetValidationFindingsViaReflection()
        {
            var result = new List<string>();
            var editorAssembly = System.Reflection.Assembly.Load("Assembly-CSharp-Editor");
            if (editorAssembly == null) return result;
            var type = editorAssembly.GetType("SBGames.Editor.Validation.ValidationService");
            var method = type?.GetMethod("GetCachedFindings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var list = method?.Invoke(null, null) as System.Collections.IEnumerable;
            if (list != null)
            {
                foreach (var f in list)
                {
                    if (f == null) continue;
                    var fType = f.GetType();
                    var sev = fType.GetField("Severity")?.GetValue(f);
                    var cat = fType.GetField("Category")?.GetValue(f);
                    var name = fType.GetField("TargetName")?.GetValue(f);
                    var msg = fType.GetField("Message")?.GetValue(f);
                    result.Add($"[{sev}] [{cat}] {name}: {msg}");
                }
            }
            return result;
        }
#endif
    }
}
