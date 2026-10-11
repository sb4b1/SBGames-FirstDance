using UnityEngine;

namespace SBGames.Testing
{
    using Input = UnityEngine.Input;
    using KeyCode = UnityEngine.KeyCode;

    /// <summary>
    /// Minimal, high-visibility runtime diagnostic overlay for the Battle Simulation Test Harness.
    /// Operates via OnGUI for zero-asset setup and immediate functionality in any test scene.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class CombatTestHUD : MonoBehaviour
    {
        [Header("Target Runner Reference")]
        [Tooltip("The BattleSimulationTestRunner to monitor. Auto-resolved if null.")]
        [SerializeField] private BattleSimulationTestRunner runner;

        [Header("UI Appearance & Behavior")]
        [Tooltip("Key to toggle overlay visibility.")]
        [SerializeField] private UnityEngine.KeyCode toggleKey = UnityEngine.KeyCode.H;

        [Tooltip("Show individual unit HP bars and names in HUD.")]
        [SerializeField] private bool showUnitRosterList = true;

        [Tooltip("Screen position offset for the HUD panel.")]
        [SerializeField] private Vector2 hudPosition = new Vector2(16f, 16f);

        [SerializeField] private float hudWidth = 340f;

        private bool isVisible = true;
        private bool isMinimized = false;
        private Vector2 scrollPosition = Vector2.zero;

        // Custom GUI styles
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle statusStyle;
        private GUIStyle bannerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle unitLabelStyle;
        private Texture2D greenTex;
        private Texture2D redTex;
        private Texture2D darkBgTex;
        private Texture2D barBgTex;
        private bool stylesInitialized = false;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindAnyObjectByType<BattleSimulationTestRunner>();
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
            }

            if (runner == null) return;

