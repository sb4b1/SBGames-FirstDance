using System;
using System.Collections.Generic;
using System.IO;
using SBGames.Characters.Traps;
using SBGames.Database;
using SBGames.Environment;
using SBGames.Inventory.Core;
using SBGames.Inventory.Data;
using SBGames.Placement;
using UnityEditor;
using UnityEngine;

namespace SBGames.Editor.Items
{
    public enum ItemOrObjectArchetype
    {
        Artifact,
        Rune,
        Consumable,
        TrapHazard,
        TrainingDummy,
        DestructibleProp
    }

    public class ItemAndObjectCreatorWizard : EditorWindow
    {
        // ----------------------------------------------------
        // Archetype Selector
        // ----------------------------------------------------
        [SerializeField] private ItemOrObjectArchetype archetype = ItemOrObjectArchetype.Artifact;

        // ----------------------------------------------------
        // Common Identity
        // ----------------------------------------------------
        [SerializeField] private string objectId = "ITEM_ARTIFACT_003";
        [SerializeField] private string displayName = "New Artifact";
        [SerializeField] private Sprite iconOrSprite;
        [SerializeField] private string description = "";

        // ----------------------------------------------------
        // Inventory Item Fields (Artifact / Rune / Consumable)
        // ----------------------------------------------------
        [SerializeField] private int baseValue = 10;
        [SerializeField] private int maxSockets = 0;
        [SerializeField] private List<ItemStatModifierData> statModifiers = new();
        [SerializeField] private ConsumableEffect consumableEffect;
        [SerializeField] private bool generateWorldDropPrefab = true;

        // ----------------------------------------------------
        // Trap Fields
        // ----------------------------------------------------
        [SerializeField] private TrapCategoryType trapCategory = TrapCategoryType.MechanicalHazard;
        [SerializeField] private TrapTriggerType triggerType = TrapTriggerType.InstantBurst;
        [SerializeField] private TrapTargetFilter targetFilter = TrapTargetFilter.AdventurersOnly;
        [SerializeField] private float trapDamage = 25f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private bool canCrit = false;
        [SerializeField] private float trapCooldown = 1.0f;
        [SerializeField] private int maxChargesPerRound = 0;
        [SerializeField] private float triggerDelay = 0.5f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private int baseEnergyCost = 10;
        [SerializeField] private Vector2Int trapFootprint = Vector2Int.one;
        [SerializeField] private bool hasNavMeshObstacle = false;
        [SerializeField] private float livingTrapMaxHealth = 100f;

        // ----------------------------------------------------
        // Training Dummy Fields
        // ----------------------------------------------------
        [SerializeField] private RuntimeAnimatorController dummyAnimatorController;
        [SerializeField] private float dummyMaxLife = 500f;
        [SerializeField] private CharacterRank dummyRank = CharacterRank.D;
        [SerializeField] private Vector2 dummyTriggerOffset = new Vector2(0f, 0f);
        [SerializeField] private Vector2 dummyTriggerSize = new Vector2(0.6f, 1.2f);
        [SerializeField] private Vector2 dummyCollisionOffset = new Vector2(0f, -0.4f);
        [SerializeField] private Vector2 dummyCollisionSize = new Vector2(0.6f, 0.6f);

        // ----------------------------------------------------
        // Destructible Prop Fields
        // ----------------------------------------------------
        [SerializeField] private Sprite brokenOrOpenSprite;
        [SerializeField] private float propHealth = 30f;
        [SerializeField] private bool isInvulnerable = false;
        [SerializeField] private GameObject propDropPrefab;
        [SerializeField] private ItemDataSO propDropItemData;
        [SerializeField] private float propDropChance = 1f;
        [SerializeField] private Vector2 propColliderOffset = Vector2.zero;
        [SerializeField] private Vector2 propColliderSize = new Vector2(0.6f, 0.6f);

        // ----------------------------------------------------
        // Save Paths & Database Registration
        // ----------------------------------------------------
        [SerializeField] private string soFolderPath = "Assets/ScriptableObjects/Item/Artifacts";
        [SerializeField] private string prefabFolderPath = "Assets/Prefabs/Items/Artifacts";
        [SerializeField] private bool autoRegisterInDatabase = true;
        [SerializeField] private ItemDatabaseSO itemDatabase;
        [SerializeField] private CharacterDatabaseSO characterDatabase;

