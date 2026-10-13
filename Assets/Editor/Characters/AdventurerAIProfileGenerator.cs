using System.Collections.Generic;
using SBGames.Database;
using SBGames.Database.Editor;
using SBGames.Database.Editor.CSV;
using SBGames.Inventory.Core;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Characters.AI.Editor
{
    public static class AdventurerAIProfileGenerator
    {
        private const string ScriptableObjectsRoot = "Assets/ScriptableObjects";
        private const string ProfilesFolder = "Assets/ScriptableObjects/AIProfiles";
        private const string ItemsRootFolder = "Assets/ScriptableObjects/Item";
        private const string ConsumablesFolder = "Assets/ScriptableObjects/Item/Consumables";

        [MenuItem("SBGames/AI/Create Default Adventurer Profiles")]
        public static void CreateDefaultAdventurerProfiles()
        {
            if (!AssetDatabase.IsValidFolder(ScriptableObjectsRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "AIProfiles");
            }

            int createdCount = 0;

            createdCount += CreateProfileIfMissing(
                assetName: "Profile_Rank_E",
                rank: CharacterRank.E,
                decisionInterval: 0.5f,
                visionRadius: 5f,
                fleeHpThreshold: 0.40f,
                doorwayHesitation: 0.6f,
                hazardMemoryDuration: 10f,
                targetPriorityMode: TargetPriorityMode.Nearest,
                squadOrderCompliance: 0.50f,
                accuracy: LeaderAssessmentAccuracy.Optimistic,
                safetyMargin: LeaderSafetyMargin.Thin,
                itemUse: ItemUseQuality.LateWasteful,
                rewardWeight: 1.60f,
                successionReactionDelay: 1.80f,
                stepUpThreshold: 0.70f,
                leaderlessBehavior: LeaderlessBehavior.Dissolve
            );

            createdCount += CreateProfileIfMissing(
                assetName: "Profile_Rank_D",
                rank: CharacterRank.D,
                decisionInterval: 0.4f,
                visionRadius: 6f,
                fleeHpThreshold: 0.35f,
                doorwayHesitation: 0.45f,
                hazardMemoryDuration: 25f,
                targetPriorityMode: TargetPriorityMode.LowestHp,
                squadOrderCompliance: 0.75f,
                accuracy: LeaderAssessmentAccuracy.NearTrue,
                safetyMargin: LeaderSafetyMargin.Medium,
                itemUse: ItemUseQuality.Adequate,
                rewardWeight: 1.15f,
                successionReactionDelay: 0.90f,
                stepUpThreshold: 0.50f,
                leaderlessBehavior: LeaderlessBehavior.StayLoose
            );

            createdCount += CreateProfileIfMissing(
                assetName: "Profile_Rank_C",
                rank: CharacterRank.C,
                decisionInterval: 0.3f,
                visionRadius: 7f,
                fleeHpThreshold: 0.30f,
                doorwayHesitation: 0.3f,
                hazardMemoryDuration: 45f,
                targetPriorityMode: TargetPriorityMode.SupportFirstThenLowestHp,
                squadOrderCompliance: 0.90f,
                accuracy: LeaderAssessmentAccuracy.Accurate,
                safetyMargin: LeaderSafetyMargin.Thick,
                itemUse: ItemUseQuality.WellTimed,
                rewardWeight: 0.90f,
                successionReactionDelay: 0.15f,
                stepUpThreshold: 0.25f,
                leaderlessBehavior: LeaderlessBehavior.StayLoose
            );

            if (createdCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[AdventurerAIProfileGenerator] Verified default AdventurerAIProfile assets in {ProfilesFolder} ({createdCount} created, existing preserved).");
        }

        /// <summary>
        /// Sets ONLY the <c>targetPriorityMode</c> serialized field on the three existing profile assets
        /// (E = Nearest, D = LowestHp, C = SupportFirstThenLowestHp) without overwriting any other fields.
        /// </summary>
        [MenuItem("SBGames/AI/Apply Default Target Priority Modes")]
        public static void ApplyDefaultTargetPriorityModes()
        {
            int updatedCount = 0;
            updatedCount += SetTargetPriorityModeOnly("Profile_Rank_E", TargetPriorityMode.Nearest);
            updatedCount += SetTargetPriorityModeOnly("Profile_Rank_D", TargetPriorityMode.LowestHp);
            updatedCount += SetTargetPriorityModeOnly("Profile_Rank_C", TargetPriorityMode.SupportFirstThenLowestHp);

            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[AdventurerAIProfileGenerator] Applied targetPriorityMode on {updatedCount} profile asset(s) in {ProfilesFolder} (all other fields preserved).");
        }

        /// <summary>
        /// Sets ONLY the Phase 4 checkpoint and succession serialized fields (<c>rewardWeight</c>,
        /// <c>successionReactionDelay</c>, <c>stepUpThreshold</c>, <c>leaderlessBehavior</c>) on the three existing
        /// profile assets without overwriting any other fields.
        /// </summary>
        [MenuItem("SBGames/AI/Apply Default Phase 4 Checkpoint And Succession Fields")]
        public static void ApplyDefaultPhase4Fields()
        {
            int updatedCount = 0;
            updatedCount += SetPhase4FieldsOnly("Profile_Rank_E", 1.60f, 1.80f, 0.70f, LeaderlessBehavior.Dissolve);
            updatedCount += SetPhase4FieldsOnly("Profile_Rank_D", 1.15f, 0.90f, 0.50f, LeaderlessBehavior.StayLoose);
            updatedCount += SetPhase4FieldsOnly("Profile_Rank_C", 0.90f, 0.15f, 0.25f, LeaderlessBehavior.StayLoose);

            if (updatedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[AdventurerAIProfileGenerator] Applied Phase 4 fields on {updatedCount} profile asset(s) in {ProfilesFolder} (all other fields preserved).");
        }

        [MenuItem("SBGames/AI/Create Default Class Style Table")]
        public static void CreateDefaultClassStyleTable()
        {
            if (!AssetDatabase.IsValidFolder(ScriptableObjectsRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "AIProfiles");
            }

            string assetPath = $"{ProfilesFolder}/CharacterClassStyleTable.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterClassStyleTableSO>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[AdventurerAIProfileGenerator] CharacterClassStyleTable already exists at {assetPath} (preserved).");
                return;
            }

            var table = ScriptableObject.CreateInstance<CharacterClassStyleTableSO>();
            AssetDatabase.CreateAsset(table, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AdventurerAIProfileGenerator] Created default CharacterClassStyleTable at {assetPath}.");
        }

        [MenuItem("SBGames/AI/Create Default Leader Checkpoint Tuning")]
        public static void CreateDefaultLeaderCheckpointTuning()
        {
            if (!AssetDatabase.IsValidFolder(ScriptableObjectsRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }

            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "AIProfiles");
            }

            string assetPath = $"{ProfilesFolder}/LeaderCheckpointTuning.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LeaderCheckpointTuningSO>(assetPath);
            if (existing != null)
            {
                Debug.Log($"[AdventurerAIProfileGenerator] LeaderCheckpointTuning already exists at {assetPath} (preserved).");
                return;
            }

            var tuning = ScriptableObject.CreateInstance<LeaderCheckpointTuningSO>();
            AssetDatabase.CreateAsset(tuning, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AdventurerAIProfileGenerator] Created default LeaderCheckpointTuning at {assetPath}.");
        }

        /// <summary>
        /// Creates the default consumable effect &amp; item assets (HP Potion, Mana Potion, Status Cure),
        /// registers them in <see cref="ItemDatabaseSO"/> and <c>Items.csv</c>, creates the 3 rank kits
        /// (<c>ItemKit_Rank_E</c>, <c>ItemKit_Rank_D</c>, <c>ItemKit_Rank_C</c>), the default
        /// <see cref="ItemKitModifiers"/> asset, the default <see cref="ItemUseQualityTuningSO"/> asset,
        /// and assigns ONLY <c>startingItemKit</c> on <c>Profile_Rank_E</c>, <c>Profile_Rank_D</c>, and <c>Profile_Rank_C</c>.
        /// </summary>
        [MenuItem("SBGames/AI/Create Default Phase 5 Item Kits And Modifiers")]
        public static void CreateDefaultPhase5ItemKitsAndModifiers()
        {
            if (!AssetDatabase.IsValidFolder(ScriptableObjectsRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "AIProfiles");
            }
            if (!AssetDatabase.IsValidFolder(ItemsRootFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "Item");
            }
            if (!AssetDatabase.IsValidFolder(ConsumablesFolder))
            {
                AssetDatabase.CreateFolder(ItemsRootFolder, "Consumables");
            }

            // 1. Ensure ConsumableEffect assets exist
            var hpEffect = GetOrCreateHpEffect($"{ConsumablesFolder}/RestoreHealthEffect_Default.asset", 45f);
            var manaEffect = GetOrCreateManaEffect($"{ConsumablesFolder}/RestoreManaEffect_Default.asset", 30f);
            var cureEffect = GetOrCreateCureEffect($"{ConsumablesFolder}/CureStatusEffect_Default.asset");

            // 2. Ensure Consumable ItemDataSO assets exist (without using any [Obsolete] members)
            var hpPotion = GetOrCreateConsumableItem(
                $"{ConsumablesFolder}/ITEM_CONSUMABLE_HP_POTION.asset",
                "ITEM_CONSUMABLE_HP_POTION",
                "HP Potion",
                "Restores a moderate amount of life when consumed.",
                20,
                hpEffect);

            var manaPotion = GetOrCreateConsumableItem(
                $"{ConsumablesFolder}/ITEM_CONSUMABLE_MANA_POTION.asset",
                "ITEM_CONSUMABLE_MANA_POTION",
                "Mana Potion",
                "Restores mana to spellcasters and healers when consumed.",
                25,
                manaEffect);

            var statusCure = GetOrCreateConsumableItem(
                $"{ConsumablesFolder}/ITEM_CONSUMABLE_STATUS_CURE.asset",
                "ITEM_CONSUMABLE_STATUS_CURE",
                "Status Cure",
                "Cures active harmful status effects when consumed.",
                18,
                cureEffect);

            // 3. Register new items in ItemDatabase.asset and sync Items.csv
            DatabaseAutoIndexer.RebuildAllCatalogs();
            DatabaseCsvPipeline.ExportItemsCsv();

            // 4. Create the 3 starting rank kits (E = 1 HP, D = 2 HP + 1 Cure, C = 3 HP + 2 Cure)
            var kitE = GetOrCreateRankKit(
                $"{ProfilesFolder}/ItemKit_Rank_E.asset",
                new AdventurerItemKitEntry[]
                {
                    new AdventurerItemKitEntry(hpPotion, 1)
                });

            var kitD = GetOrCreateRankKit(
                $"{ProfilesFolder}/ItemKit_Rank_D.asset",
                new AdventurerItemKitEntry[]
                {
                    new AdventurerItemKitEntry(hpPotion, 2),
                    new AdventurerItemKitEntry(statusCure, 1)
                });

            var kitC = GetOrCreateRankKit(
                $"{ProfilesFolder}/ItemKit_Rank_C.asset",
                new AdventurerItemKitEntry[]
                {
                    new AdventurerItemKitEntry(hpPotion, 3),
                    new AdventurerItemKitEntry(statusCure, 2)
                });

            // 5. Create DefaultItemKitModifiers.asset
            string modifiersPath = $"{ProfilesFolder}/DefaultItemKitModifiers.asset";
            var modifiers = AssetDatabase.LoadAssetAtPath<ItemKitModifiers>(modifiersPath);
            if (modifiers == null)
            {
                modifiers = ScriptableObject.CreateInstance<ItemKitModifiers>();
                AssetDatabase.CreateAsset(modifiers, modifiersPath);
            }
            modifiers.DefaultHpPotionItem = hpPotion;
            modifiers.DefaultManaPotionItem = manaPotion;
            modifiers.DefaultStatusCureItem = statusCure;
            EditorUtility.SetDirty(modifiers);

            // 6. Create ItemUseQualityTuning.asset
            string qualityTuningPath = $"{ProfilesFolder}/ItemUseQualityTuning.asset";
            var qualityTuning = AssetDatabase.LoadAssetAtPath<ItemUseQualityTuningSO>(qualityTuningPath);
            if (qualityTuning == null)
            {
                qualityTuning = ScriptableObject.CreateInstance<ItemUseQualityTuningSO>();
                AssetDatabase.CreateAsset(qualityTuning, qualityTuningPath);
                EditorUtility.SetDirty(qualityTuning);
            }

            // 7. Assign ONLY startingItemKit on Profile_Rank_E, Profile_Rank_D, and Profile_Rank_C
            int updatedProfiles = 0;
            updatedProfiles += SetStartingItemKitOnly("Profile_Rank_E", kitE);
            updatedProfiles += SetStartingItemKitOnly("Profile_Rank_D", kitD);
            updatedProfiles += SetStartingItemKitOnly("Profile_Rank_C", kitC);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AdventurerAIProfileGenerator] Created/verified Phase 5 consumable items, rank kits, DefaultItemKitModifiers, ItemUseQualityTuning, and assigned startingItemKit on {updatedProfiles} profile(s).");
        }

        [MenuItem("SBGames/AI/Create Default Phase 6 Room Loop Tuning")]
        public static void CreateDefaultPhase6RoomLoopTuning()
        {
            if (!AssetDatabase.IsValidFolder(ScriptableObjectsRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                AssetDatabase.CreateFolder(ScriptableObjectsRoot, "AIProfiles");
            }

            var tuningE = GetOrCreateRoomLoopTuning($"{ProfilesFolder}/RoomLoopTuning_Rank_E.asset", CharacterRank.E);
            var tuningD = GetOrCreateRoomLoopTuning($"{ProfilesFolder}/RoomLoopTuning_Rank_D.asset", CharacterRank.D);
            var tuningC = GetOrCreateRoomLoopTuning($"{ProfilesFolder}/RoomLoopTuning_Rank_C.asset", CharacterRank.C);

            int updatedProfiles = 0;
            updatedProfiles += SetRoomLoopTuningOnly("Profile_Rank_E", tuningE);
            updatedProfiles += SetRoomLoopTuningOnly("Profile_Rank_D", tuningD);
            updatedProfiles += SetRoomLoopTuningOnly("Profile_Rank_C", tuningC);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AdventurerAIProfileGenerator] Created/verified Phase 6 RoomLoopTuning assets and assigned to {updatedProfiles} profile(s).");
        }

        private static RoomLoopTuningSO GetOrCreateRoomLoopTuning(string path, CharacterRank rank)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<RoomLoopTuningSO>(path);
            if (tuning == null)
            {
                tuning = RoomLoopTuningSO.CreateDefault(rank);
                AssetDatabase.CreateAsset(tuning, path);
            }
            return tuning;
        }

        private static int SetRoomLoopTuningOnly(string profileAssetName, RoomLoopTuningSO tuning)
        {
            string assetPath = $"{ProfilesFolder}/{profileAssetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>(assetPath);
            if (existing == null)
            {
                return 0;
            }

            var so = new SerializedObject(existing);
            var prop = so.FindProperty("roomLoopTuning");
            if (prop == null)
            {
                return 0;
            }

            prop.objectReferenceValue = tuning;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            return 1;
        }

        private static RestoreHealthEffect GetOrCreateHpEffect(string path, float healAmount)
        {
            var effect = AssetDatabase.LoadAssetAtPath<RestoreHealthEffect>(path);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<RestoreHealthEffect>();
                effect.HealAmount = healAmount;
                AssetDatabase.CreateAsset(effect, path);
            }
            return effect;
        }

        private static RestoreManaEffect GetOrCreateManaEffect(string path, float manaAmount)
        {
            var effect = AssetDatabase.LoadAssetAtPath<RestoreManaEffect>(path);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<RestoreManaEffect>();
                effect.ManaAmount = manaAmount;
                AssetDatabase.CreateAsset(effect, path);
            }
            return effect;
        }

        private static CureStatusEffect GetOrCreateCureEffect(string path)
        {
            var effect = AssetDatabase.LoadAssetAtPath<CureStatusEffect>(path);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<CureStatusEffect>();
                AssetDatabase.CreateAsset(effect, path);
            }
            return effect;
        }

        private static ItemDataSO GetOrCreateConsumableItem(
            string path,
            string id,
            string displayName,
            string description,
            int baseValue,
            ConsumableEffect effect)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDataSO>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            so.FindProperty("itemID").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("itemType").intValue = (int)ItemType.Consumable;
            so.FindProperty("equippableSlotType").intValue = (int)EquipmentSlotType.None;
            so.FindProperty("maxStack").intValue = 99;
            so.FindProperty("isStackable").boolValue = true;
            so.FindProperty("baseValue").intValue = baseValue;
            so.FindProperty("consumableEffect").objectReferenceValue = effect;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static AdventurerItemKit GetOrCreateRankKit(string path, IReadOnlyList<AdventurerItemKitEntry> entries)
        {
            var kit = AssetDatabase.LoadAssetAtPath<AdventurerItemKit>(path);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<AdventurerItemKit>();
                kit.SetEntries(entries);
                AssetDatabase.CreateAsset(kit, path);
            }
            else
            {
                kit.SetEntries(entries);
                EditorUtility.SetDirty(kit);
            }
            return kit;
        }

        private static int SetStartingItemKitOnly(string profileAssetName, AdventurerItemKit kit)
        {
            string assetPath = $"{ProfilesFolder}/{profileAssetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>(assetPath);
            if (existing == null)
            {
                return 0;
            }

            var so = new SerializedObject(existing);
            var kitProp = so.FindProperty("startingItemKit");
            if (kitProp == null)
            {
                return 0;
            }

            kitProp.objectReferenceValue = kit;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            return 1;
        }

        private static int SetTargetPriorityModeOnly(string assetName, TargetPriorityMode mode)
        {
            string assetPath = $"{ProfilesFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>(assetPath);
            if (existing == null)
            {
                return 0;
            }

            var so = new SerializedObject(existing);
            var prop = so.FindProperty("targetPriorityMode");
            if (prop == null)
            {
                return 0;
            }

            prop.intValue = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            return 1;
        }

        private static int SetPhase4FieldsOnly(
            string assetName,
            float rewardWeight,
            float successionReactionDelay,
            float stepUpThreshold,
            LeaderlessBehavior leaderlessBehavior)
        {
            string assetPath = $"{ProfilesFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>(assetPath);
            if (existing == null)
            {
                return 0;
            }

            var so = new SerializedObject(existing);
            var rewardProp = so.FindProperty("rewardWeight");
            var delayProp = so.FindProperty("successionReactionDelay");
            var thresholdProp = so.FindProperty("stepUpThreshold");
            var leaderlessProp = so.FindProperty("leaderlessBehavior");

            if (rewardProp != null) rewardProp.floatValue = rewardWeight;
            if (delayProp != null) delayProp.floatValue = successionReactionDelay;
            if (thresholdProp != null) thresholdProp.floatValue = stepUpThreshold;
            if (leaderlessProp != null) leaderlessProp.intValue = (int)leaderlessBehavior;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            return 1;
        }

        private static int CreateProfileIfMissing(
            string assetName,
            CharacterRank rank,
            float decisionInterval,
            float visionRadius,
            float fleeHpThreshold,
            float doorwayHesitation,
            float hazardMemoryDuration,
            TargetPriorityMode targetPriorityMode,
            float squadOrderCompliance,
            LeaderAssessmentAccuracy accuracy,
            LeaderSafetyMargin safetyMargin,
            ItemUseQuality itemUse,
            float rewardWeight,
            float successionReactionDelay,
            float stepUpThreshold,
            LeaderlessBehavior leaderlessBehavior)
        {
            string assetPath = $"{ProfilesFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>(assetPath);
            if (existing != null)
            {
                return 0;
            }

            var profile = ScriptableObject.CreateInstance<AdventurerAIProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("rank").intValue = (int)rank;
            so.FindProperty("decisionInterval").floatValue = decisionInterval;
            so.FindProperty("visionRadius").floatValue = visionRadius;
            so.FindProperty("individualFleeHpThreshold").floatValue = fleeHpThreshold;
            so.FindProperty("doorwayHesitation").floatValue = doorwayHesitation;
            so.FindProperty("hazardMemoryDuration").floatValue = hazardMemoryDuration;
            so.FindProperty("targetPriorityMode").intValue = (int)targetPriorityMode;
            so.FindProperty("squadOrderCompliance").floatValue = squadOrderCompliance;
            so.FindProperty("leaderAssessmentAccuracy").intValue = (int)accuracy;
            so.FindProperty("leaderSafetyMargin").intValue = (int)safetyMargin;
            so.FindProperty("itemUseQuality").intValue = (int)itemUse;
            so.FindProperty("rewardWeight").floatValue = rewardWeight;
            so.FindProperty("successionReactionDelay").floatValue = successionReactionDelay;
            so.FindProperty("stepUpThreshold").floatValue = stepUpThreshold;
            so.FindProperty("leaderlessBehavior").intValue = (int)leaderlessBehavior;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, assetPath);
            return 1;
        }
    }
}
