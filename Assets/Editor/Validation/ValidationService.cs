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
using SBGames.Database.Editor;
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
    /// <summary>
    /// Shared UI-free validation service that runs AI Setup and Data audits across characters, skills, items, and global setup.
    /// </summary>
    public static class ValidationService
    {
        private static List<ValidationFinding> _cachedFindings;
        private static bool _isStale;

        public static bool IsStale => _isStale;

        public static void MarkStale()
        {
            _isStale = true;
        }

        public static List<ValidationFinding> GetCachedFindings()
        {
            return _cachedFindings;
        }

        public static List<ValidationFinding> RunFullValidation(Action<float, string> progressCallback = null)
        {
            var findings = new List<ValidationFinding>();

            progressCallback?.Invoke(0.1f, "Auditing Character Prefabs...");
            AuditCharacterPrefabs(findings);

            progressCallback?.Invoke(0.35f, "Auditing Skills & Effects...");
            AuditSkills(findings);

            progressCallback?.Invoke(0.6f, "Auditing GOAP & Utility AI...");
            AuditGoapAndUtilityAI(findings);
            AISetupValidatorCore.AuditUtilityCurves(findings);

            progressCallback?.Invoke(0.8f, "Auditing Data & Catalogs...");
            AuditDataAndServices(findings);

            progressCallback?.Invoke(0.95f, "Auditing Scenes & Physics Layers...");
            AuditScenes(findings);

            _cachedFindings = findings.OrderBy(f => f.Severity).ToList();
            _isStale = false;
            progressCallback?.Invoke(1.0f, "Validation Completed.");
            return _cachedFindings;
        }

        private static Dictionary<string, string> BuildPrefabToCharacterIdMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var charDb = DatabaseAutoIndexer.GetOrCreateCatalog<CharacterDatabaseSO>("CharacterDatabase");
            if (charDb != null && charDb.Characters != null)
            {
                foreach (var entry in charDb.Characters)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    var prefab = entry.GetPrefabDirect();
                    if (prefab != null)
                    {
                        map[prefab.name] = entry.id;
                        string path = AssetDatabase.GetAssetPath(prefab);
                        if (!string.IsNullOrEmpty(path))
                        {
                            map[path] = entry.id;
                        }
                    }
                }
            }
            return map;
        }

        // =====================================================================
        // SECTION A: CHARACTER PREFABS
        // =====================================================================
        private static void AuditCharacterPrefabs(List<ValidationFinding> findings)
        {
            var prefabToCharId = BuildPrefabToCharacterIdMap();

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
                string charId = unitName;
                if (prefabToCharId.TryGetValue(unitName, out var mappedId))
                {
                    charId = mappedId;
                }
                else if (prefabToCharId.TryGetValue(path, out var mappedPathId))
                {
                    charId = mappedPathId;
                }

                void AddCharFinding(ValidationSeverity sev, string msg, string fix)
                {
                    findings.Add(new ValidationFinding(
                        sev,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Character,
                        charId,
                        "Character Prefab",
                        unitName,
                        path,
                        msg,
                        fix));
                }

                // 1. Root Components Check
                var brain = go.GetComponent<CharacterBrain>();
                if (brain == null)
                    AddCharFinding(ValidationSeverity.Error,
                        "Root GameObject is missing 'CharacterBrain'.", "Add CharacterBrain component to root GameObject.");

                var stats = go.GetComponent<CharacterStatsManager>();
                if (stats == null)
                    AddCharFinding(ValidationSeverity.Error,
                        "Root GameObject is missing 'CharacterStatsManager'.", "Add CharacterStatsManager component to root GameObject.");

                var combat = go.GetComponent<CharacterCombatController>();
                if (combat == null)
                    AddCharFinding(ValidationSeverity.Error,
                        "Root GameObject is missing 'CharacterCombatController'.", "Add CharacterCombatController component to root GameObject.");

                var anim = go.GetComponent<AnimationObjectController>() ?? go.GetComponentInChildren<AnimationObjectController>(true);
                if (anim == null)
                    AddCharFinding(ValidationSeverity.Error,
                        "Character is missing AnimationObjectController / AnimationController.", "Attach AnimationController on Model child.");

                var faction = go.GetComponent<FactionMember>();
                if (faction == null)
                {
                    AddCharFinding(ValidationSeverity.Error,
                        "Root GameObject is missing 'FactionMember'.", "Add FactionMember component to root GameObject.");
                }
                else
                {
                    string expectedTag = faction.Faction.ToString();
                    if (!string.Equals(go.tag, expectedTag, StringComparison.OrdinalIgnoreCase))
                    {
                        AddCharFinding(ValidationSeverity.Warning,
                            $"Root tag '{go.tag}' does not match FactionMember faction '{faction.Faction}'.",
                            $"Set root GameObject tag to '{expectedTag}'.");
                    }
                }

                var nonTriggerCol = go.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c => !c.isTrigger);
                if (nonTriggerCol == null)
                {
                    AddCharFinding(ValidationSeverity.Warning,
                        "Character prefab has no non-trigger body collider anywhere in hierarchy.",
                        "Add a non-trigger BoxCollider2D or CapsuleCollider2D (on root or Collision child) to provide physical body presence.");
                }

                if (go.layer != 6)
                {
                    AddCharFinding(ValidationSeverity.Error,
                        $"Root layer is {go.layer} ({LayerMask.LayerToName(go.layer)}), expected layer 6 (Character).",
                        "Set root GameObject layer to 'Character' (6).");
                }

                // 2. AI Augment Child Check
                var aiAug = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.CompareTag("AI_Augment"));
                if (aiAug == null)
                {
                    AddCharFinding(ValidationSeverity.Error,
                        "Missing child GameObject tagged 'AI_Augment'.", "Create child GameObject named 'AI_Augment' with tag 'AI_Augment'.");
                }
                else
                {
                    if (!aiAug.CompareTag("AI_Augment"))
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            $"AI child '{aiAug.name}' is missing the 'AI_Augment' tag.",
                            "Set AI child tag to 'AI_Augment'.");
                    }

                    var baseAI = aiAug.GetComponent<BaseAI>();
                    if (baseAI == null)
                        AddCharFinding(ValidationSeverity.Error,
                            "AI_Augment child is missing 'BaseAI'.", "Attach BaseAI component to AI_Augment.");

                    var goapProv = aiAug.GetComponent<GoapActionProvider>();
                    if (goapProv == null)
                        AddCharFinding(ValidationSeverity.Error,
                            "AI_Augment child is missing 'GoapActionProvider'.", "Attach GoapActionProvider component to AI_Augment.");

                    var agentBhv = aiAug.GetComponent<AgentBehaviour>();
                    if (agentBhv == null)
                        AddCharFinding(ValidationSeverity.Error,
                            "AI_Augment child is missing 'AgentBehaviour'.", "Attach AgentBehaviour component to AI_Augment.");

                    var moveBhv = aiAug.GetComponent<AgentMoveBehaviour>();
                    if (moveBhv == null)
                        AddCharFinding(ValidationSeverity.Error,
                            "AI_Augment child is missing 'AgentMoveBehaviour'.", "Attach AgentMoveBehaviour component to AI_Augment.");

                    var nav = go.GetComponent<NavMeshAgent>() ?? go.GetComponentInChildren<NavMeshAgent>(true);
                    if (nav == null)
                        AddCharFinding(ValidationSeverity.Error,
                            "Character is missing 'NavMeshAgent'.", "Attach NavMeshAgent component.");

                    var vision = go.GetComponentInChildren<VisionController>(true);
                    if (vision == null)
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            "Character is missing 'VisionController'.", "Attach VisionController under Sensor child.");
                    }
                    else
                    {
                        if (vision.Tuning == null)
                        {
                            AddCharFinding(ValidationSeverity.Warning,
                                "VisionController has no PerceptionTuningSO assigned.", "Assign 'PerceptionTuning_Default.asset' to VisionController tuning.");
                        }

                        var sensors = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sensors" || t.name == "Sensor");
                        if (sensors == null)
                        {
                            AddCharFinding(ValidationSeverity.Error,
                                "Missing 'Sensors' child GameObject carrying perception sensors.", "Create 'Sensors' child with RangeSensor2D and LOSSensor2D.");
                        }
                        else
                        {
                            var range = sensors.GetComponentInChildren<RangeSensor2D>(true);
                            var los = sensors.GetComponentInChildren<LOSSensor2D>(true);
                            if (range == null)
                                AddCharFinding(ValidationSeverity.Error,
                                    "Sensors child is missing 'RangeSensor2D'.", "Add RangeSensor2D component to Sensors child.");
                            if (los == null)
                                AddCharFinding(ValidationSeverity.Error,
                                    "Sensors child is missing 'LOSSensor2D'.", "Add LOSSensor2D component to Sensors child.");

                            if (vision.Sensor == null)
                            {
                                AddCharFinding(ValidationSeverity.Error,
                                    "VisionController 'sensor' reference is unassigned.", "Wire RangeSensor2D or LOSSensor2D into VisionController sensor field.");
                            }
                        }
                    }

                    var skillCtrl = go.GetComponentInChildren<CharacterSkillController>(true);
                    if (skillCtrl == null)
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            "Character is missing 'CharacterSkillController'.", "Attach CharacterSkillController under Skills child.");
                    }

                    if (baseAI != null)
                    {
                        var aiSo = new SerializedObject(baseAI);
                        string agentTypeStr = aiSo.FindProperty("agentType")?.stringValue ?? "";
                        if (string.IsNullOrEmpty(agentTypeStr))
                        {
                            AddCharFinding(ValidationSeverity.Error,
                                "BaseAI agentType string is empty.", "Set agentType string to 'NormalAgent', 'MageAgent', or 'MinionAgent'.");
                        }

                        bool isHealer = baseAI.Goals != null && baseAI.Goals.Any(g => g != null && g.name.Contains("Heal"));
                        if (isHealer && agentTypeStr == "MageAgent")
                        {
                            AddCharFinding(ValidationSeverity.Error,
                                $"Unit '{unitName}' has healing goals/skills but uses '{agentTypeStr}' which omits HealCapabilityFactory.",
                                "Switch agentType to 'NormalAgent' or register HealCapabilityFactory inside MageAgentTypeFactory.cs.");
                        }

                        bool hasCombatGoal = baseAI.Goals != null && baseAI.Goals.Any(g => g != null && (g.name.Contains("Kill") || g.name.Contains("Survive")));
                        if (!hasCombatGoal)
                        {
                            AddCharFinding(ValidationSeverity.Warning,
                                "BaseAI has neither KillEnemyGoal nor SurviveGoal assigned.", "Assign DefaultKillEnemyGoal and DefaultSurviveGoal to BaseAI.goals.");
                        }
                    }
                }

                // 3. Stats & Regen Tuning
                if (stats != null)
                {
                    if (stats.BaseStats == null)
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            "CharacterStatsManager has no 'BaseStats' ScriptableObject assigned.", "Assign corresponding CharacterBaseStatSO asset to BaseStats field.");
                    }
                    else
                    {
                        var bs = stats.BaseStats;
                        if (bs.MaxLife <= 0)
                            AddCharFinding(ValidationSeverity.Error,
                                $"BaseStats MaxLife is {bs.MaxLife} (must be > 0).", "Configure MaxLife in CharacterBaseStatSO and Characters.csv.");
                        if (bs.Movespeed <= 0)
                            AddCharFinding(ValidationSeverity.Error,
                                $"BaseStats Movespeed is {bs.Movespeed} (must be > 0).", "Configure Movespeed in CharacterBaseStatSO and Characters.csv.");

                        var skillCtrlCheck = go.GetComponentInChildren<CharacterSkillController>(true);
                        bool hasUsableSkills = skillCtrlCheck != null && skillCtrlCheck.Skills != null && skillCtrlCheck.Skills.Count > 0;
                        if (bs.basicAttackMode == BasicAttackMode.None && !hasUsableSkills)
                        {
                            findings.Add(new ValidationFinding(
                                ValidationSeverity.Info,
                                ValidationFindingSource.AISetup,
                                ValidationSubjectType.Character,
                                charId,
                                "Character Prefab",
                                unitName,
                                path,
                                "BasicAttackMode is None (mage/healer mode) and unit has no usable skills assigned.",
                                "Assign usable offensive or support skills to the unit."));
                        }

                        float longestSkillRange = 0f;
                        if (skillCtrlCheck != null && skillCtrlCheck.Skills != null)
                        {
                            foreach (var sk in skillCtrlCheck.Skills)
                            {
                                if (sk != null && sk.Data != null && sk.Data.EffectiveRange > longestSkillRange)
                                    longestSkillRange = sk.Data.EffectiveRange;
                            }
                        }
                        float refRange = SBGames.Character.KeepDistanceResolver.ResolveReferenceRange(bs.AttackRange, bs.basicAttackMode, longestSkillRange);
                        bool keepDistanceEnabled = SBGames.Character.KeepDistanceResolver.ResolveIsEnabled(bs.keepDistanceMode, refRange);
                        if (keepDistanceEnabled && refRange <= 1.5f && bs.comfortDistanceOverride <= 0f)
                        {
                            AddCharFinding(ValidationSeverity.Warning,
                                $"Unit '{unitName}' has Keep Distance enabled (mode {bs.keepDistanceMode}) but has no usable range (refRange={refRange:F1}).",
                                "Provide a ranged basic attack, a ranged skill, set comfortDistanceOverride, or set KeepDistanceMode to Off.");
                        }
                    }

                    if (stats.RegenTuning == null)
                    {
                        AddCharFinding(ValidationSeverity.Warning,
                            "CharacterStatsManager has no 'RegenTuning' SO assigned.", "Assign 'RegenTuning_Default.asset' to CharacterStatsManager.");
                    }
                }

                // 4. Adventurer-Specific Wire-ups
                if (path.Contains("Adventurer"))
                {
                    var sm = go.GetComponentInChildren<SquadMember>(true);
                    var pl = go.GetComponentInChildren<PartyLeader>(true);
                    if (sm == null && pl == null)
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            "Adventurer is missing SquadMember or PartyLeader component.", "Attach SquadMember component to adventurer root.");
                    }

                    var itemKit = go.GetComponentInChildren<AdventurerItemKitController>(true);
                    if (itemKit == null)
                    {
                        AddCharFinding(ValidationSeverity.Error,
                            "Adventurer is missing 'AdventurerItemKitController'.", "Attach AdventurerItemKitController component to adventurer root.");
                    }

                    var doorway = go.GetComponentInChildren<DoorwayStagingController>(true);
                    if (doorway == null)
                    {
                        AddCharFinding(ValidationSeverity.Warning,
                            "Adventurer is missing 'DoorwayStagingController'.", "Attach DoorwayStagingController component to AI_Augment child.");
                    }

                    var memory = go.GetComponentInChildren<CharacterAIMemory>(true);
                    if (memory == null)
                    {
                        AddCharFinding(ValidationSeverity.Warning,
                            "Adventurer is missing 'CharacterAIMemory'.", "Attach CharacterAIMemory component to AI_Augment child.");
                    }

                    var throttler = go.GetComponentInChildren<AIDecisionThrottler>(true);
                    if (throttler == null)
                    {
                        AddCharFinding(ValidationSeverity.Warning,
                            "Adventurer is missing 'AIDecisionThrottler'.", "Attach AIDecisionThrottler component to AI_Augment child.");
                    }

                    if (stats != null && stats.BaseStats != null)
                    {
                        if (stats.BaseStats.CharacterFunction == AdventurerFunction.None)
                        {
                            AddCharFinding(ValidationSeverity.Warning,
                                "Adventurer has CharacterFunction set to 'None' in CharacterBaseStatSO.", "Assign role (Tank, DPS, Healer, or Scout) in CharacterBaseStatSO.");
                        }
                    }
                }
            }
        }

        // =====================================================================
        // SECTION B: SKILLS & EFFECTS
        // =====================================================================
        private static void AuditSkills(List<ValidationFinding> findings)
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
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Error,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Skill,
                        data.name,
                        "Skill Data",
                        data.name,
                        path,
                        "SkillData has an empty 'skillID'.",
                        "Assign a unique skill ID (e.g. SKILL_xxx)."));
                    continue;
                }

                if (seenIds.Contains(id))
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Error,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Skill,
                        id,
                        "Skill Data",
                        id,
                        path,
                        $"Duplicate SkillID '{id}' found.",
                        "Ensure every SkillData has a globally unique skillID."));
                }
                seenIds.Add(id);

                if (data.type == SkillType.Projectile)
                {
                    if (data.projectileSpeed <= 0f)
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Error,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Data",
                            id,
                            path,
                            $"Projectile skill '{id}' has non-positive projectileSpeed ({data.projectileSpeed}).",
                            "Set projectileSpeed > 0."));
                    if (data.projectileLifetime <= 0f)
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Error,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Data",
                            id,
                            path,
                            $"Projectile skill '{id}' has non-positive projectileLifetime ({data.projectileLifetime}).",
                            "Set projectileLifetime > 0."));
                }

                if (data.area && data.areaRadius <= 0f)
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Warning,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Skill,
                        id,
                        "Skill Data",
                        id,
                        path,
                        $"Area skill '{id}' has area enabled but areaRadius is {data.areaRadius}.",
                        "Set areaRadius > 0."));
                }

                if (data.chain && data.maxChainTargets <= 0)
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Warning,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Skill,
                        id,
                        "Skill Data",
                        id,
                        path,
                        $"Chain skill '{id}' has chain enabled but maxChainTargets is {data.maxChainTargets}.",
                        "Set maxChainTargets >= 1."));
                }

                // Effect Prefab check
                if (data.skillPrefab == null)
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Error,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Skill,
                        id,
                        "Skill Data",
                        id,
                        path,
                        $"Skill '{id}' has no effect prefab ('skillPrefab') assigned.",
                        "Generate and assign a SkillFX effect prefab."));
                }
                else
                {
                    var fx = data.skillPrefab;
                    string fxPath = AssetDatabase.GetAssetPath(fx);

                    if (fx.layer != 9)
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Error,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Effect",
                            fx.name,
                            fxPath,
                            $"Skill effect prefab layer is {fx.layer} ({LayerMask.LayerToName(fx.layer)}), expected layer 9 (SkillFX).",
                            "Set SkillFX prefab layer to 'SkillFX' (9)."));
                    }

                    var col = fx.GetComponentInChildren<Collider2D>(true);
                    if (col == null)
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Error,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Effect",
                            fx.name,
                            fxPath,
                            $"Skill effect prefab '{fx.name}' has no 2D Collider.",
                            "Add a 2D Trigger Collider (Circle, Box, or Capsule)."));
                    }
                    else if (!col.isTrigger)
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Warning,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Effect",
                            fx.name,
                            fxPath,
                            $"Skill effect collider on '{col.gameObject.name}' is not set to 'isTrigger=true'.",
                            "Check 'Is Trigger' on the Collider2D."));
                    }

                    var sc = fx.GetComponentInChildren<SkillCollision>(true);
                    if (sc == null)
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Error,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Effect",
                            fx.name,
                            fxPath,
                            $"Skill effect prefab '{fx.name}' is missing 'SkillCollision' component.",
                            "Add SkillCollision component."));
                    }

                    if (data.targetFilter == SkillData.SkillTargetFilter.Everyone || data.targetFilter == SkillData.SkillTargetFilter.AllExceptCaster)
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Warning,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Data",
                            id,
                            path,
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
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Warning,
                            ValidationFindingSource.AISetup,
                            ValidationSubjectType.Skill,
                            id,
                            "Skill Data",
                            id,
                            path,
                            $"Skill '{id}' has no direct UtilityData assigned and is missing from UtilitySkillDatabase.asset.",
                            "Create UtilitySkill asset and assign to SkillData or register in UtilitySkillDatabase.asset."));
                    }
                }
            }
        }

        // =====================================================================
        // SECTION C: GOAP & UTILITY AI
        // =====================================================================
        private static void AuditGoapAndUtilityAI(List<ValidationFinding> findings)
        {
            var goalBaseTypes = TypeCache.GetTypesDerivedFrom<GoalBase>().Where(t => !t.IsAbstract).ToList();
            var utilGoalTypes = TypeCache.GetTypesDerivedFrom<UtilityGoal>().Where(t => !t.IsAbstract).ToList();

            foreach (var gb in goalBaseTypes)
            {
                string baseName = gb.Name;
                if (baseName.EndsWith("Goal")) baseName = baseName.Substring(0, baseName.Length - 4);

                bool hasMatchingUtil = utilGoalTypes.Any(u => u.Name.Contains(baseName));
                if (!hasMatchingUtil && gb.Name == "DebriefLeaderGoal")
                {
                    hasMatchingUtil = utilGoalTypes.Any(u => u.Name == "UtilityDebriefGoal");
                }
                if (!hasMatchingUtil && gb.Name != "IdleGoal")
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Warning,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Global,
                        string.Empty,
                        "GOAP Architecture",
                        gb.Name,
                        "Assets/Scripts/Character/AI/GOAP/Goals/",
                        $"GoalBase '{gb.Name}' has no matching UtilityGoal counterpart.",
                        $"Implement Utility{gb.Name} derived from UtilityGoal to enable Utility AI evaluation."));
                }
            }

            var factoryTypes = TypeCache.GetTypesDerivedFrom<CapabilityFactoryBase>().Where(t => !t.IsAbstract).ToList();

            if (factoryTypes.Count < 10)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Info,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "GOAP Architecture",
                    "Capability Factories",
                    "Assets/Scripts/Character/AI/GOAP/Capabilities/",
                    $"Found {factoryTypes.Count} capability factories. (Expected 10 canonical factories).",
                    "Verify all capability factories are preserved."));
            }

            string mageFactoryPath = "Assets/Scripts/Character/AI/GOAP/AgentTypes/MageAgentTypeFactory.cs";
            if (File.Exists(mageFactoryPath))
            {
                string code = File.ReadAllText(mageFactoryPath);
                if (!code.Contains("HealCapabilityFactory"))
                {
                    findings.Add(new ValidationFinding(
                        ValidationSeverity.Warning,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Global,
                        string.Empty,
                        "GOAP Architecture",
                        "MageAgentTypeFactory",
                        mageFactoryPath,
                        "MageAgentTypeFactory does not register 'HealCapabilityFactory'. Mages and Clerics using MageAgent cannot execute ally healing plans.",
                        "Add 'factory.AddCapability<HealCapabilityFactory>();' to MageAgentTypeFactory.cs."));
                }
            }
        }

        // =====================================================================
        // SECTION D: DATA & SERVICES
        // =====================================================================
        private static void AuditDataAndServices(List<ValidationFinding> findings)
        {
            var masterDb = AssetDatabase.LoadAssetAtPath<MasterGameDatabaseSO>("Assets/Resources/MasterGameDatabase.asset");
            if (masterDb == null)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Error,
                    ValidationFindingSource.Data,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Database",
                    "MasterGameDatabase",
                    "Assets/Resources/MasterGameDatabase.asset",
                    "MasterGameDatabase.asset is missing from Assets/Resources/ folder.",
                    "Create MasterGameDatabaseSO asset at Assets/Resources/MasterGameDatabase.asset."));
                return;
            }

            if (masterDb.Characters == null)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Error,
                    ValidationFindingSource.Data,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Database",
                    "CharacterDatabase",
                    "Assets/Resources/MasterGameDatabase.asset",
                    "CharacterDatabase sub-catalog is unassigned on MasterGameDatabase.",
                    "Assign CharacterDatabase.asset to MasterGameDatabase."));
            }
            else
            {
                foreach (var c in masterDb.Characters.Characters)
                {
                    if (c == null) continue;
                    if (c.prefab == null && (c.hybridPrefab == null || !c.hybridPrefab.HasDirectAsset))
                    {
                        findings.Add(new ValidationFinding(
                            ValidationSeverity.Warning,
                            ValidationFindingSource.Data,
                            ValidationSubjectType.Character,
                            c.id,
                            "Database",
                            c.id,
                            "Assets/ScriptableObjects/Database/CharacterDatabase.asset",
                            $"Character entry '{c.id}' has no prefab assigned in CharacterDatabase.",
                            "Assign prefab to character entry."));
                    }
                }
            }

            if (masterDb.Skills == null)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Error,
                    ValidationFindingSource.Data,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Database",
                    "SkillDatabase",
                    "Assets/Resources/MasterGameDatabase.asset",
                    "SkillDatabase sub-catalog is unassigned on MasterGameDatabase.",
                    "Assign SkillDatabase.asset to MasterGameDatabase."));
            }

            if (masterDb.Items == null)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Error,
                    ValidationFindingSource.Data,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Database",
                    "ItemDatabase",
                    "Assets/Resources/MasterGameDatabase.asset",
                    "ItemDatabase sub-catalog is unassigned on MasterGameDatabase.",
                    "Assign ItemDatabase.asset to MasterGameDatabase."));
            }

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
                                if (charId.IndexOf("DUMMY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    charId.IndexOf("BOSS", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    continue;
                                }

                                var masterCharDb = masterDb != null ? masterDb.Characters : null;
                                var charEntry = masterCharDb != null && masterCharDb.Characters != null ? masterCharDb.Characters.FirstOrDefault(e => e != null && string.Equals(e.id, charId, StringComparison.OrdinalIgnoreCase)) : null;
                                var prefabObj = charEntry != null ? charEntry.GetPrefabDirect() : null;
                                bool prefabHasSkills = false;
                                if (prefabObj != null)
                                {
                                    var sc = prefabObj.GetComponentInChildren<CharacterSkillController>(true);
                                    prefabHasSkills = sc != null && sc.Skills != null && sc.Skills.Count > 0;
                                }

                                if (prefabHasSkills)
                                {
                                    findings.Add(new ValidationFinding(
                                        ValidationSeverity.Warning,
                                        ValidationFindingSource.Data,
                                        ValidationSubjectType.Character,
                                        charId,
                                        "Data Table",
                                        charId,
                                        charCsvPath,
                                        $"Characters.csv row '{charId}' has an empty DefaultSkillIds column but its prefab has skills configured.",
                                        "Export Characters.csv from Database tool or populate DefaultSkillIds."));
                                }
                            }
                        }
                    }
                }
            }
        }

        // =====================================================================
        // SECTION E: SCENES
        // =====================================================================
        private static void AuditScenes(List<ValidationFinding> findings)
        {
            int layerChar = 6;
            int layerTrigger = 7;
            int layerCol = 8;
            int layerSkill = 9;

            bool charIgnoreSkill = Physics2D.GetIgnoreLayerCollision(layerChar, layerSkill);
            if (!charIgnoreSkill)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Physics Layer Matrix",
                    "Character (6) <-> SkillFX (9)",
                    "ProjectSettings/Physics2DSettings.asset",
                    "Character (6) and SkillFX (9) physical collision is NOT ignored. Direct skill collider contact with character body may trigger unwanted physics pushes.",
                    "Set Physics2D.IgnoreLayerCollision(6, 9, true) so projectile hitboxes use Trigger queries."));
            }

            bool triggerIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerTrigger, layerTrigger);
            if (!triggerIgnoreTrigger)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Physics Layer Matrix",
                    "HitboxTrigger (7) <-> HitboxTrigger (7)",
                    "ProjectSettings/Physics2DSettings.asset",
                    "HitboxTrigger collides with HitboxTrigger. Every character's trigger volume generates unnecessary physics overlap events against other characters every physics step.",
                    "Set Physics2D.IgnoreLayerCollision(7, 7, true) in Project Settings -> Physics 2D."));
            }

            bool charIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerChar, layerTrigger);
            if (!charIgnoreTrigger)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Physics Layer Matrix",
                    "Character (6) <-> HitboxTrigger (7)",
                    "ProjectSettings/Physics2DSettings.asset",
                    "Character root collides with HitboxTrigger. Can generate redundant self/child trigger collision events.",
                    "Set Physics2D.IgnoreLayerCollision(6, 7, true) in Project Settings -> Physics 2D."));
            }

            bool colIgnoreTrigger = Physics2D.GetIgnoreLayerCollision(layerCol, layerTrigger);
            if (!colIgnoreTrigger)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Physics Layer Matrix",
                    "HitboxCollision (8) <-> HitboxTrigger (7)",
                    "ProjectSettings/Physics2DSettings.asset",
                    "HitboxCollision collides with HitboxTrigger. Physical obstacle geometry unnecessarily triggers combat hitbox overlap callbacks.",
                    "Set Physics2D.IgnoreLayerCollision(8, 7, true) in Project Settings -> Physics 2D."));
            }

            bool skillHitsTrigger = !Physics2D.GetIgnoreLayerCollision(layerSkill, layerTrigger);
            if (!skillHitsTrigger)
            {
                findings.Add(new ValidationFinding(
                    ValidationSeverity.Error,
                    ValidationFindingSource.AISetup,
                    ValidationSubjectType.Global,
                    string.Empty,
                    "Physics Layer Matrix",
                    "SkillFX (9) <-> HitboxTrigger (7)",
                    "ProjectSettings/Physics2DSettings.asset",
                    "SkillFX is set to IGNORE HitboxTrigger! Skill projectiles and AOE effects cannot register hits against character hitboxes.",
                    "Uncheck 'Ignore Layer Collision' between SkillFX (9) and HitboxTrigger (7)."));
            }

            var starterSet = AssetDatabase.LoadAssetAtPath<PieceSetSO>("Assets/DungeonPiece/StarterSet/StarterPieceSet.asset");
            if (starterSet != null)
            {
                var badges = starterSet.ValidateSet();
                foreach (var b in badges)
                {
                    var sev = b.Severity == PieceValidationSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning;
                    findings.Add(new ValidationFinding(
                        sev,
                        ValidationFindingSource.AISetup,
                        ValidationSubjectType.Global,
                        string.Empty,
                        "Dungeon Piece Set",
                        b.PieceId ?? starterSet.name,
                        "Assets/DungeonPiece/StarterSet/StarterPieceSet.asset",
                        b.Message,
                        "Review opening markers, buildable mask and entry anchors in Piece Authoring tool."));
                }
            }
        }

        // =====================================================================
        // REPORT GENERATION
        // =====================================================================
        public static string ExportReportToMarkdown(List<ValidationFinding> findings)
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
