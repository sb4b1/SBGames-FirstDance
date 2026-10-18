using System;
using System.Collections.Generic;
using System.IO;
using Micosmo.SensorToolkit;
using SBGames.Database;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    /// <summary>
    /// Supported prefab template archetypes for generating skill effect prefabs.
    /// </summary>
    public enum SkillEffectTemplate
    {
        None = 0,
        Projectile,
        ProjectileArea,
        Area,
        Beam
    }

    /// <summary>
    /// Service for auto-generating, validating, and regenerating skill effect and caster prefabs
    /// from <see cref="SkillData"/> rows in the Stats &amp; Balance tool.
    /// Editor-only: builds prefab assets; never mutates ScriptableObject templates at runtime.
    /// </summary>
    public static class SkillEffectPrefabGenerator
    {
        public const string SkillEffectRootFolder = "Assets/Prefabs/SkillEffect";
        public const string SkillCasterRootFolder = "Assets/Prefabs/Skill";
        public const string PlaceholderSpritePath = "UI/Skin/Knob.psd";
        public const string SkillFXLayerName = "SkillFX";
        public const string CharacterLayerName = "Character";
        public const string HitboxTriggerLayerName = "HitboxTrigger";

        /// <summary>
        /// Maps a <see cref="SkillData"/> to its corresponding template archetype.
        /// Returns <see cref="SkillEffectTemplate.None"/> if the configuration has no matching template.
        /// </summary>
        public static SkillEffectTemplate DetermineTemplate(SkillData skill)
        {
            if (skill == null) return SkillEffectTemplate.None;

            // Beam skills
            if (skill.beamTickInterval > 0f && (skill.type == SkillType.Default || skill.type == SkillType.Projectile) && skill.projectileSpeed <= 0f)
            {
                return SkillEffectTemplate.Beam;
            }

            // Projectile with Area
            if ((skill.type == SkillType.Projectile || (skill.type == SkillType.Default && skill.projectileSpeed > 0f)) &&
                (skill.area || skill.areaRadius > 0f))
            {
                return SkillEffectTemplate.ProjectileArea;
            }

            // Pure Area
            if (skill.type == SkillType.Area || (skill.area && skill.projectileSpeed <= 0f))
            {
                return SkillEffectTemplate.Area;
            }

            // Projectile (including Chain)
            if (skill.type == SkillType.Projectile || skill.type == SkillType.Default)
            {
                return SkillEffectTemplate.Projectile;
            }

            return SkillEffectTemplate.None;
        }

        /// <summary>
        /// Checks whether a skill effect prefab can be generated from the given <see cref="SkillData"/>.
        /// </summary>
        public static bool CanGenerate(SkillData skill, out string reason)
        {
            if (skill == null)
            {
                reason = "SkillData is null.";
                return false;
            }

            var template = DetermineTemplate(skill);
            if (template == SkillEffectTemplate.None)
            {
                reason = $"Unsupported skill configuration for '{skill.skillID}' (Type: {skill.type}, Area: {skill.area}, BeamTick: {skill.beamTickInterval}). No matching template.";
                return false;
            }

            int fxLayer = LayerMask.NameToLayer(SkillFXLayerName);
            if (fxLayer < 0)
            {
                reason = $"Layer '{SkillFXLayerName}' is not defined in TagManager.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Resolves the target folder for the skill effect prefab based on the skill's category or ID.
        /// </summary>
        public static string ResolveEffectFolderPath(SkillData skill)
        {
            string category = DetermineCategory(skill);
            return $"{SkillEffectRootFolder}/{category}";
        }

        /// <summary>
        /// Resolves the target folder for the skill caster prefab based on the skill's category or ID.
        /// </summary>
        public static string ResolveCasterFolderPath(SkillData skill)
        {
            string category = DetermineCategory(skill);
            return $"{SkillCasterRootFolder}/{category}";
        }

        public static string DetermineCategory(SkillData skill)
        {
            if (skill == null) return "Default";
            string id = skill.skillID ?? "";
            if (id.StartsWith("SKILL_F", StringComparison.OrdinalIgnoreCase)) return "Fire";
            if (id.StartsWith("SKILL_L", StringComparison.OrdinalIgnoreCase)) return "Lightning";
            if (id.StartsWith("SKILL_WA", StringComparison.OrdinalIgnoreCase)) return "Water";
            if (id.StartsWith("SKILL_WI", StringComparison.OrdinalIgnoreCase)) return "Wind";
            return "Default";
        }

        /// <summary>
        /// Generates the skill effect prefab for <paramref name="skill"/>.
        /// Also generates the matching caster prefab if one does not already exist.
        /// </summary>
        public static GameObject GenerateEffectPrefab(SkillData skill, string outputPath = null, bool overwrite = false)
        {
            if (!CanGenerate(skill, out string reason))
            {
                Debug.LogError($"[SkillEffectPrefabGenerator] Cannot generate effect prefab for '{skill?.skillID}': {reason}");
                return null;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                string folder = ResolveEffectFolderPath(skill);
                EnsureFolderExists(folder);
                outputPath = $"{folder}/SKILLFX_{skill.skillID}.prefab";
            }
            else
            {
                string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder)) EnsureFolderExists(folder);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) != null && !overwrite)
            {
                Debug.LogWarning($"[SkillEffectPrefabGenerator] Prefab at '{outputPath}' already exists and overwrite=false; skipped generation.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            }

            int fxLayer = LayerMask.NameToLayer(SkillFXLayerName);
            var template = DetermineTemplate(skill);

            // 1. Root GameObject
            var root = new GameObject($"SKILLFX_{skill.skillID}");
            root.layer = fxLayer;

            try
            {
                // Visuals: SpriteRenderer with placeholder knob sprite
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(PlaceholderSpritePath);
                sr.sortingOrder = 5;

                // Core components
                root.AddComponent<SkillHitResolver>();
                var skillInstance = root.AddComponent<SkillInstance>();

                // Movement / Archetype components
                switch (template)
                {
                    case SkillEffectTemplate.Projectile:
                        root.AddComponent<LinearProjectileMovement>();
                        break;
                    case SkillEffectTemplate.ProjectileArea:
                        root.AddComponent<LinearProjectileMovement>();
                        root.AddComponent<AoEDetonation>();
                        break;
                    case SkillEffectTemplate.Area:
                        root.AddComponent<AoEDetonation>();
                        break;
                    case SkillEffectTemplate.Beam:
                        root.AddComponent<BeamMovement>();
                        root.AddComponent<BeamSkill>();
                        break;
                }

                // Hitbox child
                var hitbox = new GameObject("Hitbox");
                hitbox.layer = fxLayer;
                hitbox.transform.SetParent(root.transform, false);

                var circleCollider = hitbox.AddComponent<CircleCollider2D>();
                circleCollider.isTrigger = true;
                circleCollider.radius = 0.4f;

                hitbox.AddComponent<SkillCollision>();

                // Wire hitbox into SkillInstance via SerializedObject
                var instSO = new SerializedObject(skillInstance);
                var hitboxProp = instSO.FindProperty("hitboxObject");
                if (hitboxProp != null)
                {
                    hitboxProp.objectReferenceValue = hitbox;
                    instSO.ApplyModifiedPropertiesWithoutUndo();
                }

                // Sensor child (for Chain, Area, or Beam)
                bool needsSensor = (template == SkillEffectTemplate.Projectile && (skill.chain || skill.maxChainTargets > 0)) ||
                                   template == SkillEffectTemplate.ProjectileArea ||
                                   template == SkillEffectTemplate.Area ||
                                   template == SkillEffectTemplate.Beam;

                if (needsSensor)
                {
                    var sensorObj = new GameObject("Sensor");
                    sensorObj.layer = fxLayer;
                    sensorObj.transform.SetParent(root.transform, false);

                    var rangeSensor = sensorObj.AddComponent<RangeSensor2D>();
                    ConfigureRangeSensor(rangeSensor, skill, template);

                    var targeting = sensorObj.AddComponent<SkillTargeting>();
                    var targetingSO = new SerializedObject(targeting);
                    var sensorProp = targetingSO.FindProperty("sensor");
                    if (sensorProp != null)
                    {
                        sensorProp.objectReferenceValue = rangeSensor;
                        targetingSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                // Save effect prefab
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                Debug.Log($"[SkillEffectPrefabGenerator] Generated skill effect prefab '{outputPath}' for skill '{skill.skillID}'.", savedPrefab);

                // Assign back to SkillData asset
                if (skill.skillPrefab != savedPrefab)
                {
                    skill.skillPrefab = savedPrefab;
                    EditorUtility.SetDirty(skill);
                }

                // Ensure matching caster prefab exists
                EnsureCasterPrefabExists(skill);

                return savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureRangeSensor(RangeSensor2D sensor, SkillData skill, SkillEffectTemplate template)
        {
            var sensorSO = new SerializedObject(sensor);

            // Shape: Circle (0)
            var shapeProp = sensorSO.FindProperty("Shape");
            if (shapeProp != null) shapeProp.enumValueIndex = 0;

            // Radius
            float radius = 5f;
            if (template == SkillEffectTemplate.Area || template == SkillEffectTemplate.ProjectileArea)
            {
                radius = skill.areaRadius > 0f ? skill.areaRadius : 3f;
            }
            else if (skill.chain || skill.maxChainTargets > 0)
            {
                radius = 7f;
            }
            var radiusProp = sensorSO.FindProperty("Circle.Radius");
            if (radiusProp != null) radiusProp.floatValue = radius;

            // DetectsOnLayers: Characters (Layer 6) + HitboxTrigger (Layer 7)
            int charLayer = LayerMask.NameToLayer(CharacterLayerName);
            int triggerLayer = LayerMask.NameToLayer(HitboxTriggerLayerName);
            int mask = 0;
            if (charLayer >= 0) mask |= (1 << charLayer);
            if (triggerLayer >= 0) mask |= (1 << triggerLayer);
            if (mask == 0) mask = 64; // fallback to layer 6

            var detectsProp = sensorSO.FindProperty("DetectsOnLayers");
            if (detectsProp != null) detectsProp.intValue = mask;

            var ignoreTriggers = sensorSO.FindProperty("IgnoreTriggerColliders");
            if (ignoreTriggers != null) ignoreTriggers.boolValue = false;

            var pulseMode = sensorSO.FindProperty("pulseRoutine.Mode.value");
            if (pulseMode != null) pulseMode.enumValueIndex = 2; // EachFrame

            var pulseInterval = sensorSO.FindProperty("pulseRoutine.Interval.value");
            if (pulseInterval != null) pulseInterval.floatValue = 1f;

            sensorSO.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Ensures a skill caster prefab exists under <see cref="SkillCasterRootFolder"/> referencing this SkillData.
        /// If not found, creates one with <see cref="SkillStatsManager"/> and <see cref="SkillCaster"/>.
        /// </summary>
        public static GameObject EnsureCasterPrefabExists(SkillData skill, string outputPath = null)
        {
            if (skill == null) return null;

            var existing = CharacterPrefabSkillSync.ResolveCasterPrefab(skill.skillID);
            if (existing != null) return existing;

            if (string.IsNullOrEmpty(outputPath))
            {
                string folder = ResolveCasterFolderPath(skill);
                EnsureFolderExists(folder);
                outputPath = $"{folder}/{skill.skillID}.prefab";
            }
            else
            {
                string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder)) EnsureFolderExists(folder);
            }

            var root = new GameObject(skill.skillID);
            try
            {
                var statsManager = root.AddComponent<SkillStatsManager>();
                var statsSO = new SerializedObject(statsManager);
                statsSO.FindProperty("baseData")?.SetValue(skill);
                statsSO.FindProperty("damage")?.SetValue(skill.baseDamage);
                statsSO.FindProperty("manaCost")?.SetValue(skill.baseManaCost);
                statsSO.FindProperty("baseCooldown")?.SetValue(1f);
                statsSO.FindProperty("cooldown")?.SetValue(1f);
                statsSO.FindProperty("projectileSpeed")?.SetValue(skill.projectileSpeed);
                statsSO.FindProperty("projectileLifetime")?.SetValue(skill.projectileLifetime > 0 ? skill.projectileLifetime : 3f);
                statsSO.FindProperty("areaRadius")?.SetValue(skill.areaRadius);
                statsSO.FindProperty("beamTickInterval")?.SetValue(skill.beamTickInterval);
                statsSO.FindProperty("chainDamageMultiplier")?.SetValue(skill.chainDamageMultiplier);
                statsSO.FindProperty("maxChainTargets")?.SetValue((float)skill.maxChainTargets);
                statsSO.ApplyModifiedPropertiesWithoutUndo();

                var caster = root.AddComponent<SkillCaster>();
                var casterSO = new SerializedObject(caster);
                casterSO.FindProperty("stats")?.SetValue(statsManager);
                
                // For ally heals or targeting skills
                bool isHealOrAlly = skill.targetFilter == SkillData.SkillTargetFilter.AlliesOnly ||
                                   skill.targetFilter == SkillData.SkillTargetFilter.SelfOnly;
                casterSO.FindProperty("requireTarget")?.SetValue(isHealOrAlly);
                casterSO.FindProperty("spawnType")?.SetValue(isHealOrAlly ? 2 : 0); // 2 = AtTarget, 0 = AtCaster
                casterSO.FindProperty("forwardSpawnOffset")?.SetValue(0.6f);
                casterSO.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                Debug.Log($"[SkillEffectPrefabGenerator] Generated skill caster prefab '{outputPath}' for skill '{skill.skillID}'.", prefab);
                CharacterPrefabSkillSync.ClearCasterPrefabCache();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetValue(this SerializedProperty prop, UnityEngine.Object obj)
        {
            if (prop != null) prop.objectReferenceValue = obj;
        }

        private static void SetValue(this SerializedProperty prop, float val)
        {
            if (prop != null) prop.floatValue = val;
        }

        private static void SetValue(this SerializedProperty prop, bool val)
        {
            if (prop != null) prop.boolValue = val;
        }

        private static void SetValue(this SerializedProperty prop, int val)
        {
            if (prop != null) prop.intValue = val;
        }

        /// <summary>
        /// Validates a skill effect prefab against sanity rules (missing collider, missing required component, no layer set).
        /// </summary>
        public static bool ValidatePrefab(GameObject prefab, SkillData skill, out List<string> errors)
        {
            errors = new List<string>();
            if (prefab == null)
            {
                errors.Add("Prefab is null.");
                return false;
            }

            int fxLayer = LayerMask.NameToLayer(SkillFXLayerName);
            if (fxLayer >= 0 && prefab.layer != fxLayer)
            {
                errors.Add($"Root layer is '{LayerMask.LayerToName(prefab.layer)}' ({prefab.layer}); expected '{SkillFXLayerName}' ({fxLayer}).");
            }

            if (prefab.GetComponent<SkillHitResolver>() == null)
            {
                errors.Add("Missing required SkillHitResolver component on root.");
            }

            if (prefab.GetComponent<SkillInstance>() == null)
            {
                errors.Add("Missing required SkillInstance component on root.");
            }

            var colliders = prefab.GetComponentsInChildren<Collider2D>(true);
            if (colliders.Length == 0)
            {
                errors.Add("No Collider2D found in prefab hierarchy.");
            }
            else
            {
                bool hasTrigger = false;
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i].isTrigger) hasTrigger = true;
                }
                if (!hasTrigger)
                {
                    errors.Add("No 2D trigger collider found in prefab hierarchy.");
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Checks whether an existing prefab has been customized or modified from what template generation would produce.
        /// </summary>
        public static bool HasCustomModifications(GameObject existingPrefab, SkillData skill)
        {
            if (existingPrefab == null) return false;

            // Check if SpriteRenderer has a custom sprite (not Knob or null)
            var sr = existingPrefab.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null && sr.sprite.name != "Knob")
            {
                return true;
            }

            // Check if Animator with runtime controller is present
            var anim = existingPrefab.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                return true;
            }

            // Check for particle systems
            if (existingPrefab.GetComponentInChildren<ParticleSystem>(true) != null)
            {
                return true;
            }

            // Check for additional custom mono scripts
            var monoComponents = existingPrefab.GetComponents<MonoBehaviour>();
            foreach (var m in monoComponents)
            {
                if (m == null) continue;
                var t = m.GetType();
                if (t != typeof(SkillHitResolver) &&
                    t != typeof(SkillInstance) &&
                    t != typeof(LinearProjectileMovement) &&
                    t != typeof(AoEDetonation) &&
                    t != typeof(BeamMovement) &&
                    t != typeof(BeamSkill) &&
                    t != typeof(SkillTargeting))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            string leaf = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
