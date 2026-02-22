#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializeDictionaryAttribute))]
public class SerializableDictionaryDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var keys = property.FindPropertyRelative("keys");
        return EditorGUI.GetPropertyHeight(keys, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var keys = property.FindPropertyRelative("keys");
        var values = property.FindPropertyRelative("values");

        EditorGUI.BeginProperty(position, label, property);

        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label);

        EditorGUI.indentLevel++;

        for (int i = 0; i < keys.arraySize; i++)
        {
            position.y += EditorGUIUtility.singleLineHeight + 2;

            float halfWidth = position.width / 2;

            Rect keyRect = new(position.x, position.y, halfWidth - 5, EditorGUIUtility.singleLineHeight);
            Rect valueRect = new(position.x + halfWidth + 5, position.y, halfWidth - 5, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(keyRect, keys.GetArrayElementAtIndex(i), GUIContent.none);
            EditorGUI.PropertyField(valueRect, values.GetArrayElementAtIndex(i), GUIContent.none);
        }

        position.y += EditorGUIUtility.singleLineHeight + 4;

        if (GUI.Button(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), "Add"))
        {
            keys.arraySize++;
            values.arraySize++;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
#endif