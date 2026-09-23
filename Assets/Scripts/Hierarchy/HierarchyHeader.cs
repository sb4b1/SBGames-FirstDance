using UnityEngine;

[DisallowMultipleComponent]
public class HierarchyHeader : MonoBehaviour
{
    [Header("Header")]
    [SerializeField]
    private string headerText = "HEADER";

    [SerializeField]
    private Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [SerializeField]
    private Color textColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Header("Text")]
    [SerializeField]
    [Range(8, 20)]
    private int fontSize = 11;

    [SerializeField]
    private bool bold = true;

    [Header("Lines")]
    [SerializeField]
    private bool showLines = true;

    [SerializeField]
    private Color lineColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [SerializeField]
    [Range(10f, 100f)]
    private float linePadding = 20f;

    [Header("Layout")]
    [SerializeField]
    [Range(16f, 40f)]
    private float height = 22f;

    public string HeaderText => headerText;
    public Color BackgroundColor => backgroundColor;
    public Color TextColor => textColor;
    public int FontSize => fontSize;
    public bool Bold => bold;
    public bool ShowLines => showLines;
    public Color LineColor => lineColor;
    public float LinePadding => linePadding;
    public float Height => height;
}