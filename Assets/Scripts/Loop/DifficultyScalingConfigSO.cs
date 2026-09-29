using System;
using UnityEngine;

namespace SBGames.Loop
{
    /// <summary>
    /// Categorical challenge assessment based on the ratio between Dungeon Defense Power and Adventurer Encounter Rating.
    /// </summary>
    public enum DifficultyTier
    {
        /// <summary>Dungeon defenses vastly outmatch the adventurers; guaranteed victory.</summary>
        Trivial = 0,
        /// <summary>Dungeon defenses are well-matched with the adventurer party; balanced contest.</summary>
        Balanced = 1,
        /// <summary>Adventurers pose a serious threat to defenses; tactical placement required.</summary>
        Challenging = 2,
        /// <summary>Adventurers significantly outpower current defenses; high likelihood of defense breach.</summary>
        Overwhelming = 3,
        /// <summary>Extreme danger; adventurers will almost certainly breach defenses and deal heavy essence damage.</summary>
        Lethal = 4
    }

    /// <summary>
    /// Configuration ScriptableObject defining monster stat weights, rank scaling multipliers,
    /// Dungeon Master artifact/essence synergy weights, and difficulty tier thresholds.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyScalingConfig", menuName = "SBGames/Progression/Difficulty Scaling Config", order = 26)]
    public class DifficultyScalingConfigSO : ScriptableObject
    {
        [Header("Baseline & Level Power (Aligned with CharacterCombatPower)")]
        [Tooltip("Base combat power baseline granted to any monster.")]
        [SerializeField, Min(0)] private int baseMonsterPower = 100;

        [Tooltip("Combat power granted per monster level.")]
        [SerializeField, Min(0)] private int powerPerLevel = 25;

        [Tooltip("Combat power granted per active modifier on a monster.")]
        [SerializeField, Min(0)] private int powerPerModifier = 15;

        [Tooltip("Multiplier applied to a monster's equipped artifact power.")]
        [SerializeField, Min(0f)] private float monsterArtifactPowerMultiplier = 1.0f;

        [Header("Core Monster Stat Weights")]
        [Tooltip("Power weight per unit of Max Health.")]
        [SerializeField, Min(0f)] private float maxHealthWeight = 0.5f;

        [Tooltip("Power weight per unit of Damage / Attack.")]
        [SerializeField, Min(0f)] private float damageWeight = 3.0f;

        [Tooltip("Power weight per attack per second (1.0 / AttackInterval).")]
        [SerializeField, Min(0f)] private float attackSpeedWeight = 10.0f;

        [Tooltip("Power weight per unit of Defense / Armor.")]
        [SerializeField, Min(0f)] private float defenseWeight = 2.5f;

        [Tooltip("Power weight per unit of Move Speed.")]
        [SerializeField, Min(0f)] private float moveSpeedWeight = 2.0f;

        [Header("Dungeon Rank Scaling Multipliers")]
        [SerializeField, Min(0.1f)] private float rankEMultiplier = 1.0f;
        [SerializeField, Min(0.1f)] private float rankDMultiplier = 1.4f;
        [SerializeField, Min(0.1f)] private float rankCMultiplier = 2.0f;
        [SerializeField, Min(0.1f)] private float rankBMultiplier = 3.0f;
        [SerializeField, Min(0.1f)] private float rankAMultiplier = 4.5f;
        [SerializeField, Min(0.1f)] private float rankSMultiplier = 7.0f;

        [Header("Dungeon Master Synergy & Artifact Power")]
        [Tooltip("Bonus defense power granted per unit of total DM Artifact Power Level.")]
        [SerializeField, Min(0f)] private float powerPerDmArtifactLevel = 35.0f;

        [Tooltip("Weight applied to DM Essence percentage (0.0 to 1.0) as a resilience defense modifier.")]
        [SerializeField, Range(0f, 1f)] private float dmEssenceResilienceWeight = 0.20f;

        [Header("Difficulty Tier Ratio Thresholds (Defense Power / Encounter Rating)")]
        [Tooltip("Ratio above which the dungeon is considered Trivial.")]
        [SerializeField, Min(0.1f)] private float trivialThreshold = 1.6f;

        [Tooltip("Ratio above which the dungeon is considered Balanced (up to Trivial).")]
        [SerializeField, Min(0.1f)] private float balancedThreshold = 0.95f;

        [Tooltip("Ratio above which the dungeon is considered Challenging (up to Balanced).")]
        [SerializeField, Min(0.1f)] private float challengingThreshold = 0.65f;

        [Tooltip("Ratio above which the dungeon is considered Overwhelming (below this is Lethal).")]
        [SerializeField, Min(0.1f)] private float overwhelmingThreshold = 0.35f;

        #region Public Getters

        public int BaseMonsterPower => baseMonsterPower;
        public int PowerPerLevel => powerPerLevel;
        public int PowerPerModifier => powerPerModifier;
        public float MonsterArtifactPowerMultiplier => monsterArtifactPowerMultiplier;

        public float MaxHealthWeight => maxHealthWeight;
        public float DamageWeight => damageWeight;
        public float AttackSpeedWeight => attackSpeedWeight;
        public float DefenseWeight => defenseWeight;
        public float MoveSpeedWeight => moveSpeedWeight;

        public float PowerPerDmArtifactLevel => powerPerDmArtifactLevel;
        public float DmEssenceResilienceWeight => dmEssenceResilienceWeight;

        public float TrivialThreshold => trivialThreshold;
        public float BalancedThreshold => balancedThreshold;
        public float ChallengingThreshold => challengingThreshold;
        public float OverwhelmingThreshold => overwhelmingThreshold;

        #endregion

        /// <summary>
        /// Retrieves the rank multiplier associated with a specific <see cref="DungeonRank"/>.
        /// </summary>
        /// <param name="rank">The target rank.</param>
        /// <returns>Positive scalar scaling factor.</returns>
        public float GetRankMultiplier(DungeonRank rank)
        {
            return rank switch
            {
                DungeonRank.E => rankEMultiplier,
                DungeonRank.D => rankDMultiplier,
                DungeonRank.C => rankCMultiplier,
                DungeonRank.B => rankBMultiplier,
                DungeonRank.A => rankAMultiplier,
                DungeonRank.S => rankSMultiplier,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Evaluates the challenge tier based on the ratio between Defense Power and Encounter Rating.
        /// </summary>
        /// <param name="balanceRatio">Dungeon Defense Power divided by Scaled Adventurer Encounter Rating.</param>
        /// <returns>The evaluated <see cref="DifficultyTier"/>.</returns>
        public DifficultyTier EvaluateTier(float balanceRatio)
        {
            if (balanceRatio >= trivialThreshold) return DifficultyTier.Trivial;
            if (balanceRatio >= balancedThreshold) return DifficultyTier.Balanced;
            if (balanceRatio >= challengingThreshold) return DifficultyTier.Challenging;
            if (balanceRatio >= overwhelmingThreshold) return DifficultyTier.Overwhelming;
            return DifficultyTier.Lethal;
        }
    }
}
