using System;
using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using SBGames.Inventory.Character;
using SBGames.Inventory.Core;
using UnityEngine;

namespace SBGames.Inventory.Stats
{
    /// <summary>
    /// Detailed itemized breakdown of a character's Combat Power (CP) calculation.
    /// </summary>
    [Serializable]
    public struct CombatPowerBreakdown
    {
        public int LevelPower;
        public int RunePower;
        public int ArtifactPower;
        public int ModifierPower;
        public int StatPower;
        public int TotalCombatPower;

        public int Level;
        public int InscribedRuneCount;
        public bool HasEquippedArtifact;
        public int FlatModifierCount;
        public int PercentAddModifierCount;
        public int PercentMultModifierCount;
        public int TotalModifierCount;

        public override string ToString()
        {
            return $"Total CP: {TotalCombatPower} [Level: {LevelPower}, Runes({InscribedRuneCount}): {RunePower}, " +
                   $"Artifact({(HasEquippedArtifact ? "Yes" : "No")}): {ArtifactPower}, " +
                   $"Modifiers({TotalModifierCount}): {ModifierPower}, Stats: {StatPower}]";
        }
    }

    /// <summary>
    /// Evaluates and tracks a character's Combat Power (CP) in real-time.
    /// Synthesizes character level, inscribed runes, equipped artifact, active modifiers count,
    /// and effective final stats into a unified combat power score.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsManager))]
    [RequireComponent(typeof(CharacterInventoryManager))]
    [DisallowMultipleComponent]
    public class CharacterCombatPower : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterStatsManager statsManager;
        [SerializeField] private CharacterInventoryManager inventoryManager;

        [Header("Configuration Override (Optional)")]
        [Tooltip("Optional shared ScriptableObject for global balance tuning. If null, fallback fields below are used.")]
        [SerializeField] private CombatPowerConfigSO customConfig;

        [Header("Fallback Baseline & Progression")]
        [SerializeField, Min(0)] private int fallbackBaseCombatPower = 100;
        [SerializeField, Min(0)] private int fallbackPowerPerLevel = 25;

        [Header("Fallback Equipment Tuning")]
        [SerializeField, Min(0)] private int fallbackPowerPerInscribedRune = 15;
        [SerializeField, Min(0)] private int fallbackPowerPerEquippedArtifact = 40;

        [Header("Fallback Modifier Tuning")]
        [SerializeField, Min(0)] private int fallbackPowerPerFlatModifier = 10;
        [SerializeField, Min(0)] private int fallbackPowerPerPercentAddModifier = 15;
        [SerializeField, Min(0)] private int fallbackPowerPerPercentMultModifier = 20;

        [Header("Fallback Stat Weights")]
        [SerializeField, Min(0f)] private float fallbackMaxLifeWeight = 0.5f;
        [SerializeField, Min(0f)] private float fallbackMaxManaWeight = 0.2f;
        [SerializeField, Min(0f)] private float fallbackDamageWeight = 3.0f;
        [SerializeField, Min(0f)] private float fallbackMoveSpeedWeight = 2.0f;
        [SerializeField, Min(0f)] private float fallbackAttackSpeedWeight = 10.0f;

#if UNITY_EDITOR
        [Header("Combat Power Preview")]
        [SerializeField, ReadOnly] private int currentCombatPower;
        [SerializeField, ReadOnly] private int previewLevelPower;
        [SerializeField, ReadOnly] private int previewRunePower;
        [SerializeField, ReadOnly] private int previewArtifactPower;
        [SerializeField, ReadOnly] private int previewModifierPower;
        [SerializeField, ReadOnly] private int previewStatPower;
        [SerializeField, ReadOnly] private int previewInscribedRunes;
        [SerializeField, ReadOnly] private bool previewHasArtifact;
        [SerializeField, ReadOnly] private int previewTotalModifiers;
#else
        private int currentCombatPower;
#endif

        private CombatPowerBreakdown _breakdown;

        public CharacterStatsManager StatsManager => statsManager != null ? statsManager : (statsManager = GetComponent<CharacterStatsManager>());
        public CharacterInventoryManager InventoryManager => inventoryManager != null ? inventoryManager : (inventoryManager = GetComponent<CharacterInventoryManager>());
        public CombatPowerConfigSO Config => customConfig;

        public int CurrentCombatPower => currentCombatPower;
        public CombatPowerBreakdown CurrentBreakdown => _breakdown;
        public int Level => StatsManager != null ? StatsManager.Level : 1;
        public int InscribedRuneCount => _breakdown.InscribedRuneCount;
        public bool HasEquippedArtifact => _breakdown.HasEquippedArtifact;
        public int TotalActiveModifiers => _breakdown.TotalModifierCount;

        /// <summary>
        /// Fired when combat power changes, providing both the new total CP and the complete itemized breakdown.
        /// </summary>
        public event Action<int, CombatPowerBreakdown> OnCombatPowerChanged;

