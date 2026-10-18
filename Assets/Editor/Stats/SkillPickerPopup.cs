using System;
using System.Collections.Generic;
using SBGames.Database;
using SBGames.Database.Editor;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// Searchable dropdown listing Skill database entries. Skills already on the character are hidden;
    /// skills without a caster or effect prefab are shown disabled with a reason tooltip.
    /// </summary>
    public class SkillPickerPopup : PopupWindowContent
    {
        private readonly Func<string, bool> _isAlreadyAssigned;
        private readonly Action<string> _onPicked;
        private readonly List<SkillData> _skills = new();
        private string _search = "";
        private Vector2 _scroll;
        private bool _focusSearch = true;

        public SkillPickerPopup(Func<string, bool> isAlreadyAssigned, Action<string> onPicked)
        {
            _isAlreadyAssigned = isAlreadyAssigned;
            _onPicked = onPicked;

            CharacterPrefabSkillSync.ClearCasterPrefabCache();
            var db = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            if (db != null && db.Skills != null)
            {
                foreach (var s in db.Skills)
                {
                    if (s != null && !string.IsNullOrWhiteSpace(s.skillID)) _skills.Add(s);
                }
            }
            _skills.Sort((a, b) => string.Compare(a.skillID, b.skillID, StringComparison.OrdinalIgnoreCase));
        }

        public override Vector2 GetWindowSize() => new(520, 320);

        /// <summary>Why a skill cannot be added, or null if it can.</summary>
        public static string GetDisabledReason(SkillData skill)
        {
            if (skill == null) return "Skill is null.";
            if (skill.skillPrefab == null)
            {
                if (!SkillEffectPrefabGenerator.CanGenerate(skill, out string genReason))
                {
                    return genReason ?? "No effect prefab assigned, and no matching template exists to auto-generate one.";
                }
                return null;
            }
            return null;
        }

        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName("SkillPickerSearch");
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (_focusSearch)
            {
                EditorGUI.FocusTextInControl("SkillPickerSearch");
                _focusSearch = false;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("ID", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(130));
                GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                GUILayout.Label("Damage", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                GUILayout.Label("Mana", EditorStyles.miniBoldLabel, GUILayout.Width(45));
                GUILayout.Label("Status", EditorStyles.miniBoldLabel, GUILayout.Width(90));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            foreach (var skill in _skills)
            {
                if (_isAlreadyAssigned(skill.skillID)) continue;
                if (!string.IsNullOrWhiteSpace(_search) &&
                    skill.skillID.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (skill.skillName ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                shown++;
                string reason = GetDisabledReason(skill);
                bool willGenerate = skill.skillPrefab == null && reason == null;
                string statusText = reason != null ? "No template" : (willGenerate ? "Will generate" : "Ready");
                string tooltip = reason != null
                    ? reason
                    : (willGenerate
                        ? "No prefab, will be generated on Save All."
                        : "Add to Default Skill IDs (applied to the prefab on Save All).");

                using (new EditorGUI.DisabledScope(reason != null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                    GUI.Label(new Rect(r.x, r.y, 110, r.height), new GUIContent(skill.skillID, tooltip));
                    GUI.Label(new Rect(r.x + 114, r.y, 130, r.height), new GUIContent(skill.skillName, tooltip));
                    GUI.Label(new Rect(r.x + 248, r.y, 70, r.height), new GUIContent(skill.type.ToString(), tooltip));
                    GUI.Label(new Rect(r.x + 322, r.y, 70, r.height), new GUIContent(skill.damageType.ToString(), tooltip));
                    GUI.Label(new Rect(r.x + 396, r.y, 45, r.height), new GUIContent(skill.baseManaCost.ToString("0.#"), tooltip));
                    
                    var statusStyle = willGenerate ? EditorStyles.miniLabel : EditorStyles.label;
                    GUI.Label(new Rect(r.x + 445, r.y, 90, r.height), new GUIContent(statusText, tooltip), statusStyle);

                    if (reason == null && GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        _onPicked(skill.skillID);
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            if (shown == 0) EditorGUILayout.HelpBox("No matching skills.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
