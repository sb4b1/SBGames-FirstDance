#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Micosmo.SensorToolkit;
using NUnit.Framework;
using SBGames.Characters.AI;
using SBGames.Database.CSV;
using SBGames.Inventory.Core.Effects;
using SBGames.Inventory.Data;
using SBGames.Loop;
using SBGames.Loop.Talents;
using SBGames.Personality;
using SBGames.Personality.Data;
using UnityEditor;
using UnityEngine;

namespace SBGames.Tests
{
    [TestFixture]
    public class AdventurerAIProfileTests
    {
        private GameObject _rootGo;

        [SetUp]
        public void SetUp()
        {
            SBGames.Dungeon.Markers.DungeonRoomRegistry.Clear();
            _rootGo = new GameObject("Test_Adventurer");
        }

        [TearDown]
        public void TearDown()
        {
            SBGames.Dungeon.Markers.DungeonRoomRegistry.Clear();
            if (_rootGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_rootGo);
            }
        }

        [Test]
        public void GetAdjustedFleeThreshold_ClampsPersonalityMultiplierTo15Percent_AndZeroAlloc()
        {
            var profile = ScriptableObject.CreateInstance<AdventurerAIProfile>();
            var so = new SerializedObject(profile);
            so.FindProperty("individualFleeHpThreshold").floatValue = 0.40f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Exact 1.0 multiplier -> 0.40
            Assert.AreEqual(0.40f, profile.GetAdjustedFleeThreshold(1.0f), 0.0001f);

            // Extreme high multiplier (2.0) clamped to 1.15 -> 0.40 * 1.15 = 0.46
            Assert.AreEqual(0.46f, profile.GetAdjustedFleeThreshold(2.0f), 0.0001f);

            // Extreme low multiplier (0.0) clamped to 0.85 -> 0.40 * 0.85 = 0.34
            Assert.AreEqual(0.34f, profile.GetAdjustedFleeThreshold(0.0f), 0.0001f);

            // Warm up JIT
            for (int i = 0; i < 20; i++)
            {
                profile.GetAdjustedFleeThreshold(1.1f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetTotalMemory(true);
            for (int i = 0; i < 500; i++)
            {
                profile.GetAdjustedFleeThreshold(1.05f);
            }
            long after = GC.GetTotalMemory(false);

            Assert.LessOrEqual(after - before, 0, "GetAdjustedFleeThreshold must not allocate managed memory.");
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void ApplyAdventurerProfile_NullProfile_LeavesDefaultsUntouched()
        {
            var throttler = _rootGo.AddComponent<AIDecisionThrottler>();
            var memory = _rootGo.AddComponent<CharacterAIMemory>();
            var staging = _rootGo.AddComponent<DoorwayStagingController>();
            var rangeSensor = _rootGo.AddComponent<RangeSensor2D>();
            rangeSensor.Circle.Radius = 8f;
            var vision = _rootGo.AddComponent<VisionController>();
            var baseAI = _rootGo.GetComponent<BaseAI>() ?? _rootGo.AddComponent<BaseAI>();

            float defaultInterval = throttler.DecisionInterval;
            float defaultHazard = memory.DefaultHazardDuration;
            float defaultMinHesitation = staging.MinHesitationDuration;
            float defaultMaxHesitation = staging.MaxHesitationDuration;
            float defaultSurvival = baseAI.SurvivalHpThreshold;

            baseAI.ApplyAdventurerProfile(null);

            Assert.IsNull(baseAI.AdventurerProfile);
            Assert.AreEqual(defaultInterval, throttler.DecisionInterval, 0.0001f);
            Assert.AreEqual(8f, rangeSensor.Circle.Radius, 0.0001f);
            Assert.AreEqual(defaultHazard, memory.DefaultHazardDuration, 0.0001f);
            Assert.AreEqual(defaultMinHesitation, staging.MinHesitationDuration, 0.0001f);
            Assert.AreEqual(defaultMaxHesitation, staging.MaxHesitationDuration, 0.0001f);
            Assert.AreEqual(defaultSurvival, baseAI.SurvivalHpThreshold, 0.0001f);
        }

        [Test]
        public void ApplyAdventurerProfile_AppliesAllSubsystemValues_AndReappliesOnPoolSpawn()
        {
            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            var brain = _rootGo.AddComponent<CharacterBrain>();
            var personality = _rootGo.AddComponent<CharacterPersonality>();
            var throttler = _rootGo.AddComponent<AIDecisionThrottler>();
            var memory = _rootGo.AddComponent<CharacterAIMemory>();
            var staging = _rootGo.AddComponent<DoorwayStagingController>();

            var rangeSensor = _rootGo.AddComponent<RangeSensor2D>();
            var losSensor = _rootGo.AddComponent<LOSSensor2D>();
            losSensor.InputSensor = rangeSensor;
            losSensor.LimitDistance = true;

            var vision = _rootGo.AddComponent<VisionController>();
            var soVision = new SerializedObject(vision);
            soVision.FindProperty("sensor").objectReferenceValue = losSensor;
            soVision.ApplyModifiedPropertiesWithoutUndo();

            var baseAI = _rootGo.GetComponent<BaseAI>() ?? _rootGo.AddComponent<BaseAI>();

            // Configure Cowardly trait (survivalHpThreshold = 0.50f -> ratio 2.0 -> clamped to 1.15x)
            var cowardlyTrait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            var soTrait = new SerializedObject(cowardlyTrait);
            soTrait.FindProperty("traitId").stringValue = "Cowardly";
            soTrait.FindProperty("survivalHpThreshold").floatValue = 0.50f;
            soTrait.FindProperty("fearMultiplier").floatValue = 2.0f;
            soTrait.ApplyModifiedPropertiesWithoutUndo();
            personality.Initialize(null, cowardlyTrait);

            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            Assert.IsNotNull(profileC, "Profile_Rank_C.asset should exist.");

            baseAI.ApplyAdventurerProfile(profileC);

            Assert.AreSame(profileC, baseAI.AdventurerProfile);
            Assert.AreEqual(CharacterRank.C, profileC.Rank);
            Assert.AreEqual(0.3f, throttler.DecisionInterval, 0.0001f);
            Assert.AreEqual(7f, losSensor.MaxDistance, 0.0001f);
            Assert.AreEqual(7f, rangeSensor.Circle.Radius, 0.0001f);
            Assert.AreEqual(45f, memory.DefaultHazardDuration, 0.0001f);
            Assert.AreEqual(0.3f, staging.MinHesitationDuration, 0.0001f);
            Assert.AreEqual(0.3f, staging.MaxHesitationDuration, 0.0001f);
            // Rank C base flee is 0.30, Cowardly shifts by at most +15% -> 0.30 * 1.15 = 0.345
            Assert.AreEqual(0.345f, baseAI.SurvivalHpThreshold, 0.0001f);

            // Mutate subsystem values and verify IPoolable.OnSpawn on CharacterBrain reapplies profile
            throttler.SetDecisionInterval(0.9f);
            memory.SetDefaultHazardDuration(5f);
            staging.SetHesitationDuration(1.2f);
            vision.SetVisionRadius(3f);

            brain.OnSpawn();

            Assert.AreEqual(0.3f, throttler.DecisionInterval, 0.0001f);
            Assert.AreEqual(7f, vision.GetVisionRadius(), 0.0001f);
            Assert.AreEqual(45f, memory.DefaultHazardDuration, 0.0001f);
            Assert.AreEqual(0.3f, staging.MinHesitationDuration, 0.0001f);

            UnityEngine.Object.DestroyImmediate(cowardlyTrait);
        }

        [Test]
        public void DefaultProfileAssets_ExistAndMatchRankSpecs()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");

            Assert.IsNotNull(profileE, "Profile_Rank_E should exist.");
            Assert.AreEqual(CharacterRank.E, profileE.Rank);
            Assert.AreEqual(0.5f, profileE.DecisionInterval, 0.0001f);
            Assert.AreEqual(5f, profileE.VisionRadius, 0.0001f);
            Assert.AreEqual(0.40f, profileE.IndividualFleeHpThreshold, 0.0001f);
            Assert.AreEqual(0.6f, profileE.DoorwayHesitation, 0.0001f);
            Assert.AreEqual(10f, profileE.HazardMemoryDuration, 0.0001f);
            Assert.AreEqual(0.50f, profileE.SquadOrderCompliance, 0.0001f);
            Assert.AreEqual(LeaderAssessmentAccuracy.Optimistic, profileE.LeaderAssessmentAccuracy);
            Assert.AreEqual(LeaderSafetyMargin.Thin, profileE.LeaderSafetyMargin);
            Assert.AreEqual(ItemUseQuality.LateWasteful, profileE.ItemUseQuality);
            Assert.AreEqual(TargetPriorityMode.Nearest, profileE.TargetPriorityMode);

            Assert.IsNotNull(profileD, "Profile_Rank_D should exist.");
            Assert.AreEqual(CharacterRank.D, profileD.Rank);
            Assert.AreEqual(0.4f, profileD.DecisionInterval, 0.0001f);
            Assert.AreEqual(6f, profileD.VisionRadius, 0.0001f);
            Assert.AreEqual(0.35f, profileD.IndividualFleeHpThreshold, 0.0001f);
            Assert.AreEqual(0.45f, profileD.DoorwayHesitation, 0.0001f);
            Assert.AreEqual(25f, profileD.HazardMemoryDuration, 0.0001f);
            Assert.AreEqual(0.75f, profileD.SquadOrderCompliance, 0.0001f);
            Assert.AreEqual(LeaderAssessmentAccuracy.NearTrue, profileD.LeaderAssessmentAccuracy);
            Assert.AreEqual(LeaderSafetyMargin.Medium, profileD.LeaderSafetyMargin);
            Assert.AreEqual(ItemUseQuality.Adequate, profileD.ItemUseQuality);
            Assert.AreEqual(TargetPriorityMode.LowestHp, profileD.TargetPriorityMode);

            Assert.IsNotNull(profileC, "Profile_Rank_C should exist.");
            Assert.AreEqual(CharacterRank.C, profileC.Rank);
            Assert.AreEqual(0.3f, profileC.DecisionInterval, 0.0001f);
            Assert.AreEqual(7f, profileC.VisionRadius, 0.0001f);
            Assert.AreEqual(0.30f, profileC.IndividualFleeHpThreshold, 0.0001f);
            Assert.AreEqual(0.3f, profileC.DoorwayHesitation, 0.0001f);
            Assert.AreEqual(45f, profileC.HazardMemoryDuration, 0.0001f);
            Assert.AreEqual(0.90f, profileC.SquadOrderCompliance, 0.0001f);
            Assert.AreEqual(LeaderAssessmentAccuracy.Accurate, profileC.LeaderAssessmentAccuracy);
            Assert.AreEqual(LeaderSafetyMargin.Thick, profileC.LeaderSafetyMargin);
            Assert.AreEqual(ItemUseQuality.WellTimed, profileC.ItemUseQuality);
            Assert.AreEqual(TargetPriorityMode.SupportFirstThenLowestHp, profileC.TargetPriorityMode);
        }

        [Test]
        public void AdventurerFunction_ExposesOnCharacterStack_AndTreatsNoneAsDPS_AndRoundtripsCsv()
        {
            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            var brain = _rootGo.AddComponent<CharacterBrain>();
            var identifier = _rootGo.AddComponent<CharacterIdentifier>();
            var baseAI = _rootGo.AddComponent<BaseAI>();

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.characterFunction = AdventurerFunction.None;
            stats.SetBaseStats(baseStat);

            Assert.AreEqual(AdventurerFunction.None, stats.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.DPS, stats.EffectiveFunction);
            Assert.AreEqual(AdventurerFunction.None, brain.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.DPS, brain.EffectiveFunction);
            Assert.AreEqual(AdventurerFunction.None, identifier.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.DPS, identifier.EffectiveFunction);
            Assert.AreEqual(AdventurerFunction.None, baseAI.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.DPS, baseAI.EffectiveFunction);

            baseStat.characterFunction = AdventurerFunction.Healer;
            Assert.AreEqual(AdventurerFunction.Healer, brain.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.Healer, brain.EffectiveFunction);
            Assert.AreEqual(AdventurerFunction.Healer, identifier.CharacterFunction);
            Assert.AreEqual(AdventurerFunction.Healer, baseAI.CharacterFunction);

            // CSV roundtrip + missing/empty column fallback to None
            var entry = new SBGames.Database.CharacterEntry
            {
                id = "char_test",
                characterName = "Test Healer",
                baseStats = baseStat
            };
            var row = SBGames.Database.CSV.CsvParser.BuildCharacterCsvRow(entry);
            Assert.AreEqual("Healer", row["Function"]);

            var importedStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            SBGames.Database.CSV.CsvParser.ApplyCharacterCsvRowToStats(row, importedStat);
            Assert.AreEqual(AdventurerFunction.Healer, importedStat.characterFunction);

            // Empty cell -> None
            row["Function"] = "";
            SBGames.Database.CSV.CsvParser.ApplyCharacterCsvRowToStats(row, importedStat);
            Assert.AreEqual(AdventurerFunction.None, importedStat.characterFunction);

            // Missing Function column (old CSV) -> None
            string legacyCsv = "Id,CharacterName,MaxLife\nchar_old,Old Hero,120";
            var legacyTable = SBGames.Database.CSV.CsvParser.ParseTable(legacyCsv);
            importedStat.characterFunction = AdventurerFunction.Tank;
            SBGames.Database.CSV.CsvParser.ApplyCharacterCsvRowToStats(legacyTable[0], importedStat);
            Assert.AreEqual(AdventurerFunction.None, importedStat.characterFunction);

            UnityEngine.Object.DestroyImmediate(baseStat);
            UnityEngine.Object.DestroyImmediate(importedStat);
        }

        [Test]
        public void CharacterClassStyleTable_ProvidesStandingDistanceAndSkillReliance_WithoutRestrictingSkills()
        {
            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            var baseAI = _rootGo.AddComponent<BaseAI>();
            var styleTable = ScriptableObject.CreateInstance<CharacterClassStyleTableSO>();
            baseAI.SetClassStyleTable(styleTable);

            var baseStat = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStat.AttackRange = 10f;
            baseStat.characterClass = CharacterClass.Warrior;
            stats.SetBaseStats(baseStat);

            float warriorStandDist = baseAI.GetPreferredStandingDistance(stats.AttackRange.Value);
            float warriorSkillReliance = baseAI.SkillReliance;

            baseStat.characterClass = CharacterClass.Mage;
            float mageStandDist = baseAI.GetPreferredStandingDistance(stats.AttackRange.Value);
            float mageSkillReliance = baseAI.SkillReliance;

            Assert.Less(warriorStandDist, mageStandDist, "Warrior should prefer standing closer as a fraction of AttackRange than Mage.");
            Assert.Greater(mageSkillReliance, warriorSkillReliance, "Mage should have higher skill reliance than Warrior.");

            var utilSkill = ScriptableObject.CreateInstance<UtilitySkill>();
            var soSkill = new SerializedObject(utilSkill);
            soSkill.FindProperty("useDistanceConsideration").boolValue = false;
            soSkill.FindProperty("baseWeight").floatValue = 0.6f;
            soSkill.ApplyModifiedPropertiesWithoutUndo();

            var baseAttackSkill = ScriptableObject.CreateInstance<UtilityBaseSkill>();

            float mageSkillScore = utilSkill.Evaluate(baseAI);
            float mageBasicScore = baseAttackSkill.Evaluate(baseAI);

            baseStat.characterClass = CharacterClass.Warrior;
            float warriorSkillScore = utilSkill.Evaluate(baseAI);
            float warriorBasicScore = baseAttackSkill.Evaluate(baseAI);

            Assert.Greater(mageSkillScore, warriorSkillScore, "Higher skillReliance should increase UtilitySkill score.");
            Assert.Less(mageBasicScore, warriorBasicScore, "Higher skillReliance should reduce UtilityBaseSkill fallback weight.");

            UnityEngine.Object.DestroyImmediate(utilSkill);
            UnityEngine.Object.DestroyImmediate(baseAttackSkill);
            UnityEngine.Object.DestroyImmediate(baseStat);
            UnityEngine.Object.DestroyImmediate(styleTable);
        }

        [Test]
        public void PartyFormation_TravellingMarchOrder_AndScoutFallsBackInCombat()
        {
            var leaderGo = new GameObject("Leader");
            leaderGo.transform.position = Vector3.zero;
            var leader = leaderGo.AddComponent<PartyLeader>();
            leader.MarchingForward = Vector3.up; // +Y is ahead, -Y is rear, +X is right

            SquadMember CreateMember(string name, AdventurerFunction func, out GameObject go)
            {
                go = new GameObject(name);
                var st = go.AddComponent<CharacterStatsManager>();
                var bs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
                bs.characterFunction = func;
                st.SetBaseStats(bs);
                var sm = go.AddComponent<SquadMember>();
                go.AddComponent<BaseAI>();
                leader.RegisterMember(sm);
                return sm;
            }

            var scout = CreateMember("Scout", AdventurerFunction.Scout, out var scoutGo);
            var tank = CreateMember("Tank", AdventurerFunction.Tank, out var tankGo);
            var dps = CreateMember("DPS", AdventurerFunction.DPS, out var dpsGo);
            var noneMember = CreateMember("NoneFunc", AdventurerFunction.None, out var noneGo);
            var healer = CreateMember("Healer", AdventurerFunction.Healer, out var healerGo);

            // Out of combat: Scout ahead of Tank > Tank > DPS (and None) > Healer at rear
            Vector3 scoutTravel = leader.GetFormationWorldPosition(scout);
            Vector3 tankTravel = leader.GetFormationWorldPosition(tank);
            Vector3 dpsTravel = leader.GetFormationWorldPosition(dps);
            Vector3 noneTravel = leader.GetFormationWorldPosition(noneMember);
            Vector3 healerTravel = leader.GetFormationWorldPosition(healer);

            Assert.Greater(scoutTravel.y, tankTravel.y, "Scout must march ahead of Tank out of combat.");
            Assert.Greater(tankTravel.y, dpsTravel.y, "Tank must march ahead of DPS out of combat.");
            Assert.AreEqual(dpsTravel.y, noneTravel.y, 0.001f, "None function must be treated like DPS.");
            Assert.Greater(dpsTravel.y, healerTravel.y, "DPS must march ahead of Healer (Healer at the rear).");

            // Enter combat (report an enemy on blackboard)
            var enemyGo = new GameObject("Enemy");
            leader.Blackboard.ReportEnemy(enemyGo.transform, new Vector3(0f, 10f, 0f), Time.time, 10f);
            Assert.IsTrue(leader.IsInCombat());

            Vector3 scoutCombat = leader.GetFormationWorldPosition(scout);
            Vector3 tankCombat = leader.GetFormationWorldPosition(tank);

            Assert.Less(scoutCombat.y, tankCombat.y, "When combat starts, Scout must fall back behind Tank.");
            Assert.Greater(Mathf.Abs(scoutCombat.x), 0.5f, "When combat starts, Scout must fall back to the side/rear.");

            UnityEngine.Object.DestroyImmediate(enemyGo);
            UnityEngine.Object.DestroyImmediate(scoutGo);
            UnityEngine.Object.DestroyImmediate(tankGo);
            UnityEngine.Object.DestroyImmediate(dpsGo);
            UnityEngine.Object.DestroyImmediate(noneGo);
            UnityEngine.Object.DestroyImmediate(healerGo);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void TargetPriorityMode_SelectsNearest_LowestHp_AndSupportFirstThenLowestHp()
        {
            var baseAI = _rootGo.AddComponent<BaseAI>();
            _rootGo.transform.position = Vector3.zero;

            GameObject CreateEnemy(string name, Vector3 pos, float currentHp, AdventurerFunction func)
            {
                var go = new GameObject(name);
                go.transform.position = pos;
                var st = go.AddComponent<CharacterStatsManager>();
                var bs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
                bs.MaxLife = 100f;
                bs.characterFunction = func;
                st.SetBaseStats(bs);
                if (currentHp < 100f)
                {
                    st.TakeDamage(new DamageData(100f - currentHp, DamageType.True));
                }
                return go;
            }

            // Enemy A: Nearest (dist 2, HP 90, Tank)
            var enemyNearest = CreateEnemy("Enemy_Nearest", new Vector3(2f, 0f, 0f), 90f, AdventurerFunction.Tank);
            // Enemy B: Lowest HP non-healer (dist 6, HP 15, DPS)
            var enemyLowestHp = CreateEnemy("Enemy_LowestHp", new Vector3(6f, 0f, 0f), 15f, AdventurerFunction.DPS);
            // Enemy C: Healer with higher HP (dist 8, HP 80, Healer)
            var enemyHealerHighHp = CreateEnemy("Enemy_HealerHigh", new Vector3(8f, 0f, 0f), 80f, AdventurerFunction.Healer);
            // Enemy D: Healer with lower HP (dist 9, HP 40, Healer)
            var enemyHealerLowHp = CreateEnemy("Enemy_HealerLow", new Vector3(9f, 0f, 0f), 40f, AdventurerFunction.Healer);

            baseAI.EnemyTargets = new System.Collections.Generic.List<Transform>
            {
                enemyNearest.transform,
                enemyLowestHp.transform,
                enemyHealerHighHp.transform,
                enemyHealerLowHp.transform
            };

            var receiver = _rootGo.AddComponent<CrashKonijn.Agent.Runtime.AgentBehaviour>();
            var sensor = new EnemyTargetSensor();

            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");

            // 1. Rank E (Nearest) -> picks enemyNearest
            baseAI.ApplyAdventurerProfile(profileE);
            sensor.Sense(receiver, null, null);
            Assert.AreEqual(enemyNearest.transform, baseAI.CurrentTarget);

            // 2. Rank D (LowestHp) -> picks enemyLowestHp (15 HP)
            baseAI.ApplyAdventurerProfile(profileD);
            sensor.Sense(receiver, null, null);
            Assert.AreEqual(enemyLowestHp.transform, baseAI.CurrentTarget);

            // 3. Rank C (SupportFirstThenLowestHp) -> picks enemyHealerLowHp (Healer with 40 HP over Healer with 80 HP and DPS with 15 HP)
            baseAI.ApplyAdventurerProfile(profileC);
            sensor.Sense(receiver, null, null);
            Assert.AreEqual(enemyHealerLowHp.transform, baseAI.CurrentTarget);

            UnityEngine.Object.DestroyImmediate(enemyNearest);
            UnityEngine.Object.DestroyImmediate(enemyLowestHp);
            UnityEngine.Object.DestroyImmediate(enemyHealerHighHp);
            UnityEngine.Object.DestroyImmediate(enemyHealerLowHp);
        }

        [Test]
        public void PartyLeader_CanScoutAndScoutMember_UpdateWhenScoutLeavesOrDies()
        {
            var leaderGo = new GameObject("Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();

            var tankGo = new GameObject("Tank");
            var tankStats = tankGo.AddComponent<CharacterStatsManager>();
            var tankBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            tankBs.characterFunction = AdventurerFunction.Tank;
            tankStats.SetBaseStats(tankBs);
            var tankMember = tankGo.AddComponent<SquadMember>();

            var scoutGo = new GameObject("Scout");
            var scoutStats = scoutGo.AddComponent<CharacterStatsManager>();
            var scoutBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            scoutBs.characterFunction = AdventurerFunction.Scout;
            scoutStats.SetBaseStats(scoutBs);
            var scoutMember = scoutGo.AddComponent<SquadMember>();

            leader.RegisterMember(tankMember);
            Assert.IsFalse(leader.CanScout);
            Assert.IsNull(leader.ScoutMember);

            leader.RegisterMember(scoutMember);
            Assert.IsTrue(leader.CanScout);
            Assert.AreEqual(scoutMember, leader.ScoutMember);

            // Member leaves squad -> CanScout becomes false, ScoutMember becomes null
            scoutMember.LeaveSquad();
            Assert.IsFalse(leader.CanScout);
            Assert.IsNull(leader.ScoutMember);

            // Re-register and then kill scout -> CanScout becomes false, ScoutMember becomes null
            leader.RegisterMember(scoutMember);
            Assert.IsTrue(leader.CanScout);
            scoutStats.ForceDeath();
            Assert.IsFalse(leader.CanScout);
            Assert.IsNull(leader.ScoutMember);

            UnityEngine.Object.DestroyImmediate(tankBs);
            UnityEngine.Object.DestroyImmediate(scoutBs);
            UnityEngine.Object.DestroyImmediate(tankGo);
            UnityEngine.Object.DestroyImmediate(scoutGo);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void Phase4_Assets_HaveExpectedCheckpointAndSuccessionValues()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<LeaderCheckpointTuningSO>("Assets/ScriptableObjects/AIProfiles/LeaderCheckpointTuning.asset");

            Assert.IsNotNull(profileE);
            Assert.IsNotNull(profileD);
            Assert.IsNotNull(profileC);
            Assert.IsNotNull(tuning);

            Assert.AreEqual(1.60f, profileE.RewardWeight, 0.001f);
            Assert.AreEqual(1.80f, profileE.SuccessionReactionDelay, 0.001f);
            Assert.AreEqual(0.70f, profileE.StepUpThreshold, 0.001f);
            Assert.AreEqual(LeaderlessBehavior.Dissolve, profileE.LeaderlessBehavior);

            Assert.AreEqual(1.15f, profileD.RewardWeight, 0.001f);
            Assert.AreEqual(0.90f, profileD.SuccessionReactionDelay, 0.001f);
            Assert.AreEqual(0.50f, profileD.StepUpThreshold, 0.001f);
            Assert.AreEqual(LeaderlessBehavior.StayLoose, profileD.LeaderlessBehavior);

            Assert.AreEqual(0.90f, profileC.RewardWeight, 0.001f);
            Assert.AreEqual(0.15f, profileC.SuccessionReactionDelay, 0.001f);
            Assert.AreEqual(0.25f, profileC.StepUpThreshold, 0.001f);
            Assert.AreEqual(LeaderlessBehavior.StayLoose, profileC.LeaderlessBehavior);

            Assert.Greater(
                tuning.GetFunctionStepUpWeight(AdventurerFunction.Tank),
                tuning.GetFunctionStepUpWeight(AdventurerFunction.DPS));
            Assert.AreEqual(
                tuning.GetFunctionStepUpWeight(AdventurerFunction.DPS),
                tuning.GetFunctionStepUpWeight(AdventurerFunction.None),
                0.001f);
            Assert.Greater(
                tuning.GetFunctionStepUpWeight(AdventurerFunction.DPS),
                tuning.GetFunctionStepUpWeight(AdventurerFunction.Healer));
            Assert.Greater(
                tuning.GetFunctionStepUpWeight(AdventurerFunction.Healer),
                tuning.GetFunctionStepUpWeight(AdventurerFunction.Scout));
        }

        [Test]
        public void RequestCheckpoint_ERankAdvances_WhereCRankRetreats_OnSamePartyAndRewardState()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<LeaderCheckpointTuningSO>("Assets/ScriptableObjects/AIProfiles/LeaderCheckpointTuning.asset");

            var leaderGo = new GameObject("CheckpointLeader");
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBs.MaxLife = 100f;
            leaderBs.MaxMana = 100f;
            leaderBs.characterFunction = AdventurerFunction.DPS;
            leaderStats.SetBaseStats(leaderBs);
            leaderStats.TakeDamage(new DamageData(30f, DamageType.True)); // 70% HP (>= 0.65 recovery threshold), moderate risk

            var leaderAI = leaderGo.AddComponent<BaseAI>();
            var leader = leaderGo.AddComponent<PartyLeader>();
            leader.CheckpointTuning = tuning;
            leader.RecoveryItemsRemaining = 0;

            // Substantial carried loot at stake (120) vs moderate potential ahead (80)
            leader.RewardProvider = new StandInPartyRewardProvider(
                carriedPerMember: 120f,
                knownAhead: 40f,
                unknownPerRoom: 40f,
                explicitPartyCarried: 120f);

            UnityEngine.Random.InitState(42);
            leaderAI.ApplyAdventurerProfile(profileE);
            CheckpointDecision eDecision = leader.RequestCheckpoint(out CheckpointReason eReason);
            Assert.AreEqual(CheckpointDecision.Advance, eDecision, "E-rank leader (optimistic + high greed + thin margin) should Advance.");
            Assert.AreEqual(CheckpointReason.FavorableMargin, eReason);
            Assert.AreEqual(SquadOrderType.FollowLeader, leader.Blackboard.CurrentOrder);

            UnityEngine.Random.InitState(42);
            leaderAI.ApplyAdventurerProfile(profileC);
            CheckpointDecision cDecision = leader.RequestCheckpoint(out CheckpointReason cReason);
            Assert.AreEqual(CheckpointDecision.Retreat, cDecision, "C-rank leader (accurate + balanced greed + thick margin) should Retreat on the same state.");
            Assert.AreEqual(CheckpointReason.SafetyMarginNotMet, cReason);
            Assert.AreEqual(SquadOrderType.FallBack, leader.Blackboard.CurrentOrder);

            UnityEngine.Object.DestroyImmediate(leaderBs);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void RequestCheckpoint_KeyRoleLost_TriggersRetreat_AndRetreatBlocked_ForcesFallback()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");

            var leaderGo = new GameObject("Leader");
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBs.MaxLife = 100f;
            leaderBs.MaxMana = 100f;
            leaderBs.characterFunction = AdventurerFunction.DPS;
            leaderStats.SetBaseStats(leaderBs);
            leaderStats.TakeDamage(new DamageData(50f, DamageType.True)); // 50% HP -> needs recovery if retreat is blocked
            var leaderAI = leaderGo.AddComponent<BaseAI>();
            leaderAI.ApplyAdventurerProfile(profileE);
            var leader = leaderGo.AddComponent<PartyLeader>();
            leader.RecoveryItemsRemaining = 2;

            var healerGo = new GameObject("Healer");
            var healerStats = healerGo.AddComponent<CharacterStatsManager>();
            var healerBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            healerBs.MaxLife = 100f;
            healerBs.characterFunction = AdventurerFunction.Healer;
            healerStats.SetBaseStats(healerBs);
            var healerMember = healerGo.AddComponent<SquadMember>();
            leader.RegisterMember(healerMember);

            // Kill the only Healer
            healerStats.ForceDeath();

            CheckpointDecision decision = leader.RequestCheckpoint(out CheckpointReason reason);
            Assert.AreEqual(CheckpointDecision.Retreat, decision, "Losing the only Healer should cause Retreat when retreat is not blocked.");
            Assert.AreEqual(CheckpointReason.KeyRoleLost, reason);
            Assert.AreEqual(SquadOrderType.FallBack, leader.Blackboard.CurrentOrder);

            // Block retreat -> must pick next best decision (Recover because leader HP is 50% and recovery items exist)
            leader.RetreatBlocked = true;
            CheckpointDecision blockedDecision = leader.RequestCheckpoint(out CheckpointReason blockedReason);
            Assert.AreNotEqual(CheckpointDecision.Retreat, blockedDecision, "Retreat must not be chosen when RetreatBlocked is true.");
            Assert.AreEqual(CheckpointDecision.Recover, blockedDecision);
            Assert.AreEqual(CheckpointReason.RetreatBlockedFallback, blockedReason);
            Assert.AreEqual(SquadOrderType.Recover, leader.Blackboard.CurrentOrder);

            UnityEngine.Object.DestroyImmediate(leaderBs);
            UnityEngine.Object.DestroyImmediate(healerBs);
            UnityEngine.Object.DestroyImmediate(healerGo);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void RequestCheckpoint_Advance_UsesScoutAheadWhenCanScout_AndPostCombatHookTriggersCheckpoint()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");

            var leaderGo = new GameObject("Leader");
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBs.MaxLife = 100f;
            leaderBs.MaxMana = 100f;
            leaderBs.characterFunction = AdventurerFunction.Tank;
            leaderStats.SetBaseStats(leaderBs);
            var leaderAI = leaderGo.AddComponent<BaseAI>();
            leaderAI.ApplyAdventurerProfile(profileE);
            var leader = leaderGo.AddComponent<PartyLeader>();
            var evaluator = leaderGo.AddComponent<SquadTacticalEvaluator>();
            evaluator.PostCombatCheckpointDelay = 1.0f;

            var scoutGo = new GameObject("Scout");
            var scoutStats = scoutGo.AddComponent<CharacterStatsManager>();
            var scoutBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            scoutBs.MaxLife = 100f;
            scoutBs.characterFunction = AdventurerFunction.Scout;
            scoutStats.SetBaseStats(scoutBs);
            var scoutMember = scoutGo.AddComponent<SquadMember>();
            leader.RegisterMember(scoutMember);

            int checkpointCount = 0;
            leader.OnCheckpointDecision += (dec, rsn) => checkpointCount++;

            // Simulate active enemy then enemy cleared
            var dummyEnemy = new GameObject("DummyEnemy");
            leader.Blackboard.ReportEnemy(dummyEnemy.transform, Vector3.zero, Time.time, 10f);
            evaluator.EvaluateTactics(0.5f);
            Assert.AreEqual(0, checkpointCount);

            leader.Blackboard.KnownEnemies.Clear();
            evaluator.EvaluateTactics(0.5f);
            Assert.AreEqual(0, checkpointCount, "Should wait until PostCombatCheckpointDelay elapses.");

            evaluator.EvaluateTactics(0.6f);
            Assert.AreEqual(1, checkpointCount, "Post-combat quiet timer should trigger RequestCheckpoint.");
            Assert.AreEqual(CheckpointDecision.Advance, leader.LastCheckpointDecision);
            Assert.AreEqual(SquadOrderType.ScoutAhead, leader.Blackboard.CurrentOrder);
            Assert.AreEqual(SquadOrderType.ScoutAhead, scoutMember.CurrentOrder);

            UnityEngine.Object.DestroyImmediate(dummyEnemy);
            UnityEngine.Object.DestroyImmediate(leaderBs);
            UnityEngine.Object.DestroyImmediate(scoutBs);
            UnityEngine.Object.DestroyImmediate(scoutGo);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void EmergentSuccession_TankBeatsScout_InheritsBlackboard_AndRunsCheckpoint()
        {
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<LeaderCheckpointTuningSO>("Assets/ScriptableObjects/AIProfiles/LeaderCheckpointTuning.asset");

            var leaderGo = new GameObject("OldLeader");
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBs.MaxLife = 100f;
            leaderBs.characterFunction = AdventurerFunction.DPS;
            leaderStats.SetBaseStats(leaderBs);
            var leaderAI = leaderGo.AddComponent<BaseAI>();
            leaderAI.ApplyAdventurerProfile(profileC);
            var oldLeader = leaderGo.AddComponent<PartyLeader>();
            oldLeader.CheckpointTuning = tuning;
            oldLeader.RetreatBlocked = true;
            oldLeader.Blackboard.ReportHazard(new Vector2Int(3, 4), new Vector3(3f, 4f, 0f), 40f, 30f);

            var scoutGo = new GameObject("ScoutCandidate");
            var scoutStats = scoutGo.AddComponent<CharacterStatsManager>();
            var scoutBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            scoutBs.MaxLife = 100f;
            scoutBs.characterFunction = AdventurerFunction.Scout;
            scoutStats.SetBaseStats(scoutBs);
            var scoutAI = scoutGo.AddComponent<BaseAI>();
            scoutAI.ApplyAdventurerProfile(profileC);
            var scoutMember = scoutGo.AddComponent<SquadMember>();

            var tankGo = new GameObject("TankCandidate");
            var tankStats = tankGo.AddComponent<CharacterStatsManager>();
            var tankBs = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            tankBs.MaxLife = 100f;
            tankBs.characterFunction = AdventurerFunction.Tank;
            tankStats.SetBaseStats(tankBs);
            var tankAI = tankGo.AddComponent<BaseAI>();
            tankAI.ApplyAdventurerProfile(profileC);
            var tankMember = tankGo.AddComponent<SquadMember>();

            // Register Scout first and Tank second to prove succession is score-based, not list-order-based
            oldLeader.RegisterMember(scoutMember);
            oldLeader.RegisterMember(tankMember);

            Assert.Greater(
                oldLeader.ComputeCandidateStepUpScore(tankMember),
                oldLeader.ComputeCandidateStepUpScore(scoutMember),
                "Tank candidate must score higher than Scout candidate.");

            bool diedFired = false;
            PartyLeader changedNewLeader = null;
            oldLeader.OnLeaderDied += _ => diedFired = true;
            oldLeader.OnLeaderChanged += (_, newL) => changedNewLeader = newL;

            UnityEngine.Random.InitState(123);
            leaderStats.ForceDeath();
            PartyLeader promoted = oldLeader.CompleteSuccessionForTesting();

            Assert.IsTrue(diedFired);
            Assert.IsNotNull(promoted);
            Assert.AreEqual(tankGo, promoted.gameObject, "Tank should step up as the new leader over Scout.");
            Assert.AreEqual(promoted, changedNewLeader);
            Assert.IsTrue(promoted.RetreatBlocked, "New leader must inherit RetreatBlocked from SquadBlackboard.");
            Assert.AreEqual(1, promoted.Blackboard.SharedHazards.Count, "New leader must inherit shared hazards from SquadBlackboard.");
            Assert.AreNotEqual(CheckpointReason.None, promoted.LastCheckpointReason, "New leader must immediately run RequestCheckpoint.");

            UnityEngine.Object.DestroyImmediate(leaderBs);
            UnityEngine.Object.DestroyImmediate(scoutBs);
            UnityEngine.Object.DestroyImmediate(tankBs);
            UnityEngine.Object.DestroyImmediate(scoutGo);
            UnityEngine.Object.DestroyImmediate(tankGo);
            UnityEngine.Object.DestroyImmediate(leaderGo);
        }

        [Test]
        public void EmergentSuccession_WhenNobodyStepsUp_AppliesStayLooseOrDissolve()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");

            // 1. E-Rank (Dissolve): low-HP Scouts below stepUpThreshold (0.70)
            var leaderEGo = new GameObject("LeaderE");
            var leaderEStats = leaderEGo.AddComponent<CharacterStatsManager>();
            var bsE = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            bsE.MaxLife = 100f;
            bsE.characterFunction = AdventurerFunction.Scout;
            leaderEStats.SetBaseStats(bsE);
            var leaderEAI = leaderEGo.AddComponent<BaseAI>();
            leaderEAI.ApplyAdventurerProfile(profileE);
            var leaderE = leaderEGo.AddComponent<PartyLeader>();

            var survE1Go = new GameObject("SurvE1");
            var survE1Stats = survE1Go.AddComponent<CharacterStatsManager>();
            survE1Stats.SetBaseStats(bsE);
            survE1Stats.TakeDamage(new DamageData(80f, DamageType.True));
            var survE1AI = survE1Go.AddComponent<BaseAI>();
            survE1AI.ApplyAdventurerProfile(profileE);
            var survE1 = survE1Go.AddComponent<SquadMember>();
            leaderE.RegisterMember(survE1);

            leaderEStats.ForceDeath();
            PartyLeader promotedE = leaderE.CompleteSuccessionForTesting();
            Assert.IsNull(promotedE, "Low-HP E-rank scout should not meet stepUpThreshold.");
            Assert.IsFalse(survE1.HasLeader);
            Assert.IsFalse(survE1.IsStayingLoose, "E-rank leaderless behavior is Dissolve.");

            // 2. D-Rank (StayLoose): low-HP Scouts with fear below stepUpThreshold (0.50)
            var leaderDGo = new GameObject("LeaderD");
            var leaderDStats = leaderDGo.AddComponent<CharacterStatsManager>();
            leaderDStats.SetBaseStats(bsE);
            var leaderDAI = leaderDGo.AddComponent<BaseAI>();
            leaderDAI.ApplyAdventurerProfile(profileD);
            var leaderD = leaderDGo.AddComponent<PartyLeader>();

            var survD1Go = new GameObject("SurvD1");
            var survD1Stats = survD1Go.AddComponent<CharacterStatsManager>();
            survD1Stats.SetBaseStats(bsE);
            survD1Stats.TakeDamage(new DamageData(85f, DamageType.True));
            var survD1AI = survD1Go.AddComponent<BaseAI>();
            survD1AI.ApplyAdventurerProfile(profileD);
            survD1AI.SetFearLevel(80);
            var survD1 = survD1Go.AddComponent<SquadMember>();

            var survD2Go = new GameObject("SurvD2");
            survD2Go.transform.position = new Vector3(4f, 0f, 0f);
            var survD2Stats = survD2Go.AddComponent<CharacterStatsManager>();
            survD2Stats.SetBaseStats(bsE);
            survD2Stats.TakeDamage(new DamageData(85f, DamageType.True));
            var survD2AI = survD2Go.AddComponent<BaseAI>();
            survD2AI.ApplyAdventurerProfile(profileD);
            survD2AI.SetFearLevel(80);
            var survD2 = survD2Go.AddComponent<SquadMember>();

            leaderD.RegisterMember(survD1);
            leaderD.RegisterMember(survD2);

            leaderDStats.ForceDeath();
            PartyLeader promotedD = leaderD.CompleteSuccessionForTesting();
            Assert.IsNull(promotedD, "Panicked low-HP D-rank scouts should not meet stepUpThreshold.");
            Assert.IsFalse(survD1.HasLeader);
            Assert.IsTrue(survD1.IsStayingLoose, "D-rank leaderless behavior is StayLoose.");
            Assert.AreEqual(survD2.transform, survD1.LeaderTransform, "StayLoose survivor should loosely anchor to nearest living peer.");

            UnityEngine.Object.DestroyImmediate(bsE);
            UnityEngine.Object.DestroyImmediate(survE1Go);
            UnityEngine.Object.DestroyImmediate(leaderEGo);
            UnityEngine.Object.DestroyImmediate(survD1Go);
            UnityEngine.Object.DestroyImmediate(survD2Go);
            UnityEngine.Object.DestroyImmediate(leaderDGo);
        }

        [Test]
        public void Phase5_AdventurerWealth_And_CsvImport_DefaultToAverageWhenMissing()
        {
            Assert.AreEqual(0, (int)AdventurerWealth.Average);
            Assert.AreEqual(1, (int)AdventurerWealth.Poor);
            Assert.AreEqual(2, (int)AdventurerWealth.Rich);

            var stats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            Assert.AreEqual(AdventurerWealth.Average, stats.CharacterWealth);

            // Missing Wealth column -> Average
            var rowMissing = new Dictionary<string, string>
            {
                ["Id"] = "ADV_01",
                ["MaxLife"] = "120",
                ["Function"] = "Tank"
            };
            CsvParser.ApplyCharacterCsvRowToStats(rowMissing, stats);
            Assert.AreEqual(AdventurerFunction.Tank, stats.CharacterFunction);
            Assert.AreEqual(AdventurerWealth.Average, stats.CharacterWealth);

            // Empty Wealth cell -> Average
            var rowEmpty = new Dictionary<string, string>
            {
                ["Id"] = "ADV_01",
                ["Wealth"] = "   "
            };
            stats.characterWealth = AdventurerWealth.Rich;
            CsvParser.ApplyCharacterCsvRowToStats(rowEmpty, stats);
            Assert.AreEqual(AdventurerWealth.Average, stats.CharacterWealth);

            // Explicit Wealth cells -> Poor / Rich
            var rowRich = new Dictionary<string, string> { ["Wealth"] = "Rich" };
            CsvParser.ApplyCharacterCsvRowToStats(rowRich, stats);
            Assert.AreEqual(AdventurerWealth.Rich, stats.CharacterWealth);

            var rowPoor = new Dictionary<string, string> { ["Wealth"] = "Poor" };
            CsvParser.ApplyCharacterCsvRowToStats(rowPoor, stats);
            Assert.AreEqual(AdventurerWealth.Poor, stats.CharacterWealth);

            UnityEngine.Object.DestroyImmediate(stats);
        }

        [Test]
        public void Phase5_ItemKitResolution_CombinesRankFunctionAndWealth_AndGatesManaByMaxMana()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var modifiers = AssetDatabase.LoadAssetAtPath<ItemKitModifiers>("Assets/ScriptableObjects/AIProfiles/DefaultItemKitModifiers.asset");

            Assert.IsNotNull(profileE.StartingItemKit, "Profile_Rank_E must reference ItemKit_Rank_E.");
            Assert.IsNotNull(profileD.StartingItemKit, "Profile_Rank_D must reference ItemKit_Rank_D.");
            Assert.IsNotNull(profileC.StartingItemKit, "Profile_Rank_C must reference ItemKit_Rank_C.");
            Assert.IsNotNull(modifiers, "DefaultItemKitModifiers asset must exist.");

            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.KitModifiers = modifiers;

            // 1. Rank E (1 HP) + DPS (0) + Poor (-1 HP) -> floored at 0 HP potions
            kitCtrl.ResolveRuntimeKit(profileE.StartingItemKit, modifiers, AdventurerFunction.DPS, AdventurerWealth.Poor, hasManaPool: true);
            Assert.AreEqual(0, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion));
            Assert.AreEqual(0, kitCtrl.TotalRemainingCount);

            // 2. Rank D (2 HP + 1 Cure) + Tank (+1 HP) + Rich (+1 HP) -> 4 HP + 1 Cure = 5 total
            kitCtrl.ResolveRuntimeKit(profileD.StartingItemKit, modifiers, AdventurerFunction.Tank, AdventurerWealth.Rich, hasManaPool: false);
            Assert.AreEqual(4, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion));
            Assert.AreEqual(1, kitCtrl.GetRemainingCount(ConsumableItemCategory.StatusCure));
            Assert.AreEqual(0, kitCtrl.GetRemainingCount(ConsumableItemCategory.ManaPotion));
            Assert.AreEqual(5, kitCtrl.TotalRemainingCount);

            // 3. Rank C (3 HP + 2 Cure) + Healer (+1 Mana) + Average (0) with MaxMana > 0 -> 3 HP + 2 Cure + 1 Mana = 6 total
            kitCtrl.ResolveRuntimeKit(profileC.StartingItemKit, modifiers, AdventurerFunction.Healer, AdventurerWealth.Average, hasManaPool: true);
            Assert.AreEqual(3, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion));
            Assert.AreEqual(2, kitCtrl.GetRemainingCount(ConsumableItemCategory.StatusCure));
            Assert.AreEqual(1, kitCtrl.GetRemainingCount(ConsumableItemCategory.ManaPotion));
            Assert.AreEqual(6, kitCtrl.TotalRemainingCount);