            // Hotkey shortcuts
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.R))
            {
                runner.RestartSimulation();
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space))
            {
                runner.TogglePause();
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha1)) runner.SetTimeScale(1f);
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha2)) runner.SetTimeScale(2f);
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha5)) runner.SetTimeScale(5f);
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha0)) runner.SetTimeScale(10f);
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            EnsureStyles();

            if (runner == null)
            {
                DrawMissingRunnerWarning();
                return;
            }

            float currentWidth = hudWidth;
            float height = isMinimized ? 70f : (showUnitRosterList ? 580f : 290f);
            Rect panelRect = new Rect(hudPosition.x, hudPosition.y, currentWidth, height);

            GUILayout.BeginArea(panelRect, panelStyle);
            {
                DrawHeader();

                if (!isMinimized)
                {
                    DrawSimulationStatus();
                    DrawFactionScorecards();
                    DrawOutcomeBanner();
                    DrawControlButtons();

                    if (showUnitRosterList)
                    {
                        DrawCombatantRoster();
                    }
                }
            }
            GUILayout.EndArea();
        }

        #region GUI Layout Elements

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("⚔ Combat Sandbox Harness", headerStyle);
                GUILayout.FlexibleSpace();

                string minLabel = isMinimized ? "Expand" : "Min";
                if (GUILayout.Button(minLabel, GUILayout.Width(50), GUILayout.Height(22)))
                {
                    isMinimized = !isMinimized;
                }
            }
            GUILayout.EndHorizontal();

            if (isMinimized)
            {
                int monsters = runner.AliveMonstersCount;
                int adventurers = runner.AliveAdventurersCount;
                GUILayout.Label($"Time: {runner.ElapsedTime:F1}s | M: {monsters} vs A: {adventurers} | Speed: {Time.timeScale:F1}x", subHeaderStyle);
            }
        }

        private void DrawSimulationStatus()
        {
            GUILayout.BeginHorizontal();
            {
                string stateColor = runner.State switch
                {
                    BattleSimulationTestRunner.SimulationState.Running => "#00FF66",
                    BattleSimulationTestRunner.SimulationState.Paused => "#FFAA00",
                    BattleSimulationTestRunner.SimulationState.Completed => "#00CCFF",
                    _ => "#AAAAAA"
                };

                GUILayout.Label($"Status: <color={stateColor}><b>{runner.State.ToString().ToUpper()}</b></color>", statusStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Stopwatch: <b>{FormatTime(runner.ElapsedTime)}</b>", statusStyle);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawFactionScorecards()
        {
            GUILayout.Space(4f);

            // Monsters Bar
            int monstersAlive = runner.AliveMonstersCount;
            int monstersTotal = runner.InitialMonsters;
            float monsterPct = monstersTotal > 0 ? (float)monstersAlive / monstersTotal : 0f;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label($"<b>Monsters</b> ({monstersAlive}/{monstersTotal})", unitLabelStyle, GUILayout.Width(130));
                DrawProgressBar(monsterPct, new Color(0.2f, 0.85f, 0.3f));
            }
            GUILayout.EndHorizontal();

            // Adventurers Bar
            int advAlive = runner.AliveAdventurersCount;
            int advTotal = runner.InitialAdventurers;
            float advPct = advTotal > 0 ? (float)advAlive / advTotal : 0f;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label($"<b>Adventurers</b> ({advAlive}/{advTotal})", unitLabelStyle, GUILayout.Width(130));
                DrawProgressBar(advPct, new Color(0.9f, 0.25f, 0.25f));
            }
            GUILayout.EndHorizontal();
        }

        private void DrawOutcomeBanner()
        {
            if (runner.State != BattleSimulationTestRunner.SimulationState.Completed) return;

            GUILayout.Space(6f);
            string bannerText = runner.CurrentOutcome switch
            {
                BattleSimulationTestRunner.BattleOutcome.MonstersWin => "★ VICTORY: MONSTERS WIN ★",
                BattleSimulationTestRunner.BattleOutcome.AdventurersWin => "★ VICTORY: ADVENTURERS WIN ★",
                BattleSimulationTestRunner.BattleOutcome.Draw => "⚔ MUTUAL ANNIHILATION (DRAW) ⚔",
                BattleSimulationTestRunner.BattleOutcome.Timeout => "⏱ COMBAT TIMEOUT (STALEMATE) ⏱",
                _ => "SIMULATION ENDED"
            };

            GUI.color = runner.CurrentOutcome == BattleSimulationTestRunner.BattleOutcome.MonstersWin ? Color.green :
                        (runner.CurrentOutcome == BattleSimulationTestRunner.BattleOutcome.AdventurersWin ? Color.red : Color.yellow);
            GUILayout.Box(bannerText, bannerStyle, GUILayout.Height(30));
            GUI.color = Color.white;

            if (runner.LastReport.duration > 0f)
            {
                GUILayout.Label($"Time: {runner.LastReport.duration:F2}s | Casualties - M: {runner.LastReport.monsterCasualties}, A: {runner.LastReport.adventurerCasualties}", subHeaderStyle);
            }
        }

        private void DrawControlButtons()
        {
            GUILayout.Space(6f);

            // Primary Action Buttons
            GUILayout.BeginHorizontal();
            {
                if (runner.State == BattleSimulationTestRunner.SimulationState.Running)
                {
                    if (GUILayout.Button("🔄 Rerun (R)", buttonStyle, GUILayout.Height(26)))
                    {
                        runner.RestartSimulation();
                    }
                }
                else
                {
                    if (GUILayout.Button("▶ Start Simulation", buttonStyle, GUILayout.Height(26)))
                    {
                        runner.StartSimulation();
                    }
                }

                if (GUILayout.Button("⏹ Clear Board", buttonStyle, GUILayout.Height(26)))
                {
                    runner.ResetSimulation();
                }

                string pauseText = runner.State == BattleSimulationTestRunner.SimulationState.Paused ? "▶ Resume" : "⏸ Pause";
                if (GUILayout.Button(pauseText, buttonStyle, GUILayout.Height(26)))
                {
                    runner.TogglePause();
                }
            }
            GUILayout.EndHorizontal();

            // Time Scale Buttons
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Speed:", GUILayout.Width(45));

                DrawSpeedButton(1f, "1x");
                DrawSpeedButton(2f, "2x");
                DrawSpeedButton(5f, "5x");
                DrawSpeedButton(10f, "10x");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSpeedButton(float speed, string label)
        {
            bool isCurrent = Mathf.Approximately(Time.timeScale, speed);
            Color oldBg = GUI.backgroundColor;
            if (isCurrent) GUI.backgroundColor = Color.cyan;

            if (GUILayout.Button(label, GUILayout.Height(20)))
            {
                runner.SetTimeScale(speed);
            }

            GUI.backgroundColor = oldBg;
        }

        private void DrawCombatantRoster()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Active Combatants Status:", subHeaderStyle);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(210));
            {
                // Monsters
                if (runner.Monsters.Count > 0)
                {
                    GUILayout.Label("<color=#44FF44>--- Monsters ---</color>", unitLabelStyle);
                    for (int i = 0; i < runner.Monsters.Count; i++)
                    {
                        DrawCombatantRow(runner.Monsters[i], new Color(0.2f, 0.8f, 0.2f));
                    }
                }

                // Adventurers
                if (runner.Adventurers.Count > 0)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label("<color=#FF5555>--- Adventurers ---</color>", unitLabelStyle);
                    for (int i = 0; i < runner.Adventurers.Count; i++)
                    {
                        DrawCombatantRow(runner.Adventurers[i], new Color(0.9f, 0.3f, 0.3f));
                    }
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label("<color=#777777>Shortcuts: [R] Restart | [Space] Pause | [1/2/5/0] Speed | [H] Hide</color>", subHeaderStyle);
        }

        private void DrawCombatantRow(BattleSimulationTestRunner.TrackedCombatant combatant, Color barColor)
        {
            if (combatant == null) return;

            GUILayout.BeginHorizontal();
            {
                string statusText = combatant.IsAlive
                    ? $"<b>{combatant.GameObject.name}</b>"
                    : $"<s><color=#888888>{(combatant.GameObject != null ? combatant.GameObject.name : "Dead Unit")}</color></s>";

                GUILayout.Label(statusText, unitLabelStyle, GUILayout.Width(130));

                if (combatant.IsAlive)
                {
                    DrawProgressBar(combatant.HealthPercent, barColor, $"{combatant.CurrentLife:F0}/{combatant.MaxLife:F0}");
                }
                else
                {
                    DrawProgressBar(0f, Color.gray, "DEAD");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawProgressBar(float percent, Color fillColor, string text = "")
        {
            Rect barRect = GUILayoutUtility.GetRect(120, 16, GUILayout.ExpandWidth(true));

            // Background
            GUI.DrawTexture(barRect, barBgTex);

            // Fill
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(percent), barRect.height);
            Color oldColor = GUI.color;
            GUI.color = fillColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            // Optional centered text
            if (!string.IsNullOrEmpty(text))
            {
                GUIStyle textStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = Color.white }
                };
                GUI.Label(barRect, text, textStyle);
            }
        }

        private void DrawMissingRunnerWarning()
        {
            Rect rect = new Rect(16, 16, 320, 80);
            GUI.Box(rect, "<b>⚠ Combat Test HUD</b>\nNo BattleSimulationTestRunner found in scene.\nAdd BattleSimulationTestRunner component to use.", panelStyle);
        }

        #endregion

        #region Helpers & Styles

        private string FormatTime(float seconds)
        {
            int mins = (int)(seconds / 60f);
            float secs = seconds % 60f;
            return $"{mins:00}:{secs:05.2f}";
        }

        private void EnsureStyles()
        {
            if (stylesInitialized && panelStyle != null) return;

            darkBgTex = MakeColorTexture(new Color(0.08f, 0.08f, 0.1f, 0.88f));
            barBgTex = MakeColorTexture(new Color(0.2f, 0.2f, 0.25f, 0.9f));
            greenTex = MakeColorTexture(new Color(0.2f, 0.8f, 0.3f, 1f));
            redTex = MakeColorTexture(new Color(0.85f, 0.2f, 0.2f, 1f));

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = darkBgTex },
                padding = new RectOffset(10, 10, 8, 8)
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true,
                normal = { textColor = Color.white }
            };

            bannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            unitLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                richText = true,
                normal = { textColor = Color.white }
            };

            stylesInitialized = true;
        }

        private Texture2D MakeColorTexture(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