        /// <summary>
        /// Fired when combat power changes, providing the integer total CP.
        /// </summary>
        public event Action<int> OnCombatPowerValueChanged;

        private bool _isSubscribed;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize(CharacterStatsManager customStats = null, CharacterInventoryManager customInv = null, CombatPowerConfigSO config = null)
        {
            if (customStats != null) statsManager = customStats;
            if (customInv != null) inventoryManager = customInv;
            if (config != null) customConfig = config;

            if (statsManager == null) statsManager = GetComponent<CharacterStatsManager>();
            if (inventoryManager == null) inventoryManager = GetComponent<CharacterInventoryManager>();

            if (inventoryManager != null)
            {
                inventoryManager.InitializeInventory();
            }

            SubscribeEvents();
            RecalculateCombatPower();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            RecalculateCombatPower();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            RecalculateCombatPower();
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;

            if (StatsManager != null)
            {
                StatsManager.OnLevelChanged += HandleLevelChanged;
            }

            if (InventoryManager != null)
            {
                InventoryManager.OnEquipmentOrStatsChanged += HandleEquipmentOrStatsChanged;
            }

            _isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;

            if (statsManager != null)
            {
                statsManager.OnLevelChanged -= HandleLevelChanged;
            }

            if (inventoryManager != null)
            {
                inventoryManager.OnEquipmentOrStatsChanged -= HandleEquipmentOrStatsChanged;
            }

            _isSubscribed = false;
        }

        private void HandleLevelChanged(int newLevel)
        {
            RecalculateCombatPower();
        }

        private void HandleEquipmentOrStatsChanged()
        {
            RecalculateCombatPower();
        }

        /// <summary>
        /// Manually sets the character's level via CharacterStatsManager.
        /// </summary>
        public void SetLevel(int newLevel)
        {
            StatsManager?.SetLevel(newLevel);
        }

        /// <summary>
        /// Assigns a custom configuration asset dynamically at runtime.
        /// </summary>
        public void SetConfig(CombatPowerConfigSO config)
        {
            customConfig = config;
            RecalculateCombatPower();
        }

