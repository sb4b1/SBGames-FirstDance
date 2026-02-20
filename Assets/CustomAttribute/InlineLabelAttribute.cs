using UnityEngine;

public class InlineLabelAttribute : PropertyAttribute
{
    public string label;

    public InlineLabelAttribute(string label)
    {
        this.label = label;
    }
}
