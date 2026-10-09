using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Events;
using SBGames.Events.Types;
using SBGames.Loop;
using SBGames.Personality;
using SBGames.Personality.Altar;
using SBGames.Personality.Data;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class CharacterPersonalityAndTraitTests
    {
        private GameObject _holderGo;

        [SetUp]
        public void SetUp()
        {
            _holderGo = new GameObject("Test_PersonalityHolder");
        }

        [TearDown]
        public void TearDown()
        {
            if (_holderGo != null)
            {
                Object.DestroyImmediate(_holderGo);
            }
        }

        [Test]
        public void Snapshot_RecklessTrait_LocksFearAndIgnoresSurvival()
        {
            var race = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();

            // Configure reckless trait via reflection or snapshot builder
            var so = new UnityEditor.SerializedObject(trait);
            so.FindProperty("traitId").stringValue = "Reckless";
            so.FindProperty("zeroFear").boolValue = true;
            so.FindProperty("neverFlee").boolValue = true;
            so.FindProperty("clusterCountOffset").intValue = -2;
            so.FindProperty("threatMultiplier").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var snap = trait.BuildSnapshot(race);

            Assert.IsTrue(snap.IsFearLocked, "Reckless should lock fear.");
            Assert.AreEqual(0f, snap.LockedFearValue, "Reckless locked fear value should be 0.");
            Assert.IsTrue(snap.IgnoreSurvivalGoal, "Reckless should ignore survival.");
            Assert.AreEqual(-2, snap.ClusterCountOffset, "Cluster offset should be -2.");
            Assert.AreEqual(1.5f, snap.ThreatMultiplier, "Threat multiplier should be 1.5.");
        }

        [Test]
        public void Snapshot_UndeadRace_LocksFearRegardlessOfTrait()
        {
            var race = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var soRace = new UnityEditor.SerializedObject(race);
            soRace.FindProperty("lockFear").boolValue = true;
            soRace.FindProperty("lockedFearValue").floatValue = 0f;
            soRace.FindProperty("immuneToPanic").boolValue = true;
            soRace.ApplyModifiedPropertiesWithoutUndo();

            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soTrait = new UnityEditor.SerializedObject(trait);
            soTrait.FindProperty("fearMultiplier").floatValue = 2.0f; // Cowardly trait
            soTrait.ApplyModifiedPropertiesWithoutUndo();

            var snap = trait.BuildSnapshot(race);

            Assert.IsTrue(snap.IsFearLocked, "Undead race should permanently lock fear.");
            Assert.AreEqual(0f, snap.FearMultiplier, "Fear multiplier should be 0 for fear-locked Undead.");
            Assert.IsTrue(snap.IgnoreSurvivalGoal, "Undead race should be immune to panic.");
        }

        [Test]
        public void CharacterPersonality_SetTrait_UpdatesSnapshotAndInvokesEvent()
        {
            var personality = _holderGo.AddComponent<CharacterPersonality>();

            var race = ScriptableObject.CreateInstance<RaceInstinctSO>();
            var traitA = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soA = new UnityEditor.SerializedObject(traitA);
            soA.FindProperty("traitId").stringValue = "TraitA";
            soA.FindProperty("threatMultiplier").floatValue = 1.0f;
            soA.ApplyModifiedPropertiesWithoutUndo();

            var traitB = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soB = new UnityEditor.SerializedObject(traitB);
            soB.FindProperty("traitId").stringValue = "TraitB";
            soB.FindProperty("threatMultiplier").floatValue = 2.5f;
            soB.ApplyModifiedPropertiesWithoutUndo();

            personality.Initialize(race, traitA);
            Assert.AreEqual(1.0f, personality.Snapshot.ThreatMultiplier);

            bool eventFired = false;
            CharacterTraitSO oldFired = null;
            CharacterTraitSO newFired = null;
            personality.OnTraitChanged += (o, n) =>
            {
                eventFired = true;
                oldFired = o;
                newFired = n;
            };

            personality.SetTrait(traitB);

            Assert.IsTrue(eventFired, "OnTraitChanged event should be invoked.");
            Assert.AreEqual(traitA, oldFired);
            Assert.AreEqual(traitB, newFired);
            Assert.AreEqual(2.5f, personality.Snapshot.ThreatMultiplier, "Snapshot should be updated immediately with TraitB values.");
        }

        [Test]
        public void UtilitySurviveGoal_RecklessUnit_ScoresZero()
        {
            var personality = _holderGo.AddComponent<CharacterPersonality>();
            var ai = _holderGo.AddComponent<BaseAI>();

            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var so = new UnityEditor.SerializedObject(trait);
            so.FindProperty("neverFlee").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            personality.Initialize(null, trait);

            var surviveGoal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();
            ai.SetAgentLife(10f); // Low HP

            float score = surviveGoal.Evaluate(ai);
            Assert.AreEqual(0f, score, "Reckless unit with IgnoreSurvivalGoal should score 0 on UtilitySurviveGoal.");
        }

        [Test]
        public void AltarEventManager_AffordabilityCheck_ValidatesBloodAndEnergy()
        {
            var dmGo = new GameObject("Test_DM");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var altarMgrGo = new GameObject("Test_AltarMgr");
            var altarMgr = altarMgrGo.AddComponent<AltarEventManager>();

            var altarEvent = ScriptableObject.CreateInstance<AltarEventSO>();
            var so = new UnityEditor.SerializedObject(altarEvent);
            so.FindProperty("bloodCost").intValue = 10;
            so.FindProperty("energyCost").intValue = 60; // More than starting energy (50)
            so.ApplyModifiedPropertiesWithoutUndo();

            // Test Energy unaffordable
            var soMgr = new UnityEditor.SerializedObject(altarMgr);
            soMgr.FindProperty("_activeCurrencyType").enumValueIndex = (int)AltarCurrencyType.Arcane;
            soMgr.FindProperty("_activeCost").intValue = 60;
            soMgr.ApplyModifiedPropertiesWithoutUndo();

            bool canAffordEnergy = altarMgr.CanAffordCost(out var reasonEnergy);
            Assert.IsFalse(canAffordEnergy, "Should not afford 60 energy with 50 current energy.");
            Assert.IsTrue(reasonEnergy.Contains("Energy"));

            // Test Blood affordable
            soMgr.FindProperty("_activeCurrencyType").enumValueIndex = (int)AltarCurrencyType.Blood;
            soMgr.FindProperty("_activeCost").intValue = 10;
            soMgr.ApplyModifiedPropertiesWithoutUndo();

            bool canAffordBlood = altarMgr.CanAffordCost(out var reasonBlood);
            Assert.IsTrue(canAffordBlood, "Should afford 10 essence with 100 current essence.");

            Object.DestroyImmediate(dmGo);
            Object.DestroyImmediate(altarMgrGo);
        }

        [Test]
        public void DungeonEventManager_RoundGating_PreventsEarlyTrigger()
        {
            var mgrGo = new GameObject("Test_EventMgr");
            var mgr = mgrGo.AddComponent<DungeonEventManager>();

            var so = new UnityEditor.SerializedObject(mgr);
            so.FindProperty("minRoundToStart").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Round 1 and 2 should not trigger
            bool triggeredRound1 = mgr.TriggerRandomEvent(1);
            Assert.IsFalse(triggeredRound1, "Event should not trigger on round 1 when minRound is 3.");

            bool triggeredRound2 = mgr.TriggerRandomEvent(2);
            Assert.IsFalse(triggeredRound2, "Event should not trigger on round 2 when minRound is 3.");

            Object.DestroyImmediate(mgrGo);
        }

        [Test]
        public void AltarEventManager_ConfirmTransmutation_FailsGracefully_WhenTargetUnitDestroyed()
        {
            var dmGo = new GameObject("Test_DM");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var altarMgrGo = new GameObject("Test_AltarMgr");
            var altarMgr = altarMgrGo.AddComponent<AltarEventManager>();

            var unitGo = new GameObject("Test_TargetUnit");
            var pers = unitGo.AddComponent<CharacterPersonality>();

            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();

            var target = new AltarUnitTarget("Test_TargetUnit", true, Vector2Int.zero, -1, pers, null, null);
            altarMgr.SelectUnit(target);

            // Destroy the unit's GameObject before confirmation
            Object.DestroyImmediate(unitGo);

            Assert.IsFalse(target.IsValid, "Target should not be valid once GameObject is destroyed.");

            bool result = altarMgr.ConfirmTransmutation(trait, keepCurrentTrait: false);
            Assert.IsFalse(result, "ConfirmTransmutation must return false when unit is destroyed without throwing exception.");
            Assert.AreEqual(100, dm.Essence, "Essence must not be deducted when unit is destroyed.");

            Object.DestroyImmediate(dmGo);
            Object.DestroyImmediate(altarMgrGo);
            Object.DestroyImmediate(trait);
        }

        [Test]
        public void AltarEventManager_ConfirmTransmutation_DeductsCurrencyWithFallbackDM()
        {
            var dmGo = new GameObject("Test_DM");
            var dm = dmGo.AddComponent<DungeonMaster>();
            dm.Initialize(startingEssence: 100, startingEnergy: 50);

            var altarMgrGo = new GameObject("Test_AltarMgr");
            var altarMgr = altarMgrGo.AddComponent<AltarEventManager>();

            var unitGo = new GameObject("Test_TargetUnit");
            var pers = unitGo.AddComponent<CharacterPersonality>();
            var trait = ScriptableObject.CreateInstance<CharacterTraitSO>();

            var target = new AltarUnitTarget("Test_TargetUnit", true, Vector2Int.zero, -1, pers, null, null);
            altarMgr.SelectUnit(target);

            var soMgr = new UnityEditor.SerializedObject(altarMgr);
            soMgr.FindProperty("_activeCurrencyType").enumValueIndex = (int)AltarCurrencyType.Blood;
            soMgr.FindProperty("_activeCost").intValue = 25;
            soMgr.ApplyModifiedPropertiesWithoutUndo();

            bool result = altarMgr.ConfirmTransmutation(trait, keepCurrentTrait: false);
            Assert.IsTrue(result, "ConfirmTransmutation should succeed with valid unit and sufficient currency.");
            Assert.AreEqual(75, dm.Essence, "Essence should be reduced by 25.");
            Assert.AreSame(trait, pers.ActiveTrait, "Unit should have the new trait applied.");

            Object.DestroyImmediate(unitGo);
            Object.DestroyImmediate(dmGo);
            Object.DestroyImmediate(altarMgrGo);
            Object.DestroyImmediate(trait);
        }

        [Test]
        public void DungeonEventManager_ResolveActiveEvent_ForceClosesAltarUI()
        {
            var altarMgrGo = new GameObject("Test_AltarMgr");
            var altarMgr = altarMgrGo.AddComponent<AltarEventManager>();

            var mgrGo = new GameObject("Test_EventMgr");
            var mgr = mgrGo.AddComponent<DungeonEventManager>();

            // Assert that calling ResolveActiveEvent does not throw even when no active event
            Assert.DoesNotThrow(() => mgr.ResolveActiveEvent());

            Object.DestroyImmediate(altarMgrGo);
            Object.DestroyImmediate(mgrGo);
        }
    }
}
