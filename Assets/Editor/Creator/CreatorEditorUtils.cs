using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Creator
{
    public static class CreatorEditorUtils
    {
        public static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            if (target is UnityEngine.Object unityObj)
            {
                var so = new SerializedObject(unityObj);
                var prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    if (value is UnityEngine.Object objVal)
                    {
                        prop.objectReferenceValue = objVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is string strVal)
                    {
                        prop.stringValue = strVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is int intVal)
                    {
                        prop.intValue = intVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is float floatVal)
                    {
                        prop.floatValue = floatVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is bool boolVal)
                    {
                        prop.boolValue = boolVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is Enum enumVal)
                    {
                        prop.enumValueIndex = Convert.ToInt32(enumVal);
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is Vector2 v2Val)
                    {
                        prop.vector2Value = v2Val;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is Vector2Int v2iVal)
                    {
                        prop.vector2IntValue = v2iVal;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is Vector3 v3Val)
                    {
                        prop.vector3Value = v3Val;
                        so.ApplyModifiedProperties();
                        return;
                    }
                    if (value is IList listVal)
                    {
                        if (prop.isArray)
                        {
                            prop.arraySize = listVal.Count;
                            for (int i = 0; i < listVal.Count; i++)
                            {
                                var elemProp = prop.GetArrayElementAtIndex(i);
                                object item = listVal[i];
                                if (item is UnityEngine.Object uo)
                                {
                                    elemProp.objectReferenceValue = uo;
                                }
                                else if (item is string s)
                                {
                                    elemProp.stringValue = s;
                                }
                                else if (item is int n)
                                {
                                    elemProp.intValue = n;
                                }
                                else if (item is float f)
                                {
                                    elemProp.floatValue = f;
                                }
                                else if (item is bool b)
                                {
                                    elemProp.boolValue = b;
                                }
                            }
                            so.ApplyModifiedProperties();
                            return;
                        }
                    }
                }
            }

            // Fallback to reflection walking up class hierarchy if needed
            var type = target.GetType();
            while (type != null && type != typeof(object))
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
        }

        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            var comp = go.GetComponent<T>();
            return comp != null ? comp : go.AddComponent<T>();
        }

        public static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            string normalized = folderPath.Replace("\\", "/").TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0) return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static void DrawHeader(string title, string subtitle)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField(title, titleStyle);
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    EditorGUILayout.LabelField(subtitle, EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        public static void DrawGenerateButton(string label, Action onClick, Color? color = null)
        {
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = color ?? new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button(label, btnStyle))
            {
                onClick?.Invoke();
            }
            GUI.backgroundColor = prevColor;
        }

        public static T FindFirstAsset<T>(string searchFilter = null) where T : UnityEngine.Object
        {
            string filter = string.IsNullOrEmpty(searchFilter) ? $"t:{typeof(T).Name}" : searchFilter;
            string[] guids = AssetDatabase.FindAssets(filter);
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        public static string NextFreeId(string prefix, string folder, int defaultDigits = 3)
        {
            int highestNum = 0;
            // Build regex to match numbers following prefix
            string escapedPrefix = Regex.Escape(prefix);
            var pattern = new Regex($@"{escapedPrefix}(\d+)", RegexOptions.IgnoreCase);

            void ScanDirectory(string dirPath)
            {
                if (!Directory.Exists(dirPath)) return;
                var files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    var match = pattern.Match(fileName);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                    {
                        if (num > highestNum) highestNum = num;
                    }
                }
            }

            if (!string.IsNullOrEmpty(folder))
            {
                ScanDirectory(folder);
            }

            // Also check global Skills folders for SKILL_<prefix>
            if (prefix.StartsWith("SKILL_", StringComparison.OrdinalIgnoreCase))
            {
                ScanDirectory("Assets/ScriptableObjects/Skill");
                ScanDirectory("Assets/Prefabs/Skills");
            }

            int nextNum = highestNum + 1;
            return $"{prefix}{nextNum.ToString($"D{defaultDigits}")}";
        }
    }
}
