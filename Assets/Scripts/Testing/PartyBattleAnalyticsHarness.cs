using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SB4b1.GameMath;

namespace SBGames.Testing
{
    /// <summary>
    /// Specialized battle testing and deep analytics harness for the 5-character adventurer party
    /// (Warrior, Mage, Rogue, Scout, Cleric) across test scenarios 1-7.
    /// Tracks goals, actions, positioning, skills, focus-fire, flip-flopping, and jitter.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class PartyBattleAnalyticsHarness : MonoBehaviour
    {
        #region Data Structures

        [Serializable]
        public class GoalTimelineSegment
        {
            public string goalName;
            public float duration;
        }

        [Serializable]
        public class ActionTimelineSegment
        {
            public string actionName;
            public float duration;
        }

        [Serializable]
        public class SkillMetric
        {
            public string skillName;
            public int castCount;
            public int castsAtEffectiveRange;
        }

        [Serializable]
        public class CharacterTelemetryRecord
        {
            public string roleName; // Warrior, Mage, Rogue, Scout, Cleric
            public bool isAlive;
            public float damageDealt;
            public float damageTaken;
            public float healingReceived;
            public float healingDone;

            // Positioning metrics
            public float avgDistToLeader;
            public float avgDistToNearestEnemy;

            // Goal & Action metrics
            public int goalFlipFlopCount;
            public List<GoalTimelineSegment> goalTimeline = new();
            public List<ActionTimelineSegment> actionTimeline = new();

            // Skill telemetry
            public List<SkillMetric> skills = new();

            // Quality & Jitter
            public float stuckOrIdleDuration;
            public int jitterCount;
            public bool hasNaN;
        }

        [Serializable]
        public class ScenarioRunResult
        {
            public int scenarioIndex;
            public string scenarioName;
            public int seed;
            public BattleSimulationTestRunner.BattleOutcome outcome;
            public float duration;
            public float focusFireTargetAgreementRate; // Fraction of frames where attackers shared the same target
            public List<CharacterTelemetryRecord> characterTelemetry = new();
            public List<string> errorsAndWarnings = new();
        }

        [Serializable]
        public class ScenarioAggregateSummary
        {
            public int scenarioIndex;
            public string scenarioName;
            public int totalRuns;
            public int wins;
            public int losses;
            public int draws;
            public int timeouts;
            public float winRate;
            public float avgDuration;
            public float durationVariance;
            public float avgFocusFireRate;
            public List<ScenarioRunResult> runs = new();
        }

        #endregion

        #region Serialized Prefabs & Assets

        [Header("Party Character Prefabs")]
        [SerializeField] private GameObject warriorPrefab; // N001
        [SerializeField] private GameObject magePrefab;    // N003
        [SerializeField] private GameObject roguePrefab;   // N005
        [SerializeField] private GameObject scoutPrefab;   // N002 or N005 with Scout role
        [SerializeField] private GameObject clericPrefab;  // N001 or N004 with Healer role

        [Header("Monster Prefabs")]
        [SerializeField] private GameObject monsterPrefab1; // N001
        [SerializeField] private GameObject monsterPrefab2; // N002
        [SerializeField] private GameObject monsterPrefab3; // N003
        [SerializeField] private GameObject bossPrefab;     // BOSS_001

        [Header("AI Profiles")]
        [SerializeField] private AdventurerAIProfile defaultProfileRankD;

        [Header("Runtime Runner")]
        [SerializeField] private BattleSimulationTestRunner runner;

        #endregion

        #region Runtime State

        public bool IsRunning { get; private set; }
        public readonly List<ScenarioAggregateSummary> aggregateSummaries = new();

        #endregion

        private void Awake()
        {
            if (runner == null)
            {
                runner = GetComponent<BattleSimulationTestRunner>() ?? FindAnyObjectByType<BattleSimulationTestRunner>();
            }
            LoadAssets();
        }

        public void LoadAssets()
        {
#if UNITY_EDITOR
            if (warriorPrefab == null)
                warriorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N001.prefab");
            if (magePrefab == null)
                magePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N003.prefab");
            if (roguePrefab == null)
                roguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N005.prefab");
            if (scoutPrefab == null)
                scoutPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N005.prefab");
            if (clericPrefab == null)
                clericPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N001.prefab");

            if (monsterPrefab1 == null)
                monsterPrefab1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");
            if (monsterPrefab2 == null)
                monsterPrefab2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N002.prefab");
            if (monsterPrefab3 == null)
                monsterPrefab3 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab");
            if (bossPrefab == null)
                bossPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Monster/Boss/MONSTER_BOSS_001.prefab");

            if (defaultProfileRankD == null)
                defaultProfileRankD = UnityEditor.AssetDatabase.LoadAssetAtPath<AdventurerAIProfile>("Assets/ScriptableObjects/AIProfiles/Profile_Rank_D.asset");
#endif
        }

        /// <summary>
        /// Populates runner roster with the standard 5-character adventurer party.
        /// Warrior (Leader, Front), Mage (Backline), Rogue (Flank), Scout (Recon/Flank), Cleric (Backline/Support).
        /// </summary>
        public void SetupFiveAdventurerParty(bool lowHpScenario = false)
        {
            if (runner == null) return;
            LoadAssets();

            // 1. Warrior (Party Leader, Tank, Front-line)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "Adventurer Warrior (Leader)",
                unitPrefab = warriorPrefab,
                customSpawnPosition = new Vector3(1.5f, 0.0f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.Tank,
                profileOverride = defaultProfileRankD,
                isPartyLeader = true,
                overrideStats = lowHpScenario,
                customMaxLife = lowHpScenario ? 120f : 120f
            });

            // 2. Mage (DPS, Back-line)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "Adventurer Mage",
                unitPrefab = magePrefab,
                customSpawnPosition = new Vector3(3.5f, 1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.DPS,
                profileOverride = defaultProfileRankD,
                isPartyLeader = false,
                overrideStats = lowHpScenario,
                customMaxLife = lowHpScenario ? 70f : 70f
            });

