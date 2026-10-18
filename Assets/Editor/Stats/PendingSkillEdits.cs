using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// In-memory, queued skill edits for one character row. Holds the Default Skill IDs being edited
    /// plus the queued prefab work (removals, explicit sync). Nothing touches the prefab until <see cref="Commit"/>.
    /// </summary>
    public class PendingSkillEdits
    {
        private readonly List<string> _savedIds;
        private readonly HashSet<string> _removedIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Current (possibly unsaved) Default Skill IDs, in controller order.</summary>
        public List<string> Ids { get; }

        /// <summary>Set by "Sync prefab skills": rebuild the prefab even if the IDs are unchanged.</summary>
        public bool SyncRequested { get; private set; }

        public IReadOnlyCollection<string> RemovedIds => _removedIds;

        public PendingSkillEdits(IEnumerable<string> savedIds)
        {
            _savedIds = savedIds != null ? new List<string>(savedIds) : new List<string>();
            Ids = new List<string>(_savedIds);
        }

        public bool HasPrefabChanges => SyncRequested || _removedIds.Count > 0 || !SameSequence(Ids, _savedIds);

        /// <summary>True when the ID was added since the last save (shown as "pending prefab change").</summary>
        public bool IsPendingAdd(string id) => !_savedIds.Exists(s => string.Equals(s, id, StringComparison.OrdinalIgnoreCase));

        public bool Contains(string id) => Ids.Exists(s => string.Equals(s, id, StringComparison.OrdinalIgnoreCase));

        public bool TryAdd(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || Contains(id)) return false;
            Ids.Add(id);
            _removedIds.Remove(id);
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Ids.Count) return;
            string id = Ids[index];
            Ids.RemoveAt(index);
            if (!IsPendingAdd(id)) _removedIds.Add(id);
        }

        public void Move(int from, int to)
        {
            if (from < 0 || from >= Ids.Count) return;
            to = Mathf.Clamp(to, 0, Ids.Count - 1);
            if (from == to) return;
            string id = Ids[from];
            Ids.RemoveAt(from);
            Ids.Insert(to, id);
        }

        public void RequestSync() => SyncRequested = true;

        /// <summary>Drops all queued edits and restores the last saved IDs.</summary>
        public void Discard()
        {
            Ids.Clear();
            Ids.AddRange(_savedIds);
            _removedIds.Clear();
            SyncRequested = false;
        }

        /// <summary>Records the current IDs as saved and clears queued prefab work.</summary>
        public void MarkSaved()
        {
            _savedIds.Clear();
            _savedIds.AddRange(Ids);
            _removedIds.Clear();
            SyncRequested = false;
        }

        /// <summary>Applies queued edits to the prefab asset. Clears the queue only on success.</summary>
        public CharacterPrefabSkillSync.SyncResult Commit(GameObject prefabAsset, Func<string, GameObject> resolveCasterPrefab,
            Func<GameObject, string, bool> confirmDelete)
        {
            var result = CharacterPrefabSkillSync.ApplyToPrefabAsset(prefabAsset, Ids, _removedIds, resolveCasterPrefab, confirmDelete);
            if (result.Success) MarkSaved();
            return result;
        }

        private static bool SameSequence(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
    }
}
