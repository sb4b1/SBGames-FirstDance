using System;
using System.Collections.Generic;
using System.Text;
using SBGames.Database;
using SBGames.Database.Editor;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// Keeps a character prefab's skill hierarchy in sync with its Default Skill IDs.
    /// The ID list is the source of truth; the <c>Skills/SkillList</c> children and
    /// <see cref="CharacterSkillController"/>'s serialized <c>skills</c> list are derived from it.
    /// Editor-only: edits prefab assets, never SkillData templates.
    /// </summary>
    public static class CharacterPrefabSkillSync
    {
        public const string SkillsRootName = "Skills";
        public const string SkillListName = "SkillList";
        public const string SkillCasterPrefabFolder = "Assets/Prefabs/Skill";

        public enum IssueKind
        {
            MissingChild,
            ChildNotInController,
            ControllerEntryNotInIds,
            OrderMismatch,
            ExtraChild,
            MissingFromDatabase,
            NoController,
            NoPrefab
        }

        public readonly struct Issue
        {
            public readonly IssueKind Kind;
            public readonly string SkillId;
            public readonly string Message;

            public Issue(IssueKind kind, string skillId, string message)
            {
                Kind = kind;
                SkillId = skillId;
                Message = message;
            }

            /// <summary>True for issues that a Sync can resolve on the prefab.</summary>
            public bool IsFixableBySync => Kind == IssueKind.MissingChild || Kind == IssueKind.ChildNotInController ||
                                           Kind == IssueKind.ControllerEntryNotInIds || Kind == IssueKind.OrderMismatch;
        }

        public class SyncResult
        {
            public bool Success;
            public bool PrefabModified;
            public readonly List<string> Messages = new();
        }

        // ---------------------------------------------------------------------
        // Skill caster prefab lookup
        // ---------------------------------------------------------------------

        private static Dictionary<string, GameObject> _casterPrefabCache;

        public static void ClearCasterPrefabCache() => _casterPrefabCache = null;

        /// <summary>
        /// Finds the skill caster prefab (a prefab with <see cref="SkillCaster"/> whose Data has this skill ID).
        /// This is the object instantiated under SkillList; SkillData.skillPrefab is the spawned effect.
        /// </summary>
        public static GameObject ResolveCasterPrefab(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return null;
            if (_casterPrefabCache == null) BuildCasterPrefabCache();
            if (_casterPrefabCache.TryGetValue(skillId, out var prefab) && prefab != null)
                return prefab;

            // Auto-generation fallback: If SkillData exists in database and missing prefab, generate on demand
            var db = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            if (db != null && db.TryGet(skillId, out var skill) && skill != null)
            {
                if (skill.skillPrefab == null)
                {
                    SkillEffectPrefabGenerator.GenerateEffectPrefab(skill);
                }
                var caster = SkillEffectPrefabGenerator.EnsureCasterPrefabExists(skill);
                if (caster != null)
                {
                    _casterPrefabCache[skillId] = caster;
                    return caster;
                }
            }

            return null;
        }

        private static void BuildCasterPrefabCache()
        {
            _casterPrefabCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            string[] folders = AssetDatabase.IsValidFolder(SkillCasterPrefabFolder) ? new[] { SkillCasterPrefabFolder } : new[] { "Assets" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                var caster = go.GetComponent<SkillCaster>();
                string id = caster != null && caster.Data != null ? caster.Data.skillID : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!_casterPrefabCache.ContainsKey(id)) _casterPrefabCache[id] = go;
            }
        }

        public static bool SkillExistsInDatabase(string skillId)
        {
            var db = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            return db != null && db.TryGet(skillId, out var data) && data != null;
        }

        // ---------------------------------------------------------------------
        // Hierarchy helpers
        // ---------------------------------------------------------------------

        public static CharacterSkillController FindController(GameObject root) =>
            root != null ? root.GetComponentInChildren<CharacterSkillController>(true) : null;

        /// <summary>Finds <c>Skills/SkillList</c>; optionally creates the missing parts.</summary>
        public static Transform FindSkillList(GameObject root, bool create)
        {
            if (root == null) return null;
            var controller = FindController(root);

            if (controller != null)
            {
                var direct = controller.transform.Find(SkillListName);
                if (direct != null) return direct;
            }

            var any = FindDescendant(root.transform, SkillListName);
            if (any != null || !create) return any;

            Transform skillsRoot = controller != null && controller.gameObject.name == SkillsRootName
                ? controller.transform
                : FindDescendant(root.transform, SkillsRootName);
            if (skillsRoot == null)
            {
                skillsRoot = new GameObject(SkillsRootName).transform;
                skillsRoot.SetParent(root.transform, false);
            }

            var list = new GameObject(SkillListName).transform;
            list.SetParent(skillsRoot, false);
            return list;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var nested = FindDescendant(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        /// <summary>Skill ID a SkillList child represents: its name if it matches, else its SkillCaster data ID.</summary>
        public static string GetChildSkillId(GameObject child, IReadOnlyList<string> knownIds)
        {
            if (child == null) return null;
            if (knownIds != null)
            {
                for (int i = 0; i < knownIds.Count; i++)
                {
                    if (string.Equals(child.name, knownIds[i], StringComparison.OrdinalIgnoreCase)) return knownIds[i];
                }
            }
            var caster = child.GetComponent<SkillCaster>();
            if (caster != null && caster.Data != null && !string.IsNullOrWhiteSpace(caster.Data.skillID)) return caster.Data.skillID;
            return child.name;
        }

        /// <summary>Maps skill ID -> first SkillList child for it. Unmatched/duplicate children go to <paramref name="extras"/>.</summary>
        private static Dictionary<string, GameObject> MapChildren(Transform skillList, IReadOnlyList<string> ids, HashSet<string> removedIds, List<GameObject> extras)
        {
            var map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            if (skillList == null) return map;

            var known = new List<string>(ids);
            if (removedIds != null) known.AddRange(removedIds);

            for (int i = 0; i < skillList.childCount; i++)
            {
                var child = skillList.GetChild(i).gameObject;
                string id = GetChildSkillId(child, known);
                bool tracked = ContainsId(ids, id) || (removedIds != null && removedIds.Contains(id));
                if (tracked && !map.ContainsKey(id)) map[id] = child;
                else extras?.Add(child);
            }
            return map;
        }

        private static bool ContainsId(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static List<GameObject> ReadControllerEntries(CharacterSkillController controller)
        {
            var result = new List<GameObject>();
            var so = new SerializedObject(controller);
            var list = so.FindProperty("skills");
            if (list == null) return result;
            for (int i = 0; i < list.arraySize; i++)
            {
                var prop = list.GetArrayElementAtIndex(i).FindPropertyRelative("skillPrefab");
                result.Add(prop != null ? prop.objectReferenceValue as GameObject : null);
            }
            return result;
        }

        // ---------------------------------------------------------------------
        // Analysis (read-only)
        // ---------------------------------------------------------------------

        /// <summary>Compares a prefab (asset or loaded contents) against the skill ID list. Never modifies anything.</summary>
        public static List<Issue> Analyze(GameObject prefabRoot, IReadOnlyList<string> ids, Func<string, bool> existsInDatabase = null)
        {
            var issues = new List<Issue>();
            ids ??= Array.Empty<string>();

            if (existsInDatabase != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (!existsInDatabase(ids[i]))
                        issues.Add(new Issue(IssueKind.MissingFromDatabase, ids[i], $"Skill ID '{ids[i]}' is missing from the Skill database."));
                }
            }

            if (prefabRoot == null)
            {
                if (ids.Count > 0) issues.Add(new Issue(IssueKind.NoPrefab, null, "No prefab assigned; prefab skills cannot be synced."));
                return issues;
            }

            var controller = FindController(prefabRoot);
            if (controller == null)
            {
                if (ids.Count > 0) issues.Add(new Issue(IssueKind.NoController, null, "Prefab has no CharacterSkillController."));
                return issues;
            }

            var skillList = FindSkillList(prefabRoot, false);
            var extras = new List<GameObject>();
            var children = MapChildren(skillList, ids, null, extras);
            var entries = ReadControllerEntries(controller);

            var expectedOrder = new List<GameObject>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!children.TryGetValue(ids[i], out var child))
                {
                    issues.Add(new Issue(IssueKind.MissingChild, ids[i], $"'{ids[i]}' has no child under {SkillsRootName}/{SkillListName}."));
                    continue;
                }
                expectedOrder.Add(child);
                if (!entries.Contains(child))
                    issues.Add(new Issue(IssueKind.ChildNotInController, ids[i], $"'{ids[i]}' child exists but is not in CharacterSkillController.skills."));
            }

            var childSet = new HashSet<GameObject>(children.Values);
            var inListOrder = new List<GameObject>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !childSet.Contains(entry))
                {
                    string label = entry != null ? entry.name : "<null>";
                    issues.Add(new Issue(IssueKind.ControllerEntryNotInIds, label, $"Controller skill entry '{label}' is not in the Default Skill IDs."));
                }
                else inListOrder.Add(entry);
            }

            var presentExpected = expectedOrder.FindAll(inListOrder.Contains);
            bool orderMatches = presentExpected.Count == inListOrder.Count;
            for (int i = 0; orderMatches && i < presentExpected.Count; i++)
            {
                if (presentExpected[i] != inListOrder[i]) orderMatches = false;
            }
            if (!orderMatches)
                issues.Add(new Issue(IssueKind.OrderMismatch, null, "Controller skill order differs from Default Skill ID order."));

            foreach (var extra in extras)
                issues.Add(new Issue(IssueKind.ExtraChild, extra.name, $"Extra child '{extra.name}' under {SkillListName} is not in the ID list (left untouched)."));

            return issues;
        }

        public static string Summarize(List<Issue> issues)
        {
            if (issues == null || issues.Count == 0) return "";
            var sb = new StringBuilder("Prefab skill sync:");
            foreach (var issue in issues) sb.Append("\n• ").Append(issue.Message);
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Apply (mutates prefab contents)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Loads the prefab asset's contents, applies <see cref="ApplyToContents"/>, and saves only if something changed.
        /// Logs and returns a failed result instead of throwing.
        /// </summary>
        public static SyncResult ApplyToPrefabAsset(GameObject prefabAsset, IReadOnlyList<string> ids, ICollection<string> removedIds,
            Func<string, GameObject> resolveCasterPrefab, Func<GameObject, string, bool> confirmDelete)
        {
            var result = new SyncResult();
            if (prefabAsset == null)
            {
                result.Messages.Add("No prefab assigned; skipped.");
                return result;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            var assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
            if (string.IsNullOrEmpty(path) || assetType == PrefabAssetType.NotAPrefab || assetType == PrefabAssetType.Model || assetType == PrefabAssetType.MissingAsset)
            {
                result.Messages.Add($"'{prefabAsset.name}' is not an editable prefab asset ({assetType}); skipped.");
                return result;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                var inner = ApplyToContents(contents, ids, removedIds, resolveCasterPrefab, confirmDelete);
                result.Messages.AddRange(inner.Messages);
                result.Success = inner.Success;
                result.PrefabModified = inner.PrefabModified;
                if (inner.PrefabModified)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool saved);
                    if (!saved)
                    {
                        result.Success = false;
                        result.Messages.Add($"Failed to save prefab '{path}'.");
                    }
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Messages.Add($"Exception while syncing '{path}': {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
            return result;
        }

        /// <summary>
        /// Makes loaded prefab contents match <paramref name="ids"/>: creates missing SkillList children as nested
        /// prefab instances, deletes children for explicitly removed IDs, and rebuilds the controller list in ID order.
        /// Idempotent. Children not in either list are never deleted.
        /// </summary>
        public static SyncResult ApplyToContents(GameObject root, IReadOnlyList<string> ids, ICollection<string> removedIds,
            Func<string, GameObject> resolveCasterPrefab, Func<GameObject, string, bool> confirmDelete)
        {
            var result = new SyncResult { Success = true };
            ids ??= Array.Empty<string>();
            var removed = new HashSet<string>(removedIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids) removed.Remove(id);

            var controller = FindController(root);
            if (controller == null)
            {
                result.Success = false;
                result.Messages.Add("Prefab has no CharacterSkillController; skipped.");
                return result;
            }

            var skillList = FindSkillList(root, false);
            if (skillList == null)
            {
                if (ids.Count == 0) return result;
                skillList = FindSkillList(root, true);
                result.PrefabModified = true;
            }

            var children = MapChildren(skillList, ids, removed, null);

            // Removals
            foreach (var id in removed)
            {
                if (!children.TryGetValue(id, out var child)) continue;

                if (PrefabUtility.IsPartOfPrefabInstance(skillList.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(child))
                {
                    result.Success = false;
                    result.Messages.Add($"'{id}' child belongs to a base/nested prefab and cannot be deleted here; remove it in the source prefab.");
                    continue;
                }

                if (NeedsDeleteConfirmation(child, out string reason) && confirmDelete != null && !confirmDelete(child, reason))
                {
                    result.Messages.Add($"Kept '{id}' child (deletion not confirmed).");
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child);
                children.Remove(id);
                result.PrefabModified = true;
                result.Messages.Add($"Removed '{id}' child.");
            }

            // Additions
            var ordered = new List<GameObject>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!children.TryGetValue(id, out var child))
                {
                    var casterPrefab = resolveCasterPrefab?.Invoke(id);
                    if (casterPrefab == null)
                    {
                        result.Success = false;
                        result.Messages.Add($"No skill caster prefab found for '{id}'; skipped.");
                        continue;
                    }

                    child = PrefabUtility.InstantiatePrefab(casterPrefab, skillList) as GameObject;
                    if (child == null)
                    {
                        result.Success = false;
                        result.Messages.Add($"Failed to instantiate skill prefab for '{id}'.");
                        continue;
                    }
                    child.name = id;
                    children[id] = child;
                    result.PrefabModified = true;
                    result.Messages.Add($"Added '{id}' child.");
                }

                if (child.GetComponent<SkillCaster>() == null)
                    result.Messages.Add($"Warning: '{id}' child has no SkillCaster component.");
                ordered.Add(child);
            }

            // Controller list, in ID order
            var current = ReadControllerEntries(controller);
            bool same = current.Count == ordered.Count;
            for (int i = 0; same && i < ordered.Count; i++) same = current[i] == ordered[i];
            if (!same)
            {
                var so = new SerializedObject(controller);
                var list = so.FindProperty("skills");
                list.arraySize = ordered.Count;
                for (int i = 0; i < ordered.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("skillPrefab").objectReferenceValue = ordered[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                result.PrefabModified = true;
                result.Messages.Add("Rebuilt CharacterSkillController.skills in Default Skill ID order.");
            }

            return result;
        }

        private static bool NeedsDeleteConfirmation(GameObject child, out string reason)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child))
            {
                reason = "it is not a nested skill prefab instance (manually built object)";
                return true;
            }
            if (PrefabUtility.GetAddedComponents(child).Count > 0 || PrefabUtility.GetAddedGameObjects(child).Count > 0)
            {
                reason = "it has extra components or children beyond the nested prefab";
                return true;
            }
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(child, false))
            {
                reason = "it has local overrides";
                return true;
            }
            reason = null;
            return false;
        }
    }
}
