using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterBrain))]
public class CharacterBrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector first
        DrawDefaultInspector();

        CharacterBrain brain = (CharacterBrain)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "Control Mode",
            brain.ControlMode.ToString()
        );

        EditorGUILayout.LabelField(
            "Agent Status",
            brain.AgentStatus.ToString()
        );

        // Ensure repaint during Play Mode
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
