#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchyHeaderEditor
{
    private static GUIStyle textStyle;

    static HierarchyHeaderEditor()
    {
        EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUI;
        EditorApplication.hierarchyChanged += RepaintHierarchy;
    }

    private static void RepaintHierarchy()
    {
        EditorApplication.RepaintHierarchyWindow();
    }

    private static void OnHierarchyGUI(
        EntityId entityId,
        Rect selectionRect)
    {
        GameObject gameObject =
            EditorUtility.EntityIdToObject(entityId) as GameObject;

        if (gameObject == null)
            return;

        HierarchyHeader header =
            gameObject.GetComponent<HierarchyHeader>();

        if (header == null)
            return;

        DrawHeader(selectionRect, header);
    }

    private static void DrawHeader(
        Rect rect,
        HierarchyHeader header)
    {
        // ---------------------------------------------
        // Background
        // ---------------------------------------------

        Rect backgroundRect = new(
            0f,
            rect.y,
            rect.width,
            header.Height
        );

        EditorGUI.DrawRect(
            backgroundRect,
            header.BackgroundColor
        );

        // ---------------------------------------------
        // Text Style
        // ---------------------------------------------

        if (textStyle == null)
        {
            textStyle = new GUIStyle(EditorStyles.label);
        }

        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.fontSize = header.FontSize;

        textStyle.fontStyle = header.Bold
            ? FontStyle.Bold
            : FontStyle.Normal;

        textStyle.normal.textColor =
            header.TextColor;

        // ---------------------------------------------
        // Calculate Text
        // ---------------------------------------------

        GUIContent content =
            new(header.HeaderText);

        Vector2 textSize =
            textStyle.CalcSize(content);

        float centerX =
            rect.width * 0.5f;

        Rect textRect = new(
            centerX - textSize.x * 0.5f,
            rect.y,
            textSize.x,
            header.Height
        );

        // ---------------------------------------------
        // Text Background
        // ---------------------------------------------

        Rect textBackgroundRect = new(
            textRect.x - header.LinePadding * 0.5f,
            textRect.y,
            textRect.width + header.LinePadding,
            textRect.height
        );

        EditorGUI.DrawRect(
            textBackgroundRect,
            header.BackgroundColor
        );

        // ---------------------------------------------
        // Lines
        // ---------------------------------------------

        if (header.ShowLines)
        {
            float lineY =
                rect.y + rect.height * 0.5f;

            float gap =
                header.LinePadding;

            float leftStart = 0f;
            float leftEnd =
                textRect.x - gap;

            float rightStart =
                textRect.x +
                textRect.width +
                gap;

            float rightEnd =
                rect.width;

            const float lineHeight = 1f;

            // Left line
            if (leftEnd > leftStart)
            {
                EditorGUI.DrawRect(
                    new Rect(
                        leftStart,
                        lineY,
                        leftEnd - leftStart,
                        lineHeight
                    ),
                    header.LineColor
                );
            }

            // Right line
            if (rightEnd > rightStart)
            {
                EditorGUI.DrawRect(
                    new Rect(
                        rightStart,
                        lineY,
                        rightEnd - rightStart,
                        lineHeight
                    ),
                    header.LineColor
                );
            }
        }

        // ---------------------------------------------
        // Draw Text
        // ---------------------------------------------

        GUI.Label(
            textRect,
            content,
            textStyle
        );
    }
}

#endif