        /// <summary>
        /// Evaluates and recalculates the character's Combat Power.
        /// </summary>
        /// <returns>The newly calculated total Combat Power score.</returns>
        public int RecalculateCombatPower()
        {
            if (StatsManager == null) return 0;

            int charLevel = StatsManager.Level;

            // 1. Resolve configuration weights (custom SO or component fallbacks)
            int baseCP = customConfig != null ? customConfig.BaseCombatPower : fallbackBaseCombatPower;
            int powerPerLvl = customConfig != null ? customConfig.PowerPerLevel : fallbackPowerPerLevel;
            int powerPerRune = customConfig != null ? customConfig.PowerPerInscribedRune : fallbackPowerPerInscribedRune;
            int powerPerArtifact = customConfig != null ? customConfig.PowerPerEquippedArtifact : fallbackPowerPerEquippedArtifact;
            int powerFlatMod = customConfig != null ? customConfig.PowerPerFlatModifier : fallbackPowerPerFlatModifier;
            int powerPercentAddMod = customConfig != null ? customConfig.PowerPerPercentAddModifier : fallbackPowerPerPercentAddModifier;
            int powerPercentMultMod = customConfig != null ? customConfig.PowerPerPercentMultModifier : fallbackPowerPerPercentMultModifier;

            float maxHpWeight = customConfig != null ? customConfig.MaxLifeWeight : fallbackMaxLifeWeight;
            float maxManaWeight = customConfig != null ? customConfig.MaxManaWeight : fallbackMaxManaWeight;
            float dmgWeight = customConfig != null ? customConfig.DamageWeight : fallbackDamageWeight;
            float speedWeight = customConfig != null ? customConfig.MoveSpeedWeight : fallbackMoveSpeedWeight;
            float attackSpdWeight = customConfig != null ? customConfig.AttackSpeedWeight : fallbackAttackSpeedWeight;

            // 2. Level Calculation
            int levelPower = baseCP + (charLevel * powerPerLvl);

            // 3. Equipment Calculation (Inscribed Runes & Equipped Artifact)
            int runeCount = 0;
            bool hasArtifact = false;

            if (InventoryManager != null && InventoryManager.Inventory != null)
            {
                var slots = InventoryManager.Inventory.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot != null && slot.IsOccupied && slot.EquippedItem != null)
                    {
                        if (slot.SlotType == EquipmentSlotType.Rune || slot.EquippedItem.ItemType == ItemType.Rune)
                        {
                            runeCount++;
                        }
                        else if (slot.SlotType == EquipmentSlotType.Artifact || slot.EquippedItem.ItemType == ItemType.Artifact)
                        {
                            hasArtifact = true;
                        }
                    }
                }
            }

            int runePower = runeCount * powerPerRune;
            int artifactPower = hasArtifact ? powerPerArtifact : 0;

            // 4. Modifier Count Calculation (Count all active StatModifiers across all character stats)
            int flatModCount = 0;
            int percentAddModCount = 0;
            int percentMultModCount = 0;

            void CountModifiersOnStat(Stat stat)
            {
                if (stat == null || stat.Modifiers == null) return;
                var mods = stat.Modifiers;
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = mods[i];
                    switch (m.Type)
                    {
                        case StatModType.Flat:
                            flatModCount++;
                            break;
                        case StatModType.PercentAdd:
                            percentAddModCount++;
                            break;
                        case StatModType.PercentMult:
                            percentMultModCount++;
                            break;
                    }
                }
            }

            CountModifiersOnStat(StatsManager.MaxLife);
            CountModifiersOnStat(StatsManager.MaxMana);
            CountModifiersOnStat(StatsManager.Movespeed);
            CountModifiersOnStat(StatsManager.Damage);
            CountModifiersOnStat(StatsManager.AttackRange);
            CountModifiersOnStat(StatsManager.AttackTimeInterval);

            // Also inspect supplementary stats registered via extensions
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                if (statType == StatType.Health || statType == StatType.MaxHealth ||
                    statType == StatType.Attack || statType == StatType.MoveSpeed ||
                    statType == StatType.AttackSpeed)
                {
                    continue; // Already counted primary stats above
                }

                var extraStat = StatsManager.GetStat(statType);
                if (extraStat != null)
                {
                    CountModifiersOnStat(extraStat);
                }
            }

            int totalModCount = flatModCount + percentAddModCount + percentMultModCount;
            int modifierPower = (flatModCount * powerFlatMod) +
                                (percentAddModCount * powerPercentAddMod) +
                                (percentMultModCount * powerPercentMultMod);

            // 5. Stat Values Contribution
            float statPoints = 0f;

            if (StatsManager.MaxLife != null)
                statPoints += StatsManager.MaxLife.Value * maxHpWeight;

            if (StatsManager.MaxMana != null)
                statPoints += StatsManager.MaxMana.Value * maxManaWeight;

            if (StatsManager.Damage != null)
                statPoints += StatsManager.Damage.Value * dmgWeight;

            if (StatsManager.Movespeed != null)
                statPoints += StatsManager.Movespeed.Value * speedWeight;

            if (StatsManager.AttackTimeInterval != null && StatsManager.AttackTimeInterval.Value > 0.001f)
            {
                // Attacks per second = 1.0f / interval
                float attacksPerSec = 1.0f / StatsManager.AttackTimeInterval.Value;
                statPoints += attacksPerSec * attackSpdWeight;
            }

            // Supplementary stats scoring
            foreach (StatType statType in Enum.GetValues(typeof(StatType)))
            {
                if (statType == StatType.Health || statType == StatType.MaxHealth ||
                    statType == StatType.Attack || statType == StatType.MoveSpeed ||
                    statType == StatType.AttackSpeed)
                {
                    continue;
                }

                float weight = customConfig != null ? customConfig.GetStatWeight(statType) : 1.0f;
                statPoints += StatsManager.GetStatValue(statType) * weight;
            }

            int statPower = Mathf.Max(0, Mathf.FloorToInt(statPoints));

            // 6. Total Combat Power
            int totalCP = Mathf.Max(0, levelPower + runePower + artifactPower + modifierPower + statPower);

            _breakdown = new CombatPowerBreakdown
            {
                LevelPower = levelPower,
                RunePower = runePower,
                ArtifactPower = artifactPower,
                ModifierPower = modifierPower,
                StatPower = statPower,
                TotalCombatPower = totalCP,
                Level = charLevel,
                InscribedRuneCount = runeCount,
                HasEquippedArtifact = hasArtifact,
                FlatModifierCount = flatModCount,
                PercentAddModifierCount = percentAddModCount,
                PercentMultModifierCount = percentMultModCount,
                TotalModifierCount = totalModCount
            };

            currentCombatPower = totalCP;

#if UNITY_EDITOR
            previewLevelPower = levelPower;
            previewRunePower = runePower;
            previewArtifactPower = artifactPower;
            previewModifierPower = modifierPower;
            previewStatPower = statPower;
            previewInscribedRunes = runeCount;
            previewHasArtifact = hasArtifact;
            previewTotalModifiers = totalModCount;
#endif

            OnCombatPowerChanged?.Invoke(currentCombatPower, _breakdown);
            OnCombatPowerValueChanged?.Invoke(currentCombatPower);

            return currentCombatPower;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (statsManager == null) statsManager = GetComponent<CharacterStatsManager>();
            if (inventoryManager == null) inventoryManager = GetComponent<CharacterInventoryManager>();
        }
#endif
    }
}