            // 3. Rogue (DPS, Flank)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "Adventurer Rogue",
                unitPrefab = roguePrefab,
                customSpawnPosition = new Vector3(2.2f, -1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.DPS,
                profileOverride = defaultProfileRankD,
                isPartyLeader = false,
                overrideStats = lowHpScenario,
                customMaxLife = lowHpScenario ? 80f : 80f
            });

            // 4. Scout (Scout, Recon)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "Adventurer Scout",
                unitPrefab = scoutPrefab,
                customSpawnPosition = new Vector3(2.2f, 1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.Scout,
                profileOverride = defaultProfileRankD,
                isPartyLeader = false,
                overrideStats = lowHpScenario,
                customMaxLife = lowHpScenario ? 80f : 80f
            });

            // 5. Cleric (Healer, Support, Back-line)
            runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
            {
                slotLabel = "Adventurer Cleric",
                unitPrefab = clericPrefab,
                customSpawnPosition = new Vector3(3.5f, -1.2f, 0f),
                faction = BattleSimulationTestRunner.BattleFaction.Adventurer,
                functionOverride = AdventurerFunction.Healer,
                profileOverride = defaultProfileRankD,
                isPartyLeader = false,
                overrideStats = lowHpScenario,
                customMaxLife = lowHpScenario ? 100f : 100f
            });
        }

        /// <summary>
        /// Configures one of the 7 test scenarios into the runner's roster.
        /// </summary>
        public void ConfigureScenarioByIndex(int scenarioIndex, int seed)
        {
            if (runner == null) return;
            LoadAssets();
            runner.Roster.Clear();

            var gameRng = new GameRng((uint)Math.Abs(seed == 0 ? 1 : seed));
            UnityEngine.Random.InitState(seed);

            switch (scenarioIndex)
            {
                case 1:
                    // Scenario 1: Baseline Party vs 1 Monster
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster Single (N001)",
                        unitPrefab = monsterPrefab1,
                        customSpawnPosition = new Vector3(-2.5f, 0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    break;

                case 2:
                    // Scenario 2: Party vs 3-5 monsters spawned clustered
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    int clusterCount = 4;
                    Vector3 clusterCenter = new Vector3(-2.8f, 0f, 0f);
                    for (int i = 0; i < clusterCount; i++)
                    {
                        float offsetX = (i % 2 == 0 ? -0.4f : 0.4f);
                        float offsetY = (i < 2 ? 0.5f : -0.5f);
                        GameObject prefab = i % 2 == 0 ? monsterPrefab1 : monsterPrefab2;
                        runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                        {
                            slotLabel = $"Monster Clustered #{i + 1}",
                            unitPrefab = prefab,
                            customSpawnPosition = clusterCenter + new Vector3(offsetX, offsetY, 0f),
                            faction = BattleSimulationTestRunner.BattleFaction.Monster
                        });
                    }
                    break;

                case 3:
                    // Scenario 3: Party vs monsters spread across the arena
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster Top (N001)",
                        unitPrefab = monsterPrefab1,
                        customSpawnPosition = new Vector3(-3.0f, 3.5f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster Mid (N002)",
                        unitPrefab = monsterPrefab2,
                        customSpawnPosition = new Vector3(-4.0f, 0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster Bottom (N003)",
                        unitPrefab = monsterPrefab3,
                        customSpawnPosition = new Vector3(-3.0f, -3.5f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    break;

                case 4:
                    // Scenario 4: Party vs strong single target (Boss)
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster Boss (BOSS_001)",
                        unitPrefab = bossPrefab != null ? bossPrefab : monsterPrefab1,
                        customSpawnPosition = new Vector3(-3.0f, 0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster,
                        overrideStats = true,
                        customMaxLife = 500f,
                        customDamage = 18f,
                        customMovespeed = 4.0f,
                        customAttackRange = 1.2f,
                        customAttackInterval = 1.0f
                    });
                    break;

                case 5:
                    // Scenario 5: Party at low HP (damaged early to test Survive, FallBack, and Cleric Heal)
                    SetupFiveAdventurerParty(lowHpScenario: true);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster 1 (N001)",
                        unitPrefab = monsterPrefab1,
                        customSpawnPosition = new Vector3(-2.5f, 1.0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster 2 (N002)",
                        unitPrefab = monsterPrefab2,
                        customSpawnPosition = new Vector3(-2.5f, -1.0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    break;

                case 6:
                    // Scenario 6: Party with leader killed early
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster 1 (N001)",
                        unitPrefab = monsterPrefab1,
                        customSpawnPosition = new Vector3(-2.5f, 1.0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Monster 2 (N002)",
                        unitPrefab = monsterPrefab2,
                        customSpawnPosition = new Vector3(-2.5f, -1.0f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    break;

                case 7:
                    // Scenario 7: Monsters start outside vision range (> 10 units away)
                    SetupFiveAdventurerParty(lowHpScenario: false);
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Distant Monster 1 (N001)",
                        unitPrefab = monsterPrefab1,
                        customSpawnPosition = new Vector3(-10.0f, 1.5f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    runner.Roster.Add(new BattleSimulationTestRunner.UnitSpawnSlot
                    {
                        slotLabel = "Distant Monster 2 (N002)",
                        unitPrefab = monsterPrefab2,
                        customSpawnPosition = new Vector3(-10.0f, -1.5f, 0f),
                        faction = BattleSimulationTestRunner.BattleFaction.Monster
                    });
                    break;
            }
        }

        /// <summary>
        /// Executes a single simulation run and records all requested telemetry.
        /// </summary>
        public IEnumerator RunSingleScenarioRoutine(int scenarioIndex, string scenarioName, int seed, float timeScale, Action<ScenarioRunResult> onComplete)
        {
            ConfigureScenarioByIndex(scenarioIndex, seed);
            runner.SetTimeScale(timeScale);

            // Seed GameRng in VisionControllers and Random systems
            var testRng = new GameRng((uint)Math.Abs(seed == 0 ? 1 : seed));
            UnityEngine.Random.InitState(seed);

            runner.StartSimulation();

            var runResult = new ScenarioRunResult
            {
                scenarioIndex = scenarioIndex,
                scenarioName = scenarioName,
                seed = seed
            };

            // Identify and link telemetries for the 5 roles
            var trackedRoles = new (string roleName, BattleSimulationTestRunner.TrackedCombatant combatant)[]
            {
                ("Warrior", null),
                ("Mage", null),
                ("Rogue", null),
                ("Scout", null),
                ("Cleric", null)
            };

            var combatants = runner.Adventurers;
            for (int i = 0; i < combatants.Count && i < 5; i++)
            {
                var c = combatants[i];
                string label = c.SourceSlot != null ? c.SourceSlot.slotLabel : "";
                if (label.Contains("Warrior")) trackedRoles[0].combatant = c;
                else if (label.Contains("Mage")) trackedRoles[1].combatant = c;
                else if (label.Contains("Rogue")) trackedRoles[2].combatant = c;
                else if (label.Contains("Scout")) trackedRoles[3].combatant = c;
                else if (label.Contains("Cleric")) trackedRoles[4].combatant = c;
                else trackedRoles[i].combatant = c;
            }

            // Prepare telemetry trackers
            var roleTelemetries = new CharacterTelemetryRecord[5];
            var lastGoals = new EnumUtilGoal[5];
            var lastGoalTimes = new float[5];
            var lastActions = new string[5];
            var lastActionTimes = new float[5];
            var distToLeaderSum = new float[5];
            var distToEnemySum = new float[5];
            var distSamples = new int[5];
            var lastPositions = new Vector3[5];
            var samePosTimes = new float[5];

            for (int i = 0; i < 5; i++)
            {
                roleTelemetries[i] = new CharacterTelemetryRecord
                {
                    roleName = trackedRoles[i].roleName,
                    isAlive = true
                };
                lastGoals[i] = EnumUtilGoal.None;
                lastGoalTimes[i] = 0f;
                lastActions[i] = "None";
                lastActionTimes[i] = 0f;
            }

            // For Scenario 5: apply early damage to party at t=0.5s to test low HP
            bool scenario5DamageApplied = false;

            // For Scenario 6: kill warrior leader early at t=1.5s
            bool scenario6LeaderKilled = false;

            float focusFireAgreementFrames = 0f;
            float totalCombatFrames = 0f;

            // Sampling loop
            while (runner.State == BattleSimulationTestRunner.SimulationState.Running)
            {
                float t = runner.ElapsedTime;
                float dt = Time.deltaTime;

                // Scenario 5 trigger: inflict damage to trigger Survive/FallBack/Heal
                if (scenarioIndex == 5 && !scenario5DamageApplied && t >= 0.5f)
                {
                    scenario5DamageApplied = true;
                    for (int i = 0; i < 5; i++)
                    {
                        var c = trackedRoles[i].combatant;
                        if (c != null && c.Stats != null)
                        {
                            // Reduce to 25% HP to trigger Survive / FallBack thresholds
                            float dmg = c.Stats.Life - (c.MaxLife * 0.25f);
                            if (dmg > 0f) c.Stats.TakeDamage(new DamageData(dmg, DamageType.True));
                        }
                    }
                }

                // Scenario 6 trigger: kill leader early
                if (scenarioIndex == 6 && !scenario6LeaderKilled && t >= 1.5f)
                {
                    scenario6LeaderKilled = true;
                    var warrior = trackedRoles[0].combatant;
                    if (warrior != null && warrior.Stats != null && warrior.IsAlive)
                    {
                        warrior.Stats.TakeDamage(new DamageData(9999f, DamageType.True));
                    }
                }

                // Locate active Leader position
                Transform currentLeaderTf = null;
                for (int i = 0; i < 5; i++)
                {
                    var c = trackedRoles[i].combatant;
                    if (c != null && c.IsAlive && c.GameObject != null)
                    {
                        var pl = c.GameObject.GetComponent<PartyLeader>();
                        if (pl != null && pl.IsAlive)
                        {
                            currentLeaderTf = pl.transform;
                            break;
                        }
                    }
                }

                // Track Target choice across party attackers
                Transform sharedTarget = null;
                bool attackersAgreed = true;
                int attackerCount = 0;

                for (int i = 0; i < 5; i++)
                {
                    var c = trackedRoles[i].combatant;
                    if (c == null || !c.IsAlive || c.GameObject == null)
                    {
                        roleTelemetries[i].isAlive = false;
                        continue;
                    }

                    Vector3 pos = c.GameObject.transform.position;

                    // NaN Check
                    if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
                    {
                        roleTelemetries[i].hasNaN = true;
                        runResult.errorsAndWarnings.Add($"NaN detected in position of {roleTelemetries[i].roleName} at t={t:F2}s");
                    }

                    // Positioning relative to leader
                    if (currentLeaderTf != null)
                    {
                        float distLeader = Vector3.Distance(pos, currentLeaderTf.position);
                        distToLeaderSum[i] += distLeader;
                    }

                    // Positioning relative to nearest monster
                    float nearestEnemyDist = float.MaxValue;
                    var monsters = runner.Monsters;
                    for (int m = 0; m < monsters.Count; m++)
                    {
                        if (monsters[m].IsAlive && monsters[m].GameObject != null)
                        {
                            float d = Vector3.Distance(pos, monsters[m].GameObject.transform.position);
                            if (d < nearestEnemyDist) nearestEnemyDist = d;
                        }
                    }
                    if (nearestEnemyDist < float.MaxValue)
                    {
                        distToEnemySum[i] += nearestEnemyDist;
                    }
                    distSamples[i]++;

                    // Goal Tracking & Flip-flopping
                    if (c.AI != null)
                    {
                        EnumUtilGoal g = c.AI.CurrentGoalKey;
                        if (g != lastGoals[i])
                        {
                            // Goal transition
                            if (lastGoals[i] != EnumUtilGoal.None)
                            {
                                float duration = t - lastGoalTimes[i];
                                roleTelemetries[i].goalTimeline.Add(new GoalTimelineSegment
                                {
                                    goalName = lastGoals[i].ToString(),
                                    duration = duration
                                });

                                // Flip-flop check: switched goal within < 0.4 seconds
                                if (duration < 0.4f)
                                {
                                    roleTelemetries[i].goalFlipFlopCount++;
                                }
                            }
                            lastGoals[i] = g;
                            lastGoalTimes[i] = t;
                        }

                        // Target agreement (FocusFire check)
                        if (c.AI.CurrentTarget != null)
                        {
                            attackerCount++;
                            if (sharedTarget == null)
                            {
                                sharedTarget = c.AI.CurrentTarget;
                            }
                            else if (sharedTarget != c.AI.CurrentTarget)
                            {
                                attackersAgreed = false;
                            }
                        }
                    }

                    // GOAP Action Tracking
                    var agentBehaviour = c.GameObject.GetComponentInChildren<CrashKonijn.Agent.Runtime.AgentBehaviour>();
                    string currentActionName = "None";
                    if (agentBehaviour != null && agentBehaviour.CurrentAction != null)
                    {
                        currentActionName = agentBehaviour.CurrentAction.GetType().Name;
                    }
                    if (currentActionName != lastActions[i])
                    {
                        if (lastActions[i] != "None")
                        {
                            roleTelemetries[i].actionTimeline.Add(new ActionTimelineSegment
                            {
                                actionName = lastActions[i],
                                duration = t - lastActionTimes[i]
                            });
                        }
                        lastActions[i] = currentActionName;
                        lastActionTimes[i] = t;
                    }

                    // Stuck or Jitter detection
                    if (c.Agent != null && c.Agent.isActiveAndEnabled && c.Agent.isOnNavMesh)
                    {
                        float deltaPos = (pos - lastPositions[i]).magnitude;
                        if (c.Agent.hasPath && deltaPos < 0.01f)
                        {
                            samePosTimes[i] += dt;
                            if (samePosTimes[i] > 1.0f)
                            {
                                roleTelemetries[i].stuckOrIdleDuration += dt;
                            }
                        }
                        else
                        {
                            samePosTimes[i] = 0f;
                        }

                        // Jitter: high agent velocity direction changes without net position translation
                        if (c.Agent.velocity.sqrMagnitude > 1.0f && deltaPos < 0.005f)
                        {
                            roleTelemetries[i].jitterCount++;
                        }
                    }
                    lastPositions[i] = pos;
                }

                if (attackerCount > 1)
                {
                    totalCombatFrames++;
                    if (attackersAgreed) focusFireAgreementFrames++;
                }

                yield return null;
            }

            // Close lingering timelines at end of simulation
            float finalT = runner.ElapsedTime;
            for (int i = 0; i < 5; i++)
            {
                if (lastGoals[i] != EnumUtilGoal.None)
                {
                    roleTelemetries[i].goalTimeline.Add(new GoalTimelineSegment
                    {
                        goalName = lastGoals[i].ToString(),
                        duration = finalT - lastGoalTimes[i]
                    });
                }
                if (lastActions[i] != "None")
                {
                    roleTelemetries[i].actionTimeline.Add(new ActionTimelineSegment
                    {
                        actionName = lastActions[i],
                        duration = finalT - lastActionTimes[i]
                    });
                }

                var c = trackedRoles[i].combatant;
                if (c != null)
                {
                    roleTelemetries[i].isAlive = c.IsAlive;
                    roleTelemetries[i].damageDealt = c.DamageDealt;
                    roleTelemetries[i].damageTaken = c.DamageTaken;
                    roleTelemetries[i].healingReceived = c.HealingReceived;
                    roleTelemetries[i].healingDone = c.SkillHealingReceived; // tracked per receiver or caster
                }

                int samples = Mathf.Max(1, distSamples[i]);
                roleTelemetries[i].avgDistToLeader = distToLeaderSum[i] / samples;
                roleTelemetries[i].avgDistToNearestEnemy = distToEnemySum[i] / samples;

                runResult.characterTelemetry.Add(roleTelemetries[i]);
            }

            runResult.duration = runner.ElapsedTime;
            runResult.outcome = runner.CurrentOutcome;
            runResult.focusFireTargetAgreementRate = totalCombatFrames > 0f ? (focusFireAgreementFrames / totalCombatFrames) : 1f;

            runner.ResetSimulation(resetTimeScale: false);
            yield return null;

            onComplete?.Invoke(runResult);
        }

        /// <summary>
        /// Runs a scenario first with fixed seed 42, then 20 random seeds, and aggregates statistics.
        /// </summary>
        public IEnumerator RunFullScenarioSuiteRoutine(int scenarioIndex, string scenarioName, float timeScale, Action<ScenarioAggregateSummary> onSuiteComplete)
        {
            var summary = new ScenarioAggregateSummary
            {
                scenarioIndex = scenarioIndex,
                scenarioName = scenarioName
            };

            // Fixed seed run first
            int fixedSeed = 42;
            ScenarioRunResult fixedResult = null;
            yield return StartCoroutine(RunSingleScenarioRoutine(scenarioIndex, scenarioName, fixedSeed, timeScale, res => fixedResult = res));
            summary.runs.Add(fixedResult);

            // 20 randomized seeds
            int[] seeds = new int[20];
            var rng = new GameRng(1001u + (uint)scenarioIndex * 77u);
            for (int i = 0; i < 20; i++)
            {
                seeds[i] = rng.Range(10000, 99999);
            }

            for (int i = 0; i < 20; i++)
            {
                ScenarioRunResult randomResult = null;
                yield return StartCoroutine(RunSingleScenarioRoutine(scenarioIndex, scenarioName, seeds[i], timeScale, res => randomResult = res));
                summary.runs.Add(randomResult);
            }

            // Compute aggregates
            summary.totalRuns = summary.runs.Count;
            float durationSum = 0f;
            float focusFireSum = 0f;

            for (int i = 0; i < summary.runs.Count; i++)
            {
                var r = summary.runs[i];
                durationSum += r.duration;
                focusFireSum += r.focusFireTargetAgreementRate;

                switch (r.outcome)
                {
                    case BattleSimulationTestRunner.BattleOutcome.AdventurersWin:
                        summary.wins++;
                        break;
                    case BattleSimulationTestRunner.BattleOutcome.MonstersWin:
                        summary.losses++;
                        break;
                    case BattleSimulationTestRunner.BattleOutcome.Draw:
                        summary.draws++;
                        break;
                    case BattleSimulationTestRunner.BattleOutcome.Timeout:
                        summary.timeouts++;
                        break;
                }
            }

            summary.winRate = (float)summary.wins / summary.totalRuns;
            summary.avgDuration = durationSum / summary.totalRuns;
            summary.avgFocusFireRate = focusFireSum / summary.totalRuns;

            // Duration Variance
            float varianceSum = 0f;
            for (int i = 0; i < summary.runs.Count; i++)
            {
                float diff = summary.runs[i].duration - summary.avgDuration;
                varianceSum += diff * diff;
            }
            summary.durationVariance = varianceSum / summary.totalRuns;

            aggregateSummaries.Add(summary);
            Debug.Log($"<b>[PARTY HARNESS SUITE COMPLETE]</b> Scenario {scenarioIndex}: {scenarioName} | WinRate: {summary.winRate * 100f:F1}% ({summary.wins}/{summary.totalRuns}) | AvgDuration: {summary.avgDuration:F2}s (Var: {summary.durationVariance:F2}) | FocusFireRate: {summary.avgFocusFireRate * 100f:F1}%");

            onSuiteComplete?.Invoke(summary);
        }

        /// <summary>
        /// Master coroutine executing all 7 scenarios across 1 fixed seed + 20 random seeds each.
        /// </summary>
        public IEnumerator RunAllSevenScenariosRoutine(float timeScale = 5f, Action onAllDone = null)
        {
            IsRunning = true;
            aggregateSummaries.Clear();

            var scenarioDefs = new (int idx, string name)[]
            {
                (1, "Party vs 1 Monster (Baseline Engagement & Roles)"),
                (2, "Party vs Clustered Monsters (3-4 Enemies)"),
                (3, "Party vs Spread Monsters (Arena Spread / Formation Check)"),
                (4, "Party vs Strong Target (Boss / FocusFire Check)"),
                (5, "Party at Low HP (Survive, FallBack & Cleric Heal Check)"),
                (6, "Party Leader Killed Early (Squad Succession & Functionality)"),
                (7, "Monsters Outside Vision (ScoutRecon & FollowLeader Check)")
            };

            foreach (var sc in scenarioDefs)
            {
                Debug.Log($"<color=yellow>>>> STARTING SUITE {sc.idx}: {sc.name} <<<</color>");
                yield return StartCoroutine(RunFullScenarioSuiteRoutine(sc.idx, sc.name, timeScale, null));
            }

            IsRunning = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            Debug.Log("<color=green><b>[ALL 7 SCENARIOS COMPLETED SUCCESSFULLY!]</b></color>");
            onAllDone?.Invoke();
        }
    }
}
