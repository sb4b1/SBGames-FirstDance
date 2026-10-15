using UnityEditor;
using UnityEngine;
using SBGames.Dungeon.Unity;

namespace SBGames.Editor.Dungeon
{
    [CustomEditor(typeof(DungeonRuntimeBuilder))]
    public class DungeonRuntimeBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var builder = (DungeonRuntimeBuilder)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Dungeon Generation Testing", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🎲 Generate Random Dungeon", GUILayout.Height(32)))
                {
                    int seed = Random.Range(1000, 999999);
                    builder.GenerateDungeonForRun(seed);
                }

                if (GUILayout.Button("Clear Dungeon", GUILayout.Height(32), GUILayout.Width(110)))
                {
                    builder.ClearSpawnedObjects();
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Open Batch Simulation Window", GUILayout.Height(26)))
            {
                DungeonPieceAuthoringWindow.OpenBatchSimulationWindow();
            }
        }
    }
}
