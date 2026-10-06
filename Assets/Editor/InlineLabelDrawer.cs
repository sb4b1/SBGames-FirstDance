using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InlineLabelAttribute))]
public class InlineLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        InlineLabelAttribute attr = (InlineLabelAttribute)attribute;

        string labelText = string.IsNullOrEmpty(attr.label)
            ? label.text
            : attr.label;

        string valueText = GetValueAsString(property);

        float labelWidth = 95f;

        Rect labelRect = new(
            position.x,
            position.y,
            labelWidth,
            position.height);

        Rect valueRect = new(
            position.x + labelWidth,
            position.y,
            position.width - labelWidth,
            position.height);

        EditorGUI.LabelField(labelRect, labelText + " :");
        EditorGUI.LabelField(valueRect, valueText, EditorStyles.boldLabel);
    }

    private string GetValueAsString(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue != null
                    ? property.objectReferenceValue.name
                    : "None";

            case SerializedPropertyType.String:
                return property.stringValue;

            case SerializedPropertyType.Integer:
                return property.intValue.ToString();

            case SerializedPropertyType.Float:
                return property.floatValue.ToString("0.00");

            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();

            default:
                return "";
        }
    }
}