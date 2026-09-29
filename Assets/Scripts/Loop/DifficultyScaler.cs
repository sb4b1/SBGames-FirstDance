using System;
using System.Collections.Generic;
using System.Linq;
using ArtificeToolkit.Attributes;
using SBGames.Inventory.Stats;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Encapsulates the comprehensive outcome of a dungeon difficulty evaluation,
    /// detailing raw monster combat power, player/DM modifier contributions,
    /// and the final challenge tier.
    /// </summary>
    [Serializable]
    public struct DifficultyEvaluation
    {
        [Tooltip("Total unscaled Combat Power sum of all monsters placed in the dungeon.")]
        public int RawMonsterCombatPower;

        [Tooltip("Number of monsters evaluated.")]
        public int MonsterCount;

        [Tooltip("Target dungeon/gate rank evaluated.")]
        public DungeonRank EvaluatedRank;

        [Tooltip("Scaling multiplier corresponding to the dungeon rank.")]
        public float RankMultiplier;

        [Tooltip("Effective player modifier factor (e.g. passive tree, buffs, relics).")]
        public float PlayerModifier;

        [Tooltip("Bonus defense power granted by Dungeon Master artifacts and essence state.")]
        public int DmSynergyPowerBonus;

        [Tooltip("Total scaled Dungeon Defense Power rating.")]
        public int TotalDungeonDefensePower;

        [Tooltip("Expected baseline power rating of an adventurer party at this rank.")]
        public int ScaledAdventurerEncounterRating;

        [Tooltip("Ratio of Total Dungeon Defense Power to Adventurer Encounter Rating.")]
        public float DifficultyBalanceRatio;

        [Tooltip("Categorical challenge tier assessment.")]
        public DifficultyTier ChallengeTier;

        [Tooltip("Recommended adventurer rank tier required to challenge this setup.")]
        public DungeonRank RecommendedAdventurerRank;

        [Tooltip("Projected DM Essence damage if the defense fails.")]
        public int EstimatedEssenceRisk;

        public override string ToString()
        {
            return $"[Difficulty Tier: {ChallengeTier}] Defense Power: {TotalDungeonDefensePower} vs Adventurer Rating: {ScaledAdventurerEncounterRating} " +
                   $"(Ratio: {DifficultyBalanceRatio:F2}, Monsters: {MonsterCount}, Raw CP: {RawMonsterCombatPower}, DM Bonus: +{DmSynergyPowerBonus})";
        }
    }

    /// <summary>
    /// Calculates dungeon difficulty and combat power scaling by aggregating monster attributes
    /// (aligned with <see cref="CharacterCombatPower"/>), applying dungeon rank multipliers,
    /// and incorporating player/Dungeon Master power enhancements.
    /// </summary>
    [DisallowMultipleComponent]
    public class DifficultyScaler : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Optional ScriptableObject configuration asset. If null, built-in baseline values are used.")]
        [SerializeField] private DifficultyScalingConfigSO customConfig;

        [Header("Dependencies (Optional)")]
        [Tooltip("Reference to DungeonMaster for live artifact and essence synergy evaluation.")]
        [SerializeField] private DungeonMaster dungeonMaster;

        [Tooltip("Reference to DungeonProgression for active gate rank query.")]
        [SerializeField] private DungeonProgression dungeonProgression;

        [Header("Runtime Preview (Read Only)")]
        [SerializeField, ReadOnly] private int lastTotalDefensePower;
        [SerializeField, ReadOnly] private float lastBalanceRatio;
        [SerializeField, ReadOnly] private DifficultyTier lastEvaluatedTier = DifficultyTier.Balanced;
        [SerializeField, ReadOnly] private DungeonRank lastRecommendedRank = DungeonRank.E;

        /// <summary>Active configuration asset or null.</summary>
        public DifficultyScalingConfigSO Config => customConfig;

        /// <summary>Fired when a difficulty evaluation is calculated.</summary>
        public event Action<DifficultyEvaluation> OnDifficultyEvaluated;

        private void Awake()
        {
            if (dungeonMaster == null)
            {
                dungeonMaster = FindAnyObjectByType<DungeonMaster>();
            }
            if (dungeonProgression == null)
            {
                dungeonProgression = FindAnyObjectByType<DungeonProgression>();
            }
        }

        /// <summary>
        /// Programmatic initialization for runtime setups and headless unit testing.
        /// </summary>
        public void Initialize(
            DifficultyScalingConfigSO config = null,
            DungeonMaster dm = null,
            DungeonProgression progression = null)
        {
            customConfig = config;
            dungeonMaster = dm;
            dungeonProgression = progression;
        }

        #region Core Calculation Engine (Pure Static Methods)

        /// <summary>
        /// Calculates the individual Combat Power for a single monster based on its stats,
        /// using weighting directly aligned with <see cref="CharacterCombatPower"/>.
        /// </summary>
        /// <param name="monster">The monster stats to evaluate.</param>
        /// <param name="config">Optional configuration asset.</param>
        /// <returns>Integer Combat Power score (minimum 0).</returns>
        public static int CalculateMonsterCombatPower(IMonsterStats monster, DifficultyScalingConfigSO config = null)
        {
            if (monster == null) return 0;

            int basePwr = config != null ? config.BaseMonsterPower : 100;
            int pwrPerLvl = config != null ? config.PowerPerLevel : 25;
            int pwrPerMod = config != null ? config.PowerPerModifier : 15;
            float artMult = config != null ? config.MonsterArtifactPowerMultiplier : 1.0f;

            float hpWeight = config != null ? config.MaxHealthWeight : 0.5f;
            float dmgWeight = config != null ? config.DamageWeight : 3.0f;
            float atkSpdWeight = config != null ? config.AttackSpeedWeight : 10.0f;
            float defWeight = config != null ? config.DefenseWeight : 2.5f;
            float spdWeight = config != null ? config.MoveSpeedWeight : 2.0f;

            // 1. Level Contribution
            int levelPower = basePwr + (Mathf.Max(1, monster.Level) * pwrPerLvl);

            // 2. Stat Points Contribution
            float statPoints = 0f;
            statPoints += Mathf.Max(0f, monster.MaxHealth) * hpWeight;
            statPoints += Mathf.Max(0f, monster.Damage) * dmgWeight;

            if (monster.AttackInterval > 0.001f)
            {
                float attacksPerSec = 1.0f / monster.AttackInterval;
                statPoints += attacksPerSec * atkSpdWeight;
            }

            statPoints += Mathf.Max(0f, monster.Defense) * defWeight;
            statPoints += Mathf.Max(0f, monster.MoveSpeed) * spdWeight;

            int statPower = Mathf.Max(0, Mathf.FloorToInt(statPoints));

            // 3. Modifiers & Artifact Contribution
            int modifierPower = Mathf.Max(0, monster.ActiveModifierCount) * pwrPerMod;
            int artifactPower = Mathf.Max(0, Mathf.FloorToInt(monster.EquippedArtifactPower * artMult));

            // 4. Total Monster Power
            return Mathf.Max(0, levelPower + statPower + modifierPower + artifactPower);
        }

        /// <summary>
        /// Calculates the total difficulty evaluation for a collection of monsters in a dungeon.
        /// </summary>
        /// <param name="rank">The target dungeon rank.</param>
        /// <param name="monsters">Collection of monster stats present in the dungeon.</param>
        /// <param name="playerModifier">External player/DM power modifier multiplier (default 1.0).</param>
        /// <param name="dm">Optional Dungeon Master reference to factor in artifact power and essence resilience.</param>
        /// <param name="config">Optional configuration asset.</param>
        /// <returns>A comprehensive <see cref="DifficultyEvaluation"/> record.</returns>
        public static DifficultyEvaluation CalculateDifficulty(
            DungeonRank rank,
            IEnumerable<IMonsterStats> monsters,
            float playerModifier = 1.0f,
            DungeonMaster dm = null,
            DifficultyScalingConfigSO config = null)
        {
            float safePlayerMod = Mathf.Max(0.01f, playerModifier);
            float rankMultiplier = config != null ? config.GetRankMultiplier(rank) : GetDefaultRankMultiplier(rank);

            // 1. Aggregate Monster Power
            int rawMonsterPower = 0;
            int monsterCount = 0;

            if (monsters != null)
            {
                foreach (var m in monsters)
                {
                    if (m != null)
                    {
                        rawMonsterPower += CalculateMonsterCombatPower(m, config);
                        monsterCount++;
                    }
                }
            }

            // 2. DM Synergy Bonus (Artifact power levels + Essence resilience)
            int dmSynergyBonus = 0;
            if (dm != null)
            {
                float powerPerArtifactLvl = config != null ? config.PowerPerDmArtifactLevel : 35.0f;
                float essenceResilienceWeight = config != null ? config.DmEssenceResilienceWeight : 0.20f;

                int totalArtifactPowerLevel = 0;
                if (dm.Artifacts != null)
                {
                    for (int i = 0; i < dm.Artifacts.Count; i++)
                    {
                        var art = dm.Artifacts[i];
                        if (art != null)
                        {
                            totalArtifactPowerLevel += art.PowerLevel;
                        }
                    }
                }

                float essenceRatio = dm.MaxEssence > 0 ? (float)dm.Essence / dm.MaxEssence : 0f;
                float essenceBonus = rawMonsterPower * (essenceRatio * essenceResilienceWeight);
                float artifactBonus = totalArtifactPowerLevel * powerPerArtifactLvl;

                dmSynergyBonus = Mathf.Max(0, Mathf.RoundToInt(artifactBonus + essenceBonus));
            }

            // 3. Total Dungeon Defense Power
            int scaledMonsterDefense = Mathf.RoundToInt(rawMonsterPower * rankMultiplier * safePlayerMod);
            int totalDefensePower = Mathf.Max(0, scaledMonsterDefense + dmSynergyBonus);

            // 4. Scaled Adventurer Encounter Rating Baseline
            int baseAdventurerRating = rank.GetBasePowerBaseline();
            int scaledAdventurerRating = Mathf.Max(1, Mathf.RoundToInt(baseAdventurerRating * rankMultiplier));

            // 5. Difficulty Balance Ratio & Tier
            float balanceRatio = scaledAdventurerRating > 0 ? (float)totalDefensePower / scaledAdventurerRating : 1.0f;
            DifficultyTier tier = config != null ? config.EvaluateTier(balanceRatio) : EvaluateDefaultTier(balanceRatio);

            // 6. Recommended Adventurer Rank
            DungeonRank recommendedRank = DetermineRecommendedRank(totalDefensePower);

            // 7. Estimated Essence Risk
            int baseEssencePenalty = dm != null ? dm.RoundLossEssencePenalty : 25;
            int essenceRisk = CalculateEstimatedEssenceRisk(tier, baseEssencePenalty);

            return new DifficultyEvaluation
            {
                RawMonsterCombatPower = rawMonsterPower,
                MonsterCount = monsterCount,
                EvaluatedRank = rank,
                RankMultiplier = rankMultiplier,
                PlayerModifier = safePlayerMod,
                DmSynergyPowerBonus = dmSynergyBonus,
                TotalDungeonDefensePower = totalDefensePower,
                ScaledAdventurerEncounterRating = scaledAdventurerRating,
                DifficultyBalanceRatio = balanceRatio,
                ChallengeTier = tier,
                RecommendedAdventurerRank = recommendedRank,
                EstimatedEssenceRisk = essenceRisk
            };
        }

        /// <summary>
        /// Overload accepting GameObjects with <see cref="CharacterCombatPower"/> or <see cref="CharacterStatsManager"/> components.
        /// </summary>
        public static DifficultyEvaluation CalculateDifficulty(
            DungeonRank rank,
            IEnumerable<GameObject> monsterGameObjects,
            float playerModifier = 1.0f,
            DungeonMaster dm = null,
            DifficultyScalingConfigSO config = null)
        {
            var monsterList = new List<IMonsterStats>();
            if (monsterGameObjects != null)
            {
                foreach (var go in monsterGameObjects)
                {
                    if (go != null)
                    {
                        monsterList.Add(MonsterStats.FromGameObject(go));
                    }
                }
            }

            return CalculateDifficulty(rank, monsterList, playerModifier, dm, config);
        }

        /// <summary>
        /// Overload accepting <see cref="CharacterCombatPower"/> components directly.
        /// </summary>
        public static DifficultyEvaluation CalculateDifficulty(
            DungeonRank rank,
            IEnumerable<CharacterCombatPower> combatPowers,
            float playerModifier = 1.0f,
            DungeonMaster dm = null,
            DifficultyScalingConfigSO config = null)
        {
            var monsterList = new List<IMonsterStats>();
            if (combatPowers != null)
            {
                foreach (var ccp in combatPowers)
                {
                    if (ccp != null)
                    {
                        monsterList.Add(MonsterStats.FromCharacterCombatPower(ccp));
                    }
                }
            }

            return CalculateDifficulty(rank, monsterList, playerModifier, dm, config);
        }

        #endregion

        #region Public Instance Methods

        /// <summary>
        /// Evaluates current difficulty using the active instance's references and configuration.
        /// </summary>
        /// <param name="monsters">Monsters currently defending the dungeon.</param>
        /// <param name="playerModifier">Optional additional player modifier (default 1.0).</param>
        /// <returns>A comprehensive <see cref="DifficultyEvaluation"/> record.</returns>
        public DifficultyEvaluation EvaluateDifficulty(IEnumerable<IMonsterStats> monsters, float playerModifier = 1.0f)
        {
            var rank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            var eval = CalculateDifficulty(rank, monsters, playerModifier, dungeonMaster, customConfig);

            lastTotalDefensePower = eval.TotalDungeonDefensePower;
            lastBalanceRatio = eval.DifficultyBalanceRatio;
            lastEvaluatedTier = eval.ChallengeTier;
            lastRecommendedRank = eval.RecommendedAdventurerRank;

            OnDifficultyEvaluated?.Invoke(eval);
            return eval;
        }

        /// <summary>
        /// Evaluates current difficulty from GameObject monster instances.
        /// </summary>
        public DifficultyEvaluation EvaluateDifficulty(IEnumerable<GameObject> monsterObjects, float playerModifier = 1.0f)
        {
            var rank = dungeonProgression != null ? dungeonProgression.CurrentRank : DungeonRank.E;
            var eval = CalculateDifficulty(rank, monsterObjects, playerModifier, dungeonMaster, customConfig);

            lastTotalDefensePower = eval.TotalDungeonDefensePower;
            lastBalanceRatio = eval.DifficultyBalanceRatio;
            lastEvaluatedTier = eval.ChallengeTier;
            lastRecommendedRank = eval.RecommendedAdventurerRank;

            OnDifficultyEvaluated?.Invoke(eval);
            return eval;
        }

        #endregion

        #region Helper Fallbacks

        private static float GetDefaultRankMultiplier(DungeonRank rank)
        {
            return rank switch
            {
                DungeonRank.E => 1.0f,
                DungeonRank.D => 1.4f,
                DungeonRank.C => 2.0f,
                DungeonRank.B => 3.0f,
                DungeonRank.A => 4.5f,
                DungeonRank.S => 7.0f,
                _ => 1.0f
            };
        }

        private static DifficultyTier EvaluateDefaultTier(float balanceRatio)
        {
            if (balanceRatio >= 1.6f) return DifficultyTier.Trivial;
            if (balanceRatio >= 0.95f) return DifficultyTier.Balanced;
            if (balanceRatio >= 0.65f) return DifficultyTier.Challenging;
            if (balanceRatio >= 0.35f) return DifficultyTier.Overwhelming;
            return DifficultyTier.Lethal;
        }

        private static DungeonRank DetermineRecommendedRank(int totalDefensePower)
        {
            for (int r = (int)DungeonRankExtensions.MaxRank; r >= 0; r--)
            {
                var rank = (DungeonRank)r;
                if (totalDefensePower >= rank.GetBasePowerBaseline())
                {
                    return rank;
                }
            }
            return DungeonRankExtensions.MinRank;
        }

        private static int CalculateEstimatedEssenceRisk(DifficultyTier tier, int baseEssencePenalty)
        {
            return tier switch
            {
                DifficultyTier.Trivial => Mathf.RoundToInt(baseEssencePenalty * 0.5f),
                DifficultyTier.Balanced => baseEssencePenalty,
                DifficultyTier.Challenging => Mathf.RoundToInt(baseEssencePenalty * 1.3f),
                DifficultyTier.Overwhelming => Mathf.RoundToInt(baseEssencePenalty * 1.8f),
                DifficultyTier.Lethal => Mathf.RoundToInt(baseEssencePenalty * 2.5f),
                _ => baseEssencePenalty
            };
        }

        #endregion
    }
}
