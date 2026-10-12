using UnityEditor;
using UnityEngine;

namespace SBGames.Characters.AI.Editor
{
    public static class AdventurerAIProfileGenerator
    {
        private const string ScriptableObjectsRoot = "Assets/ScriptableObjects";
        private const string ProfilesFolder = "Assets/ScriptableObjects/AIProfiles";

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
                itemUse: ItemUseQuality.LateWasteful
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
                itemUse: ItemUseQuality.Adequate
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
                itemUse: ItemUseQuality.WellTimed
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
            ItemUseQuality itemUse)
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
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, assetPath);
            return 1;
        }
    }
}
