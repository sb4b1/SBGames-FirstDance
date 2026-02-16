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
        VTree.CloneTree(root);

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

        playerButton.RegisterCallback<ClickEvent>(OnPlayerButtonClicked);
        agentButton.RegisterCallback<ClickEvent>(OnAgentButtonClicked);
        toggleAIButton.RegisterCallback<ClickEvent>(OnToggleAIButtonClicked);

        return root;
    }

    private void OnPlayerButtonClicked(ClickEvent evt)
    {
        characterBrain.PlayerControl();
    }
    private void OnAgentButtonClicked(ClickEvent evt)
    {
        characterBrain.AgentControl();
    }
    private void OnToggleAIButtonClicked(ClickEvent evt)
    {
        characterBrain.ToggleAI();
    }
}
