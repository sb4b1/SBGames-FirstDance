using System;
using System.Collections.Generic;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Database.Editor
{
    [CustomEditor(typeof(ItemDatabaseSO))]
    public class ItemDatabaseSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _artifactsProp;
        private SerializedProperty _runesProp;
        private SerializedProperty _consumablesProp;
        private SerializedProperty _otherItemsProp;

        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "All Categories", "Artifacts", "Runes", "Consumables", "Other Items" };

        private bool _foldoutArtifacts = true;
        private bool _foldoutRunes = true;
        private bool _foldoutConsumables = true;
        private bool _foldoutOthers = true;

        private string _searchFilter = string.Empty;

        private void OnEnable()
        {
            _artifactsProp = serializedObject.FindProperty("artifacts");
            _runesProp = serializedObject.FindProperty("runes");
            _consumablesProp = serializedObject.FindProperty("consumables");
            _otherItemsProp = serializedObject.FindProperty("otherItems");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var database = (ItemDatabaseSO)target;

            EditorGUILayout.Space(6);
            DrawSummaryBox();
            EditorGUILayout.Space(4);

            DrawToolbar(database);
            EditorGUILayout.Space(6);

            _searchFilter = EditorGUILayout.TextField("Search Filter", _searchFilter);
            EditorGUILayout.Space(6);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0:
                    DrawCategorySection("Artifacts", _artifactsProp, ref _foldoutArtifacts, ItemType.Artifact, new Color(0.9f, 0.6f, 0.2f, 0.2f));
                    EditorGUILayout.Space(6);
                    DrawCategorySection("Runes", _runesProp, ref _foldoutRunes, ItemType.Rune, new Color(0.2f, 0.7f, 0.9f, 0.2f));
                    EditorGUILayout.Space(6);
                    DrawCategorySection("Consumables", _consumablesProp, ref _foldoutConsumables, ItemType.Consumable, new Color(0.3f, 0.8f, 0.4f, 0.2f));
                    EditorGUILayout.Space(6);
                    DrawCategorySection("Other Items", _otherItemsProp, ref _foldoutOthers, ItemType.Misc, new Color(0.6f, 0.6f, 0.6f, 0.2f));
                    break;

                case 1:
                    DrawCategoryList("Artifacts", _artifactsProp, ItemType.Artifact);
                    break;

                case 2:
                    DrawCategoryList("Runes", _runesProp, ItemType.Rune);
                    break;

                case 3:
                    DrawCategoryList("Consumables", _consumablesProp, ItemType.Consumable);
                    break;

                case 4:
                    DrawCategoryList("Other Items", _otherItemsProp, ItemType.Misc);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSummaryBox()
        {
            int total = (_artifactsProp?.arraySize ?? 0) +
                        (_runesProp?.arraySize ?? 0) +
                        (_consumablesProp?.arraySize ?? 0) +
                        (_otherItemsProp?.arraySize ?? 0);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Item Database Catalog", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total Items: {total} | Artifacts: {_artifactsProp?.arraySize ?? 0} | Runes: {_runesProp?.arraySize ?? 0} | Consumables: {_consumablesProp?.arraySize ?? 0} | Others: {_otherItemsProp?.arraySize ?? 0}", EditorStyles.miniLabel);
            }
        }

        private void DrawToolbar(ItemDatabaseSO database)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto-Index Catalogs", GUILayout.Height(24)))
                {
                    DatabaseAutoIndexer.RebuildAllCatalogs();
                }

                if (GUILayout.Button("Sort Lists By ID", GUILayout.Height(24)))
                {
                    SortDatabase(database);
                }
            }
        }

        private void DrawCategorySection(string title, SerializedProperty listProp, ref bool foldout, ItemType expectedType, Color tint)
        {
            int count = listProp.arraySize;
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = tint;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prevColor;

                foldout = EditorGUILayout.Foldout(foldout, $"{title} ({count})", true, EditorStyles.foldoutHeader);
                if (foldout)
                {
                    DrawCategoryItems(listProp, expectedType);
                }
            }
        }

        private void DrawCategoryList(string title, SerializedProperty listProp, ItemType expectedType)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{title} ({listProp.arraySize})", EditorStyles.boldLabel);
                DrawCategoryItems(listProp, expectedType);
            }
        }

        private void DrawCategoryItems(SerializedProperty listProp, ItemType expectedType)
        {
            EditorGUI.indentLevel++;

            int toRemove = -1;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                var item = element.objectReferenceValue as ItemDataSO;

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    bool match = false;
                    if (item != null)
                    {
                        if ((item.Id != null && item.Id.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (item.ItemName != null && item.ItemName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            match = true;
                        }
                    }

                    if (!match) continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(element, new GUIContent(item != null ? $"[{item.Id}] {item.ItemName}" : $"Element {i}"));

                    if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        toRemove = i;
                    }
                }
            }

            if (toRemove >= 0)
            {
                listProp.DeleteArrayElementAtIndex(toRemove);
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"+ Add {expectedType}", GUILayout.Height(20)))
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                }

                if (GUILayout.Button("Clear Empty", GUILayout.Width(90), GUILayout.Height(20)))
                {
                    for (int i = listProp.arraySize - 1; i >= 0; i--)
                    {
                        if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        {
                            listProp.DeleteArrayElementAtIndex(i);
                        }
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private void SortDatabase(ItemDatabaseSO db)
        {
            Undo.RecordObject(db, "Sort Item Database");

            SortList(_artifactsProp);
            SortList(_runesProp);
            SortList(_consumablesProp);
            SortList(_otherItemsProp);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
        }

        private void SortList(SerializedProperty listProp)
        {
            var list = new List<ItemDataSO>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var item = listProp.GetArrayElementAtIndex(i).objectReferenceValue as ItemDataSO;
                if (item != null) list.Add(item);
            }

            list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));

            listProp.ClearArray();
            for (int i = 0; i < list.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
        }
    }
}
