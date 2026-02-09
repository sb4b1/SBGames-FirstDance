using UnityEngine;

public class LabelWithValueAttribute : PropertyAttribute
{
    public string Label;

    public LabelWithValueAttribute(string label)
    {
        Label = label;
    }
}