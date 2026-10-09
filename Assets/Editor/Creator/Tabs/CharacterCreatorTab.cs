using System;
using System.Collections.Generic;
using System.IO;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using Micosmo.SensorToolkit;
using SBGames.Database;
using SBGames.Inventory.Character;
using SBGames.Inventory.Stats;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.AI;

namespace SBGames.Editor.Creator.Tabs
{
    public enum CharacterArchetype
    {
        NormalMonster,
        EliteMonster,
        BossMonster,
        Adventurer
    }

    [Serializable]
    public class CharacterCreatorTab : ICreatorTab
    {
        [NonSerialized] private SBGamesCreatorWindow _hostWindow;

        public string Title => "⚔️ Character";

        // ----------------------------------------------------
        // Presets & Basic Config
        // ----------------------------------------------------
        [SerializeField] private CharacterArchetype archetype = CharacterArchetype.NormalMonster;
        [SerializeField] private string characterId = "MONSTER_N003";
        [SerializeField] private string characterDisplayName = "New Monster";

        // ----------------------------------------------------
        // Visuals & Animation
        // ----------------------------------------------------
        [SerializeField] private Sprite sprite;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Vector3 modelScale = new(5f, 5f, 1f);
        [SerializeField] private string sortingLayerName = "Character";
        [SerializeField] private int sortingOrder = 0;

        // ----------------------------------------------------
        // Faction & Layers
        // ----------------------------------------------------
        [SerializeField] private string rootTag = "Monster";
        [SerializeField] private int rootLayer = 6; // Character
        [SerializeField] private int hitboxTriggerLayer = 7; // HitboxTrigger
        [SerializeField] private int hitboxCollisionLayer = 8; // HitboxCollision

        // ----------------------------------------------------
        // Stats & Companion SO
        // ----------------------------------------------------
        [SerializeField] private bool createBaseStatsSO = true;
        [SerializeField] private CharacterBaseStatSO existingBaseStatsSO;
        [SerializeField] private int characterLevel = 1;
        [SerializeField] private CharacterRank characterRank = CharacterRank.D;
        [SerializeField] private float maxLife = 100f;
        [SerializeField] private float maxMana = 50f;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float attackTimeInterval = 1f;
        [SerializeField] private float defaultPostAttackRecovery = 0.3f;
        [SerializeField] private float defaultPostCastRecovery = 0.3f;
        [SerializeField] private float decisionInterval = 0.3f;
        [SerializeField] private float intervalJitter = 0.05f;

        // ----------------------------------------------------
        // AI & GOAP Settings
        // ----------------------------------------------------
        [SerializeField] private bool hasAI = true;
        [SerializeField] private ControlMode controlMode = ControlMode.AI;
        [SerializeField] private AgentStatus agentStatus = AgentStatus.Active;
        [SerializeField] private string agentType = "NormalAgent";
        [SerializeField] private List<UtilityGoal> defaultGoals = new();

        // ----------------------------------------------------
        // Skills & Points
        // ----------------------------------------------------
        [SerializeField] public List<GameObject> startingSkillPrefabs = new();
        [SerializeField] private Vector2 attackPointOffset = new(0.102f, -0.038f);
        [SerializeField] private float attackGizmoRadius = 0.4f;
        [SerializeField] private Vector2 skillSpawnOffset = new(0.563f, -0.191f);
        [SerializeField] private float skillSpawnGizmoRadius = 0.15f;

        // ----------------------------------------------------
        // Colliders & Hitboxes
        // ----------------------------------------------------
        [SerializeField] private Vector2 triggerOffset = new(-0.02f, -0.115f);
        [SerializeField] private Vector2 triggerSize = new(0.55f, 1.2f);
        [SerializeField] private Vector2 collisionOffset = new(-0.03f, -0.42f);
        [SerializeField] private Vector2 collisionSize = new(0.55f, 0.6f);

        // ----------------------------------------------------
        // Paths & Database Registration
        // ----------------------------------------------------
        [SerializeField] private string prefabFolderPath = "Assets/Prefabs/Characters/Monsters/Normal";
        [SerializeField] private string statsFolderPath = "Assets/ScriptableObjects/Character/Monster/Normal";
        [SerializeField] private bool autoRegisterInDatabase = true;
        [SerializeField] private CharacterDatabaseSO characterDatabase;

