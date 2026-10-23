using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SBGames.Editor.Stats;
using UnityEditor;
using UnityEngine;

namespace SBGames.Tests
{
    /// <summary>
    /// Covers the Default Skill IDs -> character prefab sync used by the Stats &amp; Balance Manager.
    /// Builds throwaway prefabs in a temp folder; the skill caster resolver is injected.
    /// </summary>
    [TestFixture]
    public class CharacterPrefabSkillSyncTests
    {
        private const string TempFolder = "Assets/Tests/Editor/_SkillSyncTemp";
        private const string SkillA = "TEST_SKILL_A";
        private const string SkillB = "TEST_SKILL_B";

        private string _characterPath;
        private Dictionary<string, GameObject> _casterPrefabs;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets/Tests/Editor", "_SkillSyncTemp");

            _casterPrefabs = new Dictionary<string, GameObject>
            {
                [SkillA] = CreateCasterPrefab(SkillA),
                [SkillB] = CreateCasterPrefab(SkillB)
            };
            _characterPath = CreateCharacterPrefab("TEST_CHARACTER", withChildFor: null);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }

        private GameObject Resolve(string id) => _casterPrefabs.TryGetValue(id, out var p) ? p : null;

        private static bool AlwaysConfirm(GameObject child, string reason) => true;

