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

namespace SBGames.Editor.Characters
{
    public enum CharacterArchetype
    {
        NormalMonster,
        EliteMonster,
        BossMonster,
        Adventurer
    }

    public class CharacterCreatorWizard : EditorWindow
    {
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
        [SerializeField] private List<GameObject> startingSkillPrefabs = new();
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

        [MenuItem("SBGames/Characters/Character Creator Wizard", priority = 10)]
        [MenuItem("Assets/Create/SBGames/Character Creator Wizard", priority = 50)]
        public static void OpenWindow()
        {
            var window = GetWindow<CharacterCreatorWizard>("Character Creator");
            window.minSize = new Vector2(460, 680);
            window.Show();
        }

        private void OnEnable()
        {
            AutoFindDefaults();
        }

        private void AutoFindDefaults()
        {
            // Auto-locate CharacterDatabaseSO if not already set
            if (characterDatabase == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:CharacterDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    characterDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(path);
                }
            }

            // Auto-locate default GOAP goals if list is empty
            if (defaultGoals.Count == 0)
            {
                var killGoal = AssetDatabase.LoadAssetAtPath<UtilityGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultKillEnemyGoal.asset");
                var wanderGoal = AssetDatabase.LoadAssetAtPath<UtilityGoal>("Assets/ScriptableObjects/UtilityGoal/Default/DefaultWanderGoal.asset");
                if (killGoal != null) defaultGoals.Add(killGoal);
                if (wanderGoal != null) defaultGoals.Add(wanderGoal);
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
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

            DrawCreateButton();
            EditorGUILayout.Space(12);

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("⚔️ Character Prefab Creator Wizard", titleStyle);
                EditorGUILayout.LabelField("Procedurally construct character prefabs with full wiring based on MONSTER_N001 blueprint.", EditorStyles.centeredGreyMiniLabel);
            }
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
                    moveSpeed = 3.5f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Monsters/Boss";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Monster/Boss";
                    break;