        // UI Foldouts
        private bool _foldoutBasic = true;
        private bool _foldoutVisuals = true;
        private bool _foldoutStats = true;
        private bool _foldoutAI = true;
        private bool _foldoutSkills = true;
        private bool _foldoutColliders = false;
        private bool _foldoutSaving = true;

        private Vector2 _scrollPos;

        public void Initialize(SBGamesCreatorWindow window)
        {
            _hostWindow = window;
        }

        public void OnEnable()
        {
            AutoFindDefaults();
        }

        private void AutoFindDefaults()
        {
            if (characterDatabase == null)
            {
                characterDatabase = CreatorEditorUtils.FindFirstAsset<CharacterDatabaseSO>();
            }

            if (defaultGoals.Count == 0)
            {
                var killGoal = AssetDatabase.LoadAssetAtPath<UtilityGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultKillEnemyGoal.asset");
                var wanderGoal = AssetDatabase.LoadAssetAtPath<UtilityGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultWanderGoal.asset");
                if (killGoal != null) defaultGoals.Add(killGoal);
                if (wanderGoal != null) defaultGoals.Add(wanderGoal);
            }
        }

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            CreatorEditorUtils.DrawHeader("⚔️ Character Prefab Creator", "Procedurally construct character prefabs with full wiring based on MONSTER_N001 blueprint.");
            EditorGUILayout.Space(8);

            DrawArchetypeSelector();
            EditorGUILayout.Space(6);

            DrawBasicSection();
            EditorGUILayout.Space(6);

            DrawVisualsSection();
            EditorGUILayout.Space(6);

            DrawStatsSection();
            EditorGUILayout.Space(6);

            DrawAISection();
            EditorGUILayout.Space(6);

            DrawSkillsSection();
            EditorGUILayout.Space(6);

            DrawCollidersSection();
            EditorGUILayout.Space(6);

            DrawSavingSection();
            EditorGUILayout.Space(12);

            CreatorEditorUtils.DrawGenerateButton("🚀 Generate Character Prefab", Generate, new Color(0.3f, 0.85f, 0.4f));
            EditorGUILayout.Space(12);

            EditorGUILayout.EndScrollView();
        }

        public void Generate()
        {
            CreateCharacterPrefab();
        }

