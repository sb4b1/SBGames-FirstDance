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
    public class HealAllyGoalTests
    {
        private GameObject root, ally;
        private BaseAI ai;
        private CharacterStatsManager stats, allyStats;
        private CharacterSkillController controller;
        private SkillCaster caster;
        private SkillData heal;
        private UtilityHealAllyGoal goal;
        private readonly List<Object> owned = new();
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Healer"); root.tag = "Adventurer";
            root.AddComponent<CharacterBrain>();
            stats = root.GetComponent<CharacterStatsManager>();
            var data = Own(ScriptableObject.CreateInstance<CharacterBaseStatSO>());
            data.MaxLife = 100; data.MaxMana = 100;
            stats.SetBaseStats(data);
            controller = root.AddComponent<CharacterSkillController>();
            ai = root.AddComponent<BaseAI>(); Set(ai, "skillController", controller);
            var profile = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset")));
            Set(profile, "rank", CharacterRank.B);
            Set(profile, "healReactionDelay", 0f);
            ai.ApplyAdventurerProfile(profile);
            ally = new GameObject("Ally"); ally.tag = "Adventurer";
            allyStats = ally.AddComponent<CharacterStatsManager>(); allyStats.SetBaseStats(data);
            ai.AllyTargets = new List<Transform> { ally.transform, root.transform };
            var skillObject = new GameObject("HealCaster"); skillObject.transform.SetParent(root.transform);
            caster = skillObject.AddComponent<SkillCaster>();
            heal = Own(ScriptableObject.CreateInstance<SkillData>());
            heal.skillID = "HEAL_GOAL_TEST"; heal.targetFilter = SkillData.SkillTargetFilter.AlliesOnly;
            heal.baseHeal = 25; heal.baseManaCost = 10;
            caster.GetComponent<SkillStatsManager>().BaseData = heal;
            controller.AddSkill(new CharacterSkillController.Skill(skillObject, controller));
            goal = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<UtilityHealAllyGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultHealAllyGoal.asset")));
            ai.UpdateResources();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(ally);
            foreach (var obj in owned) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        [Test]
        public void NoWound_NoScore() => Assert.Zero(goal.Evaluate(ai));

        [Test]
        public void NoAgentProfile_UsesGoalConfiguredTuning()
        {
            var profile = ai.AdventurerProfile;
            Assert.IsNotNull(new SerializedObject(goal).FindProperty("fallbackProfile").objectReferenceValue);
            Set(ai, "adventurerProfile", null);
            Set(goal, "fallbackProfile", profile);
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            Assert.Greater(goal.Evaluate(ai), 0f);
            Set(goal, "fallbackProfile", null);
            Assert.Zero(goal.Evaluate(ai));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NotReady_CooldownOrMana_NoScore(bool noMana)
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            Assert.Greater(goal.Evaluate(ai), 0);
            if (noMana) stats.TryConsumeMana(100);
            else Set(caster, "cooldownTimer", 10f);
            Assert.Zero(goal.Evaluate(ai));
        }

        [Test]
        public void SelfExcludedOrOnly_NoAllyGoal()
        {
            stats.TakeDamage(new DamageData(20, DamageType.True));
            heal.selfTarget = SkillData.SelfTarget.Excluded;
            Assert.Zero(goal.Evaluate(ai));
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            heal.selfTarget = SkillData.SelfTarget.Only;
            Assert.Zero(goal.Evaluate(ai));
            heal.selfTarget = SkillData.SelfTarget.Allowed;
            Assert.Greater(goal.Evaluate(ai), 0);
        }

        [Test]
        public void LowestHealthSweep_IsMonotonicAndNormalized()
        {
            float previous = 0;
            for (int damage = 1; damage <= 99; damage++)
            {
                allyStats.TakeDamage(new DamageData(1, DamageType.True));
                float score = goal.Evaluate(ai);
                Assert.That(score, Is.InRange(previous, 1f)); previous = score;
            }
            Assert.Greater(previous, 0.55f);
            allyStats.TakeDamage(new DamageData(1, DamageType.True));
            Assert.Zero(goal.Evaluate(ai), "Dead candidates are excluded.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HealthyHealerUnderThreat_DoesNotSelectHealAlly(bool lowHealth)
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            if (lowHealth) stats.TakeDamage(new DamageData(90, DamageType.True));
            ai.RecoverySafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/RecoverySafety_Default.asset");
            ai.HealSafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/HealSafety_Default.asset");
            for (int i = 0; i < 10; i++) ai.SenseEnemy();
            var survive = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<UtilitySurviveGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultSurviveGoal.asset")));
            ai.Goals = new List<UtilityGoal> { goal, survive };
            ai.SetActiveGoalForTesting(EnumUtilGoal.HealAlly);

            Assert.Greater(survive.Evaluate(ai), goal.Evaluate(ai));
            Assert.AreEqual(0f, goal.Evaluate(ai));
            Assert.AreEqual(EnumUtilGoal.Survive, ai.EvaluateBestGoalForTesting());
        }

        [Test]
        public void HealScore_FallsAsSafetyFalls_AndIsZeroWhenFullyThreatened()
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            ai.HealSafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/HealSafety_Default.asset");
            ai.WoundedAllyDetectedTime = Time.time - 2f;

            // Safe baseline
            Set(ai, "threatLevel", 0);
            float safeScore = goal.Evaluate(ai);
            Assert.Greater(safeScore, 0.5f);

            // Partial threat
            Set(ai, "threatLevel", 40);
            float midScore = goal.Evaluate(ai);
            Assert.Less(midScore, safeScore);
            Assert.Greater(midScore, 0f);

            // Maximum threat (maximumThreat = 80f)
            Set(ai, "threatLevel", 80);
            float maxThreatScore = goal.Evaluate(ai);
            Assert.AreEqual(0f, maxThreatScore);
        }

        [Test]
        public void HealScore_UnchangedWhenSafe()
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            ai.HealSafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/HealSafety_Default.asset");
            ai.WoundedAllyDetectedTime = Time.time - 2f;
            Set(ai, "threatLevel", 0);
            Assert.AreEqual(1f, ai.HealSafety);

            float threshold = ai.AdventurerProfile.HealHpThreshold;
            float deficit = (threshold - 0.1f) / threshold;
            var curve = new SerializedObject(goal).FindProperty("urgencyCurve").animationCurveValue;
            float expected = curve.Evaluate(deficit);
            Assert.AreEqual(expected, goal.Evaluate(ai), 0.0001f);
        }

        [Test]
        public void HealAlly_NeverLeaves01_AcrossSweep()
        {
            ai.HealSafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/HealSafety_Default.asset");
            ai.WoundedAllyDetectedTime = Time.time - 2f;

            for (float allyHp = 10f; allyHp <= 100f; allyHp += 10f)
            {
                Set(allyStats, "life", allyHp);
                for (int threat = 0; threat <= 120; threat += 15)
                {
                    Set(ai, "threatLevel", threat);
                    float score = goal.Evaluate(ai);
                    Assert.GreaterOrEqual(score, 0f);
                    Assert.LessOrEqual(score, 1f);
                }
            }
        }

        [Test]
        public void RetreatTypeGoal_BeatsHealAlly_UnderThreat()
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            ai.RecoverySafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/RecoverySafety_Default.asset");
            ai.HealSafetyTuning = AssetDatabase.LoadAssetAtPath<RecoverySafetyTuningSO>("Assets/Resources/AI/HealSafety_Default.asset");
            for (int i = 0; i < 10; i++) ai.SenseEnemy();

            var survive = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<UtilitySurviveGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultSurviveGoal.asset")));
            float surviveScore = survive.Evaluate(ai);
            float healScore = goal.Evaluate(ai);

            Assert.Greater(surviveScore, healScore);
            Assert.Greater(surviveScore, 0.5f);
            Assert.AreEqual(0f, healScore);
        }

        [Test]
        public void Evaluate_AllocatesZeroManagedBytes()
        {
            allyStats.TakeDamage(new DamageData(90, DamageType.True));
            for (int i = 0; i < 100; i++) goal.Evaluate(ai);
            long before = GC.GetAllocatedBytesForCurrentThread(); float sum = 0;
            for (int i = 0; i < 1000; i++) sum += goal.Evaluate(ai);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Greater(sum, 0); Assert.Zero(bytes);
        }

        [Test]
        public void Validator_GoalRemoved_ReportsCharacterFinding()
        {
            ai.Goals = new List<UtilityGoal> { goal };
            var findings = new List<ValidationFinding>();
            ValidationService.ValidateHealerGoal(root, "HEALER", "Assets/Healer.prefab", findings);
            Assert.IsEmpty(findings);
            ai.Goals.Clear();
            ValidationService.ValidateHealerGoal(root, "HEALER", "Assets/Healer.prefab", findings);
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("HEALER", findings[0].SubjectId);
            StringAssert.Contains("missing a HealAlly", findings[0].Message);
        }

        [Test]
        public void SelfExcluded_WithAIOnChild_UsesCharacterRoot()
        {
            var profile = ai.AdventurerProfile;
            Object.DestroyImmediate(ai);
            var augment = new GameObject("AI_Augment"); augment.transform.SetParent(root.transform);
            ai = augment.AddComponent<BaseAI>(); Set(ai, "skillController", controller);
            ai.ApplyAdventurerProfile(profile);
            ai.AllyTargets = new List<Transform> { root.transform };
            stats.TakeDamage(new DamageData(55, DamageType.True));
            heal.selfTarget = SkillData.SelfTarget.Excluded;
            Assert.IsFalse(ai.GetBestWoundedAllyTarget(out _, out _, SkillData.SelfTarget.Excluded));
            Assert.Zero(goal.Evaluate(ai));
        }

        [Test]
        public void AllReadySkills_AreCheckedForPermittedWounds()
        {
            stats.TakeDamage(new DamageData(55, DamageType.True));
            heal.selfTarget = SkillData.SelfTarget.Excluded;
            Assert.Zero(goal.Evaluate(ai));
            var otherData = Own(Object.Instantiate(heal));
            otherData.selfTarget = SkillData.SelfTarget.Allowed;
            var otherCaster = new GameObject("AllowedHeal"); otherCaster.transform.SetParent(root.transform);
            otherCaster.AddComponent<SkillCaster>();
            otherCaster.GetComponent<SkillStatsManager>().BaseData = otherData;
            controller.AddSkill(new CharacterSkillController.Skill(otherCaster, controller));
            Assert.Greater(goal.Evaluate(ai), 0f);
        }

        [Test]
        public void UrgencyUsesLowestHP_WhileProfileStillPrefersTank()
        {
            var profile = ai.AdventurerProfile;
            Set(profile, "healPriorityMode", HealerPriorityMode.TankFirstThenLowestHp);
            allyStats.SetFunctionOverride(AdventurerFunction.Tank);
            allyStats.TakeDamage(new DamageData(60, DamageType.True));
            var low = Own(new GameObject("LowerHealthAlly"));
            var lowStats = low.AddComponent<CharacterStatsManager>(); lowStats.SetBaseStats(stats.BaseStats);
            lowStats.TakeDamage(new DamageData(90, DamageType.True));
            ai.AllyTargets.Add(low.transform);
            Assert.IsTrue(ai.GetBestWoundedAllyTarget(out var target, out _));
            Assert.AreEqual(ally.transform, target);
            ai.WoundedAllyDetectedTime = Time.time - 2f;
            float threshold = profile.HealHpThreshold;
            var curve = new SerializedObject(goal).FindProperty("urgencyCurve").animationCurveValue;
            Assert.AreEqual(curve.Evaluate((threshold - 0.1f) / threshold), goal.Evaluate(ai), 0.0001f);
        }

        [Test]
        public void HealTelemetry_SkipsDestroyedCombatants()
        {
            var harness = Own(new GameObject("Runner"));
            var runner = harness.AddComponent<SBGames.Testing.BattleSimulationTestRunner>();
            var dead = new GameObject("DestroyedCombatant");
            var list = (List<SBGames.Testing.BattleSimulationTestRunner.TrackedCombatant>)typeof(SBGames.Testing.BattleSimulationTestRunner)
                .GetField("activeCombatants", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
            list.Add(new SBGames.Testing.BattleSimulationTestRunner.TrackedCombatant(dead, null, null, null,
                SBGames.Testing.BattleSimulationTestRunner.BattleFaction.Adventurer, null));
            var live = new SBGames.Testing.BattleSimulationTestRunner.TrackedCombatant(ally, null, allyStats, null,
                SBGames.Testing.BattleSimulationTestRunner.BattleFaction.Adventurer, null);
            list.Add(live);
            Object.DestroyImmediate(dead);
            typeof(SBGames.Testing.BattleSimulationTestRunner).GetMethod("HandleSkillHealWithOverhealApplied", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runner, new object[] { null, ally, 5f, 20f });
            Assert.AreEqual(1, live.SkillHealsApplied);
            Assert.AreEqual(5f, live.SkillHealingReceived);
            Assert.AreEqual(20f, live.OverhealReceived);
        }
    }
}