            // 4. Same Healer with MaxMana == 0 -> Mana Potion is NOT granted
            kitCtrl.ResolveRuntimeKit(profileC.StartingItemKit, modifiers, AdventurerFunction.Healer, AdventurerWealth.Average, hasManaPool: false);
            Assert.AreEqual(0, kitCtrl.GetRemainingCount(ConsumableItemCategory.ManaPotion), "Mana potions must only be granted to members with MaxMana > 0.");
        }

        [Test]
        public void Phase5_ItemKitController_InterruptibleCast_ConsumesOnlyOnCompletion_AndCancelsOnStunOrDeath()
        {
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var modifiers = AssetDatabase.LoadAssetAtPath<ItemKitModifiers>("Assets/ScriptableObjects/AIProfiles/DefaultItemKitModifiers.asset");

            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 100f;
            baseStats.MaxMana = 50f;

            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);
            var brain = _rootGo.GetComponent<CharacterBrain>();
            var statusMgr = _rootGo.AddComponent<StatusEffectManager>();
            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.KitModifiers = modifiers;
            kitCtrl.CastDuration = 0.8f;
            kitCtrl.ItemCooldown = 6.0f;

            kitCtrl.ResolveRuntimeKit(profileD.StartingItemKit, modifiers, AdventurerFunction.DPS, AdventurerWealth.Average, hasManaPool: true);
            Assert.AreEqual(2, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion));

            // Deal 60 damage so HP is 40/100
            stats.TakeDamage(new DamageData(60f, DamageType.True));
            Assert.AreEqual(40f, stats.Life, 0.01f);

            int consumedEvents = 0;
            ItemDataSO lastConsumedItem = null;
            kitCtrl.OnPotionConsumed += (_, item) =>
            {
                consumedEvents++;
                lastConsumedItem = item;
            };

            // Start cast, advance halfway (0.4s), then interrupt via CharacterBrain.InterruptAllActions()
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.IsCastingItem);
            Assert.IsFalse(kitCtrl.AdvanceCast(0.4f));
            Assert.AreEqual(2, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion), "Item must not be consumed before cast completes.");

            brain.InterruptAllActions();
            Assert.IsFalse(kitCtrl.IsCastingItem, "InterruptAllActions must cancel active item cast.");
            Assert.AreEqual(2, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion), "Interrupted cast must not consume the item.");
            Assert.AreEqual(0, consumedEvents);
            Assert.AreEqual(40f, stats.Life, 0.01f);

            // Start cast again and let it complete (0.85s)
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.AdvanceCast(0.85f));
            Assert.IsFalse(kitCtrl.IsCastingItem);
            Assert.AreEqual(1, kitCtrl.GetRemainingCount(ConsumableItemCategory.HealthPotion));
            Assert.AreEqual(1, consumedEvents);
            Assert.IsNotNull(lastConsumedItem);
            Assert.Greater(stats.Life, 40f, "RestoreHealthEffect must heal the character on cast completion.");

            // Cooldown prevents immediate second HP potion cast until 6.0s elapses
            Assert.IsFalse(kitCtrl.CanUse(ConsumableItemCategory.HealthPotion), "Per-item cooldown must block immediate reuse.");
            kitCtrl.AdvanceCast(6.1f);
            Assert.IsTrue(kitCtrl.CanUse(ConsumableItemCategory.HealthPotion), "Slot should be usable again after cooldown expires.");

            // Test Status Cure removing harmful status effect
            var poisonData = ScriptableObject.CreateInstance<StatusEffectData>();
            poisonData.ID = "STATUS_POISON_TEST";
            poisonData.displayName = "Poison";
            poisonData.isHarmful = true;
            poisonData.hasDuration = true;
            poisonData.duration = 10f;
            statusMgr.ApplyEffect(poisonData);
            Assert.IsTrue(statusMgr.HasHarmfulEffect());

            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.StatusCure));
            Assert.IsTrue(kitCtrl.AdvanceCast(0.85f));
            Assert.IsFalse(statusMgr.HasHarmfulEffect(), "CureStatusEffect must remove active harmful status effects.");
            Assert.AreEqual(0, kitCtrl.GetRemainingCount(ConsumableItemCategory.StatusCure));

            UnityEngine.Object.DestroyImmediate(poisonData);
            UnityEngine.Object.DestroyImmediate(baseStats);
        }

        [Test]
        public void Phase5_RecoverAndSurviveCompetition_Hysteresis_And_RankQualityPositioning()
        {
            var profileE = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_E.asset");
            var profileD = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var modifiers = AssetDatabase.LoadAssetAtPath<ItemKitModifiers>("Assets/ScriptableObjects/AIProfiles/DefaultItemKitModifiers.asset");
            var qualityTuning = AssetDatabase.LoadAssetAtPath<ItemUseQualityTuningSO>("Assets/ScriptableObjects/AIProfiles/ItemUseQualityTuning.asset");

            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 100f;
            baseStats.MaxMana = 50f;

            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);
            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.KitModifiers = modifiers;
            kitCtrl.QualityTuning = qualityTuning;
            var ai = _rootGo.AddComponent<BaseAI>();
            ai.ItemUseQualityTuning = qualityTuning;

            var surviveGoal = ScriptableObject.CreateInstance<UtilitySurviveGoal>();
            var recoverGoal = ScriptableObject.CreateInstance<UtilityRecoverGoal>();
            ai.Goals = new List<UtilityGoal> { surviveGoal, recoverGoal };

            var enemyGo = new GameObject("MeleeEnemy");
            enemyGo.transform.position = new Vector3(1.0f, 0f, 0f); // 1 unit away (in melee)
            ai.EnemyTargets = new List<Transform> { enemyGo.transform };
            ai.SetTarget(enemyGo.transform, 1.0f);

            // 1. Rank E (LateWasteful): at 25% HP (below flee threshold 0.40 and below E trigger 0.28),
            // Recover wins while 1 HP potion is available, and drinks in place (desiredPos == transform.position)!
            ai.ApplyAdventurerProfile(profileE);
            stats.TakeDamage(new DamageData(75f, DamageType.True)); // 25/100 HP
            ai.UpdateResources();

            Assert.AreEqual(EnumUtilGoal.Recover, ai.EvaluateBestGoalForTesting(), "Adventurer must choose Recover over Survive when usable HP potion exists.");
            Assert.AreEqual(_rootGo.transform.position, ai.GetDesiredRecoveryPosition(), "LateWasteful (Rank E) must drink in place even under melee attack.");
            Assert.IsTrue(ai.IsReadyToDrinkAtCurrentPosition());

            // Consume Rank E's only potion -> kit is now empty -> Survive (flee) immediately wins!
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.AdvanceCast(1.0f));
            stats.TakeDamage(new DamageData(50f, DamageType.True)); // drop back to low HP with 0 potions left
            ai.UpdateResources();
            Assert.AreEqual(EnumUtilGoal.Survive, ai.EvaluateBestGoalForTesting(), "Adventurer must flee (Survive) when no usable recovery items remain.");

            // 2. Rank D (Adequate): steps back out of melee range before drinking
            stats.RestoreLife(100f);
            ai.ApplyAdventurerProfile(profileD);
            stats.TakeDamage(new DamageData(65f, DamageType.True)); // 35% HP
            ai.UpdateResources();
            Vector3 dPos = ai.GetDesiredRecoveryPosition();
            float distFromEnemyD = Vector3.Distance(dPos, enemyGo.transform.position);
            Assert.GreaterOrEqual(distFromEnemyD, 3.4f, "Adequate (Rank D) must step back out of melee range before drinking.");
            Assert.IsFalse(ai.IsReadyToDrinkAtCurrentPosition(), "Rank D at 1.0m from enemy must step back first.");

            // 3. Rank C (WellTimed): seeks position behind Tank and respects per-room quota (2 items)
            var leaderTankGo = new GameObject("LeaderTank");
            leaderTankGo.transform.position = new Vector3(3.0f, 0f, 0f);
            var tankBaseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            tankBaseStats.characterFunction = AdventurerFunction.Tank;
            var tankStats = leaderTankGo.AddComponent<CharacterStatsManager>();
            tankStats.SetBaseStats(tankBaseStats);
            var leader = leaderTankGo.AddComponent<PartyLeader>();

            var member = _rootGo.AddComponent<SquadMember>();
            leader.RegisterMember(member);

            stats.RestoreLife(100f);
            ai.ApplyAdventurerProfile(profileC);
            stats.TakeDamage(new DamageData(50f, DamageType.True)); // 50% HP (below C trigger 0.65)
            ai.UpdateResources();

            Vector3 cPos = ai.GetDesiredRecoveryPosition();
            Assert.Greater(cPos.x, leaderTankGo.transform.position.x, "WellTimed (Rank C) must position behind the Tank away from the enemy.");

            // Simulate using 2 items in the current room -> hits C's per-room quota (2) even with items still remaining
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.AdvanceCast(1.0f));
            kitCtrl.AdvanceCast(6.5f); // clear cooldown
            stats.TakeDamage(new DamageData(50f, DamageType.True));
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.AdvanceCast(1.0f));
            kitCtrl.AdvanceCast(6.5f); // clear cooldown

            Assert.Greater(kitCtrl.TotalRemainingCount, 0, "Rank C still has items in kit.");
            Assert.AreEqual(2, kitCtrl.ItemsUsedInCurrentRoom);
            Assert.IsFalse(ai.CanRecoverWithItemNow(), "WellTimed (Rank C) must respect per-room item quota (2).");

            UnityEngine.Object.DestroyImmediate(surviveGoal);
            UnityEngine.Object.DestroyImmediate(recoverGoal);
            UnityEngine.Object.DestroyImmediate(baseStats);
            UnityEngine.Object.DestroyImmediate(tankBaseStats);
            UnityEngine.Object.DestroyImmediate(enemyGo);
            UnityEngine.Object.DestroyImmediate(leaderTankGo);
        }

        [Test]
        public void Phase5_PartyLeader_RecoverOrder_UsesRealKitCounts_AndReRunsCheckpointWhenComplete()
        {
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            var modifiers = AssetDatabase.LoadAssetAtPath<ItemKitModifiers>("Assets/ScriptableObjects/AIProfiles/DefaultItemKitModifiers.asset");
            var qualityTuning = AssetDatabase.LoadAssetAtPath<ItemUseQualityTuningSO>("Assets/ScriptableObjects/AIProfiles/ItemUseQualityTuning.asset");
            var checkpointTuning = AssetDatabase.LoadAssetAtPath<LeaderCheckpointTuningSO>("Assets/ScriptableObjects/AIProfiles/LeaderCheckpointTuning.asset");

            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 100f;
            baseStats.MaxMana = 50f;
            baseStats.characterFunction = AdventurerFunction.Tank;

            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);
            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.KitModifiers = modifiers;
            kitCtrl.QualityTuning = qualityTuning;
            var ai = _rootGo.AddComponent<BaseAI>();
            ai.ItemUseQualityTuning = qualityTuning;
            ai.ApplyAdventurerProfile(profileC);

            var leader = _rootGo.AddComponent<PartyLeader>();
            leader.CheckpointTuning = checkpointTuning;
            leader.StandInRewardProvider.ExplicitPartyCarriedValue = 40f;
            leader.StandInRewardProvider.KnownPotentialAhead = 150f;
            leader.StandInRewardProvider.UnknownExpectationPerRoom = 100f;
            // Set stand-in recoveryItemsRemaining to 0 to prove PartyLeader uses the real AdventurerItemKitController count!
            leader.RecoveryItemsRemaining = 0;

            var evaluator = _rootGo.AddComponent<SquadTacticalEvaluator>();
            evaluator.AutonomousAI = false;

            // Damage leader to 50% HP -> needs recovery, and real kit has HP potions -> CheckpointDecision.Recover -> SquadOrderType.Recover
            stats.TakeDamage(new DamageData(50f, DamageType.True));
            CheckpointDecision decision1 = leader.RequestCheckpoint(out CheckpointReason reason1);
            Assert.AreEqual(CheckpointDecision.Recover, decision1);
            Assert.AreEqual(CheckpointReason.NeedsRecoveryBeforeAdvance, reason1);
            Assert.AreEqual(SquadOrderType.Recover, leader.Blackboard.CurrentOrder, "CheckpointDecision.Recover must map to SquadOrderType.Recover.");
            Assert.IsFalse(leader.IsPartyRecoveryComplete());

            // Drink HP potion to heal above 85% HP -> recovery is now complete -> evaluator re-runs RequestCheckpoint -> Advance!
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.HealthPotion));
            Assert.IsTrue(kitCtrl.AdvanceCast(1.0f));
            stats.RestoreLife(100f); // ensure >= 85% target

            Assert.IsTrue(leader.IsPartyRecoveryComplete());
            evaluator.EvaluateTactics(0.5f);
            Assert.AreEqual(CheckpointDecision.Advance, leader.LastCheckpointDecision, "PartyLeader must re-run RequestCheckpoint and advance once SquadOrderType.Recover completes.");
            Assert.AreEqual(SquadOrderType.FollowLeader, leader.Blackboard.CurrentOrder);

            UnityEngine.Object.DestroyImmediate(baseStats);
        }

        [Test]
        public void Phase6_RoomLoop_DeterministicForkRule_SelectsNearestUntraversed_AndIgnoresFullyExploredBranch()
        {
            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();

            var room1Go = new GameObject("Room1");
            var room1 = room1Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room1.RoomId = 1;
            room1.SetBounds(new Bounds(Vector3.zero, Vector3.one * 10f));

            var room2Go = new GameObject("Room2");
            var room2 = room2Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room2.RoomId = 2;
            room2.SetBounds(new Bounds(new Vector3(20, 0, 0), Vector3.one * 10f));

            var room3Go = new GameObject("Room3");
            var room3 = room3Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room3.RoomId = 3;
            room3.SetBounds(new Bounds(new Vector3(0, 20, 0), Vector3.one * 10f));

            var door1Go = new GameObject("Door1");
            door1Go.transform.position = new Vector3(5, 0, 0); // dist 5 to room 2
            var door1 = door1Go.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door1.DoorId = 10;
            door1.RoomA = room1;
            door1.RoomB = room2;
            room1.AddExit(door1);

            var door2Go = new GameObject("Door2");
            door2Go.transform.position = new Vector3(0, 3, 0); // dist 3 to room 3
            var door2 = door2Go.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door2.DoorId = 20;
            door2.RoomA = room1;
            door2.RoomB = room3;
            room1.AddExit(door2);

            // Door2 is closer (dist 3 < dist 5), so it should be chosen first
            var selected1 = leader.SelectNextExit(room1);
            Assert.AreSame(door2, selected1, "Should select nearest unexplored door (Door2).");

            // Mark Room 3 as fully explored -> should now skip Door 2 and select Door 1
            leader.Blackboard.MarkRoomFullyExplored(3);
            var selected2 = leader.SelectNextExit(room1);
            Assert.AreSame(door1, selected2, "Should never enter a fully explored branch.");

            // Mark Door 1 traversed -> no more untraversed exits in Room 1
            leader.Blackboard.MarkDoorTraversed(door1.DoorId);
            var selected3 = leader.SelectNextExit(room1);
            Assert.IsNull(selected3, "When all exits from Room 1 are explored/traversed, return null.");
            Assert.IsTrue(leader.Blackboard.IsRoomFullyExplored(1));

            UnityEngine.Object.DestroyImmediate(room1Go);
            UnityEngine.Object.DestroyImmediate(room2Go);
            UnityEngine.Object.DestroyImmediate(room3Go);
            UnityEngine.Object.DestroyImmediate(door1Go);
            UnityEngine.Object.DestroyImmediate(door2Go);
        }

        [Test]
        public void Phase6_RoomLoop_RankC_HazardAvoidance_PenalizesHazardousExits()
        {
            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();
            var profileC = AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_C.asset");
            if (profileC != null)
            {
                var ai = leaderGo.AddComponent<BaseAI>();
                ai.ApplyAdventurerProfile(profileC);
            }

            var room1Go = new GameObject("Room1");
            var room1 = room1Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room1.RoomId = 1;
            room1.SetBounds(new Bounds(Vector3.zero, Vector3.one * 10f));

            var room2Go = new GameObject("Room2_Hazardous");
            var room2 = room2Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room2.RoomId = 2;
            room2.IsHazardous = true;
            room2.SetBounds(new Bounds(new Vector3(10, 0, 0), Vector3.one * 10f));

            var room3Go = new GameObject("Room3_Safe");
            var room3 = room3Go.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room3.RoomId = 3;
            room3.SetBounds(new Bounds(new Vector3(0, 15, 0), Vector3.one * 10f));

            var door1Go = new GameObject("Door1_To_Hazard");
            door1Go.transform.position = new Vector3(3, 0, 0); // Closer (dist 3) but into hazardous room!
            var door1 = door1Go.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door1.DoorId = 10;
            door1.RoomA = room1;
            door1.RoomB = room2;
            room1.AddExit(door1);

            var door2Go = new GameObject("Door2_To_Safe");
            door2Go.transform.position = new Vector3(0, 5, 0); // Farther (dist 5) but safe!
            var door2 = door2Go.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door2.DoorId = 20;
            door2.RoomA = room1;
            door2.RoomB = room3;
            room1.AddExit(door2);

            // With Rank C hazard avoidance weight, Door 2 (safe) must be preferred over Door 1 (hazardous) despite being farther
            var selected = leader.SelectNextExit(room1);
            Assert.AreSame(door2, selected, "Rank C should avoid hazardous room and pick the safe exit.");

            UnityEngine.Object.DestroyImmediate(room1Go);
            UnityEngine.Object.DestroyImmediate(room2Go);
            UnityEngine.Object.DestroyImmediate(room3Go);
            UnityEngine.Object.DestroyImmediate(door1Go);
            UnityEngine.Object.DestroyImmediate(door2Go);
        }

        [Test]
        public void Phase6_RoomLoop_BossDoor_RecognizedOnSight()
        {
            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();

            var doorGo = new GameObject("BossDoor");
            doorGo.transform.position = new Vector3(10, 0, 0);
            var door = doorGo.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door.DoorId = 99;
            door.IsBossDoor = true;

            Assert.IsFalse(leader.Blackboard.BossDoorRecognized);

            // Record door seen
            leader.Blackboard.RecordDoorSeen(door.DoorId, door.Position, door.IsBossDoor, -1);
            Assert.IsTrue(leader.Blackboard.BossDoorRecognized);
            Assert.AreEqual(door.Position, leader.Blackboard.BossDoorPosition);

            UnityEngine.Object.DestroyImmediate(doorGo);
        }

        [Test]
        public void Phase6_RoomLoop_SafeRoom_TriggersRecoverOrder()
        {
            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();

            var roomGo = new GameObject("SafeRoom");
            var room = roomGo.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room.RoomId = 77;
            room.IsSafeRoom = true;
            room.SetBounds(new Bounds(Vector3.zero, Vector3.one * 10f));

            leader.StartRoomLoop();
            // Leader detects current room is safe room -> transitions to SafeRoomStop -> SquadOrderType.Recover
            Assert.AreEqual(RoomLoopState.SafeRoomStop, leader.CurrentRoomLoopState);
            Assert.AreEqual(SquadOrderType.Recover, leader.Blackboard.CurrentOrder);

            UnityEngine.Object.DestroyImmediate(roomGo);
        }

        [Test]
        public void Phase7_AdventurerEventHub_RaisesEvents_ZeroAlloc()
        {
            bool potionConsumedFired = false;
            bool bossFightFired = false;
            bool retreatBlockedFired = false;
            bool unitEscapedFired = false;

            Action<PotionConsumedEvent> potionHandler = (e) =>
            {
                if (e.UserName == "HeroA" && e.ItemId == "item_hp") potionConsumedFired = true;
            };
            Action<BossFightStartedEvent> bossHandler = (e) =>
            {
                bossFightFired = true;
            };
            Action<LeaderRetreatBlockedEvent> blockedHandler = (e) =>
            {
                if (e.RefusalReason == CheckpointReason.RetreatBlockedFallback) retreatBlockedFired = true;
            };
            Action<UnitEscapedEvent> escapeHandler = (e) =>
            {
                if (e.UnitId == "adv_1") unitEscapedFired = true;
            };

            AdventurerEventHub.OnPotionConsumed += potionHandler;
            AdventurerEventHub.OnBossFightStarted += bossHandler;
            AdventurerEventHub.OnLeaderRetreatBlocked += blockedHandler;
            AdventurerEventHub.OnUnitEscaped += escapeHandler;

            try
            {
                // JIT warmup
                for (int i = 0; i < 10; i++)
                {
                    AdventurerEventHub.RaisePotionConsumed(_rootGo, "HeroA", ConsumableItemCategory.HealthPotion, "item_hp");
                    AdventurerEventHub.RaiseBossFightStarted(null, null, null);
                    AdventurerEventHub.RaiseLeaderRetreatBlocked(null, CheckpointReason.RetreatBlockedFallback, Vector3.zero);
                    AdventurerEventHub.RaiseUnitEscaped("adv_1", "HeroA", Vector3.zero);
                }

                GC.Collect();
                long allocStart = GC.GetAllocatedBytesForCurrentThread();

                AdventurerEventHub.RaisePotionConsumed(_rootGo, "HeroA", ConsumableItemCategory.HealthPotion, "item_hp");
                AdventurerEventHub.RaiseBossFightStarted(null, null, null);
                AdventurerEventHub.RaiseLeaderRetreatBlocked(null, CheckpointReason.RetreatBlockedFallback, Vector3.zero);
                AdventurerEventHub.RaiseUnitEscaped("adv_1", "HeroA", Vector3.zero);

                long allocEnd = GC.GetAllocatedBytesForCurrentThread();

                Assert.IsTrue(potionConsumedFired);
                Assert.IsTrue(bossFightFired);
                Assert.IsTrue(retreatBlockedFired);
                Assert.IsTrue(unitEscapedFired);
                Assert.AreEqual(0, allocEnd - allocStart, "Event hub dispatches struct payloads with zero heap allocation.");
            }
            finally
            {
                AdventurerEventHub.OnPotionConsumed -= potionHandler;
                AdventurerEventHub.OnBossFightStarted -= bossHandler;
                AdventurerEventHub.OnLeaderRetreatBlocked -= blockedHandler;
                AdventurerEventHub.OnUnitEscaped -= escapeHandler;
            }
        }

        [Test]
        public void Phase7_DoorMarker_OpenClose_TogglesBlockers()
        {
            var doorGo = new GameObject("DoorTest");
            var door = doorGo.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();

            var colliderGo = new GameObject("Collider");
            colliderGo.transform.SetParent(doorGo.transform);
            var col = colliderGo.AddComponent<BoxCollider2D>();

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(doorGo.transform);

            var so = new SerializedObject(door);
            so.FindProperty("physicalBlockerCollider").objectReferenceValue = col;
            so.FindProperty("visualObstacle").objectReferenceValue = visualGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            door.Open();
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Explored, door.State);
            Assert.IsFalse(col.enabled);
            Assert.IsFalse(visualGo.activeSelf);

            door.Close();
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Sealed, door.State);
            Assert.IsTrue(col.enabled);
            Assert.IsTrue(visualGo.activeSelf);

            door.Open();
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Explored, door.State);
            Assert.IsFalse(col.enabled);
            Assert.IsFalse(visualGo.activeSelf);

            UnityEngine.Object.DestroyImmediate(doorGo);
        }

        [Test]
        public void Phase7_DMSealAbilityController_LocksDoorAndSetsRetreatBlocked_AndReleasesOnBossDeath()
        {
            var dmGo = new GameObject("DMSealTest");
            var dmSeal = dmGo.AddComponent<DMSealAbilityController>();

            var talentManager = dmGo.AddComponent<DMTalentTreeManager>();
            var treeConfig = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
            var sealNode = ScriptableObject.CreateInstance<TalentNodeSO>();
            sealNode.Initialize("DM_CONTROL_SEAL", "Dungeon Seal", 40, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D);
            treeConfig.Initialize("TestTree", new[] { sealNode });

            var talentSo = new SerializedObject(talentManager);
            talentSo.FindProperty("customConfig").objectReferenceValue = treeConfig;
            talentSo.ApplyModifiedPropertiesWithoutUndo();
            talentManager.ForceUnlock(sealNode);

            var combatHandler = dmGo.AddComponent<CombatResultHandler>();
            dmSeal.Initialize(talentManager, combatHandler);

            var doorGo = new GameObject("BossDoor");
            var door = doorGo.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door.IsBossDoor = true;

            var roomGo = new GameObject("BossRoom");
            var room = roomGo.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room.IsBossRoom = true;
            room.AddExit(door);

            var leaderGo = new GameObject("Leader");
            var leader = leaderGo.AddComponent<PartyLeader>();

            combatHandler.BeginCombat(1);

            // Trigger boss fight started
            AdventurerEventHub.RaiseBossFightStarted(leader, room, door);

            // Fast-forward countdown
            dmSeal.TriggerImmediateSealForTesting(door, leader);

            Assert.IsTrue(dmSeal.IsSealActive);
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Sealed, door.State);
            Assert.IsTrue(leader.RetreatBlocked);

            // Now simulate boss death
            combatHandler.ReportBossDeath();

            Assert.IsFalse(dmSeal.IsSealActive);
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Explored, door.State);
            Assert.IsFalse(leader.RetreatBlocked);

            UnityEngine.Object.DestroyImmediate(leaderGo);
            UnityEngine.Object.DestroyImmediate(roomGo);
            UnityEngine.Object.DestroyImmediate(doorGo);
            UnityEngine.Object.DestroyImmediate(dmGo);
        }

        [Test]
        public void Phase7_CombatResultHandler_EscapedAdventurer_DoesNotCountAsKill_AndEndsBattleWhenAllEscaped()
        {
            var handlerGo = new GameObject("CombatHandlerTest");
            var handler = handlerGo.AddComponent<CombatResultHandler>();

            handler.BeginCombat(1);
            handler.RegisterAdventurer("hero_1", "Fleeing Hero 1", 100f);
            handler.RegisterAdventurer("hero_2", "Fleeing Hero 2", 100f);

            Assert.AreEqual(2, handler.TotalAdventurersCount);
            Assert.AreEqual(2, handler.ActiveAdventurersCount);
            Assert.AreEqual(0, handler.EscapedAdventurersCount);
            Assert.AreEqual(0, handler.DefeatedAdventurersCount);

            // First hero escapes
            handler.ReportAdventurerEscape("hero_1");
            Assert.AreEqual(1, handler.ActiveAdventurersCount);
            Assert.AreEqual(1, handler.EscapedAdventurersCount);
            Assert.AreEqual(0, handler.DefeatedAdventurersCount, "Escaped heroes do not count as kills.");
            Assert.AreEqual(CombatState.CombatActive, handler.CurrentCombatState);

            // Second hero escapes -> all units out of dungeon
            handler.ReportAdventurerEscape("hero_2");
            Assert.AreEqual(0, handler.ActiveAdventurersCount);
            Assert.AreEqual(2, handler.EscapedAdventurersCount);
            Assert.AreEqual(0, handler.DefeatedAdventurersCount, "Defeated count remains 0 when all escape.");

            // Outcome evaluated as victory for dungeon defense
            Assert.AreEqual(BattleOutcome.Victory, handler.EvaluatedOutcome);
            Assert.AreEqual(CombatState.AwaitingAcknowledgment, handler.CurrentCombatState);
            Assert.IsTrue(handler.LastResultData.HasValue);

            var result = handler.LastResultData.Value;
            Assert.AreEqual(2, result.EscapedAdventurers);
            Assert.AreEqual(0, result.DefeatedAdventurers);
            Assert.IsNotNull(result.Report);
            Assert.AreEqual(2, result.Report.Escapes.Count);

            UnityEngine.Object.DestroyImmediate(handlerGo);
        }

        [Test]
        public void Phase7_PostBattleReport_RecordsEventsUpToCapacity()
        {
            var report = new PostBattleReport();

            for (int i = 0; i < PostBattleReport.MAX_CAPACITY + 10; i++)
            {
                report.RecordEscape($"unit_{i}", $"Hero_{i}", i);
                report.RecordPotionUsed($"Hero_{i}", ConsumableItemCategory.HealthPotion, $"item_{i}", i);
                report.RecordBlockedRetreat($"Hero_{i}", CheckpointReason.RetreatBlockedFallback, i);
                report.RecordDeath($"Hero_{i}", Vector3.zero, i);
            }

            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.Escapes.Count);
            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.PotionsUsed.Count);
            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.BlockedRetreats.Count);
            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.Deaths.Count);

            report.Clear();
            Assert.AreEqual(0, report.Escapes.Count);
            Assert.AreEqual(0, report.PotionsUsed.Count);
            Assert.AreEqual(0, report.BlockedRetreats.Count);
            Assert.AreEqual(0, report.Deaths.Count);
        }

        [Test]
        public void Phase8_ExtraItemIds_KitResolution_ResolvesWarpCategory()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.extraItemIds = new List<string> { "ITEM_CONSUMABLE_WARP" };
            baseStats.characterFunction = AdventurerFunction.DPS;

            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);

            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.ResolveRuntimeKit();

            Assert.Greater(kitCtrl.GetRemainingCount(ConsumableItemCategory.Warp), 0);
            Assert.AreEqual(1, kitCtrl.GetRemainingCount(ConsumableItemCategory.Warp));

            UnityEngine.Object.DestroyImmediate(baseStats);
        }

        [Test]
        public void Phase8_WarpCastCompletion_MarksEscaped_DeactivatesCarrier_AndRaisesEvents()
        {
            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 100f;
            baseStats.extraItemIds = new List<string> { "ITEM_CONSUMABLE_WARP" };
            stats.SetBaseStats(baseStats);

            var member = _rootGo.AddComponent<SquadMember>();
            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.ResolveRuntimeKit();

            bool warpStartedFired = false;
            bool warpCompletedFired = false;
            bool unitEscapedFired = false;

            Action<AdventurerWarpEventData> onStarted = (e) =>
            {
                if (e.User == _rootGo) warpStartedFired = true;
            };
            Action<AdventurerWarpEventData> onCompleted = (e) =>
            {
                if (e.User == _rootGo) warpCompletedFired = true;
            };
            Action<UnitEscapedEvent> onEscaped = (e) =>
            {
                if (e.UnitName == _rootGo.name) unitEscapedFired = true;
            };

            AdventurerEventHub.OnWarpStarted += onStarted;
            AdventurerEventHub.OnWarpCompleted += onCompleted;
            AdventurerEventHub.OnUnitEscaped += onEscaped;

            try
            {
                Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.Warp));
                Assert.IsTrue(kitCtrl.IsCastingItem);
                Assert.AreEqual(ConsumableItemCategory.Warp, kitCtrl.CastingCategory);
                Assert.IsTrue(warpStartedFired);

                // Advance cast to completion
                kitCtrl.AdvanceCast(10f);

                Assert.IsFalse(kitCtrl.IsCastingItem);
                Assert.IsTrue(warpCompletedFired);
                Assert.IsTrue(unitEscapedFired);
                Assert.IsTrue(member.IsEscaped);
                Assert.IsFalse(_rootGo.activeSelf);
            }
            finally
            {
                AdventurerEventHub.OnWarpStarted -= onStarted;
                AdventurerEventHub.OnWarpCompleted -= onCompleted;
                AdventurerEventHub.OnUnitEscaped -= onEscaped;
                UnityEngine.Object.DestroyImmediate(baseStats);
            }
        }

        [Test]
        public void Phase8_LeaderSuccession_WhenLeaderWarps_PromotesNextLivingCandidate()
        {
            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();

            var member2Go = new GameObject("Member2_Candidate");
            var member2 = member2Go.AddComponent<SquadMember>();
            var stats2 = member2Go.AddComponent<CharacterStatsManager>();
            var baseStats2 = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats2.MaxLife = 100f;
            baseStats2.characterFunction = AdventurerFunction.DPS;
            stats2.SetBaseStats(baseStats2);

            var member3Go = new GameObject("Member3_Escaped");
            var member3 = member3Go.AddComponent<SquadMember>();
            var stats3 = member3Go.AddComponent<CharacterStatsManager>();
            var baseStats3 = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats3.MaxLife = 100f;
            baseStats3.characterFunction = AdventurerFunction.Tank;
            stats3.SetBaseStats(baseStats3);
            member3.IsEscaped = true; // Candidate 3 already escaped

            leader.RegisterMember(member2);
            leader.RegisterMember(member3);

            bool leaderChangedFired = false;
            Action<LeaderChangedEvent> onLeaderChanged = (e) =>
            {
                if (e.NewLeader != null && e.NewLeader.gameObject == member2Go) leaderChangedFired = true;
            };
            AdventurerEventHub.OnLeaderChanged += onLeaderChanged;

            try
            {
                // Leader warps out
                leader.HandleLeaderDeparted("Warped");

                Assert.IsTrue(leader.IsEscaped);
                Assert.IsTrue(leaderChangedFired, "Promoted candidate must become new leader.");
            }
            finally
            {
                AdventurerEventHub.OnLeaderChanged -= onLeaderChanged;
                UnityEngine.Object.DestroyImmediate(member2Go);
                UnityEngine.Object.DestroyImmediate(member3Go);
                UnityEngine.Object.DestroyImmediate(baseStats2);
                UnityEngine.Object.DestroyImmediate(baseStats3);
            }
        }

        [Test]
        public void Phase8_CarrierDyingWithUnusedWarp_ForfeitsBonusEnergyToDM_AndRaisesEvent()
        {
            var dmGo = new GameObject("DM_Instance");
            var dm = dmGo.AddComponent<DungeonMaster>();
            int initialEnergy = dm.Energy;

            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 50f;
            baseStats.extraItemIds = new List<string> { "ITEM_CONSUMABLE_WARP" };
            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);

            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.ForfeitBonusEnergy = 15;
            kitCtrl.ResolveRuntimeKit();

            bool forfeitEventFired = false;
            Action<AdventurerWarpForfeitEventData> onForfeit = (e) =>
            {
                if (e.User == _rootGo && e.EnergyAwarded == 15) forfeitEventFired = true;
            };
            AdventurerEventHub.OnWarpItemForfeited += onForfeit;

            try
            {
                Assert.AreEqual(1, kitCtrl.GetRemainingCount(ConsumableItemCategory.Warp));

                // Carrier dies
                stats.TakeDamage(new DamageData(100f, DamageType.True));

                Assert.IsTrue(forfeitEventFired, "AdventurerEventHub.OnWarpItemForfeited must fire on death with unused warp.");
                Assert.AreEqual(initialEnergy + 15, dm.Energy, "DM Energy must increase by forfeit bonus.");
            }
            finally
            {
                AdventurerEventHub.OnWarpItemForfeited -= onForfeit;
                UnityEngine.Object.DestroyImmediate(baseStats);
                UnityEngine.Object.DestroyImmediate(dmGo);
            }
        }

        [Test]
        public void Phase8_DMSeal_OpensWhenAllLivingInteriorMembersEscapedOrDead()
        {
            var dmGo = new GameObject("DMSealTest");
            var dmSeal = dmGo.AddComponent<DMSealAbilityController>();

            var talentManager = dmGo.AddComponent<DMTalentTreeManager>();
            var treeConfig = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
            var sealNode = ScriptableObject.CreateInstance<TalentNodeSO>();
            sealNode.Initialize("DM_CONTROL_SEAL", "Dungeon Seal", 40, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D);
            treeConfig.Initialize("TestTree", new[] { sealNode });

            var talentSo = new SerializedObject(talentManager);
            talentSo.FindProperty("customConfig").objectReferenceValue = treeConfig;
            talentSo.ApplyModifiedPropertiesWithoutUndo();
            talentManager.ForceUnlock(sealNode);

            var combatHandler = dmGo.AddComponent<CombatResultHandler>();
            dmSeal.Initialize(talentManager, combatHandler);

            var doorGo = new GameObject("BossDoor");
            var door = doorGo.AddComponent<SBGames.Dungeon.Markers.DoorMarker>();
            door.IsBossDoor = true;

            var roomGo = new GameObject("BossRoom");
            var room = roomGo.AddComponent<SBGames.Dungeon.Markers.RoomVolume>();
            room.IsBossRoom = true;
            room.SetBounds(new Bounds(Vector3.zero, Vector3.one * 20f));
            room.AddExit(door);

            var leaderGo = _rootGo;
            var leader = leaderGo.AddComponent<PartyLeader>();
            var leaderStats = leaderGo.AddComponent<CharacterStatsManager>();
            var leaderBaseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            leaderBaseStats.MaxLife = 100f;
            leaderStats.SetBaseStats(leaderBaseStats);

            var memberGo = new GameObject("MemberInside");
            var member = memberGo.AddComponent<SquadMember>();
            var memberStats = memberGo.AddComponent<CharacterStatsManager>();
            var memberBaseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            memberBaseStats.MaxLife = 100f;
            memberStats.SetBaseStats(memberBaseStats);
            leader.RegisterMember(member);

            combatHandler.BeginCombat(1);

            // Trigger seal
            dmSeal.TriggerImmediateSealForTesting(door, leader, room);
            Assert.IsTrue(dmSeal.IsSealActive);
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Sealed, door.State);

            // While both alive inside room, seal condition not satisfied
            Assert.IsFalse(dmSeal.EvaluateSealReleaseForTesting());

            // Member dies
            memberStats.TakeDamage(new DamageData(100f, DamageType.True));
            Assert.IsFalse(dmSeal.EvaluateSealReleaseForTesting(), "Leader is still alive inside room.");

            // Leader escapes (via warp)
            leader.IsEscaped = true;

            // Now all interior members are either dead or escaped -> seal opens!
            Assert.IsTrue(dmSeal.EvaluateSealReleaseForTesting(), "Seal must open when all interior members dead or escaped.");
            Assert.IsFalse(dmSeal.IsSealActive);
            Assert.AreEqual(SBGames.Dungeon.Markers.DoorState.Explored, door.State);
            Assert.IsFalse(leader.RetreatBlocked);

            UnityEngine.Object.DestroyImmediate(memberGo);
            UnityEngine.Object.DestroyImmediate(roomGo);
            UnityEngine.Object.DestroyImmediate(doorGo);
            UnityEngine.Object.DestroyImmediate(dmGo);
            UnityEngine.Object.DestroyImmediate(leaderBaseStats);
            UnityEngine.Object.DestroyImmediate(memberBaseStats);
        }

        [Test]
        public void Phase8_UtilityWarpGoal_EvaluatesHysteresisDuringCast()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterBaseStatSO>();
            baseStats.MaxLife = 100f;
            baseStats.extraItemIds = new List<string> { "ITEM_CONSUMABLE_WARP" };

            var stats = _rootGo.AddComponent<CharacterStatsManager>();
            stats.SetBaseStats(baseStats);

            var kitCtrl = _rootGo.AddComponent<AdventurerItemKitController>();
            kitCtrl.ResolveRuntimeKit();

            var ai = _rootGo.AddComponent<BaseAI>();
            var warpGoal = ScriptableObject.CreateInstance<UtilityWarpGoal>();

            // When not casting, score evaluates based on health deficit
            float idleScore = warpGoal.Evaluate(ai);
            Assert.Less(idleScore, 1.0f);

            // Start casting warp
            Assert.IsTrue(kitCtrl.TryStartUse(ConsumableItemCategory.Warp));
            Assert.IsTrue(kitCtrl.IsCastingItem);

            // During active warp cast, commitment score must be 1.10f
            float activeScore = warpGoal.Evaluate(ai);
            Assert.AreEqual(1.10f, activeScore, 0.0001f, "UtilityWarpGoal must return 1.10f commitment score during active warp cast.");

            UnityEngine.Object.DestroyImmediate(warpGoal);
            UnityEngine.Object.DestroyImmediate(baseStats);
        }

        [Test]
        public void Phase8_PostBattleReport_RecordsWarpUsageAndForfeit()
        {
            var report = new PostBattleReport();

            for (int i = 0; i < PostBattleReport.MAX_CAPACITY + 5; i++)
            {
                report.RecordWarpUsage($"Hero_{i}", "ITEM_CONSUMABLE_WARP", "Completed", i * 1.5f);
                report.RecordWarpForfeit($"Hero_{i}", "ITEM_CONSUMABLE_WARP", 10, i * 2.0f);
            }

            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.WarpUsages.Count);
            Assert.AreEqual(PostBattleReport.MAX_CAPACITY, report.WarpForfeits.Count);

            Assert.AreEqual("Hero_0", report.WarpUsages[0].UserName);
            Assert.AreEqual("ITEM_CONSUMABLE_WARP", report.WarpUsages[0].ItemId);
            Assert.AreEqual("Completed", report.WarpUsages[0].Outcome);

            Assert.AreEqual("Hero_0", report.WarpForfeits[0].UserName);
            Assert.AreEqual(10, report.WarpForfeits[0].EnergyAwarded);

            report.Clear();
            Assert.AreEqual(0, report.WarpUsages.Count);
            Assert.AreEqual(0, report.WarpForfeits.Count);
        }
    }
}
#endif
