#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SBGames.Editor.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SBGames.Tests.Editor
{
    public class SpriteAnimationBuilderSkillTests
    {
        private const string TestOutputDir = "Assets/Tests/TempSkillAnimTest";

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TestOutputDir))
            {
                AssetDatabase.DeleteAsset(TestOutputDir);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void GetStandardPresets_IncludesSkillFXPresetsWithCorrectAttributes()
        {
            var presets = SpriteAnimationBuilder.GetStandardPresets();
            Assert.IsNotNull(presets);

            var skillPresets = presets.Where(p => p.category == PresetCategory.SkillFX).ToList();
            Assert.AreEqual(3, skillPresets.Count, "Expected exactly 3 Skill FX presets.");

            var projPreset = skillPresets.FirstOrDefault(p => p.presetName.Contains("Projectile"));
            Assert.IsNotNull(projPreset);
            Assert.AreEqual(2, projPreset.actions.Count);
            Assert.AreEqual("Move", projPreset.actions[0].actionName);
            Assert.IsTrue(projPreset.actions[0].isLooping);
            Assert.AreEqual("Hit", projPreset.actions[1].actionName);
            Assert.IsFalse(projPreset.actions[1].isLooping);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_ON_DESPAWN, projPreset.actions[1].completeEventName);

            var areaPreset = skillPresets.FirstOrDefault(p => p.presetName.Contains("Area Burst"));
            Assert.IsNotNull(areaPreset);
            Assert.AreEqual(2, areaPreset.actions.Count);
            Assert.AreEqual("Startup", areaPreset.actions[0].actionName);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_ACTIVATE_HITBOX, areaPreset.actions[0].impactEventName);
            Assert.AreEqual("Burst", areaPreset.actions[1].actionName);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_DEACTIVATE_HITBOX, areaPreset.actions[1].impactEventName);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_ON_DESPAWN, areaPreset.actions[1].completeEventName);

            var oneShotPreset = skillPresets.FirstOrDefault(p => p.presetName.Contains("One-Shot"));
            Assert.IsNotNull(oneShotPreset);
            Assert.AreEqual(1, oneShotPreset.actions.Count);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_ON_DESPAWN, oneShotPreset.actions[0].completeEventName);
        }

        [Test]
        public void GetDefaultMultiSheetSkillActions_ReturnsExpectedSlots()
        {
            var skillSlots = SpriteAnimationBuilder.GetDefaultMultiSheetSkillActions();
            Assert.IsNotNull(skillSlots);
            Assert.AreEqual(2, skillSlots.Count);
            Assert.AreEqual("Move", skillSlots[0].actionName);
            Assert.AreEqual("Hit", skillSlots[1].actionName);
            Assert.AreEqual(SpriteAnimationBuilder.EVENT_ON_DESPAWN, skillSlots[1].completeEventName);
        }

        [Test]
        public void CreateSkillFXAnimatorController_BuildsProjectileTransitionsProperly()
        {
            string controllerPath = $"{TestOutputDir}/TestProjectile.controller";
            var actionClips = new Dictionary<string, AnimationClip>
            {
                { "Move", new AnimationClip() },
                { "Hit", new AnimationClip() }
            };

            var controller = SpriteAnimationBuilder.CreateSkillFXAnimatorController(controllerPath, actionClips);
            Assert.IsNotNull(controller);

            // Check parameter
            Assert.IsTrue(controller.parameters.Any(p => p.name == SpriteAnimationBuilder.PARAM_ON_HIT && p.type == AnimatorControllerParameterType.Trigger), "Missing OnHit trigger parameter");

            // Check default state is Move
            var stateMachine = controller.layers[0].stateMachine;
            Assert.AreEqual("Move", stateMachine.defaultState.name);

            // Check transition from Move to Hit
            var moveTransitions = stateMachine.defaultState.transitions;
            Assert.AreEqual(1, moveTransitions.Length);
            Assert.AreEqual("Hit", moveTransitions[0].destinationState.name);
            Assert.IsFalse(moveTransitions[0].hasExitTime);
            Assert.IsTrue(moveTransitions[0].conditions.Any(c => c.parameter == SpriteAnimationBuilder.PARAM_ON_HIT));
        }

        [Test]
        public void CreateSkillFXAnimatorController_BuildsAreaBurstTransitionsProperly()
        {
            string controllerPath = $"{TestOutputDir}/TestAreaBurst.controller";
            var actionClips = new Dictionary<string, AnimationClip>
            {
                { "Startup", new AnimationClip() },
                { "Burst", new AnimationClip() }
            };

            var controller = SpriteAnimationBuilder.CreateSkillFXAnimatorController(controllerPath, actionClips);
            Assert.IsNotNull(controller);

            var stateMachine = controller.layers[0].stateMachine;
            Assert.AreEqual("Startup", stateMachine.defaultState.name);

            // Check transition from Startup to Burst
            var startTransitions = stateMachine.defaultState.transitions;
            Assert.AreEqual(1, startTransitions.Length);
            Assert.AreEqual("Burst", startTransitions[0].destinationState.name);
            Assert.IsTrue(startTransitions[0].hasExitTime);
        }
    }
}
#endif
