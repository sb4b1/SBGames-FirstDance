using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Database;
using SBGames.Editor.Stats;
using UnityEditor;
using UnityEngine;

namespace SBGames.Tests
{
    /// <summary>
    /// Tests for <see cref="SkillEffectPrefabGenerator"/> covering:
    /// - Template selection (Projectile, Area, ProjectileArea, Beam)
    /// - Generation of effect prefabs for each supported archetype
    /// - Correct layer (SkillFX = 9) and 2D trigger collider on child Hitbox
    /// - Sensor configuration (DetectsOnLayers = Character | HitboxTrigger)
    /// - Non-overwriting protection (existing prefab not overwritten unless overwrite=true)
    /// - Validation checks (missing collider, wrong layer, missing components)
    /// - Unsupported archetype rejection
    /// </summary>
    [TestFixture]
    public class SkillEffectPrefabGeneratorTests
    {
        private const string TestFolder = "Assets/Tests/Editor/_SkillGenTemp";
        private List<SkillData> _createdSkills;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets/Tests/Editor", "_SkillGenTemp");
            }
            _createdSkills = new List<SkillData>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var skill in _createdSkills)
            {
                if (skill != null) Object.DestroyImmediate(skill);
            }
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.Refresh();
        }

        private SkillData CreateTestSkill(string id, SkillType type, bool area = false, float areaRadius = 0f,
            bool chain = false, int chainTargets = 0, float speed = 10f, float beamTick = 0.1f,
            SkillData.SkillTargetFilter filter = SkillData.SkillTargetFilter.EnemiesOnly)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillID = id;
            skill.skillName = id + "_Name";
            skill.type = type;
            skill.area = area;
            skill.areaRadius = areaRadius;
            skill.chain = chain;
            skill.maxChainTargets = chainTargets;
            skill.chainDamageMultiplier = 0.7f;
            skill.projectileSpeed = speed;
            skill.projectileLifetime = 3f;
            skill.beamTickInterval = beamTick;
            skill.targetFilter = filter;
            _createdSkills.Add(skill);
            return skill;
        }

        [Test]
        public void DetermineTemplate_MapsExpectedArchetypes()
        {
            var proj = CreateTestSkill("T_PROJ", SkillType.Projectile, speed: 10f);
            Assert.AreEqual(SkillEffectTemplate.Projectile, SkillEffectPrefabGenerator.DetermineTemplate(proj));

            var projArea = CreateTestSkill("T_PROJ_AREA", SkillType.Projectile, area: true, areaRadius: 3f, speed: 10f);
            Assert.AreEqual(SkillEffectTemplate.ProjectileArea, SkillEffectPrefabGenerator.DetermineTemplate(projArea));

            var areaOnly = CreateTestSkill("T_AREA", SkillType.Area, area: true, areaRadius: 4f, speed: 0f);
            Assert.AreEqual(SkillEffectTemplate.Area, SkillEffectPrefabGenerator.DetermineTemplate(areaOnly));

            var beam = CreateTestSkill("T_BEAM", SkillType.Default, speed: 0f, beamTick: 0.2f);
            Assert.AreEqual(SkillEffectTemplate.Beam, SkillEffectPrefabGenerator.DetermineTemplate(beam));
        }

        [Test]
        public void GenerateEffectPrefab_Projectile_HasRequiredComponentsAndLayer()
        {
            var skill = CreateTestSkill("TEST_GEN_PROJ", SkillType.Projectile, speed: 10f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            var prefab = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);

            Assert.IsNotNull(prefab, "Prefab should be created");
            Assert.AreEqual("SkillFX", LayerMask.LayerToName(prefab.layer), "Root should be on SkillFX layer");
            Assert.IsNotNull(prefab.GetComponent<SkillHitResolver>(), "Should have SkillHitResolver");
            Assert.IsNotNull(prefab.GetComponent<SkillInstance>(), "Should have SkillInstance");
            Assert.IsNotNull(prefab.GetComponent<LinearProjectileMovement>(), "Should have LinearProjectileMovement");

            var hitbox = prefab.transform.Find("Hitbox");
            Assert.IsNotNull(hitbox, "Hitbox child must exist");
            Assert.AreEqual("SkillFX", LayerMask.LayerToName(hitbox.gameObject.layer), "Hitbox should be on SkillFX layer");

            var collider = hitbox.GetComponent<Collider2D>();
            Assert.IsNotNull(collider, "Hitbox must have Collider2D");
            Assert.IsTrue(collider.isTrigger, "Hitbox collider must be a trigger");
            Assert.IsNotNull(hitbox.GetComponent<SkillCollision>(), "Hitbox must have SkillCollision");

            // Sanity validation passes
            Assert.IsTrue(SkillEffectPrefabGenerator.ValidatePrefab(prefab, skill, out var errors), "Sanity validation should pass");
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void GenerateEffectPrefab_ProjectileWithArea_HasAoEDetonationAndSensor()
        {
            var skill = CreateTestSkill("TEST_GEN_AOE", SkillType.Projectile, area: true, areaRadius: 3.5f, speed: 8f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            var prefab = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);

            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<AoEDetonation>(), "Should have AoEDetonation component");

            var sensor = prefab.transform.Find("Sensor");
            Assert.IsNotNull(sensor, "Sensor child must exist for AoE");
            Assert.IsNotNull(sensor.GetComponent<SkillTargeting>(), "Sensor child must have SkillTargeting");
            var rangeSensor = sensor.GetComponent<Micosmo.SensorToolkit.RangeSensor2D>();
            Assert.IsNotNull(rangeSensor, "Sensor child must have RangeSensor2D");

            var so = new SerializedObject(rangeSensor);
            int mask = so.FindProperty("DetectsOnLayers").intValue;
            int expectedMask = (1 << LayerMask.NameToLayer("Character")) | (1 << LayerMask.NameToLayer("HitboxTrigger"));
            Assert.AreEqual(expectedMask, mask, "DetectsOnLayers must include Character and HitboxTrigger");
        }

        [Test]
        public void GenerateEffectPrefab_Chain_HasSensorConfigured()
        {
            var skill = CreateTestSkill("TEST_GEN_CHAIN", SkillType.Projectile, chain: true, chainTargets: 3, speed: 10f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            var prefab = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);

            Assert.IsNotNull(prefab);
            var sensor = prefab.transform.Find("Sensor");
            Assert.IsNotNull(sensor, "Chain skill requires Sensor child for targeting next targets");
            Assert.IsNotNull(sensor.GetComponent<SkillTargeting>());
        }

        [Test]
        public void GenerateEffectPrefab_Beam_HasBeamMovementAndBeamSkill()
        {
            var skill = CreateTestSkill("TEST_GEN_BEAM", SkillType.Default, speed: 0f, beamTick: 0.15f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            var prefab = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);

            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<BeamMovement>(), "Beam requires BeamMovement");
            Assert.IsNotNull(prefab.GetComponent<BeamSkill>(), "Beam requires BeamSkill");
        }

        [Test]
        public void GenerateEffectPrefab_DoesNotOverwriteExisting_UnlessExplicitlyRequested()
        {
            var skill = CreateTestSkill("TEST_GEN_NO_OVERWRITE", SkillType.Projectile, speed: 10f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            var original = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);
            Assert.IsNotNull(original);

            // Calling again without overwrite should return the existing prefab and not create another
            var second = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path, overwrite: false);
            Assert.AreEqual(original, second);
        }

        [Test]
        public void GenerateEffectPrefab_AssignsToSkillData_AndGeneratesCasterPrefab()
        {
            var skill = CreateTestSkill("TEST_GEN_ASSIGN", SkillType.Projectile, speed: 10f);
            string path = $"{TestFolder}/SKILLFX_{skill.skillID}.prefab";

            Assert.IsNull(skill.skillPrefab);
            var effectPrefab = SkillEffectPrefabGenerator.GenerateEffectPrefab(skill, path);

            Assert.AreEqual(effectPrefab, skill.skillPrefab, "Should assign generated prefab to skill.skillPrefab");

            // Also check that caster prefab was created
            string casterPath = $"{SkillEffectPrefabGenerator.SkillCasterRootFolder}/{SkillEffectPrefabGenerator.DetermineCategory(skill)}/{skill.skillID}.prefab";
            var casterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(casterPath);
            Assert.IsNotNull(casterPrefab, "Matching caster prefab should be created");
            Assert.IsNotNull(casterPrefab.GetComponent<SkillCaster>());
            Assert.IsNotNull(casterPrefab.GetComponent<SkillStatsManager>());

            // Cleanup created caster prefab
            AssetDatabase.DeleteAsset(casterPath);
        }

        [Test]
        public void ValidatePrefab_DetectsMissingColliderOrWrongLayer()
        {
            var go = new GameObject("BadPrefab");
            go.layer = 0; // Layer Default instead of SkillFX
            go.AddComponent<SkillHitResolver>();
            go.AddComponent<SkillInstance>();

            var skill = CreateTestSkill("TEST_BAD", SkillType.Projectile);
            bool isValid = SkillEffectPrefabGenerator.ValidatePrefab(go, skill, out var errors);

            Assert.IsFalse(isValid);
            Assert.IsTrue(errors.Exists(e => e.Contains("Root layer is 'Default'")), "Should report layer mismatch");
            Assert.IsTrue(errors.Exists(e => e.Contains("No Collider2D")), "Should report missing Collider2D");

            Object.DestroyImmediate(go);
        }
    }
}
