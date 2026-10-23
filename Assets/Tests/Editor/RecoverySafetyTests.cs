using SB4b1.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SBGames.Editor.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SBGames.Tests
{
    public class ArbitraryUtilityTestGoal : UtilityGoal
    {
        public float Score;
        public EnumUtilGoal Key;
        public override float Evaluate(BaseAI ai) => Score;
        public override EnumUtilGoal GetGOAPGoalKey() => Key;
    }

    public class RecoverySafetyTests
    {
        private GameObject root, enemy, caster;
        private BaseAI ai;
        private CharacterStatsManager stats;
        private CharacterSkillController controller;
        private readonly List<Object> owned = new();

        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Recoverer"); root.tag = "Adventurer";
            root.AddComponent<CharacterBrain>();
            stats = root.GetComponent<CharacterStatsManager>();
            var data = Own(ScriptableObject.CreateInstance<CharacterBaseStatSO>());
            data.MaxLife = 100f; data.MaxMana = 100f;
            stats.SetBaseStats(data);
            controller = root.AddComponent<CharacterSkillController>();
            ai = root.AddComponent<BaseAI>();
            Set(ai, "skillController", controller);
            ai.RecoverySafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/RecoverySafety_Default.asset");
            Assert.IsNotNull(ai.RecoverySafetyTuning);
            enemy = new GameObject("Threat"); enemy.transform.position = Vector3.right * 10f;
            caster = new GameObject("SelfHealCaster"); caster.transform.SetParent(root.transform);
            var skillCaster = caster.AddComponent<SkillCaster>();
            var heal = Own(ScriptableObject.CreateInstance<SkillData>());
            heal.skillID = "SELF_HEAL_TEST"; heal.type = SkillType.TargetedBurst;
            heal.targetFilter = SkillData.SkillTargetFilter.AlliesOnly; heal.selfTarget = SkillData.SelfTarget.Only;
            heal.baseHeal = 25f; heal.baseManaCost = 0f;
            caster.GetComponent<SkillStatsManager>().BaseData = heal;
            controller.AddSkill(new CharacterSkillController.Skill(caster, controller));
            stats.TakeDamage(new DamageData(80f, DamageType.True));
            (ai.AIMemory ?? root.AddComponent<CharacterAIMemory>()).ResetCombatMemory();
            ai.UpdateResources();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(enemy);
            foreach (var obj in owned) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelfOnlyHeal_SafeRecovers_HeavyPressureSurvives_AndActionObeys(bool pressure)
        {
            var recover = Own(ScriptableObject.CreateInstance<UtilityRecoverGoal>());
            var survive = Own(ScriptableObject.CreateInstance<UtilitySurviveGoal>());
            ai.Goals = new List<UtilityGoal> { recover, survive };
            if (pressure)
            {
                enemy.transform.position = root.transform.position + Vector3.right;
                ai.EnemyTargets = new List<Transform> { enemy.transform };
                for (int i = 0; i < 10; i++) ai.SenseEnemy();
            }
            Assert.IsTrue(ai.HasReadySelfOnlyHealSkill());
            Assert.AreEqual(pressure ? EnumUtilGoal.Survive : EnumUtilGoal.Recover, ai.EvaluateBestGoalForTesting());
            var action = new RecoverAction();
            var actionData = new RecoverAction.Data { AI = ai, Stats = stats, SkillController = controller };
            action.Perform(null, actionData, null);
            Assert.AreEqual(!pressure, controller.HasPendingCast);
            if (!pressure) Assert.AreEqual(root, controller.PendingTarget);
        }

        [Test]
        public void Safety_UsesEachCachedSignal_AndRecoversAfterMemoryReset()
        {
            Assert.AreEqual(1f, ai.RecoverySafety, 0.0001f);
            ai.AIMemory.RecordDamageIntake(new DamageData(30f, DamageType.True), enemy);
            Assert.AreEqual(0f, ai.RecoverySafety);
            (ai.AIMemory ?? root.AddComponent<CharacterAIMemory>()).ResetCombatMemory();
            Assert.AreEqual(1f, ai.RecoverySafety);
            ai.EnemyTargets = new List<Transform> { enemy.transform };
            float previous = 0f;
            for (int distance = 1; distance <= 6; distance++)
            {
                enemy.transform.position = Vector3.right * distance;
                float safety = ai.RecoverySafety;
                Assert.That(safety, Is.InRange(previous, 1f));
                previous = safety;
            }
            ai.EnemyTargets.Clear();
            for (int i = 0; i < 8; i++) ai.SenseEnemy();
            Assert.AreEqual(0f, ai.RecoverySafety);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EqualRawScores_SurviveWins_InEitherOrder_DespiteRecoverHysteresis(bool reverse)
        {
            var survive = Own(ScriptableObject.CreateInstance<ArbitraryUtilityTestGoal>());
            var recover = Own(ScriptableObject.CreateInstance<ArbitraryUtilityTestGoal>());
            survive.Key = EnumUtilGoal.Survive; recover.Key = EnumUtilGoal.Recover;
            survive.Score = recover.Score = 0.6f;
            ai.Goals = reverse ? new List<UtilityGoal> { survive, recover } : new List<UtilityGoal> { recover, survive };
            ai.SetActiveGoalForTesting(EnumUtilGoal.Recover);
            Assert.AreEqual(EnumUtilGoal.Survive, ai.EvaluateBestGoalForTesting());
        }

        [Test]
        public void Arbiter_ClampsFutureGoalOutputs_AndNeverStoresHysteresis()
        {
            var goal = Own(ScriptableObject.CreateInstance<ArbitraryUtilityTestGoal>());
            goal.Key = EnumUtilGoal.Recover;
            ai.Goals = new List<UtilityGoal> { goal };
            foreach (float input in new[] { float.NegativeInfinity, -10f, -0.01f, 0f, 0.2f, 0.99f, 1f, 1.02f, 100f, float.PositiveInfinity, float.NaN })
            {
                goal.Score = input;
                ai.SetActiveGoalForTesting(EnumUtilGoal.Recover);
                ai.EvaluateBestGoalForTesting();
                var scores = (SerializableDictionary<EnumUtilGoal, float>)typeof(BaseAI).GetField("goalScores", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ai);
                Assert.AreEqual(float.IsNaN(input) ? 0f : Mathf.Clamp01(input), scores.GetPairs()[0].Value);
            }
        }

        [Test]
        public void AllAuthoredGoals_StayNormalized_AcrossResourceThreatDistanceSweep()
        {
            var goals = new List<UtilityGoal>();
            foreach (string guid in AssetDatabase.FindAssets("t:UtilityGoal"))
                goals.Add(AssetDatabase.LoadAssetAtPath<UtilityGoal>(AssetDatabase.GUIDToAssetPath(guid)));
            Assert.Greater(goals.Count, 0);
            ai.Goals = goals;
            ai.EnemyTargets = new List<Transform> { enemy.transform };
            foreach (float hp in new[] { 1f, 20f, 40f, 70f, 100f })
            foreach (float mana in new[] { 0f, 50f, 100f })
            foreach (int threat in new[] { 0, 40, 100 })
            foreach (float distance in new[] { 1f, 3f, 10f })
            {
                stats.RestoreLife(100f);
                stats.TakeDamage(new DamageData(100f - hp, DamageType.True));
                stats.RestoreMana(100f); stats.TryConsumeMana(100f - mana);
                Set(ai, "threatLevel", threat); ai.SetFearLevel(threat);
                enemy.transform.position = Vector3.right * distance;
                ai.UpdateResources(); ai.AIMemory.ResetCombatMemory();
                ai.EvaluateBestGoalForTesting();
                var scores = (SerializableDictionary<EnumUtilGoal, float>)typeof(BaseAI).GetField("goalScores", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ai);
                foreach (var pair in scores.GetPairs()) Assert.That(pair.Value, Is.InRange(0f, 1f), pair.Key.ToString());
            }
        }

        [Test]
        public void RecoveryAndSurviveEvaluate_AllocateZeroManagedBytesAfterWarmup()
        {
            var recover = Own(ScriptableObject.CreateInstance<UtilityRecoverGoal>());
            var survive = Own(ScriptableObject.CreateInstance<UtilitySurviveGoal>());
            for (int i = 0; i < 100; i++) { recover.Evaluate(ai); survive.Evaluate(ai); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            float sum = 0f;
            for (int i = 0; i < 1000; i++) sum += recover.Evaluate(ai) + survive.Evaluate(ai);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Greater(sum, 0f);
            Assert.AreEqual(0L, bytes);
        }

        [Test]
        public void HealerPrefab_HasHealGoal_AndSuppliedAnimatedEffect()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N006.prefab");
            bool hasGoal = false;
            foreach (var goal in prefab.GetComponentInChildren<BaseAI>(true).Goals) if (goal is UtilityHealAllyGoal) hasGoal = true;
            Assert.IsTrue(hasGoal, "A heal skill without its utility goal cannot be selected by AI.");
            var fx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkillEffect/Default/SKILLFX_SKILL_HEAL_001.prefab");
            var renderer = fx.GetComponent<SpriteRenderer>();
            Assert.AreEqual("SkillFX", renderer.sortingLayerName);
            Assert.AreEqual(5, renderer.sortingOrder);
            Assert.AreEqual("Assets/Resources/Skills/Heal/H001/Sprite_S_H001.png", AssetDatabase.GetAssetPath(renderer.sprite));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animators/Skills/Heal/Anim_S_H001/Anim_S_H001_Hit.anim");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length);
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            var sprites = new HashSet<Object>();
            foreach (var frame in frames) sprites.Add(frame.value);
            Assert.AreEqual(12, sprites.Count);
            Assert.IsFalse(sprites.Contains(null));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(renderer.sprite));
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsTrue(importer.alphaIsTransparency);
        }

        [Test]
        public void UtilityCurveValidation_CatchesNegativePositiveAndTangentOvershoot()
        {
            Assert.IsTrue(AISetupValidatorCore.IsUtilityCurveNormalized(AnimationCurve.Linear(0, 0, 1, 1)));
            Assert.IsFalse(AISetupValidatorCore.IsUtilityCurveNormalized(AnimationCurve.Linear(0, -0.1f, 1, 1)));
            Assert.IsFalse(AISetupValidatorCore.IsUtilityCurveNormalized(AnimationCurve.Linear(0, 0, 1, 1.01f)));
            Assert.IsFalse(AISetupValidatorCore.IsUtilityCurveNormalized(new AnimationCurve(new Keyframe(0, 0.5f, 0, 8), new Keyframe(1, 0.5f, -8, 0))));
            Assert.IsFalse(AISetupValidatorCore.IsUtilityCurveNormalized(new AnimationCurve(new Keyframe(0, 0.5f, 0, -8), new Keyframe(1, 0.5f, 8, 0))));
        }
    }
}