                case CharacterArchetype.Adventurer:
                    rootTag = "Adventurer";
                    rootLayer = 6;
                    controlMode = ControlMode.Player;
                    agentStatus = AgentStatus.Active;
                    hasAI = false;
                    agentType = "AdventurerAgent";
                    characterRank = CharacterRank.C;
                    maxLife = 150f;
                    damage = 15f;
                    moveSpeed = 5f;
                    prefabFolderPath = "Assets/Prefabs/Characters/Adventurer";
                    statsFolderPath = "Assets/ScriptableObjects/Character/Adventurer";
                    break;
            }
        }

        private void DrawBasicSection()
        {
            _foldoutBasic = EditorGUILayout.Foldout(_foldoutBasic, "1. Basic Identity & Faction", true, EditorStyles.foldoutHeader);
            if (!_foldoutBasic) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                characterId = EditorGUILayout.TextField(new GUIContent("Character ID", "Unique identifier code (e.g. MONSTER_N003, PLAYER_MAGE)"), characterId);
                characterDisplayName = EditorGUILayout.TextField(new GUIContent("Display Name", "In-game display name (e.g. Purple Goblin)"), characterDisplayName);

                EditorGUILayout.Space(2);
                rootTag = EditorGUILayout.TagField(new GUIContent("Root Tag", "Faction / classification tag"), rootTag);
                rootLayer = EditorGUILayout.LayerField(new GUIContent("Root Layer", "Default Character layer"), rootLayer);
            }
        }

        private void DrawVisualsSection()
        {
            _foldoutVisuals = EditorGUILayout.Foldout(_foldoutVisuals, "2. Visuals & Animations", true, EditorStyles.foldoutHeader);
            if (!_foldoutVisuals) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                sprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Default Sprite", "Initial sprite for SpriteRenderer"), sprite, typeof(Sprite), false);
                animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(new GUIContent("Animator Controller", "Animation controller asset"), animatorController, typeof(RuntimeAnimatorController), false);
                modelScale = EditorGUILayout.Vector3Field(new GUIContent("Model Scale", "Scale applied to the Model child (MONSTER_N001 uses 5, 5, 1)"), modelScale);
                sortingLayerName = EditorGUILayout.TextField(new GUIContent("Sorting Layer", "Sprite sorting layer name"), sortingLayerName);
                sortingOrder = EditorGUILayout.IntField(new GUIContent("Order in Layer", "Initial sprite sorting order"), sortingOrder);
            }
        }

        private void DrawStatsSection()
        {
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, "3. Base Stats & Companion SO", true, EditorStyles.foldoutHeader);
            if (!_foldoutStats) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                createBaseStatsSO = EditorGUILayout.Toggle(new GUIContent("Auto-Create BaseStatSO", "Generate a new CharacterBaseStatSO asset matching Character ID"), createBaseStatsSO);

                if (!createBaseStatsSO)
                {
                    existingBaseStatsSO = (CharacterBaseStatSO)EditorGUILayout.ObjectField(new GUIContent("Existing BaseStatSO", "Assign pre-existing CharacterBaseStatSO asset"), existingBaseStatsSO, typeof(CharacterBaseStatSO), false);
                }
                else
                {
                    characterLevel = EditorGUILayout.IntSlider("Initial Level", characterLevel, 1, 100);
                    characterRank = (CharacterRank)EditorGUILayout.EnumPopup("Character Rank", characterRank);
                    maxLife = EditorGUILayout.FloatField("Max Life", maxLife);
                    maxMana = EditorGUILayout.FloatField("Max Mana", maxMana);
                    moveSpeed = EditorGUILayout.FloatField("Move Speed", moveSpeed);
                    damage = EditorGUILayout.FloatField("Damage", damage);
                    attackRange = EditorGUILayout.FloatField("Attack Range", attackRange);
                    attackTimeInterval = EditorGUILayout.FloatField("Attack Interval (s)", attackTimeInterval);

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Recovery & AI Throttling", EditorStyles.miniBoldLabel);
                    defaultPostAttackRecovery = EditorGUILayout.FloatField("Post-Attack Recovery (s)", defaultPostAttackRecovery);
                    defaultPostCastRecovery = EditorGUILayout.FloatField("Post-Cast Recovery (s)", defaultPostCastRecovery);
                    decisionInterval = EditorGUILayout.FloatField("Decision Interval (s)", decisionInterval);
                    intervalJitter = EditorGUILayout.Slider("Interval Jitter (s)", intervalJitter, 0f, 0.5f);
                }
            }
        }

        private void DrawAISection()
        {
            _foldoutAI = EditorGUILayout.Foldout(_foldoutAI, "4. AI Augment & GOAP Setup", true, EditorStyles.foldoutHeader);
            if (!_foldoutAI) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                hasAI = EditorGUILayout.Toggle(new GUIContent("Include AI Augment", "Add GOAP ActionProvider, AgentBehaviour, BaseAI, AIDecisionThrottler, and Sensors"), hasAI);
                controlMode = (ControlMode)EditorGUILayout.EnumPopup("Control Mode", controlMode);
                agentStatus = (AgentStatus)EditorGUILayout.EnumPopup("Agent Status", agentStatus);

                if (hasAI)
                {
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
            _foldoutSkills = EditorGUILayout.Foldout(_foldoutSkills, "5. Combat & Skills Setup", true, EditorStyles.foldoutHeader);
            if (!_foldoutSkills) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                attackPointOffset = EditorGUILayout.Vector2Field("AttackPoint Offset", attackPointOffset);
                attackGizmoRadius = EditorGUILayout.FloatField("Attack Radius Gizmo", attackGizmoRadius);

                EditorGUILayout.Space(2);
                skillSpawnOffset = EditorGUILayout.Vector2Field("SkillSpawn Offset", skillSpawnOffset);
                skillSpawnGizmoRadius = EditorGUILayout.FloatField("SkillSpawn Radius Gizmo", skillSpawnGizmoRadius);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Starting Skill Prefabs (SkillList)", EditorStyles.miniBoldLabel);
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
            }
        }

        private void DrawCollidersSection()
        {
            _foldoutColliders = EditorGUILayout.Foldout(_foldoutColliders, "6. Hitbox & Colliders (Advanced)", true, EditorStyles.foldoutHeader);
            if (!_foldoutColliders) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                hitboxTriggerLayer = EditorGUILayout.LayerField("Trigger Layer (7)", hitboxTriggerLayer);
                triggerOffset = EditorGUILayout.Vector2Field("Trigger Offset", triggerOffset);
                triggerSize = EditorGUILayout.Vector2Field("Trigger Box Size", triggerSize);

                EditorGUILayout.Space(4);
                hitboxCollisionLayer = EditorGUILayout.LayerField("Collision Layer (8)", hitboxCollisionLayer);
                collisionOffset = EditorGUILayout.Vector2Field("Collision Offset", collisionOffset);
                collisionSize = EditorGUILayout.Vector2Field("Collision Box Size", collisionSize);
            }
        }

        private void DrawSavingSection()
        {
            _foldoutSaving = EditorGUILayout.Foldout(_foldoutSaving, "7. Output Paths & Database Registration", true, EditorStyles.foldoutHeader);
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

        private void DrawCreateButton()
        {
            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("🚀 Generate Character Prefab", btnStyle))
            {
                CreateCharacterPrefab();
            }
            GUI.backgroundColor = Color.white;
        }

        // ----------------------------------------------------
        // Generation Logic
        // ----------------------------------------------------
        private void CreateCharacterPrefab()
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                EditorUtility.DisplayDialog("Validation Error", "Character ID cannot be empty!", "OK");
                return;
            }

            string cleanId = characterId.Trim();
            string cleanDisplayName = string.IsNullOrWhiteSpace(characterDisplayName) ? cleanId : characterDisplayName.Trim();

            // Ensure destination directories exist
            EnsureFolderExists(prefabFolderPath);
            if (createBaseStatsSO)
            {
                EnsureFolderExists(statsFolderPath);
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

                // 1. Create or resolve CharacterBaseStatSO
                CharacterBaseStatSO statAsset = existingBaseStatsSO;
                if (createBaseStatsSO)
                {
                    string statPath = Path.Combine(statsFolderPath, $"{cleanId}.asset").Replace("\\", "/");
                    statAsset = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(statPath);
                    if (statAsset == null)
                    {
                        statAsset = CreateInstance<CharacterBaseStatSO>();
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

                // 2. Build root GameObject
                GameObject rootGo = new(cleanId);
                rootGo.tag = string.IsNullOrEmpty(rootTag) ? "Untagged" : rootTag;
                rootGo.layer = rootLayer;

                // Root Rigidbody2D
                var rb = rootGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;

                // Root NavMeshAgent
                var agent = rootGo.AddComponent<NavMeshAgent>();
                agent.radius = 0.06f;
                agent.height = 0.08f;
                agent.baseOffset = 0.145f;
                agent.speed = moveSpeed;
                agent.acceleration = 8f;
                agent.angularSpeed = 120f;
                agent.stoppingDistance = 0f;
                agent.areaMask = 9; // Walkable + Custom area

                // Root Controllers
                var brain = rootGo.AddComponent<CharacterBrain>();
                var movement = rootGo.AddComponent<CharacterMovementController>();
                var statsManager = rootGo.AddComponent<CharacterStatsManager>();
                var combatController = rootGo.AddComponent<CharacterCombatController>();
                var identifier = rootGo.AddComponent<CharacterIdentifier>();
                var inventoryManager = rootGo.AddComponent<CharacterInventoryManager>();
                var combatPower = rootGo.AddComponent<CharacterCombatPower>();

                // Configure StatsManager
                SetPrivateField(statsManager, "baseStats", statAsset);
                SetPrivateField(statsManager, "level", characterLevel);

                // Configure Identifier
                SetPrivateField(identifier, "characterName", cleanId);

                // Configure InventoryManager
                SetPrivateField(inventoryManager, "characterDisplayName", cleanDisplayName);
                SetPrivateField(inventoryManager, "statsManager", statsManager);

                // Configure CombatPower
                SetPrivateField(combatPower, "statsManager", statsManager);
                SetPrivateField(combatPower, "inventoryManager", inventoryManager);

                // 3. Construct Model child
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
                SetPrivateField(flipRotator, "rootTransform", rootGo.transform);

                var animEventController = modelGo.AddComponent<AnimationEventController>();

                // Model/Helper/AttackPoint
                GameObject modelHelperGo = new("Helper");
                modelHelperGo.transform.SetParent(modelGo.transform, false);

                GameObject attackPointGo = new("AttackPoint");
                attackPointGo.transform.SetParent(modelHelperGo.transform, false);
                attackPointGo.transform.localPosition = attackPointOffset;
                attackPointGo.transform.localScale = new Vector3(2f, 2f, 1f);

                var attackGizmo = attackPointGo.AddComponent<AttackPointGizmo>();
                SetPrivateField(attackGizmo, "attackPoint", attackPointGo.transform);
                SetPrivateField(attackGizmo, "attackRadius", attackGizmoRadius);

                // 4. Construct Hitbox child
                GameObject hitboxGo = new("Hitbox");
                hitboxGo.transform.SetParent(rootGo.transform, false);

                // Hitbox/Trigger
                GameObject triggerGo = new("Trigger");
                triggerGo.transform.SetParent(hitboxGo.transform, false);
                triggerGo.layer = hitboxTriggerLayer;
                var triggerCollider = triggerGo.AddComponent<BoxCollider2D>();
                triggerCollider.isTrigger = true;
                triggerCollider.offset = triggerOffset;
                triggerCollider.size = triggerSize;

                // Hitbox/Collision
                GameObject collisionGo = new("Collision");
                collisionGo.transform.SetParent(hitboxGo.transform, false);
                collisionGo.layer = hitboxCollisionLayer;
                var collisionCollider = collisionGo.AddComponent<BoxCollider2D>();
                collisionCollider.isTrigger = false;
                collisionCollider.offset = collisionOffset;
                collisionCollider.size = collisionSize;

                // 5. Construct Skills child
                GameObject skillsGo = new("Skills");
                skillsGo.transform.SetParent(rootGo.transform, false);

                var skillController = skillsGo.AddComponent<CharacterSkillController>();

                GameObject skillListGo = new("SkillList");
                skillListGo.transform.SetParent(skillsGo.transform, false);

                // Instantiate initial skill prefabs into SkillList
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
                SetPrivateField(spawnGizmo, "skillSpawnPoint", spawnPointGo.transform);
                SetPrivateField(spawnGizmo, "spawnRadius", skillSpawnGizmoRadius);

                // 6. Construct AI & Sensors (if enabled)
                GameObject aiAugmentGo = null;
                BaseAI baseAI = null;
                VisionController visionController = null;
                SkillTargeting skillTargeting = null;

                if (hasAI)
                {
                    // Sensor Child
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

                    // Link RangeSensor -> LOSSensor
                    var soLos = new SerializedObject(losSensor);
                    var inputProp = soLos.FindProperty("inputSensor.value");
                    if (inputProp != null)
                    {
                        inputProp.objectReferenceValue = rangeSensor;
                        soLos.ApplyModifiedProperties();
                    }

                    skillTargeting = sensorGo.AddComponent<SkillTargeting>();
                    SetPrivateField(skillTargeting, "sensor", losSensor);

                    visionController = sensorGo.AddComponent<VisionController>();
                    SetPrivateField(visionController, "sensor", losSensor);

                    // NormalAI Child
                    aiAugmentGo = new GameObject("NormalAI");
                    aiAugmentGo.tag = "AI_Augment";
                    aiAugmentGo.transform.SetParent(rootGo.transform, false);

                    var goapProvider = aiAugmentGo.AddComponent<GoapActionProvider>();
                    var agentBehaviour = aiAugmentGo.AddComponent<AgentBehaviour>();
                    var agentMoveBehaviour = aiAugmentGo.AddComponent<AgentMoveBehaviour>();
                    baseAI = aiAugmentGo.AddComponent<BaseAI>();
                    var throttler = aiAugmentGo.AddComponent<AIDecisionThrottler>();

                    // Configure AgentBehaviour
                    SetPrivateField(agentBehaviour, "<ActionProviderBase>k__BackingField", goapProvider);

                    // Configure AgentMoveBehaviour
                    SetPrivateField(agentMoveBehaviour, "agent", baseAI);
                    SetPrivateField(agentMoveBehaviour, "agentBehaviour", agentBehaviour);

                    // Configure BaseAI
                    SetPrivateField(baseAI, "agent", agent);
                    SetPrivateField(baseAI, "agentBehaviour", agentBehaviour);
                    SetPrivateField(baseAI, "brain", brain);
                    SetPrivateField(baseAI, "stats", statsManager);
                    SetPrivateField(baseAI, "motionController", movement);
                    SetPrivateField(baseAI, "skillController", skillController);
                    SetPrivateField(baseAI, "vision", visionController);
                    SetPrivateField(baseAI, "agentType", agentType);

                    var goalsList = new List<UtilityGoal>();
                    if (defaultGoals != null)
                    {
                        foreach (var g in defaultGoals)
                        {
                            if (g != null) goalsList.Add(g);
                        }
                    }
                    SetPrivateField(baseAI, "goals", goalsList);

                    // Configure AIDecisionThrottler
                    SetPrivateField(throttler, "baseAI", baseAI);
                    SetPrivateField(throttler, "characterBrain", brain);
                    SetPrivateField(throttler, "statsManager", statsManager);
                    SetPrivateField(throttler, "defaultDecisionInterval", decisionInterval);
                    SetPrivateField(throttler, "defaultIntervalJitter", intervalJitter);

                    // Link Sensor Events -> BaseAI
                    UnityEventTools.AddVoidPersistentListener(losSensor.OnDetected, baseAI.SenseEnemy);
                    UnityEventTools.AddVoidPersistentListener(losSensor.OnLostDetection, baseAI.LostEnemy);

                    // Link VisionController -> BaseAI
                    SetPrivateField(visionController, "brain", baseAI);
                }

                // 7. Wire Remaining Cross-References
                // CharacterBrain
                SetPrivateField(brain, "controlMode", controlMode);
                SetPrivateField(brain, "agentStatus", agentStatus);
                SetPrivateField(brain, "aiAugment", aiAugmentGo);
                SetPrivateField(brain, "statsManager", statsManager);
                SetPrivateField(brain, "animController", animController);
                SetPrivateField(brain, "skillController", skillController);

                // CharacterMovementController
                SetPrivateField(movement, "rb", rb);
                SetPrivateField(movement, "brain", brain);
                SetPrivateField(movement, "stats", statsManager);
                SetPrivateField(movement, "dashSpeed", 20f);
                SetPrivateField(movement, "dashDuration", 0.2f);
                SetPrivateField(movement, "dashCooldown", 1f);

                // CharacterCombatController
                SetPrivateField(combatController, "brain", brain);
                SetPrivateField(combatController, "stats", statsManager);
                SetPrivateField(combatController, "attackPoint", attackPointGo.transform);

                // AnimationController
                SetPrivateField(animController, "animator", animator);
                SetPrivateField(animController, "sprite", spriteRenderer);
                SetPrivateField(animController, "brain", brain);
                SetPrivateField(animController, "movement", movement);
                SetPrivateField(animController, "agent", agent);
                SetPrivateField(animController, "flipRotator", flipRotator);

                // AnimationEventController
                SetPrivateField(animEventController, "combatController", combatController);
                SetPrivateField(animEventController, "skillController", skillController);
                SetPrivateField(animEventController, "brain", brain);

                // CharacterSkillController
                SetPrivateField(skillController, "baseAI", baseAI);
                SetPrivateField(skillController, "brain", brain);
                SetPrivateField(skillController, "skillTargeting", skillTargeting);
                SetPrivateField(skillController, "skillSpawnPoint", spawnPointGo);

                EditorUtility.DisplayProgressBar("Character Creator", "Saving Prefab Asset...", 0.8f);

                // 8. Save Prefab
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
                DestroyImmediate(rootGo);

                // 9. Register in CharacterDatabaseSO (if enabled)
                if (autoRegisterInDatabase && characterDatabase != null)
                {
                    RegisterInDatabase(cleanId, cleanDisplayName, savedPrefab, statAsset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                // 10. Ping & Select in Project Window
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);

                Debug.Log($"<color=green>[CharacterCreatorWizard]</color> Successfully created character prefab at '{prefabPath}'!");
                EditorUtility.DisplayDialog("Success", $"Character Prefab '{cleanId}' successfully created!\n\nPrefab: {prefabPath}\nBaseStats: {(statAsset != null ? AssetDatabase.GetAssetPath(statAsset) : "None")}", "Awesome!");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[CharacterCreatorWizard] Failed to create character prefab: {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to create character prefab:\n{ex.Message}", "OK");
            }
        }

        private void RegisterInDatabase(string id, string name, GameObject prefab, CharacterBaseStatSO baseStats)
        {
            var serializedDb = new SerializedObject(characterDatabase);
            var charactersProp = serializedDb.FindProperty("characters");

            if (charactersProp == null) return;

            // Check if entry with this ID already exists
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
            Debug.Log($"[CharacterCreatorWizard] Registered/Updated '{id}' in CharacterDatabaseSO.");
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;

            if (target is UnityEngine.Object unityObj)
            {
                var so = new SerializedObject(unityObj);
                var prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    if (value is UnityEngine.Object objVal)
                    {
                        prop.objectReferenceValue = objVal;
                    }
                    else if (value is string strVal)
                    {
                        prop.stringValue = strVal;
                    }
                    else if (value is int intVal)
                    {
                        prop.intValue = intVal;
                    }
                    else if (value is float floatVal)
                    {
                        prop.floatValue = floatVal;
                    }
                    else if (value is bool boolVal)
                    {
                        prop.boolValue = boolVal;
                    }
                    else if (value is Enum enumVal)
                    {
                        prop.enumValueIndex = Convert.ToInt32(enumVal);
                    }
                    else if (value is Vector2 v2Val)
                    {
                        prop.vector2Value = v2Val;
                    }
                    else if (value is Vector3 v3Val)
                    {
                        prop.vector3Value = v3Val;
                    }
                    so.ApplyModifiedProperties();
                    return;
                }
            }

            // Fallback to reflection
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            string[] parts = folderPath.Replace("\\", "/").Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