        // UI Scroll
        private Vector2 _scrollPos;

        [MenuItem("SBGames/Items/Item & Object Creator Wizard", priority = 20)]
        [MenuItem("Assets/Create/SBGames/Item & Object Creator Wizard", priority = 51)]
        public static void OpenWindow()
        {
            var window = GetWindow<ItemAndObjectCreatorWizard>("Item & Object Creator");
            window.minSize = new Vector2(460, 680);
            window.Show();
        }

        private void OnEnable()
        {
            AutoFindDatabases();
            ApplyArchetypePreset(archetype);
        }

        private void AutoFindDatabases()
        {
            if (itemDatabase == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(path);
                }
            }

            if (characterDatabase == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:CharacterDatabaseSO");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    characterDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(path);
                }
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(8);

            DrawArchetypeSelector();
            EditorGUILayout.Space(6);

            DrawCommonIdentitySection();
            EditorGUILayout.Space(6);

            DrawContextualSection();
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
                EditorGUILayout.LabelField("📦 Item & World Object Creator Wizard", titleStyle);
                EditorGUILayout.LabelField("Create ScriptableObjects & Prefabs for Artifacts, Runes, Consumables, Traps, Dummies, & Props.", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawArchetypeSelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Select Object Archetype", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                archetype = (ItemOrObjectArchetype)EditorGUILayout.EnumPopup("Archetype", archetype);
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyArchetypePreset(archetype);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Artifact", EditorStyles.miniButtonLeft)) { archetype = ItemOrObjectArchetype.Artifact; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Rune", EditorStyles.miniButtonMid)) { archetype = ItemOrObjectArchetype.Rune; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Consumable", EditorStyles.miniButtonMid)) { archetype = ItemOrObjectArchetype.Consumable; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Trap", EditorStyles.miniButtonMid)) { archetype = ItemOrObjectArchetype.TrapHazard; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Dummy", EditorStyles.miniButtonMid)) { archetype = ItemOrObjectArchetype.TrainingDummy; ApplyArchetypePreset(archetype); }
                if (GUILayout.Button("Prop", EditorStyles.miniButtonRight)) { archetype = ItemOrObjectArchetype.DestructibleProp; ApplyArchetypePreset(archetype); }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ApplyArchetypePreset(ItemOrObjectArchetype type)
        {
            switch (type)
            {
                case ItemOrObjectArchetype.Artifact:
                    objectId = "ITEM_ARTIFACT_003";
                    displayName = "New Artifact";
                    soFolderPath = "Assets/ScriptableObjects/Item/Artifacts";
                    prefabFolderPath = "Assets/Prefabs/Items/Artifacts";
                    generateWorldDropPrefab = true;
                    break;

                case ItemOrObjectArchetype.Rune:
                    objectId = "ITEM_RUNE_002";
                    displayName = "New Rune";
                    soFolderPath = "Assets/ScriptableObjects/Item/Runes";
                    prefabFolderPath = "Assets/Prefabs/Items/Runes";
                    generateWorldDropPrefab = true;
                    break;

                case ItemOrObjectArchetype.Consumable:
                    objectId = "ITEM_POTION_001";
                    displayName = "Health Potion";
                    soFolderPath = "Assets/ScriptableObjects/Item/Consumables";
                    prefabFolderPath = "Assets/Prefabs/Items/Consumables";
                    generateWorldDropPrefab = true;
                    break;

                case ItemOrObjectArchetype.TrapHazard:
                    objectId = "TRAP_SPIKE_001";
                    displayName = "Spike Trap";
                    soFolderPath = "Assets/ScriptableObjects/Placement/Traps";
                    prefabFolderPath = "Assets/Prefabs/Traps";
                    trapCategory = TrapCategoryType.MechanicalHazard;
                    triggerType = TrapTriggerType.InstantBurst;
                    trapDamage = 25f;
                    break;

                case ItemOrObjectArchetype.TrainingDummy:
                    objectId = "DUMMY_TARGET_001";
                    displayName = "Training Dummy";
                    soFolderPath = "Assets/ScriptableObjects/Character/Dummy";
                    prefabFolderPath = "Assets/Prefabs/Characters/Dummy";
                    dummyMaxLife = 500f;
                    break;

                case ItemOrObjectArchetype.DestructibleProp:
                    objectId = "PROP_CRATE_001";
                    displayName = "Wooden Crate";
                    soFolderPath = "Assets/ScriptableObjects/Environment/Props";
                    prefabFolderPath = "Assets/Prefabs/Environment/Props";
                    propHealth = 30f;
                    break;
            }
        }

        private void DrawCommonIdentitySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. General Identity", EditorStyles.boldLabel);
                objectId = EditorGUILayout.TextField(new GUIContent("ID / Code", "Unique asset identifier (e.g. ITEM_ARTIFACT_003, TRAP_SPIKE_001)"), objectId);
                displayName = EditorGUILayout.TextField(new GUIContent("Display Name", "User-facing title / name in UI"), displayName);
                iconOrSprite = (Sprite)EditorGUILayout.ObjectField(new GUIContent("Sprite / Icon", "Primary 2D sprite or UI icon"), iconOrSprite, typeof(Sprite), false);
                description = EditorGUILayout.TextField(new GUIContent("Description", "Tooltip or lore description"), description);
            }
        }

        private void DrawContextualSection()
        {
            switch (archetype)
            {
                case ItemOrObjectArchetype.Artifact:
                case ItemOrObjectArchetype.Rune:
                case ItemOrObjectArchetype.Consumable:
                    DrawInventoryItemSection();
                    break;

                case ItemOrObjectArchetype.TrapHazard:
                    DrawTrapSection();
                    break;

                case ItemOrObjectArchetype.TrainingDummy:
                    DrawDummySection();
                    break;

                case ItemOrObjectArchetype.DestructibleProp:
                    DrawPropSection();
                    break;
            }
        }

        private void DrawInventoryItemSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Item Configuration & Stats", EditorStyles.boldLabel);
                baseValue = EditorGUILayout.IntField("Base Value (Gold)", baseValue);

                if (archetype == ItemOrObjectArchetype.Artifact)
                {
                    maxSockets = EditorGUILayout.IntSlider("Rune Sockets", maxSockets, 0, 4);
                }

                if (archetype == ItemOrObjectArchetype.Consumable)
                {
                    consumableEffect = (ConsumableEffect)EditorGUILayout.ObjectField("Consumable Effect SO", consumableEffect, typeof(ConsumableEffect), false);
                }

                if (archetype == ItemOrObjectArchetype.Artifact || archetype == ItemOrObjectArchetype.Rune)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Stat Modifiers", EditorStyles.miniBoldLabel);

                    for (int i = 0; i < statModifiers.Count; i++)
                    {
                        var mod = statModifiers[i];
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal();
                        mod.StatType = (StatType)EditorGUILayout.EnumPopup("Stat", mod.StatType);
                        if (GUILayout.Button("X", GUILayout.Width(22)))
                        {
                            statModifiers.RemoveAt(i);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        mod.ModType = (StatModType)EditorGUILayout.EnumPopup("Mod Type", mod.ModType);
                        mod.Value = EditorGUILayout.FloatField("Value", mod.Value);
                        mod.OrderOverride = EditorGUILayout.IntField("Order Override (-1=default)", mod.OrderOverride);

                        statModifiers[i] = mod;
                        EditorGUILayout.EndVertical();
                    }

                    if (GUILayout.Button("+ Add Stat Modifier", GUILayout.Height(20)))
                    {
                        statModifiers.Add(new ItemStatModifierData(StatType.MaxHealth, 10f, StatModType.Flat, -1));
                    }
                }

                EditorGUILayout.Space(4);
                generateWorldDropPrefab = EditorGUILayout.Toggle("Generate World Drop Prefab", generateWorldDropPrefab);
            }
        }

        private void DrawTrapSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Trap & Hazard Dynamics", EditorStyles.boldLabel);
                trapCategory = (TrapCategoryType)EditorGUILayout.EnumPopup("Category Type", trapCategory);
                triggerType = (TrapTriggerType)EditorGUILayout.EnumPopup("Trigger Type", triggerType);
                targetFilter = (TrapTargetFilter)EditorGUILayout.EnumPopup("Target Filter", targetFilter);

                EditorGUILayout.Space(2);
                trapDamage = EditorGUILayout.FloatField("Base Damage", trapDamage);
                damageType = (DamageType)EditorGUILayout.EnumPopup("Damage Type", damageType);
                canCrit = EditorGUILayout.Toggle("Can Crit", canCrit);

                EditorGUILayout.Space(2);
                trapCooldown = EditorGUILayout.FloatField("Cooldown (s)", trapCooldown);
                maxChargesPerRound = EditorGUILayout.IntField("Max Charges (0 = unlimited)", maxChargesPerRound);

                if (triggerType == TrapTriggerType.DelayedDetonation)
                {
                    triggerDelay = EditorGUILayout.FloatField("Trigger Delay (s)", triggerDelay);
                }
                else if (triggerType == TrapTriggerType.PeriodicTick)
                {
                    tickInterval = EditorGUILayout.FloatField("Tick Interval (s)", tickInterval);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Dungeon Placement & Pathfinding", EditorStyles.miniBoldLabel);
                baseEnergyCost = EditorGUILayout.IntField("Energy Cost", baseEnergyCost);
                trapFootprint = EditorGUILayout.Vector2IntField("Grid Footprint", trapFootprint);
                hasNavMeshObstacle = EditorGUILayout.Toggle("Has NavMesh Obstacle", hasNavMeshObstacle);

                if (trapCategory == TrapCategoryType.LivingUnit)
                {
                    livingTrapMaxHealth = EditorGUILayout.FloatField("Living Trap Max HP", livingTrapMaxHealth);
                }
            }
        }

        private void DrawDummySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Training Dummy & Hitbox Setup", EditorStyles.boldLabel);
                dummyAnimatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Animator Controller", dummyAnimatorController, typeof(RuntimeAnimatorController), false);
                dummyMaxLife = EditorGUILayout.FloatField("Dummy Max Health", dummyMaxLife);
                dummyRank = (CharacterRank)EditorGUILayout.EnumPopup("Dummy Rank", dummyRank);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Hitboxes & Colliders", EditorStyles.miniBoldLabel);
                dummyTriggerOffset = EditorGUILayout.Vector2Field("Trigger Offset", dummyTriggerOffset);
                dummyTriggerSize = EditorGUILayout.Vector2Field("Trigger Box Size", dummyTriggerSize);
                dummyCollisionOffset = EditorGUILayout.Vector2Field("Collision Offset", dummyCollisionOffset);
                dummyCollisionSize = EditorGUILayout.Vector2Field("Collision Box Size", dummyCollisionSize);
            }
        }

        private void DrawPropSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. Destructible Prop & Loot Setup", EditorStyles.boldLabel);
                brokenOrOpenSprite = (Sprite)EditorGUILayout.ObjectField("Broken / Open Sprite", brokenOrOpenSprite, typeof(Sprite), false);
                propHealth = EditorGUILayout.FloatField("Max Health", propHealth);
                isInvulnerable = EditorGUILayout.Toggle("Is Invulnerable", isInvulnerable);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Loot Drop on Destruction", EditorStyles.miniBoldLabel);
                propDropPrefab = (GameObject)EditorGUILayout.ObjectField("Drop Prefab", propDropPrefab, typeof(GameObject), false);
                propDropItemData = (ItemDataSO)EditorGUILayout.ObjectField("Drop ItemDataSO", propDropItemData, typeof(ItemDataSO), false);
                propDropChance = EditorGUILayout.Slider("Drop Chance", propDropChance, 0f, 1f);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Collider Bounds", EditorStyles.miniBoldLabel);
                propColliderOffset = EditorGUILayout.Vector2Field("Collider Offset", propColliderOffset);
                propColliderSize = EditorGUILayout.Vector2Field("Collider Box Size", propColliderSize);
            }
        }

        private void DrawSavingSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("3. Output Folders & Database Registration", EditorStyles.boldLabel);
                soFolderPath = EditorGUILayout.TextField("ScriptableObject Folder", soFolderPath);
                prefabFolderPath = EditorGUILayout.TextField("Prefab Folder", prefabFolderPath);

                EditorGUILayout.Space(2);
                autoRegisterInDatabase = EditorGUILayout.Toggle("Auto-Register in Database", autoRegisterInDatabase);
                if (autoRegisterInDatabase)
                {
                    if (archetype == ItemOrObjectArchetype.Artifact || archetype == ItemOrObjectArchetype.Rune || archetype == ItemOrObjectArchetype.Consumable)
                    {
                        itemDatabase = (ItemDatabaseSO)EditorGUILayout.ObjectField("Item Database", itemDatabase, typeof(ItemDatabaseSO), false);
                    }
                    else if (archetype == ItemOrObjectArchetype.TrainingDummy)
                    {
                        characterDatabase = (CharacterDatabaseSO)EditorGUILayout.ObjectField("Character Database", characterDatabase, typeof(CharacterDatabaseSO), false);
                    }
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

            GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
            if (GUILayout.Button($"🚀 Generate {archetype}", btnStyle))
            {
                CreateAssetAndPrefab();
            }
            GUI.backgroundColor = Color.white;
        }

        // ----------------------------------------------------
        // Creation Logic
        // ----------------------------------------------------
        private void CreateAssetAndPrefab()
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                EditorUtility.DisplayDialog("Validation Error", "ID cannot be empty!", "OK");
                return;
            }

            string cleanId = objectId.Trim();
            string cleanDisplayName = string.IsNullOrWhiteSpace(displayName) ? cleanId : displayName.Trim();

            EnsureFolderExists(soFolderPath);
            EnsureFolderExists(prefabFolderPath);

            switch (archetype)
            {
                case ItemOrObjectArchetype.Artifact:
                case ItemOrObjectArchetype.Rune:
                case ItemOrObjectArchetype.Consumable:
                    CreateInventoryItem(cleanId, cleanDisplayName);
                    break;

                case ItemOrObjectArchetype.TrapHazard:
                    CreateTrap(cleanId, cleanDisplayName);
                    break;

                case ItemOrObjectArchetype.TrainingDummy:
                    CreateTrainingDummy(cleanId, cleanDisplayName);
                    break;

                case ItemOrObjectArchetype.DestructibleProp:
                    CreateDestructibleProp(cleanId, cleanDisplayName);
                    break;
            }
        }

        private void CreateInventoryItem(string id, string name)
        {
            string soPath = Path.Combine(soFolderPath, $"{id}.asset").Replace("\\", "/");
            var itemSO = AssetDatabase.LoadAssetAtPath<ItemDataSO>(soPath);
            if (itemSO == null)
            {
                itemSO = CreateInstance<ItemDataSO>();
                AssetDatabase.CreateAsset(itemSO, soPath);
            }

            ItemType it = archetype == ItemOrObjectArchetype.Artifact ? ItemType.Artifact :
                          archetype == ItemOrObjectArchetype.Rune ? ItemType.Rune : ItemType.Consumable;

            EquipmentSlotType slot = archetype == ItemOrObjectArchetype.Artifact ? EquipmentSlotType.Artifact :
                                    archetype == ItemOrObjectArchetype.Rune ? EquipmentSlotType.Rune : EquipmentSlotType.None;

            itemSO.InitializeRuntimeData(
                id: id,
                name: name,
                type: it,
                maxStack: it == ItemType.Rune ? 10 : it == ItemType.Consumable ? 99 : 1,
                baseValue: baseValue,
                slotType: slot,
                modifiers: statModifiers,
                effect: consumableEffect,
                maxSockets: maxSockets
            );

            // Set private icon and description
            SetPrivateField(itemSO, "icon", iconOrSprite);
            SetPrivateField(itemSO, "description", description);

            EditorUtility.SetDirty(itemSO);

            GameObject prefabAsset = null;
            if (generateWorldDropPrefab)
            {
                string prefabPath = Path.Combine(prefabFolderPath, $"{id}.prefab").Replace("\\", "/");
                var dropGo = new GameObject(id);
                var sr = dropGo.AddComponent<SpriteRenderer>();
                sr.sprite = iconOrSprite;
                sr.sortingLayerName = "Item";

                var col = dropGo.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.2f;

                prefabAsset = PrefabUtility.SaveAsPrefabAsset(dropGo, prefabPath);
                DestroyImmediate(dropGo);
            }

            if (autoRegisterInDatabase && itemDatabase != null)
            {
                itemDatabase.RegisterRuntimeItem(itemSO);
                EditorUtility.SetDirty(itemDatabase);
                Debug.Log($"[ItemCreatorWizard] Registered '{id}' into ItemDatabaseSO.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = itemSO;
            EditorGUIUtility.PingObject(itemSO);

            EditorUtility.DisplayDialog("Success", $"Item '{id}' successfully created!\n\nScriptableObject: {soPath}", "Awesome!");
        }

        private void CreateTrap(string id, string name)
        {
            // 1. Create TrapDataSO
            string soPath = Path.Combine(soFolderPath, $"{id}.asset").Replace("\\", "/");
            var trapData = AssetDatabase.LoadAssetAtPath<TrapDataSO>(soPath);
            if (trapData == null)
            {
                trapData = CreateInstance<TrapDataSO>();
                AssetDatabase.CreateAsset(trapData, soPath);
            }

            SetPrivateField(trapData, "unitId", id);
            SetPrivateField(trapData, "displayName", name);
            SetPrivateField(trapData, "icon", iconOrSprite);
            SetPrivateField(trapData, "trapType", trapCategory);
            SetPrivateField(trapData, "triggerType", triggerType);
            SetPrivateField(trapData, "targetFilter", targetFilter);
            SetPrivateField(trapData, "damageAmount", trapDamage);
            SetPrivateField(trapData, "damageType", damageType);
            SetPrivateField(trapData, "canCrit", canCrit);
            SetPrivateField(trapData, "cooldown", trapCooldown);
            SetPrivateField(trapData, "maxChargesPerRound", maxChargesPerRound);
            SetPrivateField(trapData, "triggerDelay", triggerDelay);
            SetPrivateField(trapData, "tickInterval", tickInterval);
            SetPrivateField(trapData, "baseCost", baseEnergyCost);
            SetPrivateField(trapData, "footprint", trapFootprint);
            SetPrivateField(trapData, "hasNavMeshObstacle", hasNavMeshObstacle);
            SetPrivateField(trapData, "maxHealth", livingTrapMaxHealth);

            // 2. Create Trap Prefab
            string prefabPath = Path.Combine(prefabFolderPath, $"{id}.prefab").Replace("\\", "/");
            var trapGo = new GameObject(id);

            var col = trapGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(trapFootprint.x, trapFootprint.y);

            var sr = trapGo.AddComponent<SpriteRenderer>();
            sr.sprite = iconOrSprite;
            sr.sortingLayerName = "Character";

            var controller = trapGo.AddComponent<TrapController>();
            SetPrivateField(controller, "trapData", trapData);

            if (hasNavMeshObstacle)
            {
                var obstacle = trapGo.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(trapFootprint.x, trapFootprint.y, 1f);
                obstacle.carving = true;
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(trapGo, prefabPath);
            DestroyImmediate(trapGo);

            // Link Prefab back into TrapDataSO
            SetPrivateField(trapData, "prefab", prefabAsset);
            EditorUtility.SetDirty(trapData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);

            EditorUtility.DisplayDialog("Success", $"Trap '{id}' successfully created!\n\nPrefab: {prefabPath}\nTrapDataSO: {soPath}", "Awesome!");
        }

        private void CreateTrainingDummy(string id, string name)
        {
            // 1. Create BaseStats SO
            string statPath = Path.Combine(soFolderPath, $"{id}.asset").Replace("\\", "/");
            var statAsset = AssetDatabase.LoadAssetAtPath<CharacterBaseStatSO>(statPath);
            if (statAsset == null)
            {
                statAsset = CreateInstance<CharacterBaseStatSO>();
                AssetDatabase.CreateAsset(statAsset, statPath);
            }

            statAsset.characterLevel = 1;
            statAsset.characterRank = dummyRank;
            statAsset.MaxLife = dummyMaxLife;
            statAsset.MaxMana = 0f;
            statAsset.Movespeed = 0f;
            statAsset.Damage = 0f;
            statAsset.AttackRange = 1f;
            statAsset.AttackTimeInterval = 1f;

            EditorUtility.SetDirty(statAsset);

            // 2. Build Dummy Hierarchy (WITHOUT AI or Brain movement)
            string prefabPath = Path.Combine(prefabFolderPath, $"{id}.prefab").Replace("\\", "/");
            var rootGo = new GameObject(id);
            rootGo.layer = 6; // Character
            rootGo.tag = "Untagged";

            var rb = rootGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            var statsManager = rootGo.AddComponent<CharacterStatsManager>();
            SetPrivateField(statsManager, "baseStats", statAsset);
            SetPrivateField(statsManager, "level", 1);

            var identifier = rootGo.AddComponent<CharacterIdentifier>();
            SetPrivateField(identifier, "characterName", id);

            // Model Child
            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(rootGo.transform, false);
            var sr = modelGo.AddComponent<SpriteRenderer>();
            sr.sprite = iconOrSprite;
            sr.sortingLayerName = "Character";

            if (dummyAnimatorController != null)
            {
                var anim = modelGo.AddComponent<Animator>();
                anim.runtimeAnimatorController = dummyAnimatorController;
            }

            // Hitbox Child
            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(rootGo.transform, false);

            var triggerGo = new GameObject("Trigger");
            triggerGo.transform.SetParent(hitboxGo.transform, false);
            triggerGo.layer = 7; // HitboxTrigger
            var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.offset = dummyTriggerOffset;
            triggerCol.size = dummyTriggerSize;

            var collisionGo = new GameObject("Collision");
            collisionGo.transform.SetParent(hitboxGo.transform, false);
            collisionGo.layer = 8; // HitboxCollision
            var collisionCol = collisionGo.AddComponent<BoxCollider2D>();
            collisionCol.isTrigger = false;
            collisionCol.offset = dummyCollisionOffset;
            collisionCol.size = dummyCollisionSize;

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            DestroyImmediate(rootGo);

            if (autoRegisterInDatabase && characterDatabase != null)
            {
                var soDb = new SerializedObject(characterDatabase);
                var charsProp = soDb.FindProperty("characters");
                if (charsProp != null)
                {
                    charsProp.InsertArrayElementAtIndex(charsProp.arraySize);
                    var el = charsProp.GetArrayElementAtIndex(charsProp.arraySize - 1);
                    el.FindPropertyRelative("id").stringValue = id;
                    el.FindPropertyRelative("characterName").stringValue = name;
                    el.FindPropertyRelative("prefab").objectReferenceValue = prefabAsset;
                    el.FindPropertyRelative("baseStats").objectReferenceValue = statAsset;
                    soDb.ApplyModifiedProperties();
                    EditorUtility.SetDirty(characterDatabase);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);

            EditorUtility.DisplayDialog("Success", $"Training Dummy '{id}' successfully created!\n\nPrefab: {prefabPath}", "Awesome!");
        }

        private void CreateDestructibleProp(string id, string name)
        {
            string prefabPath = Path.Combine(prefabFolderPath, $"{id}.prefab").Replace("\\", "/");
            var propGo = new GameObject(id);

            var col = propGo.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.offset = propColliderOffset;
            col.size = propColliderSize;

            var sr = propGo.AddComponent<SpriteRenderer>();
            sr.sprite = iconOrSprite;
            sr.sortingLayerName = "Character";

            var controller = propGo.AddComponent<DestructiblePropController>();
            SetPrivateField(controller, "propName", name);
            SetPrivateField(controller, "maxHealth", propHealth);
            SetPrivateField(controller, "currentHealth", propHealth);
            SetPrivateField(controller, "isInvulnerable", isInvulnerable);
            SetPrivateField(controller, "spriteRenderer", sr);
            SetPrivateField(controller, "intactSprite", iconOrSprite);
            SetPrivateField(controller, "brokenOrOpenSprite", brokenOrOpenSprite);
            SetPrivateField(controller, "propCollider", col);
            SetPrivateField(controller, "dropPrefab", propDropPrefab);
            SetPrivateField(controller, "dropItemData", propDropItemData);
            SetPrivateField(controller, "dropChance", propDropChance);

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(propGo, prefabPath);
            DestroyImmediate(propGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);

            EditorUtility.DisplayDialog("Success", $"Destructible Prop '{id}' successfully created!\n\nPrefab: {prefabPath}", "Awesome!");
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
                    else if (value is Vector2Int v2iVal)
                    {
                        prop.vector2IntValue = v2iVal;
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
