using System;
using System.Collections.Generic;
using System.IO;
using Micosmo.SensorToolkit;
using SBGames.Database;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Creator.Tabs
{
    public enum SkillElement
    {
        None,
        Fire,
        Lightning,
        Water,
        Wind
    }

    public enum SkillArchetype
    {
        Projectile,
        ChainProjectile,
        [InspectorName("AOE Burst")]
        AoEBurst,
        Beam,
        MeleeDefault
    }

    public enum SkillCasterSpawnType
    {
        AtCaster = 0,
        AtCasterSpawnPoint = 1,
        AtTarget = 2
    }

    [Serializable]
    public class SkillCreatorTab : ICreatorTab
    {
        [NonSerialized] private SBGamesCreatorWindow _hostWindow;

        public string Title => "✨ Skill";

        // ----------------------------------------------------
        // 1. Identity
        // ----------------------------------------------------
        [SerializeField] private SkillElement element = SkillElement.Fire;
        [SerializeField] private string skillID = "SKILL_F002";
        [SerializeField] private string skillName = "Fireball";
        [SerializeField] private string description = "Launches a fiery projectile that burns targets.";
        [SerializeField] private Sprite icon;

        // ----------------------------------------------------
        // Archetype Preset
        // ----------------------------------------------------
        [SerializeField] private SkillArchetype archetype = SkillArchetype.Projectile;

        // ----------------------------------------------------
        // 2. Stats
        // ----------------------------------------------------
        [SerializeField] private SkillType skillType = SkillType.Projectile;
        [SerializeField] private float baseDamage = 15f;
        [SerializeField] private DamageType damageType = DamageType.Magical;
        [SerializeField] private float baseManaCost = 10f;
        [SerializeField] private float castRange = 0f;
        [SerializeField] private SkillData.SkillTargetFilter targetFilter = SkillData.SkillTargetFilter.EnemiesOnly;

        [SerializeField] private bool useCustomRecoveryDuration = true;
        [SerializeField] private float postCastRecoveryDuration = 0.35f;

        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float projectileLifetime = 5f;
        [SerializeField] private SkillData.ProjectileForward projectileForward = SkillData.ProjectileForward.Up;
        [SerializeField] private float projectileRotationOffset = 0f;

        [SerializeField] private bool chain = false;
        [SerializeField] private int maxChainTargets = 0;
        [SerializeField] private float chainDamageMultiplier = 0.7f;

        [SerializeField] private bool area = false;
        [SerializeField] private float areaRadius = 0f;

        [SerializeField] private float beamTickInterval = 0.1f;

        // ----------------------------------------------------
        // 3. On-Hit Status Effects
        // ----------------------------------------------------
        [SerializeField] public List<StatusEffectApplication> statusEffects = new();

        // ----------------------------------------------------
        // 4. FX Visuals
        // ----------------------------------------------------
        [SerializeField] private Sprite fxSprite;
        [SerializeField] private RuntimeAnimatorController fxAnimatorController;
        [SerializeField] private float hitboxRadius = 0.25f;
        [SerializeField] private LayerMask sensorDetectionLayers = 1 << 6; // Layer 6 Character
        [SerializeField] private float sensorRadius = 5f;

        // ----------------------------------------------------
        // 5. Caster Prefab Settings
        // ----------------------------------------------------
        [SerializeField] private SkillCasterSpawnType spawnType = SkillCasterSpawnType.AtCasterSpawnPoint;
        [SerializeField] private bool requireTarget = false;
        [SerializeField] private float forwardSpawnOffset = 0.6f;
        [SerializeField] private float baseCooldown = 1.0f;

        // ----------------------------------------------------
        // 6. AI Utility
        // ----------------------------------------------------
        [SerializeField] private bool createUtilitySkill = true;
        [SerializeField] private float baseWeight = 1.0f;
        [SerializeField] private bool checkHealthThreshold = false;
        [SerializeField] private float minHealthPercent = 0f;
        [SerializeField] private float maxHealthPercent = 100f;

        // ----------------------------------------------------
        // 7. Output Paths & Database Registration
        // ----------------------------------------------------
        [SerializeField] private string skillDataPath = "Assets/ScriptableObjects/Skill/Fire/SKILL_F002.asset";
        [SerializeField] private string casterPrefabPath = "Assets/Prefabs/Skills/SKILL_F002.prefab";
        [SerializeField] private string fxPrefabPath = "Assets/Prefabs/Skills/SkillEffects/SKILLFX_F002.prefab";
        [SerializeField] private string utilitySkillPath = "Assets/ScriptableObjects/UtilitySkill/Fire/Utility_SKILL_F002.asset";

        [SerializeField] private bool autoRegisterInDatabases = true;
        [SerializeField] private SkillDatabaseSO skillDatabase;
        [SerializeField] private UtilitySkillDatabaseSO utilitySkillDatabase;

        // Foldout States
        private bool _foldoutIdentity = true;
        private bool _foldoutStats = true;
        private bool _foldoutStatusEffects = true;
        private bool _foldoutFX = true;
        private bool _foldoutCaster = true;
        private bool _foldoutUtility = true;
        private bool _foldoutPaths = true;

        private Vector2 _scrollPos;

        // Post-generation feedback
        [NonSerialized] private GameObject _lastGeneratedCasterPrefab;
        [NonSerialized] private bool _showSuccessPanel = false;

        public void Initialize(SBGamesCreatorWindow window)
        {
            _hostWindow = window;
        }

        public void OnEnable()
        {
            AutoFindDatabases();
            UpdatePathsAndId(false);
        }

        private void AutoFindDatabases()
        {
            if (skillDatabase == null)
            {
                skillDatabase = CreatorEditorUtils.FindFirstAsset<SkillDatabaseSO>();
            }
            if (utilitySkillDatabase == null)
            {
                utilitySkillDatabase = CreatorEditorUtils.FindFirstAsset<UtilitySkillDatabaseSO>();
            }
        }

        public void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            CreatorEditorUtils.DrawHeader("✨ Skill & FX Creator", "Procedurally generate SkillData, FX Prefabs, Caster Prefabs, and AI UtilitySkill assets.");
            EditorGUILayout.Space(8);

            DrawPresetSelector();
            EditorGUILayout.Space(6);

            DrawIdentitySection();
            EditorGUILayout.Space(6);

            DrawStatsSection();
            EditorGUILayout.Space(6);

            DrawStatusEffectsSection();
            EditorGUILayout.Space(6);

            DrawFXSection();
            EditorGUILayout.Space(6);

            DrawCasterSection();
            EditorGUILayout.Space(6);

            DrawAIUtilitySection();
            EditorGUILayout.Space(6);

            DrawPathsSection();
            EditorGUILayout.Space(12);

            CreatorEditorUtils.DrawGenerateButton("🚀 Generate Skill Assets", Generate, new Color(0.85f, 0.45f, 0.2f));
            EditorGUILayout.Space(8);

            DrawSuccessPanel();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Archetype Preset", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                archetype = (SkillArchetype)EditorGUILayout.EnumPopup("Preset", archetype);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyArchetypePreset(archetype);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Projectile", EditorStyles.miniButtonLeft)) { archetype = SkillArchetype.Projectile; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Chain Projectile", EditorStyles.miniButtonMid)) { archetype = SkillArchetype.ChainProjectile; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("AoE Burst", EditorStyles.miniButtonMid)) { archetype = SkillArchetype.AoEBurst; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Beam", EditorStyles.miniButtonMid)) { archetype = SkillArchetype.Beam; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Melee / Default", EditorStyles.miniButtonRight)) { archetype = SkillArchetype.MeleeDefault; ApplyArchetypePreset(archetype); }
                EditorGUILayout.EndHorizontal();
            }
        }

        public void ApplyArchetypePreset(SkillArchetype type)
        {
            archetype = type;
            switch (type)
            {
                case SkillArchetype.Projectile:
                    skillType = SkillType.Projectile;
                    projectileSpeed = 10f;
                    projectileLifetime = 5f;
                    chain = false;
                    maxChainTargets = 0;
                    area = false;
                    areaRadius = 0f;
                    hitboxRadius = 0.25f;
                    spawnType = SkillCasterSpawnType.AtCasterSpawnPoint;
                    requireTarget = false;
                    break;

                case SkillArchetype.ChainProjectile:
                    skillType = SkillType.Projectile;
                    projectileSpeed = 10f;
                    projectileLifetime = 5f;
                    chain = true;
                    maxChainTargets = 3;
                    chainDamageMultiplier = 0.7f;
                    area = false;
                    areaRadius = 0f;
                    hitboxRadius = 0.25f;
                    sensorRadius = 7f;
                    spawnType = SkillCasterSpawnType.AtCasterSpawnPoint;
                    requireTarget = true;
                    break;

                case SkillArchetype.AoEBurst:
                    skillType = SkillType.Area;
                    area = true;
                    areaRadius = 3f;
                    chain = false;
                    maxChainTargets = 0;
                    hitboxRadius = 3f;
                    sensorRadius = 3f;
                    spawnType = SkillCasterSpawnType.AtTarget;
                    requireTarget = true;
                    break;

                case SkillArchetype.Beam:
                    skillType = SkillType.Default;
                    beamTickInterval = 0.1f;
                    chain = false;
                    maxChainTargets = 0;
                    area = false;
                    areaRadius = 0f;
                    hitboxRadius = 0.35f;
                    spawnType = SkillCasterSpawnType.AtCasterSpawnPoint;
                    requireTarget = false;
                    break;

                case SkillArchetype.MeleeDefault:
                    skillType = SkillType.Default;
                    chain = false;
                    maxChainTargets = 0;
                    area = false;
                    areaRadius = 0f;
                    hitboxRadius = 0.4f;
                    spawnType = SkillCasterSpawnType.AtCaster;
                    requireTarget = false;
                    break;
            }
        }

        private void DrawIdentitySection()
        {
            _foldoutIdentity = EditorGUILayout.Foldout(_foldoutIdentity, "1. Identity & Naming", true, EditorStyles.foldoutHeader);
            if (!_foldoutIdentity) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                element = (SkillElement)EditorGUILayout.EnumPopup("Element", element);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePathsAndId(true);
                }

                EditorGUILayout.BeginHorizontal();
                skillID = EditorGUILayout.TextField(new GUIContent("Skill ID", "Format: SKILL_<P>NNN"), skillID);
                if (GUILayout.Button("Next free", GUILayout.Width(80)))
                {
                    UpdatePathsAndId(true);
                }
                EditorGUILayout.EndHorizontal();

                skillName = EditorGUILayout.TextField("Skill Name", skillName);
                description = EditorGUILayout.TextField("Description", description);
                icon = (Sprite)EditorGUILayout.ObjectField("Icon", icon, typeof(Sprite), false);
            }
        }

        private string GetElementPrefix(SkillElement elem)
        {
            return elem switch
            {
                SkillElement.Fire => "F",
                SkillElement.Lightning => "L",
                SkillElement.Water => "WA",
                SkillElement.Wind => "WI",
                _ => ""
            };
        }

        private string GetElementFolder(SkillElement elem)
        {
            return elem == SkillElement.None ? "Default" : elem.ToString();
        }

        public void UpdatePathsAndId(bool forceNextFree)
        {
            string prefix = GetElementPrefix(element);
            string folder = GetElementFolder(element);
            string searchFolder = $"Assets/ScriptableObjects/Skill/{folder}";

            if (forceNextFree || string.IsNullOrEmpty(skillID))
            {
                skillID = CreatorEditorUtils.NextFreeId($"SKILL_{prefix}", searchFolder, 3);
            }

            string numPart = skillID.StartsWith("SKILL_", StringComparison.OrdinalIgnoreCase)
                ? skillID.Substring("SKILL_".Length)
                : skillID;

            skillDataPath = $"Assets/ScriptableObjects/Skill/{folder}/{skillID}.asset";
            casterPrefabPath = $"Assets/Prefabs/Skills/{skillID}.prefab";
            fxPrefabPath = $"Assets/Prefabs/Skills/SkillEffects/SKILLFX_{numPart}.prefab";
            utilitySkillPath = $"Assets/ScriptableObjects/UtilitySkill/{folder}/Utility_{skillID}.asset";
        }

        private void DrawStatsSection()
        {
            _foldoutStats = EditorGUILayout.Foldout(_foldoutStats, "2. Stats & Combat Parameters", true, EditorStyles.foldoutHeader);
            if (!_foldoutStats) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                skillType = (SkillType)EditorGUILayout.EnumPopup("Skill Type", skillType);
                baseDamage = EditorGUILayout.FloatField("Base Damage", baseDamage);
                damageType = (DamageType)EditorGUILayout.EnumPopup("Damage Type", damageType);
                baseManaCost = EditorGUILayout.FloatField("Base Mana Cost", baseManaCost);
                castRange = EditorGUILayout.FloatField("Cast Range (0 = auto)", castRange);
                targetFilter = (SkillData.SkillTargetFilter)EditorGUILayout.EnumPopup("Target Filter", targetFilter);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Recovery / Backswing", EditorStyles.miniBoldLabel);
                useCustomRecoveryDuration = EditorGUILayout.Toggle("Use Custom Recovery", useCustomRecoveryDuration);
                if (useCustomRecoveryDuration)
                {
                    postCastRecoveryDuration = EditorGUILayout.FloatField("Post Cast Recovery (s)", postCastRecoveryDuration);
                }

                if (skillType == SkillType.Projectile)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Projectile Movement", EditorStyles.miniBoldLabel);
                    projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", projectileSpeed);
                    projectileLifetime = EditorGUILayout.FloatField("Projectile Lifetime", projectileLifetime);
                    projectileForward = (SkillData.ProjectileForward)EditorGUILayout.EnumPopup("Forward Direction", projectileForward);
                    projectileRotationOffset = EditorGUILayout.FloatField("Rotation Offset (deg)", projectileRotationOffset);

                    EditorGUILayout.Space(2);
                    chain = EditorGUILayout.Toggle("Enable Chain", chain);
                    if (chain)
                    {
                        maxChainTargets = EditorGUILayout.IntField("Max Chain Targets", maxChainTargets);
                        chainDamageMultiplier = EditorGUILayout.FloatField("Chain Damage Multiplier", chainDamageMultiplier);
                    }
                }

                if (skillType == SkillType.Area || area)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Area Detonation", EditorStyles.miniBoldLabel);
                    area = EditorGUILayout.Toggle("Is Area Skill", area);
                    areaRadius = EditorGUILayout.FloatField("Area Radius", areaRadius);
                }

                if (skillType == SkillType.Default && archetype == SkillArchetype.Beam)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Beam Tick Settings", EditorStyles.miniBoldLabel);
                    beamTickInterval = EditorGUILayout.FloatField("Beam Tick Interval (s)", beamTickInterval);
                }
            }
        }

        private void DrawStatusEffectsSection()
        {
            _foldoutStatusEffects = EditorGUILayout.Foldout(_foldoutStatusEffects, "3. On-Hit Status Effects", true, EditorStyles.foldoutHeader);
            if (!_foldoutStatusEffects) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_hostWindow != null)
                {
                    var soWindow = new SerializedObject(_hostWindow);
                    var prop = soWindow.FindProperty("skillTab.statusEffects");
                    if (prop != null)
                    {
                        EditorGUILayout.PropertyField(prop, new GUIContent("Status Effects"), true);
                        soWindow.ApplyModifiedProperties();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Host window serialized context unavailable.", MessageType.Info);
                }
            }
        }

        private void DrawFXSection()
        {
            _foldoutFX = EditorGUILayout.Foldout(_foldoutFX, "4. FX Visuals & Hitbox (Layer 9)", true, EditorStyles.foldoutHeader);
            if (!_foldoutFX) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                fxSprite = (Sprite)EditorGUILayout.ObjectField("FX Sprite", fxSprite, typeof(Sprite), false);
                fxAnimatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("FX Animator Controller", fxAnimatorController, typeof(RuntimeAnimatorController), false);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Hitbox Child (CircleCollider2D Trigger)", EditorStyles.miniBoldLabel);
                hitboxRadius = EditorGUILayout.FloatField("Hitbox Radius", hitboxRadius);

                if (archetype == SkillArchetype.ChainProjectile || archetype == SkillArchetype.AoEBurst)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Sensor Child (RangeSensor2D)", EditorStyles.miniBoldLabel);
                    sensorRadius = EditorGUILayout.FloatField("Sensor Radius", sensorRadius);
                    sensorDetectionLayers = EditorGUILayout.MaskField("Sensor Detection Layers", sensorDetectionLayers, UnityEditorInternal.InternalEditorUtility.layers);
                }
            }
        }

        private void DrawCasterSection()
        {
            _foldoutCaster = EditorGUILayout.Foldout(_foldoutCaster, "5. Caster Prefab Settings", true, EditorStyles.foldoutHeader);
            if (!_foldoutCaster) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                spawnType = (SkillCasterSpawnType)EditorGUILayout.EnumPopup("Spawn Type", spawnType);
                requireTarget = EditorGUILayout.Toggle("Require Target", requireTarget);
                forwardSpawnOffset = EditorGUILayout.FloatField("Forward Spawn Offset", forwardSpawnOffset);
                baseCooldown = EditorGUILayout.FloatField("Base Cooldown (s)", baseCooldown);
            }
        }

        private void DrawAIUtilitySection()
        {
            _foldoutUtility = EditorGUILayout.Foldout(_foldoutUtility, "6. AI UtilitySkill Settings", true, EditorStyles.foldoutHeader);
            if (!_foldoutUtility) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                createUtilitySkill = EditorGUILayout.Toggle("Create UtilitySkill Asset", createUtilitySkill);
                if (createUtilitySkill)
                {
                    baseWeight = EditorGUILayout.Slider("Base Weight", baseWeight, 0.1f, 10f);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Health Threshold Gate", EditorStyles.miniBoldLabel);
                    checkHealthThreshold = EditorGUILayout.Toggle("Check Health Threshold", checkHealthThreshold);
                    if (checkHealthThreshold)
                    {
                        minHealthPercent = EditorGUILayout.Slider("Min Health %", minHealthPercent, 0f, 100f);
                        maxHealthPercent = EditorGUILayout.Slider("Max Health %", maxHealthPercent, 0f, 100f);
                    }

                    EditorGUILayout.HelpBox("Min Mana Cost, Max Evaluation Range, and Cluster Consideration are automatically wired from skill stats.", MessageType.None);
                }
            }
        }

        private void DrawPathsSection()
        {
            _foldoutPaths = EditorGUILayout.Foldout(_foldoutPaths, "7. Output Paths & Database Registration", true, EditorStyles.foldoutHeader);
            if (!_foldoutPaths) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                skillDataPath = EditorGUILayout.TextField("SkillData Asset Path", skillDataPath);
                casterPrefabPath = EditorGUILayout.TextField("Caster Prefab Path", casterPrefabPath);
                fxPrefabPath = EditorGUILayout.TextField("FX Prefab Path", fxPrefabPath);
                if (createUtilitySkill)
                {
                    utilitySkillPath = EditorGUILayout.TextField("UtilitySkill Asset Path", utilitySkillPath);
                }

                EditorGUILayout.Space(4);
                autoRegisterInDatabases = EditorGUILayout.Toggle("Auto-Register in Databases", autoRegisterInDatabases);
                if (autoRegisterInDatabases)
                {
                    skillDatabase = (SkillDatabaseSO)EditorGUILayout.ObjectField("Skill Database SO", skillDatabase, typeof(SkillDatabaseSO), false);
                    utilitySkillDatabase = (UtilitySkillDatabaseSO)EditorGUILayout.ObjectField("UtilitySkill Database SO", utilitySkillDatabase, typeof(UtilitySkillDatabaseSO), false);
                }
            }
        }

        private void DrawSuccessPanel()
        {
            if (!_showSuccessPanel || _lastGeneratedCasterPrefab == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🎉 Skill Generation Succeeded!", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Caster Prefab: {_lastGeneratedCasterPrefab.name}", EditorStyles.miniLabel);

                if (GUILayout.Button("➡️ Send to Character tab", GUILayout.Height(30)))
                {
                    if (_hostWindow != null && _hostWindow.CharacterTab != null)
                    {
                        _hostWindow.CharacterTab.startingSkillPrefabs.Add(_lastGeneratedCasterPrefab);
                        _hostWindow.SelectTab(0);
                    }
                }
            }
        }

        public void Generate()
        {
            Generate(true);
        }

        public void Generate(bool showDialogs)
        {
            CreateSkillAssets(showDialogs);
        }

        private void CreateSkillAssets(bool showDialogs = true)
        {
            if (string.IsNullOrWhiteSpace(skillID))
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Validation Error", "Skill ID cannot be empty!", "OK");
                }
                else
                {
                    Debug.LogError("[SkillCreatorTab] Validation Error: Skill ID cannot be empty!");
                }
                return;
            }

            string cleanId = skillID.Trim();

            // Check existing files before creating
            List<string> existingFiles = new();
            if (File.Exists(skillDataPath)) existingFiles.Add(skillDataPath);
            if (File.Exists(casterPrefabPath)) existingFiles.Add(casterPrefabPath);
            if (File.Exists(fxPrefabPath)) existingFiles.Add(fxPrefabPath);
            if (createUtilitySkill && File.Exists(utilitySkillPath)) existingFiles.Add(utilitySkillPath);

            if (existingFiles.Count > 0 && showDialogs)
            {
                string listStr = string.Join("\n", existingFiles);
                if (!EditorUtility.DisplayDialog("Assets Already Exist", $"The following assets already exist:\n\n{listStr}\n\nDo you want to overwrite them?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            try
            {
                // Ensure target directories exist
                CreatorEditorUtils.EnsureFolderExists(Path.GetDirectoryName(skillDataPath));
                CreatorEditorUtils.EnsureFolderExists(Path.GetDirectoryName(casterPrefabPath));
                CreatorEditorUtils.EnsureFolderExists(Path.GetDirectoryName(fxPrefabPath));
                if (createUtilitySkill)
                {
                    CreatorEditorUtils.EnsureFolderExists(Path.GetDirectoryName(utilitySkillPath));
                }

                // ========================================================
                // 1. Create or Update SkillData
                // ========================================================
                EditorUtility.DisplayProgressBar("Generating Skill", "Creating SkillData ScriptableObject...", 0.15f);

                var skillData = AssetDatabase.LoadAssetAtPath<SkillData>(skillDataPath);
                if (skillData == null)
                {
                    skillData = ScriptableObject.CreateInstance<SkillData>();
                    AssetDatabase.CreateAsset(skillData, skillDataPath);
                }

                skillData.skillID = cleanId;
                skillData.skillName = string.IsNullOrWhiteSpace(skillName) ? cleanId : skillName.Trim();
                skillData.description = description;
                skillData.icon = icon;
                skillData.type = skillType;
                skillData.targetFilter = targetFilter;
                skillData.castRange = castRange;
                skillData.baseDamage = baseDamage;
                skillData.damageType = damageType;
                skillData.baseManaCost = baseManaCost;
                skillData.useCustomRecoveryDuration = useCustomRecoveryDuration;
                skillData.postCastRecoveryDuration = postCastRecoveryDuration;

                skillData.projectileSpeed = projectileSpeed;
                skillData.projectileLifetime = projectileLifetime;
                skillData.projectileForward = projectileForward;
                skillData.projectileRotationOffset = projectileRotationOffset;

                skillData.chain = chain;
                skillData.maxChainTargets = maxChainTargets;
                skillData.chainDamageMultiplier = chainDamageMultiplier;

                skillData.area = area || skillType == SkillType.Area;
                skillData.areaRadius = areaRadius;
                skillData.beamTickInterval = beamTickInterval;

                skillData.statusEffects = new List<StatusEffectApplication>(statusEffects);

                EditorUtility.SetDirty(skillData);

                // ========================================================
                // 2. Build FX Prefab
                // ========================================================
                EditorUtility.DisplayProgressBar("Generating Skill", "Constructing FX Prefab...", 0.35f);

                string fxName = Path.GetFileNameWithoutExtension(fxPrefabPath);
                var fxGo = new GameObject(fxName);
                fxGo.layer = 9; // Layer 9 for all FX objects

                var sr = fxGo.AddComponent<SpriteRenderer>();
                sr.sprite = fxSprite;
                sr.sortingLayerName = "Character";
                sr.sortingOrder = 0;

                var animator = fxGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = fxAnimatorController;

                fxGo.AddComponent<SkillHitResolver>();
                var skillInstance = fxGo.AddComponent<SkillInstance>();

                // Preset movement/detonation component
                switch (archetype)
                {
                    case SkillArchetype.Projectile:
                    case SkillArchetype.ChainProjectile:
                        fxGo.AddComponent<LinearProjectileMovement>();
                        break;
                    case SkillArchetype.AoEBurst:
                        fxGo.AddComponent<AoEDetonation>();
                        break;
                    case SkillArchetype.Beam:
                        fxGo.AddComponent<BeamMovement>();
                        break;
                }

                // Child: Hitbox
                var hitboxGo = new GameObject("Hitbox");
                hitboxGo.layer = 9;
                hitboxGo.transform.SetParent(fxGo.transform, false);

                var hitboxCol = hitboxGo.AddComponent<CircleCollider2D>();
                hitboxCol.isTrigger = true;
                hitboxCol.radius = hitboxRadius;

                hitboxGo.AddComponent<SkillCollision>();

                // Child: Sensor (Chain Projectile or AoE Burst)
                if (archetype == SkillArchetype.ChainProjectile || archetype == SkillArchetype.AoEBurst)
                {
                    var sensorGo = new GameObject("Sensor");
                    sensorGo.layer = 9;
                    sensorGo.transform.SetParent(fxGo.transform, false);

                    var rangeSensor = sensorGo.AddComponent<RangeSensor2D>();
                    rangeSensor.Shape = RangeSensor2D.Shapes.Circle;
                    rangeSensor.Circle.Radius = sensorRadius > 0f ? sensorRadius : (areaRadius > 0f ? areaRadius : 5f);
                    rangeSensor.DetectsOnLayers = sensorDetectionLayers;

                    var skillTargeting = sensorGo.AddComponent<SkillTargeting>();
                    CreatorEditorUtils.SetPrivateField(skillTargeting, "sensor", rangeSensor);
                }

                // Wire SkillInstance animator & hitboxObject
                CreatorEditorUtils.SetPrivateField(skillInstance, "animator", animator);
                CreatorEditorUtils.SetPrivateField(skillInstance, "hitboxObject", hitboxGo);

                var fxAsset = PrefabUtility.SaveAsPrefabAsset(fxGo, fxPrefabPath);
                UnityEngine.Object.DestroyImmediate(fxGo);

                // ========================================================
                // 3. Build Caster Prefab
                // ========================================================
                EditorUtility.DisplayProgressBar("Generating Skill", "Constructing Caster Prefab...", 0.6f);

                string casterName = Path.GetFileNameWithoutExtension(casterPrefabPath);
                var casterGo = new GameObject(casterName);
                casterGo.layer = 0;

                var statsComp = casterGo.AddComponent<SkillStatsManager>();
                CreatorEditorUtils.SetPrivateField(statsComp, "baseData", skillData);
                CreatorEditorUtils.SetPrivateField(statsComp, "baseCooldown", baseCooldown);

                var casterComp = casterGo.AddComponent<SkillCaster>();
                CreatorEditorUtils.SetPrivateField(casterComp, "stats", statsComp);
                CreatorEditorUtils.SetPrivateField(casterComp, "spawnType", (int)spawnType);
                CreatorEditorUtils.SetPrivateField(casterComp, "requireTarget", requireTarget);
                CreatorEditorUtils.SetPrivateField(casterComp, "forwardSpawnOffset", forwardSpawnOffset);

                var casterAsset = PrefabUtility.SaveAsPrefabAsset(casterGo, casterPrefabPath);
                UnityEngine.Object.DestroyImmediate(casterGo);

                // ========================================================
                // 4. Optionally Create UtilitySkill Asset
                // ========================================================
                UtilitySkill utilityAsset = null;
                if (createUtilitySkill)
                {
                    EditorUtility.DisplayProgressBar("Generating Skill", "Creating UtilitySkill ScriptableObject...", 0.75f);

                    utilityAsset = AssetDatabase.LoadAssetAtPath<UtilitySkill>(utilitySkillPath);
                    if (utilityAsset == null)
                    {
                        utilityAsset = ScriptableObject.CreateInstance<UtilitySkill>();
                        AssetDatabase.CreateAsset(utilityAsset, utilitySkillPath);
                    }

                    CreatorEditorUtils.SetPrivateField(utilityAsset, "skillID", cleanId);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "baseWeight", baseWeight);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "minManaCost", baseManaCost);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "useDistanceConsideration", true);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "maxEvaluationRange", skillData.EffectiveRange);

                    bool isAoE = archetype == SkillArchetype.AoEBurst || skillData.area;
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "useClusterConsideration", isAoE);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "clusterRadius", areaRadius > 0f ? areaRadius : 3f);

                    CreatorEditorUtils.SetPrivateField(utilityAsset, "checkHealthThreshold", checkHealthThreshold);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "minHealthPercent", minHealthPercent);
                    CreatorEditorUtils.SetPrivateField(utilityAsset, "maxHealthPercent", maxHealthPercent);

                    EditorUtility.SetDirty(utilityAsset);
                }

                // ========================================================
                // 5. Link Assets
                // ========================================================
                skillData.skillPrefab = fxAsset;
                skillData.utilityData = utilityAsset;
                EditorUtility.SetDirty(skillData);

                // ========================================================
                // 6. Database Registration
                // ========================================================
                if (autoRegisterInDatabases)
                {
                    EditorUtility.DisplayProgressBar("Generating Skill", "Registering in databases...", 0.9f);

                    // SkillDatabaseSO
                    if (skillDatabase != null)
                    {
                        var soDb = new SerializedObject(skillDatabase);
                        var skillsProp = soDb.FindProperty("skills");
                        if (skillsProp != null)
                        {
                            bool alreadyInDb = false;
                            for (int i = 0; i < skillsProp.arraySize; i++)
                            {
                                if (skillsProp.GetArrayElementAtIndex(i).objectReferenceValue == skillData)
                                {
                                    alreadyInDb = true;
                                    break;
                                }
                            }
                            if (!alreadyInDb)
                            {
                                skillsProp.InsertArrayElementAtIndex(skillsProp.arraySize);
                                skillsProp.GetArrayElementAtIndex(skillsProp.arraySize - 1).objectReferenceValue = skillData;
                                soDb.ApplyModifiedProperties();
                                EditorUtility.SetDirty(skillDatabase);
                                Debug.Log($"[SkillCreatorTab] Appended '{cleanId}' into SkillDatabaseSO.");
                            }
                        }
                    }

                    // UtilitySkillDatabaseSO
                    if (utilitySkillDatabase != null && utilityAsset != null)
                    {
                        if (utilitySkillDatabase.utilSkills == null)
                        {
                            utilitySkillDatabase.utilSkills = new List<UtilitySkillEntry>();
                        }
                        var existing = utilitySkillDatabase.utilSkills.Find(e => string.Equals(e.id, cleanId, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.so = utilityAsset;
                        }
                        else
                        {
                            utilitySkillDatabase.utilSkills.Add(new UtilitySkillEntry { id = cleanId, so = utilityAsset });
                        }
                        EditorUtility.SetDirty(utilitySkillDatabase);
                        Debug.Log($"[SkillCreatorTab] Upserted '{cleanId}' into UtilitySkillDatabaseSO.");
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                Selection.activeObject = casterAsset;
                EditorGUIUtility.PingObject(casterAsset);

                _lastGeneratedCasterPrefab = casterAsset;
                _showSuccessPanel = true;

                Debug.Log($"<color=green>[SkillCreatorTab]</color> Successfully created skill '{cleanId}'!");
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Success", $"Skill '{cleanId}' successfully created!\n\nSkillData: {skillDataPath}\nCaster Prefab: {casterPrefabPath}\nFX Prefab: {fxPrefabPath}", "Awesome!");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[SkillCreatorTab] Failed to generate skill: {ex}");
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to generate skill:\n{ex.Message}", "OK");
                }
            }
        }
    }
}
