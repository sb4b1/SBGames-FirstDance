using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SBGames.Tests
{
    public class TargetedBurstVisualFitTests
    {
        private GameObject target, caster, fx;
        private SpriteRenderer body;
        private TargetedBurstDetonation burst;
        private SkillInstance skill;
        private SkillData data;
        private Texture2D texture;
        private Sprite bodySprite, effectSprite;
        private Action update;

        private void Set(string name, object value) => typeof(TargetedBurstDetonation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(burst, value);

        [SetUp]
        public void Setup()
        {
            target = new GameObject("VisualTarget"); target.tag = "Adventurer";
            caster = new GameObject("VisualCaster"); caster.tag = "Adventurer";
            texture = new Texture2D(64, 64);
            bodySprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), Vector2.one * 0.5f, 16f);
            body = target.AddComponent<SpriteRenderer>(); body.sprite = bodySprite;
            effectSprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100f);
            fx = new GameObject("AutoFitHeal"); fx.AddComponent<SpriteRenderer>().sprite = effectSprite;
            skill = fx.AddComponent<SkillInstance>();
            burst = fx.AddComponent<TargetedBurstDetonation>();
            Set("fitTargetVisuals", true); Set("fitReferenceSprite", effectSprite);
            data = ScriptableObject.CreateInstance<SkillData>();
            data.type = SkillType.TargetedBurst; data.targetFilter = SkillData.SkillTargetFilter.AlliesOnly;
            data.windupDuration = 100f; data.projectileLifetime = 200f;
            update = (Action)Delegate.CreateDelegate(typeof(Action), burst, typeof(TargetedBurstDetonation).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(fx); Object.DestroyImmediate(target); Object.DestroyImmediate(caster);
            Object.DestroyImmediate(data); Object.DestroyImmediate(bodySprite); Object.DestroyImmediate(effectSprite); Object.DestroyImmediate(texture);
        }

        private void Cast() => skill.Initialize(data, Vector3.right, "caster", target, caster);
        private Vector3 RingGround => fx.transform.TransformPoint(new Vector3(0f, -0.19f, 0f));

        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void FitsVisibleOutline_AndAnchorsRingToFeet_ForEachTargetSize(float size)
        {
            target.transform.localScale = Vector3.one * size;
            Cast();
            float expected = Mathf.Max(size / 0.416f, size * 2f / 0.51f) * 1.1f;
            Assert.AreEqual(expected, fx.transform.localScale.x, 0.0001f);
            Assert.AreEqual(expected, fx.transform.localScale.y, 0.0001f);
            Assert.That((RingGround - new Vector3(0f, -size, 0f)).sqrMagnitude, Is.LessThan(0.000001f));
        }

        [Test]
        public void TracksMovementScaleAndMirroring_WithoutAnimationSizeJitter()
        {
            Cast(); float originalScale = fx.transform.localScale.x;
            target.transform.position = new Vector3(3f, 5f, 0f);
            target.transform.localScale = new Vector3(-2f, 2f, 1f);
            update();
            Assert.AreEqual(originalScale * 2f, fx.transform.localScale.x, 0.0001f);
            Assert.Greater(fx.transform.localScale.x, 0f);
            Assert.That((RingGround - new Vector3(3f, 3f, 0f)).sqrMagnitude, Is.LessThan(0.000001f));
            body.sprite = effectSprite; update();
            Assert.AreEqual(originalScale * 2f, fx.transform.localScale.x, 0.0001f, "A different animation frame must not resize the ring.");
        }

        [Test]
        public void IgnoresChildEffectsAndUI_WhenMeasuringCharacter()
        {
            var decoration = new GameObject("ExistingAura"); decoration.transform.SetParent(target.transform);
            var renderer = decoration.AddComponent<SpriteRenderer>(); renderer.sprite = bodySprite;
            decoration.transform.localScale = Vector3.one * 100f;
            renderer.sortingLayerName = "SkillFX";
            Cast(); float scale = fx.transform.localScale.x;
            Assert.AreEqual(2f / 0.51f * 1.1f, scale, 0.0001f);
            renderer.sortingLayerName = "UI"; Cast();
            Assert.AreEqual(scale, fx.transform.localScale.x, 0.0001f);
        }

        [Test]
        public void PoolReuse_RebindsTargetSize_AndFallsBackWithoutVisuals()
        {
            target.transform.localScale = Vector3.one * 2f; Cast(); float large = fx.transform.localScale.x;
            burst.OnDespawn(); skill.OnDespawn();
            target.transform.localScale = Vector3.one * 0.5f; Cast();
            Assert.AreEqual(large / 4f, fx.transform.localScale.x, 0.0001f);
            burst.OnDespawn(); skill.OnDespawn();
            body.enabled = false; Cast();
            Assert.AreEqual(Vector3.one, fx.transform.localScale);
            Assert.AreEqual(target.transform.position, fx.transform.position);
        }

        [Test]
        public void ImportedCharacter_UsesVisibleOutlineRatherThanPaddedCanvasForFeet()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N006.prefab");
            Sprite sprite = prefab.GetComponentInChildren<SpriteRenderer>(true).sprite;
            Assert.Greater(sprite.GetPhysicsShapeCount(), 0);
            var points = new List<Vector2>();
            float minimum = float.PositiveInfinity;
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, points);
                foreach (var point in points) minimum = Mathf.Min(minimum, point.y);
            }
            Assert.Greater(minimum, sprite.bounds.min.y, "Fixture must contain transparent bottom padding.");
            body.sprite = sprite;
            Cast();
            Assert.AreEqual(minimum, RingGround.y, 0.0001f);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, points);
                foreach (var point in points) maxY = Mathf.Max(maxY, point.y);
            }
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, points);
                foreach (var point in points)
                {
                    if (point.y > minimum + (maxY - minimum) * 0.15f) continue;
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                }
            }
            float feetCenter = (minX + maxX) * 0.5f;
            Assert.Greater(Mathf.Abs(feetCenter), 0.01f, "The fixture must have an off-center foot silhouette.");
            Assert.AreEqual(feetCenter, RingGround.x, 0.0001f, "Center on the feet rather than the weapon-inclusive silhouette.");
            body.flipX = true; update();
            Assert.AreEqual(-feetCenter, RingGround.x, 0.0001f, "Mirroring must mirror the foot anchor.");
        }

        [Test]
        public void PlacementUpdate_AllocatesZeroManagedBytesAfterBinding()
        {
            Cast(); for (int i = 0; i < 50; i++) update();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) update();
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}

