using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(CharacterBrain))]
public class CharacterBrainEditor : Editor
{
    public VisualTreeAsset VTree;
    private CharacterBrain characterBrain;

    Button playerButton;
    Button agentButton;
    Button toggleAIButton;

    private void OnEnable()
    {
        characterBrain = (CharacterBrain)target;
    }

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        if (VTree != null)
        {
            VTree.CloneTree(root);
        }

        var foldout = new Foldout()
        {
            text = "CharacterBrain Properties",
            value = false
        };
        InspectorElement.FillDefaultInspector(foldout, serializedObject, this);
        root.Add(foldout);

        playerButton = root.Q<Button>("PlayerControlButton");
        agentButton = root.Q<Button>("AgentControlButton");
        toggleAIButton = root.Q<Button>("ToggleAIButton");

        if (playerButton != null) playerButton.RegisterCallback<ClickEvent>(OnPlayerButtonClicked);
        if (agentButton != null) agentButton.RegisterCallback<ClickEvent>(OnAgentButtonClicked);
        if (toggleAIButton != null) toggleAIButton.RegisterCallback<ClickEvent>(OnToggleAIButtonClicked);

        root.Bind(serializedObject);

        return root;
    }

    private void OnPlayerButtonClicked(ClickEvent evt)
    {
        if (characterBrain == null) return;

        Undo.RecordObject(characterBrain, "Set Player Control");
        characterBrain.PlayerControl();
        MarkDirtyAndSave(characterBrain);
    }

    private void OnAgentButtonClicked(ClickEvent evt)
    {
        if (characterBrain == null) return;

        Undo.RecordObject(characterBrain, "Set AI Control");
        characterBrain.AgentControl();
        MarkDirtyAndSave(characterBrain);
    }

    private void OnToggleAIButtonClicked(ClickEvent evt)
    {
        if (characterBrain == null) return;

        Undo.RecordObject(characterBrain, "Toggle AI Control");
        characterBrain.ToggleAI();
        MarkDirtyAndSave(characterBrain);
    }

    private void MarkDirtyAndSave(CharacterBrain brain)
    {
        serializedObject.Update();
        EditorUtility.SetDirty(brain);
        if (brain.gameObject != null)
        {
            EditorUtility.SetDirty(brain.gameObject);
        }

        if (PrefabUtility.IsPartOfPrefabInstance(brain))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
        }
    }
}
