using System.Collections.Generic;
using SBGames.Loop;
using SBGames.Loop.Talents;
using UnityEditor;
using UnityEngine;

namespace SBGames.Loop.Editor
{
    /// <summary>
    /// Editor utility that automatically creates and links the full set of 21 mock TalentNodeSO assets
    /// and the DefaultTalentTreeConfig asset in 'Assets/Resources/Talents/'.
    /// </summary>
    public static class DMTalentTreeGenerator
    {
        private const string ResourceFolder = "Assets/Resources";
        private const string TalentsFolder = "Assets/Resources/Talents";
        private const string ConfigPath = "Assets/Resources/Talents/DefaultTalentTreeConfig.asset";

        [MenuItem("Tools/SBGames/Generate Mock Talent Tree Assets", priority = 100)]
        public static void GenerateMockTalentTree()
        {
            EnsureDirectories();

            var talentMap = new Dictionary<string, TalentNodeSO>();

            // 1. Core Foundation
            var core01 = CreateOrUpdateNode(
                id: "DM_CORE_01",
                name: "Dungeon Heart Awakening",
                cost: 15,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier0,
                minRank: DungeonRank.E,
                desc: "Harmonizes DM core with ley lines. Increases Max Energy capacity and restores bonus essence on clean rounds."
            );
            talentMap[core01.TalentId] = core01;

            var util01 = CreateOrUpdateNode(
                id: "DM_UTILITY_01",
                name: "Relic Transmutation",
                cost: 40,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.D,
                desc: "Enhances artifact sacrifice energy payout and discounts artifact forging essence costs."
            );
            talentMap[util01.TalentId] = util01;

            var artifactOvercharge = CreateOrUpdateNode(
                id: "DM_ARTIFACT_OVERCHARGE",
                name: "Relic Overcharge",
                cost: 45,
                pillar: TalentPillar.Core,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.C,
                desc: "Unlocks the ability to continuously empower artifacts beyond their initial tier, increasing global power levels."
            );
            talentMap[artifactOvercharge.TalentId] = artifactOvercharge;

            // 2. Swarm Legion
            var swarm01 = CreateOrUpdateNode(
                id: "DM_SWARM_01",
                name: "Brood Hivemind",
                cost: 20,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.E,
                desc: "Reduces dungeon capacity footprint of common minion units by 20%, allowing larger armies."
            );
            talentMap[swarm01.TalentId] = swarm01;

            var swarm02 = CreateOrUpdateNode(
                id: "DM_SWARM_02",
                name: "Necrotic Reanimation",
                cost: 35,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                type: TalentType.Triggered,
                desc: "Defeated non-boss minions have a 30% chance to rise as temporary Skeleton Thralls."
            );
            talentMap[swarm02.TalentId] = swarm02;

            var swarm03 = CreateOrUpdateNode(
                id: "DM_SWARM_03",
                name: "Pack Frenzy",
                cost: 35,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                desc: "Grants stacking attack speed and move speed bonuses for every 3 allied monsters alive."
            );
            talentMap[swarm03.TalentId] = swarm03;

            var swarm04 = CreateOrUpdateNode(
                id: "DM_SWARM_04",
                name: "Endless Tide",
                cost: 55,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                type: TalentType.ActivePower,
                activeCost: 10,
                cd: 45f,
                prereqLogic: TalentPrerequisiteLogic.Any,
                desc: "Active Power: Instantly summons a reinforcement pack of 3 Swarmlings at the entrance."
            );
            talentMap[swarm04.TalentId] = swarm04;

            var swarm05 = CreateOrUpdateNode(
                id: "DM_SWARM_05",
                name: "Volatile Biomass",
                cost: 60,
                pillar: TalentPillar.MonsterSwarm,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                desc: "Defeated minions detonate in a noxious cloud, dealing AoE damage and reducing adventurer defense."
            );
            talentMap[swarm05.TalentId] = swarm05;

            // 3. Eldritch Traps
            var trap01 = CreateOrUpdateNode(
                id: "DM_TRAP_01",
                name: "Rune Etching",
                cost: 20,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.E,
                desc: "Reduces the Energy cost of all Traps and Environmental Hazards by 25%."
            );
            talentMap[trap01.TalentId] = trap01;

            var trap02 = CreateOrUpdateNode(
                id: "DM_TRAP_02",
                name: "Leyline Siphon",
                cost: 35,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                type: TalentType.Triggered,
                desc: "Sprung traps harvest Energy directly into the DM pool and grant Essence on lethal strikes."
            );
            talentMap[trap02.TalentId] = trap02;

            var trap03 = CreateOrUpdateNode(
                id: "DM_TRAP_03",
                name: "Choke Snare",
                cost: 35,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                desc: "Trap detonation heavily slows adventurers and makes them take increased damage from monsters."
            );
            talentMap[trap03.TalentId] = trap03;

            var trapSeal = CreateOrUpdateNode(
                id: "DM_CONTROL_SEAL",
                name: "Dungeon Seal",
                cost: 40,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                type: TalentType.Triggered,
                desc: "Auto-Active: When adventurers breach the boss room, automatically seals the entrance door after 5s and blocks adventurer retreat until the encounter resolves."
            );
            talentMap[trapSeal.TalentId] = trapSeal;

            var trap04 = CreateOrUpdateNode(
                id: "DM_TRAP_04",
                name: "Chain Reaction",
                cost: 55,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                prereqLogic: TalentPrerequisiteLogic.Any,
                desc: "Traps triggering near other hazards trigger adjacent traps in a devastating cascade."
            );
            talentMap[trap04.TalentId] = trap04;

            var trap05 = CreateOrUpdateNode(
                id: "DM_TRAP_05",
                name: "Abyssal Barricade",
                cost: 60,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                type: TalentType.ActivePower,
                activeCost: 15,
                cd: 60f,
                desc: "Active Power: Erects an indestructible shadow barrier forcing adventurers into trap corridors."
            );
            talentMap[trap05.TalentId] = trap05;

            // 4. Apex Overlord
            var boss01 = CreateOrUpdateNode(
                id: "DM_BOSS_01",
                name: "Colossus Vessel",
                cost: 20,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier1,
                minRank: DungeonRank.E,
                desc: "Unlocks placement of Boss-tier entities. Bosses gain +30% Max Health and armor penetration."
            );
            talentMap[boss01.TalentId] = boss01;

            var boss02 = CreateOrUpdateNode(
                id: "DM_BOSS_02",
                name: "Tyrant's Presence",
                cost: 35,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                type: TalentType.Aura,
                desc: "The Boss emits a Dread Aura, reducing enemy attack speed and disrupting hero utility spells."
            );
            talentMap[boss02.TalentId] = boss02;

            var boss03 = CreateOrUpdateNode(
                id: "DM_BOSS_03",
                name: "Blood Frenzy Domain",
                cost: 35,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                desc: "When the Boss drops below 50% HP, enters Enraged Titan state with massive damage and CC immunity."
            );
            talentMap[boss03.TalentId] = boss03;

            var boss04 = CreateOrUpdateNode(
                id: "DM_BOSS_04",
                name: "Cataclysm Protocol",
                cost: 55,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                type: TalentType.ActivePower,
                activeCost: 20,
                cd: 30f,
                prereqLogic: TalentPrerequisiteLogic.Any,
                desc: "Active Power: Overcharges the Boss with essence to trigger an arena-wide stun shockwave."
            );
            talentMap[boss04.TalentId] = boss04;

            var boss05 = CreateOrUpdateNode(
                id: "DM_BOSS_05",
                name: "Soul Conduit",
                cost: 60,
                pillar: TalentPillar.ApexBoss,
                tier: TalentTier.Tier3,
                minRank: DungeonRank.C,
                desc: "Infuses the Boss with DM Artifact energy, granting damage and protection per active relic."
            );
            talentMap[boss05.TalentId] = boss05;

            // 5. Hybrids & Capstones
            var hyb01 = CreateOrUpdateNode(
                id: "DM_HYBRID_01",
                name: "Grave Mine Spores",
                cost: 80,
                pillar: TalentPillar.Hybrid,
                tier: TalentTier.Tier4,
                minRank: DungeonRank.B,
                desc: "Monster deaths seed spore traps that detonate on enemy contact and spawn mini-spores."
            );
            talentMap[hyb01.TalentId] = hyb01;

            var hyb02 = CreateOrUpdateNode(
                id: "DM_HYBRID_02",
                name: "Fortress Dreadnought",
                cost: 80,
                pillar: TalentPillar.Hybrid,
                tier: TalentTier.Tier4,
                minRank: DungeonRank.B,
                desc: "Traps beam damage reduction to the Boss and trap triggers heal the Boss."
            );
            talentMap[hyb02.TalentId] = hyb02;

            var hyb03 = CreateOrUpdateNode(
                id: "DM_HYBRID_03",
                name: "Legion Sovereign",
                cost: 80,
                pillar: TalentPillar.Hybrid,
                tier: TalentTier.Tier4,
                minRank: DungeonRank.B,
                desc: "Minions near the Boss gain high crit chance and can sacrifice themselves to absorb lethal boss damage."
            );
            talentMap[hyb03.TalentId] = hyb03;

            var asc01 = CreateOrUpdateNode(
                id: "DM_ASCENDANT_01",
                name: "Dungeon Omnipotence",
                cost: 100,
                pillar: TalentPillar.Transcendent,
                tier: TalentTier.Tier4,
                minRank: DungeonRank.A,
                type: TalentType.ActivePower,
                activeCost: 25,
                cd: 90f,
                desc: "Active Power: Boosts dungeon defense power and planning allowance. Active cast executes a hero."
            );
            talentMap[asc01.TalentId] = asc01;

            // Link Prerequisites
            util01.Initialize("DM_UTILITY_01", "Relic Transmutation", 40, TalentPillar.Core, TalentTier.Tier1, TalentType.Passive, DungeonRank.D, new[] { core01 }, desc: "Enhances artifact sacrifice energy payout and discounts artifact forging essence costs.");
            artifactOvercharge.Initialize("DM_ARTIFACT_OVERCHARGE", "Relic Overcharge", 45, TalentPillar.Core, TalentTier.Tier2, TalentType.Passive, DungeonRank.C, new[] { util01 }, desc: "Unlocks the ability to continuously empower artifacts beyond their initial tier, increasing global power levels.");
            swarm01.Initialize("DM_SWARM_01", "Brood Hivemind", 20, TalentPillar.MonsterSwarm, TalentTier.Tier1, TalentType.Passive, DungeonRank.E, new[] { core01 }, desc: "Reduces dungeon capacity footprint of common minion units by 20%, allowing larger armies.");
            swarm02.Initialize("DM_SWARM_02", "Necrotic Reanimation", 35, TalentPillar.MonsterSwarm, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D, new[] { swarm01 }, desc: "Defeated non-boss minions have a 30% chance to rise as temporary Skeleton Thralls.");
            swarm03.Initialize("DM_SWARM_03", "Pack Frenzy", 35, TalentPillar.MonsterSwarm, TalentTier.Tier2, TalentType.Passive, DungeonRank.D, new[] { swarm01 }, desc: "Grants stacking attack speed and move speed bonuses for every 3 allied monsters alive.");
            swarm04.Initialize("DM_SWARM_04", "Endless Tide", 55, TalentPillar.MonsterSwarm, TalentTier.Tier3, TalentType.ActivePower, DungeonRank.C, new[] { swarm02, swarm03 }, TalentPrerequisiteLogic.Any, 10, 45f, desc: "Active Power: Instantly summons a reinforcement pack of 3 Swarmlings at the entrance.");
            swarm05.Initialize("DM_SWARM_05", "Volatile Biomass", 60, TalentPillar.MonsterSwarm, TalentTier.Tier3, TalentType.Passive, DungeonRank.C, new[] { swarm02, swarm03 }, desc: "Defeated minions detonate in a noxious cloud, dealing AoE damage and reducing adventurer defense.");

            trap01.Initialize("DM_TRAP_01", "Rune Etching", 20, TalentPillar.TrapControl, TalentTier.Tier1, TalentType.Passive, DungeonRank.E, new[] { core01 }, desc: "Reduces the Energy cost of all Traps and Environmental Hazards by 25%.");
            trap02.Initialize("DM_TRAP_02", "Leyline Siphon", 35, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D, new[] { trap01 }, desc: "Sprung traps harvest Energy directly into the DM pool and grant Essence on lethal strikes.");
            trap03.Initialize("DM_TRAP_03", "Choke Snare", 35, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Passive, DungeonRank.D, new[] { trap01 }, desc: "Trap detonation heavily slows adventurers and makes them take increased damage from monsters.");
            trapSeal.Initialize("DM_CONTROL_SEAL", "Dungeon Seal", 40, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D, new[] { trap01 }, desc: "Auto-Active: When adventurers breach the boss room, automatically seals the entrance door after 5s and blocks adventurer retreat until the encounter resolves.");
            trap04.Initialize("DM_TRAP_04", "Chain Reaction", 55, TalentPillar.TrapControl, TalentTier.Tier3, TalentType.Passive, DungeonRank.C, new[] { trap02, trap03 }, TalentPrerequisiteLogic.Any, desc: "Traps triggering near other hazards trigger adjacent traps in a devastating cascade.");
            trap05.Initialize("DM_TRAP_05", "Abyssal Barricade", 60, TalentPillar.TrapControl, TalentTier.Tier3, TalentType.ActivePower, DungeonRank.C, new[] { trap02, trap03 }, activeCost: 15, cooldown: 60f, desc: "Active Power: Erects an indestructible shadow barrier forcing adventurers into trap corridors.");

            boss01.Initialize("DM_BOSS_01", "Colossus Vessel", 20, TalentPillar.ApexBoss, TalentTier.Tier1, TalentType.Passive, DungeonRank.E, new[] { core01 }, desc: "Unlocks placement of Boss-tier entities. Bosses gain +30% Max Health and armor penetration.");
            boss02.Initialize("DM_BOSS_02", "Tyrant's Presence", 35, TalentPillar.ApexBoss, TalentTier.Tier2, TalentType.Aura, DungeonRank.D, new[] { boss01 }, desc: "The Boss emits a Dread Aura, reducing enemy attack speed and disrupting hero utility spells.");
            boss03.Initialize("DM_BOSS_03", "Blood Frenzy Domain", 35, TalentPillar.ApexBoss, TalentTier.Tier2, TalentType.Passive, DungeonRank.D, new[] { boss01 }, desc: "When the Boss drops below 50% HP, enters Enraged Titan state with massive damage and CC immunity.");
            boss04.Initialize("DM_BOSS_04", "Cataclysm Protocol", 55, TalentPillar.ApexBoss, TalentTier.Tier3, TalentType.ActivePower, DungeonRank.C, new[] { boss02, boss03 }, TalentPrerequisiteLogic.Any, 20, 30f, desc: "Active Power: Overcharges the Boss with essence to trigger an arena-wide stun shockwave.");
            boss05.Initialize("DM_BOSS_05", "Soul Conduit", 60, TalentPillar.ApexBoss, TalentTier.Tier3, TalentType.Passive, DungeonRank.C, new[] { boss02, boss03 }, desc: "Infuses the Boss with DM Artifact energy, granting damage and protection per active relic.");

            hyb01.Initialize("DM_HYBRID_01", "Grave Mine Spores", 80, TalentPillar.Hybrid, TalentTier.Tier4, TalentType.Passive, DungeonRank.B, new[] { swarm04, trap04 }, desc: "Monster deaths seed spore traps that detonate on enemy contact and spawn mini-spores.");
            hyb02.Initialize("DM_HYBRID_02", "Fortress Dreadnought", 80, TalentPillar.Hybrid, TalentTier.Tier4, TalentType.Passive, DungeonRank.B, new[] { trap04, boss04 }, desc: "Traps beam damage reduction to the Boss and trap triggers heal the Boss.");
            hyb03.Initialize("DM_HYBRID_03", "Legion Sovereign", 80, TalentPillar.Hybrid, TalentTier.Tier4, TalentType.Passive, DungeonRank.B, new[] { swarm04, boss04 }, desc: "Minions near the Boss gain high crit chance and can sacrifice themselves to absorb lethal boss damage.");
            asc01.Initialize("DM_ASCENDANT_01", "Dungeon Omnipotence", 100, TalentPillar.Transcendent, TalentTier.Tier4, TalentType.ActivePower, DungeonRank.A, new[] { swarm05, trap05, boss05 }, activeCost: 25, cooldown: 90f, desc: "Active Power: Boosts dungeon defense power and planning allowance. Active cast executes a hero.");

            // Mark nodes dirty
            foreach (var node in talentMap.Values)
            {
                EditorUtility.SetDirty(node);
            }

            // Create or update DefaultTalentTreeConfig
            var config = AssetDatabase.LoadAssetAtPath<TalentTreeConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<TalentTreeConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.Initialize("Dungeon Master Talent Tree", talentMap.Values);
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool valid = config.ValidateTree(out var errors);
            if (valid)
            {
                Debug.Log($"[DMTalentTreeGenerator] Successfully created {talentMap.Count} TalentNodeSO assets and {ConfigPath} (Tree Valid: True)!");
            }
            else
            {
                Debug.LogError($"[DMTalentTreeGenerator] Tree validation failed with errors: {string.Join("; ", errors)}");
            }
        }

