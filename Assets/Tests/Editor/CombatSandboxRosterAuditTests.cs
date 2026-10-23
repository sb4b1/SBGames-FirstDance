using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SBGames.Testing;

namespace SBGames.Tests
{
    public class CombatSandboxRosterAuditTests
    {
        [Test]
        public void AuditAndPrintTargetRoster()
        {
            var prefabPaths = new Dictionary<string, string>
            {
                { "ADVENTURER_N001", "Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N001.prefab" },
                { "ADVENTURER_N002", "Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N002.prefab" },
                { "ADVENTURER_N003", "Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N003.prefab" },
                { "ADVENTURER_N004", "Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N004.prefab" },
                { "ADVENTURER_N005", "Assets/Prefabs/Character/Adventurer/Normal/ADVENTURER_N005.prefab" },
                { "MONSTER_N001", "Assets/Prefabs/Character/Monster/Normal/MONSTER_N001.prefab" }
            };

            Debug.Log("================ ROSTER AUDIT START ================");
            foreach (var kvp in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kvp.Value);
                if (prefab == null)
                {
                    Debug.Log($"[ROSTER AUDIT] {kvp.Key}: PREFAB NOT FOUND on disk at '{kvp.Value}'");
                    continue;
                }

                var stats = prefab.GetComponent<CharacterStatsManager>() ?? prefab.GetComponentInChildren<CharacterStatsManager>(true);
                var ai = prefab.GetComponent<BaseAI>() ?? prefab.GetComponentInChildren<BaseAI>(true);

                string charId = prefab.name;
                string function = stats != null ? stats.CharacterFunction.ToString() : "None";
                string rank = (ai != null && ai.AdventurerProfile != null) ? ai.AdventurerProfile.Rank.ToString() : (stats != null && stats.BaseStats != null ? stats.BaseStats.characterRank.ToString() : "Unknown");
                string basicAttackMode = stats != null ? stats.BasicAttackMode.ToString() : "Auto";
                string keepDistanceMode = stats != null ? stats.KeepDistanceMode.ToString() : "Auto";

                var skillList = new List<string>();
                var skillCtrl = prefab.GetComponent<CharacterSkillController>() ?? prefab.GetComponentInChildren<CharacterSkillController>(true);
                if (skillCtrl != null && skillCtrl.Skills != null)
                {
                    foreach (var s in skillCtrl.Skills)
                    {
                        if (s != null && s.SkillPrefab != null)
                        {
                            skillList.Add(s.SkillPrefab.name);
                        }
                    }
                }
                string equippedSkills = skillList.Count > 0 ? string.Join(", ", skillList) : "None";

                string agentType = "Unknown";
                if (ai != null)
                {
                    var serializedAI = new SerializedObject(ai);
                    var agentTypeProp = serializedAI.FindProperty("agentType");
                    if (agentTypeProp != null) agentType = agentTypeProp.stringValue;
                }

                var goalList = new List<string>();
                if (ai != null && ai.Goals != null)
                {
                    foreach (var g in ai.Goals)
                    {
                        if (g != null) goalList.Add(g.name);
                    }
                }
                string goals = goalList.Count > 0 ? string.Join(", ", goalList) : "None";

                Debug.Log($"[ROSTER AUDIT] CharacterId: {charId} | CharacterFunction: {function} | Rank: {rank} | BasicAttackMode: {basicAttackMode} | KeepDistanceMode: {keepDistanceMode} | Skills: [{equippedSkills}] | AgentType: {agentType} | BaseAI Goals: [{goals}]");
            }
            Debug.Log("================ ROSTER AUDIT END ================");
        }
    }
}