        private static GameObject CreateCasterPrefab(string id)
        {
            var go = new GameObject(id);
            go.AddComponent<SkillCaster>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{TempFolder}/{id}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Root/Skills(CharacterSkillController)/SkillList, optionally with an existing skill child and an empty controller list.</summary>
        private string CreateCharacterPrefab(string name, string withChildFor)
        {
            var root = new GameObject(name);
            var skills = new GameObject(CharacterPrefabSkillSync.SkillsRootName);
            skills.transform.SetParent(root.transform, false);
            skills.AddComponent<CharacterSkillController>();
            var list = new GameObject(CharacterPrefabSkillSync.SkillListName);
            list.transform.SetParent(skills.transform, false);

            if (withChildFor != null)
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(_casterPrefabs[withChildFor], list.transform);
                child.name = withChildFor;
            }

            string path = $"{TempFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private GameObject LoadCharacter() => AssetDatabase.LoadAssetAtPath<GameObject>(_characterPath);

        private List<string> SkillListChildNames()
        {
            var names = new List<string>();
            var list = CharacterPrefabSkillSync.FindSkillList(LoadCharacter(), false);
            for (int i = 0; i < list.childCount; i++) names.Add(list.GetChild(i).name);
            return names;
        }

        private List<GameObject> ControllerEntries()
        {
            var result = new List<GameObject>();
            var controller = CharacterPrefabSkillSync.FindController(LoadCharacter());
            foreach (var s in controller.Skills) result.Add(s.SkillPrefab);
            return result;
        }

        [Test]
        public void AddSkill_SaveAll_CreatesNestedChildAndControllerEntry()
        {
            var edits = new PendingSkillEdits(null);
            Assert.IsTrue(edits.TryAdd(SkillA));
            Assert.IsTrue(edits.IsPendingAdd(SkillA));

            var result = edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));
            CollectionAssert.AreEqual(new[] { SkillA }, SkillListChildNames());
            var entries = ControllerEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(SkillA, entries[0].name);
            Assert.AreSame(CharacterPrefabSkillSync.FindSkillList(LoadCharacter(), false).GetChild(0).gameObject, entries[0]);
            Assert.AreSame(_casterPrefabs[SkillA], PrefabUtility.GetCorrespondingObjectFromSource(entries[0]));
            Assert.IsFalse(edits.HasPrefabChanges);
        }

        [Test]
        public void RemoveSkill_SaveAll_RemovesChildAndControllerEntry()
        {
            var edits = new PendingSkillEdits(null);
            edits.TryAdd(SkillA);
            edits.TryAdd(SkillB);
            edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            edits.RemoveAt(0);
            CollectionAssert.Contains(edits.RemovedIds, SkillA);
            var result = edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));
            CollectionAssert.AreEqual(new[] { SkillB }, SkillListChildNames());
            var entries = ControllerEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(SkillB, entries[0].name);
        }

        [Test]
        public void Revert_BeforeSave_LeavesPrefabUntouched()
        {
            string before = File.ReadAllText(_characterPath);
            var edits = new PendingSkillEdits(null);
            edits.TryAdd(SkillA);
            edits.RequestSync();

            Assert.AreEqual(before, File.ReadAllText(_characterPath), "Pending edits must not touch the prefab.");

            edits.Discard();

            Assert.IsFalse(edits.HasPrefabChanges);
            Assert.AreEqual(0, edits.Ids.Count);
            Assert.AreEqual(before, File.ReadAllText(_characterPath));
            Assert.AreEqual(0, SkillListChildNames().Count);
        }

        [Test]
        public void SaveAllTwice_IsIdempotent()
        {
            var edits = new PendingSkillEdits(null);
            edits.TryAdd(SkillA);
            edits.TryAdd(SkillB);
            edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);
            string afterFirst = File.ReadAllText(_characterPath);

            edits.RequestSync();
            var second = edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            Assert.IsTrue(second.Success);
            Assert.IsFalse(second.PrefabModified, string.Join("\n", second.Messages));
            Assert.AreEqual(afterFirst, File.ReadAllText(_characterPath));
            CollectionAssert.AreEqual(new[] { SkillA, SkillB }, SkillListChildNames());
            Assert.AreEqual(2, ControllerEntries().Count);
        }

        [Test]
        public void Reorder_UpdatesControllerOrder_NotHierarchyOrder()
        {
            var edits = new PendingSkillEdits(null);
            edits.TryAdd(SkillA);
            edits.TryAdd(SkillB);
            edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            edits.Move(1, 0);
            Assert.IsTrue(edits.HasPrefabChanges);
            edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            var entries = ControllerEntries();
            Assert.AreEqual(SkillB, entries[0].name);
            Assert.AreEqual(SkillA, entries[1].name);
            CollectionAssert.AreEqual(new[] { SkillA, SkillB }, SkillListChildNames());
        }

        [Test]
        public void Sync_FixesCharacterWithIdsButEmptyControllerList()
        {
            _characterPath = CreateCharacterPrefab("TEST_CHARACTER_EMPTY", withChildFor: SkillA);
            var ids = new[] { SkillA, SkillB };

            var before = CharacterPrefabSkillSync.Analyze(LoadCharacter(), ids);
            Assert.IsTrue(before.Exists(i => i.Kind == CharacterPrefabSkillSync.IssueKind.ChildNotInController && i.SkillId == SkillA));
            Assert.IsTrue(before.Exists(i => i.Kind == CharacterPrefabSkillSync.IssueKind.MissingChild && i.SkillId == SkillB));

            var edits = new PendingSkillEdits(ids);
            Assert.IsFalse(edits.HasPrefabChanges);
            edits.RequestSync();
            var result = edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));
            Assert.AreEqual(0, CharacterPrefabSkillSync.Analyze(LoadCharacter(), ids).Count);
            var entries = ControllerEntries();
            Assert.AreEqual(SkillA, entries[0].name);
            Assert.AreEqual(SkillB, entries[1].name);
        }

        [Test]
        public void ExtraManualChild_IsWarnedButNeverDeleted()
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(_characterPath))
            {
                var list = CharacterPrefabSkillSync.FindSkillList(scope.prefabContentsRoot, false);
                new GameObject("MANUAL_EXTRA").transform.SetParent(list, false);
            }

            var issues = CharacterPrefabSkillSync.Analyze(LoadCharacter(), new string[0]);
            Assert.IsTrue(issues.Exists(i => i.Kind == CharacterPrefabSkillSync.IssueKind.ExtraChild));
            Assert.IsFalse(issues.Exists(i => i.IsFixableBySync));

            var edits = new PendingSkillEdits(null);
            edits.RequestSync();
            edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);

            CollectionAssert.Contains(SkillListChildNames(), "MANUAL_EXTRA");
        }

        [Test]
        public void MissingPrefabOrCaster_FailsGracefullyWithoutThrowing()
        {
            var edits = new PendingSkillEdits(null);
            edits.TryAdd("UNKNOWN_SKILL");

            Assert.DoesNotThrow(() =>
            {
                var noPrefab = edits.Commit(null, Resolve, AlwaysConfirm);
                Assert.IsFalse(noPrefab.Success);
                var noCaster = edits.Commit(LoadCharacter(), Resolve, AlwaysConfirm);
                Assert.IsFalse(noCaster.Success);
            });
            Assert.IsTrue(edits.HasPrefabChanges, "Failed commits keep the queue for retry.");
        }

        [Test]
        public void AddSkill_AutoGeneratesUtilityAssetAndAssignsInMatchingOrder()
        {
            // SKILL_F001 has no utility asset initially
            var skillDb = SBGames.Database.Editor.DatabaseAutoIndexer.GetOrCreateCatalog<SBGames.Database.SkillDatabaseSO>("SkillDatabase");
            Assert.IsNotNull(skillDb);
            Assert.IsTrue(skillDb.TryGet("SKILL_F001", out var f001Data));

            string expectedPath = UtilitySkillGenerator.ResolveUtilityAssetPath(f001Data);
            if (File.Exists(expectedPath)) AssetDatabase.DeleteAsset(expectedPath);
            f001Data.utilityData = null;

            var edits = new PendingSkillEdits(null);
            edits.TryAdd("SKILL_F001");
            Assert.IsTrue(edits.HasPrefabChanges);

            var result = edits.Commit(LoadCharacter(), CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);
            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));

            // Verify utility asset was generated at the canonical path
            Assert.IsTrue(File.Exists(expectedPath), $"Expected utility asset at {expectedPath}");
            var generatedAsset = AssetDatabase.LoadAssetAtPath<UtilitySkill>(expectedPath);
            Assert.IsNotNull(generatedAsset);
            Assert.AreEqual("SKILL_F001", generatedAsset.GetID());

            // Verify BaseAI.skills has it in matching order
            var ai = LoadCharacter().GetComponentInChildren<BaseAI>(true);
            if (ai != null)
            {
                var aiSo = new SerializedObject(ai);
                var aiSkills = aiSo.FindProperty("skills");
                Assert.AreEqual(1, aiSkills.arraySize);
                Assert.AreSame(generatedAsset, aiSkills.GetArrayElementAtIndex(0).objectReferenceValue);
            }
        }

        [Test]
        public void RemoveSkill_RemovesAssignmentFromCharacter_UtilityAssetFileRemains()
        {
            var edits = new PendingSkillEdits(new[] { "SKILL_F001" });
            edits.Commit(LoadCharacter(), CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);

            var skillDb = SBGames.Database.Editor.DatabaseAutoIndexer.GetOrCreateCatalog<SBGames.Database.SkillDatabaseSO>("SkillDatabase");
            skillDb.TryGet("SKILL_F001", out var f001Data);
            string assetPath = UtilitySkillGenerator.ResolveUtilityAssetPath(f001Data);
            Assert.IsTrue(File.Exists(assetPath));

            // Now remove
            edits.RemoveAt(0);
            Assert.IsTrue(edits.HasPrefabChanges);
            var result = edits.Commit(LoadCharacter(), CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);
            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));

            // Assignment removed from controller
            Assert.AreEqual(0, ControllerEntries().Count);

            // BaseAI skills empty
            var ai = LoadCharacter().GetComponentInChildren<BaseAI>(true);
            if (ai != null)
            {
                var aiSo = new SerializedObject(ai);
                Assert.AreEqual(0, aiSo.FindProperty("skills").arraySize);
            }

            // Asset file MUST still exist on disk
            Assert.IsTrue(File.Exists(assetPath), "Utility asset file must remain when skill is removed from character.");
        }

        [Test]
        public void RevertBeforeSave_LeavesEverythingUntouched()
        {
            var edits = new PendingSkillEdits(new[] { SkillA });
            edits.TryAdd("SKILL_F001");
            Assert.AreEqual(2, edits.Ids.Count);
            Assert.IsTrue(edits.HasPrefabChanges);

            // Revert / Discard
            edits.Discard();
            Assert.IsFalse(edits.HasPrefabChanges);
            Assert.AreEqual(1, edits.Ids.Count);
            Assert.AreEqual(SkillA, edits.Ids[0]);
        }

        [Test]
        public void SaveAllTwiceWithGeneratedUtility_IsIdempotent()
        {
            var edits = new PendingSkillEdits(new[] { "SKILL_F001" });
            var res1 = edits.Commit(LoadCharacter(), CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);
            Assert.IsTrue(res1.Success);

            // Second commit
            edits.RequestSync();
            var res2 = edits.Commit(LoadCharacter(), CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);
            Assert.IsTrue(res2.Success);
            Assert.IsFalse(res2.PrefabModified, "Second save with unchanged edits must not modify the prefab.");
            Assert.AreEqual(1, ControllerEntries().Count);
        }

        [Test]
        public void Sync_FixesADVENTURER_N002()
        {
            var n002Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N002.prefab");
            Assert.IsNotNull(n002Prefab);

            var db = SBGames.Database.Editor.DatabaseAutoIndexer.GetOrCreateCatalog<SBGames.Database.CharacterDatabaseSO>("CharacterDatabase");
            var entry = db.Get("ADVENTURER_N002");
            Assert.IsNotNull(entry);

            // Request sync and commit
            var edits = new PendingSkillEdits(entry.defaultSkillIds);
            edits.RequestSync();
            var result = edits.Commit(n002Prefab, CharacterPrefabSkillSync.ResolveCasterPrefab, AlwaysConfirm);
            Assert.IsTrue(result.Success, string.Join("\n", result.Messages));

            var issues = CharacterPrefabSkillSync.Analyze(n002Prefab, entry.defaultSkillIds, CharacterPrefabSkillSync.SkillExistsInDatabase);
            Assert.AreEqual(0, issues.Count, string.Join("\n", issues.ConvertAll(i => i.Message)));
        }

        [Test]
        public void Validation_WarnsWhenMageAgentCannotPlanHealSkill()
        {
            var n099Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N099.prefab");
            Assert.IsNotNull(n099Prefab);

            var issues = CharacterPrefabSkillSync.Analyze(n099Prefab, new[] { "SKILL_HEAL_001" }, CharacterPrefabSkillSync.SkillExistsInDatabase);
            Assert.IsTrue(issues.Exists(i => i.Kind == CharacterPrefabSkillSync.IssueKind.IncompatibleAgentType),
                "Analyze must flag IncompatibleAgentType when MageAgent is assigned a heal skill.");
        }
    }
}