        private static TalentNodeSO CreateOrUpdateNode(
            string id,
            string name,
            int cost,
            TalentPillar pillar,
            TalentTier tier,
            DungeonRank minRank,
            string desc,
            TalentType type = TalentType.Passive,
            int activeCost = 0,
            float cd = 0f,
            TalentPrerequisiteLogic prereqLogic = TalentPrerequisiteLogic.All)
        {
            string path = $"{TalentsFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TalentNodeSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TalentNodeSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Initialize(
                id: id,
                name: name,
                cost: cost,
                pillar: pillar,
                tier: tier,
                type: type,
                minRank: minRank,
                prereqs: null,
                prereqLogic: prereqLogic,
                activeCost: activeCost,
                cooldown: cd,
                playerModBonus: 0f,
                synergyBonus: 0,
                desc: desc
            );

            return asset;
        }

        [MenuItem("Tools/SBGames/Generate DM Seal Talent Node", priority = 101)]
        public static TalentNodeSO GenerateDMSealTalentNode()
        {
            EnsureDirectories();
            var node = CreateOrUpdateNode(
                id: "DM_CONTROL_SEAL",
                name: "Dungeon Seal",
                cost: 40,
                pillar: TalentPillar.TrapControl,
                tier: TalentTier.Tier2,
                minRank: DungeonRank.D,
                desc: "Auto-Active: When adventurers breach the boss room, automatically seals the entrance door after 5s and blocks adventurer retreat until the encounter resolves.",
                type: TalentType.Triggered
            );
            var trap01Path = $"{TalentsFolder}/DM_TRAP_01.asset";
            var trap01 = AssetDatabase.LoadAssetAtPath<TalentNodeSO>(trap01Path);
            if (trap01 != null)
            {
                node.Initialize("DM_CONTROL_SEAL", "Dungeon Seal", 40, TalentPillar.TrapControl, TalentTier.Tier2, TalentType.Triggered, DungeonRank.D, new[] { trap01 }, desc: "Auto-Active: When adventurers breach the boss room, automatically seals the entrance door after 5s and blocks adventurer retreat until the encounter resolves.");
            }
            EditorUtility.SetDirty(node);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DMTalentTreeGenerator] Generated DM Seal Talent Node at {TalentsFolder}/DM_CONTROL_SEAL.asset");
            return node;
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(ResourceFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(TalentsFolder))
            {
                AssetDatabase.CreateFolder(ResourceFolder, "Talents");
            }
        }
    }
}
