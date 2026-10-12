#if UNITY_EDITOR
using System;
using Micosmo.SensorToolkit;
using NUnit.Framework;
using SBGames.Characters.AI;
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
            _rootGo = new GameObject("Test_Adventurer");
        }

        [TearDown]
        public void TearDown()
        {
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
    }
}
#endif
