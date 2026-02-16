using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(BaseAI))]
public class BaseAIEditor : Editor
{
    public VisualTreeAsset VTree;

    /*
    private void OnEnable()
    {
        baseAI = (BaseAI)target;
    }
    */

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        VTree.CloneTree(root);

        var foldout = new Foldout()
        {
            text = "BaseAI Properties",
            value = false
        };
        InspectorElement.FillDefaultInspector(foldout, serializedObject, this);
        root.Add(foldout);

        return root;
    }


}
