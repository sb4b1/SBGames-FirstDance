using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    /// <summary>
    /// DevTool Panel 1: Scenario Runner.
    /// Allows picking a scenario, running it, resetting, cycling speed (1x/2x/5x), and viewing pass/fail status.
    /// </summary>
    public class ScenarioRunnerPanel : MonoBehaviour, IDevToolPanel
    {
        [SerializeField] private DevTool targetDevTool;
        [SerializeField] private SandboxRunnerBase runner;

        public string PanelId => "ScenarioRunner";
        public string PanelTitle => "Scenario Runner";
        public int Order => 10;

        private int _selectedScenarioIndex = 0;
        private Vector2 _scrollPos;

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
            if (runner == null)
            {
                GUILayout.Label("<b>Scenario Runner:</b> <color=red>No SandboxRunner found in scene.</color>");
                if (GUILayout.Button("Find Runner"))
                {
                    runner = FindAnyObjectByType<SandboxRunnerBase>();
                }
                return;
            }

            GUILayout.BeginVertical();

            GUILayout.Label($"<b>Sandbox:</b> {runner.SandboxId} | <b>Status:</b> {(runner.IsRunning ? "<color=yellow>RUNNING</color>" : "<color=green>IDLE</color>")}");

            // Speed controls
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Speed:</b>", GUILayout.Width(50f));
            if (GUILayout.Button("1x", GUILayout.Width(45f))) runner.TimeScale = 1f;
            if (GUILayout.Button("2x", GUILayout.Width(45f))) runner.TimeScale = 2f;
            if (GUILayout.Button("5x", GUILayout.Width(45f))) runner.TimeScale = 5f;
            GUILayout.Label($"Current: {Time.timeScale:F1}x");
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            var scenarios = runner.AvailableScenarios;
            if (scenarios != null && scenarios.Count > 0)
            {
                string[] names = new string[scenarios.Count];
                for (int i = 0; i < scenarios.Count; i++)
                {
                    names[i] = string.IsNullOrEmpty(scenarios[i].DisplayName) ? scenarios[i].ScenarioId : $"{scenarios[i].ScenarioId} - {scenarios[i].DisplayName}";
                }

                GUILayout.Label("<b>Select Scenario:</b>");
                _selectedScenarioIndex = Mathf.Clamp(_selectedScenarioIndex, 0, scenarios.Count - 1);
                _selectedScenarioIndex = GUILayout.SelectionGrid(_selectedScenarioIndex, names, 1);

                GUILayout.Space(4f);
                var sel = scenarios[_selectedScenarioIndex];
                if (sel != null && !string.IsNullOrEmpty(sel.Description))
                {
                    GUILayout.Label($"<i>{sel.Description}</i>", GUI.skin.box);
                }

                GUILayout.BeginHorizontal();
                GUI.enabled = !runner.IsRunning;
                if (GUILayout.Button("Run Selected", GUILayout.Height(28f)))
                {
                    runner.StartCoroutine(runner.RunScenarioRoutine(sel, null));
                }
                if (GUILayout.Button("Run All Scenarios", GUILayout.Height(28f)))
                {
                    runner.StartCoroutine(runner.RunAllScenariosRoutine(null));
                }
                GUI.enabled = true;

                if (GUILayout.Button("Reset", GUILayout.Height(28f), GUILayout.Width(70f)))
                {
                    runner.ResetSandbox();
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("<i>No scenarios available on runner.</i>");
                if (GUILayout.Button("Reset Sandbox"))
                {
                    runner.ResetSandbox();
                }
            }

            // Results preview
            if (runner.LastReport != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"<b>Last Run Results:</b> Passed: {runner.LastReport.PassedRuns}/{runner.LastReport.TotalRuns}");
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(120f));
                foreach (var res in runner.LastReport.scenarioResults)
                {
                    string color = res.IsPass ? "green" : "red";
                    GUILayout.Label($"• <color={color}>[{res.outcome}]</color> {res.scenarioId} ({res.durationSeconds:F2}s)");
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }
    }
}
