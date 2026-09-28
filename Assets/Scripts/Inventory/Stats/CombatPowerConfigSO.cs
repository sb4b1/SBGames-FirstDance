using System;
using UnityEngine;

namespace SBGames.Inventory.Stats
{
    /// <summary>
    /// ScriptableObject defining tuning weights and base values for calculating a character's Combat Power (CP).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatPowerConfig", menuName = "SBGames/Stats/Combat Power Config", order = 20)]
    public class CombatPowerConfigSO : ScriptableObject
    {
        [Header("Baseline & Progression")]
        [Tooltip("Base baseline combat power granted to any valid character.")]
        [SerializeField, Min(0)] private int baseCombatPower = 100;

        [Tooltip("Combat power granted per character level.")]
        [SerializeField, Min(0)] private int powerPerLevel = 25;

        [Header("Equipment Contribution")]
        [Tooltip("Base combat power granted for each inscribed rune in rune slots.")]
        [SerializeField, Min(0)] private int powerPerInscribedRune = 15;

        [Tooltip("Base combat power granted for having an artifact equipped.")]
        [SerializeField, Min(0)] private int powerPerEquippedArtifact = 40;

        [Header("Modifier Contribution")]
        [Tooltip("Combat power granted per active Flat StatModifier on the character.")]
        [SerializeField, Min(0)] private int powerPerFlatModifier = 10;

        [Tooltip("Combat power granted per active PercentAdd StatModifier on the character.")]
        [SerializeField, Min(0)] private int powerPerPercentAddModifier = 15;

        [Tooltip("Combat power granted per active PercentMult StatModifier on the character.")]
        [SerializeField, Min(0)] private int powerPerPercentMultModifier = 20;

        [Header("Core Stat Weights")]
        [Tooltip("CP weight per unit of Max Health.")]
        [SerializeField, Min(0f)] private float maxLifeWeight = 0.5f;

        [Tooltip("CP weight per unit of Max Mana.")]
        [SerializeField, Min(0f)] private float maxManaWeight = 0.2f;

        [Tooltip("CP weight per unit of Damage/Attack power.")]
        [SerializeField, Min(0f)] private float damageWeight = 3.0f;

        [Tooltip("CP weight per unit of Move Speed.")]
        [SerializeField, Min(0f)] private float moveSpeedWeight = 2.0f;

        [Tooltip("CP weight per attack per second (1 / AttackTimeInterval).")]
        [SerializeField, Min(0f)] private float attackSpeedWeight = 10.0f;

        [Header("Supplementary Stat Weights")]
        [SerializeField, Min(0f)] private float defenseWeight = 2.5f;
        [SerializeField, Min(0f)] private float critChanceWeight = 20.0f;
        [SerializeField, Min(0f)] private float critMultiplierWeight = 15.0f;
        [SerializeField, Min(0f)] private float armorPenetrationWeight = 5.0f;
        [SerializeField, Min(0f)] private float damageOverTimeWeight = 2.0f;

        public int BaseCombatPower => baseCombatPower;
        public int PowerPerLevel => powerPerLevel;
        public int PowerPerInscribedRune => powerPerInscribedRune;
        public int PowerPerEquippedArtifact => powerPerEquippedArtifact;
        public int PowerPerFlatModifier => powerPerFlatModifier;
        public int PowerPerPercentAddModifier => powerPerPercentAddModifier;
        public int PowerPerPercentMultModifier => powerPerPercentMultModifier;

        public float MaxLifeWeight => maxLifeWeight;
        public float MaxManaWeight => maxManaWeight;
        public float DamageWeight => damageWeight;
        public float MoveSpeedWeight => moveSpeedWeight;
        public float AttackSpeedWeight => attackSpeedWeight;

        public float DefenseWeight => defenseWeight;
        public float CritChanceWeight => critChanceWeight;
        public float CritMultiplierWeight => critMultiplierWeight;
        public float ArmorPenetrationWeight => armorPenetrationWeight;
        public float DamageOverTimeWeight => damageOverTimeWeight;

        /// <summary>
        /// Returns the configured weight for a specific StatType enum.
        /// </summary>
        public float GetStatWeight(StatType statType)
        {
            switch (statType)
            {
                case StatType.Health:
                case StatType.MaxHealth:
                    return maxLifeWeight;
                case StatType.Attack:
                    return damageWeight;
                case StatType.MoveSpeed:
                    return moveSpeedWeight;
                case StatType.AttackSpeed:
                    return attackSpeedWeight;
                case StatType.Defense:
                    return defenseWeight;
                case StatType.CritChance:
                    return critChanceWeight;
                case StatType.CritMultiplier:
                    return critMultiplierWeight;
                case StatType.ArmorPenetration:
                    return armorPenetrationWeight;
                case StatType.DamageOverTime:
                    return damageOverTimeWeight;
                default:
                    return 1.0f;
            }
        }
    }
}
