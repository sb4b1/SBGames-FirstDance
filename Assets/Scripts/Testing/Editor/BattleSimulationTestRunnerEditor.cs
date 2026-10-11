using UnityEditor;
using UnityEngine;

namespace SBGames.Testing.Editor
{
    [CustomEditor(typeof(BattleSimulationTestRunner))]
    public class BattleSimulationTestRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var runner = (BattleSimulationTestRunner)target;

            // Header Banner
            DrawHeaderBanner(runner);

            // Live Simulation Controls in Play Mode
            if (Application.isPlaying)
            {
                DrawRuntimeControls(runner);
            }
            else
            {
                DrawEditModeHelpers(runner);
            }

            EditorGUILayout.Space(8f);

            // Default Inspector Fields
            DrawDefaultInspector();
        }

        private void DrawHeaderBanner(BattleSimulationTestRunner runner)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Battle Simulation Test Harness", EditorStyles.boldLabel);

                if (Application.isPlaying)
                {
                    string stateColor = runner.State switch
                    {
                        BattleSimulationTestRunner.SimulationState.Running => "green",
                        BattleSimulationTestRunner.SimulationState.Paused => "yellow",
                        BattleSimulationTestRunner.SimulationState.Completed => "cyan",
                        _ => "white"
                    };

                    EditorGUILayout.LabelField($"Status: <color={stateColor}><b>{runner.State}</b></color> | Elapsed: <b>{runner.ElapsedTime:F2}s</b> | TimeScale: <b>{Time.timeScale:F1}x</b>", new GUIStyle(EditorStyles.label) { richText = true });
                    EditorGUILayout.LabelField($"Alive Units - Monsters: <b>{runner.AliveMonstersCount}/{runner.InitialMonsters}</b> | Adventurers: <b>{runner.AliveAdventurersCount}/{runner.InitialAdventurers}</b>", new GUIStyle(EditorStyles.label) { richText = true });

                    if (runner.State == BattleSimulationTestRunner.SimulationState.Completed)
                    {
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.LabelField($"Result: <b>{runner.CurrentOutcome}</b>", EditorStyles.boldLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"Configured Roster: <b>{runner.Roster?.Count ?? 0}</b> slots defined.", new GUIStyle(EditorStyles.label) { richText = true });
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeControls(BattleSimulationTestRunner runner)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Simulation Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                if (GUILayout.Button("▶ Start", GUILayout.Height(28)))
                {
                    runner.StartSimulation();
                }

                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("🔄 Rerun", GUILayout.Height(28)))
                {
                    runner.RestartSimulation();
                }

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹ Reset", GUILayout.Height(28)))
                {
                    runner.ResetSimulation();
                }

                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("⏩ Cycle Speed", GUILayout.Height(22)))
                {
                    runner.ToggleSpeed();
                }

                string pauseLabel = runner.State == BattleSimulationTestRunner.SimulationState.Paused ? "▶ Resume" : "⏸ Pause";
                if (GUILayout.Button(pauseLabel, GUILayout.Height(22)))
                {
                    runner.TogglePause();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditModeHelpers(BattleSimulationTestRunner runner)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Quick Roster Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("1v1 Duel"))
                {
                    SetupPreset(runner, 1, 1);
                }

                if (GUILayout.Button("3v3 Skirmish"))
                {
                    SetupPreset(runner, 3, 3);
                }

                if (GUILayout.Button("5v5 Brawl"))
                {
                    SetupPreset(runner, 5, 5);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SetupPreset(BattleSimulationTestRunner runner, int monsterCount, int adventurerCount)
        {
            Undo.RecordObject(runner, "Setup Simulation Roster Preset");

            // Look for monster and adventurer prefabs
            var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");
            if (monsterPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab MONSTER");
                if (guids.Length > 0)
                {
                    monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            var adventurerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N002.prefab");
            if (adventurerPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab ADVENTURER");
                if (guids.Length > 0)
                {
                    adventurerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            runner.ClearRoster();

            // Monsters on the left (X = -3 to -5)
            float monsterSpacing = monsterCount > 1 ? 6f / (monsterCount - 1) : 0f;
            float monsterStartY = monsterCount > 1 ? -3f : 0f;

            for (int i = 0; i < monsterCount; i++)
            {
                Vector3 pos = new Vector3(-3.5f, monsterStartY + (i * monsterSpacing), 0f);
                runner.AddSlot(monsterPrefab, pos, BattleSimulationTestRunner.BattleFaction.Monster, 1);
            }

            // Adventurers on the right (X = +3.5)
            float advSpacing = adventurerCount > 1 ? 6f / (adventurerCount - 1) : 0f;
            float advStartY = adventurerCount > 1 ? -3f : 0f;

            for (int i = 0; i < adventurerCount; i++)
            {
                Vector3 pos = new Vector3(3.5f, advStartY + (i * advSpacing), 0f);
                runner.AddSlot(adventurerPrefab, pos, BattleSimulationTestRunner.BattleFaction.Adventurer, 1);
            }

            EditorUtility.SetDirty(runner);
            Debug.Log($"[BattleSimulationTestRunner] Applied preset: {monsterCount} Monsters vs {adventurerCount} Adventurers.");
        }
    }
}
