using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LabelWithValueAttribute))]
public class LabelWithValueDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        LabelWithValueAttribute att = (LabelWithValueAttribute)attribute;

        Rect labelRect = new Rect(position.x, position.y, 80, position.height);
        Rect valueRect = new Rect(position.x + 85, position.y,
                                  position.width - 85, position.height);

        EditorGUI.LabelField(labelRect, att.Label);
        EditorGUI.PropertyField(valueRect, property, GUIContent.none);
    }
}