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

        var serializedGoalUtilities = serializedObject.FindProperty("goalScores");
        var pairsProperty = serializedGoalUtilities.FindPropertyRelative("pairs");

        var listView = new ListView
        {
            showAddRemoveFooter = true,
            showFoldoutHeader = true,
            headerTitle = "Goal Utilities",
            reorderable = true,
            bindingPath = pairsProperty.propertyPath,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
        };

        listView.makeItem = () =>
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var keyField = new PropertyField();
            keyField.style.flexGrow = 1;

            var valueField = new PropertyField();
            valueField.style.flexGrow = 1;

            container.Add(keyField);
            container.Add(valueField);

            return container;
        };

        listView.bindItem = (element, index) =>
        {
            var pair = pairsProperty.GetArrayElementAtIndex(index);

            var keyProp = pair.FindPropertyRelative("Key");
            var valueProp = pair.FindPropertyRelative("Value");

            ((PropertyField)element.ElementAt(0)).BindProperty(keyProp);
            ((PropertyField)element.ElementAt(1)).BindProperty(valueProp);
        };

        listView.itemsSource = new int[pairsProperty.arraySize];
        listView.Bind(serializedObject);

        root.Add(listView);

        var serializedSkillUtilities = serializedObject.FindProperty("skillScores");
        if (serializedSkillUtilities != null)
        {
            var skillPairsProperty = serializedSkillUtilities.FindPropertyRelative("pairs");
            if (skillPairsProperty != null)
            {
                var skillListView = new ListView
                {
                    showAddRemoveFooter = true,
                    showFoldoutHeader = true,
                    headerTitle = "Skill Utilities",
                    reorderable = true,
                    bindingPath = skillPairsProperty.propertyPath,
                    virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
                };

                skillListView.makeItem = () =>
                {
                    var container = new VisualElement();
                    container.style.flexDirection = FlexDirection.Row;

                    var keyField = new PropertyField();
                    keyField.style.flexGrow = 1;

                    var valueField = new PropertyField();
                    valueField.style.flexGrow = 1;

                    container.Add(keyField);
                    container.Add(valueField);

                    return container;
                };

                skillListView.bindItem = (element, index) =>
                {
                    var pair = skillPairsProperty.GetArrayElementAtIndex(index);

                    var keyProp = pair.FindPropertyRelative("Key");
                    var valueProp = pair.FindPropertyRelative("Value");

                    ((PropertyField)element.ElementAt(0)).BindProperty(keyProp);
                    ((PropertyField)element.ElementAt(1)).BindProperty(valueProp);
                };

                skillListView.itemsSource = new int[skillPairsProperty.arraySize];
                skillListView.Bind(serializedObject);

                root.Add(skillListView);
            }
        }

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
