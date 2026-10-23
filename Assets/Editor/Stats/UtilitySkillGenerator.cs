using System;
using System.IO;
using SBGames.Database;
using SBGames.Database.Editor;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Stats
{
    public enum UtilitySkillTemplate
    {
        None = 0,
        Damage,
        Heal
    }

    /// <summary>
    /// Utility and generator for per-skill UtilitySkill ScriptableObjects in Stats & Balance.
    /// Manages template resolution, creation, folder conventions, and SkillData linkage.
    /// Editor-only: builds/updates assets; never mutates ScriptableObject templates at runtime.
    /// </summary>
    public static class UtilitySkillGenerator
    {
        public const string UtilitySkillRootFolder = "Assets/ScriptableObjects/UtilitySkill";
        public const string DamageTemplatePath = "Assets/ScriptableObjects/UtilitySkill/Fire/Utility_SKILL_F002.asset";
        public const string HealTemplatePath = "Assets/ScriptableObjects/UtilitySkill/Default/Utility_SKILL_HEAL_001.asset";

        /// <summary>
        /// Determines the appropriate UtilitySkill template archetype for a SkillData.
        /// AlliesOnly skills map to Heal; damage-dealing skills map to Damage.
        /// </summary>
        public static UtilitySkillTemplate DetermineTemplate(SkillData skill)
        {
            if (skill == null) return UtilitySkillTemplate.None;

            if (skill.targetFilter == SkillData.SkillTargetFilter.AlliesOnly)
            {
                return UtilitySkillTemplate.Heal;
            }

            if (skill.baseDamage > 0f || skill.targetFilter == SkillData.SkillTargetFilter.EnemiesOnly)
            {
                return UtilitySkillTemplate.Damage;
            }

            return UtilitySkillTemplate.None;
        }

        /// <summary>
        /// Derives the canonical subfolder name for a skill, following the existing structure:
        /// Fire, Water, Lightning, Wind, Default.
        /// </summary>
        public static string DetermineCategory(SkillData skill)
        {
            if (skill == null) return "Default";
            string id = skill.skillID ?? "";

            if (id.StartsWith("SKILL_F", StringComparison.OrdinalIgnoreCase)) return "Fire";
            if (id.StartsWith("SKILL_WA", StringComparison.OrdinalIgnoreCase)) return "Water";
            if (id.StartsWith("SKILL_L", StringComparison.OrdinalIgnoreCase)) return "Lightning";
            if (id.StartsWith("SKILL_WI", StringComparison.OrdinalIgnoreCase)) return "Wind";

            string name = skill.skillName ?? "";
            if (name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0) return "Fire";
            if (name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0) return "Water";
            if (name.IndexOf("lightning", StringComparison.OrdinalIgnoreCase) >= 0) return "Lightning";
            if (name.IndexOf("wind", StringComparison.OrdinalIgnoreCase) >= 0) return "Wind";

            return "Default";
        }

        /// <summary>
        /// Canonical asset path for a skill's Utility asset:
        /// Assets/ScriptableObjects/UtilitySkill/{Category}/Utility_{SkillID}.asset
        /// </summary>
        public static string ResolveUtilityAssetPath(SkillData skill)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.skillID)) return null;
            string category = DetermineCategory(skill);
            return $"{UtilitySkillRootFolder}/{category}/Utility_{skill.skillID}.asset";
        }

        /// <summary>
        /// Resolves the existing UtilitySkill asset for a skill ID, or returns null if not found.
        /// Priority: SkillData.utilityData -> file at canonical path -> UtilitySkillDatabaseSO lookup.
        /// </summary>
        public static UtilitySkill ResolveUtilityAsset(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return null;

            var skillDb = DatabaseAutoIndexer.GetOrCreateCatalog<SkillDatabaseSO>("SkillDatabase");
            if (skillDb != null && skillDb.TryGet(skillId, out var skill) && skill != null)
            {
                if (skill.utilityData != null) return skill.utilityData;

                string path = ResolveUtilityAssetPath(skill);
                if (!string.IsNullOrEmpty(path))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<UtilitySkill>(path);
                    if (existing != null) return existing;
                }
            }

            // Fallback: search by ID in project
            string[] guids = AssetDatabase.FindAssets($"t:UtilitySkill Utility_{skillId}");
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UtilitySkill>(p);
                if (asset != null && string.Equals(asset.GetID(), skillId, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether the given agent type (e.g. NormalAgent, MageAgent, MinionAgent) can plan/cast the skill.
        /// MageAgent does not register HealCapabilityFactory (cannot plan heal skills).
        /// MinionAgent does not register SkillCapabilityFactory or HealCapabilityFactory (cannot plan active skills).
        /// Returns false and provides a reason if incompatible.
        /// </summary>
        public static bool CanAgentPlanSkill(string agentType, SkillData skill, out string reason)
        {
            reason = null;
            if (skill == null) return true;

            string agent = string.IsNullOrWhiteSpace(agentType) ? "NormalAgent" : agentType.Trim();

            // Check MinionAgent (no active skills supported)
            if (string.Equals(agent, "MinionAgent", StringComparison.OrdinalIgnoreCase))
            {
                reason = "MinionAgent does not register SkillCapabilityFactory or HealCapabilityFactory (cannot plan active skills).";
                return false;
            }

            // Check MageAgent (only damage/offensive skills, no HealCapabilityFactory)
            if (string.Equals(agent, "MageAgent", StringComparison.OrdinalIgnoreCase))
            {
                if (skill.targetFilter == SkillData.SkillTargetFilter.AlliesOnly)
                {
                    reason = "MageAgent does not register HealCapabilityFactory (heal skills cannot be planned).";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ensures a UtilitySkill asset exists for the given SkillData.
        /// If an asset already exists at the canonical path or on the skill, returns it without overwriting.
        /// Otherwise, clones from the template matching the skill type, configures its values, saves it,
        /// assigns it to SkillData.utilityData, and optionally registers it in UtilitySkillDatabaseSO.
        /// </summary>
        public static UtilitySkill EnsureUtilityAssetExists(SkillData skill, out bool createdNew, bool autoRegisterInDb = true)
        {
            createdNew = false;
            if (skill == null || string.IsNullOrWhiteSpace(skill.skillID)) return null;

            // 1. Check if already assigned on SkillData
            if (skill.utilityData != null)
            {
                return skill.utilityData;
            }

            // 2. Check canonical path
            string targetPath = ResolveUtilityAssetPath(skill);
            var existing = AssetDatabase.LoadAssetAtPath<UtilitySkill>(targetPath);
            if (existing != null)
            {
                skill.utilityData = existing;
                EditorUtility.SetDirty(skill);
                return existing;
            }

            // 3. Select template by skill archetype
            var templateType = DetermineTemplate(skill);
            string templatePath = templateType switch
            {
                UtilitySkillTemplate.Heal => HealTemplatePath,
                UtilitySkillTemplate.Damage => DamageTemplatePath,
                _ => null
            };

            UtilitySkill templateAsset = null;
            if (!string.IsNullOrEmpty(templatePath))
            {
                templateAsset = AssetDatabase.LoadAssetAtPath<UtilitySkill>(templatePath);
            }

            // Fallback: any existing UtilitySkill if template asset is missing
            if (templateAsset == null && templateType == UtilitySkillTemplate.Damage)
            {
                templateAsset = AssetDatabase.LoadAssetAtPath<UtilitySkill>("Assets/ScriptableObjects/UtilitySkill/Default/Utility_SKILL_001.asset");
            }

            // Ensure directory exists
            string dir = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            UtilitySkill newAsset;
            if (templateAsset != null)
            {
                newAsset = UnityEngine.Object.Instantiate(templateAsset);
            }
            else
            {
                newAsset = ScriptableObject.CreateInstance<UtilitySkill>();
            }

            newAsset.name = $"Utility_{skill.skillID}";
            newAsset.SetSkillID(skill.skillID);

            // Configure properties according to skill configuration
            var so = new SerializedObject(newAsset);
            var idProp = so.FindProperty("skillID");
            if (idProp != null) idProp.stringValue = skill.skillID;

            var manaProp = so.FindProperty("minManaCost");
            if (manaProp != null) manaProp.floatValue = skill.baseManaCost;

            if (skill.castRange > 0f)
            {
                var rangeProp = so.FindProperty("maxEvaluationRange");
                if (rangeProp != null) rangeProp.floatValue = skill.castRange;
            }

            if (skill.area || skill.areaRadius > 0f)
            {
                var clusterProp = so.FindProperty("useClusterConsideration");
                if (clusterProp != null) clusterProp.boolValue = true;

                var radiusProp = so.FindProperty("clusterRadius");
                if (radiusProp != null && skill.areaRadius > 0f) radiusProp.floatValue = skill.areaRadius;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(newAsset, targetPath);
            EditorUtility.SetDirty(newAsset);

            skill.utilityData = newAsset;
            EditorUtility.SetDirty(skill);

            if (autoRegisterInDb)
            {
                RegisterInUtilityDatabase(skill.skillID, newAsset);
            }

            createdNew = true;
            return newAsset;
        }

        private static void RegisterInUtilityDatabase(string skillId, UtilitySkill utilityAsset)
        {
            var db = AssetDatabase.LoadAssetAtPath<UtilitySkillDatabaseSO>("Assets/ScriptableObjects/Database/UtilitySkillDatabase.asset");
            if (db == null) return;

            if (db.utilSkills == null)
            {
                db.utilSkills = new System.Collections.Generic.List<UtilitySkillEntry>();
            }

            var entry = db.utilSkills.Find(e => string.Equals(e.id, skillId, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                entry.so = utilityAsset;
            }
            else
            {
                db.utilSkills.Add(new UtilitySkillEntry { id = skillId, so = utilityAsset });
            }
            EditorUtility.SetDirty(db);
        }
    }
}