        private void DrawArchetypeSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Archetype Preset", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                archetype = (CharacterArchetype)EditorGUILayout.EnumPopup("Archetype", archetype);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyArchetypePreset(archetype);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Normal Monster", EditorStyles.miniButtonLeft)) { archetype = CharacterArchetype.NormalMonster; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Elite Monster", EditorStyles.miniButtonMid)) { archetype = CharacterArchetype.EliteMonster; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Boss Monster", EditorStyles.miniButtonMid)) { archetype = CharacterArchetype.BossMonster; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Adventurer Character", EditorStyles.miniButtonRight)) { archetype = CharacterArchetype.Adventurer; ApplyArchetypePreset(archetype); }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ApplyArchetypePreset(CharacterArchetype type)
        {
            switch (type)
            {
                case CharacterArchetype.NormalMonster:
                    rootTag = "Monster";
                    rootLayer = 6;
                    controlMode = ControlMode.AI;
                    agentStatus = AgentStatus.Active;
                    hasAI = true;
                    agentType = "NormalAgent";
                    characterRank = CharacterRank.D;
                    maxLife = 100f;
                    damage = 10f;
                    moveSpeed = 3.5f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Monsters/Normal";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Monster/Normal";
                    break;

                case CharacterArchetype.EliteMonster:
                    rootTag = "Monster";
                    rootLayer = 6;
                    controlMode = ControlMode.AI;
                    agentStatus = AgentStatus.Active;
                    hasAI = true;
                    agentType = "EliteAgent";
                    characterRank = CharacterRank.B;
                    maxLife = 250f;
                    damage = 25f;
                    moveSpeed = 4f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Monsters/Elite";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Monster/Elite";
                    break;

                case CharacterArchetype.BossMonster:
                    rootTag = "Monster";
                    rootLayer = 6;
                    controlMode = ControlMode.AI;
                    agentStatus = AgentStatus.Active;
                    hasAI = true;
                    agentType = "BossAgent";
                    characterRank = CharacterRank.S;
                    maxLife = 1000f;
                    damage = 50f;
                    moveSpeed = 4.5f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Monsters/Boss";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Monster/Boss";
                    break;

                case CharacterArchetype.Adventurer:
                    rootTag = "Adventurer";
                    rootLayer = 6;
                    controlMode = ControlMode.Player;
                    agentStatus = AgentStatus.Inactive;
                    hasAI = false;
                    agentType = "";
                    characterRank = CharacterRank.C;
                    maxLife = 150f;
                    damage = 15f;
                    moveSpeed = 5f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Adventurers";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Adventurers";
                    break;
            }
        }

        private void DrawBasicSection()
        {
            _foldoutBasic = EditorGUILayout.Foldout(_foldoutBasic, "1. Identity & Naming", true, EditorStyles.foldoutHeader);
            if (!_foldoutBasic) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                characterId = EditorGUILayout.TextField(new GUIContent("Character ID", "Unique identifier code (e.g. MONSTER_N003)"), characterId);
                characterDisplayName = EditorGUILayout.TextField(new GUIContent("Display Name", "In-game human readable name"), characterDisplayName);
            }
        }

        private void DrawVisualsSection()
        {
            _foldoutVisuals = EditorGUILayout.Foldout(_foldoutVisuals, "2. Visuals & Presentation", true, EditorStyles.foldoutHeader);
            if (!_foldoutVisuals) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                sprite = (Sprite)EditorGUILayout.ObjectField("Default Sprite", sprite, typeof(Sprite), false);
                animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(RuntimeAnimatorController), false);
                modelScale = EditorGUILayout.Vector3Field("Model Scale", modelScale);

                EditorGUILayout.Space(2);
                sortingLayerName = EditorGUILayout.TextField("Sorting Layer", sortingLayerName);
                sortingOrder = EditorGUILayout.IntField("Sorting Order", sortingOrder);
            }
        }

        private void DrawStatsSection()
        {
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, "3. Stats & Companion Data", true, EditorStyles.foldoutHeader);
            if (!_foldoutStats) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                createBaseStatsSO = EditorGUILayout.Toggle(new GUIContent("Create BaseStats SO", "Generate an independent CharacterBaseStatSO asset alongside prefab"), createBaseStatsSO);

                if (!createBaseStatsSO)
                {
                    existingBaseStatsSO = (CharacterBaseStatSO)EditorGUILayout.ObjectField("Existing BaseStats SO", existingBaseStatsSO, typeof(CharacterBaseStatSO), false);
                }
                else
                {
                    EditorGUILayout.LabelField("Base Stats Parameters", EditorStyles.miniBoldLabel);
                    characterLevel = EditorGUILayout.IntSlider("Level", characterLevel, 1, 100);
                    characterRank = (CharacterRank)EditorGUILayout.EnumPopup("Rank", characterRank);

                    EditorGUILayout.Space(2);
                    maxLife = EditorGUILayout.FloatField("Max Life", maxLife);
                    maxMana = EditorGUILayout.FloatField("Max Mana", maxMana);
                    moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
                    damage = EditorGUILayout.FloatField("Base Damage", damage);
                    attackRange = EditorGUILayout.FloatField("Attack Range", attackRange);
                    attackTimeInterval = EditorGUILayout.FloatField("Attack Interval", attackTimeInterval);

                    EditorGUILayout.Space(2);
                    defaultPostAttackRecovery = EditorGUILayout.FloatField("Post-Attack Recovery (s)", defaultPostAttackRecovery);
                    defaultPostCastRecovery = EditorGUILayout.FloatField("Post-Cast Recovery (s)", defaultPostCastRecovery);
                    decisionInterval = EditorGUILayout.FloatField("AI Decision Interval (s)", decisionInterval);
                    intervalJitter = EditorGUILayout.FloatField("AI Interval Jitter (s)", intervalJitter);
                }
            }
        }

        private void DrawAISection()
        {
            _foldoutAI = EditorGUILayout.Foldout(_foldoutAI, "4. AI & Decision Making", true, EditorStyles.foldoutHeader);
            if (!_foldoutAI) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                hasAI = EditorGUILayout.Toggle("Enable AI Brain & Sensors", hasAI);
                controlMode = (ControlMode)EditorGUILayout.EnumPopup("Initial Control Mode", controlMode);
                agentStatus = (AgentStatus)EditorGUILayout.EnumPopup("Initial Agent Status", agentStatus);

                if (hasAI)
                {
                    EditorGUILayout.Space(4);
                    agentType = EditorGUILayout.TextField(new GUIContent("Agent Type", "GOAP Agent Type name (e.g. NormalAgent)"), agentType);

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Default Utility Goals", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < defaultGoals.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        defaultGoals[i] = (UtilityGoal)EditorGUILayout.ObjectField(defaultGoals[i], typeof(UtilityGoal), false);
                        if (GUILayout.Button("X", GUILayout.Width(22)))
                        {
                            defaultGoals.RemoveAt(i);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+ Add Goal", GUILayout.Height(18)))
                    {
                        defaultGoals.Add(null);
                    }
                }
            }
        }

        private void DrawSkillsSection()
        {
            _foldoutSkills = EditorGUILayout.Foldout(_foldoutSkills, "5. Combat & Skills", true, EditorStyles.foldoutHeader);
            if (!_foldoutSkills) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Starting Skill Prefabs", EditorStyles.miniBoldLabel);
                for (int i = 0; i < startingSkillPrefabs.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    startingSkillPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(startingSkillPrefabs[i], typeof(GameObject), false);
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        startingSkillPrefabs.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ Add Skill Prefab", GUILayout.Height(18)))
                {
                    startingSkillPrefabs.Add(null);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Attack & Spawn Helper Points", EditorStyles.miniBoldLabel);
                attackPointOffset = EditorGUILayout.Vector2Field("Attack Point Offset", attackPointOffset);
                attackGizmoRadius = EditorGUILayout.FloatField("Attack Gizmo Radius", attackGizmoRadius);

                EditorGUILayout.Space(2);
                skillSpawnOffset = EditorGUILayout.Vector2Field("Skill Spawn Offset", skillSpawnOffset);
                skillSpawnGizmoRadius = EditorGUILayout.FloatField("Skill Spawn Gizmo Radius", skillSpawnGizmoRadius);
            }
        }

        private void DrawCollidersSection()
        {
            _foldoutColliders = EditorGUILayout.Foldout(_foldoutColliders, "6. Hitbox & Colliders", true, EditorStyles.foldoutHeader);
            if (!_foldoutColliders) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Hitbox Layers", EditorStyles.miniBoldLabel);
                rootLayer = EditorGUILayout.LayerField("Root Layer", rootLayer);
                hitboxTriggerLayer = EditorGUILayout.LayerField("Hitbox Trigger Layer", hitboxTriggerLayer);
                hitboxCollisionLayer = EditorGUILayout.LayerField("Hitbox Collision Layer", hitboxCollisionLayer);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Trigger Collider (Damage Sensing)", EditorStyles.miniBoldLabel);
                triggerOffset = EditorGUILayout.Vector2Field("Trigger Offset", triggerOffset);
                triggerSize = EditorGUILayout.Vector2Field("Trigger Size", triggerSize);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Collision Collider (Physics Wall/Body)", EditorStyles.miniBoldLabel);
                collisionOffset = EditorGUILayout.Vector2Field("Collision Offset", collisionOffset);
                collisionSize = EditorGUILayout.Vector2Field("Collision Size", collisionSize);
            }
        }

        private void DrawSavingSection()
        {
            _foldoutSaving = EditorGUILayout.Foldout(_foldoutSaving, "7. Paths & Auto-Registration", true, EditorStyles.foldoutHeader);
            if (!_foldoutSaving) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                prefabFolderPath = EditorGUILayout.TextField("Prefab Folder", prefabFolderPath);
                statsFolderPath = EditorGUILayout.TextField("BaseStats Folder", statsFolderPath);

                EditorGUILayout.Space(2);
                autoRegisterInDatabase = EditorGUILayout.Toggle("Auto-Register in Database", autoRegisterInDatabase);
                if (autoRegisterInDatabase)
                {
                    characterDatabase = (CharacterDatabaseSO)EditorGUILayout.ObjectField("Character Database", characterDatabase, typeof(CharacterDatabaseSO), false);
                }
            }
        }

        private void CreateCharacterPrefab()
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                EditorUtility.DisplayDialog("Validation Error", "Character ID cannot be empty!", "OK");
                return;
            }

            string cleanId = characterId.Trim();
            string cleanDisplayName = string.IsNullOrWhiteSpace(characterDisplayName) ? cleanId : characterDisplayName.Trim();

            CreatorEditorUtils.EnsureFolderExists(prefabFolderPath);
            if (createBaseStatsSO)
            {
                CreatorEditorUtils.EnsureFolderExists(statsFolderPath);
            }

            string prefabPath = Path.Combine(prefabFolderPath, $"{cleanId}.prefab").Replace("\\", "/");
            if (File.Exists(prefabPath))
            {
                if (!EditorUtility.DisplayDialog("Prefab Exists", $"A prefab at '{prefabPath}' already exists. Overwrite it?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            try
            {
                EditorUtility.DisplayProgressBar("Character Creator", "Generating BaseStatSO...", 0.2f);

                CharacterBaseStatSO statAsset = existingBaseStatsSO;
                if (createBaseStatsSO)
                {
                    string statPath = Path.Combine(statsFolderPath, $"{cleanId}.asset").Replace("\\", "/");
                    statAsset = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(statPath);
                    if (statAsset == null)
                    {
                        statAsset = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
                        AssetDatabase.CreateAsset(statAsset, statPath);
                    }

                    statAsset.characterLevel = characterLevel;
                    statAsset.characterRank = characterRank;
                    statAsset.MaxLife = maxLife;
                    statAsset.MaxMana = maxMana;
                    statAsset.Movespeed = moveSpeed;
                    statAsset.Damage = damage;
                    statAsset.AttackRange = attackRange;
                    statAsset.AttackTimeInterval = attackTimeInterval;
                    statAsset.DefaultPostAttackRecovery = defaultPostAttackRecovery;
                    statAsset.DefaultPostCastRecovery = defaultPostCastRecovery;
                    statAsset.DecisionInterval = decisionInterval;
                    statAsset.IntervalJitter = intervalJitter;

                    EditorUtility.SetDirty(statAsset);
                    AssetDatabase.SaveAssets();
                }

                EditorUtility.DisplayProgressBar("Character Creator", "Constructing Hierarchy...", 0.5f);

                GameObject rootGo = new(cleanId);
                rootGo.tag = string.IsNullOrEmpty(rootTag) ? "Untagged" : rootTag;
                rootGo.layer = rootLayer;

                var rb = rootGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                var agent = rootGo.AddComponent<NavMeshAgent>();
                agent.radius = 0.06f;
                agent.height = 0.08f;
                agent.baseOffset = 0.145f;
                agent.speed = moveSpeed;
                agent.acceleration = 8f;
                agent.angularSpeed = 120f;
                agent.stoppingDistance = 0f;
                agent.areaMask = 9;

                var brain = rootGo.AddComponent<CharacterBrain>();
                var movement = rootGo.AddComponent<CharacterMovementController>();
                var statsManager = rootGo.AddComponent<CharacterStatsManager>();
                var combatController = rootGo.AddComponent<CharacterCombatController>();
                var identifier = rootGo.AddComponent<CharacterIdentifier>();
                var inventoryManager = rootGo.AddComponent<CharacterInventoryManager>();
                var combatPower = rootGo.AddComponent<CharacterCombatPower>();

                CreatorEditorUtils.SetPrivateField(statsManager, "baseStats", statAsset);
                CreatorEditorUtils.SetPrivateField(statsManager, "level", characterLevel);
                CreatorEditorUtils.SetPrivateField(identifier, "characterName", cleanId);
                CreatorEditorUtils.SetPrivateField(inventoryManager, "characterDisplayName", cleanDisplayName);
                CreatorEditorUtils.SetPrivateField(inventoryManager, "statsManager", statsManager);
                CreatorEditorUtils.SetPrivateField(combatPower, "statsManager", statsManager);
                CreatorEditorUtils.SetPrivateField(combatPower, "inventoryManager", inventoryManager);

                GameObject modelGo = new("Model");
                modelGo.transform.SetParent(rootGo.transform, false);
                modelGo.transform.localScale = modelScale;

                var spriteRenderer = modelGo.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingLayerName = sortingLayerName;
                spriteRenderer.sortingOrder = sortingOrder;

                var animator = modelGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;

                var animController = modelGo.AddComponent<AnimationController>();
                var flipRotator = modelGo.AddComponent<CharacterFlipRotator>();
                flipRotator.flipAxis = CharacterFlipRotator.Axis.X;
                CreatorEditorUtils.SetPrivateField(flipRotator, "rootTransform", rootGo.transform);

                var animEventController = modelGo.AddComponent<AnimationEventController>();

                GameObject modelHelperGo = new("Helper");
                modelHelperGo.transform.SetParent(modelGo.transform, false);

                GameObject attackPointGo = new("AttackPoint");
                attackPointGo.transform.SetParent(modelHelperGo.transform, false);
                attackPointGo.transform.localPosition = attackPointOffset;
                attackPointGo.transform.localScale = new Vector3(2f, 2f, 1f);

                var attackGizmo = attackPointGo.AddComponent<AttackPointGizmo>();
                CreatorEditorUtils.SetPrivateField(attackGizmo, "attackPoint", attackPointGo.transform);
                CreatorEditorUtils.SetPrivateField(attackGizmo, "attackRadius", attackGizmoRadius);

                GameObject hitboxGo = new("Hitbox");
                hitboxGo.transform.SetParent(rootGo.transform, false);

                GameObject triggerGo = new("Trigger");
                triggerGo.transform.SetParent(hitboxGo.transform, false);
                triggerGo.layer = hitboxTriggerLayer;
                var triggerCollider = triggerGo.AddComponent<BoxCollider2D>();
                triggerCollider.isTrigger = true;
                triggerCollider.offset = triggerOffset;
                triggerCollider.size = triggerSize;

                GameObject collisionGo = new("Collision");
                collisionGo.transform.SetParent(hitboxGo.transform, false);
                collisionGo.layer = hitboxCollisionLayer;
                var collisionCollider = collisionGo.AddComponent<BoxCollider2D>();
                collisionCollider.isTrigger = false;
                collisionCollider.offset = collisionOffset;
                collisionCollider.size = collisionSize;

                GameObject skillsGo = new("Skills");
                skillsGo.transform.SetParent(rootGo.transform, false);

                var skillController = skillsGo.AddComponent<CharacterSkillController>();

                GameObject skillListGo = new("SkillList");
                skillListGo.transform.SetParent(skillsGo.transform, false);

                if (startingSkillPrefabs != null)
                {
                    foreach (var skillPrefab in startingSkillPrefabs)
                    {
                        if (skillPrefab != null)
                        {
                            var skillInstance = (GameObject)PrefabUtility.InstantiatePrefab(skillPrefab, skillListGo.transform);
                            skillInstance.name = skillPrefab.name;
                        }
                    }
                }

                GameObject skillHelperGo = new("Helper");
                skillHelperGo.transform.SetParent(skillsGo.transform, false);

                GameObject spawnPointGo = new("SpawnPoint");
                spawnPointGo.transform.SetParent(skillHelperGo.transform, false);
                spawnPointGo.transform.localPosition = skillSpawnOffset;

                var spawnGizmo = spawnPointGo.AddComponent<SkillSpawnPointGizmo>();
                CreatorEditorUtils.SetPrivateField(spawnGizmo, "skillSpawnPoint", spawnPointGo.transform);
                CreatorEditorUtils.SetPrivateField(spawnGizmo, "spawnRadius", skillSpawnGizmoRadius);

                GameObject aiAugmentGo = null;
                BaseAI baseAI = null;
                VisionController visionController = null;
                SkillTargeting skillTargeting = null;

                if (hasAI)
                {
                    GameObject sensorGo = new("Sensor");
                    sensorGo.transform.SetParent(rootGo.transform, false);
                    sensorGo.transform.localPosition = new Vector3(0.1f, 0.05f, 0f);
                    sensorGo.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

                    var rangeSensor = sensorGo.AddComponent<RangeSensor2D>();
                    rangeSensor.Shape = RangeSensor2D.Shapes.Circle;
                    rangeSensor.Circle.Radius = 5f;
                    rangeSensor.DetectsOnLayers = LayerMask.GetMask("Character", "Default");
                    rangeSensor.EnableTagFilter = true;
                    rangeSensor.AllowedTags = new string[] { "Adventurer", "Monster" };
                    rangeSensor.IgnoreList.Add(rootGo);

                    var losSensor = sensorGo.AddComponent<LOSSensor2D>();
                    losSensor.NumberOfRays = 1;
                    losSensor.MinimumVisibility = 0.5f;
                    losSensor.MaxDistance = 5f;
                    losSensor.MaxViewAngle = 80f;
                    losSensor.LimitDistance = true;
                    losSensor.LimitViewAngle = true;

                    var soLos = new SerializedObject(losSensor);
                    var inputProp = soLos.FindProperty("inputSensor.value");
                    if (inputProp != null)
                    {
                        inputProp.objectReferenceValue = rangeSensor;
                        soLos.ApplyModifiedProperties();
                    }

                    skillTargeting = sensorGo.AddComponent<SkillTargeting>();
                    CreatorEditorUtils.SetPrivateField(skillTargeting, "sensor", losSensor);

                    visionController = sensorGo.AddComponent<VisionController>();
                    CreatorEditorUtils.SetPrivateField(visionController, "sensor", losSensor);

                    aiAugmentGo = new GameObject("NormalAI");
                    aiAugmentGo.tag = "AI_Augment";
                    aiAugmentGo.transform.SetParent(rootGo.transform, false);

                    var goapProvider = aiAugmentGo.AddComponent<GoapActionProvider>();
                    var agentBehaviour = aiAugmentGo.AddComponent<AgentBehaviour>();
                    var agentMoveBehaviour = aiAugmentGo.AddComponent<AgentMoveBehaviour>();
                    baseAI = aiAugmentGo.AddComponent<BaseAI>();
                    var throttler = aiAugmentGo.AddComponent<AIDecisionThrottler>();

                    CreatorEditorUtils.SetPrivateField(agentBehaviour, "<ActionProviderBase>k__BackingField", goapProvider);
                    CreatorEditorUtils.SetPrivateField(agentMoveBehaviour, "agent", baseAI);
                    CreatorEditorUtils.SetPrivateField(agentMoveBehaviour, "agentBehaviour", agentBehaviour);

                    CreatorEditorUtils.SetPrivateField(baseAI, "agent", agent);
                    CreatorEditorUtils.SetPrivateField(baseAI, "agentBehaviour", agentBehaviour);
                    CreatorEditorUtils.SetPrivateField(baseAI, "brain", brain);
                    CreatorEditorUtils.SetPrivateField(baseAI, "stats", statsManager);
                    CreatorEditorUtils.SetPrivateField(baseAI, "motionController", movement);
                    CreatorEditorUtils.SetPrivateField(baseAI, "skillController", skillController);
                    CreatorEditorUtils.SetPrivateField(baseAI, "vision", visionController);
                    CreatorEditorUtils.SetPrivateField(baseAI, "agentType", agentType);

                    var goalsList = new List<UtilityGoal>();
                    if (defaultGoals != null)
                    {
                        foreach (var g in defaultGoals)
                        {
                            if (g != null) goalsList.Add(g);
                        }
                    }
                    CreatorEditorUtils.SetPrivateField(baseAI, "goals", goalsList);

                    CreatorEditorUtils.SetPrivateField(throttler, "baseAI", baseAI);
                    CreatorEditorUtils.SetPrivateField(throttler, "characterBrain", brain);
                    CreatorEditorUtils.SetPrivateField(throttler, "statsManager", statsManager);
                    CreatorEditorUtils.SetPrivateField(throttler, "defaultDecisionInterval", decisionInterval);
                    CreatorEditorUtils.SetPrivateField(throttler, "defaultIntervalJitter", intervalJitter);

                    UnityEventTools.AddVoidPersistentListener(losSensor.OnDetected, baseAI.SenseEnemy);
                    UnityEventTools.AddVoidPersistentListener(losSensor.OnLostDetection, baseAI.LostEnemy);

                    CreatorEditorUtils.SetPrivateField(visionController, "brain", baseAI);
                }

                CreatorEditorUtils.SetPrivateField(brain, "controlMode", controlMode);
                CreatorEditorUtils.SetPrivateField(brain, "agentStatus", agentStatus);
                CreatorEditorUtils.SetPrivateField(brain, "aiAugment", aiAugmentGo);
                CreatorEditorUtils.SetPrivateField(brain, "statsManager", statsManager);
                CreatorEditorUtils.SetPrivateField(brain, "animController", animController);
                CreatorEditorUtils.SetPrivateField(brain, "skillController", skillController);

                CreatorEditorUtils.SetPrivateField(movement, "rb", rb);
                CreatorEditorUtils.SetPrivateField(movement, "brain", brain);
                CreatorEditorUtils.SetPrivateField(movement, "stats", statsManager);
                CreatorEditorUtils.SetPrivateField(movement, "dashSpeed", 20f);
                CreatorEditorUtils.SetPrivateField(movement, "dashDuration", 0.2f);
                CreatorEditorUtils.SetPrivateField(movement, "dashCooldown", 1f);

                CreatorEditorUtils.SetPrivateField(combatController, "brain", brain);
                CreatorEditorUtils.SetPrivateField(combatController, "stats", statsManager);
                CreatorEditorUtils.SetPrivateField(combatController, "attackPoint", attackPointGo.transform);

                CreatorEditorUtils.SetPrivateField(animController, "animator", animator);
                CreatorEditorUtils.SetPrivateField(animController, "sprite", spriteRenderer);
                CreatorEditorUtils.SetPrivateField(animController, "brain", brain);
                CreatorEditorUtils.SetPrivateField(animController, "movement", movement);
                CreatorEditorUtils.SetPrivateField(animController, "agent", agent);
                CreatorEditorUtils.SetPrivateField(animController, "flipRotator", flipRotator);

                CreatorEditorUtils.SetPrivateField(animEventController, "combatController", combatController);
                CreatorEditorUtils.SetPrivateField(animEventController, "skillController", skillController);
                CreatorEditorUtils.SetPrivateField(animEventController, "brain", brain);

                CreatorEditorUtils.SetPrivateField(skillController, "baseAI", baseAI);
                CreatorEditorUtils.SetPrivateField(skillController, "brain", brain);
                CreatorEditorUtils.SetPrivateField(skillController, "skillTargeting", skillTargeting);
                CreatorEditorUtils.SetPrivateField(skillController, "skillSpawnPoint", spawnPointGo);

                EditorUtility.DisplayProgressBar("Character Creator", "Saving Prefab Asset...", 0.8f);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
                UnityEngine.Object.DestroyImmediate(rootGo);

                if (autoRegisterInDatabase && characterDatabase != null)
                {
                    RegisterInDatabase(cleanId, cleanDisplayName, savedPrefab, statAsset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);

                Debug.Log($"<color=green>[CharacterCreatorTab]</color> Successfully created character prefab at '{prefabPath}'!");
                EditorUtility.DisplayDialog("Success", $"Character Prefab '{cleanId}' successfully created!\n\nPrefab: {prefabPath}\nBaseStats: {(statAsset != null ? AssetDatabase.GetAssetPath(statAsset) : "None")}", "Awesome!");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[CharacterCreatorTab] Failed to create character prefab: {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to create character prefab:\n{ex.Message}", "OK");
            }
        }

        private void RegisterInDatabase(string id, string name, GameObject prefab, CharacterBaseStatSO baseStats)
        {
            var serializedDb = new SerializedObject(characterDatabase);
            var charactersProp = serializedDb.FindProperty("characters");

            if (charactersProp == null) return;

            int existingIndex = -1;
            for (int i = 0; i < charactersProp.arraySize; i++)
            {
                var element = charactersProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                if (idProp != null && string.Equals(idProp.stringValue, id, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            SerializedProperty targetElement;
            if (existingIndex >= 0)
            {
                targetElement = charactersProp.GetArrayElementAtIndex(existingIndex);
            }
            else
            {
                charactersProp.InsertArrayElementAtIndex(charactersProp.arraySize);
                targetElement = charactersProp.GetArrayElementAtIndex(charactersProp.arraySize - 1);
            }

            var idField = targetElement.FindPropertyRelative("id");
            var nameField = targetElement.FindPropertyRelative("characterName");
            var prefabField = targetElement.FindPropertyRelative("prefab");
            var statsField = targetElement.FindPropertyRelative("baseStats");

            if (idField != null) idField.stringValue = id;
            if (nameField != null) nameField.stringValue = name;
            if (prefabField != null) prefabField.objectReferenceValue = prefab;
            if (statsField != null) statsField.objectReferenceValue = baseStats;

            serializedDb.ApplyModifiedProperties();
            EditorUtility.SetDirty(characterDatabase);
            Debug.Log($"[CharacterCreatorTab] Registered/Updated '{id}' in CharacterDatabaseSO.");
        }
    }
}
