using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using Micosmo.SensorToolkit;
using SBGames.Cameras;
using SBGames.Characters.AI;
using SBGames.Database;
using SBGames.Dungeon.Core.Pieces;
using SBGames.Dungeon.Unity;
using SBGames.Dungeon.Unity.Pieces;
using SBGames.Inventory.Data;
using SBGames.Loop;
using SBGames.Placement;
using SBGames.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SBGames.Editor.Validation
{
    public enum ValidationSeverity
    {
        Error = 0,
        Warning = 1,
        Info = 2
    }

    [Serializable]
    public struct ValidationBadge
    {
        public ValidationSeverity Severity;
        public string Category;
        public string TargetName;
        public string AssetPath;
        public string Message;
        public string SuggestedFix;

        public ValidationBadge(ValidationSeverity severity, string category, string targetName, string assetPath, string message, string suggestedFix)
        {
            Severity = severity;
            Category = category;
            TargetName = targetName;
            AssetPath = assetPath;
            Message = message;
            SuggestedFix = suggestedFix;
        }

        public override string ToString() => $"[{Severity}] ({Category}) {TargetName}: {Message}";
    }

    public class AISetupValidatorWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private List<ValidationBadge> _findings = new();
        private bool _filterErrors = true;
        private bool _filterWarnings = true;
        private bool _filterInfo = true;
        private string _searchFilter = "";

        [MenuItem("SBGames/AI Setup Validator")]
        public static void Open()
        {
            var window = GetWindow<AISetupValidatorWindow>("AI Setup Validator");
            window.minSize = new Vector2(750, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Run Full Audit", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                _findings = AISetupValidatorCore.RunAudit();
            }
            if (GUILayout.Button("Export Markdown Report", EditorStyles.toolbarButton, GUILayout.Width(160)))
            {
                if (_findings.Count == 0) _findings = AISetupValidatorCore.RunAudit();
                string path = AISetupValidatorCore.ExportReportToMarkdown(_findings);
                EditorUtility.DisplayDialog("Report Exported", $"Audit report saved to:\n{path}", "OK");
            }
            GUILayout.FlexibleSpace();
            _filterErrors = GUILayout.Toggle(_filterErrors, $"Errors ({_findings.Count(f => f.Severity == ValidationSeverity.Error)})", EditorStyles.toolbarButton);
            _filterWarnings = GUILayout.Toggle(_filterWarnings, $"Warnings ({_findings.Count(f => f.Severity == ValidationSeverity.Warning)})", EditorStyles.toolbarButton);
            _filterInfo = GUILayout.Toggle(_filterInfo, $"Info ({_findings.Count(f => f.Severity == ValidationSeverity.Info)})", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var filtered = _findings.Where(f =>
            {
                if (f.Severity == ValidationSeverity.Error && !_filterErrors) return false;
                if (f.Severity == ValidationSeverity.Warning && !_filterWarnings) return false;
                if (f.Severity == ValidationSeverity.Info && !_filterInfo) return false;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    string s = _searchFilter.ToLowerInvariant();
                    return (f.TargetName != null && f.TargetName.ToLowerInvariant().Contains(s)) ||
                           (f.Message != null && f.Message.ToLowerInvariant().Contains(s)) ||
                           (f.Category != null && f.Category.ToLowerInvariant().Contains(s));
                }
                return true;
            }).ToList();

            foreach (var finding in filtered)
            {
                Color boxColor = finding.Severity switch
                {
                    ValidationSeverity.Error => new Color(1f, 0.4f, 0.4f, 0.15f),
                    ValidationSeverity.Warning => new Color(1f, 0.85f, 0.3f, 0.15f),
                    _ => new Color(0.4f, 0.7f, 1f, 0.15f)
                };

                string badgePrefix = finding.Severity switch
                {
                    ValidationSeverity.Error => "❌ [ERROR]",
                    ValidationSeverity.Warning => "⚠ [WARNING]",
                    _ => "ℹ [INFO]"
                };

                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = boxColor;
                EditorGUILayout.LabelField($"{badgePrefix} [{finding.Category}] {finding.TargetName}", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField($"Asset: {finding.AssetPath}", EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(finding.Message, finding.Severity switch
                {
                    ValidationSeverity.Error => MessageType.Error,
                    ValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                });
                EditorGUILayout.LabelField($"Suggested Fix: {finding.SuggestedFix}", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    public static class AISetupValidatorCore
    {
        [MenuItem("SBGames/Run AI Setup Validator (Headless)")]
        public static void RunHeadless()
        {
            var findings = RunAudit();
            string path = ExportReportToMarkdown(findings);
            Debug.Log($"[AISetupValidator] Headless validation completed. Found {findings.Count} findings. Report written to {path}");
        }

        public static List<ValidationBadge> RunAudit()
        {
            var findings = new List<ValidationBadge>();

            AuditCharacterPrefabs(findings);
            AuditSkills(findings);
            AuditGoapAndUtilityAI(findings);
            AuditDataAndServices(findings);
            AuditScenes(findings);

            return findings.OrderBy(f => f.Severity).ToList();
        }

        // =====================================================================
        // SECTION A: CHARACTER PREFABS
        // =====================================================================
        private static void AuditCharacterPrefabs(List<ValidationBadge> findings)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g))
                             .Where(p => p.Contains("Prefab") && (p.Contains("Adventurer") || p.Contains("Monster")))
                             .Distinct()
                             .ToList();

            foreach (var path in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                string unitName = go.name;

                // 1. Root Components Check
                var brain = go.GetComponent<CharacterBrain>();
                if (brain == null)
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Root GameObject is missing 'CharacterBrain'.", "Add CharacterBrain component to root GameObject."));

                var stats = go.GetComponent<CharacterStatsManager>();
                if (stats == null)
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Root GameObject is missing 'CharacterStatsManager'.", "Add CharacterStatsManager component to root GameObject."));

                var combat = go.GetComponent<CharacterCombatController>();
                if (combat == null)
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Root GameObject is missing 'CharacterCombatController'.", "Add CharacterCombatController component to root GameObject."));

                var anim = go.GetComponent<AnimationObjectController>() ?? go.GetComponentInChildren<AnimationObjectController>(true);
                if (anim == null)
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Character is missing AnimationObjectController / AnimationController.", "Attach AnimationController on Model child."));

                var faction = go.GetComponent<FactionMember>();
                if (faction == null)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Root GameObject is missing 'FactionMember'.", "Add FactionMember component to root GameObject."));
                }
                else
                {
                    // Tag matches faction
                    string expectedTag = faction.Faction.ToString();
                    if (!string.Equals(go.tag, expectedTag, StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                            $"Root tag '{go.tag}' does not match FactionMember faction '{faction.Faction}'.",
                            $"Set root GameObject tag to '{expectedTag}'."));
                    }
                }

                // Check CapsuleCollider2D on root
                var capsuleCol = go.GetComponent<CapsuleCollider2D>();
                if (capsuleCol == null)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                        "Root GameObject is missing 'CapsuleCollider2D'. Root physics relies on nested Trigger/Collision BoxCollider2D instead.",
                        "Add CapsuleCollider2D to root or document intentional use of BoxCollider2D on child objects."));
                }

                // Layer check (Layer 6 = Character)
                if (go.layer != 6)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        $"Root layer is {go.layer} ({LayerMask.LayerToName(go.layer)}), expected layer 6 (Character).",
                        "Set root GameObject layer to 'Character' (6)."));
                }

                // 2. AI Augment Child Check
                var aiAug = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.CompareTag("AI_Augment"));
                if (aiAug == null)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                        "Missing child GameObject tagged 'AI_Augment'.", "Create child GameObject named 'AI_Augment' with tag 'AI_Augment'."));
                }
                else
                {
                    if (aiAug.name != "AI_Augment")
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Info, "Character Prefab", unitName, path,
                            $"AI Augment child has name '{aiAug.name}' instead of standardized 'AI_Augment'.",
                            "Rename child GameObject to 'AI_Augment' for consistency."));
                    }

                    var baseAI = aiAug.GetComponent<BaseAI>();
                    if (baseAI == null)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "AI_Augment child is missing 'BaseAI'.", "Attach BaseAI component to AI_Augment."));

                    var goapProv = aiAug.GetComponent<GoapActionProvider>();
                    if (goapProv == null)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "AI_Augment child is missing 'GoapActionProvider'.", "Attach GoapActionProvider component to AI_Augment."));

                    var agentBhv = aiAug.GetComponent<AgentBehaviour>();
                    if (agentBhv == null)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "AI_Augment child is missing 'AgentBehaviour'.", "Attach AgentBehaviour component to AI_Augment."));

                    var moveBhv = aiAug.GetComponent<AgentMoveBehaviour>();
                    if (moveBhv == null)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "AI_Augment child is missing 'AgentMoveBehaviour'.", "Attach AgentMoveBehaviour component to AI_Augment."));

                    var nav = go.GetComponent<NavMeshAgent>() ?? go.GetComponentInChildren<NavMeshAgent>(true);
                    if (nav == null)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "Character is missing 'NavMeshAgent'.", "Attach NavMeshAgent component."));

                    // VisionController and Sensors
                    var vision = go.GetComponentInChildren<VisionController>(true);
                    if (vision == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "Character is missing 'VisionController'.", "Attach VisionController under Sensor child."));
                    }
                    else
                    {
                        if (vision.Tuning == null)
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                                "VisionController has no PerceptionTuningSO assigned.", "Assign 'PerceptionTuning_Default.asset' to VisionController tuning."));
                        }

                        var sensors = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sensors" || t.name == "Sensor");
                        if (sensors == null)
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                "Missing 'Sensors' child GameObject carrying perception sensors.", "Create 'Sensors' child with RangeSensor2D and LOSSensor2D."));
                        }
                        else
                        {
                            var range = sensors.GetComponentInChildren<RangeSensor2D>(true);
                            var los = sensors.GetComponentInChildren<LOSSensor2D>(true);
                            if (range == null)
                                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                    "Sensors child is missing 'RangeSensor2D'.", "Add RangeSensor2D component to Sensors child."));
                            if (los == null)
                                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                    "Sensors child is missing 'LOSSensor2D'.", "Add LOSSensor2D component to Sensors child."));

                            if (vision.Sensor == null)
                            {
                                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                    "VisionController 'sensor' reference is unassigned.", "Wire RangeSensor2D or LOSSensor2D into VisionController sensor field."));
                            }
                        }
                    }

                    // Skill Controller
                    var skillCtrl = go.GetComponentInChildren<CharacterSkillController>(true);
                    if (skillCtrl == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "Character is missing 'CharacterSkillController'.", "Attach CharacterSkillController under Skills child."));
                    }

                    // Agent Type vs Archetype & Capability Factory
                    if (baseAI != null)
                    {
                        var aiSo = new SerializedObject(baseAI);
                        string agentTypeStr = aiSo.FindProperty("agentType")?.stringValue ?? "";
                        if (string.IsNullOrEmpty(agentTypeStr))
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                "BaseAI agentType string is empty.", "Set agentType string to 'NormalAgent', 'MageAgent', or 'MinionAgent'."));
                        }

                        // Check healers on agent types without HealCapabilityFactory
                        bool isHealer = baseAI.Goals != null && baseAI.Goals.Any(g => g != null && g.name.Contains("Heal"));
                        if (isHealer && agentTypeStr == "MageAgent")
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                $"Unit '{unitName}' has healing goals/skills but uses '{agentTypeStr}' which omits HealCapabilityFactory.",
                                "Switch agentType to 'NormalAgent' or register HealCapabilityFactory inside MageAgentTypeFactory.cs."));
                        }

                        // Combat goals check
                        bool hasCombatGoal = baseAI.Goals != null && baseAI.Goals.Any(g => g != null && (g.name.Contains("Kill") || g.name.Contains("Survive")));
                        if (!hasCombatGoal)
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                                "BaseAI has neither KillEnemyGoal nor SurviveGoal assigned.", "Assign DefaultKillEnemyGoal and DefaultSurviveGoal to BaseAI.goals."));
                        }
                    }
                }

                // 3. Stats & Regen Tuning
                if (stats != null)
                {
                    if (stats.BaseStats == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "CharacterStatsManager has no 'BaseStats' ScriptableObject assigned.", "Assign corresponding CharacterBaseStatSO asset to BaseStats field."));
                    }
                    else
                    {
                        var bs = stats.BaseStats;
                        if (bs.MaxLife <= 0)
                            findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                $"BaseStats MaxLife is {bs.MaxLife} (must be > 0).", "Configure MaxLife in CharacterBaseStatSO and Characters.csv."));
                        if (bs.Movespeed <= 0)
                            findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                                $"BaseStats Movespeed is {bs.Movespeed} (must be > 0).", "Configure Movespeed in CharacterBaseStatSO and Characters.csv."));

                        // BasicAttackMode vs Role
                        if (bs.basicAttackMode == BasicAttackMode.None && bs.Damage > 0)
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Info, "Character Prefab", unitName, path,
                                "BasicAttackMode is None (mage/healer mode). Unit cannot perform standard basic attacks and relies entirely on GOAP skills.",
                                "Verify unit has usable offensive or support skills assigned."));
                        }
                    }

                    if (stats.RegenTuning == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                            "CharacterStatsManager has no 'RegenTuning' SO assigned.", "Assign 'RegenTuning_Default.asset' to CharacterStatsManager."));
                    }
                }

                // 4. Adventurer-Specific Wire-ups
                if (path.Contains("Adventurer"))
                {
                    var sm = go.GetComponentInChildren<SquadMember>(true);
                    var pl = go.GetComponentInChildren<PartyLeader>(true);
                    if (sm == null && pl == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "Adventurer is missing SquadMember or PartyLeader component.", "Attach SquadMember component to adventurer root."));
                    }

                    var itemKit = go.GetComponentInChildren<AdventurerItemKitController>(true);
                    if (itemKit == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Character Prefab", unitName, path,
                            "Adventurer is missing 'AdventurerItemKitController'.", "Attach AdventurerItemKitController component to adventurer root."));
                    }

                    var doorway = go.GetComponentInChildren<DoorwayStagingController>(true);
                    if (doorway == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                            "Adventurer is missing 'DoorwayStagingController'.", "Attach DoorwayStagingController component to AI_Augment child."));
                    }

                    var memory = go.GetComponentInChildren<CharacterAIMemory>(true);
                    if (memory == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                            "Adventurer is missing 'CharacterAIMemory'.", "Attach CharacterAIMemory component to AI_Augment child."));
                    }

                    var throttler = go.GetComponentInChildren<AIDecisionThrottler>(true);
                    if (throttler == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                            "Adventurer is missing 'AIDecisionThrottler'.", "Attach AIDecisionThrottler component to AI_Augment child."));
                    }

                    if (stats != null && stats.BaseStats != null)
                    {
                        if (stats.BaseStats.CharacterFunction == AdventurerFunction.None)
                        {
                            findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Character Prefab", unitName, path,
                                "Adventurer has CharacterFunction set to 'None' in CharacterBaseStatSO.", "Assign role (Tank, DPS, Healer, or Scout) in CharacterBaseStatSO."));
                        }
                    }
                }
            }
        }

        // =====================================================================
        // SECTION B: SKILLS & EFFECTS
        // =====================================================================
        private static void AuditSkills(List<ValidationBadge> findings)
        {
            var skillGuids = AssetDatabase.FindAssets("t:SkillData");
            var skillPaths = skillGuids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToList();
            var utilDb = AssetDatabase.LoadAssetAtPath<UtilitySkillDatabaseSO>("Assets/ScriptableObjects/Database/UtilitySkillDatabase.asset");

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in skillPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data == null) continue;

                string id = data.skillID;
                if (string.IsNullOrWhiteSpace(id))
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Data", data.name, path,
                        "SkillData has an empty 'skillID'.", "Assign a unique skill ID (e.g. SKILL_xxx)."));
                    continue;
                }

                if (seenIds.Contains(id))
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Data", id, path,
                        $"Duplicate SkillID '{id}' found.", "Ensure every SkillData has a globally unique skillID."));
                }
                seenIds.Add(id);

                // Type checks
                if (data.type == SkillType.Projectile)
                {
                    if (data.projectileSpeed <= 0f)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Data", id, path,
                            $"Projectile skill '{id}' has non-positive projectileSpeed ({data.projectileSpeed}).", "Set projectileSpeed > 0."));
                    if (data.projectileLifetime <= 0f)
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Data", id, path,
                            $"Projectile skill '{id}' has non-positive projectileLifetime ({data.projectileLifetime}).", "Set projectileLifetime > 0."));
                }

                if (data.area && data.areaRadius <= 0f)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Skill Data", id, path,
                        $"Area skill '{id}' has area enabled but areaRadius is {data.areaRadius}.", "Set areaRadius > 0."));
                }

                if (data.chain && data.maxChainTargets <= 0)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Skill Data", id, path,
                        $"Chain skill '{id}' has chain enabled but maxChainTargets is {data.maxChainTargets}.", "Set maxChainTargets >= 1."));
                }

                // Effect Prefab check
                if (data.skillPrefab == null)
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Data", id, path,
                        $"Skill '{id}' has no effect prefab ('skillPrefab') assigned.", "Generate and assign a SkillFX effect prefab."));
                }
                else
                {
                    var fx = data.skillPrefab;
                    string fxPath = AssetDatabase.GetAssetPath(fx);

                    // Layer check (Layer 9 = SkillFX)
                    if (fx.layer != 9)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Effect", fx.name, fxPath,
                            $"Skill effect prefab layer is {fx.layer} ({LayerMask.LayerToName(fx.layer)}), expected layer 9 (SkillFX).",
                            "Set SkillFX prefab layer to 'SkillFX' (9)."));
                    }

                    // Trigger collider check
                    var col = fx.GetComponentInChildren<Collider2D>(true);
                    if (col == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Effect", fx.name, fxPath,
                            $"Skill effect prefab '{fx.name}' has no 2D Collider.", "Add a 2D Trigger Collider (Circle, Box, or Capsule)."));
                    }
                    else if (!col.isTrigger)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Skill Effect", fx.name, fxPath,
                            $"Skill effect collider on '{col.gameObject.name}' is not set to 'isTrigger=true'.", "Check 'Is Trigger' on the Collider2D."));
                    }

                    // SkillCollision component
                    var sc = fx.GetComponentInChildren<SkillCollision>(true);
                    if (sc == null)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Error, "Skill Effect", fx.name, fxPath,
                            $"Skill effect prefab '{fx.name}' is missing 'SkillCollision' component.", "Add SkillCollision component."));
                    }

                    // Friendly fire check
                    if (data.targetFilter == SkillData.SkillTargetFilter.Everyone || data.targetFilter == SkillData.SkillTargetFilter.AllExceptCaster)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Skill Data", id, path,
                            $"Skill '{id}' uses target filter '{data.targetFilter}' which can damage friendly allies (Friendly Fire).",
                            "Change targetFilter to 'EnemiesOnly' unless friendly fire is intentionally desired."));
                    }
                }

                // Utility asset registration check
                if (data.utilityData == null)
                {
                    bool inDb = utilDb != null && utilDb.utilSkills != null && utilDb.utilSkills.Any(u => string.Equals(u.id, id, StringComparison.OrdinalIgnoreCase));
                    if (!inDb)
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Skill Data", id, path,
                            $"Skill '{id}' has no direct UtilityData assigned and is missing from UtilitySkillDatabase.asset.",
                            "Create UtilitySkill asset and assign to SkillData or register in UtilitySkillDatabase.asset."));
                    }
                }
            }
        }

        // =====================================================================
        // SECTION C: GOAP & UTILITY AI
        // =====================================================================
        private static void AuditGoapAndUtilityAI(List<ValidationBadge> findings)
        {
            // 1. GoalBase vs UtilityGoal symmetry
            var goalBaseTypes = TypeCache.GetTypesDerivedFrom<GoalBase>().Where(t => !t.IsAbstract).ToList();
            var utilGoalTypes = TypeCache.GetTypesDerivedFrom<UtilityGoal>().Where(t => !t.IsAbstract).ToList();

            foreach (var gb in goalBaseTypes)
            {
                string baseName = gb.Name;
                if (baseName.EndsWith("Goal")) baseName = baseName.Substring(0, baseName.Length - 4);

                bool hasMatchingUtil = utilGoalTypes.Any(u => u.Name.Contains(baseName));
                if (!hasMatchingUtil && gb.Name != "IdleGoal")
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Warning, "GOAP Architecture", gb.Name, "Assets/Scripts/Character/AI/GOAP/Goals/",
                        $"GoalBase '{gb.Name}' has no matching UtilityGoal counterpart.",
                        $"Implement Utility{gb.Name} derived from UtilityGoal to enable Utility AI evaluation."));
                }
            }

            // 2. Capability Factory bindings
            var factoryTypes = TypeCache.GetTypesDerivedFrom<CapabilityFactoryBase>().Where(t => !t.IsAbstract).ToList();
            var agentFactoryTypes = TypeCache.GetTypesDerivedFrom<AgentTypeFactoryBase>().Where(t => !t.IsAbstract).ToList();

            if (factoryTypes.Count < 10)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Info, "GOAP Architecture", "Capability Factories", "Assets/Scripts/Character/AI/GOAP/Capabilities/",
                    $"Found {factoryTypes.Count} capability factories. (Expected 10 canonical factories).", "Verify all capability factories are preserved."));
            }

            // 3. Check MageAgentTypeFactory omitting HealCapabilityFactory
            var mageFactoryGo = Resources.FindObjectsOfTypeAll<MageAgentTypeFactory>().FirstOrDefault();
            // Code audit check
            string mageFactoryPath = "Assets/Scripts/Character/AI/GOAP/AgentTypes/MageAgentTypeFactory.cs";
            if (File.Exists(mageFactoryPath))
            {
                string code = File.ReadAllText(mageFactoryPath);
                if (!code.Contains("HealCapabilityFactory"))
                {
                    findings.Add(new ValidationBadge(ValidationSeverity.Warning, "GOAP Architecture", "MageAgentTypeFactory", mageFactoryPath,
                        "MageAgentTypeFactory does not register 'HealCapabilityFactory'. Mages and Clerics using MageAgent cannot execute ally healing plans.",
                        "Add 'factory.AddCapability<HealCapabilityFactory>();' to MageAgentTypeFactory.cs."));
                }
            }
        }

        // =====================================================================
        // SECTION D: DATA & SERVICES
        // =====================================================================
        private static void AuditDataAndServices(List<ValidationBadge> findings)
        {
            var masterDb = AssetDatabase.LoadAssetAtPath<MasterGameDatabaseSO>("Assets/Resources/MasterGameDatabase.asset");
            if (masterDb == null)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Database", "MasterGameDatabase", "Assets/Resources/MasterGameDatabase.asset",
                    "MasterGameDatabase.asset is missing from Assets/Resources/ folder.", "Create MasterGameDatabaseSO asset at Assets/Resources/MasterGameDatabase.asset."));
                return;
            }

            if (masterDb.Characters == null)
                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Database", "CharacterDatabase", "Assets/Resources/MasterGameDatabase.asset",
                    "CharacterDatabase sub-catalog is unassigned on MasterGameDatabase.", "Assign CharacterDatabase.asset to MasterGameDatabase."));
            else
            {
                foreach (var c in masterDb.Characters.Characters)
                {
                    if (c.prefab == null && (c.hybridPrefab == null || !c.hybridPrefab.HasDirectAsset))
                    {
                        findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Database", c.id, "Assets/ScriptableObjects/Database/CharacterDatabase.asset",
                            $"Character entry '{c.id}' has no prefab assigned in CharacterDatabase.", "Assign prefab to character entry."));
                    }
                }
            }

            if (masterDb.Skills == null)
                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Database", "SkillDatabase", "Assets/Resources/MasterGameDatabase.asset",
                    "SkillDatabase sub-catalog is unassigned on MasterGameDatabase.", "Assign SkillDatabase.asset to MasterGameDatabase."));

            if (masterDb.Items == null)
                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Database", "ItemDatabase", "Assets/Resources/MasterGameDatabase.asset",
                    "ItemDatabase sub-catalog is unassigned on MasterGameDatabase.", "Assign ItemDatabase.asset to MasterGameDatabase."));

            // CSV audit
            string charCsvPath = "Assets/DataTables/Characters.csv";
            if (File.Exists(charCsvPath))
            {
                var lines = File.ReadAllLines(charCsvPath);
                if (lines.Length > 0)
                {
                    var headers = lines[0].Split(',');
                    int defSkillsIdx = Array.IndexOf(headers, "DefaultSkillIds");
                    if (defSkillsIdx >= 0)
                    {
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var cols = lines[i].Split(',');
                            if (cols.Length > defSkillsIdx && string.IsNullOrWhiteSpace(cols[defSkillsIdx]))
                            {
                                string charId = cols[0];
                                findings.Add(new ValidationBadge(ValidationSeverity.Info, "Data Table", charId, charCsvPath,
                                    $"Characters.csv row '{charId}' has an empty DefaultSkillIds column.",
                                    "Populate DefaultSkillIds or sync from character prefab."));
                            }
                        }
                    }
                }
            }
        }

        // =====================================================================
        // SECTION E: SCENES
        // =====================================================================
        private static void AuditScenes(List<ValidationBadge> findings)
        {
            // 1. Layer collision matrix check
            int layerChar = 6;
            int layerTrigger = 7;
            int layerCol = 8;
            int layerSkill = 9;

            // Character <-> SkillFX (should be ignored)
            bool charIgnoreSkill = Physics2D.GetIgnoreLayerCollision(layerChar, layerSkill);
            if (!charIgnoreSkill)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Physics Layer Matrix", "Character (6) <-> SkillFX (9)", "ProjectSettings/Physics2DSettings.asset",
                    "Character (6) and SkillFX (9) physical collision is NOT ignored. Direct skill collider contact with character body may trigger unwanted physics pushes.",
                    "Set Physics2D.IgnoreLayerCollision(6, 9, true) so projectile hitboxes use Trigger queries."));
            }

            // HitboxTrigger <-> HitboxTrigger (should be ignored to avoid redundant trigger events)
            bool triggerIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerTrigger, layerTrigger);
            if (!triggerIgnoreTrigger)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Physics Layer Matrix", "HitboxTrigger (7) <-> HitboxTrigger (7)", "ProjectSettings/Physics2DSettings.asset",
                    "HitboxTrigger collides with HitboxTrigger. Every character's trigger volume generates unnecessary physics overlap events against other characters every physics step.",
                    "Set Physics2D.IgnoreLayerCollision(7, 7, true) in Project Settings -> Physics 2D."));
            }

            // Character <-> HitboxTrigger (should be ignored)
            bool charIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerChar, layerTrigger);
            if (!charIgnoreTrigger)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Physics Layer Matrix", "Character (6) <-> HitboxTrigger (7)", "ProjectSettings/Physics2DSettings.asset",
                    "Character root collides with HitboxTrigger. Can generate redundant self/child trigger collision events.",
                    "Set Physics2D.IgnoreLayerCollision(6, 7, true) in Project Settings -> Physics 2D."));
            }

            // HitboxCollision <-> HitboxTrigger (should be ignored)
            bool colIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerCol, layerTrigger);
            if (!colIgnoreTrigger)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Warning, "Physics Layer Matrix", "HitboxCollision (8) <-> HitboxTrigger (7)", "ProjectSettings/Physics2DSettings.asset",
                    "HitboxCollision collides with HitboxTrigger. Physical obstacle geometry unnecessarily triggers combat hitbox overlap callbacks.",
                    "Set Physics2D.IgnoreLayerCollision(8, 7, true) in Project Settings -> Physics 2D."));
            }

            // SkillFX <-> HitboxTrigger (MUST collide)
            bool skillHitsTrigger = !Physics2D.GetIgnoreLayerCollision(layerSkill, layerTrigger);
            if (!skillHitsTrigger)
            {
                findings.Add(new ValidationBadge(ValidationSeverity.Error, "Physics Layer Matrix", "SkillFX (9) <-> HitboxTrigger (7)", "ProjectSettings/Physics2DSettings.asset",
                    "SkillFX is set to IGNORE HitboxTrigger! Skill projectiles and AOE effects cannot register hits against character hitboxes.",
                    "Uncheck 'Ignore Layer Collision' between SkillFX (9) and HitboxTrigger (7)."));
            }

            // 2. Piece Set audit
            var starterSet = AssetDatabase.LoadAssetAtPath<PieceSetSO>("Assets/DungeonPiece/StarterSet/StarterPieceSet.asset");
            if (starterSet != null)
            {
                var badges = starterSet.ValidateSet();
                foreach (var b in badges)
                {
                    var sev = b.Severity == PieceValidationSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning;
                    findings.Add(new ValidationBadge(sev, "Dungeon Piece Set", b.PieceId ?? starterSet.name, "Assets/DungeonPiece/StarterSet/StarterPieceSet.asset",
                        b.Message, "Review opening markers, buildable mask and entry anchors in Piece Authoring tool."));
                }
            }
        }

        // =====================================================================
        // REPORT GENERATION
        // =====================================================================
        public static string ExportReportToMarkdown(List<ValidationBadge> findings)
        {
            string dir = "docs/testing";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string fullPath = Path.Combine(dir, "ai_setup_validation_report.md");

            var sb = new StringBuilder();
            sb.AppendLine("# AI Setup Validation Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
            sb.AppendLine($"**Total Findings:** {findings.Count} ({findings.Count(f => f.Severity == ValidationSeverity.Error)} Errors, {findings.Count(f => f.Severity == ValidationSeverity.Warning)} Warnings, {findings.Count(f => f.Severity == ValidationSeverity.Info)} Info)  ");
            sb.AppendLine();

            sb.AppendLine("## Summary of Findings by Category");
            sb.AppendLine();
            sb.AppendLine("| Category | Error | Warning | Info | Total |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: |");

            var groups = findings.GroupBy(f => f.Category).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                int errs = g.Count(f => f.Severity == ValidationSeverity.Error);
                int warns = g.Count(f => f.Severity == ValidationSeverity.Warning);
                int infos = g.Count(f => f.Severity == ValidationSeverity.Info);
                sb.AppendLine($"| {g.Key} | {errs} | {warns} | {infos} | {g.Count()} |");
            }
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var g in groups)
            {
                sb.AppendLine($"## Category: {g.Key}");
                sb.AppendLine();

                var sorted = g.OrderBy(f => f.Severity).ThenBy(f => f.TargetName);
                foreach (var f in sorted)
                {
                    string badge = f.Severity switch
                    {
                        ValidationSeverity.Error => "🔴 **ERROR**",
                        ValidationSeverity.Warning => "🟡 **WARNING**",
                        _ => "🔵 **INFO**"
                    };

                    sb.AppendLine($"### {badge} {f.TargetName}");
                    sb.AppendLine($"- **Asset Path:** `{f.AssetPath}`");
                    sb.AppendLine($"- **Problem:** {f.Message}");
                    sb.AppendLine($"- **Suggested Fix:** {f.SuggestedFix}");
                    sb.AppendLine();
                }
            }

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            return fullPath;
        }
    }
}
