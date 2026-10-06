using SBGames.Loop;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor
{
    [CustomEditor(typeof(CoreGameLoop))]
    public class CoreGameLoopEditor : UnityEditor.Editor
    {
        private int jumpRoundInput = 1;
        private GameOverReason selectedGameOverReason = GameOverReason.EssenceDepleted;

        private void OnEnable()
        {
            var loop = (CoreGameLoop)target;
            if (loop != null)
            {
                jumpRoundInput = Mathf.Max(1, loop.CurrentRound);
            }
        }

        public override void OnInspectorGUI()
        {
            var loop = (CoreGameLoop)target;
            serializedObject.Update();

            // Draw default serialized fields
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(12);

            // Draw Custom Debug Panel
            DrawDebugControlPanel(loop);
        }

        private void DrawDebugControlPanel(CoreGameLoop loop)
        {
            // Outer Box
            GUIStyle boxStyle = new(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            EditorGUILayout.BeginVertical(boxStyle);

            // Title Header
            GUIStyle headerStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("🎮 Stage & Loop Inspector Debugger", headerStyle);
            EditorGUILayout.Space(4);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to interact with real-time stage transitions, round jumping, and battle simulations.", MessageType.Info);
                
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset To Default State", GUILayout.Height(24)))
                {
                    loop.DebugResetGame();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                return;
            }

            // --- Status Box ---
            Color phaseColor = loop.CurrentPhase switch
            {
                GamePhase.Planning => new Color(0.2f, 0.65f, 1.0f),
                GamePhase.Battle => new Color(1.0f, 0.55f, 0.2f),
                GamePhase.GameOver => new Color(0.9f, 0.25f, 0.25f),
                _ => Color.gray
            };

            GUIStyle statusStyle = new(EditorStyles.toolbarButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28,
                alignment = TextAnchor.MiddleCenter
            };

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = phaseColor;
            string statusText = $"PHASE: {loop.CurrentPhase.ToString().ToUpper()}  |  ROUND / STAGE: {loop.CurrentRound}";
            if (loop.IsGameOver)
            {
                statusText += $" ({loop.ReasonForGameOver})";
            }
            GUILayout.Box(statusText, statusStyle);
            GUI.backgroundColor = originalColor;

            // DM Vitals if available
            if (loop.DM != null)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"❤️ Essence: {loop.DM.Essence}/{loop.DM.MaxEssence}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"⚡ Energy: {loop.DM.Energy}/{loop.DM.MaxEnergy}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"💎 Artifacts: {loop.DM.ArtifactCount}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            // --- Section: Phase Switching ---
            EditorGUILayout.LabelField("Change Phase / Stage:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = loop.CurrentPhase == GamePhase.Planning ? new Color(0.3f, 0.8f, 1f) : originalColor;
            if (GUILayout.Button("🛠 Go to Planning", GUILayout.Height(26)))
            {
                loop.DebugStartPlanningPhase();
            }

            GUI.backgroundColor = loop.CurrentPhase == GamePhase.Battle ? new Color(1f, 0.6f, 0.3f) : originalColor;
            if (GUILayout.Button("⚔ Go to Battle", GUILayout.Height(26)))
            {
                loop.DebugStartBattlePhase();
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // --- Section: Round Stepper & Stage Jumper ---
            EditorGUILayout.LabelField("Round / Wave Navigation:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Prev Round", GUILayout.Height(24)))
            {
                loop.DebugPreviousRound();
                jumpRoundInput = loop.CurrentRound;
            }
            if (GUILayout.Button("Next Round ▶", GUILayout.Height(24)))
            {
                loop.DebugNextRound();
                jumpRoundInput = loop.CurrentRound;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Jump to Round");
            jumpRoundInput = EditorGUILayout.IntField(jumpRoundInput, GUILayout.Width(60));
            if (GUILayout.Button("Jump", GUILayout.Width(70), GUILayout.Height(20)))
            {
                loop.DebugSetRound(jumpRoundInput, true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // --- Section: Battle Simulation ---
            EditorGUILayout.LabelField("Battle Outcomes (Simulate):", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
            if (GUILayout.Button("★ Win Battle (Victory)", GUILayout.Height(28)))
            {
                loop.DebugWinBattle();
                jumpRoundInput = loop.CurrentRound;
            }

            GUI.backgroundColor = new Color(0.95f, 0.35f, 0.35f);
            if (GUILayout.Button("☠ Lose Battle (Defeat)", GUILayout.Height(28)))
            {
                loop.DebugLoseBattle();
                jumpRoundInput = loop.CurrentRound;
            }

            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // --- Section: Game Over & Reset ---
            EditorGUILayout.LabelField("Game Over & Reset:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            selectedGameOverReason = (GameOverReason)EditorGUILayout.EnumPopup(selectedGameOverReason, GUILayout.Width(130));
            if (GUILayout.Button("Trigger Game Over", GUILayout.Height(22)))
            {
                loop.DebugTriggerGameOver(selectedGameOverReason);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Start / Restart Game", GUILayout.Height(24)))
            {
                loop.DebugResetGame();
                jumpRoundInput = loop.CurrentRound;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
