using System.Collections.Generic;
using SBGames.Events;
using SBGames.Events.Types;
using SBGames.Personality.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Personality.Editor
{
    public static class PersonalityAssetGenerator
    {
        [MenuItem("SBGames/Personality/Generate Default Assets")]
        public static void GenerateDefaultAssets()
        {
            string persDir = "Assets/ScriptableObjects/Personality";
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(persDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Personality");
            if (!AssetDatabase.IsValidFolder(persDir + "/Races"))
                AssetDatabase.CreateFolder(persDir, "Races");
            if (!AssetDatabase.IsValidFolder(persDir + "/Traits"))
                AssetDatabase.CreateFolder(persDir, "Traits");

            string evtDir = "Assets/ScriptableObjects/Events";
            if (!AssetDatabase.IsValidFolder(evtDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Events");

            // 1. Create Races
            var raceUndead = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var soUndead = new SerializedObject(raceUndead);
            soUndead.FindProperty("raceId").stringValue = "Undead";
            soUndead.FindProperty("displayName").stringValue = "Undead";
            soUndead.FindProperty("description").stringValue = "Fear is locked to 0. Immune to panic and retreat.";
            soUndead.FindProperty("lockFear").boolValue = true;
            soUndead.FindProperty("lockedFearValue").floatValue = 0f;
            soUndead.FindProperty("fearIntakeMultiplier").floatValue = 0f;
            soUndead.FindProperty("immuneToPanic").boolValue = true;
            soUndead.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(raceUndead, persDir + "/Races/Race_Undead.asset");

            var raceBeast = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var soBeast = new SerializedObject(raceBeast);
            soBeast.FindProperty("raceId").stringValue = "Beast";
            soBeast.FindProperty("displayName").stringValue = "Beast";
            soBeast.FindProperty("description").stringValue = "Instinctively fixates on wounded targets. High ferocity.";
            soBeast.FindProperty("woundedTargetPriority").floatValue = 1.7f;
            soBeast.FindProperty("baselineSurvivalHpThreshold").floatValue = 0.15f;
            soBeast.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(raceBeast, persDir + "/Races/Race_Beast.asset");

            var raceGoblinoid = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var soGoblin = new SerializedObject(raceGoblinoid);
            soGoblin.FindProperty("raceId").stringValue = "Goblinoid";
            soGoblin.FindProperty("displayName").stringValue = "Goblinoid";
            soGoblin.FindProperty("description").stringValue = "Easily startled and prone to panic. Flees when wounded.";
            soGoblin.FindProperty("fearIntakeMultiplier").floatValue = 1.8f;
            soGoblin.FindProperty("baselineSurvivalHpThreshold").floatValue = 0.40f;
            soGoblin.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(raceGoblinoid, persDir + "/Races/Race_Goblinoid.asset");

            // 2. Create Traits
            var traits = new List<CharacterTraitSO>();

            var tReckless = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soR = new SerializedObject(tReckless);
            soR.FindProperty("traitId").stringValue = "Reckless";
            soR.FindProperty("displayName").stringValue = "Reckless";
            soR.FindProperty("description").stringValue = "Fear is eliminated. Fires AoE skills even on a single target. Never retreats.";
            soR.FindProperty("threatMultiplier").floatValue = 1.4f;
            soR.FindProperty("fearMultiplier").floatValue = 0f;
            soR.FindProperty("zeroFear").boolValue = true;
            soR.FindProperty("neverFlee").boolValue = true;
            soR.FindProperty("clusterCountOffset").intValue = -2;
            soR.FindProperty("badgeColor").colorValue = new Color(0.95f, 0.25f, 0.2f, 1f);
            soR.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tReckless, persDir + "/Traits/Trait_Reckless.asset");
            traits.Add(tReckless);

            var tCowardly = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soC = new SerializedObject(tCowardly);
            soC.FindProperty("traitId").stringValue = "Cowardly";
            soC.FindProperty("displayName").stringValue = "Cowardly";
            soC.FindProperty("description").stringValue = "Doubles fear generation. Panic triggers at 50% HP.";
            soC.FindProperty("threatMultiplier").floatValue = 0.7f;
            soC.FindProperty("fearMultiplier").floatValue = 2.0f;
            soC.FindProperty("survivalHpThreshold").floatValue = 0.50f;
            soC.FindProperty("badgeColor").colorValue = new Color(0.4f, 0.7f, 0.95f, 1f);
            soC.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tCowardly, persDir + "/Traits/Trait_Cowardly.asset");
            traits.Add(tCowardly);

            var tGuardian = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soG = new SerializedObject(tGuardian);
            soG.FindProperty("traitId").stringValue = "Guardian";
            soG.FindProperty("displayName").stringValue = "Guardian";
            soG.FindProperty("description").stringValue = "Prioritizes protecting allies. Boosts defensive and peel skills when an ally is critically wounded.";
            soG.FindProperty("peelingHealthThreshold").floatValue = 0.35f;
            soG.FindProperty("peelingBonusMultiplier").floatValue = 1.8f;
            soG.FindProperty("badgeColor").colorValue = new Color(0.3f, 0.85f, 0.5f, 1f);
            soG.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tGuardian, persDir + "/Traits/Trait_Guardian.asset");
            traits.Add(tGuardian);

            var tOpportunist = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soO = new SerializedObject(tOpportunist);
            soO.FindProperty("traitId").stringValue = "Opportunist";
            soO.FindProperty("displayName").stringValue = "Opportunist";
            soO.FindProperty("description").stringValue = "Favors ranged kiting and strikes against wounded foes.";
            soO.FindProperty("rangePreferenceMultiplier").floatValue = 1.6f;
            soO.FindProperty("executionHealthThreshold").floatValue = 0.30f;
            soO.FindProperty("executionBonusMultiplier").floatValue = 1.35f;
            soO.FindProperty("badgeColor").colorValue = new Color(0.85f, 0.75f, 0.2f, 1f);
            soO.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tOpportunist, persDir + "/Traits/Trait_Opportunist.asset");
            traits.Add(tOpportunist);

            var tPragmatic = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soP = new SerializedObject(tPragmatic);
            soP.FindProperty("traitId").stringValue = "Pragmatic";
            soP.FindProperty("displayName").stringValue = "Pragmatic";
            soP.FindProperty("description").stringValue = "Disciplined combatant. Conserves AoE skills until at least 3 enemies are grouped.";
            soP.FindProperty("clusterCountOffset").intValue = 1;
            soP.FindProperty("badgeColor").colorValue = new Color(0.65f, 0.5f, 0.85f, 1f);
            soP.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tPragmatic, persDir + "/Traits/Trait_Pragmatic.asset");
            traits.Add(tPragmatic);

            var tBloodthirsty = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soB = new SerializedObject(tBloodthirsty);
            soB.FindProperty("traitId").stringValue = "Bloodthirsty";
            soB.FindProperty("displayName").stringValue = "Bloodthirsty";
            soB.FindProperty("description").stringValue = "Frenzied executioner. Massive skill priority boost against targets below 30% HP.";
            soB.FindProperty("executionHealthThreshold").floatValue = 0.30f;
            soB.FindProperty("executionBonusMultiplier").floatValue = 1.75f;
            soB.FindProperty("lowHealthTargetPriority").floatValue = 1.8f;
            soB.FindProperty("badgeColor").colorValue = new Color(0.9f, 0.15f, 0.35f, 1f);
            soB.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(tBloodthirsty, persDir + "/Traits/Trait_Bloodthirsty.asset");
            traits.Add(tBloodthirsty);

            // 3. Create PersonalityDatabase
            var pDb = ScriptableObject.CreateInstance<PersonalityDatabaseSO>();
            var soDb = new SerializedObject(pDb);
            var traitsProp = soDb.FindProperty("availableTraits");
            traitsProp.arraySize = traits.Count;
            for (int i = 0; i < traits.Count; i++)
            {
                traitsProp.GetArrayElementAtIndex(i).objectReferenceValue = traits[i];
            }

            var racesProp = soDb.FindProperty("availableRaces");
            racesProp.arraySize = 3;
            racesProp.GetArrayElementAtIndex(0).objectReferenceValue = raceUndead;
            racesProp.GetArrayElementAtIndex(1).objectReferenceValue = raceBeast;
            racesProp.GetArrayElementAtIndex(2).objectReferenceValue = raceGoblinoid;

            soDb.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pDb, persDir + "/PersonalityDatabase.asset");

            // 4. Create Events
            var altarEvt = ScriptableObject.CreateInstance<AltarEventSO>();
            var soAltar = new SerializedObject(altarEvt);
            soAltar.FindProperty("eventId").stringValue = "Event_AltarOfTransmutation";
            soAltar.FindProperty("eventTitle").stringValue = "Altar of Transmutation";
            soAltar.FindProperty("eventSubtitle").stringValue = "Ancient Dark Ritual";
            soAltar.FindProperty("flavorDescription").stringValue = "An obsidian monolith surfaces from the dungeon depths, offering to reshape the instincts of your minions.";
            soAltar.FindProperty("minRound").intValue = 2;
            soAltar.FindProperty("bloodCost").intValue = 10;
            soAltar.FindProperty("energyCost").intValue = 25;
            soAltar.FindProperty("bloodAltarChance").floatValue = 0.5f;
            soAltar.FindProperty("draftOptionCount").intValue = 3;
            soAltar.FindProperty("personalityDatabase").objectReferenceValue = pDb;
            soAltar.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(altarEvt, evtDir + "/Event_AltarOfTransmutation.asset");

            var boonEvt = ScriptableObject.CreateInstance<DarkBoonEventSO>();
            var soBoon = new SerializedObject(boonEvt);
            soBoon.FindProperty("eventId").stringValue = "Event_DarkBoon";
            soBoon.FindProperty("eventTitle").stringValue = "Dark Boon";
            soBoon.FindProperty("eventSubtitle").stringValue = "Essence Pact";
            soBoon.FindProperty("flavorDescription").stringValue = "The dungeon's dark leylines surge. Sacrifice vital essence in exchange for dark energy.";
            soBoon.FindProperty("minRound").intValue = 3;
            soBoon.FindProperty("essenceSacrifice").intValue = 15;
            soBoon.FindProperty("energyReward").intValue = 40;
            soBoon.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(boonEvt, evtDir + "/Event_DarkBoon.asset");

            var evtDb = ScriptableObject.CreateInstance<EventDatabaseSO>();
            var soEvtDb = new SerializedObject(evtDb);
            var evtsProp = soEvtDb.FindProperty("events");
            evtsProp.arraySize = 2;
            evtsProp.GetArrayElementAtIndex(0).objectReferenceValue = altarEvt;
            evtsProp.GetArrayElementAtIndex(1).objectReferenceValue = boonEvt;
            soEvtDb.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(evtDb, evtDir + "/EventDatabase.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PersonalityAssetGenerator] All default Personality and Event assets created successfully!");
        }
    }
}
