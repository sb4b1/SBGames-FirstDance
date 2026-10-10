using System.Collections.Generic;
using System.Text;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using Micosmo.SensorToolkit;
using SBGames.Personality;
using SBGames.Personality.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor
{
    /// <summary>
    /// Validation and Setup Utility for Normal / Minion Monster prefabs in SBGames.
    /// Ensures compliance with CrashKonijn GOAP MinionAgent architecture, Utility AI goal set,
    /// SensorToolkit perception, Squad hierarchy, and Personality models.
    /// </summary>
    public static class MinionMonsterPrefabValidator
    {
        public const string MINION_AGENT_KEY = "MinionAgent";
        public const float RECOMMENDED_DECISION_INTERVAL_MIN = 0.30f;
        public const float RECOMMENDED_DECISION_INTERVAL_MAX = 0.50f;

        public struct ValidationResult
        {
            public bool IsValid;
            public List<string> Errors;
            public List<string> Warnings;

            public string FormatReport()
            {
                var sb = new StringBuilder();
                sb.AppendLine(IsValid ? "[PASS] Minion Prefab Validation Passed" : "[FAIL] Minion Prefab Validation Failed");
                if (Errors != null && Errors.Count > 0)
                {
                    sb.AppendLine("\nErrors:");
                    foreach (var err in Errors) sb.AppendLine($" - [ERROR] {err}");
                }
                if (Warnings != null && Warnings.Count > 0)
                {
                    sb.AppendLine("\nWarnings:");
                    foreach (var warn in Warnings) sb.AppendLine($" - [WARN] {warn}");
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Validates a GameObject hierarchy against Minion monster architectural requirements.
        /// </summary>
        public static ValidationResult Validate(GameObject root)
        {
            var res = new ValidationResult
            {
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            if (root == null)
            {
                res.Errors.Add("Target GameObject is null.");
                res.IsValid = false;
                return res;
            }

            // 1. Root Components Check
            var brain = root.GetComponent<CharacterBrain>();
            if (brain == null) res.Errors.Add("Root is missing 'CharacterBrain'.");

            var stats = root.GetComponent<CharacterStatsManager>();
            if (stats == null)
            {
                res.Errors.Add("Root is missing 'CharacterStatsManager'.");
            }
            else
            {
                if (stats.BaseStats == null)
                    res.Warnings.Add("CharacterStatsManager has no 'BaseStats' SO assigned.");
            }

            var combat = root.GetComponent<CharacterCombatController>();
            if (combat == null) res.Errors.Add("Root is missing 'CharacterCombatController'.");

            var faction = root.GetComponent<FactionMember>();
            if (faction == null)
            {
                res.Errors.Add("Root is missing 'FactionMember'.");
            }
            else if (faction.Faction != FactionId.Monster)
            {
                res.Warnings.Add($"FactionMember faction is '{faction.Faction}', expected 'FactionId.Monster'.");
            }

            var squadMember = root.GetComponent<SquadMember>();
            if (squadMember == null) res.Errors.Add("Root is missing 'SquadMember' for squad hierarchy.");

            var personality = root.GetComponent<CharacterPersonality>();
            if (personality == null)
            {
                res.Warnings.Add("Root is missing 'CharacterPersonality' (Race/Trait psychology model).");
            }

            var vision = root.GetComponentInChildren<VisionController>();
            if (vision == null)
            {
                res.Errors.Add("Hierarchy is missing 'VisionController' (Micosmo SensorToolkit wrapper).");
            }

            // 2. AI_Augment Child Check
            Transform augmentChild = null;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "AI_Augment" || child.name == "NormalAI" || child.CompareTag("AI_Augment"))
                {
                    augmentChild = child;
                    break;
                }
            }

            if (augmentChild == null)
            {
                // Fallback check on children with BaseAI
                var foundBaseAI = root.GetComponentInChildren<BaseAI>();
                if (foundBaseAI != null) augmentChild = foundBaseAI.transform;
            }

            if (augmentChild == null)
            {
                res.Errors.Add("Missing child GameObject designated for AI ('AI_Augment' with tag 'AI_Augment').");
            }
            else
            {
                var baseAI = augmentChild.GetComponent<BaseAI>();
                if (baseAI == null)
                {
                    res.Errors.Add("AI child is missing 'BaseAI'.");
                }
                else
                {
                    // Check agentType
                    var agentTypeField = typeof(BaseAI).GetField("agentType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    string agentType = agentTypeField?.GetValue(baseAI) as string;
                    if (agentType != MINION_AGENT_KEY)
                    {
                        res.Warnings.Add($"BaseAI.agentType is '{agentType}', recommended: '{MINION_AGENT_KEY}'.");
                    }

                    // Check skills list is empty
                    if (baseAI.Skills != null && baseAI.Skills.Count > 0)
                    {
                        res.Warnings.Add("BaseAI.skills has active skills assigned. Minions should perform only basic attacks.");
                    }

                    // Check goals list
                    var goalsField = typeof(BaseAI).GetField("goals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var goals = goalsField?.GetValue(baseAI) as List<UtilityGoal>;
                    if (goals == null || goals.Count == 0)
                    {
                        res.Errors.Add("BaseAI.goals list is empty. Expected 6 Minion UtilityGoals.");
                    }
                    else
                    {
                        bool hasFallBack = false;
                        bool hasFocusFire = false;
                        bool hasFollowLeader = false;
                        bool hasKillEnemy = false;
                        bool hasSurvive = false;
                        bool hasWander = false;

                        foreach (var g in goals)
                        {
                            if (g is UtilityFallBackGoal) hasFallBack = true;
                            else if (g is UtilityFocusFireGoal) hasFocusFire = true;
                            else if (g is UtilityFollowLeaderGoal) hasFollowLeader = true;
                            else if (g is UtilityKillEnemyGoal) hasKillEnemy = true;
                            else if (g is UtilitySurviveGoal) hasSurvive = true;
                            else if (g is UtilityWanderGoal) hasWander = true;
                        }

                        if (!hasFallBack) res.Warnings.Add("BaseAI.goals missing UtilityFallBackGoal.");
                        if (!hasFocusFire) res.Warnings.Add("BaseAI.goals missing UtilityFocusFireGoal.");
                        if (!hasFollowLeader) res.Warnings.Add("BaseAI.goals missing UtilityFollowLeaderGoal.");
                        if (!hasKillEnemy) res.Warnings.Add("BaseAI.goals missing UtilityKillEnemyGoal.");
                        if (!hasSurvive) res.Warnings.Add("BaseAI.goals missing UtilitySurviveGoal.");
                        if (!hasWander) res.Warnings.Add("BaseAI.goals missing UtilityWanderGoal.");
                    }
                }

                var provider = augmentChild.GetComponent<GoapActionProvider>();
                if (provider == null) res.Errors.Add("AI child is missing 'GoapActionProvider'.");

                var throttler = augmentChild.GetComponent<AIDecisionThrottler>();
                if (throttler == null)
                {
                    res.Warnings.Add("AI child is missing 'AIDecisionThrottler' (will be auto-added at runtime if missing, but should be serialized).");
                }
                else
                {
                    float interval = throttler.DecisionInterval;
                    if (interval < RECOMMENDED_DECISION_INTERVAL_MIN || interval > RECOMMENDED_DECISION_INTERVAL_MAX)
                    {
                        res.Warnings.Add($"AIDecisionThrottler interval is {interval:F2}s. Recommended for minions: 0.35s - 0.45s with jitter.");
                    }
                }

                var moveBehaviour = augmentChild.GetComponent<AgentMoveBehaviour>();
                if (moveBehaviour == null) res.Errors.Add("AI child is missing 'AgentMoveBehaviour'.");
            }

            res.IsValid = res.Errors.Count == 0;
            return res;
        }

        /// <summary>
        /// Automatically configures or upgrades a GameObject into a compliant Minion Monster.
        /// </summary>
        public static void ConfigureAsMinion(GameObject root)
        {
            if (root == null) return;
            Undo.RegisterFullObjectHierarchyUndo(root, "Configure As Minion Monster");

            // 1. Root components
            var brain = root.GetComponent<CharacterBrain>() ?? root.AddComponent<CharacterBrain>();
            var stats = root.GetComponent<CharacterStatsManager>() ?? root.AddComponent<CharacterStatsManager>();
            var combat = root.GetComponent<CharacterCombatController>() ?? root.AddComponent<CharacterCombatController>();
            var faction = root.GetComponent<FactionMember>() ?? root.AddComponent<FactionMember>();
            faction.Faction = FactionId.Monster;

            var squadMember = root.GetComponent<SquadMember>() ?? root.AddComponent<SquadMember>();
            var personality = root.GetComponent<CharacterPersonality>() ?? root.AddComponent<CharacterPersonality>();

            // 2. Child AI_Augment
            Transform augmentChild = null;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "AI_Augment" || child.name == "NormalAI")
                {
                    augmentChild = child;
                    break;
                }
            }

            if (augmentChild == null)
            {
                var newGo = new GameObject("AI_Augment");
                newGo.transform.SetParent(root.transform, false);
                augmentChild = newGo.transform;
            }

            augmentChild.tag = "AI_Augment";

            var baseAI = augmentChild.GetComponent<BaseAI>() ?? augmentChild.gameObject.AddComponent<BaseAI>();
            var provider = augmentChild.GetComponent<GoapActionProvider>() ?? augmentChild.gameObject.AddComponent<GoapActionProvider>();
            var throttler = augmentChild.GetComponent<AIDecisionThrottler>() ?? augmentChild.gameObject.AddComponent<AIDecisionThrottler>();
            var moveBehaviour = augmentChild.GetComponent<AgentMoveBehaviour>() ?? augmentChild.gameObject.AddComponent<AgentMoveBehaviour>();

            // Set AgentType to MinionAgent
            var agentTypeField = typeof(BaseAI).GetField("agentType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            agentTypeField?.SetValue(baseAI, MINION_AGENT_KEY);

            // Configure Throttler
            var intervalField = typeof(AIDecisionThrottler).GetField("defaultDecisionInterval", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            intervalField?.SetValue(throttler, 0.40f);
            var jitterField = typeof(AIDecisionThrottler).GetField("defaultIntervalJitter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            jitterField?.SetValue(throttler, 0.05f);

            // Populate Minion Goals
            var goalsField = typeof(BaseAI).GetField("goals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var goalList = new List<UtilityGoal>();

            // Load from AssetDatabase
            AddGoalAsset<UtilityFallBackGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/DefaultFallBackGoal.asset");
            AddGoalAsset<UtilityFocusFireGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/DefaultFocusFireGoal.asset");
            AddGoalAsset<UtilityFollowLeaderGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/DefaultFollowLeaderGoal.asset");
            AddGoalAsset<UtilityKillEnemyGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/DefaultKillEnemyGoal.asset");
            AddGoalAsset<UtilitySurviveGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/MinionSurviveGoal.asset");
            AddGoalAsset<UtilityWanderGoal>(goalList, "Assets/ScriptableObjects/UtilityGoal/Default/DefaultWanderGoal.asset");

            goalsField?.SetValue(baseAI, goalList);

            // Ensure skills list is empty
            var skillsField = typeof(BaseAI).GetField("skills", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            skillsField?.SetValue(baseAI, new List<UtilitySkill>());

            // Wire Brain AI augment reference
            var aiAugmentField = typeof(CharacterBrain).GetField("aiAugment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            aiAugmentField?.SetValue(brain, augmentChild.gameObject);

            EditorUtility.SetDirty(root);
            if (augmentChild != null) EditorUtility.SetDirty(augmentChild.gameObject);
        }

        private static void AddGoalAsset<T>(List<UtilityGoal> list, string path) where T : UtilityGoal
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                list.Add(asset);
            }
            else
            {
                // Fallback: search by type in AssetDatabase
                string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
                if (guids.Length > 0)
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var found = AssetDatabase.LoadAssetAtPath<T>(foundPath);
                    if (found != null) list.Add(found);
                }
            }
        }

        [MenuItem("SBGames/AI/Validate Selected as Minion Prefab")]
        public static void ValidateSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Minion Validator", "Please select a GameObject in the Project or Hierarchy.", "OK");
                return;
            }

            var report = Validate(selected);
            Debug.Log(report.FormatReport(), selected);
            EditorUtility.DisplayDialog("Minion Validation Result", report.FormatReport(), "OK");
        }

        [MenuItem("SBGames/AI/Configure Selected as Minion Monster")]
        public static void ConfigureSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Minion Setup", "Please select a GameObject in the Project or Hierarchy.", "OK");
                return;
            }

            ConfigureAsMinion(selected);
            var report = Validate(selected);
            Debug.Log($"[Minion Setup Applied]\n{report.FormatReport()}", selected);
            EditorUtility.DisplayDialog("Minion Setup Completed", report.FormatReport(), "OK");
        }

        /// <summary>
        /// Registers MinionAgent in the active scene's GoapBehaviour if not already registered.
        /// </summary>
        [MenuItem("SBGames/AI/Register MinionAgent in Active Scene")]
        public static void RegisterMinionAgentInActiveScene()
        {
            var goap = Object.FindAnyObjectByType<GoapBehaviour>(FindObjectsInactive.Include);
            if (goap == null)
            {
                EditorUtility.DisplayDialog("Register MinionAgent", "No GoapBehaviour found in the active scene.", "OK");
                return;
            }

            // Check if MinionAgent child already exists
            MinionAgentTypeFactory minionFactory = null;
            for (int i = 0; i < goap.transform.childCount; i++)
            {
                var child = goap.transform.GetChild(i);
                var f = child.GetComponent<MinionAgentTypeFactory>();
                if (f != null)
                {
                    minionFactory = f;
                    break;
                }
            }

            if (minionFactory == null)
            {
                var factoryGo = new GameObject("MinionAgent");
                factoryGo.transform.SetParent(goap.transform, false);
                minionFactory = factoryGo.AddComponent<MinionAgentTypeFactory>();
                Undo.RegisterCreatedObjectUndo(factoryGo, "Create MinionAgent in Scene");
            }

            // Ensure factory is in agentTypeConfigFactories list
            var factoriesField = typeof(GoapBehaviour).GetField("agentTypeConfigFactories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (factoriesField != null)
            {
                var list = factoriesField.GetValue(goap) as List<AgentTypeFactoryBase>;
                if (list != null && !list.Contains(minionFactory))
                {
                    list.Add(minionFactory);
                    EditorUtility.SetDirty(goap);
                }
            }

            Debug.Log("[GoapBehaviour] MinionAgent registered successfully in active scene.", goap);
            EditorUtility.DisplayDialog("Register MinionAgent", "MinionAgent successfully registered under GoapBehaviour in active scene.", "OK");
        }
    }
}
